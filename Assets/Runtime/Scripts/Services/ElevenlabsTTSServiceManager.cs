using NAudio.Wave;
using Newtonsoft.Json;
using OpenCover.Framework.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Core;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;
using static AudioStreamer;


namespace TimShaw.VoiceBox.TTS
{
    class ElevenLabsTTSServiceManager : ITextToSpeechService
    {

        HttpClient client;
        public ElevenlabsTTSServiceConfig ttsServiceObjectDerived;
        private string fileExtension;

        [System.Serializable]
        public class ElevenLabsTTSRequest
        {
            public string text;
            public string model_id;
            public VoiceSettings voice_settings;
        }

        [System.Serializable]
        public class ElevenLabsStreamedResponse
        {
            public string audio;
            public bool isFinal;
            // We can ignore the alignment data for now if we don't need it
        }

        public void Initialize(ScriptableObject config)
        {

            ttsServiceObjectDerived = config as ElevenlabsTTSServiceConfig;
            if (ttsServiceObjectDerived.apiKey.Length == 0)
            {
                Debug.LogError("No Elevenlabs API key found.");
                return;
            }

            if (ttsServiceObjectDerived.voiceId.Length == 0)
            {
                Debug.LogError("No Elevenlabs Voice ID found.");
                return;
            }

            client = new HttpClient();

            fileExtension = ttsServiceObjectDerived.output_format.Contains("mp3") ? ".mp3" : ".wav";

            client.DefaultRequestHeaders.Add("xi-api-key", ttsServiceObjectDerived.apiKey); // Add API Key header
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg")); // Add accepted file extension header
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/wav")); // Add accepted file extension header

        }

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
        /// <para>
        /// Requests file containing AI Voice saying the prompt and outputs the directory to said file.
        /// </para>
        /// <para>
        /// TODO: Stream audio into audioclip, allow previous and next text parameters, allow previous and next ID parameters
        /// </para>
        /// </summary>
        /// <param name="prompt"></param>
        /// <param name="fileName"></param>
        /// <param name="dir"></param>
        /// <returns></returns>
        public async Task RequestAudioFile(string prompt, string fileName, string dir)
        {
            string url = ttsServiceObjectDerived.serviceEndpoint + ttsServiceObjectDerived.voiceId; // Concatenate Voice ID to end of URL
            Debug.Log(url);

            var payload = new ElevenLabsTTSRequest
            {
                text = prompt,
                model_id = ttsServiceObjectDerived.modelID,
                voice_settings = ttsServiceObjectDerived.voiceSettings
            };

            // Convert Data to JSON
            string json = JsonUtility.ToJson(payload);
            StringContent httpContent = new StringContent(json, System.Text.Encoding.Default, "application/json");

            // Request MPEG
            Debug.Log("Requesting audio...");

            try
            {
                var response = await client.PostAsync(url, httpContent);
                Debug.Log("Got response...");
                response.EnsureSuccessStatusCode();
                Debug.Log("Success...");

                // Stream response as binary data into a file
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

        public async Task<AudioClip> RequestAudioClip(string prompt)
        {
            string url = ttsServiceObjectDerived.serviceEndpoint + ttsServiceObjectDerived.voiceId;
            Debug.Log("Requesting audio from: " + url);

            var payload = new ElevenLabsTTSRequest
            {
                text = prompt,
                model_id = ttsServiceObjectDerived.modelID,
                voice_settings = ttsServiceObjectDerived.voiceSettings
            };

            string json = JsonUtility.ToJson(payload);
            byte[] postData = System.Text.Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(postData);
                www.downloadHandler = new DownloadHandlerAudioClip(new Uri(url), AudioType.MPEG);
                www.SetRequestHeader("Content-Type", "application/json");
                // Add any other necessary headers, like authentication, here
                www.SetRequestHeader("xi-api-key", ttsServiceObjectDerived.apiKey);

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

        // --- STREAMING ---
        private async Task SendSocketMessage(string message, WebSocket _webSocket, CancellationToken token)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
        }

        private async Task ReceiveAudioData(WebSocket _webSocket, StreamingMp3Decoder _mp3Decoder, CancellationToken token)
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

        public async Task ConnectAndStream(string text, WebSocket _webSocket, StreamingMp3Decoder _mp3Decoder, CancellationToken token)
        {
            var initialMessage = new { text = " " };
            string jsonMessage = JsonConvert.SerializeObject(initialMessage);
            Debug.Log(jsonMessage);
            await SendSocketMessage(jsonMessage, _webSocket, token);

            // 2. Send the text to be spoken
            var textMessage = new { text = text, try_trigger_generation = true };
            jsonMessage = JsonConvert.SerializeObject(textMessage);
            Debug.Log(jsonMessage);
            await SendSocketMessage(jsonMessage, _webSocket, token);

            // 3. Send the End of Stream message
            var eosMessage = new { text = "" };
            jsonMessage = JsonConvert.SerializeObject(eosMessage);
            Debug.Log(jsonMessage);
            await SendSocketMessage(jsonMessage, _webSocket, token);

            // 4. Start listening for audio data
            //await ReceiveAudioData(token);
            await ReceiveAudioData(_webSocket, _mp3Decoder, token);
        }

    }
}
