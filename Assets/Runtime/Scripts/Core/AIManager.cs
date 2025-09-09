using Microsoft.CognitiveServices.Speech;
using System;
using System.Collections.Generic;
using System.Threading;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.STT;
using UnityEngine;

public class AIManager : MonoBehaviour
{
    // --- Singleton Pattern ---
    // This makes the manager globally accessible via AIManager.Instance
    public static AIManager Instance { get; private set; }
    public SpeechRecognizer speechRecognizer;

    private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    // --- Configuration ---
    // The user will drag their ScriptableObject configuration assets here in the Inspector.
    [Header("Service Configurations")]
    [Tooltip("Configuration asset for the chat service (e.g., GeminiConfig, ChatGPTConfig).")]
    [SerializeField] private ScriptableObject chatServiceConfig;

    [Tooltip("Configuration asset for the STT service (e.g., AzureConfig).")]
    [SerializeField] private ScriptableObject speechToTextConfig;

    [Tooltip("Configuration asset for the TTS service (e.g., ElevenlabsConfig).")]
    [SerializeField] private ScriptableObject textToSpeechConfig;

    // --- Private Service References ---
    // These hold the actual instances of the service classes.
    private IChatService _chatService;
    private ISpeechToTextService _sttService;
    private ITextToSpeechService _ttsService;

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
        _sttService = ServiceFactory.CreateSttService(speechToTextConfig);
        speechRecognizer = (_sttService as AzureSTTServiceManager).speechRecognizer;
        _ttsService = ServiceFactory.CreateTtsService(textToSpeechConfig);
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

    public void GenerateSpeechFromText(string prompt, string fileName, string dir)
    {
        _ttsService.RequestAudio(prompt, fileName, dir);
    }

    public async void StartSpeechTranscription()
    {
        if (_sttService == null)
        {
            Debug.Log("STT Service not initialized. Check AIManager configuration.");
            return;
        }

        Debug.Log("VoiceBox: Starting speech recognition.");

        await _sttService.TranscribeAudioFromMic(cancellationTokenSource.Token);
    }

    public void StopSpeechTranscription()
    {
        cancellationTokenSource.Cancel();
    }

    private void OnDestroy()
    {
        cancellationTokenSource.Cancel();
    }
}