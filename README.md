# VoiceBox: A Unified AI Services Framework for Unity

VoiceBox is a flexible and extensible framework for integrating various AI services into your Unity projects. It provides a unified interface for interacting with different AI APIs, allowing you to easily switch between services and add new ones.

## Features

- **Unified Service Interfaces**: Common interfaces for Chat, Speech-to-Text (STT), and Text-to-Speech (TTS) services.
- **Service Agnostic**: Easily switch between different AI service providers without changing your core application logic.
- **Configuration via ScriptableObjects**: Easily configure API keys, service endpoints, and other settings in the Unity Editor.
- **Streaming Support**: Built-in support for streaming audio for TTS, reducing latency and improving user experience.
- **Extensible**: Designed to be easily extended with new AI services and functionalities.

## Supported Services

- **Chat**:
  - Google Gemini
  - OpenAI ChatGPT
  - Anthropic TODO
  - DeepSeek TODO
  - Ollama TODO
- **Speech-to-Text (STT)**:
  - Microsoft Azure Cognitive Services
  - Google Chirp TODO
- **Text-to-Speech (TTS)**:
  - ElevenLabs
  - Google Cloud TODO
  - Azure TODO

## Setup

1.  **Clone the repository**:
    ```bash
    git clone https://github.com/TimShaw1/VoiceBox.git
    ```
2.  **Open the project in Unity**:
    - Open Unity Hub and click "Add".
    - Select the cloned repository's root folder.
    - Open the project.
3.  **Create API Key Configuration**:
    - In the `Assets` directory of your project, create a new file named `keys.json`.
    - Add your API keys to this file in the following format:
      ```json
      {
        "GEMINI_API_KEY": "your_gemini_api_key",
        "AZURE_API_KEY": "your_azure_api_key",
        "ELEVENLABS_API_KEY": "your_elevenlabs_api_key"
      }
      ```
4.  **Configure Services**:
    - In the Unity Editor, navigate to the `Assets/Settings/AI_Configs` directory.
    - Here you will find ScriptableObject configurations for each service:
      - `GeminiServiceConfig.asset`
      - `AzureSTTServiceConfig.asset`
      - `TTSServiceConfig.asset`
    - Select each configuration file and fill in the required fields in the Inspector, such as model names, regions, and voice IDs.
5.  **Add AIManager to your scene**:
    - Create a new empty GameObject in your scene and name it "AIManager".
    - Add the `AIManager.cs` script to this GameObject.
    - Drag and drop the service configuration files from `Assets/Settings/AI_Configs` into the corresponding fields in the `AIManager` component in the Inspector.

## Usage

The `AIManager` is a singleton that provides a central point of access to all AI services. You can access it from any script using `AIManager.Instance`.

### Chat

To send a chat message, you can use the `SendChatMessage` method:

```csharp
using TimShaw.VoiceBox.Core;
using System.Collections.Generic;
using UnityEngine;

public class ChatExample : MonoBehaviour
{
    void Start()
    {
        var messageHistory = new List<ChatMessage>
        {
            new ChatMessage(MessageRole.User, "Hello, what's the weather like today?")
        };

        AIManager.Instance.SendChatMessage(
            messageHistory,
            response => Debug.Log("AI Response: " + response.Content),
            error => Debug.LogError("Chat Error: " + error)
        );
    }
}
```

### Speech-to-Text (STT)

To start and stop speech transcription, you can use the `StartSpeechTranscription` and `StopSpeechTranscription` methods. You can subscribe to the `OnRecognized` event to receive the transcribed text.

```csharp
using Microsoft.CognitiveServices.Speech;
using UnityEngine;

public class STTExample : MonoBehaviour
{
    void Start()
    {
        AIManager.Instance.OnRecognized += HandleSpeechRecognized;
        AIManager.Instance.StartSpeechTranscription();
    }

    void HandleSpeechRecognized(object sender, SpeechRecognitionEventArgs e)
    {
        if (e.Result.Reason == ResultReason.RecognizedSpeech)
        {
            Debug.Log("Recognized: " + e.Result.Text);
        }
    }

    void OnDestroy()
    {
        AIManager.Instance.OnRecognized -= HandleSpeechRecognized;
    }
}
```

### Text-to-Speech (TTS)

To generate an audio clip from text, you can use the `GenerateSpeechAudioClipFromText` method:

```csharp
using UnityEngine;

public class TTSExample : MonoBehaviour
{
    public AudioSource audioSource;

    void Start()
    {
        AIManager.Instance.GenerateSpeechAudioClipFromText(
            "Hello, this is a test of the text-to-speech service.",
            audioClip => audioSource.PlayOneShot(audioClip),
            error => Debug.LogError("TTS Error: " + error)
        );
    }
}
```

For streaming audio, you can use the `RequestAudioAndStream` method:

```csharp
using UnityEngine;

public class TTSStreamExample : MonoBehaviour
{
    public AudioSource audioSource;

    void Start()
    {
        // Ensure the AudioSource has an AudioStreamer component attached
        if (audioSource.GetComponent<AudioStreamer>() == null)
        {
            audioSource.AddComponent<AudioStreamer>();
        }

        AIManager.Instance.RequestAudioAndStream(
            "This is a test of the streaming text-to-speech service.",
            audioSource
        );
    }
}
```

## Extending the Framework

To add a new service, you need to:

1.  **Implement the service interface**: Create a new class that implements one of the service interfaces (`IChatService`, `ISpeechToTextService`, or `ITextToSpeechService`).
2.  **Create a configuration ScriptableObject**: Create a new class that inherits from `ScriptableObject` to hold the configuration for your new service.
3.  **Update the `ServiceFactory`**: Add a new case to the corresponding `Create` method in the `ServiceFactory` to create an instance of your new service.

---
*This README was generated by an AI assistant.*
