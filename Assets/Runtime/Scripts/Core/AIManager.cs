using Microsoft.CognitiveServices.Speech;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Generics;
using UnityEngine;

/// <summary>
/// Manages AI services, acting as a central hub for chat, speech-to-text (STT), and text-to-speech (TTS) functionalities.
/// This class follows the Singleton pattern to ensure a single instance manages all AI interactions.
/// </summary>
public class AIManager : MonoBehaviour
{
    /// <summary>
    /// Gets the singleton instance of the AIManager.
    /// </summary>
    public static AIManager Instance { get; private set; }

    private readonly CancellationTokenSource cancellationTokenSource = new();

    [Header("Service Configurations")]
    [Tooltip("Configuration asset for the chat service (e.g., GeminiConfig, ChatGPTConfig).")]
    [SerializeField] public GenericChatServiceConfig chatServiceConfig;

    [Tooltip("Configuration asset for the STT service (e.g., AzureConfig).")]
    [SerializeField] public GenericSTTServiceConfig speechToTextConfig;

    [Tooltip("Configuration asset for the TTS service (e.g., ElevenlabsConfig).")]
    [SerializeField] public GenericTTSServiceConfig textToSpeechConfig;

    private IChatService _chatService;
    public IChatService ChatService { get => _chatService; set => _chatService = value; }

    private ISpeechToTextService _sttService;
    public ISpeechToTextService SpeechToTextService { get => _sttService; set => _sttService = value; }

    private ITextToSpeechService _ttsService;
    public ITextToSpeechService TextToSpeechService { get => _ttsService; set => _ttsService = value; }

    /// <summary>
    /// Loads API keys from a JSON file and applies them to the service configurations.
    /// </summary>
    /// <param name="keysFile">The path to the JSON file containing the API keys.</param>
    public void LoadAPIKeys(string keysFile)
    {
        string jsonContent = File.ReadAllText(keysFile);
        var apiKeys = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

        if (chatServiceConfig && chatServiceConfig.apiKeyJSONString.Length > 0)
            chatServiceConfig.apiKey = apiKeys[chatServiceConfig.apiKeyJSONString];
        else
            Debug.LogWarning("Chat service config does not define an apiKeyJSONString");

        if (speechToTextConfig && speechToTextConfig.apiKeyJSONString.Length > 0)
            speechToTextConfig.apiKey = apiKeys[speechToTextConfig.apiKeyJSONString];
        else
            Debug.LogWarning("STT service config does not define an apiKeyJSONString");

        if (textToSpeechConfig && textToSpeechConfig.apiKeyJSONString.Length > 0)
            textToSpeechConfig.apiKey = apiKeys[textToSpeechConfig.apiKeyJSONString];
        else
            Debug.LogWarning("Chat service config does not define an apiKeyJSONString");
    }

    /// <summary>
    /// Unloads the API keys from the service configurations.
    /// </summary>
    private void UnloadAPIKeys()
    {
        chatServiceConfig.apiKey = "";

        speechToTextConfig.apiKey = "";

        textToSpeechConfig.apiKey = "";
    }

    /// <summary>
    /// Initializes the singleton instance, loads API keys, and sets up the AI services.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadAPIKeys(Application.dataPath + "/keys.json");

