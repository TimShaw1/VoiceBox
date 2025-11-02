using UnityEngine;
using TimShaw.VoiceBox.Generics;
using System;

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
        public static IChatService CreateChatService(GenericChatServiceConfig config)
        {
            try
            {
                Type t = config.serviceManagerType;
                IChatService service = Activator.CreateInstance(t) as IChatService;
                service.Initialize(config);
                return service;
            }
            catch (Exception ex)
            {
                if (config != null)
                {
                    if (config.modelName.Length == 0)
                        Debug.LogError("[ServiceFactory] No model name specified for chat model.");

                    if (config.serviceManagerType != null && ex.GetType() == typeof(NullReferenceException))
                        Debug.LogError($"[ServiceFactory] Unknown chat config type: {config.GetType().Name}. Does it implement IChatService?");
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
        public static ISpeechToTextService CreateSttService(GenericSTTServiceConfig config)
        {
            try
            {
                Type t = config.serviceManagerType;
                ISpeechToTextService service = Activator.CreateInstance(t) as ISpeechToTextService;
                service.Initialize(config);
                return service;
            }
            catch
            {
                if (config != null)
                {
                    if (config.serviceManagerType != null)
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
        public static ITextToSpeechService CreateTtsService(GenericTTSServiceConfig config)
        {
            try
            {
                Type t = config.serviceManagerType;
                ITextToSpeechService service = Activator.CreateInstance(t) as ITextToSpeechService;
                service.Initialize(config);
                return service;
            }
            catch
            {
                if (config != null)
                {
                    if (config.serviceManagerType != null)
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