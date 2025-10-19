using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Generics;
using UnityEngine;

namespace TimShaw.VoiceBox.Data
{
    [CreateAssetMenu(fileName = "OllamaServiceConfig", menuName = "VoiceBox/Chat/OllamaService Configuration")]
    public class OllamaChatServiceConfig : GenericChatServiceConfig
    {
        public OllamaChatServiceConfig()
        {
            serviceManagerType = typeof(OllamaChatServiceManager);
            apiKeyJSONString = "";
        }

        public string serviceEndpoint = "http://localhost:11434/";
    }
}
