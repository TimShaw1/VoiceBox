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
    /// Configuration settings for the Elevenlabs Speech-to-Text (STT) service.
    /// <br></br>
    /// This class sets <see cref="GenericSTTServiceConfig.apiKeyJSONString"/> to ELEVENLABS_API_KEY by default
    /// </summary>
    [CreateAssetMenu(fileName = "ElevenlabsSTTServiceConfig", menuName = "VoiceBox/STT/ElevenlabsSTTService Configuration")]
    public class ElevenlabsSTTServiceConfig : GenericSTTServiceConfig
    {
        /// <summary>
        /// 
        /// </summary>
        public ElevenlabsSTTServiceConfig()
        {
            serviceManagerType = typeof(ElevenlabsSTTServiceManager);
            apiKeyJSONString = "ELEVENLABS_API_KEY";
        }

        /// <summary>
        /// The language for speech recognition.
        /// </summary>
        public string language = "eng";

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
        /// Whether to request timestamps when recognizing speech
        /// </summary>
        public bool include_timestamps = false;

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