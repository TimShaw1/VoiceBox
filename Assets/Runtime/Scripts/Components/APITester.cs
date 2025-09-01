using Microsoft.CognitiveServices.Speech;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.LLM;
using TimShaw.VoiceBox.STT;
using TimShaw.VoiceBox.TTS;
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

    void logRecognizedSpeech(object s, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason == ResultReason.RecognizedSpeech)
        {
            if (e.Result.Text.Length > 0) Debug.Log(e.Result.Text);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        if (testAzure)
        {
            AIManager.Instance.speechRecognizer.Recognized += logRecognizedSpeech;
            AIManager.Instance.StartSpeechTranscription();
        }

        if (testElevenlabs)
        {
            ElevenLabs.Init("sk_fd1e422979c6cb05d645432c832029429fdba5d326ae5788", "Se2Vw1WbHmGbBbyWTuu4", 0f);
        }

        if (testChat)
        {
            var chats = new List<TimShaw.VoiceBox.Core.ChatMessage>();
            var systemPrompt = new TimShaw.VoiceBox.Core.ChatMessage(TimShaw.VoiceBox.Core.MessageRole.System, "You are an AI assistant that answers a user's questions.");
            var chat = new TimShaw.VoiceBox.Core.ChatMessage(TimShaw.VoiceBox.Core.MessageRole.User, "What is 2 + 2?");
            chats.Add(chat);
            AIManager.Instance.SendChatMessage(
                chats, 
                response => Debug.Log(response.Content), 
                error => Debug.Log(error) 
            );
        }
    }
}
