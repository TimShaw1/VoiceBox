using UnityEngine;

[CreateAssetMenu(fileName = "ChatServiceConfig", menuName = "VoiceBox/ChatService Configuration")]
public class ChatServiceConfig : ScriptableObject
{
    public string serviceEndpoint;
    public string apiKey;
    public string modelName;
}