## VoiceBox: A Unified AI Services Framework for Unity
VoiceBox is a flexible and extensible framework for integrating various AI services into your Unity projects. It provides a unified interface for interacting with different AI APIs, allowing you to easily switch between services and add new ones.

## Features
- **Unified Service Interfaces**: Common interfaces for Chat, Speech-to-Text (STT), and Text-to-Speech (TTS) services.
- **Service Agnostic**: Easily switch between different AI service providers without changing your core application logic.
- **Configuration via ScriptableObjects**: Easily configure model selections, service endpoints, audio devices, and other settings in the Unity Editor.
- **Audio Streaming Support**: Built-in support for streaming audio for TTS, reducing latency and improving user experience.
- **Extensible**: Designed to be easily extended with new AI services and functionalities.
- **Modding Support**: Built with game modding applications in mind, providing utilities to make mod creation just as seamless as inside the Unity editor.

## Natively Supported Services
- **Chat**
	- Google Gemini
	- ChatGPT
	- Anthropic
	- Ollama
	- Deepseek (via Ollama)
- **Speech to Text**
	- Azure Speech services
	- Whisper via [WhisperLive](https://github.com/collabora/WhisperLive)
- **Text to Speech**
	- Elevenlabs
