using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.CoreAudioApi;
using TimShaw.VoiceBox.Core;
using UnityEngine;

namespace TimShaw.VoiceBox.STT
{
    class AzureSTTServiceManager : ISpeechToTextService
    {
        public SpeechRecognizer speechRecognizer;
        private Dictionary<string, string> audioEndpoints;
        public async Task TranscribeAudioFromMic(CancellationToken token) // 1. Accept the token
        {
            try
            {
                Debug.Log("VoiceBox: Start transcribing audio...");

                var stopRecognition = new TaskCompletionSource<int>();

                // 2. Register a callback. When the token is cancelled, 
                //    it will complete our TaskCompletionSource. This is the key change.
                token.Register(() => stopRecognition.TrySetResult(0));

                await speechRecognizer.StartContinuousRecognitionAsync();

                // 3. Await the task properly instead of using WaitAny()
                await stopRecognition.Task;

                // 4. Ensure recognition is explicitly stopped for a clean shutdown.
                await speechRecognizer.StopContinuousRecognitionAsync();
                Debug.Log("VoiceBox: Transcription stopped.");
            }
            catch (System.Exception ex)
            {
                Debug.Log("STT BROKE");
                Debug.Log(ex.ToString());
            }
        }

        public void Initialize(ScriptableObject config)
        {
            var speechServiceObjectDerived = config as AzureSTTServiceConfig;
            if (speechServiceObjectDerived.apiKey.Length == 0)
            {
                Debug.Log("No API key. STT disabled.");
                return;
            }

            audioEndpoints = GetAudioInputEndpoints();

            using var audioConfig = (speechServiceObjectDerived.audioInputDeviceName == "Default") ? 
                AudioConfig.FromDefaultMicrophoneInput() : AudioConfig.FromMicrophoneInput(audioEndpoints[speechServiceObjectDerived.audioInputDeviceName]);
            var speechConfig = SpeechConfig.FromSubscription(speechServiceObjectDerived.apiKey, speechServiceObjectDerived.region);
            speechConfig.SpeechRecognitionLanguage = speechServiceObjectDerived.language;
            speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
        }

        public static Dictionary<string, string> GetAudioInputEndpoints()
        {
            var deviceList = new Dictionary<string, string>();
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();

            deviceList.Add("Default", "");

            foreach (var device in devices)
            {
                deviceList.Add(device.FriendlyName, device.ID);
            }

            return deviceList;
        }
    }
}
