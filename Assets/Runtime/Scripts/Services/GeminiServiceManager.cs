using Microsoft.Extensions.AI;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Data;
using TimShaw.VoiceBox.Generics;
using UnityEngine;

namespace TimShaw.VoiceBox.Core
{
    /// <summary>
    /// Manages the Gemini service, implementing the IChatService interface.
    /// </summary>
    public class GeminiServiceManager : IChatService
    {
        private IChatClient _client;
        private GeminiServiceConfig _config;

        /// <summary>
        /// Initializes the Gemini service with the provided configuration.
        /// </summary>
        /// <param name="config">The ScriptableObject configuration for the Gemini service.</param>
        public void Initialize(GenericChatServiceConfig config)
        {
            if (config is GeminiServiceConfig geminiConfig)
            {
                _config = geminiConfig;

                var options = new OpenAIClientOptions()
                {
                    Endpoint = new Uri(_config.serviceEndpoint)
                };

                _client = new ChatClientBuilder(
                    new OpenAIClient(new System.ClientModel.ApiKeyCredential(config.apiKey), options).GetChatClient(config.modelName ?? "gemini-2.5-flash").AsIChatClient()
                ).UseFunctionInvocation().Build();
            }
            else
            {
                Debug.LogError("Invalid configuration provided to GeminiChatService. Expected GeminiServiceConfig.");
            }
        }

        /// <summary>
        /// Sends a message to the Gemini service.
        /// </summary>
        /// <param name="messageHistory">The list of messages representing the conversation so far.</param>
        /// <param name="options">Request-level settings.</param>
        /// <param name="onSuccess">Callback invoked when a successful response is received. The ChatMessage will have the 'Model' role.</param>
        /// <param name="onError">Callback invoked when an error occurs, providing an error message.</param>
        /// <param name="token"></param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SendMessage(
            List<ChatUtils.VoiceBoxChatMessage> messageHistory,
            ChatUtils.VoiceBoxChatCompletionOptions options,
            Action<ChatUtils.VoiceBoxChatMessage> onSuccess,
            Action<string> onError,
            CancellationToken token)
        {
            if (_client == null || _config == null)
            {
                onError?.Invoke("GeminiChatService is not initialized.");
                return;
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
        /// Sends a message to the Gemini service and streams the response.
        /// </summary>
        /// <param name="messageHistory">The list of messages representing the conversation so far.</param>
        /// <param name="options">Request-level settings.</param>
        /// <param name="onChunkReceived">Callback invoked for each partial chunk of the response received from the stream.</param>
        /// <param name="onComplete">Callback invoked when the entire stream has finished.</param>
        /// <param name="onError">Callback invoked if an error occurs during the streaming process.</param>
        /// <param name="token"></param>
        public async Task SendMessageStream(
            List<ChatUtils.VoiceBoxChatMessage> messageHistory,
            ChatUtils.VoiceBoxChatCompletionOptions options,
            Action<ChatResponseUpdate> onChunkReceived,
            Action onComplete,
            Action<string> onError,
            CancellationToken token
        )
        {
            try
            {
                await foreach (ChatResponseUpdate item in _client.GetStreamingResponseAsync(messageHistory, options, token))
                {
                    onChunkReceived?.Invoke(item);
                }

                onComplete?.Invoke();
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message + ex.StackTrace);
            }
        }
    }
}