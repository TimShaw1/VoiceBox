using Microsoft.CognitiveServices.Speech;
using System.Collections.Generic;
using TimShaw.VoiceBox.Core;
using UnityEngine;

/// <summary>
/// A Unity MonoBehaviour for testing various AI service APIs like Azure Speech-to-Text,
/// ElevenLabs Text-to-Speech, and chat services.
/// </summary>
public class APITester : MonoBehaviour
{
    /// <summary>
    /// If true, tests the Azure Speech-to-Text service on start.
    /// </summary>
    [SerializeField]
    public bool testAzure = false;

    /// <summary>
    /// If true, tests the ElevenLabs Text-to-Speech service on start.
    /// </summary>
    [SerializeField]
    public bool testElevenlabs = false;

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
        if (testAzure)
        {
            AIManager.Instance.OnRecognized += logRecognizedSpeech;
            AIManager.Instance.StartSpeechTranscription();
        }

        if (testElevenlabs)
        {
            Debug.Log("VoiceBox: Testing Audio Streaming");
            AIManager.Instance.RequestAudioAndStream("This audio is streaming instead of waiting for the full response. " +
                "This approach reduces first-word latency tremendously.", audioSource);
        }

        if (testChat)
        {
            var chats = new List<ChatMessage>();
            var systemPrompt = new ChatMessage(MessageRole.System, "You are an AI assistant that answers a user's questions.");
            var chat = new ChatMessage(MessageRole.User, "Please write a 2 paragraph essay on the dog breed Husky");
            chats.Add(chat);

            /*
            AIManager.Instance.SendChatMessage(
                chats,
                response => Debug.Log(response.Content),
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
