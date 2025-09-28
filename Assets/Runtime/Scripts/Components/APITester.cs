using Microsoft.CognitiveServices.Speech;
using OpenAI.Chat;
using System.Collections.Generic;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Generics;
using TimShaw.VoiceBox.Tools;
using TimShaw.VoiceBox.Data;
using UnityEngine;

/// <summary>
/// A Unity MonoBehaviour for testing various AI service APIs like Azure Speech-to-Text,
/// ElevenLabs Text-to-Speech, and chat services.
/// </summary>
public class APITester : MonoBehaviour
{
    /// <summary>
    /// If true, spawns an AI manager
    /// </summary>
    [SerializeField]
    public bool testSpawnManager = false;

    /// <summary>
    /// If true, tests the Azure Speech-to-Text service on start.
    /// </summary>
    [SerializeField]
    public bool testSTT = false;

    /// <summary>
    /// If true, tests the ElevenLabs Text-to-Speech service on start.
    /// </summary>
    [SerializeField]
    public bool testTTS = false;

    /// <summary>
    /// If true, tests the chat service on start.
    /// </summary>
    [SerializeField]
    public bool testChat = false;

    /// <summary>
    /// The AudioSource component to use for playing generated speech.
    /// </summary>
    [SerializeField]
    public AudioSource audioSource;

    /// <summary>
    /// Callback method to log recognized speech from the Speech-to-Text service.
    /// </summary>
    /// <param name="s">The source of the event.</param>
    /// <param name="e">The event arguments containing the recognition result.</param>
    void logRecognizedSpeech(object s, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason == ResultReason.RecognizedSpeech)
        {
            if (e.Result.Text.Length > 0) Debug.Log("API Tester: Recognized: " + e.Result.Text);
        }
    }

    /// <summary>
    /// Called when the script instance is being loaded.
    /// Initializes and triggers the selected API tests.
    /// </summary>
    void Start()
    {
        if (testSpawnManager)
        {
            ModdingTools.CreateAIManagerObject<GeminiServiceConfig, AzureSTTServiceConfig, ElevenlabsTTSServiceConfig>();
        }
        if (testSTT)
        {
            AIManager.Instance.SpeechToTextService.OnRecognized += logRecognizedSpeech;
            AIManager.Instance.StartSpeechTranscription();
        }

        if (testTTS)
        {
            Debug.Log("VoiceBox: Testing Audio Streaming");
            AIManager.Instance.RequestAudioAndStream("This audio is streaming instead of waiting for the full response. " +
                "This approach reduces first-word latency tremendously.", audioSource);
        }

        if (testChat)
        {
            var chats = new List<ChatMessage>();
            var chat = new UserChatMessage("Please write a C# script that sends a request to Claude via the OpenAI dotnet library. Be sure to use callbacks for success and failure.");
            chats.Add(chat);

            /*
            AIManager.Instance.SendChatMessage(
                chats,
                response => Debug.Log(response.Content[0].Text),
                error => Debug.Log(error)
            );
            */
            

            string combinedResponse = "";

            AIManager.Instance.StreamChatMessage(
                chats,
                chunk => { Debug.Log(chunk); combinedResponse += chunk; },
                () => { Debug.Log(combinedResponse); },
                error => Debug.LogError(error)
            );
        }
    }
}
