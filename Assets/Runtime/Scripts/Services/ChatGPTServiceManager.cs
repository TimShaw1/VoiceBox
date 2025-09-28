using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Data;
using TimShaw.VoiceBox.Generics;
using Unity.VisualScripting.FullSerializer;

namespace TimShaw.VoiceBox.Core
{
    class ChatGPTServiceManager : IChatService
    {
        ChatClient _client;
        ChatGPTServiceConfig _config;
        public void Initialize(GenericChatServiceConfig config)
        {
            _config = config as ChatGPTServiceConfig;
            _client = new ChatClient(_config.model, _config.apiKey);
        }

        public async Task SendMessage(
            List<ChatMessage> messageHistory, 
            Action<ChatMessage> onSuccess, 
            Action<string> onError,
            CancellationToken token)
        {
            if (_client == null || _config == null)
            {
                onError?.Invoke("ChatGPTChatService is not initialized.");
                return;
            }

            try
            {
                var response = await _client.CompleteChatAsync(messageHistory, cancellationToken: token);
                onSuccess.Invoke(response.Value.Content[0].Text);
            }
            catch (Exception ex)
            {
                onError.Invoke(ex.Message);
            }
        }

        public async Task SendMessageStream(List<ChatMessage> messageHistory, Action<string> onChunkReceived, Action onComplete, Action<string> onError, CancellationToken token)
        {
            try
            {
                await foreach (
                    var update in _client.CompleteChatStreamingAsync(
                        messages: messageHistory,
                        cancellationToken: token
                    )
                )
                {
                    onChunkReceived(update.ContentUpdate[0].Text);
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
