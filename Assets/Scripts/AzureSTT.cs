using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.CoreAudioApi;
using UnityEngine;

namespace VoiceBox
{
    class AzureSTT
    {
        public static bool is_init = false;
        public static SpeechRecognizer speechRecognizer;
        async static Task FromMic(CancellationToken token) // 1. Accept the token
        {
            try
            {
                Debug.Log("VoiceBox: Start transcribing audio...");

                var stopRecognition = new TaskCompletionSource<int>();

                // 2. Register a callback. When the token is cancelled, 
                //    it will complete our TaskCompletionSource. This is the key change.
                token.Register(() => stopRecognition.TrySetResult(0));

                speechRecognizer.Recognizing += (s, e) =>
                {
                    //Debug.Log($"RECOGNIZING: Text={e.Result.Text}");
                };

                speechRecognizer.Recognized += (s, e) =>
                {
                    if (e.Result.Reason == ResultReason.RecognizedSpeech)
                    {
                        if (e.Result.Text.Length > 1)
                        {
                            // Use recognized text
                            Debug.Log(e.Result.Text);
                        }
                    }
                    else if (e.Result.Reason == ResultReason.NoMatch)
                    {
                        Debug.Log($"NOMATCH: Speech could not be recognized.");
                    }
                };

                speechRecognizer.Canceled += (s, e) =>
                {
                    Debug.Log($"CANCELED: Reason={e.Reason}");
                    if (e.Reason == CancellationReason.Error)
                    {
                        Debug.Log($"CANCELED: ErrorCode={e.ErrorCode}");
                        Debug.Log($"CANCELED: ErrorDetails={e.ErrorDetails}");
                    }
                    stopRecognition.TrySetResult(0);
                };

                speechRecognizer.SessionStopped += (s, e) =>
                {
                    Debug.Log("\n    Session stopped event.");
                    stopRecognition.TrySetResult(0);
                };

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

        public async static Task Main(CancellationToken token)
        {
            //Debug.Log("IN MAIN");
            try
            {
                token.ThrowIfCancellationRequested();
                await FromMic(token);
            }
            catch (System.Exception ex)
            {
                Debug.Log(ex.ToString());
            }
        }

        public static void Init(string api_key, string region, string language)
        {
            if (api_key.Length == 0)
            {
                Debug.Log("No API key. STT disabled.");
                return;
            }

            using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            var speechConfig = SpeechConfig.FromSubscription(api_key, region);
            speechConfig.SpeechRecognitionLanguage = language;
            speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
            is_init = true;
        }

        static void GetAudioDevices(string[] args)
        {
            var enumerator = new MMDeviceEnumerator();
            foreach (var endpoint in
            enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                Debug.Log($"{endpoint.FriendlyName} ({endpoint.ID})");
            }
        }
    }
}
