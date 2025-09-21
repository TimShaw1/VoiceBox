using UnityEngine;
using TimShaw.VoiceBox.Core;

namespace TimShaw.VoiceBox.Core
{
    /// <summary>
    /// A factory class for creating AI service instances based on configuration files.
    /// This class abstracts the creation logic for different service implementations.
    /// </summary>
    public static class ServiceFactory
    {
        /// <summary>
        /// Creates a chat service instance based on the provided configuration.
        /// </summary>
        /// <param name="config">The ScriptableObject configuration for the chat service.</param>
        /// <returns>An initialized chat service instance, or null if the configuration is invalid.</returns>
        public static IChatService CreateChatService(ScriptableObject config)
        {
            if (config is GeminiServiceConfig)
            {
                IChatService service = new GeminiServiceManager();
                service.Initialize(config);
                return service;
            }
            else
            {
                if (config != null)
                {
                    Debug.LogError($"[ServiceFactory] Unknown chat config type: {config.GetType().Name}");
                }
                else
                {
                    Debug.LogWarning("[ServiceFactory] Chat service config is null. No chat service will be created.");
                }
                return null;
            }
        }

        /// <summary>
        /// Creates a speech-to-text (STT) service instance based on the provided configuration.
        /// </summary>
        /// <param name="config">The ScriptableObject configuration for the STT service.</param>
        /// <returns>An initialized STT service instance, or null if the configuration is invalid.</returns>
        public static ISpeechToTextService CreateSttService(ScriptableObject config)
        {
            if (config is AzureSTTServiceConfig)
            {
                ISpeechToTextService service = new AzureSTTServiceManager();
                service.Initialize(config);
                return service;
            }
            else
            {
                if (config != null)
                {
                    Debug.LogError($"[ServiceFactory] Unknown STT config type: {config.GetType().Name}");
                }
                else
                {
                    Debug.LogWarning("[ServiceFactory] STT service config is null. No STT service will be created.");
                }
                return null;
            }
        }

        /// <summary>
        /// Creates a text-to-speech (TTS) service instance based on the provided configuration.
        /// </summary>
        /// <param name="config">The ScriptableObject configuration for the TTS service.</param>
        /// <returns>An initialized TTS service instance, or null if the configuration is invalid.</returns>
        public static ITextToSpeechService CreateTtsService(ScriptableObject config)
        {
            if (config is ElevenlabsTTSServiceConfig)
            {
                ITextToSpeechService service = new ElevenLabsTTSServiceManager();
                service.Initialize(config);
                return service;
            }
            else
            {
                if (config != null)
                {
                    Debug.LogError($"[ServiceFactory] Unknown TTS config type: {config.GetType().Name}");
                }
                else
                {
                    Debug.LogWarning("[ServiceFactory] TTS service config is null. No TTS service will be created.");
                }
                return null;
            }
        }
    }
}