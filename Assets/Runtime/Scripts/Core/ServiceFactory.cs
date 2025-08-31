using UnityEngine;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.LLM; // Your interfaces

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

    // You would have similar methods for your other services
    // public static ISpeechToTextService CreateSttService(ScriptableObject config) { ... }
}