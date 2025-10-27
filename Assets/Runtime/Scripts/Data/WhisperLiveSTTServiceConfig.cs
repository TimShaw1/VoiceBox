

using System.Linq;
using TimShaw.VoiceBox.Core;
#if UNITY_EDITOR
using TimShaw.VoiceBox.Editor;
#endif
using TimShaw.VoiceBox.Generics;
using UnityEngine;
using static TimShaw.VoiceBox.Core.STTUtils;

namespace TimShaw.VoiceBox.Data
{
    /// <summary>
    /// Configuration settings for the WhisperLive Speech-to-Text (STT) service.
    /// <br></br>
    /// This class sets <see cref="GenericSTTServiceConfig.apiKeyJSONString"/> to nothing by default
    /// </summary>
    [CreateAssetMenu(fileName = "WhisperLiveSTTServiceConfig", menuName = "VoiceBox/STT/WhisperLiveSTTService Configuration")]
    public class WhisperLiveServiceConfig : GenericSTTServiceConfig
    {
        public WhisperLiveServiceConfig()
        {
            serviceManagerType = typeof(WhisperLiveServiceManager);
            apiKeyJSONString = "";
        }

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