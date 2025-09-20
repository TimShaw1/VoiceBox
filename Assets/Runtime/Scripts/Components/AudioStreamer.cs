using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class StreamingMp3Decoder
{
    public bool HasSamples => !_decodedSamples.IsEmpty;
    private readonly ConcurrentQueue<float> _decodedSamples = new ConcurrentQueue<float>();
    private MemoryStream _mp3Stream;
    private Mp3FileReader _mp3Reader;
    private IWaveProvider _resampler;
    private readonly byte[] _conversionBuffer;
    private const int BufferSize = 4096;
    private int _sampleRate = AudioSettings.outputSampleRate;
    private AudioSpeakerMode speakerMode = AudioSettings.speakerMode;

    public StreamingMp3Decoder()
    {
        _mp3Stream = new MemoryStream();
        _conversionBuffer = new byte[BufferSize];
    }

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

                // --- THE FIX ---
                // Determine Unity's channel count
                int unityChannelCount = (speakerMode == AudioSpeakerMode.Mono) ? 1 : 2;

                // Create an output format that EXACTLY matches Unity's configuration
                var outputFormat = new WaveFormat(unitySampleRate, unityChannelCount);

                _resampler = new MediaFoundationResampler(_mp3Reader, outputFormat);
            }
            catch (InvalidDataException) { _mp3Reader = null; return; }
        }

        if (_resampler != null) ReadAndEnqueueAvailableSamples();
    }

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
    public bool TryGetSample(out float sample) { return _decodedSamples.TryDequeue(out sample); }
    public void Dispose() { _mp3Reader?.Dispose(); _mp3Stream?.Dispose(); }
}


/// <summary>
/// Enables streaming audio
/// TODO: Implement a more robust solution that can work for any TTS service
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioStreamer : MonoBehaviour
{

    private AudioSource _audioSource;
    private ClientWebSocket _webSocket;
    private CancellationTokenSource _cancellationSource;

    // Thread-safe buffer for audio data
    private ConcurrentQueue<float> _audioBuffer = new ConcurrentQueue<float>();

    private StreamingMp3Decoder _mp3Decoder;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        // We configure the AudioSource to be controlled by our script
        _audioSource.playOnAwake = false;
    }

    /// <summary>
    /// Starts streaming speech for the given text.
    /// </summary>
    public void StartStreaming(string text, ScriptableObject config)
    {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            Debug.LogWarning("Streaming is already in progress.");
            return;
        }

        _mp3Decoder = new StreamingMp3Decoder();
        _cancellationSource = new CancellationTokenSource();

        // Clear any old data
        while (_audioBuffer.TryDequeue(out _)) { }

        _webSocket = new ClientWebSocket();

        if (config is ElevenlabsTTSServiceConfig)
        {
            var derivedElevenlabsConfig = (config as ElevenlabsTTSServiceConfig);
            _webSocket.Options.SetRequestHeader("xi-api-key", derivedElevenlabsConfig.apiKey);
            string uri = $"wss://api.elevenlabs.io/v1/text-to-speech/{derivedElevenlabsConfig.voiceId}/stream-input?model_id=eleven_multilingual_v2";

            // Start the connection and streaming process
            Task.Run(() => ConnectAndStream(text, uri, _cancellationSource.Token));
            _audioSource.Play();
        }
    }

    /// <summary>
    /// Stops the current audio stream.
    /// </summary>
    public void StopStreaming()
    {
        _cancellationSource?.Cancel();
    }

    private async Task ConnectAndStream(string text, string uri, CancellationToken token)
    {
        try
        {
            Debug.Log("Connecting to WebSocket...");
            await _webSocket.ConnectAsync(new Uri(uri), token);
            Debug.Log("WebSocket Connected.");

            // 1. Send the initial authentication and configuration message

            var initialMessage = new { text = " " };
            string jsonMessage = JsonConvert.SerializeObject(initialMessage);
            Debug.Log(jsonMessage);
            await SendSocketMessage(jsonMessage);

            // 2. Send the text to be spoken
            var textMessage = new { text = text, try_trigger_generation = true };
            jsonMessage = JsonConvert.SerializeObject(textMessage);
            Debug.Log(jsonMessage);
            await SendSocketMessage(jsonMessage);

            // 3. Send the End of Stream message
            var eosMessage = new { text = "" };
            jsonMessage = JsonConvert.SerializeObject(eosMessage);
            Debug.Log(jsonMessage);
            await SendSocketMessage(jsonMessage);

            // 4. Start listening for audio data
            //await ReceiveAudioData(token);
            await ReceiveAudioData(token);
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
                Debug.Log("WebSocket disconnected.");
            }
        }
    }

    private async Task SendSocketMessage(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cancellationSource.Token);
    }

    [System.Serializable]
    public class ElevenLabsStreamedResponse
    {
        public string audio;
        public bool isFinal;
        // We can ignore the alignment data for now if we don't need it
    }

    // Place this inside your AudioStreamer.cs class, replacing the old method.
    private async Task ReceiveAudioData(CancellationToken token)
    {
        // Start playing the audio source. It will initially play silence
        // until OnAudioFilterRead starts getting data from our buffer.
        //_audioSource.Play();

        var receiveBuffer = new byte[8192]; // 8KB buffer
        var messageBuilder = new StringBuilder();

        while (_webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), token);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                // Append the received text chunk to our builder
                messageBuilder.Append(Encoding.UTF8.GetString(receiveBuffer, 0, result.Count));

                // If this is the end of a message, process the complete JSON
                if (result.EndOfMessage)
                {
                    string jsonString = messageBuilder.ToString();

                    // Check if the message contains audio data
                    if (jsonString.Contains("\"audio\""))
                    {
                        ElevenLabsStreamedResponse response = JsonUtility.FromJson<ElevenLabsStreamedResponse>(jsonString);

                        if (!string.IsNullOrEmpty(response.audio))
                        {
                            // 1. Decode the Base64 string into raw bytes
                            byte[] audioBytes = Convert.FromBase64String(response.audio);

                            // 2. Feed the bytes into our MP3 decoder
                            _mp3Decoder.Feed(audioBytes);
                        }
                    }

                    // You can add logic here to check for the "isFinal": true message
                    // to gracefully stop the AudioSource after the buffer empties.

                    // Clear the builder for the next message
                    messageBuilder.Clear();
                }
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
                break;
            }
        }

        // Optional: Add logic here to wait until the buffer is empty before stopping the AudioSource
    }

    // This method is called by Unity on the audio thread.
    // It requests data to fill the audio buffer.
    // In AudioStreamer.cs

    void OnAudioFilterRead(float[] data, int channels)
    {
        // Basic check to ensure the decoder is ready
        if (_mp3Decoder == null) return;

        // A simple, direct loop.
        // The decoder is now providing a perfectly formatted stream (correct sample rate AND channel count),
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

    private void OnDestroy()
    {
        StopStreaming();
    }
}