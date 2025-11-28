using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Core;
using UnityEditor;
using UnityEngine;

namespace TimShaw.VoiceBox.Components
{
    public class StreamingAudioDecoder : System.IDisposable
    {
        public bool HasSamples => !_decodedSamples.IsEmpty;

        // Thread-safe queue for Unity's audio thread
        private readonly ConcurrentQueue<float> _decodedSamples = new ConcurrentQueue<float>();

        // Input Streams
        private Stream _inputStream; // Generic stream (was _mp3Stream)
        private WaveStream _waveReader; // Generic reader (was _mp3Reader)
        private MediaFoundationResampler _resampler;

        // Buffers
        private readonly byte[] _conversionBuffer;
        private const int BufferSize = 4096;

        // Unity Settings
        private readonly int _sampleRate = AudioSettings.outputSampleRate;
        private readonly AudioSpeakerMode _speakerMode = AudioSettings.speakerMode;

        public StreamingAudioDecoder()
        {
            _conversionBuffer = new byte[BufferSize];
        }

        /// <summary>
        /// Feed raw file bytes (MP3 or WAV) into the decoder.
        /// </summary>
        /// <param name="fileData">The raw bytes of the file.</param>
        /// <param name="isMp3">True for MP3, False for WAV.</param>
        public void Feed(byte[] fileData, bool isMp3 = true)
        {
            // 1. Setup the stream
            if (_inputStream == null)
            {
                _inputStream = new MemoryStream();
            }

            // Write data to the stream
            long originalPosition = _inputStream.Position;
            _inputStream.Seek(0, SeekOrigin.End);
            _inputStream.Write(fileData, 0, fileData.Length);
            _inputStream.Position = originalPosition;

            // 2. Initialize the Reader if we haven't yet
            if (_waveReader == null && _inputStream.Length > 0)
            {
                try
                {
                    if (isMp3)
                    {
                        _waveReader = new Mp3FileReader(_inputStream);
                    }
                    else
                    {
                        // For WAV, we assume the header is present in the first chunk
                        _waveReader = new WaveFileReader(_inputStream);
                    }

                    InitializeResampler(_waveReader);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error initializing audio reader: {e.Message}");
                    _waveReader = null;
                    return;
                }
            }

            // 3. Process
            if (_resampler != null) ReadAndEnqueueAvailableSamples();
        }

        /// <summary>
        /// Feeds a Unity AudioClip directly into the decoder.
        /// </summary>
        public void Feed(AudioClip clip)
        {
            // 1. Extract Float Data from Unity AudioClip
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            // 2. Convert Float Array to 16-bit PCM Byte Array
            // We do this to maintain compatibility with the existing ReadAndEnqueue logic 
            // which expects Little Endian 16-bit bytes.
            byte[] pcmData = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                // Convert float (-1 to 1) to short (-32768 to 32767)
                short shortSample = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767);

                // Bit manipulation to store as Little Endian bytes
                pcmData[i * 2] = (byte)(shortSample & 0xFF);
                pcmData[i * 2 + 1] = (byte)((shortSample >> 8) & 0xFF);
            }

            // 3. Create a WaveProvider for this PCM data
            // Unity clips are usually PCM, so we create a RawSourceWaveStream
            var format = new WaveFormat(clip.frequency, clip.channels);
            var memoryStream = new MemoryStream(pcmData);

            // Reset previous streams if any
            Reset();

            _inputStream = memoryStream;
            _waveReader = new RawSourceWaveStream(memoryStream, format);

