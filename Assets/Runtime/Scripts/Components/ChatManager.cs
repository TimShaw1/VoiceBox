

using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Generics;
using UnityEngine;

namespace TimShaw.VoiceBox.Components
{
    public class ChatManager : MonoBehaviour
    {
        private readonly CancellationTokenSource internalCancellationTokenSource = new();

        [Tooltip("Path to the api keys json file. Defaults to Assets/keys.json")]
        [SerializeField] public string apiKeysJsonPath = "";

        [Tooltip("Configuration asset for the chat service (e.g., GeminiConfig, ChatGPTConfig).")]
        [SerializeField] public GenericChatServiceConfig chatServiceConfig;

        private IChatService _chatService;
        public IChatService ChatService { get => _chatService; set => _chatService = value; }

        public void Awake()
        {
            LoadAPIKey(apiKeysJsonPath.Length > 0 ? apiKeysJsonPath : Application.dataPath + "/keys.json");
            ChatService = ServiceFactory.CreateChatService(chatServiceConfig);

            var chats = new List<ChatMessage>();
            var chat = new ChatMessage(
                ChatRole.User,
                "Write a 1 paragraph essay about huskies."
                //"Display a vector2 (1.00, 3.00) to the console. Use SampleTool3. Then, display 1 sentence about huskies to the console using SampleTool."
                );
            chats.Add(chat);

            string combinedResponse = "";

            //AIManager.Instance.SendChatMessage(chats, options, chatMessage => Debug.Log(chatMessage.Content[0].Text), error => Debug.LogError(error));

            StreamChatMessage(
                chats,
                null,
                chunk => { Debug.Log(chunk.Role + ": " + chunk.Text); combinedResponse += chunk; },
                () => { Debug.Log("Combined response: " + combinedResponse); },
                error => Debug.LogError(error)
            );
        }

        /// <summary>
        /// Cancels all running tasks and unloads API keys when the application quits.
        /// </summary>
        private void OnDestroy()
        {
            internalCancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Loads API keys from a JSON file and applies them to the service configurations.
        /// </summary>
        /// <param name="keysFile">The path to the JSON file containing the API keys.</param>
        public void LoadAPIKey(string keysFile)
        {
            string jsonContent = File.ReadAllText(keysFile);
            var apiKeys = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

            if (chatServiceConfig && chatServiceConfig.apiKeyJSONString.Length > 0)
                chatServiceConfig.apiKey = apiKeys[chatServiceConfig.apiKeyJSONString];
            else
                Debug.LogWarning("Chat service config does not define an apiKeyJSONString");
        }

        /// <summary>
        /// Sends a conversation history to the configured chat service.
        /// </summary>
        /// <param name="messageHistory">A list of chat messages representing the conversation history.</param>
        /// <param name="options">Request-level settings.</param>
        /// <param name="onSuccess">Callback invoked when the message is successfully sent, returning the response.</param>
        /// <param name="onError">Callback invoked when an error occurs.</param>
        public void SendChatMessage(
            List<ChatMessage> messageHistory,
            ChatUtils.VoiceBoxChatCompletionOptions options,
            Action<ChatMessage> onSuccess,
            Action<string> onError,
            CancellationToken token = default)
        {
            if (ChatService == null)
            {
                onError?.Invoke("Chat service is not initialized. Check AIManager configuration.");
                return;
            }

            token = CancellationTokenSource.CreateLinkedTokenSource(token, internalCancellationTokenSource.Token).Token;

            Task.Run(() => ChatService.SendMessage(messageHistory, options, onSuccess, onError, token));
        }

        /// <summary>
        /// Sends a message to the Chat service and streams the response.
        /// </summary>
        /// <param name="messageHistory">The history of messages in the conversation.</param>
        /// <param name="options">Request-level settings.</param>
        /// <param name="onChunkReceived">Callback invoked when a chunk of the response is received.</param>
        /// <param name="onComplete">Callback invoked when the response is complete.</param>
        /// <param name="onError">Callback invoked when an error occurs.</param>
        public void StreamChatMessage(
            List<ChatMessage> messageHistory,
            ChatUtils.VoiceBoxChatCompletionOptions options,
            Action<ChatResponseUpdate> onChunkReceived,
            Action onComplete,
            Action<string> onError,
            CancellationToken token = default
        )
        {
            if (ChatService == null)
            {
                onError?.Invoke("Chat service is not initialized. Check AIManager configuration.");
                return;
            }

            token = CancellationTokenSource.CreateLinkedTokenSource(token, internalCancellationTokenSource.Token).Token;

            Task.Run(() => ChatService.SendMessageStream(messageHistory, options, onChunkReceived, onComplete, onError, token));
        }
    }
}