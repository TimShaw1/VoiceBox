using Azure;
using System.Collections;
using System.Collections.Generic;
using TimShaw.VoiceBox.Components;  // Import useful components from VoiceBox
using TimShaw.VoiceBox.Core;        // Import core classes from VoiceBox
using UnityEngine;

public class TestVoiceBox : MonoBehaviour
{
    /// <summary>
    /// The AudioStreamer component to use for playing generated speech.
    /// </summary>
    public AudioStreamer audioStreamer;

    // Start is called before the first frame update
    void Start()
    {
        // Create a list of chats that represents the current message history
        var chats = new List<ChatUtils.VoiceBoxChatMessage>();

        // Add a user chat to the chat history
        var chat = new ChatUtils.VoiceBoxChatMessage(
            ChatUtils.VoiceBoxChatRole.User,
            "What is 2 + 2?"
        );
        chats.Add(chat);

        Debug.Log("User: " + chat);

        // Send the chat message to the chat service
        AIManager.Instance.SendChatMessage(
            chats,
            response => Debug.Log("Assistant: " + response),    // print the response to the console
            error => Debug.LogError(error)                      // log any errors to the console
        );

        // Start transcribing speech from the user's microphone
        AIManager.Instance.StartSpeechTranscription();

        // Request and stream audio from Elevenlabs through audio source
        AIManager.Instance.RequestAudioAndStream(
            "Hello World!", 
            audioStreamer
        );
    }
}
