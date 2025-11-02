using System.Linq;
using UnityEngine;
#if UNITY_EDITOR 
using TimShaw.VoiceBox.Editor;
#endif
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Generics;
using static TimShaw.VoiceBox.Core.STTUtils;

namespace TimShaw.VoiceBox.Data
{

    /// <summary>
    /// Configuration settings for the Azure Speech-to-Text (STT) service.
    /// <br></br>
    /// This class sets <see cref="GenericSTTServiceConfig.apiKeyJSONString"/> to AZURE_SPEECH_API_KEY by default
    /// </summary>
    [CreateAssetMenu(fileName = "AzureSTTServiceConfig", menuName = "VoiceBox/STT/AzureSTTService Configuration")]
    public class AzureSTTServiceConfig : GenericSTTServiceConfig
    {
        /// <summary>
        /// 
        /// </summary>
        public AzureSTTServiceConfig()
        {
            serviceManagerType = typeof(AzureSTTServiceManager);
            apiKeyJSONString = "AZURE_SPEECH_API_KEY";
        }

        /// <summary>
        /// The region for the Azure STT service.
        /// </summary>
        public string region = "canadacentral";
        /// <summary>
        /// The language for speech recognition.
        /// </summary>
        public string language = "en-CA";

        /// <summary>
        /// The name of the audio input device to use for transcription.
        /// </summary>
        #if UNITY_EDITOR
        [Options("audioInputEndpointNames")]
        #endif
        public string audioInputDeviceName = "Default";

        /// <summary>
        /// An array of available audio input device names.
        /// </summary>
        public string[] audioInputEndpointNames = { "Default" };

        /// <summary>
        /// Called when the script is loaded or a value is changed in the Inspector.
        /// Populates the list of available audio input devices.
        /// </summary>
        public void OnValidate()
        {
            audioInputEndpointNames = GetAudioInputEndpoints().Keys.ToArray();
        }
    }
}