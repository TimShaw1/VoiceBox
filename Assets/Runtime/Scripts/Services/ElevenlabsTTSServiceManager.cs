using System;
using System.Collections.Generic;
using System.Text;
using NAudio.Wave;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using UnityEngine;
using TimShaw.VoiceBox.Core;


namespace TimShaw.VoiceBox.TTS
{
    class ElevenLabsTTSServiceManager : ITextToSpeechService
    {

        HttpClient client;
        private ElevenlabsTTSServiceConfig ttsServiceObjectDerived;
        private string fileExtension;

        [System.Serializable]
        public class ElevenLabsTTSRequest
        {
            public string text;
            public string model_id;
            public VoiceSettings voice_settings;
        }

        public void Initialize(ScriptableObject config)
        {

            ttsServiceObjectDerived = config as ElevenlabsTTSServiceConfig;
            if (ttsServiceObjectDerived.apiKey.Length == 0)
            {
                Debug.Log("No Elevenlabs API key found.");
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
        public async Task RequestAudio(string prompt, string fileName, string dir)
        {
            string url = ttsServiceObjectDerived.serviceEndpoint + ttsServiceObjectDerived.voiceID; // Concatenate Voice ID to end of URL
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
                using (FileStream fileStream = File.Create(dir + fileName.ToString() + fileExtension))
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

    }
}
