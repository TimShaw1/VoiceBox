using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Generics;
using UnityEngine;
using System.Collections.Generic;

namespace TimShaw.VoiceBox.Data
{
    /// <summary>
    /// Configuration settings for the ChatGPT chat service.
    /// </summary>
    [CreateAssetMenu(fileName = "ChatGPTServiceConfig", menuName = "VoiceBox/Chat/ChatGPTService Configuration")]
    class ChatGPTServiceConfig : GenericChatServiceConfig
    {
        public ChatGPTServiceConfig()
        {
            serviceManagerType = typeof(ChatGPTServiceManager);
            apiKeyJSONString = "CHATGPT_API_KEY";
        }

        /// <summary>
        /// The service endpoint for the Gemini API.
        /// </summary>
        public string serviceEndpoint = "https://api.openai.com/v1/responses/";

        [Tooltip("Whether to run the model response in the background.")]
        public bool background = false;

        [Tooltip("The conversation ID that this response belongs to.")]
        public string conversation;

        [Tooltip("Specify additional output data to include in the model response.")]
        public string[] include;

        [Tooltip("Text, image, or file inputs to the model.")]
        public object input;

        [Tooltip("A system (or developer) message inserted into the model's context.")]
        public string instructions;

        [Tooltip("An upper bound for the number of tokens that can be generated for a response.")]
        public int? max_output_tokens = 200;

        [Tooltip("The maximum number of total calls to built-in tools that can be processed in a response.")]
        public int? max_tool_calls;

        [Tooltip("Set of 16 key-value pairs that can be attached to an object.")]
        public Dictionary<string, string> metadata;

        [Tooltip("Model ID used to generate the response.")]
        public string model;

        [Tooltip("Whether to allow the model to run tool calls in parallel.")]
        public bool parallel_tool_calls = true;

        [Tooltip("The unique ID of the previous response to the model.")]
        public string previous_response_id;

        [Tooltip("Reference to a prompt template and its variables.")]
        public object prompt;

        [Tooltip("Used by OpenAI to cache responses for similar requests to optimize your cache hit rates.")]
        public string prompt_cache_key;

        [Tooltip("Configuration options for reasoning models.")]
        public object reasoning;

        [Tooltip("A stable identifier used to help detect users of your application that may be violating OpenAI's usage policies.")]
        public string safety_identifier;

        [Tooltip("Specifies the processing type used for serving the request.")]
        public string service_tier = "auto";

        [Tooltip("Whether to store the generated model response for later retrieval via API.")]
        public bool store = true;

        [Tooltip("If set to true, the model response data will be streamed to the client as it is generated.")]
        public bool stream = false;

        [Tooltip("Options for streaming responses.")]
        public object stream_options;

        [Tooltip("What sampling temperature to use, between 0 and 2.")]
        public float temperature = 1.0f;

        [Tooltip("Configuration options for a text response from the model.")]
        public object text;

        [Tooltip("How the model should select which tool (or tools) to use when generating a response.")]
        public object tool_choice;

        [Tooltip("An array of tools the model may call while generating a response.")]
        public object[] tools;

        [Tooltip("An integer between 0 and 20 specifying the number of most likely tokens to return at each token position.")]
        public int? top_logprobs;

        [Tooltip("An alternative to sampling with temperature, called nucleus sampling.")]
        public float top_p = 1.0f;

        [Tooltip("The truncation strategy to use for the model response.")]
        public string truncation = "disabled";
    }
}