using System;
using System.Collections.Generic;
using UnityEngine;
using TimShaw.VoiceBox.Core;

public class AIManager : MonoBehaviour
{
    // --- Singleton Pattern ---
    // This makes the manager globally accessible via AIManager.Instance
    public static AIManager Instance { get; private set; }

    // --- Configuration ---
    // The user will drag their ScriptableObject configuration assets here in the Inspector.
    [Header("Service Configurations")]
    [Tooltip("Configuration asset for the chat service (e.g., GeminiConfig, ChatGPTConfig).")]
    [SerializeField] private ScriptableObject chatServiceConfig;

    // [SerializeField] private ScriptableObject speechToTextConfig;
    // [SerializeField] private ScriptableObject textToSpeechConfig;

    // --- Private Service References ---
    // These hold the actual instances of the service classes.
    private IChatService _chatService;
    // private ISpeechToTextService _sttService;
    // private ITextToSpeechService _ttsService;

    private void Awake()
    {
        // Standard singleton setup to ensure only one instance exists.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Optional: keeps the manager alive between scenes

        // --- Initialization ---
        // Use a "Factory" to create the correct service instance based on the config file.
        _chatService = ServiceFactory.CreateChatService(chatServiceConfig);
        // _sttService = ServiceFactory.CreateSttService(speechToTextConfig);
        // _ttsService = ServiceFactory.CreateTtsService(textToSpeechConfig);
    }

    // --- Public Methods (The API for game developers) ---
    // These methods are the clean, public functions that other scripts will call.

    /// <summary>
    /// Sends a conversation history to the configured chat service.
    //  This is the method developers will call from their game scripts.
    /// </summary>
    public void SendChatMessage(
        List<ChatMessage> messageHistory,
        Action<ChatMessage> onSuccess,
        Action<string> onError)
    {
        if (_chatService == null)
        {
            onError?.Invoke("Chat service is not initialized. Check AIManager configuration.");
            return;
        }

        // The manager delegates the call to the actual service instance it's holding.
        _chatService.SendMessage(messageHistory, onSuccess, onError);
    }
}