        ChatService = ServiceFactory.CreateChatService(chatServiceConfig);
        SpeechToTextService = ServiceFactory.CreateSttService(speechToTextConfig);
        TextToSpeechService = ServiceFactory.CreateTtsService(textToSpeechConfig);
    }

    /// <summary>
    /// Cancels all running tasks and unloads API keys when the application quits.
    /// </summary>
    private void OnDestroy()
    {
        cancellationTokenSource.Cancel();
        UnloadAPIKeys();
    }

    /// <summary>
    /// Sends a conversation history to the configured chat service.
    /// </summary>
    /// <param name="messageHistory">A list of chat messages representing the conversation history.</param>
    /// <param name="onSuccess">Callback invoked when the message is successfully sent, returning the response.</param>
    /// <param name="onError">Callback invoked when an error occurs.</param>
    public void SendChatMessage(
        List<ChatMessage> messageHistory,
        Action<ChatMessage> onSuccess,
        Action<string> onError)
    {
        if (ChatService == null)
        {
            onError?.Invoke("Chat service is not initialized. Check AIManager configuration.");
            return;
        }
        Task.Run(() => ChatService.SendMessage(messageHistory, onSuccess, onError));
    }

    /// <summary>
    /// Sends a message to the Chat service and streams the response.
    /// </summary>
    /// <param name="messageHistory">The history of messages in the conversation.</param>
    /// <param name="onChunkReceived">Callback invoked when a chunk of the response is received.</param>
    /// <param name="onComplete">Callback invoked when the response is complete.</param>
    /// <param name="onError">Callback invoked when an error occurs.</param>
    public void StreamChatMessage(
        List<ChatMessage> messageHistory,
            Action<string> onChunkReceived,
            Action onComplete,
            Action<string> onError
    )
    {
        if (ChatService == null)
        {
            onError?.Invoke("Chat service is not initialized. Check AIManager configuration.");
            return;
        }

        Task.Run(() => ChatService.SendMessageStream(messageHistory, onChunkReceived, onComplete, onError, cancellationTokenSource.Token));
    }

    /// <summary>
    /// Starts transcribing audio from the microphone using the configured STT service.
    /// </summary>
    public async void StartSpeechTranscription()
    {
        if (SpeechToTextService == null)
        {
            Debug.Log("STT Service not initialized. Check AIManager configuration.");
            return;
        }
        Debug.Log("VoiceBox: Starting speech recognition.");
        await Task.Run(() => SpeechToTextService.TranscribeAudioFromMic(cancellationTokenSource.Token));
    }

    /// <summary>
    /// Stops the ongoing speech transcription.
    /// </summary>
    public void StopSpeechTranscription()
    {
        cancellationTokenSource.Cancel();
    }

    /// <summary>
    /// Generates a speech audio file from the given text prompt.
    /// </summary>
    /// <param name="prompt">The text to be converted to speech.</param>
    /// <param name="fileName">The name of the output audio file.</param>
    /// <param name="dir">The directory to save the audio file in.</param>
    public void GenerateSpeechFileFromText(string prompt, string fileName, string dir)
    {
        Task.Run(() => TextToSpeechService.RequestAudioFile(prompt, fileName, dir));
    }

    /// <summary>
    /// Generates an AudioClip from the given text prompt.
    /// </summary>
    /// <param name="prompt">The text to be converted to speech.</param>
    /// <param name="onSuccess">Callback invoked with the generated AudioClip on success.</param>
    /// <param name="onError">Callback invoked with an error message on failure.</param>
    public async void GenerateSpeechAudioClipFromText(
        string prompt, 
        Action<AudioClip> onSuccess,
        Action<string> onError)
    {
        try
        {
            AudioClip audioClip = await TextToSpeechService.RequestAudioClip(prompt);
            onSuccess?.Invoke(audioClip);
        }
        catch (Exception e)
        {
            onError?.Invoke($"Failed to generate speech: {e.Message}");
        }
    }

    /// <summary>
    /// Requests and streams audio from a text prompt to an AudioSource.
    /// </summary>
    /// <param name="prompt">The text to be converted to speech.</param>
    /// <param name="audioSource">The AudioSource to stream the audio to.</param>
    public void RequestAudioAndStream(string prompt, AudioSource audioSource)
    {
        AudioStreamer audioStreamer = audioSource.GetComponent<AudioStreamer>();
        if (audioStreamer)
            audioStreamer.StartStreaming(prompt, TextToSpeechService);
        else
            Debug.LogError("Audio Source does not have an AudioStreamer component. Attach an AudioStreamer component to enable streaming.");
    }
}