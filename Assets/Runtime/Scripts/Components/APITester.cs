using Microsoft.Extensions.AI;
using Microsoft.CognitiveServices.Speech;
using System;
using System.Collections.Generic;
using System.Text;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Data;
using TimShaw.VoiceBox.Generics;
using TimShaw.VoiceBox.Tools;
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

    public void SampleTool(string textToDisplay)
    {
        Debug.Log("AI is displaying text! --> " + textToDisplay);
    }

    public void SampleTool2(Vector2 dir, Vector3 dir2, Vector4 dir3)
    {
        Debug.Log("AI is displaying vectors! --> " + dir + " " + dir2 + " " + dir3);
    }

    public void SampleTool3(Vector2 dir)
    {
        Debug.Log("AI is displaying a vector! --> " + dir);
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
            var chat = new ChatMessage(
                ChatRole.User,
                //"Write a 1 paragraph essay about huskies. Then, display the first sentence to the console."
                "Display a vector2 (1.00, 3.00) to the console. Use SampleTool3. Then, display 1 sentence about huskies to the console using SampleTool."
                );
            chats.Add(chat);

            ChatUtils.VoiceBoxChatTool tool = new ChatUtils.VoiceBoxChatTool(this, nameof(SampleTool), "Displays provided text in the console");
            ChatUtils.VoiceBoxChatTool tool2 = new ChatUtils.VoiceBoxChatTool(this, nameof(SampleTool3), "Displays a provided vector2 to the console.");

            string combinedResponse = "";

            ChatUtils.VoiceBoxChatCompletionOptions options = new()
            {
                VoiceBoxTools = { tool, tool2 }
            };

            Debug.Log(options.Tools.Count);

            //AIManager.Instance.SendChatMessage(chats, options, chatMessage => Debug.Log(chatMessage.Content[0].Text), error => Debug.LogError(error));
            
            AIManager.Instance.StreamChatMessage(
                chats,
                options,
                chunk => { Debug.Log(chunk); combinedResponse += chunk; },
                () => { Debug.Log("Combined response: " + combinedResponse); },
                error => Debug.LogError(error)
            );
            
        }
    }
}
