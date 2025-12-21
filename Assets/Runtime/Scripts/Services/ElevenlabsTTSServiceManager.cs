using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Components;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Data;
using TimShaw.VoiceBox.Generics;
using UnityEngine;
using UnityEngine.Networking;

namespace TimShaw.VoiceBox.Core
{
    /// <summary>
    /// Manages the ElevenLabs Text-to-Speech (TTS) service.
    /// </summary>
    public class ElevenLabsTTSServiceManager : ITextToSpeechService
    {
        /// <summary>
        /// Ocurrs when the TTS service recieved audio data
        /// </summary>
        public event System.EventHandler<byte[]> OnAudioDataRecieved;

        private HttpClient client;
        /// <summary>
        /// The configuration for the ElevenLabs TTS service. 
        /// </summary>
        private ElevenlabsTTSServiceConfig _config;
        private string fileExtension;

        private Task _recieveAudioTask;

        /// <summary>
        /// Represents the request body for the ElevenLabs TTS API.
        /// </summary>
        [System.Serializable]
        private class ElevenLabsTTSRequest
        {
            public string text;
            public string model_id;
            public VoiceSettings voice_settings;
            public string previous_text = "";
            public string next_text = "";
        }

        /// <summary>
        /// Represents a streamed response from the ElevenLabs TTS API.
        /// </summary>
        [System.Serializable]
        private class ElevenLabsStreamedResponse
        {
#pragma warning disable CS0649 // Field is never assigned to
            public string audio;
            public bool isFinal;
#pragma warning restore CS0649 // Field is never assigned to
        }

        /// <summary>
        /// Initializes the ElevenLabs TTS service with the provided configuration.
        /// </summary>
        /// <param name="config">The ScriptableObject configuration for the ElevenLabs TTS service.</param>
        public void Initialize(GenericTTSServiceConfig config)
        {

            _config = config as ElevenlabsTTSServiceConfig;
            if (_config.apiKey.Length == 0)
            {
                Debug.LogError("[ElevenLabsTTSServiceManager] No Elevenlabs API key found.");
                return;
            }

            if (_config.voiceId.Length == 0)
            {
                Debug.LogError("[ElevenLabsTTSServiceManager] No Elevenlabs Voice ID found.");
                return;
            }

            client = new HttpClient();

            fileExtension = _config.output_format.Contains("mp3") ? ".mp3" : ".wav";

            client.DefaultRequestHeaders.Add("xi-api-key", _config.apiKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/wav"));

        }

        /// <summary>
        /// Converts an MP3 file to a WAV file.
        /// </summary>
        /// <param name="_inPath_">The path to the input MP3 file.</param>
        /// <param name="_outPath_">The path to the output WAV file.</param>
        private static void ConvertMp3ToWav(string _inPath_, string _outPath_)
        {
            using (Mp3FileReader mp3 = new Mp3FileReader(_inPath_))
            {
                using (WaveStream pcm = WaveFormatConversionStream.CreatePcmStream(mp3))
                {
                    WaveFileWriter.CreateWaveFile(_outPath_, pcm);
                }
            }
        }

        /// <summary>
        /// Increases the volume of a WAV file.
        /// </summary>
        /// <param name="inputPath">The path to the input WAV file.</param>
        /// <param name="outputPath">The path to the output WAV file.</param>
        /// <param name="db">The amount to increase the volume by in decibels.</param>
        private static void IncreaseVolume(string inputPath, string outputPath, double db)
        {
            double linearScalingRatio = Math.Pow(10d, db / 10d);
            using (WaveFileReader reader = new WaveFileReader(inputPath))
            {
                VolumeWaveProvider16 volumeProvider = new VolumeWaveProvider16(reader);
                using (WaveFileWriter writer = new WaveFileWriter(outputPath, reader.WaveFormat))
                {
                    while (true)
                    {
                        var frame = reader.ReadNextSampleFrame();
                        if (frame == null)
                            break;
                        var sample = frame[0] * (float)linearScalingRatio;
                        if (sample < -0.6f)
                            sample = -0.6f;
                        if (sample > 0.6f)
                            sample = 0.6f;
                        writer.WriteSample(frame[0] * (float)linearScalingRatio);
                    }
                }
            }
        }

        /// <summary>
        /// Requests an audio file from the ElevenLabs TTS service.
        /// </summary>
        /// <param name="prompt">The text to be converted to speech.</param>
        /// <param name="fileName">The name of the output audio file, excluding the file extension.</param>
        /// <param name="dir">The directory to save the audio file in.</param>
        /// <param name="onSuccess">Callback for when file is created. Should return the path to the file.</param>
        /// <param name="onError">Callback for when an error occurs</param>
        /// <param name="token"></param>
        /// <returns>The path to the file</returns>
        public async Task RequestAudioFile(string prompt, string fileName, string dir, Action<string> onSuccess, Action<string> onError, CancellationToken token)
        {
            try
            {
                fileName = Path.GetFileNameWithoutExtension(fileName);
                string url = _config.serviceEndpoint + _config.voiceId;

                var payload = new ElevenLabsTTSRequest
                {
                    text = prompt,
                    model_id = _config.modelID,
                    voice_settings = _config.voiceSettings
                };

                string json = JsonUtility.ToJson(payload);
                StringContent httpContent = new StringContent(json, System.Text.Encoding.Default, "application/json");

                Debug.Log("Requesting audio...");

            
                var response = await client.PostAsync(url, httpContent);
                Debug.Log("Got response...");
                response.EnsureSuccessStatusCode();
                Debug.Log("Success...");

                using (Stream stream = await response.Content.ReadAsStreamAsync())
                using (FileStream fileStream = System.IO.File.Create(Path.Combine(dir, fileName.ToString()) + fileExtension))
                {
                    await stream.CopyToAsync(fileStream);
                }
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.ToString());
                return;
            }

            onSuccess?.Invoke(Path.GetFullPath(Path.Combine(dir, fileName.ToString()) + fileExtension));
            return;

        }

