using UnityEngine;
using System.Collections.Generic;
using TimShaw.VoiceBox.Generics;
using TimShaw.VoiceBox.Core;

namespace TimShaw.VoiceBox.Data
{

    /// <summary>
    /// Represents the voice settings for the ElevenLabs Text-to-Speech service.
    /// </summary>
    [System.Serializable]
    public class VoiceSettings
    {
        /// <summary>
        /// A float between 0 and 1. Higher values result in more stable speech, but may sound monotonous.
        /// Lower values are more expressive, but may be unstable.
        /// </summary>
        [Tooltip("A float between 0 and 1. Higher values result in more stable speech, but may sound monotonous. Lower values are more expressive, but may be unstable.")]
        [Range(0f, 1f)]
        public float stability;

        /// <summary>
        /// A float between 0 and 1. Higher values boost the similarity to the original voice, but may introduce artifacts.
        /// Lower values are more generic, but cleaner.
        /// </summary>
        [Tooltip("A float between 0 and 1. Higher values boost the similarity to the original voice, but may introduce artifacts. Lower values are more generic, but cleaner.")]
        [Range(0f, 1f)]
        public float similarity_boost;

        /// <summary>
        /// A float between 0 and 1. A higher value will exaggerate the style of the voice.
        /// </summary>
        [Tooltip("A float between 0 and 1. A higher value will exaggerate the style of the voice.")]
        [Range(0f, 1f)]
        public float style;

        /// <summary>
        /// Whether to use the speaker boost feature, which can enhance voice clarity.
        /// </summary>
        [Tooltip("Whether to use the speaker boost feature, which can enhance voice clarity.")]
        public bool use_speaker_boost;
    }

    /// <summary>
    /// Represents a locator for a pronunciation dictionary.
    /// </summary>
    [System.Serializable]
    public class PronunciationDictionaryLocator
    {
        /// <summary>
        /// The ID of the pronunciation dictionary.
        /// </summary>
        [Tooltip("The ID of the pronunciation dictionary.")]
        public string pronunciation_dictionary_id;

        /// <summary>
        /// The version ID of the pronunciation dictionary.
        /// </summary>
        [Tooltip("The version ID of the pronunciation dictionary.")]
        public string version_id;
    }

    /// <summary>
    /// Configuration settings for the ElevenLabs Text-to-Speech (TTS) service.
    /// <br></br>
    /// This class sets <see cref="GenericTTSServiceConfig.apiKeyJSONString"/> to ELEVENLABS_API_KEY by default
    /// </summary>
    [CreateAssetMenu(fileName = "TTSServiceConfig", menuName = "VoiceBox/TTS/TTSService Configuration")]
    public class ElevenlabsTTSServiceConfig : GenericTTSServiceConfig
    {
        /// <summary>
        /// 
        /// </summary>
        public ElevenlabsTTSServiceConfig()
        {
            serviceManagerType = typeof(ElevenLabsTTSServiceManager);
            apiKeyJSONString = "ELEVENLABS_API_KEY";
        }

#if UNITY_EDITOR
        [Header("Core Configuration")]
#endif
        /// <summary>
        /// The API endpoint for the text-to-speech service. Defaults to <see href="https://api.elevenlabs.io/v1/text-to-speech/"/>
        /// </summary>
        [Tooltip("The API endpoint for the text-to-speech service.")]
        public string serviceEndpoint = "https://api.elevenlabs.io/v1/text-to-speech/";

        /// <summary>
        /// The ID of the voice you want to use.
        /// </summary>
        [Tooltip("The ID of the voice you want to use.")]
        public string voiceId = "JBFqnCBsd6RMkjVDRZzb";

        /// <summary>
        /// The ID of the model to be used. Defaults to eleven_multilingual_v2.
        /// </summary>
        [Tooltip("The ID of the model to be used. Defaults to eleven_multilingual_v2.")]
        public string modelID = "eleven_multilingual_v2";

#if UNITY_EDITOR
        [Header("Voice Settings")]
#endif
        /// <summary>
        /// Voice settings that override the stored settings for the given voice.
        /// </summary>
        [Tooltip("Voice settings that override the stored settings for the given voice.")]
        public VoiceSettings voiceSettings;

#if UNITY_EDITOR
        [Header("Audio Output")]
#endif
        /// <summary>
        /// Output format for the generated audio. Currently only mp3 is supported. e.g., mp3_44100_128
        /// </summary>
        [Tooltip("Output format for the generated audio. Currently only mp3 is supported. e.g., mp3_44100_128")]
        public string output_format = "mp3_44100_128";
    }
}