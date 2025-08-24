using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;



namespace VoiceBox
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
    public static class ChatManager
    {
        static HttpClient client;
        public static bool init_success = false;
        private static string gpt_model;
        public static void Init(string api_key, string modelToUse)
        {
            try
            {
                if (api_key.Length == 0)
                {
                    throw new ArgumentException("No ChatGPT API key!");
                }
                client = new HttpClient();
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {api_key}");
                gpt_model = modelToUse;
                Debug.Log("CHATGPT INIT SUCCESS");
                init_success = true;
            }
            catch (Exception ex)
            {
                Debug.Log("CHATGPT INIT FAILED");
                Debug.Log(ex.Message);
            }
        }

        public static string SendPromptToChatGPT(string prompt)
        {
            try
            {
                var requestBody = new
                {
                    model = gpt_model,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 200
                };

                var content = new StringContent(JsonUtility.ToJson(requestBody), Encoding.UTF8, "application/json");

                var task = client.PostAsync("https://api.openai.com/v1/chat/completions", content);
                task.Wait();
                var response = task.Result;
                response.EnsureSuccessStatusCode();

                var task2 = response.Content.ReadAsStringAsync();
                task2.Wait();
                var responseContent = task2.Result;
                var jsonResponse = JsonUtility.FromJson<ChatCompletionResponse>(responseContent);

                Debug.Log("MESSAGE RECIEVED");
                return jsonResponse.choices[0].message.content;
            }
            catch (Exception ex)
            {
                Debug.Log("CHAT BROKE");
                Debug.Log(ex.ToString()); 
                return ""; 
            }
        }
    }
}
