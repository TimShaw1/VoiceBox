using Microsoft.Extensions.AI;
using OpenAI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Data;
using TimShaw.VoiceBox.Generics;

namespace TimShaw.VoiceBox.Core
{
    class ClaudeServiceManager : IChatService
    {
        IChatClient _client;
        ClaudeServiceConfig _config;
        public void Initialize(GenericChatServiceConfig config)
        {
            _config = config as ClaudeServiceConfig;


            var options = new OpenAIClientOptions()
            {
                Endpoint = new Uri(_config.serviceEndpoint)
            };

            _client = new ChatClientBuilder(
                    new OpenAIClient(new System.ClientModel.ApiKeyCredential(config.apiKey), options).GetChatClient(config.modelName ?? "claude-haiku-4-5").AsIChatClient()
                ).UseFunctionInvocation().Build();

        }

        public async Task SendMessage(
            List<ChatUtils.VoiceBoxChatMessage> messageHistory,
            ChatUtils.VoiceBoxChatCompletionOptions options,
            Action<ChatUtils.VoiceBoxChatMessage> onSuccess,
            Action<string> onError,
            CancellationToken token)
        {
            if (_client == null || _config == null)
            {
                onError?.Invoke("Claude service is not initialized.");
                return;
            }

            try
            {
                var response = await _client.GetResponseAsync(messageHistory, options, token);

                onSuccess?.Invoke(new ChatUtils.VoiceBoxChatMessage(response.Messages[0]));
            }
            catch (Exception e)
            {
                onError?.Invoke(e.Message);
            }
        }

        public async Task SendMessageStream(
            List<ChatUtils.VoiceBoxChatMessage> messageHistory,
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
                    onChunkReceived?.Invoke(item);
                }

                onComplete?.Invoke();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
                onError?.Invoke(ex.Message);
            }
        }
    }
}
