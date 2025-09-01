using UnityEngine;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.LLM;
using TimShaw.VoiceBox.STT; // Your interfaces

public static class ServiceFactory
{
    public static IChatService CreateChatService(ScriptableObject config)
    {
        // Check the type of the ScriptableObject config file.
        if (config is ChatServiceConfig)
        {
            // If it's a GeminiConfig, create a GeminiChatService.
            IChatService service = new GeminiServiceManager();
            service.Initialize(config);
            return service;
        }
        else
        {
            // If no valid config is provided, log an error.
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
            // If no valid config is provided, log an error.
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
}