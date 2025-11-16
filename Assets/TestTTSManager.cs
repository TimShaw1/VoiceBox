using System.Collections;
using System.Collections.Generic;
using TimShaw.VoiceBox.Components;  // Import useful components from VoiceBox
using TimShaw.VoiceBox.Core;        // Import core classes from VoiceBox
using UnityEngine;

public class TestTTSManager : MonoBehaviour
{
    [SerializeField] TTSManager ttsManager;

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
    }
}