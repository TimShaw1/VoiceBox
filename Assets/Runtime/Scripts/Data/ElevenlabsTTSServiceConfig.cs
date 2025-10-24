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
        public ElevenlabsTTSServiceConfig()
        {
            serviceManagerType = typeof(ElevenLabsTTSServiceManager);
            apiKeyJSONString = "ELEVENLABS_API_KEY";
        }

        [Header("Core Configuration")]
        /// <summary>
        /// The API endpoint for the text-to-speech service.
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

        [Header("Voice Settings")]
        /// <summary>
        /// Voice settings that override the stored settings for the given voice.
        /// </summary>
        [Tooltip("Voice settings that override the stored settings for the given voice.")]
        public VoiceSettings voiceSettings;

        [Header("Audio Output")]
        /// <summary>
        /// Output format for the generated audio. Currently only mp3 is supported. e.g., mp3_44100_128
        /// </summary>
        [Tooltip("Output format for the generated audio. Currently only mp3 is supported. e.g., mp3_44100_128")]
        public string output_format = "mp3_44100_128";

        [Header("Generation Parameters")]
        /// <summary>
        /// An optional ISO 639-1 language code to enforce a language for the model. NOTE: Will error if the model does not support it.
        /// </summary>
        [Tooltip("An optional ISO 639-1 language code to enforce a language for the model. NOTE: Will error if the model does not support it.")]
        public string language_code;

        /// <summary>
        /// A seed value for deterministic sampling. Must be an integer between 0 and 4294967295.
        /// </summary>
        [Tooltip("A seed value for deterministic sampling. Must be an integer between 0 and 4294967295.")]
        public int? seed;

        /// <summary>
        /// When set to false, zero retention mode will be used.
        /// </summary>
        [Tooltip("When set to false, zero retention mode will be used.")]
        public bool enable_logging = true;

        [Header("Pronunciation Dictionaries")]
        /// <summary>
        /// A list of pronunciation dictionaries to apply to the text. Up to 3 locators per request.
        /// </summary>
        [Tooltip("A list of pronunciation dictionaries to apply to the text. Up to 3 locators per request.")]
        public List<PronunciationDictionaryLocator> pronunciation_dictionary_locators;

        [Header("Continuity")]
        /// <summary>
        /// Text that came before the current request to improve continuity.
        /// </summary>
        [Tooltip("Text that came before the current request to improve continuity.")]
        public string previous_text;

        /// <summary>
        /// Text that comes after the current request to improve continuity.
        /// </summary>
        [Tooltip("Text that comes after the current request to improve continuity.")]
        public string next_text;

        /// <summary>
        /// A list of request IDs of samples generated before this one. Max 3.
        /// </summary>
        [Tooltip("A list of request IDs of samples generated before this one. Max 3.")]
        public List<string> previous_request_ids;

        /// <summary>
        /// A list of request IDs of samples that will come after this one. Max 3.
        /// </summary>
        [Tooltip("A list of request IDs of samples that will come after this one. Max 3.")]
        public List<string> next_request_ids;

        [Header("Text Normalization")]
        /// <summary>
        /// Controls text normalization. Can be 'auto', 'on', or 'off'.
        /// </summary>
        [Tooltip("Controls text normalization. Can be 'auto', 'on', or 'off'.")]
        public string apply_text_normalization = "auto";

        /// <summary>
        /// Controls language-specific text normalization. WARNING: Can increase latency.
        /// </summary>
        [Tooltip("Controls language-specific text normalization. WARNING: Can increase latency.")]
        public bool apply_language_text_normalization = false;

        [Header("Deprecated")]
        /// <summary>
        /// [Deprecated] Latency optimizations. 0 for default, 1-4 for increasing optimization.
        /// </summary>
        [Tooltip("[Deprecated] Latency optimizations. 0 for default, 1-4 for increasing optimization.")]
        public int? optimize_streaming_latency;
    }
}