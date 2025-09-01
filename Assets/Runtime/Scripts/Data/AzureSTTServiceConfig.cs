using UnityEngine;

[CreateAssetMenu(fileName = "AzureSTTServiceConfig", menuName = "VoiceBox/AzureSTTService Configuration")]
public class AzureSTTServiceConfig : ScriptableObject
{
    public string apiKey;
    public string region;
    public string language;
    public string audioInputDeviceName;
}