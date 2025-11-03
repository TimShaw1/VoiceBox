using UnityEngine;
using System.Collections.Generic;
using System;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Generics;
using OpenAI.Chat;

namespace TimShaw.VoiceBox.Data
{

    /// <summary>
    /// Configuration settings for the Gemini service. 
    /// <br></br>
    /// This class sets <see cref="GenericChatServiceConfig.apiKeyJSONString"/> to GEMINI_API_KEY by default
    /// <br></br>
    /// TODO: add support for tools like URL context
    /// </summary>
    [CreateAssetMenu(fileName = "GeminiServiceConfig", menuName = "VoiceBox/Chat/GeminiService Configuration")]
    public class GeminiServiceConfig : GenericChatServiceConfig
    {
        /// <summary>
        /// 
        /// </summary>
        public GeminiServiceConfig()
        {
            serviceManagerType = typeof(GeminiServiceManager);
            apiKeyJSONString = "GEMINI_API_KEY";
            modelName = "gemini-2.5-flash";
        }

        /// <summary>
        /// The service endpoint for the Gemini API.
        /// </summary>
        public string serviceEndpoint = "https://generativelanguage.googleapis.com/v1beta/openai/";
    }
}