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
    /// <summary>
    /// Decodes a streaming MP3 audio feed into raw audio samples.
    /// </summary>
    public class StreamingMp3Decoder
    {
        /// <summary>
        /// Gets a value indicating whether there are decoded samples available.
        /// </summary>
        public bool HasSamples => !_decodedSamples.IsEmpty;
        private readonly ConcurrentQueue<float> _decodedSamples = new ConcurrentQueue<float>();
        private MemoryStream _mp3Stream;
        private Mp3FileReader _mp3Reader;
        private IWaveProvider _resampler;
        private readonly byte[] _conversionBuffer;
        private const int BufferSize = 4096;
        private int _sampleRate = AudioSettings.outputSampleRate;
        private AudioSpeakerMode speakerMode = AudioSettings.speakerMode;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamingMp3Decoder"/> class.
        /// </summary>
        public StreamingMp3Decoder()
        {
            _mp3Stream = new MemoryStream();
            _conversionBuffer = new byte[BufferSize];
        }

        /// <summary>
        /// Feeds MP3 data into the decoder.
        /// </summary>
        /// <param name="mp3Data">The byte array of MP3 data.</param>
        public void Feed(byte[] mp3Data)
        {
            long originalPosition = _mp3Stream.Position;
            _mp3Stream.Seek(0, SeekOrigin.End);
            _mp3Stream.Write(mp3Data, 0, mp3Data.Length);
            _mp3Stream.Position = originalPosition;

            if (_mp3Reader == null && _mp3Stream.Length > 0)
            {
                try
                {
                    _mp3Reader = new Mp3FileReader(_mp3Stream);

                    int unitySampleRate = _sampleRate;

                    // Determine Unity's channel count based on the current speaker mode.
                    int unityChannelCount = (speakerMode == AudioSpeakerMode.Mono) ? 1 : 2;

                    // Create an output format that EXACTLY matches Unity's configuration.
                    var outputFormat = new WaveFormat(unitySampleRate, unityChannelCount);

                    _resampler = new MediaFoundationResampler(_mp3Reader, outputFormat);
                }
                catch (InvalidDataException) { _mp3Reader = null; return; }
            }

            if (_resampler != null) ReadAndEnqueueAvailableSamples();
        }

        /// <summary>
        /// Reads and enqueues available audio samples from the resampler.
        /// </summary>
        private void ReadAndEnqueueAvailableSamples()
        {
            int bytesRead;
            while ((bytesRead = _resampler.Read(_conversionBuffer, 0, _conversionBuffer.Length)) > 0)
            {
                for (int i = 0; i < bytesRead; i += 2)
                {
                    short sample = (short)((_conversionBuffer[i + 1] << 8) | _conversionBuffer[i]);
                    _decodedSamples.Enqueue(sample / 32768.0f);
                }
            }
        }

        /// <summary>
        /// Tries to get the next audio sample from the queue.
        /// </summary>
        /// <param name="sample">The retrieved audio sample.</param>
        /// <returns>True if a sample was retrieved, otherwise false.</returns>
        public bool TryGetSample(out float sample) { return _decodedSamples.TryDequeue(out sample); }

        /// <summary>
        /// Disposes the MP3 reader and memory stream.
        /// </summary>
        public void Dispose() { _mp3Reader?.Dispose(); _mp3Stream?.Dispose(); }
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

        private StreamingMp3Decoder _mp3Decoder;

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
        /// Starts streaming speech for the given text using the specified service.
        /// </summary>
        /// <param name="text">The text to be converted to speech.</param>
        /// <param name="service">The text-to-speech service to use.</param>
        /// <param name="token"></param>
        public void StartStreaming(string text, ITextToSpeechService service, CancellationToken token = default)
        {
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                Debug.LogWarning("Streaming is already in progress.");
                return;
            }

            _mp3Decoder = new StreamingMp3Decoder();
            _cancellationSource = new CancellationTokenSource();

            while (_audioBuffer.TryDequeue(out _)) { }

            _webSocket = new ClientWebSocket();

            Uri uri = service.InitWebsocketAndGetUri(_webSocket);

            token = CancellationTokenSource.CreateLinkedTokenSource(token, _cancellationSource.Token).Token;

            Task.Run(() => ConnectAndStream(text, uri, service, token));
            _audioSource.Play();

        }

        /// <summary>
        /// Stops the current audio stream.
        /// </summary>
        public void StopStreaming()
        {
            _cancellationSource?.Cancel();
        }

        /// <summary>
        /// Connects to the WebSocket and streams the audio data.
        /// </summary>
        /// <param name="text">The text to be streamed.</param>
        /// <param name="uri">The WebSocket URI.</param>
        /// <param name="service">The text-to-speech service.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ConnectAndStream(string text, Uri uri, ITextToSpeechService service, CancellationToken token)
        {
            try
            {
                await _webSocket.ConnectAsync(uri, token);

                await service.ConnectAndStream(text, _webSocket, _mp3Decoder, token);

            }
            catch (OperationCanceledException)
            {
                Debug.Log("Streaming cancelled by user.");
            }
            catch (Exception e)
            {
                Debug.LogError($"WebSocket Error: {e.Message}");
            }
            finally
            {
                if (_webSocket != null)
                {
                    if (_webSocket.State == WebSocketState.Open)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Finished", CancellationToken.None);
                    }
                    _webSocket.Dispose();
                    _webSocket = null;
                }
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
            if (_mp3Decoder == null) return;

            // The decoder provides a perfectly formatted stream (correct sample rate AND channel count),
            // so we just copy it directly into Unity's buffer.
            for (int i = 0; i < data.Length; i++)
            {
                if (_mp3Decoder.TryGetSample(out float sample))
                {
                    data[i] = sample;
                }
                else
                {
                    // If the buffer runs out of samples, fill the rest with silence.
                    data[i] = 0.0f;
                }
            }
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