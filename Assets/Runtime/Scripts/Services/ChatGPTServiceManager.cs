using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Core;
using UnityEngine;

namespace TimShaw.VoiceBox.Core
{
    /// <summary>
    /// Represents the response from a chat completion request to the OpenAI API.
    /// </summary>
    [Serializable]
    public class ChatCompletionResponse
    {
        public string id;
        public string @object;
        public long created;
        public string model;
        public List<Choice> choices;
        public Usage usage;
        public string service_tier;
    }

    /// <summary>
    /// Represents a choice in a chat completion response.
    /// </summary>
    [Serializable]
    public class Choice
    {
        public int index;
        public Message message;
        public string logprobs;
        public string finish_reason;
    }

    /// <summary>
    /// Represents a message in a chat completion response.
    /// </summary>
    [Serializable]
    public class Message
    {
        public string role;
        public string content;
        public string refusal;
        public List<string> annotations;
    }

    /// <summary>
    /// Represents the usage statistics for a chat completion request.
    /// </summary>
    [Serializable]
    public class Usage
    {
        public int prompt_tokens;
        public int completion_tokens;
        public int total_tokens;
        public PromptTokensDetails prompt_tokens_details;
        public CompletionTokensDetails completion_tokens_details;
    }

    /// <summary>
    /// Represents the details of the prompt tokens used.
    /// </summary>
    [Serializable]
    public class PromptTokensDetails
    {
        public int cached_tokens;
        public int audio_tokens;
    }

    /// <summary>
    /// Represents the details of the completion tokens used.
    /// </summary>
    [Serializable]
    public class CompletionTokensDetails
    {
        public int reasoning_tokens;
        public int audio_tokens;
        public int accepted_prediction_tokens;
        public int rejected_prediction_tokens;
    }

    /// <summary>
    /// Manages the ChatGPT service, implementing the IChatService interface.
    /// </summary>
    public class ChatGPTServiceManager : IChatService
    {
        static HttpClient client;
        public static bool init_success = false;
        private string modelName;
        private string serviceEndpoint;

        /// <summary>
        /// Initializes the ChatGPT service with the provided configuration.
        /// </summary>
        /// <param name="config">The ScriptableObject configuration for the ChatGPT service.</param>
        public void Initialize(ScriptableObject config)
        {
            try
            {
                var chatServiceObjectDerived = config as GeminiServiceConfig;
                if (chatServiceObjectDerived.apiKey.Length == 0)
                {
                    throw new ArgumentException("No Chat API key!");
                }
                client = new HttpClient();
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {chatServiceObjectDerived.apiKey}");
                modelName = chatServiceObjectDerived.modelName;
                serviceEndpoint = chatServiceObjectDerived.serviceEndpoint;
                Debug.Log("CHATGPT INIT SUCCESS");
                init_success = true;
            }
            catch (Exception ex)
            {
                Debug.Log("CHATGPT INIT FAILED");
                Debug.Log(ex.Message);
            }
        }

        /// <summary>
        /// Sends a message to the ChatGPT service.
        /// </summary>
        /// <param name="messageHistory">The history of messages in the conversation.</param>
        /// <param name="onSuccess">Callback invoked when the message is successfully sent.</param>
        /// <param name="onError">Callback invoked when an error occurs.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SendMessage(List<ChatMessage> messageHistory,
            Action<ChatMessage> onSuccess,
            Action<string> onError)
        {
            try
            {
                var requestBody = new
                {
                    model = modelName,
                    messages = messageHistory,
                    max_tokens = 200
                };

                var content = new StringContent(JsonUtility.ToJson(requestBody), Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(serviceEndpoint, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonResponse = JsonUtility.FromJson<ChatCompletionResponse>(responseContent);

                Debug.Log("MESSAGE RECIEVED");
                onSuccess.Invoke(new ChatMessage(MessageRole.Model, jsonResponse.choices[0].message.content));
                return;
            }
            catch (Exception ex)
            {
                Debug.Log("CHATTING BROKE");
                onError.Invoke(ex.ToString());
                return;
            }
        }
    }
}
