using Microsoft.CognitiveServices.Speech;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Core;
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
    private SpeechRecognizer _speechRecognizer;

    private readonly CancellationTokenSource cancellationTokenSource = new();

    [Header("Service Configurations")]
    [Tooltip("Configuration asset for the chat service (e.g., GeminiConfig, ChatGPTConfig).")]
    [SerializeField] private ScriptableObject chatServiceConfig;

    [Tooltip("Configuration asset for the STT service (e.g., AzureConfig).")]
    [SerializeField] private ScriptableObject speechToTextConfig;

    [Tooltip("Configuration asset for the TTS service (e.g., ElevenlabsConfig).")]
    [SerializeField] private ScriptableObject textToSpeechConfig;

    private IChatService _chatService;
    private ISpeechToTextService _sttService;
    private ITextToSpeechService _ttsService;

    /// <summary>
    /// Occurs when the speech recognizer is processing audio and has an intermediate result.
    /// </summary>
    public event System.EventHandler<SpeechRecognitionEventArgs> OnRecognizing;
    /// <summary>
    /// Occurs when the speech recognizer has finished processing an audio stream and has a final result.
    /// </summary>
    public event System.EventHandler<SpeechRecognitionEventArgs> OnRecognized;
    /// <summary>
    /// Occurs when the speech recognizer has been canceled.
    /// </summary>
    public event System.EventHandler<SpeechRecognitionCanceledEventArgs> OnCanceled;
    /// <summary>
    /// Occurs when a recognition session has started.
    /// </summary>
    public event System.EventHandler<SessionEventArgs> OnSessionStarted;
    /// <summary>
    /// Occurs when a recognition session has stopped.
    /// </summary>
    public event System.EventHandler<SessionEventArgs> OnSessionStopped;
    /// <summary>
    /// Occurs when the start of a speech segment is detected.
    /// </summary>
    public event System.EventHandler<RecognitionEventArgs> OnSpeechStartDetected;
    /// <summary>
    /// Occurs when the end of a speech segment is detected.
    /// </summary>
    public event System.EventHandler<RecognitionEventArgs> OnSpeechEndDetected;

    /// <summary>
    /// Loads API keys from a JSON file and applies them to the service configurations.
    /// </summary>
    /// <param name="keysFile">The path to the JSON file containing the API keys.</param>
    private void LoadAPIKeys(string keysFile)
    {
        string jsonContent = File.ReadAllText(keysFile);
        var apiKeys = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

        if (chatServiceConfig != null && chatServiceConfig is GeminiServiceConfig) 
            (chatServiceConfig as GeminiServiceConfig).apiKey = apiKeys["GEMINI_API_KEY"];

        if (speechToTextConfig != null && speechToTextConfig is AzureSTTServiceConfig)
            (speechToTextConfig as AzureSTTServiceConfig).apiKey = apiKeys["AZURE_API_KEY"];

        if (textToSpeechConfig != null && textToSpeechConfig is ElevenlabsTTSServiceConfig)
            (textToSpeechConfig as ElevenlabsTTSServiceConfig).apiKey = apiKeys["ELEVENLABS_API_KEY"];
    }

    /// <summary>
    /// Unloads the API keys from the service configurations.
    /// </summary>
    private void UnloadAPIKeys()
    {
        if (chatServiceConfig != null && chatServiceConfig is GeminiServiceConfig)
            (chatServiceConfig as GeminiServiceConfig).apiKey = "";

        if (speechToTextConfig != null && speechToTextConfig is AzureSTTServiceConfig)
            (speechToTextConfig as AzureSTTServiceConfig).apiKey = "";

        if (textToSpeechConfig != null && textToSpeechConfig is ElevenlabsTTSServiceConfig)
            (textToSpeechConfig as ElevenlabsTTSServiceConfig).apiKey = "";
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

        _chatService = ServiceFactory.CreateChatService(chatServiceConfig);
        _sttService = ServiceFactory.CreateSttService(speechToTextConfig);
        _speechRecognizer = (_sttService as AzureSTTServiceManager).speechRecognizer;
        _ttsService = ServiceFactory.CreateTtsService(textToSpeechConfig);

        _speechRecognizer.Recognizing += (s, e) =>
        {
            Debug.Log($"VoiceBox Internal: Recognizing: {e.Result.Text}");
            OnRecognizing?.Invoke(this, e);
        };

        _speechRecognizer.Recognized += (s, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                Debug.Log($"VoiceBox Internal: Recognized: {e.Result.Text}");
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                Debug.Log($"VoiceBox Internal: No match.");
            }
            OnRecognized?.Invoke(this, e);
        };

        _speechRecognizer.Canceled += (s, e) =>
        {
            Debug.Log($"VoiceBox Internal: CANCELED: Reason={e.Reason}");
            OnCanceled?.Invoke(this, e);
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
        if (_chatService == null)
        {
            onError?.Invoke("Chat service is not initialized. Check AIManager configuration.");
            return;
        }
        Task.Run(() => _chatService.SendMessage(messageHistory, onSuccess, onError));
    }

    /// <summary>
    /// Starts transcribing audio from the microphone using the configured STT service.
    /// </summary>
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
        Task.Run(() => _ttsService.RequestAudioFile(prompt, fileName, dir));
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
            AudioClip audioClip = await _ttsService.RequestAudioClip(prompt);
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
            audioStreamer.StartStreaming(prompt, _ttsService);
        else
            Debug.LogError("Audio Source does not have an AudioStreamer component. Attach an AudioStreamer component to enable streaming.");
    }
}