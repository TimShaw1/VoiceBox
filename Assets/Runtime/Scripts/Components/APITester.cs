using Microsoft.CognitiveServices.Speech;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.LLM;
using TimShaw.VoiceBox.STT;
using TimShaw.VoiceBox.TTS;
using TimShaw.VoiceBox.Core;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;

public class APITester : MonoBehaviour
{

    [SerializeField]
    public bool testAzure = false;

    [SerializeField]
    public bool testElevenlabs = false;

    [SerializeField]
    public bool testChat = false;

    [SerializeField]
    public AudioSource audioSource;

    void logRecognizedSpeech(object s, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason == ResultReason.RecognizedSpeech)
        {
            if (e.Result.Text.Length > 0) Debug.Log("API Tester: Recognized: " + e.Result.Text);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        if (testAzure)
        {
            AIManager.Instance.OnRecognized += logRecognizedSpeech;
            AIManager.Instance.StartSpeechTranscription();
        }

        if (testElevenlabs)
        {
            Debug.Log("VoiceBox: Testing Audio File Request");
            Debug.Log(Application.dataPath);
            //AIManager.Instance.GenerateSpeechFileFromText("Hello World!", "test", Application.dataPath + "/");

            //Task.Delay(2000).Wait();

            /*
            Debug.Log("VoiceBox: Testing Audio Clip");
            AIManager.Instance.GenerateSpeechAudioClipFromText(
                "This is an audio clip",
                (audioclip) => audioSource.PlayOneShot(audioclip),
                (e) => Debug.Log(e)
            );
            

            Task.Delay(3000).Wait();
            */

            Debug.Log("VoiceBox: Testing Audio Streaming");
            AIManager.Instance.RequestAudioAndStream("This audio is streaming instead of waiting for the full response. " +
                "This approach reduces first-word latency tremendously.", audioSource);
        }

        if (testChat)
        {
            var chats = new List<ChatMessage>();
            var systemPrompt = new ChatMessage(MessageRole.System, "You are an AI assistant that answers a user's questions.");
            var chat = new ChatMessage(MessageRole.User, "What is 2 + 2?");
            chats.Add(chat);
            AIManager.Instance.SendChatMessage(
                chats, 
                response => Debug.Log(response.Content), 
                error => Debug.Log(error) 
            );
        }
    }
}
