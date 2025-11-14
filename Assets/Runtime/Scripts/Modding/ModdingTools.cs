

using TimShaw.VoiceBox.Components;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Generics;
using UnityEngine;

namespace TimShaw.VoiceBox.Modding
{
    /// <summary>
    /// Provides various utilities for modders who want to integrate AI tools via VoiceBoxModLib
    /// </summary>
    public static class ModdingTools
    {
        /// <summary>
        /// Creates a chat service config object. Useful for modders who want to generate configs at runtime.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="GenericChatServiceConfig"/> to create</typeparam>
        /// <returns></returns>
        public static T CreateChatServiceConfig<T>() where T : GenericChatServiceConfig
        {
            return GenericChatServiceConfig.CreateInstance<T>();
        }

        /// <summary>
        /// Creates a STT service config object. Useful for modders who want to generate configs at runtime.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="GenericSTTServiceConfig"/> to create</typeparam>
        /// <returns></returns>
        public static T CreateSTTServiceConfig<T>() where T : GenericSTTServiceConfig
        {
            return GenericSTTServiceConfig.CreateInstance<T>();
        }

        /// <summary>
        /// Creates a TTS service config object. Useful for modders who want to generate configs at runtime.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="GenericTTSServiceConfig"/> to create</typeparam>
        /// <returns></returns>
        public static T CreateTTSServiceConfig<T>() where T : GenericTTSServiceConfig
        {
            return GenericTTSServiceConfig.CreateInstance<T>();
        }

        /// <summary>
        /// Instantiates an AI manager <see cref="GameObject"/> if one does not already exist. 
        /// Useful for modders who want to spawn the AI manager at runtime.
        /// <br></br>
        /// <br></br>
        /// <example>Example Usage: <c>CreateAIManagerObject&lt;GeminiServiceConfig, AzureSTTServiceConfig, ElevenlabsTTSServiceConfig&gt;()</c></example>
        /// <br></br>
        /// <br></br>
        /// <example>Example Usage where only 1 service is needed: <c>CreateAIManagerObject&lt;GeminiServiceConfig, GenericSTTServiceConfig, GenericTTSServiceConfig&gt;()</c></example>
        /// </summary>
        /// <typeparam name="ChatConfigType">The type of <see cref="GenericChatServiceConfig"/> to attach to the AI Manager</typeparam>
        /// <typeparam name="STTConfigType">The type of <see cref="GenericSTTServiceConfig"/> to attach to the AI Manager</typeparam>
        /// <typeparam name="TTSConfigType">The type of <see cref="GenericTTSServiceConfig"/> to attach to the AI Manager</typeparam>
        /// <returns>The instantiated <see cref="AIManager"/> object</returns>
        public static GameObject CreateAIManagerObject<ChatConfigType, STTConfigType, TTSConfigType>(string apiKeysJsonPath = "")
            where ChatConfigType : GenericChatServiceConfig
            where STTConfigType : GenericSTTServiceConfig
            where TTSConfigType : GenericTTSServiceConfig
        {
            return CreateAIManagerObject(
                CreateChatServiceConfig<ChatConfigType>(),
                CreateSTTServiceConfig<STTConfigType>(),
                CreateTTSServiceConfig<TTSConfigType>(),
                apiKeysJsonPath
            );
        }

