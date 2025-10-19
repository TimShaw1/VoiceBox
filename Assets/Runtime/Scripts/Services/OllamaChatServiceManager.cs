

using Microsoft.Extensions.AI;
using OllamaSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Data;
using TimShaw.VoiceBox.Generics;

namespace TimShaw.VoiceBox.Core
{
    public class OllamaChatServiceManager : IChatService
    {
        IChatClient _client;
        OllamaChatServiceConfig _config;
        public void Initialize(GenericChatServiceConfig config)
        {
            _config = config as OllamaChatServiceConfig;

            _client = new OllamaApiClient(new Uri(_config.serviceEndpoint), _config.modelName);

        }

        /// <summary>
        /// Sends a message to the Ollama service.
        /// </summary>
        /// <param name="messageHistory">The history of messages in the conversation.</param>
        /// <param name="onSuccess">Callback invoked when the message is successfully sent.</param>
        /// <param name="onError">Callback invoked when an error occurs.</param>
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
                onError?.Invoke("Ollama is not initialized.");
                return;
            }

            try
            {
                var response = await _client.GetResponseAsync(messageHistory, options, token);

                onSuccess.Invoke(new ChatUtils.VoiceBoxChatMessage(response.Messages[0]));
            }
            catch (Exception e)
            {
                onError.Invoke(e.Message + e.StackTrace);
            }

        }

        /// <summary>
        /// Sends a message to the Ollama service and streams the response.
        /// </summary>
        /// <param name="messageHistory">The history of messages in the conversation.</param>
        /// <param name="onChunkReceived">Callback invoked when a chunk of the response is received.</param>
        /// <param name="onComplete">Callback invoked when the response is complete.</param>
        /// <param name="onError">Callback invoked when an error occurs.</param>
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
                    onChunkReceived.Invoke(item);
                }

                onComplete.Invoke();
            }
            catch (Exception ex)
            {
                onError.Invoke(ex.Message + ex.StackTrace);
            }
        }
    }
}