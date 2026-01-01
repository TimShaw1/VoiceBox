using Microsoft.CognitiveServices.Speech;
using NAudio.Wave; // Requires NAudio library
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Data;
using TimShaw.VoiceBox.Generics;
using UnityEngine;
using UnityEngine.Rendering;
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
        private const int BUFFER_MILLISECONDS = 200; // Approx 200ms chunks

        // Local VAD for limiting amount of silent chunks sent
        private DateTime previousSilence = DateTime.Now;
        private DateTime previousMessageSendTime = DateTime.Now;
        private bool isCurrentlySilent = false;
        byte[] previousSkippedSilenceChunk;

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
            var queryParams = new System.Collections.Generic.List<string>
            {
                "model_id=scribe_v2_realtime",  // TODO
                "audio_format=pcm_16000",   // TODO
                $"language_code={_config.language_code}",
                $"commit_strategy={(_config.commit_strategy == ElevenlabsSTTCommitStrategy.Manual ? "manual" : "vad")}", // "manual" or "vad"
                $"include_timestamps={_config.include_timestamps}"
            };

            // Only append specific VAD settings if using VAD strategy to keep URL clean, 
            // though API accepts them regardless.
            if (_config.commit_strategy == ElevenlabsSTTCommitStrategy.Vad)
            {
                queryParams.Add($"vad_silence_threshold_secs={_config.vad_silence_threshold_secs}");
                queryParams.Add($"vad_threshold={_config.vad_threshold}");
                queryParams.Add($"min_speech_duration_ms={_config.min_speech_duration_ms}");
                queryParams.Add($"min_silence_duration_ms={_config.min_silence_duration_ms}");
            }

            uriBuilder.Query = string.Join("&", queryParams);

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

        public bool IsSilence(byte[] pcmData, float threshold = 0.01f)
        {
            // 16-bit PCM = 2 bytes per sample
            int sampleCount = pcmData.Length / 2;
            double sum = 0;

            for (int i = 0; i < pcmData.Length; i += 2)
            {
                // Convert 2 bytes to a 16-bit signed integer (short)
                // Bit-shifting is faster than BitConverter.ToInt16
                short sample = (short)((pcmData[i + 1] << 8) | pcmData[i]);

                // Normalize to -1.0 to 1.0 range (32768 is the max value for short)
                float normalizedSample = sample / 32768f;

                // Accumulate sum of squares
                sum += normalizedSample * normalizedSample;
            }

            // Calculate RMS
            double rms = Math.Sqrt(sum / sampleCount);

            // Return true if RMS is below the threshold
            return rms < threshold;
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
                if (IsSilence(pcmData))
                {
                    if (!isCurrentlySilent)
                        previousSilence = DateTime.Now;

                    isCurrentlySilent = true;

                    // Skip this chunk if total silence duration > silence threshold + buffer*4 + buffer error padding
                    if ((DateTime.Now - previousSilence).TotalSeconds > _config.vad_silence_threshold_secs + (BUFFER_MILLISECONDS / 250) + 2f)
                    {
                        if ((DateTime.Now - previousMessageSendTime).TotalSeconds > 8)
                        {
                            await SendAudioChunk(pcmData, token);
                            previousSkippedSilenceChunk = null;
                        }
                        else
                            previousSkippedSilenceChunk = pcmData;
                        continue; 
                    }
                }
                else
                {
                    isCurrentlySilent = false;
                }

                if (previousSkippedSilenceChunk != null)
                {
                    await SendAudioChunk(previousSkippedSilenceChunk, token);
                    previousSkippedSilenceChunk = null;
                }

                await SendAudioChunk(pcmData, token);
            }
        }

        private async Task SendAudioChunk(byte[] pcmData, CancellationToken token)
        {
            // 1. Base64 Encode (NAudio already provides PCM16 byte[], no float conversion needed!)
            string base64Audio = Convert.ToBase64String(pcmData);

            // 2. Construct JSON
            // { "message_type": "input_audio_chunk", "audio_base_64": "...", "commit": false }
            string jsonMessage = $"{{\"message_type\": \"input_audio_chunk\", \"audio_base_64\": \"{base64Audio}\"}}";

            // 3. Send
            byte[] bytesToSend = Encoding.UTF8.GetBytes(jsonMessage);
            previousMessageSendTime = DateTime.Now;
            await _webSocket.SendAsync(new ArraySegment<byte>(bytesToSend), WebSocketMessageType.Text, true, token);
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
                        var responseObj = JsonConvert.DeserializeObject<ElevenLabsResponse>(responseJson);
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
            if (ElevenlabsErrorCodes.Contains(response.message_type))
            {
                string errorDetails = $"ElevenLabs Error [{response.message_type}]";

                // Log generic error to console
                Debug.LogError(errorDetails);

                // Handle specific codes if custom logic is needed (e.g., specific UI feedback)
                switch (response.message_type)
                {
                    case "input_error":
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
                    case "rate_limited":
                        // Too many concurrent connections
                        errorDetails = "Rate Limit Exceeded: Please try again later.";
                        break;
                    default:
                        // Keep the original message for unknown codes
                        break;
                }

                // Dispatch Cancellation Event
                OnCanceled?.Invoke(this, new VoiceBoxSpeechRecognitionCanceledEventArgs(STTUtils.CancellationReason.Error, response.message_type, errorDetails));

                return; // Stop processing this message
            }

            // -------------------------------------------------------------------------
            // 2. TRANSCRIPT HANDLING (Existing Logic)
            // -------------------------------------------------------------------------
            ResultReason reason;
            if (response.message_type == "committed_transcript_with_timestamps")
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

            double startSec = -1;
            double durationSec = -1;
            TimeSpan duration = TimeSpan.Zero;
            long offsetInTicks = -1;
            if (response.words != null)
            {
                startSec = response.words[0].start;
                durationSec = response.words[response.words.Length - 1].end - response.words[0].start;

                duration = TimeSpan.FromSeconds(durationSec);
                offsetInTicks = (long)(startSec * TimeSpan.TicksPerSecond);
            }

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
            public WordAlignment[] words;

            public override string ToString()
            {
                return $"message_type:{message_type}\ntext: {text}";
            }
        }

        [Serializable]
        private class WordAlignment
        {
            public string text;
            public double start;
            public double end;
            public string type;
            public double logprob;
        }

        private string[] ElevenlabsErrorCodes = { 
            "error", "auth_error", "quota_exceeded", "commit_throttled", "unaccepted_terms", "rate_limited", "queue_overflow", "resource_exhausted", 
            "session_time_limit_exceeded", "input_error", "chunk_size_exceeded", "insufficient_audio_activity", "transcriber_error"
        };
    }
}