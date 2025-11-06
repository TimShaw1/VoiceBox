using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Data;
using TimShaw.VoiceBox.Generics;
using UnityEngine;
using static TimShaw.VoiceBox.Core.STTUtils;

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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public event EventHandler<VoiceBoxSpeechRecognitionEventArgs> OnRecognizing;
        public event EventHandler<VoiceBoxSpeechRecognitionEventArgs> OnRecognized;
        public event EventHandler<SpeechRecognitionCanceledEventArgs> OnCanceled;
        public event EventHandler<SessionEventArgs> OnSessionStarted;
        public event EventHandler<SessionEventArgs> OnSessionStopped;
        public event EventHandler<RecognitionEventArgs> OnSpeechStartDetected;
        public event EventHandler<RecognitionEventArgs> OnSpeechEndDetected;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        /// <summary>
        /// Transcribes audio from the microphone using the Azure STT service.
        /// </summary>
        /// <param name="token">A cancellation token to stop the transcription.</param>
        /// <returns>A task representing the asynchronous transcription operation.</returns>
        public async Task TranscribeAudioFromMic(CancellationToken token)
        {
            try
            {
                var stopRecognition = new TaskCompletionSource<int>();

                token.Register(() => stopRecognition.TrySetResult(0));

                await speechRecognizer.StartContinuousRecognitionAsync();

                await stopRecognition.Task;

                await speechRecognizer.StopContinuousRecognitionAsync();
                Debug.Log("Azure Service Manager: Transcription stopped.");
            }
            catch (System.Exception ex)
            {
                Debug.Log("Azure Service Manager: Speech to Text error");
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

            AudioConfig audioConfig;
            try
            {
                audioConfig = (speechServiceObjectDerived.audioInputDeviceName == "Default") ?
                    AudioConfig.FromDefaultMicrophoneInput() : AudioConfig.FromMicrophoneInput(audioEndpoints[speechServiceObjectDerived.audioInputDeviceName]);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Azure Service Manager: AudioConfig error: " + ex.Message + " -- Using default microphone.");
                audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            }
            var speechConfig = SpeechConfig.FromSubscription(speechServiceObjectDerived.apiKey, speechServiceObjectDerived.region);
            speechConfig.SpeechRecognitionLanguage = speechServiceObjectDerived.language;
            speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
            InitSpeechRecognizer();
        }

        /// <summary>
        /// Wires up public events to internal speech recognizer
        /// </summary>
        private void InitSpeechRecognizer()
        {
            speechRecognizer.Recognizing += (s, e) =>
            {
                Debug.Log($"Azure Service Manager: Recognizing: {e.Result.Text}");
                OnRecognizing?.Invoke(this, (VoiceBoxSpeechRecognitionEventArgs)e);
            };

            speechRecognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    Debug.Log($"Azure Service Manager: Recognized: {e.Result.Text}");
                }
                else if (e.Result.Reason == ResultReason.NoMatch)
                {
                    Debug.Log($"Azure Service Manager: No match.");
                }
                OnRecognized?.Invoke(this, (VoiceBoxSpeechRecognitionEventArgs)e);
            };

            speechRecognizer.Canceled += (s, e) =>
            {
                Debug.Log($"Azure Service Manager: CANCELED: Reason={e.Reason}");
                OnCanceled?.Invoke(this, e);
            };

            speechRecognizer.SessionStarted += (s, e) =>
            {
                Debug.Log($"Azure Service Manager: Session Started.");
                OnSessionStarted?.Invoke(this, e);
            };

            speechRecognizer.SessionStopped += (s, e) =>
            {
                Debug.Log($"Azure Service Manager: Session Stopped.");
                OnSessionStopped?.Invoke(this, e);
            };

            speechRecognizer.SpeechStartDetected += (s, e) =>
            {
                // Debug.Log($"Azure Service Manager: Speech Start Detected.");
                OnSpeechStartDetected?.Invoke(this, e);
            };

            speechRecognizer.SpeechEndDetected += (s, e) =>
            {
                // Debug.Log($"Azure Service Manager: Speech End Detected.");
                OnSpeechEndDetected?.Invoke(this, e);
            };
        }
    }
}
