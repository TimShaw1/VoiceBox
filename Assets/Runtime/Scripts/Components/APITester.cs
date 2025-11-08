using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TimShaw.VoiceBox.Components;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Data;
using TimShaw.VoiceBox.Generics;
using TimShaw.VoiceBox.Modding;
using UnityEngine;
using static TimShaw.VoiceBox.Core.ChatUtils;
using static TimShaw.VoiceBox.Core.STTUtils;

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

    private CancellationTokenSource internalCancellationTokenSource = new CancellationTokenSource();

    /// <summary>
    /// Callback method to log recognized speech from the Speech-to-Text service.
    /// </summary>
    /// <param name="s">The source of the event.</param>
    /// <param name="e">The event arguments containing the recognition result.</param>
    void LogRecognizedSpeech(object s, VoiceBoxSpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason == ResultReason.RecognizedSpeech)
        {
            if (e.Result.Text.Length > 0) Debug.Log("API Tester: Recognized: " + e.Result.Text);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="textToDisplay"></param>
    public void SampleTool1(string textToDisplay)
    {
        Debug.Log("AI is displaying text! --> " + textToDisplay);
    }

    public void SampleTool2(Color color)
    {
        Debug.Log("AI is displaying a color! --> " + color);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dir"></param>
    /// <param name="rot"></param>
    public void SampleTool3(Vector2 dir, Quaternion rot)
    {
        Debug.Log("AI is displaying a Vector2 and a Quaternion! --> " + dir + " -- " + rot );
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
            AIManager.Instance.SpeechToTextService.OnRecognized += LogRecognizedSpeech;
            AIManager.Instance.StartSpeechTranscription(internalCancellationTokenSource.Token);
        }

        if (testTTS)
        {
            Debug.Log("VoiceBox: Testing Audio Streaming");
            AIManager.Instance.RequestAudioAndStream("This audio is streaming instead of waiting for the full response. " +
                "This approach reduces first-word latency tremendously.", audioSource.GetComponent<AudioStreamer>());
        }

        if (testChat)
        {
            var chats = new List<ChatUtils.VoiceBoxChatMessage>();
            var chat = new ChatUtils.VoiceBoxChatMessage(
                ChatUtils.VoiceBoxChatRole.User,
                //"Write a 1 paragraph essay about huskies. Then, display the first sentence to the console."
                //"Display a vector2 (1.00, 3.00) and quaternion (1.00, 2.00, 3.00, 4.00) to the console. Use SampleTool3. Then, display 1 sentence about huskies to the console using SampleTool."
                "Display the color red to the console with 0.5 alpha"
                );
            chats.Add(chat);

            var converters = new List<System.Text.Json.Serialization.JsonConverter>();
            converters.Add(new ColorJsonConverter());

            ChatUtils.VoiceBoxChatTool tool = new ChatUtils.VoiceBoxChatTool(this, nameof(SampleTool1), "Displays provided text in the console");
            ChatUtils.VoiceBoxChatTool tool2 = new VoiceBoxChatTool(
                this, 
                nameof(SampleTool2), 
                "Displays an RGBA color (with color values represented as floats between 0 and 1) to the console",
                converters
            );
            ChatUtils.VoiceBoxChatTool tool3 = new ChatUtils.VoiceBoxChatTool(this, nameof(SampleTool3), "Displays a provided vector2 and quaternion to the console.", converters);

            string combinedResponse = "";

            ChatUtils.VoiceBoxChatCompletionOptions options = new ChatUtils.VoiceBoxChatCompletionOptions()
            {
                VoiceBoxTools = { tool2 }
            };

            Debug.Log(options.Tools.Count);

            AIManager.Instance.SendChatMessage(chats, chatMessage => Debug.Log(chatMessage.Role + ": " + chatMessage.Text), error => Debug.LogError(error), options);

            /*
            AIManager.Instance.StreamChatMessage(
                chats,
                chunk => { Debug.Log(chunk.Role + ": " + chunk.Text); combinedResponse += chunk; },
                () => { Debug.Log("Combined response: " + combinedResponse); },
                error => Debug.LogError(error),
                options
            );
            */
            
            
        }
    }

    private void OnDestroy()
    {
        internalCancellationTokenSource.Cancel();
    }
}
