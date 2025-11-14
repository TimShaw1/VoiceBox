using System.Collections;
using System.Collections.Generic;
using TimShaw.VoiceBox.Components;  // Import useful components from VoiceBox
using TimShaw.VoiceBox.Core;        // Import core classes from VoiceBox
using UnityEngine;

public class TestChatManager : MonoBehaviour
{
    [SerializeField] ChatManager chatManager;

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

        // Print the user's chat to the console
        Debug.Log("User: " + chat);

        // Send the chat messages to the chat service
        //chatManager.SendChatMessage(
        //    chats,
        //    response => Debug.Log("Assistant: " + response),    // print the response to the console
        //    error => Debug.LogError(error)                      // log any errors to the console
        //);

        // Accumulate chunks into a string
        string combinedResponse = "";

        // Stream the response
        chatManager.StreamChatMessage(
            chats,
            chunk => { Debug.Log(chunk.Role + ": " + chunk.Text); combinedResponse += chunk; }, // print chunks as we get them
            () => { Debug.Log("Combined response: " + combinedResponse); },                     // print the combined response when response has finished generating
            error => Debug.LogError(error)                                                      // log any errors to the console
        );
    }
}
