using Microsoft.CognitiveServices.Speech;
using NAudio.Wave; // Requires NAudio library
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Data;
using TimShaw.VoiceBox.Generics;
using UnityEngine;
using static TimShaw.VoiceBox.Core.STTUtils;

namespace TimShaw.VoiceBox.Core
{
    public class ElevenlabsSTTServiceManager : ISpeechToTextService
    {
        // Events
        public event EventHandler<STTUtils.VoiceBoxSpeechRecognitionEventArgs> OnRecognizing;
        public event EventHandler<STTUtils.VoiceBoxSpeechRecognitionEventArgs> OnRecognized;
        public event EventHandler<VoiceBoxSpeechRecognitionCanceledEventArgs> OnCanceled;
        public event EventHandler<SessionEventArgs> OnSessionStarted;
        public event EventHandler<SessionEventArgs> OnSessionStopped;
        public event EventHandler<RecognitionEventArgs> OnSpeechStartDetected;
        public event EventHandler<RecognitionEventArgs> OnSpeechEndDetected;

        // Config & Connection
        private ElevenlabsSTTServiceConfig _config;
        private ClientWebSocket _webSocket;
        private string _apiKey;

        // NAudio & Buffering
        private WaveInEvent _waveIn;
        // Thread-safe collection to hold audio chunks between the Recorder thread and the Sender thread
        private BlockingCollection<byte[]> _audioSendQueue;

        // Constants
        private const int SAMPLE_RATE = 16000;
        private const int BITS_PER_SAMPLE = 16;
        private const int CHANNELS = 1;
        private const int BUFFER_MILLISECONDS = 100; // Approx 100ms chunks

        public void Initialize(GenericSTTServiceConfig config)
        {
            _config = config as ElevenlabsSTTServiceConfig;

            if (string.IsNullOrEmpty(_config.apiKey))
            {
                Debug.LogError("ElevenLabs API Key is missing.");
                return;
            }
            _apiKey = _config.apiKey;
        }

        public async Task TranscribeAudioFromMic(CancellationToken token)
        {
            if (_config == null)
            {
                Debug.LogError("Service not initialized.");
                return;
            }

            _webSocket = new ClientWebSocket();
            _audioSendQueue = new BlockingCollection<byte[]>();

            // 1. Prepare WebSocket Connection
            _webSocket.Options.SetRequestHeader("xi-api-key", _apiKey);
            var uriBuilder = new UriBuilder("wss://api.elevenlabs.io/v1/speech-to-text/realtime");
            // Ensure format matches NAudio settings (pcm_16000)
            uriBuilder.Query = $"model_id=scribe_v2_realtime&audio_format=pcm_16000&language_code={_config.language}&commit_strategy=vad";

            try
            {
                // 2. Connect to API
                await _webSocket.ConnectAsync(uriBuilder.Uri, token);
                Debug.Log("Connected to ElevenLabs Scribe (NAudio).");

                //OnSessionStarted?.Invoke(this, new SessionEventArgs { SessionId = Guid.NewGuid().ToString() });

                // 3. Setup NAudio Recording
                InitializeNAudio();
                _waveIn.StartRecording();

                // 4. Start Parallel Loops
                //    - SendAudioLoop: Consumes audio from _audioSendQueue and pushes to WebSocket
                //    - ReceiveMessagesLoop: Listens for transcripts
                var sendTask = SendAudioLoop(token);
                var receiveTask = ReceiveMessagesLoop(token);

                await Task.WhenAny(sendTask, receiveTask);
            }
            catch (Exception ex)
            {
                Debug.LogError($"ElevenLabs STT Error: {ex.Message}");
                OnCanceled?.Invoke(this, new VoiceBoxSpeechRecognitionCanceledEventArgs(STTUtils.CancellationReason.Error, "", ex.Message));
            }
            finally
            {
                StopRecording();

                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended", CancellationToken.None);
                }

                _webSocket?.Dispose();
                _audioSendQueue?.Dispose();
                OnSessionStopped?.Invoke(this, null);
            }
        }

