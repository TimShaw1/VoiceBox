using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Generics;
using UnityEngine;
using static System.Net.WebRequestMethods;

namespace TimShaw.VoiceBox.Data
{
    /// <summary>
    /// Configuration settings for the Claude service. 
    /// <br></br>
    /// This class sets <see cref="GenericChatServiceConfig.apiKeyJSONString"/> to ANTHROPIC_API_KEY by default
    /// </summary>
    [CreateAssetMenu(fileName = "ClaudeServiceConfig", menuName = "VoiceBox/Chat/ClaudeService Configuration")]
    public class ClaudeServiceConfig : GenericChatServiceConfig
    {
        /// <summary>
        /// 
        /// </summary>
        public ClaudeServiceConfig()
        {
            serviceManagerType = typeof(ClaudeServiceManager);
            apiKeyJSONString = "ANTHROPIC_API_KEY";
        }

        /// <summary>
        /// The endpoint to send requests to. Defaults to <see href="https://api.anthropic.com/v1"/>
        /// </summary>
        public string serviceEndpoint = "https://api.anthropic.com/v1";
    }
}
