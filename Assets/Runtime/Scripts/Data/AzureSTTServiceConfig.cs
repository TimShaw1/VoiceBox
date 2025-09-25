using System.Linq;
using UnityEngine;
using TimShaw.VoiceBox.Editor;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Generics;

/// <summary>
/// Configuration settings for the Azure Speech-to-Text (STT) service.
/// </summary>
[CreateAssetMenu(fileName = "AzureSTTServiceConfig", menuName = "VoiceBox/AzureSTTService Configuration")]
public class AzureSTTServiceConfig : GenericSTTServiceConfig
{
    public AzureSTTServiceConfig() 
    {
        serviceManagerType = typeof(AzureSTTServiceManager);
        apiKeyJSONString = "AZURE_API_KEY";
    }

    public override void _Init()
    {
        serviceManagerType = typeof(AzureSTTServiceManager);
        apiKeyJSONString = "AZURE_API_KEY";
    }

    /// <summary>
    /// The region for the Azure STT service.
    /// </summary>
    public string region;
    /// <summary>
    /// The language for speech recognition.
    /// </summary>
    public string language;

    /// <summary>
    /// The name of the audio input device to use for transcription.
    /// </summary>
    [Options("audioInputEndpointNames")]
    public string audioInputDeviceName;

    /// <summary>
    /// An array of available audio input device names.
    /// </summary>
    public string[] audioInputEndpointNames;

    /// <summary>
    /// Called when the script is loaded or a value is changed in the Inspector.
    /// Populates the list of available audio input devices.
    /// </summary>
    public void OnValidate()
    {
        audioInputEndpointNames = AzureSTTServiceManager.GetAudioInputEndpoints().Keys.ToArray();
    }
}