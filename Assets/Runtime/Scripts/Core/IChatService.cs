using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Generics;
using UnityEngine;

namespace TimShaw.VoiceBox.Core
{
    /// <summary>
    /// Defines the contract for any chat service (e.g., Gemini, ChatGPT, etc.).
    /// This interface provides a standardized way to send messages and receive responses,
    /// abstracting away the specific details of each API.
    /// </summary>
    public interface IChatService
    {
        /// <summary>
        /// Initializes the service with the necessary configuration.
        /// This should be called before any other methods.
        /// </summary>
        /// <param name="config">A ScriptableObject containing API keys, model names, and other settings.</param>
        void Initialize(GenericChatServiceConfig config);

        /// <summary>
        /// Sends a complete conversation history to the chat service and waits for a single, complete response.
        /// </summary>
        /// <param name="messageHistory">The list of messages representing the conversation so far.</param>
        /// <param name="options">Request-level settings.</param>
        /// <param name="onSuccess">Callback invoked when a successful response is received. The ChatMessage will have the 'Model' role.</param>
        /// <param name="onError">Callback invoked when an error occurs, providing an error message.</param>
        /// <param name="token"></param>
        Task SendMessage(
            List<ChatUtils.VoiceBoxChatMessage> messageHistory,
            ChatUtils.VoiceBoxChatCompletionOptions options,
            Action<ChatUtils.VoiceBoxChatMessage> onSuccess,
            Action<string> onError,
            CancellationToken token
        );

        /// <summary>
        /// Sends a conversation history and streams the response back in chunks.
        /// This is useful for creating a "typing" effect and reducing perceived latency.
        /// </summary>
        /// <param name="messageHistory">The list of messages representing the conversation so far.</param>
        /// <param name="options">Request-level settings.</param>
        /// <param name="onChunkReceived">Callback invoked for each partial chunk of the response received from the stream.</param>
        /// <param name="onComplete">Callback invoked when the entire stream has finished.</param>
        /// <param name="onError">Callback invoked if an error occurs during the streaming process.</param>
        /// <param name="token"></param>
        Task SendMessageStream(
            List<ChatUtils.VoiceBoxChatMessage> messageHistory,
            ChatUtils.VoiceBoxChatCompletionOptions options,
            Action<ChatResponseUpdate> onChunkReceived,
            Action onComplete,
            Action<string> onError,
            CancellationToken token
        );
    }
}