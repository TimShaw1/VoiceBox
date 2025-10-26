

using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Generics;
using UnityEngine;

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
    }

}