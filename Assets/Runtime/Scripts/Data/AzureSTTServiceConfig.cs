using NAudio.CoreAudioApi;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using TimShaw.VoiceBox.Editor;
using TimShaw.VoiceBox.STT;

[CreateAssetMenu(fileName = "AzureSTTServiceConfig", menuName = "VoiceBox/AzureSTTService Configuration")]
public class AzureSTTServiceConfig : ScriptableObject
{
    public string apiKey;
    public string region;
    public string language;

    [Options("audioInputEndpointNames")]
    public string audioInputDeviceName;

    public string[] audioInputEndpointNames;

    public void OnValidate()
    {
        audioInputEndpointNames = AzureSTTServiceManager.GetAudioInputEndpoints().Keys.ToArray();
    }
}