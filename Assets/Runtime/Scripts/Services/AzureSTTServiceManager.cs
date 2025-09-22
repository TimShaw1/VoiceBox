using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.CoreAudioApi;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Generics;
using UnityEngine;

namespace TimShaw.VoiceBox.Core
{
    /// <summary>
    /// Manages the Azure Speech-to-Text (STT) service.
    /// </summary>
    public class AzureSTTServiceManager : ISpeechToTextService
    {
        /// <summary>
        /// The SpeechRecognizer instance used for speech recognition.
        /// </summary>
        public SpeechRecognizer speechRecognizer;
        private Dictionary<string, string> audioEndpoints;

        /// <summary>
        /// Transcribes audio from the microphone using the Azure STT service.
        /// </summary>
        /// <param name="token">A cancellation token to stop the transcription.</param>
        /// <returns>A task representing the asynchronous transcription operation.</returns>
        public async Task TranscribeAudioFromMic(CancellationToken token)
        {
            try
            {
                Debug.Log("VoiceBox: Start transcribing audio...");

                var stopRecognition = new TaskCompletionSource<int>();

                token.Register(() => stopRecognition.TrySetResult(0));

                await speechRecognizer.StartContinuousRecognitionAsync();

                await stopRecognition.Task;

                await speechRecognizer.StopContinuousRecognitionAsync();
                Debug.Log("VoiceBox: Transcription stopped.");
            }
            catch (System.Exception ex)
            {
                Debug.Log("STT BROKE");
                Debug.Log(ex.ToString());
            }
        }

        /// <summary>
        /// Initializes the Azure STT service with the provided configuration.
        /// </summary>
        /// <param name="config">The ScriptableObject configuration for the Azure STT service.</param>
        public void Initialize(GenericSTTServiceConfig config)
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

        /// <summary>
        /// Gets a dictionary of available audio input endpoints.
        /// </summary>
        /// <returns>A dictionary where the key is the friendly name of the device and the value is the device ID.</returns>
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
