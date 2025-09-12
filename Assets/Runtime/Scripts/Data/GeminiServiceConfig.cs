using UnityEngine;
using System.Collections.Generic;
using TimShaw.VoiceBox.Core;

// Placeholder for the Content object
[System.Serializable]
public class GeminiContent
{
    public string role;
    public List<GeminiPart> parts;
}

// Placeholder for the Part object
[System.Serializable]
public class GeminiPart
{
    public string text;
}

// Placeholder for the Tool object
[System.Serializable]
public class Tool
{
    // See the documentation for the full structure of a Tool object
    public List<FunctionDeclaration> functionDeclarations;
}

// Placeholder for FunctionDeclaration
[System.Serializable]
public class FunctionDeclaration
{
    public string name;
    public string description;
}


// Placeholder for the ToolConfig object
[System.Serializable]
public class ToolConfig
{
    // See the documentation for the full structure of a ToolConfig object
    public FunctionCallingConfig functionCallingConfig;
}

[System.Serializable]
public class FunctionCallingConfig
{
    public Mode mode;
    public enum Mode
    {
        MODE_UNSPECIFIED,
        AUTO,
        ANY,
        NONE
    }
}


[System.Serializable]
public class SafetySetting
{
    public HarmCategory category;
    public HarmBlockThreshold threshold;
}

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

public enum HarmBlockThreshold
{
    HARM_BLOCK_THRESHOLD_UNSPECIFIED,
    BLOCK_LOW_AND_ABOVE,
    BLOCK_MEDIUM_AND_ABOVE,
    BLOCK_ONLY_HIGH,
    BLOCK_NONE
}


[System.Serializable]
public class GenerationConfig
{
    public List<string> stopSequences;
    [Tooltip("Number of generated responses to return. If unset, this will default to 1.")]
    public int candidateCount;
    [Tooltip("The maximum number of tokens to include in a candidate.")]
    public int maxOutputTokens;
    [Tooltip("Controls the randomness of the output. Values can range from [0.0, 2.0].")]
    public float temperature;
    [Tooltip("The maximum cumulative probability of tokens to consider when sampling.")]
    public float topP;
    [Tooltip("The maximum number of tokens to consider when sampling.")]
    public int topK;
}


[CreateAssetMenu(fileName = "GeminiServiceConfig", menuName = "VoiceBox/GeminiService Configuration")]
public class GeminiServiceConfig : ScriptableObject
{
    public string serviceEndpoint;
    public string apiKey;
    public string modelName;

    [Header("Content Generation Settings")]
    [Tooltip("Optional. A list of Tools the model may use to generate the next response.")]
    public List<Tool> tools;
    public bool useTools = false;

    [Tooltip("Optional. Tool configuration for any Tool specified in the request.")]
    public ToolConfig toolConfig;

    [Tooltip("Optional. A list of unique SafetySetting instances for blocking unsafe content.")]
    public List<SafetySetting> safetySettings;
    public bool useSafetySettings = false;

    [Tooltip("Optional. Developer set system instruction. Currently, text only.")]
    public GeminiContent systemInstruction;

    [Tooltip("Optional. Configuration options for model generation and outputs.")]
    public GenerationConfig generationConfig;
    public bool useGenerationConfig = false;
}