        private void InitializeNAudio()
        {
            int deviceNumber = GetAudioDeviceIndex(_config.audioInputDeviceName);

            _waveIn = new WaveInEvent();
            _waveIn.DeviceNumber = deviceNumber;
            _waveIn.WaveFormat = new WaveFormat(SAMPLE_RATE, BITS_PER_SAMPLE, CHANNELS);
            _waveIn.BufferMilliseconds = BUFFER_MILLISECONDS;

            // Subscribe to data available event
            _waveIn.DataAvailable += OnAudioDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
        }

        /// <summary>
        /// Event handler: Fired by NAudio whenever the buffer is full (approx every 100ms).
        /// We copy the data and enqueue it for the Sender task.
        /// </summary>
        private void OnAudioDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_audioSendQueue != null && !_audioSendQueue.IsAddingCompleted)
            {
                // Important: We must create a copy of the buffer because NAudio reuses e.Buffer
                byte[] chunk = new byte[e.BytesRecorded];
                Array.Copy(e.Buffer, chunk, e.BytesRecorded);

                _audioSendQueue.Add(chunk);
            }
        }

        private void StopRecording()
        {
            if (_waveIn != null)
            {
                _waveIn.StopRecording();
                _waveIn.Dispose();
                _waveIn = null;
            }

            // Signal the consumer loop that no more data is coming
            if (_audioSendQueue != null && !_audioSendQueue.IsAddingCompleted)
            {
                _audioSendQueue.CompleteAdding();
            }
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                Debug.LogError($"NAudio Recording Stopped Error: {e.Exception.Message}");
            }
        }

        /// <summary>
        /// Helper to map the string name from Config to NAudio's integer Device ID.
        /// </summary>
        private int GetAudioDeviceIndex(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName) || deviceName == "Default") return 0;

            int deviceCount = WaveIn.DeviceCount;
            for (int i = 0; i < deviceCount; i++)
            {
                var capabilities = WaveIn.GetCapabilities(i);
                // Case-insensitive containment check
                if (capabilities.ProductName.IndexOf(deviceName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
            }

            Debug.LogWarning($"Device '{deviceName}' not found. Defaulting to device 0.");
            return 0;
        }

        /// <summary>
        /// Consumes the BlockingCollection queue and sends chunks to WebSocket.
        /// </summary>
        private async Task SendAudioLoop(CancellationToken token)
        {
            // GetConsumingEnumerable returns an iterator that blocks until data is available
            // and breaks automatically when CompleteAdding() is called.
            foreach (var pcmData in _audioSendQueue.GetConsumingEnumerable(token))
            {
                if (_webSocket.State != WebSocketState.Open) break;

                // 1. Base64 Encode (NAudio already provides PCM16 byte[], no float conversion needed!)
                string base64Audio = Convert.ToBase64String(pcmData);

                // 2. Construct JSON
                // { "message_type": "input_audio_chunk", "audio_base_64": "...", "commit": false }
                string jsonMessage = $"{{\"message_type\": \"input_audio_chunk\", \"audio_base_64\": \"{base64Audio}\"}}";

                // 3. Send
                byte[] bytesToSend = Encoding.UTF8.GetBytes(jsonMessage);
                await _webSocket.SendAsync(new ArraySegment<byte>(bytesToSend), WebSocketMessageType.Text, true, token);
            }
        }

        // -------------------------------------------------------------------------
        // RECEIVE LOGIC (Same as previous step, included for completeness)
        // -------------------------------------------------------------------------

        private async Task ReceiveMessagesLoop(CancellationToken token)
        {
            var buffer = new byte[1024 * 32];

            while (!token.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    break;
                }

                using (var ms = new MemoryStream())
                {
                    ms.Write(buffer, 0, result.Count);
                    while (!result.EndOfMessage)
                    {
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        ms.Write(buffer, 0, result.Count);
                    }

                    string responseJson = Encoding.UTF8.GetString(ms.ToArray());
                    try
                    {
                        var responseObj = JsonUtility.FromJson<ElevenLabsResponse>(responseJson);
                        if (responseObj != null) ParseAndDispatchEvent(responseObj);
                    }
                    catch (Exception ex) { Debug.LogWarning($"JSON Parse Error: {ex.Message}"); }
                }
            }
        }

        private void ParseAndDispatchEvent(ElevenLabsResponse response)
        {
            // -------------------------------------------------------------------------
            // 1. ERROR HANDLING
            // -------------------------------------------------------------------------
            if (response.message_type == "error")
            {
                string errorDetails = $"ElevenLabs Error [{response.code}]: {response.message}";

                // Log generic error to console
                Debug.LogError(errorDetails);

                // Handle specific codes if custom logic is needed (e.g., specific UI feedback)
                switch (response.code)
                {
                    case "invalid_request":
                        // Usually bad parameters (sample rate, format)
                        errorDetails = "Invalid Request: Please check audio format and model parameters.";
                        break;
                    case "auth_error":
                        // Invalid API Key
                        errorDetails = "Authentication Failed: Please check your API Key.";
                        break;
                    case "quota_exceeded":
                        // Character limit reached
                        errorDetails = "Quota Exceeded: You have run out of ElevenLabs credits.";
                        break;
                    case "rate_limit_exceeded":
                        // Too many concurrent connections
                        errorDetails = "Rate Limit Exceeded: Please try again later.";
                        break;
                    default:
                        // Keep the original message for unknown codes
                        break;
                }

                // Dispatch Cancellation Event
                OnCanceled?.Invoke(this, new VoiceBoxSpeechRecognitionCanceledEventArgs(STTUtils.CancellationReason.Error, response.code, errorDetails));

                return; // Stop processing this message
            }

            // -------------------------------------------------------------------------
            // 2. TRANSCRIPT HANDLING (Existing Logic)
            // -------------------------------------------------------------------------
            ResultReason reason;
            if (response.message_type == "committed_transcript" || response.is_final)
            {
                reason = ResultReason.RecognizedSpeech;
            }
            else if (response.message_type == "partial_transcript")
            {
                reason = ResultReason.RecognizingSpeech;
            }
            else
            {
                // Ignore keep-alive (type="ping") or other metadata
                return;
            }

            if (string.IsNullOrEmpty(response.text)) return;

            double durationSec = response.duration;
            double startSec = response.start_timestamp;

            // Fallback timing calculation
            if ((durationSec <= 0 || startSec <= 0) && response.words != null && response.words.Length > 0)
            {
                startSec = response.words[0].start;
                durationSec = response.words[response.words.Length - 1].end - response.words[0].start;
            }

            TimeSpan duration = TimeSpan.FromSeconds(durationSec);
            long offsetInTicks = (long)(startSec * TimeSpan.TicksPerSecond);

            var args = new STTUtils.VoiceBoxSpeechRecognitionEventArgs(
                reason,
                response.text,
                duration,
                offsetInTicks
            );

            if (reason == ResultReason.RecognizingSpeech)
            {
                OnRecognizing?.Invoke(this, args);
            }
            else
            {
                OnRecognized?.Invoke(this, args);
            }
        }

        // -------------------------------------------------------------------------
        // DATA CLASSES
        // -------------------------------------------------------------------------

        [Serializable]
        private class ElevenLabsResponse
        {
            // Common fields
            public string message_type;              // "partial_transcript", "committed_transcript", or "error"

            // Transcript fields
            public string text;
            public bool is_final;
            public double start_timestamp;
            public double duration;
            public WordAlignment[] words;

            // Error fields (populated when type == "error")
            public string message;
            public string code;
        }

        [Serializable]
        private class WordAlignment
        {
            public string text;
            public double start;
            public double end;
        }
    }
}