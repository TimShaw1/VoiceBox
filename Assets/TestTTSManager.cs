using System.Collections;
using System.Collections.Generic;
using System.IO;
using TimShaw.VoiceBox.Components;  // Import useful components from VoiceBox
using TimShaw.VoiceBox.Core;        // Import core classes from VoiceBox
using UnityEngine;

public class TestTTSManager : MonoBehaviour
{
    [SerializeField] TTSManager ttsManager;
    [SerializeField] ChatManager chatManager;

    // Start is called before the first frame update
    void Start()
    {
        
        // Request an audio file and save it to Assets/helloWorld.mp3.
        // Note that the file extension is omitted. The extension is determined by the file format
        // set in the TTS config file.
        ttsManager.GenerateSpeechFileFromText(
            "Hello World!", 
            "helloWorld", 
            Application.dataPath,
            path => Debug.Log("File created at: " + path),
            err => Debug.LogError(err)
        );

        // Request an audio clip and play it through the TTS manager's audio source
        ttsManager.GenerateSpeechAudioClipFromText(
            "Hello World!",
            generatedAudioClip => ttsManager.GetComponent<AudioSource>().PlayOneShot(generatedAudioClip),
            err => Debug.LogError(err)
        );

        /// Request audio and stream it through the TTS Manager's AudioStreamer
        ttsManager.RequestAudioAndStream("Hello World!");

        string voiceId = ttsManager.CloneVoiceAndGetVoiceIDAsync(Application.dataPath, "/speechSample.mp3", "VoiceBoxTestVoice").Result;
        

        // Create a list of chats that represents the current message history
        var chats = new List<ChatUtils.VoiceBoxChatMessage>();

        // Add a user chat to the chat history
        var chat = new ChatUtils.VoiceBoxChatMessage(
            ChatUtils.VoiceBoxChatRole.User,
            "Write 2 paragraphs about Canada."
        );
        chats.Add(chat);
        AudioStreamer audioStreamer = ttsManager.GetComponent<AudioStreamer>();
        chatManager.StreamChatMessage(
            chats,
            chunk => { ttsManager.RequestAudioAndStream(chunk.Text, false, audioStreamer); Debug.Log(chunk); },
            () => ttsManager.RequestAudioAndStream(" ", true, audioStreamer),
            err => Debug.LogError(err)
        );
    }
}