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
            OpenAIUtils.VoiceBoxChatCompletionOptions options,
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
                var response = await _client.CompleteChatAsync(messageHistory, options, cancellationToken: token);
                foreach (ChatToolCall toolCall in response.Value.ToolCalls)
                {
                    foreach (var toolIhave in options.VoiceBoxTools)
                    {
                        if (toolCall.FunctionName == toolIhave.Method.Name)
                        {
                            OpenAIUtils.InvokeMethodWithJsonArguments(toolIhave.Method, toolIhave.Caller, toolCall.FunctionArguments);
                        }
                    }
                }
                if (response.Value.Content.Count == 0)
                    onSuccess.Invoke("");
                else
                    onSuccess.Invoke(new AssistantChatMessage(response.Value.Content));
            }
            catch (Exception ex)
            {
                onError.Invoke(ex.Message);
            }
        }

        public async Task SendMessageStream(
            List<ChatMessage> messageHistory,
            OpenAIUtils.VoiceBoxChatCompletionOptions options,
            Action<string> onChunkReceived, 
            Action onComplete, 
            Action<string> onError, 
            CancellationToken token)
        {
            try
            {
                OpenAIUtils.StreamingChatToolCallsBuilder toolBuilder = new();
                await foreach (
                    var streamingChatUpdate in _client.CompleteChatStreamingAsync(
                        messages: messageHistory,
                        options: (options as ChatCompletionOptions),
                        cancellationToken: token
                    )
                )
                {
                    foreach (ChatMessageContentPart contentPart in streamingChatUpdate.ContentUpdate)
                    {
                        onChunkReceived(contentPart.Text);
                    }

                    foreach (StreamingChatToolCallUpdate toolCallUpdate in streamingChatUpdate.ToolCallUpdates)
                    {
                        toolBuilder.Append(toolCallUpdate);
                    }
                }

                IReadOnlyList<ChatToolCall> toolCalls = toolBuilder.Build();
                foreach (ChatToolCall toolCall in toolCalls)
                {
                    foreach (var toolIhave in options.VoiceBoxTools)
                    {
                        if (toolCall.FunctionName == toolIhave.Method.Name)
                        {
                            OpenAIUtils.InvokeMethodWithJsonArguments(toolIhave.Method, toolIhave.Caller, toolCall.FunctionArguments);
                        }
                    }
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
