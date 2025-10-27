using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Generics;
using UnityEngine;
using System.Collections.Generic;

namespace TimShaw.VoiceBox.Data
{
    /// <summary>
    /// Configuration settings for the ChatGPT chat service.
    /// <br></br>
    /// This class sets <see cref="GenericChatServiceConfig.apiKeyJSONString"/> to OPENAI_API_KEY by default
    /// </summary>
    [CreateAssetMenu(fileName = "ChatGPTServiceConfig", menuName = "VoiceBox/Chat/ChatGPTService Configuration")]
    class ChatGPTServiceConfig : GenericChatServiceConfig
    {
        public ChatGPTServiceConfig()
        {
            serviceManagerType = typeof(ChatGPTServiceManager);
            apiKeyJSONString = "OPENAI_API_KEY";
        }

        /// <summary>
        /// The service endpoint for the Gemini API.
        /// </summary>
        public string serviceEndpoint = "https://api.openai.com/v1";
    }
}