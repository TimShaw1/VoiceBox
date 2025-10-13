

using TimShaw.VoiceBox.Components;
using TimShaw.VoiceBox.Core;
using TimShaw.VoiceBox.Generics;
using UnityEngine;

namespace TimShaw.VoiceBox.Tools
{
    static class ModdingTools
    {
        /// <summary>
        /// Creates a generic chat service config object. Useful for modders who want to generate configs at runtime.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="GenericChatServiceConfig"/> to create</typeparam>
        /// <returns></returns>
        public static T CreateGenericChatServiceConfig<T>() where T : GenericChatServiceConfig
        {
            return GenericChatServiceConfig.CreateInstance<T>();
        }

        /// <summary>
        /// Creates a generic STT service config object. Useful for modders who want to generate configs at runtime.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="GenericSTTServiceConfig"/> to create</typeparam>
        /// <returns></returns>
        public static T CreateGenericSTTServiceConfig<T>() where T : GenericSTTServiceConfig
        {
            return GenericSTTServiceConfig.CreateInstance<T>();
        }

        /// <summary>
        /// Creates a generic TTS service config object. Useful for modders who want to generate configs at runtime.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="GenericTTSServiceConfig"/> to create</typeparam>
        /// <returns></returns>
        public static T CreateGenericTTSServiceConfig<T>() where T : GenericTTSServiceConfig
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
        public static GameObject CreateAIManagerObject<ChatConfigType, STTConfigType, TTSConfigType>() 
            where ChatConfigType : GenericChatServiceConfig
            where STTConfigType : GenericSTTServiceConfig
            where TTSConfigType : GenericTTSServiceConfig
        {
            return CreateAIManagerObject(CreateGenericChatServiceConfig<ChatConfigType>(), CreateGenericSTTServiceConfig<STTConfigType>(), CreateGenericTTSServiceConfig<TTSConfigType>());
        }

        /// <summary>
        /// Instantiates an AI manager <see cref="GameObject"/> if one does not already exist using pre-existing configs. 
        /// Useful for modders who want to spawn the AI manager at runtime with customized service configs.
        /// <br></br>
        /// <br></br>
        /// <example>
        /// Example:
        /// <code>
        /// <see cref="GeminiServiceConfig"/> myChatConfig = <see cref="ModdingTools"/>.<see cref="CreateGenericChatServiceConfig{T}">CreateGenericChatServiceConfig</see>&lt;<see cref="GeminiServiceConfig"/>&gt;(); <br></br>
        /// <see cref="AzureSTTServiceConfig"/> mySTTConfig = <see cref="ModdingTools"/>.<see cref="CreateGenericSTTServiceConfig{T}">CreateGenericSTTServiceConfig</see>&lt;<see cref="AzureSTTServiceConfig"/>&gt;(); <br></br>
        /// <see cref="ElevenlabsTTSServiceConfig"/> myTTSConfig = <see cref="ModdingTools"/>.<see cref="CreateGenericTTSServiceConfig{T}">CreateGenericTTSServiceConfig</see>&lt;<see cref="ElevenlabsTTSServiceConfig"/>&gt;(); <br></br>
        /// myChatConfig.modelName = "gemini-2.5-pro"; <br></br>
        /// CreateAIManagerObject(myChatConfig, mySTTConfig, myTTSConfig);
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="genericChatServiceConfig">An existing <see cref="GenericChatServiceConfig"/> object or null</param>
        /// <param name="genericSTTServiceConfig">An existing <see cref="GenericSTTServiceConfig"/> object or null</param>
        /// <param name="genericTTSServiceConfig">An existing <see cref="GenericTTSServiceConfig"/> object or null</param>
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

                AIManager.Instance.LoadAPIKeys(apiKeysJsonPath.Length > 0 ? apiKeysJsonPath : Application.dataPath + "/keys.json");

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
        /// Initializes a <see cref="TTSManager"/> component with a <see cref="GenericTTSServiceConfig"/>. Also loads TTS API key.
        /// </summary>
        /// <param name="manager">The <see cref="TTSManager"/> to initialize</param>
        /// <param name="config">A <see cref="GenericTTSServiceConfig"/> that specifies how to set up the TTS manager. See <see cref="CreateGenericChatServiceConfig{T}"/></param>
        /// <param name="apiKeysJsonPath">Path where the api keys json file is located</param>
        /// <returns></returns>
        public static void InitChatManagerObject(ChatManager manager, GenericChatServiceConfig config, string apiKeysJsonPath = "")
        {
            manager.chatServiceConfig = config;

            manager.LoadAPIKey(apiKeysJsonPath.Length > 0 ? apiKeysJsonPath : Application.dataPath + "/keys.json");
            manager.ChatService = ServiceFactory.CreateChatService(manager.chatServiceConfig);
        }

        /// <summary>
        /// Initializes a <see cref="TTSManager"/> component with a <see cref="GenericTTSServiceConfig"/>. Also loads TTS API key.
        /// </summary>
        /// <param name="manager">The <see cref="TTSManager"/> to initialize</param>
        /// <param name="config">A <see cref="GenericTTSServiceConfig"/> that specifies how to set up the TTS manager. See <see cref="CreateGenericTTSServiceConfig{T}"/></param>
        /// <param name="apiKeysJsonPath">Path where the api keys json file is located</param>
        /// <returns></returns>
        public static void InitTTSManagerObject(TTSManager manager, GenericTTSServiceConfig config, string apiKeysJsonPath = "")
        {
            manager.textToSpeechConfig = config;

            manager.LoadAPIKey(apiKeysJsonPath.Length > 0 ? apiKeysJsonPath : Application.dataPath + "/keys.json");
            manager.TextToSpeechService = ServiceFactory.CreateTtsService(manager.textToSpeechConfig);
        }

    }
}