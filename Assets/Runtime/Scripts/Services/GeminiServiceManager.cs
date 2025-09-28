using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Data;
using TimShaw.VoiceBox.Generics;
using UnityEditor.PackageManager;
using UnityEngine;

namespace TimShaw.VoiceBox.Core
{
    /// <summary>
    /// Manages the Gemini service, implementing the IChatService interface.
    /// </summary>
    public class GeminiServiceManager : IChatService
    {
        private ChatClient _client;
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
                _client = new(
                    model: _config.modelName,
                    credential: new ApiKeyCredential(_config.apiKey),
                    options: new OpenAIClientOptions()
                    {
                        Endpoint = new Uri(_config.serviceEndpoint)
                    }
                );
            }
            else
            {
                Debug.LogError("Invalid configuration provided to GeminiChatService. Expected GeminiServiceConfig.");
            }
        }

        /// <summary>
        /// Sends a message to the Gemini service.
        /// </summary>
        /// <param name="messageHistory">The history of messages in the conversation.</param>
        /// <param name="onSuccess">Callback invoked when the message is successfully sent.</param>
        /// <param name="onError">Callback invoked when an error occurs.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SendMessage(
            List<ChatMessage> messageHistory,
            Action<ChatMessage> onSuccess,
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
                var response = await _client.CompleteChatAsync(messageHistory, cancellationToken: token);
                onSuccess.Invoke(new AssistantChatMessage(response.Value.Content));
            }
            catch (Exception ex)
            {
                onError.Invoke(ex.Message);
            }

        }

        /// <summary>
        /// Sends a message to the Gemini service and streams the response.
        /// </summary>
        /// <param name="messageHistory">The history of messages in the conversation.</param>
        /// <param name="onChunkReceived">Callback invoked when a chunk of the response is received.</param>
        /// <param name="onComplete">Callback invoked when the response is complete.</param>
        /// <param name="onError">Callback invoked when an error occurs.</param>
        public async Task SendMessageStream(
            List<ChatMessage> messageHistory,
            Action<string> onChunkReceived,
            Action onComplete,
            Action<string> onError,
            CancellationToken token
        )
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
    }
}