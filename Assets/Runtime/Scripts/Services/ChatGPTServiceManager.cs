using Azure;
using Microsoft.Extensions.AI;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Data;
using TimShaw.VoiceBox.Generics;
using Unity.VisualScripting.FullSerializer;

namespace TimShaw.VoiceBox.Core
{
    class ChatGPTServiceManager : IChatService
    {
        IChatClient _client;
        ChatGPTServiceConfig _config;
        public void Initialize(GenericChatServiceConfig config)
        {
            _config = config as ChatGPTServiceConfig;

            
            var options = new OpenAIClientOptions()
            {
                Endpoint = _config.serviceEndpoint.Length > 0 ? new Uri(_config.serviceEndpoint) : null
            };

            _client = new ChatClientBuilder(
                    new OpenAIClient(new System.ClientModel.ApiKeyCredential(config.apiKey), options).GetChatClient(config.modelName ?? "gpt-4o").AsIChatClient()
                ).UseFunctionInvocation().Build();

        }

        public async Task SendMessage(
            List<ChatMessage> messageHistory,
            ChatUtils.VoiceBoxChatCompletionOptions options,
            Action<ChatMessage> onSuccess, 
            Action<string> onError,
            CancellationToken token)
        {
            if (_client == null || _config == null)
            {
                onError?.Invoke("ChatGPT service is not initialized.");
                return;
            }

            try
            {
                var response = await _client.GetResponseAsync(messageHistory, options, token);

                onSuccess.Invoke(response.Messages[0]);
            }
            catch (Exception e)
            {
                onError.Invoke(e.Message);
            }
        }

        public async Task SendMessageStream(
            List<ChatMessage> messageHistory,
            ChatUtils.VoiceBoxChatCompletionOptions options,
            Action<ChatResponseUpdate> onChunkReceived, 
            Action onComplete, 
            Action<string> onError, 
            CancellationToken token)
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
                onError.Invoke(ex.Message);
            }
        }
    }
}
