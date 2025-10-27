

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Generics;
using UnityEngine;

namespace TimShaw.VoiceBox.Components
{
    /// <summary>
    /// An individual TTS service manager. Useful if working with multiple different TTS services or voices.
    /// </summary>
    public class TTSManager : MonoBehaviour
    {
        [Tooltip("Path to the api keys json file. Defaults to Assets/keys.json")]
        [SerializeField] public string apiKeysJsonPath = "";

        [Tooltip("Configuration asset for the TTS service (e.g., ElevenlabsConfig).")]
        [SerializeField] public GenericTTSServiceConfig textToSpeechConfig;

        private ITextToSpeechService _ttsService;
        public ITextToSpeechService TextToSpeechService { get => _ttsService; set => _ttsService = value; }

        public void Awake()
        {
            LoadAPIKey(apiKeysJsonPath.Length > 0 ? apiKeysJsonPath : Application.dataPath + "/keys.json");
            TextToSpeechService = ServiceFactory.CreateTtsService(textToSpeechConfig);
        }

        /// <summary>
        /// Unloads API key from config
        /// </summary>
        private void OnDestroy()
        {
            textToSpeechConfig.apiKey = "";
        }

        /// <summary>
        /// Loads API keys from a JSON file and applies them to the service configurations.
        /// </summary>
        /// <param name="keysFile">The path to the JSON file containing the API keys.</param>
        public void LoadAPIKey(string keysFile)
        {
            string jsonContent = File.ReadAllText(keysFile);
            var apiKeys = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

            if (textToSpeechConfig && textToSpeechConfig.apiKeyJSONString.Length > 0)
                textToSpeechConfig.apiKey = apiKeys[textToSpeechConfig.apiKeyJSONString];
            else
                Debug.LogWarning("TTS service config does not define an apiKeyJSONString");
        }

        /// <summary>
        /// Generates a speech audio file from the given text prompt.
        /// </summary>
        /// <param name="prompt">The text to be converted to speech.</param>
        /// <param name="fileName">The name of the output audio file.</param>
        /// <param name="dir">The directory to save the audio file in.</param>
        public void GenerateSpeechFileFromText(string prompt, string fileName, string dir, CancellationToken token)
        {
            Task.Run(() => TextToSpeechService.RequestAudioFile(prompt, fileName, dir, token));
        }

        /// <summary>
        /// Generates an AudioClip from the given text prompt.
        /// </summary>
        /// <param name="prompt">The text to be converted to speech.</param>
        /// <param name="onSuccess">Callback invoked with the generated AudioClip on success.</param>
        /// <param name="onError">Callback invoked with an error message on failure.</param>
        public async void GenerateSpeechAudioClipFromText(
            string prompt,
            Action<AudioClip> onSuccess,
            Action<string> onError)
        {
            try
            {
                AudioClip audioClip = await TextToSpeechService.RequestAudioClip(prompt);
                onSuccess?.Invoke(audioClip);
            }
            catch (Exception e)
            {
                onError?.Invoke($"Failed to generate speech: {e.Message}");
            }
        }

        /// <summary>
        /// Requests and streams audio from a text prompt to an AudioSource.
        /// </summary>
        /// <param name="prompt">The text to be converted to speech.</param>
        /// <param name="audioSource">The AudioSource to stream the audio to.</param>
        public void RequestAudioAndStream(string prompt, AudioSource audioSource)
        {
            AudioStreamer audioStreamer = audioSource.GetComponent<AudioStreamer>();
            if (audioStreamer)
                audioStreamer.StartStreaming(prompt, TextToSpeechService);
            else
                Debug.LogError("Audio Source does not have an AudioStreamer component. Attach an AudioStreamer component to enable streaming.");
        }
    }
}