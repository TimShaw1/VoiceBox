using Microsoft.CognitiveServices.Speech;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Generics;
using UnityEngine;

namespace TimShaw.VoiceBox.Core
{
    /// <summary>
    /// Defines the contract for any speech-to-text (STT) service.
    /// This interface provides a standardized way to initialize and perform audio transcription.
    /// </summary>
    public interface ISpeechToTextService
    {
        /// <summary>
        /// Initializes the service with the necessary configuration.
        /// </summary>
        /// <param name="config">A ScriptableObject containing API keys and other settings.</param>
        void Initialize(GenericSTTServiceConfig config);

        /// <summary>
        /// Starts transcribing audio from the microphone.
        /// </summary>
        /// <param name="token">A cancellation token to stop the transcription.</param>
        /// <returns>A task that represents the asynchronous transcription operation.</returns>
        Task TranscribeAudioFromMic(CancellationToken token);

        /// <summary>
        /// Occurs when the speech recognizer is processing audio and has an intermediate result.
        /// </summary>
        public event System.EventHandler<SpeechRecognitionEventArgs> OnRecognizing;
        /// <summary>
        /// Occurs when the speech recognizer has finished processing an audio stream and has a final result.
        /// </summary>
        public event System.EventHandler<SpeechRecognitionEventArgs> OnRecognized;
        /// <summary>
        /// Occurs when the speech recognizer has been canceled.
        /// </summary>
        public event System.EventHandler<SpeechRecognitionCanceledEventArgs> OnCanceled;
        /// <summary>
        /// Occurs when a recognition session has started.
        /// </summary>
        public event System.EventHandler<SessionEventArgs> OnSessionStarted;
        /// <summary>
        /// Occurs when a recognition session has stopped.
        /// </summary>
        public event System.EventHandler<SessionEventArgs> OnSessionStopped;
        /// <summary>
        /// Occurs when the start of a speech segment is detected.
        /// </summary>
        public event System.EventHandler<RecognitionEventArgs> OnSpeechStartDetected;
        /// <summary>
        /// Occurs when the end of a speech segment is detected.
        /// </summary>
        public event System.EventHandler<RecognitionEventArgs> OnSpeechEndDetected;
    }
}