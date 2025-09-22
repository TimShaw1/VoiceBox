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
    }
}