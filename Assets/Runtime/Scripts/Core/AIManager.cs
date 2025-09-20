using Microsoft.CognitiveServices.Speech;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.STT;
using TimShaw.VoiceBox.TTS;
using UnityEngine;
using static UnityEditor.Progress;

public class AIManager : MonoBehaviour
{
    // --- Singleton Pattern ---
    // This makes the manager globally accessible via AIManager.Instance
    public static AIManager Instance { get; private set; }
    private SpeechRecognizer _speechRecognizer;

    private readonly CancellationTokenSource cancellationTokenSource = new();

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

    public event System.EventHandler<SpeechRecognitionEventArgs> OnRecognizing;
    public event System.EventHandler<SpeechRecognitionEventArgs> OnRecognized;
    public event System.EventHandler<SpeechRecognitionCanceledEventArgs> OnCanceled;
    public event System.EventHandler<SessionEventArgs> OnSessionStarted;
    public event System.EventHandler<SessionEventArgs> OnSessionStopped;
    public event System.EventHandler<RecognitionEventArgs> OnSpeechStartDetected;
    public event System.EventHandler<RecognitionEventArgs> OnSpeechEndDetected;

    private void LoadAPIKeys(string keysFile)
    {
        string jsonContent = File.ReadAllText(keysFile);

        // Deserialize the JSON content directly into a Dictionary<string, string>.
        // The JSON structure "KEYNAME": "KEY" maps perfectly to this.
        var apiKeys = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

        if (chatServiceConfig != null && chatServiceConfig is GeminiServiceConfig) 
            (chatServiceConfig as GeminiServiceConfig).apiKey = apiKeys["GEMINI_API_KEY"];

        if (speechToTextConfig != null && speechToTextConfig is AzureSTTServiceConfig)
            (speechToTextConfig as AzureSTTServiceConfig).apiKey = apiKeys["AZURE_API_KEY"];

        if (textToSpeechConfig != null && textToSpeechConfig is ElevenlabsTTSServiceConfig)
            (textToSpeechConfig as ElevenlabsTTSServiceConfig).apiKey = apiKeys["ELEVENLABS_API_KEY"];
    }

    private void UnloadAPIKeys()
    {
        if (chatServiceConfig != null && chatServiceConfig is GeminiServiceConfig)
            (chatServiceConfig as GeminiServiceConfig).apiKey = "";

        if (speechToTextConfig != null && speechToTextConfig is AzureSTTServiceConfig)
            (speechToTextConfig as AzureSTTServiceConfig).apiKey = "";

        if (textToSpeechConfig != null && textToSpeechConfig is ElevenlabsTTSServiceConfig)
            (textToSpeechConfig as ElevenlabsTTSServiceConfig).apiKey = "";
    }

    // --- Internal Methods ---

    
    /// <summary>
    /// Initializes each service from attached configs
    /// </summary>
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

        LoadAPIKeys(Application.dataPath + "/keys.json");

        // --- Initialization ---
        // Use a "Factory" to create the correct service instance based on the config file.
        _chatService = ServiceFactory.CreateChatService(chatServiceConfig);
        _sttService = ServiceFactory.CreateSttService(speechToTextConfig);
        _speechRecognizer = (_sttService as AzureSTTServiceManager).speechRecognizer;
        _ttsService = ServiceFactory.CreateTtsService(textToSpeechConfig);

        // --- Wire up events ---
        _speechRecognizer.Recognizing += (s, e) =>
        {
            Debug.Log($"VoiceBox Internal: Recognizing: {e.Result.Text}");
            OnRecognizing?.Invoke(this, e);
        };

        _speechRecognizer.Recognized += (s, e) =>
        {
            // Optional: Your internal logic could go here
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                Debug.Log($"VoiceBox Internal: Recognized: {e.Result.Text}");
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                Debug.Log($"VoiceBox Internal: No match.");
            }
            OnRecognized?.Invoke(this, e); // Invoke your public event
        };

        _speechRecognizer.Canceled += (s, e) =>
        {
            Debug.Log($"VoiceBox Internal: CANCELED: Reason={e.Reason}");
            OnCanceled?.Invoke(this, e); // Invoke your public event
        };

        _speechRecognizer.SessionStarted += (s, e) =>
        {
            Debug.Log($"VoiceBox Internal: Session Started.");
            OnSessionStarted?.Invoke(this, e);
        };

        _speechRecognizer.SessionStopped += (s, e) =>
        {
            Debug.Log($"VoiceBox Internal: Session Stopped.");
            OnSessionStopped?.Invoke(this, e);
        };

        _speechRecognizer.SpeechStartDetected += (s, e) =>
        {
            Debug.Log($"VoiceBox Internal: Speech Start Detected.");
            OnSpeechStartDetected?.Invoke(this, e);
        };

        _speechRecognizer.SpeechEndDetected += (s, e) =>
        {
            Debug.Log($"VoiceBox Internal: Speech End Detected.");
            OnSpeechEndDetected?.Invoke(this, e);
        };
    }


    /// <summary>
    /// Cancels all running tasks when the game closes
    /// </summary>
    private void OnDestroy()
    {
        cancellationTokenSource.Cancel();
        UnloadAPIKeys();

    }

    // --- LLM Public Methods ---

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
        Task.Run(() => _chatService.SendMessage(messageHistory, onSuccess, onError));
    }

    // --- STT Public Methods ---

    public async void StartSpeechTranscription()
    {
        if (_sttService == null)
        {
            Debug.Log("STT Service not initialized. Check AIManager configuration.");
            return;
        }

        Debug.Log("VoiceBox: Starting speech recognition.");

        await Task.Run(() => _sttService.TranscribeAudioFromMic(cancellationTokenSource.Token));
    }

    public void StopSpeechTranscription()
    {
        cancellationTokenSource.Cancel();
    }

    // --- TTS Public Methods ---

    public void GenerateSpeechFileFromText(string prompt, string fileName, string dir)
    {
        Task.Run(() => _ttsService.RequestAudioFile(prompt, fileName, dir));
    }

    public async void GenerateSpeechAudioClipFromText(
        string prompt, 
        Action<AudioClip> onSuccess,
        Action<string> onError)
    {
        try
        {
            // Await the actual async method
            AudioClip audioClip = await _ttsService.RequestAudioClip(prompt);

            // Invoke the callback with the result.
            // This will execute on the main thread because of how async/await works in Unity.
            onSuccess?.Invoke(audioClip);
        }
        catch (Exception e)
        {
            onError?.Invoke($"Failed to generate speech: {e.Message}");
        }
    }

    public void RequestAudioAndStream(string prompt, AudioSource audioSource)
    {
        AudioStreamer audioStreamer = audioSource.GetComponent<AudioStreamer>();
        if (audioStreamer)
            audioStreamer.StartStreaming(prompt, _ttsService);
        else
            Debug.LogError("Audio Source does not have an AudioStreamer component. Attach an AudioStreamer component to enable streaming.");
    }
}