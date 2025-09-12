using UnityEngine;

[CreateAssetMenu(fileName = "ChatServiceConfig", menuName = "VoiceBox/ChatService Configuration")]
public class GeminiServiceConfig : ScriptableObject
{
    public string serviceEndpoint;
    public string apiKey;
    public string modelName;
}