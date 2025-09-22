using UnityEngine;
using System.Collections.Generic;
using System;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Generics;

/// <summary>
/// Represents the content of a message in the Gemini API.
/// </summary>
[System.Serializable]
public class GeminiContent
{
    /// <summary>
    /// The role of the content author.
    /// </summary>
    public string role;
    /// <summary>
    /// The parts of the content.
    /// </summary>
    public List<GeminiPart> parts;
}

/// <summary>
/// Represents a part of the content in the Gemini API.
/// </summary>
[System.Serializable]
public class GeminiPart
{
    /// <summary>
    /// The text of the part.
    /// </summary>
    public string text;
}

/// <summary>
/// Represents a tool that the model can use.
/// </summary>
[System.Serializable]
public class Tool
{
    /// <summary>
    /// A list of function declarations.
    /// </summary>
    public List<FunctionDeclaration> functionDeclarations;
}

/// <summary>
/// Represents a function declaration for a tool.
/// </summary>
[System.Serializable]
public class FunctionDeclaration
{
    /// <summary>
    /// The name of the function.
    /// </summary>
    public string name;
    /// <summary>
    /// The description of the function.
    /// </summary>
    public string description;
}


/// <summary>
/// Represents the configuration for a tool.
/// </summary>
[System.Serializable]
public class ToolConfig
{
    /// <summary>
    /// The function calling configuration.
    /// </summary>
    public FunctionCallingConfig functionCallingConfig;
}

/// <summary>
/// Represents the configuration for function calling.
/// </summary>
[System.Serializable]
public class FunctionCallingConfig
{
    /// <summary>
    /// The mode for function calling.
    /// </summary>
    public Mode mode;
    /// <summary>
    /// The available modes for function calling.
    /// </summary>
    public enum Mode
    {
        MODE_UNSPECIFIED,
        AUTO,
        ANY,
        NONE
    }
}

/// <summary>
/// Represents a safety setting for the Gemini API.
/// </summary>
[System.Serializable]
public class SafetySetting
{
    /// <summary>
    /// The category of harm.
    /// </summary>
    public HarmCategory category;
    /// <summary>
    /// The threshold for blocking harm.
    /// </summary>
    public HarmBlockThreshold threshold;
}

/// <summary>
/// The category of harm.
/// </summary>
public enum HarmCategory
{
    HARM_CATEGORY_UNSPECIFIED,
    HARM_CATEGORY_DEROGATORY,
    HARM_CATEGORY_TOXICITY,
    HARM_CATEGORY_VIOLENCE,
    HARM_CATEGORY_SEXUAL,
    HARM_CATEGORY_MEDICAL,
    HARM_CATEGORY_DANGEROUS,
    HARM_CATEGORY_HARASSMENT,
    HARM_CATEGORY_HATE_SPEECH,
    HARM_CATEGORY_SEXUALLY_EXPLICIT,
    HARM_CATEGORY_DANGEROUS_CONTENT
}

/// <summary>
/// The threshold for blocking harm.
/// </summary>
public enum HarmBlockThreshold
{
    HARM_BLOCK_THRESHOLD_UNSPECIFIED,
    BLOCK_LOW_AND_ABOVE,
    BLOCK_MEDIUM_AND_ABOVE,
    BLOCK_ONLY_HIGH,
    BLOCK_NONE
}

/// <summary>
/// Represents the generation configuration for the Gemini API.
/// </summary>
[System.Serializable]
public class GenerationConfig
{
    /// <summary>
    /// A list of stop sequences.
    /// </summary>
    public List<string> stopSequences;
    /// <summary>
    /// The number of generated responses to return.
    /// </summary>
    [Tooltip("Number of generated responses to return. If unset, this will default to 1.")]
    public int candidateCount;
    /// <summary>
    /// The maximum number of tokens to include in a candidate.
    /// </summary>
    [Tooltip("The maximum number of tokens to include in a candidate.")]
    public int maxOutputTokens;
    /// <summary>
    /// Controls the randomness of the output.
    /// </summary>
    [Tooltip("Controls the randomness of the output. Values can range from [0.0, 2.0].")]
    public float temperature;
    /// <summary>
    /// The maximum cumulative probability of tokens to consider when sampling.
    /// </summary>
    [Tooltip("The maximum cumulative probability of tokens to consider when sampling.")]
    public float topP;
    /// <summary>
    /// The maximum number of tokens to consider when sampling.
    /// </summary>
    [Tooltip("The maximum number of tokens to consider when sampling.")]
    public int topK;
}

/// <summary>
/// Configuration settings for the Gemini service.
/// </summary>
[CreateAssetMenu(fileName = "GeminiServiceConfig", menuName = "VoiceBox/GeminiService Configuration")]
public class GeminiServiceConfig : GenericChatServiceConfig
{
    public GeminiServiceConfig() 
    {
        serviceManagerType = typeof(GeminiServiceManager);
        apiKeyJSONString = "GEMINI_API_KEY";
    }

    /// <summary>
    /// The service endpoint for the Gemini API.
    /// </summary>
    public string serviceEndpoint;
    /// <summary>
    /// The name of the model to use.
    /// </summary>
    public string modelName;

    [Header("Content Generation Settings")]
    /// <summary>
    /// Optional. A list of Tools the model may use to generate the next response.
    /// </summary>
    [Tooltip("Optional. A list of Tools the model may use to generate the next response.")]
    public List<Tool> tools;
    /// <summary>
    /// Whether to use the tools.
    /// </summary>
    public bool useTools = false;

    /// <summary>
    /// Optional. Tool configuration for any Tool specified in the request.
    /// </summary>
    [Tooltip("Optional. Tool configuration for any Tool specified in the request.")]
    public ToolConfig toolConfig;

    /// <summary>
    /// Optional. A list of unique SafetySetting instances for blocking unsafe content.
    /// </summary>
    [Tooltip("Optional. A list of unique SafetySetting instances for blocking unsafe content.")]
    public List<SafetySetting> safetySettings;
    /// <summary>
    /// Whether to use the safety settings.
    /// </summary>
    public bool useSafetySettings = false;

    /// <summary>
    /// Optional. Developer set system instruction. Currently, text only.
    /// </summary>
    [Tooltip("Optional. Developer set system instruction. Currently, text only.")]
    public GeminiContent systemInstruction;

    /// <summary>
    /// Optional. Configuration options for model generation and outputs.
    /// </summary>
    [Tooltip("Optional. Configuration options for model generation and outputs.")]
    public GenerationConfig generationConfig;
    /// <summary>
    /// Whether to use the generation configuration.
    /// </summary>
    public bool useGenerationConfig = false;
}