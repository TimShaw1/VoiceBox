using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Generics;
using TimShaw.VoiceBox.Components;
using UnityEngine;

namespace TimShaw.VoiceBox.Core
{
    /// <summary>
    /// Defines the contract for any text-to-speech (TTS) service.
    /// This interface provides a standardized way to initialize and generate audio from text.
    /// </summary>
    public interface ITextToSpeechService
    {
        /// <summary>
        /// Initializes the service with the necessary configuration.
        /// </summary>
        /// <param name="config">A ScriptableObject containing API keys and other settings.</param>
        void Initialize(GenericTTSServiceConfig config);
        /// <summary>
        /// Requests an audio file from a text prompt.
        /// </summary>
        /// <param name="prompt">The text to be converted to speech.</param>
        /// <param name="fileName">The name of the output audio file, excluding the file extension.</param>
        /// <param name="dir">The directory to save the audio file in.</param>
        /// <param name="token"></param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task RequestAudioFile(string prompt, string fileName, string dir, CancellationToken token);

        /// <summary>
        /// Requests an AudioClip from a text prompt.
        /// </summary>
        /// <param name="prompt">The text to be converted to speech.</param>
        /// <returns>A task that represents the asynchronous operation, returning an AudioClip.</returns>
        public Task<AudioClip> RequestAudioClip(string prompt);

        /// <summary>
        /// Initializes a websocket (eg sets request headers) to interface with the TTS service
        /// </summary>
        /// <param name="webSocket">The websocket that should connect to the TTS service</param>
        /// <param name="mp3Decoder">The MP3 decoder to process the audio stream.</param>
        /// <param name="token"></param>
        public void InitWebsocket(ClientWebSocket webSocket, StreamingMp3Decoder mp3Decoder, CancellationToken token);

        /// <summary>
        /// Connects to a WebSocket and streams audio data.
        /// </summary>
        /// <param name="text">The text to be streamed as audio.</param>
        /// <param name="_webSocket">The WebSocket to use for the connection.</param>
        /// <param name="token">A cancellation token to stop the streaming.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task ConnectAndStream(string text, WebSocket _webSocket, CancellationToken token);
    }
}