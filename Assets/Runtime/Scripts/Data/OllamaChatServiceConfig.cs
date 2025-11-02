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
    /// <summary>
    /// Configuration settings for the Ollama service. 
    /// <br></br>
    /// This class sets <see cref="GenericChatServiceConfig.apiKeyJSONString"/> to OLLAMA_API_KEY by default
    /// </summary>
    [CreateAssetMenu(fileName = "OllamaServiceConfig", menuName = "VoiceBox/Chat/OllamaService Configuration")]
    public class OllamaChatServiceConfig : GenericChatServiceConfig
    {
        /// <summary>
        /// 
        /// </summary>
        public OllamaChatServiceConfig()
        {
            serviceManagerType = typeof(OllamaChatServiceManager);
            apiKeyJSONString = "OLLAMA_API_KEY";
        }

        /// <summary>
        /// The endpoint to send requests to. Defaults to <see href="http://localhost:11434/"/>
        /// </summary>
        public string serviceEndpoint = "http://localhost:11434/";
    }
}
