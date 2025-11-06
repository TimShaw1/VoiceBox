

using Microsoft.Extensions.AI;
using OllamaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Data;
using TimShaw.VoiceBox.Generics;

namespace TimShaw.VoiceBox.Core
{
    /// <summary>
    /// Manages the Ollama service, implementing the IChatService interface.
    /// </summary>
    public class OllamaChatServiceManager : IChatService
    {
        IChatClient _client;
        OllamaChatServiceConfig _config;

        /// <summary>
        /// Initializes the Ollama service with the provided configuration.
        /// </summary>
        /// <param name="config">The ScriptableObject configuration for the Ollama service.</param>
        public void Initialize(GenericChatServiceConfig config)
        {
            _config = config as OllamaChatServiceConfig;
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(_config.serviceEndpoint);
            httpClient.DefaultRequestHeaders.Add("Authorization", _config.apiKey);

            _client = new OllamaApiClient(httpClient, _config.modelName);

        }

        /// <summary>
        /// Sends a message to the Ollama service.
        /// </summary>
        /// <param name="messageHistory">The list of messages representing the conversation so far.</param>
        /// <param name="onSuccess">Callback invoked when a successful response is received. The ChatMessage will have the 'Model' role.</param>
        /// <param name="onError">Callback invoked when an error occurs, providing an error message.</param>
        /// <param name="options">Request-level settings.</param>
        /// <param name="token"></param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SendMessage(
            List<ChatUtils.VoiceBoxChatMessage> messageHistory,
            Action<ChatUtils.VoiceBoxChatMessage> onSuccess,
            Action<string> onError,
            ChatUtils.VoiceBoxChatCompletionOptions options,
            CancellationToken token
        )
        {
            if (_client == null || _config == null)
            {
                onError?.Invoke("Ollama is not initialized.");
                return;
            }

            if (options.Tools?.Count > 0)
            {
                UnityEngine.Debug.LogWarning("OllamaChatServiceManager: Tool calling with Ollama models is currently unsupported may and cause unexpected behaviour.");
            }

            try
            {
                var response = await _client.GetResponseAsync(messageHistory, options, token);

                onSuccess?.Invoke(new ChatUtils.VoiceBoxChatMessage(response.Messages[0]));
            }
            catch (Exception e)
            {
                onError?.Invoke(e.Message + e.StackTrace);
            }

        }

        /// <summary>
        /// Sends a message to the Ollama service and streams the response.
        /// </summary>
        /// <param name="messageHistory">The list of messages representing the conversation so far.</param>
        /// <param name="onChunkReceived">Callback invoked for each partial chunk of the response received from the stream.</param>
        /// <param name="onComplete">Callback invoked when the entire stream has finished.</param>
        /// <param name="onError">Callback invoked if an error occurs during the streaming process.</param>
        /// <param name="options">Request-level settings.</param>
        /// <param name="token"></param>
        public async Task SendMessageStream(
            List<ChatUtils.VoiceBoxChatMessage> messageHistory,
            Action<ChatResponseUpdate> onChunkReceived,
            Action onComplete,
            Action<string> onError,
            ChatUtils.VoiceBoxChatCompletionOptions options,
            CancellationToken token
        )
        {
            try
            {
                if (options.Tools?.Count > 0)
                {
                    UnityEngine.Debug.LogWarning("OllamaChatServiceManager: Tool calling with Ollama models is currently unsupported may and cause unexpected behaviour.");
                }
                await foreach (ChatResponseUpdate item in _client.GetStreamingResponseAsync(messageHistory, options, token))
                {
                    onChunkReceived?.Invoke(item);
                }

                onComplete.Invoke();
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message + ex.StackTrace);
            }
        }
    }
}