        /// <summary>
        /// Requests an AudioClip from the ElevenLabs TTS service.
        /// </summary>
        /// <param name="prompt">The text to be converted to speech.</param>
        /// <returns>A task that represents the asynchronous operation, returning an AudioClip.</returns>
        public async Task<AudioClip> RequestAudioClip(string prompt)
        {
            string url = _config.serviceEndpoint + _config.voiceId;

            var payload = new ElevenLabsTTSRequest
            {
                text = prompt,
                model_id = _config.modelID,
                voice_settings = _config.voiceSettings
            };

            string json = JsonUtility.ToJson(payload);
            byte[] postData = System.Text.Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(postData);
                www.downloadHandler = new DownloadHandlerAudioClip(new Uri(url), AudioType.MPEG);
                www.SetRequestHeader("Content-Type", "application/json");
                www.SetRequestHeader("xi-api-key", _config.apiKey);

                var operation = www.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (www.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("Successfully downloaded audio data.");
                    return DownloadHandlerAudioClip.GetContent(www);
                }
                else
                {
                    Debug.LogError("Failed to get audio clip: " + www.error);
                    Debug.LogError("Response: " + www.downloadHandler.text);
                    return null;
                }
            }
        }

        /// <summary>
        /// Sends a message over a WebSocket connection.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="_webSocket">The WebSocket to use for the connection.</param>
        /// <param name="token">A cancellation token to stop the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task SendSocketMessage(string message, WebSocket _webSocket, CancellationToken token)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
        }

        /// <summary>
        /// Receives audio data from a WebSocket connection.
        /// </summary>
        /// <param name="_webSocket">The WebSocket to use for the connection.</param>
        /// <param name="_audioDecoder">The audio decoder to process the audio stream.</param>
        /// <param name="token">A cancellation token to stop the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ReceiveAudioData(ClientWebSocket _webSocket, StreamingAudioDecoder _audioDecoder, CancellationToken token)
        {
            var receiveBuffer = new byte[8192];
            var messageBuilder = new StringBuilder();

            while (_webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), token);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    messageBuilder.Append(Encoding.UTF8.GetString(receiveBuffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        string jsonString = messageBuilder.ToString();

                        if (jsonString.Contains("\"audio\""))
                        {
                            ElevenLabsStreamedResponse response = JsonUtility.FromJson<ElevenLabsStreamedResponse>(jsonString);

                            if (!string.IsNullOrEmpty(response.audio))
                            {
                                byte[] audioBytes = Convert.FromBase64String(response.audio);

                                _audioDecoder.Feed(audioBytes, true);
                                OnAudioDataRecieved?.Invoke(this, audioBytes.ToArray());
                            }
                        }

                        messageBuilder.Clear();
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
                    break;
                }
            }
        }

        /// <summary>
        /// Connects to a WebSocket and streams audio data.
        /// TODO: add support for <c>previous_text</c> and <c>next_text</c> chunks in Elevenlabs request
        /// </summary>
        /// <param name="text">The text to be streamed as audio.</param>
        /// <param name="_webSocket">The WebSocket to use for the connection.</param>
        /// <param name="token">A cancellation token to stop the streaming.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task ConnectAndStream(string text, ClientWebSocket _webSocket, CancellationToken token)
        {
            var initialMessage = new
            {
                text = " ",
                voice_settings = _config.voiceSettings
            };
            string jsonMessage = JsonConvert.SerializeObject(initialMessage);
            await SendSocketMessage(jsonMessage, _webSocket, token);

            var textMessage = new { text = text, try_trigger_generation = true };
            jsonMessage = JsonConvert.SerializeObject(textMessage);
            await SendSocketMessage(jsonMessage, _webSocket, token);

            var flushMessage = new { text = " ", flush = true };
            jsonMessage = JsonConvert.SerializeObject(flushMessage);
            await SendSocketMessage(jsonMessage, _webSocket, token);

            //var eosMessage = new { text = "" };
            //jsonMessage = JsonConvert.SerializeObject(eosMessage);
            //await SendSocketMessage(jsonMessage, _webSocket, token);

            //await ReceiveAudioData(_webSocket, _mp3Decoder, token);
        }

        /// <summary>
        /// Sets the xi-api-key header and starts the <see cref="ReceiveAudioData(WebSocket, StreamingAudioDecoder, CancellationToken)"/> loop
        /// </summary>
        /// <param name="webSocket">The websocket that should connect to Elevenlabs</param>
        /// <param name="audioDecoder">The MP3 decoder to process the audio stream.</param>
        /// <param name="token"></param>
        public void InitWebsocket(ClientWebSocket webSocket, StreamingAudioDecoder audioDecoder, CancellationToken token)
        {
            if (webSocket.State == WebSocketState.Closed) // Reconnect WebSocket if it was closed
            {
                Uri uri = new Uri($"wss://api.elevenlabs.io/v1/text-to-speech/{_config.voiceId}/stream-input?model_id={_config.modelID}");
                Task.Run(() => webSocket.ConnectAsync(uri, token)).Wait();
                if (_recieveAudioTask != null)
                    _recieveAudioTask.Dispose();
                _recieveAudioTask = ReceiveAudioData(webSocket, audioDecoder, token);
                return;
            }
            else if (webSocket.State != WebSocketState.Open && webSocket.State != WebSocketState.Connecting) // Initialize WebSocket
            {
                webSocket.Options.SetRequestHeader("xi-api-key", _config.apiKey);
                Uri uri = new Uri($"wss://api.elevenlabs.io/v1/text-to-speech/{_config.voiceId}/stream-input?model_id={_config.modelID}");
                Task.Run(() => webSocket.ConnectAsync(uri, token)).Wait();
                if (_recieveAudioTask != null)
                    _recieveAudioTask.Dispose();
                _recieveAudioTask = ReceiveAudioData(webSocket, audioDecoder, token);
                return;
            }
            else
            {
                Debug.LogWarning("Websocket already initialized!");
                return;
            }
        }

        public async void StopStreamingAndDisconnect(ClientWebSocket webSocket, CancellationToken token = default)
        {
            var eosMessage = new { text = "" };
            var jsonMessage = JsonConvert.SerializeObject(eosMessage);
            await SendSocketMessage(jsonMessage, webSocket, token);
        }

        /// <summary>
        /// Creates a voice clone named <paramref name="voiceName"/> given a provided list of audio files.
        /// </summary>
        /// <param name="filePaths">List of file paths to upload.</param>
        /// <param name="voiceName">The name of the voice.</param>
        /// <param name="description">Optional description for the voice.</param>
        /// <param name="removeBackgroundNoise">If true, the API will attempt to clean up audio artifacts.</param>
        /// <returns>The VoiceID of the cloned voice.</returns>
        public async Task<string> CloneVoiceAndGetVoiceIDAsync(
            IEnumerable<string> filePaths,
            string voiceName,
            string description = "",
            bool removeBackgroundNoise = false)
        {
            var pathList = filePaths.ToList();

            if (pathList.Count == 0)
                throw new ArgumentException("File path list cannot be empty.", nameof(filePaths));

            // 1. Get the extension of the first file to set the standard
            string expectedExtension = Path.GetExtension(pathList[0]).TrimStart('.').ToLower();

            foreach (var path in pathList)
            {
                if (!File.Exists(path))
                    throw new FileNotFoundException($"File not found: {path}");

                // 2. CHECK: Ensure every file matches the first file's extension
                string currentExtension = Path.GetExtension(path).TrimStart('.').ToLower();
                if (currentExtension != expectedExtension)
                {
                    throw new ArgumentException($"All files must be the same type. Found mixed '{expectedExtension}' and '{currentExtension}'.");
                }
            }

            // 3. Load all files
            var audioDataTasks = pathList.Select(path => File.ReadAllBytesAsync(path));
            byte[][] audioDataArray = await Task.WhenAll(audioDataTasks);

            // 4. Handle the "mp3" -> "mpeg" mapping
            if (expectedExtension == "mp3") expectedExtension = "mpeg";

            // 5. Pass new parameters down to the core method
            return await CloneVoiceAndGetVoiceIDAsync(audioDataArray, voiceName, description, removeBackgroundNoise, expectedExtension);
        }

        /// <summary>
        /// Creates a voice clone named <paramref name="voiceName"/> given a provided <paramref name="audioDataList"/>.
        /// </summary>
        /// <param name="audioDataList">List of raw audio byte arrays.</param>
        /// <param name="voiceName">The name of the voice.</param>
        /// <param name="description">Optional description for the voice.</param>
        /// <param name="removeBackgroundNoise">If true, the API will attempt to clean up audio artifacts.</param>
        /// <param name="mediaType">The media type the audioData is (e.g., mpeg, wav).</param>
        /// <returns>The VoiceID of the cloned voice.</returns>
        public async Task<string> CloneVoiceAndGetVoiceIDAsync(
            IEnumerable<byte[]> audioDataList,
            string voiceName,
            string description = "",
            bool removeBackgroundNoise = false,
            string mediaType = "mpeg")
        {
            // Validate inputs
            if (audioDataList == null || !audioDataList.Any())
                throw new ArgumentException("Audio data list cannot be empty.", nameof(audioDataList));

            using (var client = new HttpClient())
            {
                using (var formData = new MultipartFormDataContent())
                {
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("xi-api-key", _config.apiKey);

                    // Add standard fields
                    formData.Add(new StringContent(voiceName), "name");

                    // Handle Description (ensure it's not null)
                    formData.Add(new StringContent(description ?? ""), "description");

                    // Handle Remove Background Noise (convert bool to lowercase string "true"/"false")
                    // Note: Use ToLower() because some APIs are strict about casing for boolean strings
                    formData.Add(new StringContent(removeBackgroundNoise.ToString().ToLower()), "remove_background_noise");

                    // Loop through the list and add every sample as a separate "files" entry
                    int counter = 0;
                    string extension = mediaType == "mpeg" ? "mp3" : mediaType; // cosmetic filename extension

                    foreach (var audioBytes in audioDataList)
                    {
                        var audioContent = new ByteArrayContent(audioBytes);
                        audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse($"audio/{mediaType}");

                        // Each file needs a unique filename in the form data
                        string fileName = $"sample_{counter}.{extension}";

                        // Add to form: content, field name ("files"), filename
                        formData.Add(audioContent, "files", fileName);

                        counter++;
                    }

                    // Send POST Request
                    HttpResponseMessage response = await client.PostAsync("https://api.elevenlabs.io/v1/voices/add", formData);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var json = JObject.Parse(responseBody);
                        return json["voice_id"]?.ToString();
                    }
                    else
                    {
                        throw new Exception($"Error {response.StatusCode}: {responseBody}");
                    }
                }
            }
        }
    }
}