        /// <summary>
        /// Instantiates an AI manager <see cref="GameObject"/> if one does not already exist using pre-existing configs. 
        /// Useful for modders who want to spawn the AI manager at runtime with customized service configs.
        /// <br></br>
        /// <br></br>
        /// <example>
        /// Example:
        /// <code>
        /// GeminiServiceConfig myChatConfig = ModdingTools.CreateGenericChatServiceConfig&lt;GeminiServiceConfig&gt;(); <br></br>
        /// AzureSTTServiceConfig mySTTConfig = ModdingTools.CreateGenericSTTServiceConfig&lt;AzureSTTServiceConfig&gt;(); <br></br>
        /// ElevenlabsTTSServiceConfig myTTSConfig = ModdingTools.CreateGenericTTSServiceConfig&lt;ElevenlabsTTSServiceConfig&gt;(); <br></br>
        /// myChatConfig.modelName = "gemini-2.5-pro"; <br></br>
        /// CreateAIManagerObject(myChatConfig, mySTTConfig, myTTSConfig);
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="genericChatServiceConfig">An existing <see cref="GenericChatServiceConfig"/> object or null</param>
        /// <param name="genericSTTServiceConfig">An existing <see cref="GenericSTTServiceConfig"/> object or null</param>
        /// <param name="genericTTSServiceConfig">An existing <see cref="GenericTTSServiceConfig"/> object or null</param>
        /// <param name="apiKeysJsonPath">Path to the api keys json file to load (Optional)</param>
        /// <returns>The instantiated <see cref="AIManager"/> object</returns>
        public static GameObject CreateAIManagerObject(
            GenericChatServiceConfig genericChatServiceConfig, 
            GenericSTTServiceConfig genericSTTServiceConfig, 
            GenericTTSServiceConfig genericTTSServiceConfig,
            string apiKeysJsonPath = ""
        )
        {
            if (AIManager.Instance == null)
            {
                GameObject aiManager = new GameObject("_AIManager");
                AIManager aiManagerComponent = aiManager.AddComponent<AIManager>();
                aiManagerComponent.chatServiceConfig = genericChatServiceConfig;
                aiManagerComponent.speechToTextConfig = genericSTTServiceConfig;
                aiManagerComponent.textToSpeechConfig = genericTTSServiceConfig;

                if (apiKeysJsonPath.Length > 0)
                    AIManager.Instance.LoadAPIKeys(apiKeysJsonPath);
                else
                    AIManager.Instance.LoadAPIKeys();

                aiManagerComponent.ChatService = ServiceFactory.CreateChatService(aiManagerComponent.chatServiceConfig);
                aiManagerComponent.SpeechToTextService = ServiceFactory.CreateSttService(aiManagerComponent.speechToTextConfig);
                aiManagerComponent.TextToSpeechService = ServiceFactory.CreateTtsService(aiManagerComponent.textToSpeechConfig);

                //GameObject.Instantiate(aiManager);

                return aiManager;
            }
            else
            {
                Debug.LogWarning("AI Manager already exists, no new object will be created.");
                return AIManager.Instance.gameObject;
            }
        }

        /// <summary>
        /// Initializes a <see cref="ChatManager"/> component with a <see cref="GenericChatServiceConfig"/> and creates the associated chat service. Also loads Chat API key.
        /// </summary>
        /// <param name="manager">The <see cref="ChatManager"/> to initialize</param>
        /// <param name="config">A <see cref="GenericChatServiceConfig"/> that specifies how to set up the Chat manager. See <see cref="CreateChatServiceConfig{T}"/></param>
        /// <param name="apiKeysJsonPath">Path where the api keys json file is located</param>
        /// <returns></returns>
        public static void InitChatManagerObject(ChatManager manager, GenericChatServiceConfig config, string apiKeysJsonPath = "", string chatKey = null)
        {
            manager.chatServiceConfig = config;

            if (apiKeysJsonPath.Length > 0)
                manager.LoadAPIKey(apiKeysJsonPath);
            else if (chatKey != null)
                manager.LoadAPIKey(chatKey: chatKey);
            else
                manager.LoadAPIKey();
            manager.ChatService = ServiceFactory.CreateChatService(manager.chatServiceConfig);
        }

        /// <summary>
        /// Initializes a <see cref="TTSManager"/> component with a <see cref="GenericTTSServiceConfig"/> and creates the associated TTS service. Also loads TTS API key.
        /// </summary>
        /// <param name="manager">The <see cref="TTSManager"/> to initialize</param>
        /// <param name="config">A <see cref="GenericTTSServiceConfig"/> that specifies how to set up the TTS manager. See <see cref="CreateTTSServiceConfig{T}"/></param>
        /// <param name="apiKeysJsonPath">Path where the api keys json file is located</param>
        /// <returns></returns>
        public static void InitTTSManagerObject(TTSManager manager, GenericTTSServiceConfig config, string apiKeysJsonPath = "", string ttsKey = null)
        {
            manager.textToSpeechConfig = config;

            if (apiKeysJsonPath.Length > 0)
                manager.LoadAPIKey(apiKeysJsonPath);
            else if (ttsKey != null)
                manager.LoadAPIKey(ttsKey: ttsKey);
            else
                manager.LoadAPIKey();
            manager.TextToSpeechService = ServiceFactory.CreateTtsService(manager.textToSpeechConfig);
        }

    }
}