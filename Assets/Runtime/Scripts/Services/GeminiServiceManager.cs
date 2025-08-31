using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Core;
using UnityEngine;

public class GeminiServiceManager : IChatService
{
    private HttpClient _client;
    private string _apiKey;
    private string _modelName; // Or any other supported model
    private string _endpointUrl;

    [Serializable]
    public class GeminiRequest
    {
        public List<GeminiContent> contents;
        // Other parameters like generationConfig, safetySettings etc.
    }

    [Serializable]
    public class GeminiResponse
    {
        public List<GeminiCandidate> candidates;
        // This can also contain promptFeedback
    }

    [Serializable]
    public class GeminiCandidate
    {
        public GeminiContent content;
        public string finishReason;
        public int index;
        // public List<SafetyRating> safetyRatings;
    }

    [Serializable]
    public class GeminiContent
    {
        public List<GeminiPart> parts;
        public string role;
    }

    [Serializable]
    public class GeminiPart
    {
        public string text;
    }

    public void Initialize(ScriptableObject config)
    {
        // Cast generic ScriptableObject to GeminiConfig
        if (config is ChatServiceConfig geminiConfig)
        {
            _apiKey = geminiConfig.apiKey;
            _modelName = geminiConfig.modelName;
            _endpointUrl = $"{geminiConfig.serviceEndpoint}{_modelName}:generateContent?key={_apiKey}";
            _client = new HttpClient();
        }
        else
        {
            Debug.LogError("Invalid configuration provided to GeminiChatService. Expected GeminiConfig.");
        }
    }

    /// <summary>
    /// Sends a message to the Gemini API asynchronously.
    /// </summary>
    public async void SendMessage(
        List<ChatMessage> messageHistory,
        Action<ChatMessage> onSuccess,
        Action<string> onError)
    {
        if (_client == null)
        {
            onError?.Invoke("GeminiChatService is not initialized.");
            return;
        }

        try
        {
            // 1. Create the request body by mapping our generic ChatMessage to the Gemini format.
            var requestBody = new GeminiRequest
            {
                contents = MapToGeminiContents(messageHistory)
            };
            string jsonBody = JsonUtility.ToJson(requestBody);

            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            // 2. Send the request asynchronously and wait for the response without blocking the main thread.
            HttpResponseMessage response = await _client.PostAsync(_endpointUrl, content);

            // 3. Check for errors and read the response content.
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Error from Gemini API: {response.StatusCode}\n{errorContent}");
            }

            string responseJson = await response.Content.ReadAsStringAsync();

            // 4. Deserialize the response and extract the message.
            GeminiResponse geminiResponse = JsonUtility.FromJson<GeminiResponse>(responseJson);

            if (geminiResponse?.candidates == null || geminiResponse.candidates.Count == 0)
            {
                throw new Exception("Invalid response from Gemini: No candidates found.");
            }

            string modelResponseText = geminiResponse.candidates[0].content.parts[0].text;

            // 5. Invoke the success callback with the result.
            onSuccess?.Invoke(new ChatMessage(MessageRole.Model, modelResponseText));
        }
        catch (Exception ex)
        {
            // 6. If any step fails, invoke the error callback.
            Debug.LogError($"[GeminiChatService] Error: {ex.Message}");
            onError?.Invoke(ex.Message);
        }
    }

    /// <summary>
    /// Helper function to convert your universal ChatMessage list to the Gemini-specific format.
    /// </summary>
    private List<GeminiContent> MapToGeminiContents(List<ChatMessage> messageHistory)
    {
        var geminiContents = new List<GeminiContent>();
        foreach (var message in messageHistory)
        {
            // Gemini uses "user" and "model" for roles. "system" is handled differently,
            // often as the first message in the conversation.
            string role = (message.Role == MessageRole.Model) ? "model" : "user";

            geminiContents.Add(new GeminiContent
            {
                role = role,
                parts = new List<GeminiPart> { new GeminiPart { text = message.Content } }
            });
        }
        return geminiContents;
    }

    // The streaming implementation would go here
    public void SendMessageStream(List<ChatMessage> messageHistory, Action<string> onChunkReceived, Action onComplete, Action<string> onError)
    {
        // Streaming requires a more complex setup to read the response stream chunk-by-chunk.
        // This is an advanced topic beyond a simple HttpClient.PostAsync call.
        onError?.Invoke("Streaming is not yet implemented for this service.");
    }
}