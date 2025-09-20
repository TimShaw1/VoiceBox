using UnityEngine;
using System.Collections.Generic;

// Nested class for voice_settings parameter
[System.Serializable]
public class VoiceSettings
{
    [Tooltip("A float between 0 and 1. Higher values result in more stable speech, but may sound monotonous. Lower values are more expressive, but may be unstable.")]
    [Range(0f, 1f)]
    public float stability;

    [Tooltip("A float between 0 and 1. Higher values boost the similarity to the original voice, but may introduce artifacts. Lower values are more generic, but cleaner.")]
    [Range(0f, 1f)]
    public float similarity_boost;

    [Tooltip("A float between 0 and 1. A higher value will exaggerate the style of the voice.")]
    [Range(0f, 1f)]
    public float style;

    [Tooltip("Whether to use the speaker boost feature, which can enhance voice clarity.")]
    public bool use_speaker_boost;
}

// Nested class for pronunciation_dictionary_locators
[System.Serializable]
public class PronunciationDictionaryLocator
{
    [Tooltip("The ID of the pronunciation dictionary.")]
    public string pronunciation_dictionary_id;

    [Tooltip("The version ID of the pronunciation dictionary.")]
    public string version_id;
}


[CreateAssetMenu(fileName = "TTSServiceConfig", menuName = "VoiceBox/TTSService Configuration")]
public class ElevenlabsTTSServiceConfig : ScriptableObject
{
    [Header("Core Configuration")]
    [Tooltip("The API endpoint for the text-to-speech service.")]
    public string serviceEndpoint = "https://api.elevenlabs.io/v1/text-to-speech/";

    [Tooltip("Your ElevenLabs API key.")]
    public string apiKey;

    [Tooltip("The ID of the voice you want to use.")]
    public string voiceId;

    [Tooltip("The ID of the model to be used. Defaults to eleven_multilingual_v2.")]
    public string modelID = "eleven_multilingual_v2";

    [Header("Voice Settings")]
    [Tooltip("Voice settings that override the stored settings for the given voice.")]
    public VoiceSettings voiceSettings;

    [Header("Audio Output")]
    [Tooltip("Output format for the generated audio. Currently only mp3 is supported. e.g., mp3_44100_128")]
    public string output_format = "mp3_44100_128";

    [Header("Generation Parameters")]
    [Tooltip("An optional ISO 639-1 language code to enforce a language for the model. NOTE: Will error if the model does not support it.")]
    public string language_code;

    [Tooltip("A seed value for deterministic sampling. Must be an integer between 0 and 4294967295.")]
    public int? seed;

    [Tooltip("When set to false, zero retention mode will be used.")]
    public bool enable_logging = true;

    [Header("Pronunciation Dictionaries")]
    [Tooltip("A list of pronunciation dictionaries to apply to the text. Up to 3 locators per request.")]
    public List<PronunciationDictionaryLocator> pronunciation_dictionary_locators;

    [Header("Continuity")]
    [Tooltip("Text that came before the current request to improve continuity.")]
    public string previous_text;

    [Tooltip("Text that comes after the current request to improve continuity.")]
    public string next_text;

    [Tooltip("A list of request IDs of samples generated before this one. Max 3.")]
    public List<string> previous_request_ids;

    [Tooltip("A list of request IDs of samples that will come after this one. Max 3.")]
    public List<string> next_request_ids;

    [Header("Text Normalization")]
    [Tooltip("Controls text normalization. Can be 'auto', 'on', or 'off'.")]
    public string apply_text_normalization = "auto";

    [Tooltip("Controls language-specific text normalization. WARNING: Can increase latency.")]
    public bool apply_language_text_normalization = false;

    [Header("Deprecated")]
    [Tooltip("[Deprecated] Latency optimizations. 0 for default, 1-4 for increasing optimization.")]
    public int? optimize_streaming_latency;
}