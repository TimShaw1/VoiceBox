using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Core;
using UnityEngine;



namespace TimShaw.VoiceBox.LLM
{
    [Serializable]
    public class ChatCompletionResponse
    {
        public string id;
        public string @object;   // "object" is a reserved word in C#, so use @object
        public long created;
        public string model;
        public List<Choice> choices;
        public Usage usage;
        public string service_tier;
    }

    [Serializable]
    public class Choice
    {
        public int index;
        public Message message;
        public string logprobs;   // null in your example, use string or make it object-like if needed
        public string finish_reason;
    }

    [Serializable]
    public class Message
    {
        public string role;
        public string content;
        public string refusal;      // null in your example
        public List<string> annotations;  // empty array in your example
    }

    [Serializable]
    public class Usage
    {
        public int prompt_tokens;
        public int completion_tokens;
        public int total_tokens;
        public PromptTokensDetails prompt_tokens_details;
        public CompletionTokensDetails completion_tokens_details;
    }

    [Serializable]
    public class PromptTokensDetails
    {
        public int cached_tokens;
        public int audio_tokens;
    }

    [Serializable]
    public class CompletionTokensDetails
    {
        public int reasoning_tokens;
        public int audio_tokens;
        public int accepted_prediction_tokens;
        public int rejected_prediction_tokens;
    }
    public class ChatGPTServiceManager : IChatService
    {
        static HttpClient client;
        public static bool init_success = false;
        private string modelName;
        private string serviceEndpoint;
        public void Initialize(ScriptableObject config)
        {
            try
            {
                var chatServiceObjectDerived = config as ChatServiceConfig;
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

        public async void SendMessage(List<ChatMessage> messageHistory,
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