            // 4. Initialize Resampler and Process
            InitializeResampler(_waveReader);
            ReadAndEnqueueAvailableSamples();
        }

        private void InitializeResampler(WaveStream reader)
        {
            int unityChannelCount = (_speakerMode == AudioSpeakerMode.Mono) ? 1 : 2;

            // Create an output format that EXACTLY matches Unity's configuration (16-bit PCM)
            var outputFormat = new WaveFormat(_sampleRate, 16, unityChannelCount);

            _resampler = new MediaFoundationResampler(reader, outputFormat)
            {
                ResamplerQuality = 60 // Optional: Set quality (1-60)
            };
        }

        private void ReadAndEnqueueAvailableSamples()
        {
            if (_resampler == null) return;

            int bytesRead;
            // Keep reading until the resampler buffer is empty
            while ((bytesRead = _resampler.Read(_conversionBuffer, 0, _conversionBuffer.Length)) > 0)
            {
                for (int i = 0; i < bytesRead; i += 2)
                {
                    // Convert 16-bit PCM bytes back to Float for Unity
                    short sample = (short)((_conversionBuffer[i + 1] << 8) | _conversionBuffer[i]);
                    _decodedSamples.Enqueue(sample / 32768.0f);
                }
            }
        }

        public bool TryGetSample(out float sample)
        {
            return _decodedSamples.TryDequeue(out sample);
        }

        /// <summary>
        /// Clears current readers/streams to prepare for a new audio source.
        /// </summary>
        public void Reset()
        {
            _waveReader?.Dispose();
            _inputStream?.Dispose(); // Be careful disposing this if it's shared, but here it's local
            _resampler?.Dispose();

            _waveReader = null;
            _inputStream = null;
            _resampler = null;
        }

        public void Dispose()
        {
            Reset();
        }

    }


        /// <summary>
        /// Manages streaming audio from a WebSocket and playing it through an AudioSource.
        /// It uses a streaming MP3 decoder to handle the audio data.
        /// </summary>
        [RequireComponent(typeof(AudioSource))]
    public class AudioStreamer : MonoBehaviour
    {

        private AudioSource _audioSource;
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cancellationSource;

        private ConcurrentQueue<float> _audioBuffer = new ConcurrentQueue<float>();

        private StreamingAudioDecoder _audioDecoder;

        /// <summary>
        /// Invoked when an audio sample is played during <see cref="OnAudioFilterRead(float[], int)"/>
        /// </summary>
        public EventHandler<float[]> OnAudioSamplePlayed;

#if UNITY_EDITOR
        [MenuItem("GameObject/VoiceBox/Components/Streaming Audio Source", false, 11)]
#endif
        static void CreateAudioStreamerObj()
        {
            var obj = new GameObject("StreamingAudioSource");
            obj.AddComponent<AudioStreamer>();
        }

        /// <summary>
        /// Called when the script instance is being loaded.
        /// </summary>
        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        /// <summary>
        /// Initializes streaming speech for the given text using the specified service.
        /// </summary>
        /// <param name="service">The text-to-speech service to use.</param>
        /// <param name="token"></param>
        public void InitStreaming(ITextToSpeechService service, CancellationToken token = default)
        {
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                Debug.LogWarning("Streaming is already in progress.");
                return;
            }

            if (_audioDecoder == null)
                _audioDecoder = new StreamingAudioDecoder();

            if (_cancellationSource == null)
                _cancellationSource = new CancellationTokenSource();

            while (_audioBuffer.TryDequeue(out _)) { }

            if (_webSocket == null)
                _webSocket = new ClientWebSocket();

            // Aborted websockets cannot be reused, so we create a new one
            if (_webSocket.State == WebSocketState.Aborted)
            {
                _webSocket.Dispose();
                _webSocket = new ClientWebSocket();
            }

            token = CancellationTokenSource.CreateLinkedTokenSource(token, _cancellationSource.Token).Token;
            service.InitWebsocket(_webSocket, _audioDecoder, token);

        }

        /// <summary>
        /// Stops the current audio stream.
        /// </summary>
        public void StopStreaming()
        {
            _cancellationSource?.Cancel();
            if (_webSocket != null)
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Finished", CancellationToken.None);
                }
                _webSocket.Dispose();
                _webSocket = null;
            }
        }

        /// <summary>
        /// Connects to the WebSocket and streams the audio data.
        /// </summary>
        /// <param name="text">The text to be streamed.</param>
        /// <param name="service">The text-to-speech service.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async void ConnectAndStream(string text, ITextToSpeechService service, CancellationToken token)
        {
            try
            {
                _audioSource.Play();
                await service.ConnectAndStream(text, _webSocket, token);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Streaming cancelled by user.");
            }
            catch (Exception e)
            {
                Debug.LogError($"WebSocket Error: {e.Message}");
            }
        }

        /// <summary>
        /// This method is called by Unity on the audio thread to request audio data.
        /// It fills the buffer with decoded audio samples.
        /// </summary>
        /// <param name="data">The array to fill with audio data.</param>
        /// <param name="channels">The number of channels in the audio data.</param>
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (_audioDecoder == null) return;
            //if (!_mp3Decoder.HasSamples) return;

            // The decoder provides a perfectly formatted stream (correct sample rate AND channel count),
            // so we just copy it directly into Unity's buffer.
            for (int i = 0; i < data.Length; i++)
            {
                if (_audioDecoder.TryGetSample(out float sample))
                {
                    data[i] = sample;
                }
                else
                {
                    // If the buffer runs out of samples, fill the rest with silence.
                    data[i] = 0.0f;
                }
            }

            OnAudioSamplePlayed?.Invoke(this, data);
        }

        /// <summary>
        /// Called when the MonoBehaviour will be destroyed.
        /// </summary>
        private void OnDestroy()
        {
            StopStreaming();
        }
    }
}