using UnityEngine;
using System;
using TimShaw.VoiceBox.Core;

namespace TimShaw.VoiceBox.Generics
{
    /// <summary>
    /// Generic configuration class for a TTS service. Inheriting this class enables defining your own TTS service.
    /// </summary>
    public class GenericTTSServiceConfig : ScriptableObject
    {
        /// <summary>The service manager type this config should have the <see cref="ServiceFactory"/> generate</summary>
        public Type serviceManagerType;

        /// <summary>
        /// The API key for the service.
        /// </summary>
        public string apiKey;

        /// <summary>
        /// The associated string to access the service's API key in keys.json
        /// </summary>
        public string apiKeyJSONString;

        /// <summary>
        /// Internal Init meant for modding support
        /// </summary>
        public virtual void _Init()
        {

        }
    }

}
