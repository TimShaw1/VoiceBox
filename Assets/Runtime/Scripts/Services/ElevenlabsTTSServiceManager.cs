using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Components;
using TimShaw.VoiceBox.Generics;
using TimShaw.VoiceBox.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace TimShaw.VoiceBox.Core
{
    /// <summary>
    /// Manages the ElevenLabs Text-to-Speech (TTS) service.
    /// </summary>
    public class ElevenLabsTTSServiceManager : ITextToSpeechService
    {

        private HttpClient client;
        /// <summary>
        /// The configuration for the ElevenLabs TTS service.
        /// </summary>
        private ElevenlabsTTSServiceConfig _config;
        private string fileExtension;

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
                Debug.LogError("No Elevenlabs API key found.");
                return;
            }

            if (_config.voiceId.Length == 0)
            {
                Debug.LogError("No Elevenlabs Voice ID found.");
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
        /// <param name="fileName">The name of the output audio file.</param>
        /// <param name="dir">The directory to save the audio file in.</param>
        /// <param name="token"></param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RequestAudioFile(string prompt, string fileName, string dir, CancellationToken token)
        {
            string url = _config.serviceEndpoint + _config.voiceId;
            Debug.Log(url);

            var payload = new ElevenLabsTTSRequest
            {
                text = prompt,
                model_id = _config.modelID,
                voice_settings = _config.voiceSettings
            };

            string json = JsonUtility.ToJson(payload);
            StringContent httpContent = new StringContent(json, System.Text.Encoding.Default, "application/json");

            Debug.Log("Requesting audio...");

            try
            {
                var response = await client.PostAsync(url, httpContent);
                Debug.Log("Got response...");
                response.EnsureSuccessStatusCode();
                Debug.Log("Success...");

                using (Stream stream = await response.Content.ReadAsStreamAsync())
                using (FileStream fileStream = System.IO.File.Create(dir + fileName.ToString() + fileExtension))
                {
                    await stream.CopyToAsync(fileStream);
                    Debug.Log("Streamed");
                }
            }
            catch (Exception ex)
            {
                Debug.Log(ex.ToString());
            }

            Debug.Log(dir + fileName.ToString() + fileExtension);

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
            Debug.Log("Requesting audio from: " + url);

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
        /// <param name="_mp3Decoder">The MP3 decoder to process the audio stream.</param>
        /// <param name="token">A cancellation token to stop the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ReceiveAudioData(WebSocket _webSocket, StreamingMp3Decoder _mp3Decoder, CancellationToken token)
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

                                _mp3Decoder.Feed(audioBytes);
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
        /// <param name="_mp3Decoder">The MP3 decoder to process the audio stream.</param>
        /// <param name="token">A cancellation token to stop the streaming.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task ConnectAndStream(string text, WebSocket _webSocket, StreamingMp3Decoder _mp3Decoder, CancellationToken token)
        {
            var initialMessage = new { text = " " };
            string jsonMessage = JsonConvert.SerializeObject(initialMessage);
            Debug.Log(jsonMessage);
            await SendSocketMessage(jsonMessage, _webSocket, token);

            var textMessage = new { text = text, try_trigger_generation = true };
            jsonMessage = JsonConvert.SerializeObject(textMessage);
            Debug.Log(jsonMessage);
            await SendSocketMessage(jsonMessage, _webSocket, token);

            var eosMessage = new { text = "" };
            jsonMessage = JsonConvert.SerializeObject(eosMessage);
            Debug.Log(jsonMessage);
            await SendSocketMessage(jsonMessage, _webSocket, token);

            await ReceiveAudioData(_webSocket, _mp3Decoder, token);
        }

        /// <summary>
        /// Sets the xi-api-key header and returns the generation endpoint Uri of the <see cref="ElevenlabsTTSServiceConfig.voiceId"/>
        /// </summary>
        /// <param name="webSocket">The websocket that should connect to Elevenlabs</param>
        /// <returns>The endpoint Uri of the configured voiceId on Elevenlabs</returns>
        public Uri InitWebsocketAndGetUri(ClientWebSocket webSocket)
        {
            webSocket.Options.SetRequestHeader("xi-api-key", _config.apiKey);
            return new Uri($"wss://api.elevenlabs.io/v1/text-to-speech/{_config.voiceId}/stream-input?model_id=eleven_multilingual_v2");
        }
    }
}
