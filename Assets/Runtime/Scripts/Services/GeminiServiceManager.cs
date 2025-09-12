using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Core;
using UnityEngine;
using Newtonsoft.Json;
using static UnityEditor.Timeline.TimelinePlaybackControls;

namespace TimShaw.VoiceBox.LLM
{
   
    public class GeminiServiceManager : IChatService
    {
        private HttpClient _client;
        private string _endpointUrl;
        private GeminiServiceConfig _config; // Store the entire config object

        #region API Request/Response Structures

        [Serializable]
        public class GeminiRequest
        {
            public List<GeminiContent> contents;

            // Include all configuration parameters from the GeminiServiceConfig
            //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public List<Tool> tools;

            //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public ToolConfig toolConfig;

            //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public List<SafetySetting> safetySettings;

            //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public GeminiContent systemInstruction;

            //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public GenerationConfig generationConfig;
        }

        [Serializable]
        public class GeminiResponse
        {
            public List<GeminiCandidate> candidates;
        }

        [Serializable]
        public class GeminiCandidate
        {
            public GeminiContent content;
        }

        #endregion

        public void Initialize(ScriptableObject config)
        {
            if (config is GeminiServiceConfig geminiConfig)
            {
                _config = geminiConfig; // Store the config
                _endpointUrl = $"{_config.serviceEndpoint}{_config.modelName}:generateContent?key={_config.apiKey}";
                _client = new HttpClient();
            }
            else
            {
                Debug.LogError("Invalid configuration provided to GeminiChatService. Expected GeminiServiceConfig.");
            }
        }

        public async Task SendMessage(
            List<ChatMessage> messageHistory,
            Action<ChatMessage> onSuccess,
            Action<string> onError)
        {
            if (_client == null || _config == null)
            {
                onError?.Invoke("GeminiChatService is not initialized.");
                return;
            }

            try
            {
                // 1. Create the request body, including the full configuration.
                var requestBody = new GeminiRequest
                {
                    contents = MapToGeminiContents(messageHistory),

                    // Populate the request with data from the ScriptableObject
                    generationConfig = _config.useGenerationConfig ? _config.generationConfig : null,
                    safetySettings = _config.useSafetySettings ? _config.safetySettings : null,
                    tools = _config.useTools ? _config.tools : null,
                    toolConfig = _config.useTools ? _config.toolConfig : null,
                    systemInstruction = _config.systemInstruction
                };

                // Use a settings object to ignore null values if using a more advanced JSON serializer.
                // JsonUtility includes fields with default/null values, which is usually fine.
                Debug.Log(MapToGeminiContents(messageHistory)[0]);
                string jsonBody = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings
                {
                    // This setting is useful to avoid sending empty configs to the API
                    NullValueHandling = NullValueHandling.Ignore
                });
                Debug.Log(jsonBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                // 2. Send the request.
                HttpResponseMessage response = await _client.PostAsync(_endpointUrl, content);

                // 3. Process the response.
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Error from Gemini API: {response.StatusCode}\n{errorContent}");
                }

                string responseJson = await response.Content.ReadAsStringAsync();
                GeminiResponse geminiResponse = JsonConvert.DeserializeObject<GeminiResponse>(responseJson);

                if (geminiResponse?.candidates == null || geminiResponse.candidates.Count == 0)
                {
                    throw new Exception("Invalid response from Gemini: No candidates found.");
                }

                string modelResponseText = geminiResponse.candidates[0].content.parts[0].text;
                onSuccess?.Invoke(new ChatMessage(MessageRole.Model, modelResponseText));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GeminiChatService] Error: {ex.Message}");
                onError?.Invoke(ex.Message);
            }
        }

        private List<GeminiContent> MapToGeminiContents(List<ChatMessage> messageHistory)
        {
            var geminiContents = new List<GeminiContent>();
            foreach (var message in messageHistory)
            {
                string role = (message.Role == MessageRole.Model) ? "model" : "user";
                geminiContents.Add(new GeminiContent
                {
                    role = role,
                    parts = new List<GeminiPart> { new GeminiPart { text = message.Content } }
                });
            }
            return geminiContents;
        }

        public void SendMessageStream(List<ChatMessage> messageHistory, Action<string> onChunkReceived, Action onComplete, Action<string> onError)
        {
            onError?.Invoke("Streaming is not yet implemented for this service.");
        }
    }
}