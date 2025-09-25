

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
        /// <example>Example Usage: <c>CreateAIManagerObject&lt;GeminiServiceConfig, AzureSTTServiceConfig, ElevenlabsSTTServiceConfig&gt;()</c></example>
        /// <br></br>
        /// <br></br>
        /// <example>Example Usage where only 1 service is needed: <c>CreateAIManagerObject&lt;GeminiServiceConfig, GenericSTTServiceConfig, GenericTTSServiceConfig&gt;()</c></example>
        /// </summary>
        /// <typeparam name="ChatConfigType">The type of <see cref="GenericChatServiceConfig"/> to attach to the AI Manager</typeparam>
        /// <typeparam name="STTConfigType">The type of <see cref="GenericSTTServiceConfig"/> to attach to the AI Manager</typeparam>
        /// <typeparam name="TTSConfigType">The type of <see cref="GenericTTSServiceConfig"/> to attach to the AI Manager</typeparam>
        /// <returns></returns>
        public static GameObject CreateAIManagerObject<ChatConfigType, STTConfigType, TTSConfigType>() 
            where ChatConfigType : GenericChatServiceConfig
            where STTConfigType : GenericSTTServiceConfig
            where TTSConfigType : GenericTTSServiceConfig
        {
            if (AIManager.Instance == null) {
                GameObject aiManager = new GameObject("_AIManager");
                AIManager aiManagerComponent = aiManager.AddComponent<AIManager>();
                aiManagerComponent.chatServiceConfig = CreateGenericChatServiceConfig<ChatConfigType>();
                aiManagerComponent.speechToTextConfig = CreateGenericSTTServiceConfig<STTConfigType>();
                aiManagerComponent.textToSpeechConfig = CreateGenericTTSServiceConfig<TTSConfigType>();

                //GameObject.Instantiate(aiManager);

                aiManagerComponent.chatServiceConfig._Init();
                aiManagerComponent.speechToTextConfig._Init();
                aiManagerComponent.textToSpeechConfig._Init();

                AIManager.Instance.LoadAPIKeys(Application.dataPath + "/keys.json");

                aiManagerComponent._chatService = ServiceFactory.CreateChatService(aiManagerComponent.chatServiceConfig);
                aiManagerComponent._sttService = ServiceFactory.CreateSttService(aiManagerComponent.speechToTextConfig);
                aiManagerComponent._ttsService = ServiceFactory.CreateTtsService(aiManagerComponent.textToSpeechConfig);

                if (aiManagerComponent.speechToTextConfig is AzureSTTServiceConfig)
                    AIManager.Instance.InitSpeechRecognizer();

                return aiManager;
            }
            else
            {
                return null;
            }
        }

        public static GameObject CreateAIManagerObject(
            GenericChatServiceConfig genericChatServiceConfig, 
            GenericSTTServiceConfig genericSTTServiceConfig, 
            GenericTTSServiceConfig genericTTSServiceConfig
        )
        {
            if (AIManager.Instance == null)
            {
                GameObject aiManager = new GameObject("_AIManager");
                AIManager aiManagerComponent = aiManager.AddComponent<AIManager>();
                aiManagerComponent.chatServiceConfig = genericChatServiceConfig;
                aiManagerComponent.speechToTextConfig = genericSTTServiceConfig;
                aiManagerComponent.textToSpeechConfig = genericTTSServiceConfig;

                AIManager.Instance.LoadAPIKeys(Application.dataPath + "/keys.json");

                aiManagerComponent._chatService = ServiceFactory.CreateChatService(aiManagerComponent.chatServiceConfig);
                aiManagerComponent._sttService = ServiceFactory.CreateSttService(aiManagerComponent.speechToTextConfig);
                aiManagerComponent._ttsService = ServiceFactory.CreateTtsService(aiManagerComponent.textToSpeechConfig);

                if (genericSTTServiceConfig is AzureSTTServiceConfig)
                    AIManager.Instance.InitSpeechRecognizer();

                //GameObject.Instantiate(aiManager);

                return aiManager;
            }
            else
            {
                return null;
            }
        }
    }
}