using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Generics;
using TimShaw.VoiceBox.Data;
using UnityEngine;

namespace TimShaw.VoiceBox.Core
{
    /// <summary>
    /// Manages the Gemini service, implementing the IChatService interface.
    /// </summary>
    public class GeminiServiceManager : IChatService
    {
        private HttpClient _client;
        private string _endpointUrl;
        private GeminiServiceConfig _config;

        #region API Request/Response Structures

        /// <summary>
        /// Represents the request body for the Gemini API.
        /// </summary>
        [Serializable]
        public class GeminiRequest
        {
            public List<GeminiContent> contents;
            public List<Tool> tools;
            public ToolConfig toolConfig;
            public List<SafetySetting> safetySettings;
            public GeminiContent systemInstruction;
            public GenerationConfig generationConfig;
        }

        /// <summary>
        /// Represents the response from the Gemini API.
        /// </summary>
        [Serializable]
        public class GeminiResponse
        {
            public List<GeminiCandidate> candidates;
        }

        /// <summary>
        /// Represents a candidate in the Gemini API response.
        /// </summary>
        [Serializable]
        public class GeminiCandidate
        {
            public GeminiContent content;
        }

        #endregion

        /// <summary>
        /// Initializes the Gemini service with the provided configuration.
        /// </summary>
        /// <param name="config">The ScriptableObject configuration for the Gemini service.</param>
        public void Initialize(GenericChatServiceConfig config)
        {
            if (config is GeminiServiceConfig geminiConfig)
            {
                _config = geminiConfig;
                // Note: The base endpoint is set here. The specific method will be appended later.
                _endpointUrl = $"{_config.serviceEndpoint}{_config.modelName}";
                _client = new HttpClient();
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
            Action<string> onError)
        {
            if (_client == null || _config == null)
            {
                onError?.Invoke("GeminiChatService is not initialized.");
                return;
            }

            try
            {
                var generateContentEndpoint = $"{_endpointUrl}:generateContent?key={_config.apiKey}";
                var requestBody = new GeminiRequest
                {
                    contents = MapToGeminiContents(messageHistory),
                    generationConfig = _config.useGenerationConfig ? _config.generationConfig : null,
                    safetySettings = _config.useSafetySettings ? _config.safetySettings : null,
                    tools = _config.useTools ? _config.tools : null,
                    toolConfig = _config.useTools ? _config.toolConfig : null,
                    systemInstruction = _config.systemInstruction
                };

                string jsonBody = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _client.PostAsync(generateContentEndpoint, content);

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

        /// <summary>
        /// Maps a list of ChatMessage objects to a list of GeminiContent objects.
        /// </summary>
        /// <param name="messageHistory">The list of ChatMessage objects to map.</param>
        /// <returns>A list of GeminiContent objects.</returns>
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
            if (_client == null || _config == null)
            {
                onError?.Invoke("GeminiChatService is not initialized.");
                return;
            }

            try
            {
                var streamEndpointUrl = $"{_endpointUrl}:streamGenerateContent?key={_config.apiKey}";

                var requestBody = new GeminiRequest
                {
                    contents = MapToGeminiContents(messageHistory),
                    generationConfig = _config.useGenerationConfig ? _config.generationConfig : null,
                    safetySettings = _config.useSafetySettings ? _config.safetySettings : null,
                    tools = _config.useTools ? _config.tools : null,
                    toolConfig = _config.useTools ? _config.toolConfig : null,
                    systemInstruction = _config.systemInstruction
                };

                string jsonBody = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, streamEndpointUrl) { Content = content };

                using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorContent = await response.Content.ReadAsStringAsync();
                        throw new HttpRequestException($"Error from Gemini API: {response.StatusCode}\n{errorContent}");
                    }

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var reader = new StreamReader(stream))
                    {
                        string line;
                        while ((line = await reader.ReadLineAsync()) != null && !token.IsCancellationRequested)
                        {
                            string trimmedLine = line.Trim();

                            if (trimmedLine.StartsWith("\"text\":"))
                            {
                                // Extract the value part, which starts after the first colon.
                                int firstColonIndex = trimmedLine.IndexOf(':');
                                if (firstColonIndex == -1) continue;

                                string valuePart = trimmedLine.Substring(firstColonIndex + 1).Trim();

                                // Clean up the extracted value by removing surrounding quotes and trailing commas.
                                if (valuePart.StartsWith("\""))
                                {
                                    valuePart = valuePart.Substring(1);
                                }
                                if (valuePart.EndsWith("\","))
                                {
                                    valuePart = valuePart.Substring(0, valuePart.Length - 2);
                                }
                                else if (valuePart.EndsWith("\""))
                                {
                                    valuePart = valuePart.Substring(0, valuePart.Length - 1);
                                }

                                if (!string.IsNullOrEmpty(valuePart))
                                {
                                    // Unescape characters like \n, \", etc., to get the clean text.
                                    string unescapedChunk = Regex.Unescape(valuePart);
                                    onChunkReceived?.Invoke(unescapedChunk);
                                }
                            }
                        }
                    }
                }

                if (!token.IsCancellationRequested)
                    onComplete?.Invoke();
                else
                    onError?.Invoke("Cancellation requested.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GeminiChatService] Error during streaming: {ex.Message}");
                onError?.Invoke(ex.Message);
            }
        }
    }
}