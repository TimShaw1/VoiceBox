using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.AI;
using System;
using System.Collections;
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

    private bool testIsRunning = false;

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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="color"></param>
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
            StartCoroutine(TestModdingTools());
        }
        if (testSTT)
        {
            StartCoroutine(TestSpeechToText());
        }

        if (testTTS)
        {
            StartCoroutine(TestTextToSpeech());
        }

        if (testChat)
        {

            StartCoroutine(TestChat());
            
        }
    }

    private IEnumerator TestModdingTools()
    {
        while (testIsRunning) yield return new WaitForSeconds(0.1f);
        testIsRunning = true;

        Debug.Log("VoiceBox: Testing Modding Tools");
        Debug.Log("[Test: ModdingTools] Create AIManager using ModdingTools");
        ModdingTools.CreateAIManagerObject<GeminiServiceConfig, AzureSTTServiceConfig, ElevenlabsTTSServiceConfig>();

        Debug.Log("VoiceBox: Finished Testing Modding Tools");
        testIsRunning = false;
        yield return null;
    }

    private IEnumerator TestSpeechToText()
    {
        while (testIsRunning) yield return new WaitForSeconds(0.1f);
        testIsRunning = true;

        Debug.Log("VoiceBox: Testing Speech To Text");
        Debug.Log("[Test: Speech To Text 1] Start Speech Transcription for 10s...");
        AIManager.Instance.SpeechToTextService.OnRecognized += LogRecognizedSpeech;
        AIManager.Instance.StartSpeechTranscription(internalCancellationTokenSource.Token);

        yield return new WaitForSeconds(10f);

        Debug.Log("[Test: Speech To Text 1] Stop Speech Transcription after 10s...");
        AIManager.Instance.StopSpeechTranscription();

        Debug.Log("[Test: Speech To Text 2] Wait 2s then Start Speech Transcription for 10s after previous stop...");
        yield return new WaitForSeconds(2);
        AIManager.Instance.StartSpeechTranscription();
        yield return new WaitForSeconds(10);

        Debug.Log("[Test: Speech To Text 2] Stop Speech Transcription again after 10s...");
        AIManager.Instance.StopSpeechTranscription();

        Debug.Log("VoiceBox: Finished Testing Speech To Text");
        testIsRunning = false;
        yield return null;
    }

    private IEnumerator TestTextToSpeech()
    {
        while (testIsRunning) yield return new WaitForSeconds(0.1f);
        testIsRunning = true;

        bool waitingForTTS = true;

        Debug.Log("VoiceBox: Testing Text To Speech");
        Debug.Log("[Test: Text To Speech 1] Generate audio file");
        AIManager.Instance.GenerateSpeechFileFromText(
            "Hello World",
            "helloworld",
            Application.dataPath,
            path =>
            {
                Debug.Log("[Test: Text To Speech 1] Generated audio file at " + path);
                waitingForTTS = false;
            },
            err => Debug.LogError(err)
        );

        

        while (waitingForTTS) yield return new WaitForSeconds(0.1f);
        Debug.Log("[Test: Text To Speech 2] Generating audioclip");

        waitingForTTS = true;
        AIManager.Instance.GenerateSpeechAudioClipFromText(
            "Hello World!",
            audioClip =>
            {
                Debug.Log("[Test: Text To Speech 2] Generated audioclip and playing...");
                audioSource.PlayOneShot(audioClip);
                waitingForTTS = false;
            },
            err => {
                Debug.LogError(err);
                waitingForTTS = false;
            }
        );

        // This will play while next test runs
        Debug.Log("[Test: Text To Speech 3] Stream audio");
        while (waitingForTTS) yield return new WaitForSeconds(0.1f);
        AIManager.Instance.RequestAudioAndStream("This audio is streaming instead of waiting for the full response. " +
            "This approach reduces first-word latency tremendously.", audioSource.GetComponent<AudioStreamer>());

        Debug.Log("VoiceBox: Finished Testing Text To Speech");
        testIsRunning = false;
        yield return null;
    }

    private IEnumerator TestChat()
    {
        while (testIsRunning) yield return new WaitForSeconds(0.1f);
        testIsRunning = true;

        bool waitingForResponse = false;

        Debug.Log("VoiceBox: Testing Chat");
        #region Test 1: Simple Chat Request
        Debug.Log("[Test: Chat 1] Simple chat request");

        var chats = new List<ChatUtils.VoiceBoxChatMessage>();
        var chat = new ChatUtils.VoiceBoxChatMessage(
            ChatUtils.VoiceBoxChatRole.User,
            "Write a 1 paragraph essay about huskies. "
            );
        chats.Add(chat);

        waitingForResponse = true;
        AIManager.Instance.SendChatMessage(
            chats, 
            chatMessage => { 
                Debug.Log(chatMessage.Role + ": " + chatMessage.Text); 
                waitingForResponse = false; 
            }, 
            error => { 
                Debug.LogError(error); 
                waitingForResponse = false; 
            }
        );
        #endregion

        #region Test 2: Simple streaming chat request
        while (waitingForResponse) yield return new WaitForSeconds(0.1f);
        string combinedResponse = "";
        Debug.Log("[Test: Chat 2] Simple streaming chat request");
        waitingForResponse = true;
        AIManager.Instance.StreamChatMessage(
            chats,
            chunk => { Debug.Log(chunk.Role + ": " + chunk.Text); combinedResponse += chunk; },
            () => { Debug.Log("Combined response: " + combinedResponse); waitingForResponse = false; },
            error => { Debug.LogError(error); waitingForResponse = false; }
        );
        #endregion

        #region Test 3: Simple chat request with tool
        while (waitingForResponse) yield return new WaitForSeconds(0.1f);
        Debug.Log("[Test: Chat 3] Simple chat request with tool");
        ChatUtils.VoiceBoxChatTool tool = new ChatUtils.VoiceBoxChatTool(
            this, 
            nameof(SampleTool1), 
            "Displays provided text in the console"
        );

        ChatUtils.VoiceBoxChatCompletionOptions options = new ChatUtils.VoiceBoxChatCompletionOptions()
        {
            VoiceBoxTools = { tool }
        };

        Debug.Log(options.Tools.Count);

        var chat2 = new ChatUtils.VoiceBoxChatMessage(
            ChatUtils.VoiceBoxChatRole.User,
            "Write a 1 paragraph essay about huskies. Then, display the first sentence to the console. Only display one sentence to the console."
        );
        chats.Clear();
        chats.Add(chat2);

        waitingForResponse = true;
        AIManager.Instance.SendChatMessage(
            chats,
            chatMessage => {
                Debug.Log(chatMessage.Role + ": " + chatMessage.Text);
                waitingForResponse = false;
            },
            error => {
                Debug.LogError(error);
                waitingForResponse = false;
            },
            options
        );
        #endregion

        #region Test 4: Simple chat request with complex tool
        while (waitingForResponse) yield return new WaitForSeconds(0.1f);
        Debug.Log("[Test: Chat 4] Simple chat request with complex tool");
        var chat3 = new ChatUtils.VoiceBoxChatMessage(
            ChatUtils.VoiceBoxChatRole.User,
            "Display a Vector2 (1.00, 2.00) and a Quaterion (4.00, 4.00, 4.00) to the console"
        );
        chats.Clear();
        chats.Add(chat3);

        var converters = new List<System.Text.Json.Serialization.JsonConverter>();
        converters.Add(new ColorJsonConverter());

        ChatUtils.VoiceBoxChatTool tool3 = new ChatUtils.VoiceBoxChatTool(
            this, 
            nameof(SampleTool3), 
            "Displays a provided vector2 and quaternion to the console.", 
            converters
        );

        ChatUtils.VoiceBoxChatCompletionOptions options2 = new ChatUtils.VoiceBoxChatCompletionOptions()
        {
            VoiceBoxTools = { tool3 }
        };

        waitingForResponse = true;
        AIManager.Instance.SendChatMessage(
            chats,
            chatMessage => {
                Debug.Log(chatMessage.Role + ": " + chatMessage.Text);
                waitingForResponse = false;
            },
            error => {
                Debug.LogError(error);
                waitingForResponse = false;
            },
            options2
        );
        #endregion

        Debug.Log("VoiceBox: Finished Testing Chat");
        yield return null;
    }

    private void OnDestroy()
    {
        internalCancellationTokenSource.Cancel();
    }
}
