namespace TimShaw.VoiceBox.GUI
{
    using System;
    using System.Collections; // Needed for audio analysis
    using System.Collections.Generic;
    using System.ComponentModel;
    using TimShaw.VoiceBox.Components;
    using TimShaw.VoiceBox.Core;
    using TimShaw.VoiceBox.Generics;
    using UnityEngine;
    using UnityEngine.Networking;
    using UnityEngine.Windows;

    /// <summary>
    /// This script creates a full-screen GUI with API key inputs
    /// and microphone recording controls.
    /// </summary>
    public class GUIManager : MonoBehaviour
    {
        /// <summary>
        /// Singleton instance of GUI Manager
        /// </summary>
        public static GUIManager Instance { get; private set; }

        [Header("GUI Settings")]

#pragma warning disable CS1591 // Missing XML comment
        [Tooltip("The global scale for all GUI elements.")]
        public float guiScale = 1.5f; // 1.5f = 150% size

        [Tooltip("Pixel padding from the screen edges (before scaling).")]
        public float padding = 10f;

        [Tooltip("The color for our background.")]
        public Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.75f);

        [Header("API Keys")]
        private string chatApiKey = "";
        private string sttApiKey = "";
        private string ttsApiKey = "";

        [Header("Microphone")]
        [Tooltip("How sensitive the audio indicator is. Increase this if the bar is too low.")]
        public float audioSensitivity = 8.5f; // Was 5.0f

        [Tooltip("The height of the audio level bar in scaled pixels.")]
        public float audioBarHeight = 20f;

        [Tooltip("The max height of the microphone selection box in scaled pixels.")]
        public float micListMaxHeight = 100f;

        private float sliderValue = 60;
        private DateTime recordingStartTime = DateTime.Now;

        private static string[] micDevices;
        private static int selectedMicIndex = 0;
        private static bool isRecording = false;
        private static AudioClip recordingClip;

        // --- Audio Level Detection ---
        private float currentAudioLevel = 0f;
        private float[] audioSampleData;
        private int sampleWindow = 256; // How many samples to analyze for volume

        // --- State ---
        public bool enableKeyboardShortcut = true;
        public bool visible = false;
        private Vector2 windowScrollPosition;
        private Vector2 micScrollPosition;

        // --- Styles ---
        private GUIStyle indicatorBoxStyle;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;

        // --- Managers ---
        private GenericChatServiceConfig chatServiceConfig;
        private GenericSTTServiceConfig sttServiceConfig;
        private GenericTTSServiceConfig ttsServiceConfig;

        private ChatManager[] chatManagers = null;
        private TTSManager[] tTSManagers = null;

        // --- Keyboard input ---
        private HashSet<KeyCode> currentlyPressedKeys = new HashSet<KeyCode>();

#pragma warning restore CS1591 // Missing XML comment

        /// <summary>
        /// Callback for when mic recording is stopped
        /// </summary>
        public EventHandler<AudioClip> onRecordingStopped;

        /// <summary>
        /// Called when the load api keys button is pressed
        /// </summary>
        public EventHandler onApiKeysLoaded;

        /// <summary>
        /// Called when a microphone is selected.
        /// </summary>
        public EventHandler<string> onMicrophoneSelected;

        /// <summary>
        /// Creates a new gameobject with a <see cref="GUIManager"/> component.
        /// </summary>
        /// <returns></returns>
        public static GameObject CreateGUIManagerObject()
        {
            if (Instance != null) return Instance.gameObject;

            var manager = new GameObject("_VoiceBoxGUIManager");
            manager.AddComponent<GUIManager>();

            return manager;
        }

        private void Awake()
        {
            // --- Singleton Pattern ---
            if (Instance != null && Instance != this)
            {
                // If an instance already exists, destroy this new one
                Destroy(gameObject);
                return;
            }
            // This is the first instance, so set it
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Called when the script first loads.
        /// </summary>
        void Start()
        {
            // Get all available microphone devices
            micDevices = Microphone.devices;

            // Initialize the buffer for audio sample data
            audioSampleData = new float[sampleWindow];

            // Default to a message if no mics are found
            if (micDevices.Length == 0)
            {
                Debug.LogWarning("No microphone devices found!");
                micDevices = new string[] { "No microphones available" };
            }

            Debug.Log("[GUIManager] GUI Manager created!");
        }

        /// <summary>
        /// Called every frame. Used for non-GUI logic like audio processing.
        /// </summary>
        void Update()
        {
            // --- Audio processing ---
            if (isRecording && recordingClip != null)
            {
                // Analyze the audio level
                currentAudioLevel = GetAudioLevel();
            }
            else
            {
                currentAudioLevel = 0f;
            }
        }

        /// <summary>
        /// OnGUI is called for rendering and handling GUI events.
        /// </summary>
        void OnGUI()
        {
            if (enableKeyboardShortcut)
            {
                if (Event.current.isKey && Event.current.keyCode != KeyCode.None)
                {
                    if (Event.current.type == EventType.KeyDown)
                    {
                        currentlyPressedKeys.Add(Event.current.keyCode);
                    }
                    else if (Event.current.type == EventType.KeyUp)
                    {
                        currentlyPressedKeys.Remove(Event.current.keyCode);
                    }
                }

                // Shift + V + B
                if (Event.current.shift && currentlyPressedKeys.Contains(KeyCode.V) && currentlyPressedKeys.Contains(KeyCode.B))
                    visible = true;
            }

            // --- 0. CHECK VISIBILITY ---
            if (!visible)
            {
                return;
            }

            // --- 1. APPLY SCALING ---
            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(guiScale, guiScale, 1.0f));
            float scaledPadding = padding / guiScale;
            float scaledScreenWidth = Screen.width / guiScale;
            float scaledScreenHeight = Screen.height / guiScale;

            Rect screenRect = new Rect(
                scaledPadding,
                scaledPadding,
                scaledScreenWidth - (scaledPadding * 2),
                scaledScreenHeight - (scaledPadding * 2)
            );

            // --- 2. DRAW THE BACKGROUND ---
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = backgroundColor;
            GUI.Box(screenRect, GUIContent.none);
            GUI.backgroundColor = originalColor;

            // --- Lazy initialize our styles ---
            if (indicatorBoxStyle == null)
            {
                indicatorBoxStyle = new GUIStyle(GUI.skin.box);
                indicatorBoxStyle.alignment = TextAnchor.MiddleLeft;
            }

            if (titleStyle == null)
            {
                // Copy the default label style
                titleStyle = new GUIStyle(GUI.skin.label);

                // Make it bold
                titleStyle.fontStyle = FontStyle.Bold;

                // Read the font size from the *source style* (GUI.skin.label),
                // not the new one we just created.

                // Get the base font size from the default skin's label
                int baseFontSize = GUI.skin.label.fontSize;

                // Add a fallback in case the base font size is 0
                if (baseFontSize <= 0)
                {
                    baseFontSize = 12; // A sensible default size
                }

                // Now, set the new style's font size
                titleStyle.fontSize = baseFontSize * 2;

                // (Optional) Center it
                // titleStyle.alignment = TextAnchor.MiddleCenter;
            }

            if (subtitleStyle == null)
            {
                // Copy the default label style
                subtitleStyle = new GUIStyle(GUI.skin.label);

                // Make it bold
                subtitleStyle.fontStyle = FontStyle.Bold;

                // Read the font size from the *source style* (GUI.skin.label),
                // not the new one we just created.

                // Get the base font size from the default skin's label
                int baseFontSize = GUI.skin.label.fontSize;

                // Add a fallback in case the base font size is 0
                if (baseFontSize <= 0)
                {
                    baseFontSize = 12; // A sensible default size
                }

                // Now, set the new style's font size
                subtitleStyle.fontSize = Mathf.FloorToInt(baseFontSize * 1.2f);

                // (Optional) Center it
                // titleStyle.alignment = TextAnchor.MiddleCenter;
            }

            // --- 3. BEGIN LAYOUT AREA ---
            GUILayout.BeginArea(screenRect);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace(); // This pushes the button to the right

            // We can set a fixed (scaled) size for the button
            if (GUILayout.Button("Hide", GUILayout.Width(60), GUILayout.Height(25)))
            {
                visible = false;
            }
            GUILayout.EndHorizontal();

            windowScrollPosition = GUILayout.BeginScrollView(windowScrollPosition, false, true);

            GUILayout.Label("VoiceBox Utilities GUI", titleStyle);
            GUILayout.Label("");

            // --- Section: API Keys ---
            GUILayout.Label("API Keys", subtitleStyle);
            if (GUILayout.Button("Load API Keys"))
            {
                // CHAT
                if (FindFirstObjectByType<ChatManager>() != null)
                {
                    chatManagers = FindObjectsByType<ChatManager>(FindObjectsSortMode.None);
                    chatServiceConfig = chatManagers[0].chatServiceConfig;
                    chatApiKey = chatManagers[0].chatServiceConfig?.apiKey;
                }
                if (AIManager.Instance != null && AIManager.Instance.ChatService != null)
                {
                    chatServiceConfig = AIManager.Instance.chatServiceConfig;
                    chatApiKey = AIManager.Instance.chatServiceConfig.apiKey;
                }

                // STT
                if (AIManager.Instance != null && AIManager.Instance.SpeechToTextService != null)
                {
                    sttServiceConfig = AIManager.Instance.speechToTextConfig;
                    sttApiKey = AIManager.Instance.speechToTextConfig.apiKey;
                }

                // TTS
                if (FindFirstObjectByType<TTSManager>() != null)
                {
                    tTSManagers = FindObjectsByType<TTSManager>(FindObjectsSortMode.None);
                    ttsServiceConfig = tTSManagers[0].textToSpeechConfig;
                    ttsApiKey = tTSManagers[0].textToSpeechConfig?.apiKey;
                }
                if (AIManager.Instance != null && AIManager.Instance.TextToSpeechService != null)
                {
                    ttsServiceConfig = AIManager.Instance.textToSpeechConfig;
                    ttsApiKey = AIManager.Instance.textToSpeechConfig.apiKey;
                }

                onApiKeysLoaded?.Invoke(this, default);
            }

            // Use GUILayout.PasswordField for basic masking (displays as '*')
            // CHAT
            if (chatServiceConfig != null)
            {
                GUILayout.Label(
                    "Chat (Type: " 
                    + chatServiceConfig.serviceManagerType + ") (Manager Count: "
                    + chatManagers?.Length + ") ("
                    + (AIManager.Instance != null && AIManager.Instance.ChatService != null ? "AIManager)" : ")")
                );
            }
            else
                GUILayout.Label("Chat (Not Initialized)");
            chatApiKey = GUILayout.PasswordField(chatApiKey == null ? "" : chatApiKey, '*');

            // STT
            if (sttServiceConfig != null)
            {
                GUILayout.Label("Speech to Text (Type: " + sttServiceConfig.serviceManagerType + ") (AIManager)");
            }
            else
                GUILayout.Label("Speech to Text (Not Initialized)");
            sttApiKey = GUILayout.PasswordField(sttApiKey == null ? "" : sttApiKey, '*');

            // TTS
            if (ttsServiceConfig != null)
            {
                GUILayout.Label(
                    "Text to Speech (Type: "
                    + ttsServiceConfig.serviceManagerType + ") (Manager Count: " 
                    + tTSManagers?.Length + ") (" 
                    + (AIManager.Instance != null && AIManager.Instance.TextToSpeechService != null ? "AIManager)" : ")")
                );
            }
            else
                GUILayout.Label("Text to Speech (Not Initialized)");
            ttsApiKey = GUILayout.PasswordField(ttsApiKey == null ? "" : ttsApiKey, '*');

            // --- Section: Microphone ---
            GUILayout.Space(20); // Add some visual separation
            GUILayout.Label("Microphone Utilities", subtitleStyle);

            if (micDevices.Length > 0)
            {
                // --- Mic Selection ---
                GUILayout.Label("Select a microphone (does not affect services):");

                // --- Scrollable Mic List (Dropdown style) ---
                micScrollPosition = GUILayout.BeginScrollView(micScrollPosition, GUILayout.Height(micListMaxHeight));

                var previousSelectedMicIndex = selectedMicIndex;

                selectedMicIndex = GUILayout.SelectionGrid(selectedMicIndex, micDevices, 1);

                if (previousSelectedMicIndex != selectedMicIndex)
                    onMicrophoneSelected?.Invoke(this, micDevices[selectedMicIndex]);

                GUILayout.EndScrollView();

                GUILayout.Space(10);
                GUILayout.Label("Record an AudioClip", subtitleStyle);
                GUILayout.Label("Clip length: " + sliderValue + " seconds");
                sliderValue = (int)Math.Round(GUILayout.HorizontalSlider(sliderValue, 1, 600), 0);

                // --- Record Button ---
                string recordButtonText = isRecording ? "Stop Recording" : "Start Recording";
                if (GUILayout.Button(recordButtonText))
                {
                    if (isRecording)
                    {
                        var clip = StopRecording();
                        onRecordingStopped?.Invoke(this, clip);
                    }
                    else
                    {
                        recordingStartTime = DateTime.Now;
                        StartRecording();
                    }
                }

                GUILayout.Space(5); // Padding for the bar

                if (isRecording && (DateTime.Now - recordingStartTime).Seconds >= sliderValue)
                { 
                    var clip = StopRecording();
                    onRecordingStopped?.Invoke(this, clip);
                    Debug.Log($"[GUIManager] Clip size maximum of {sliderValue} reached. Stopped recording.");
                }

                // --- Audio Level Indicator ---
                if (isRecording)
                {
                    // --- 1. Calculate the visual level ---
                    float visualLevel = Mathf.Clamp01(currentAudioLevel * audioSensitivity);
                    if (visualLevel < 0.02f) visualLevel = 0f;

                    // --- 2. Draw the text label ---
                    GUILayout.Label($"Listening... Level: {(visualLevel * 100).ToString("F0")}%");

                    // --- 3. Draw the horizontal bar ---
                    // --- 3. Draw the horizontal bar ---

                    // Reserve a rectangle for our bar using the 'box' style for layout
                    Rect barRect = GUILayoutUtility.GetRect(GUIContent.none,
                                                            indicatorBoxStyle,
                                                            GUILayout.Height(audioBarHeight),
                                                            GUILayout.ExpandWidth(true));

                    // Store original colors
                    Color oldBackgroundColor = GUI.backgroundColor;
                    Color oldContentColor = GUI.color;

                    // --- Draw the dark background of the bar ---
                    // Using GUI.backgroundColor with GUI.Box is fine for the background
                    GUI.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f); // Dark grey
                    GUI.Box(barRect, GUIContent.none, indicatorBoxStyle);

                    // --- Draw the fill ---
                    // Calculate the rectangle for the "fill" part of the bar
                    Rect fillRect = new Rect(barRect.x,
                                            barRect.y,
                                            barRect.width * visualLevel, // This is the key part!
                                            barRect.height);

                    // Draw the fill, fading from green to red based on level
                    Color fillColor = Color.Lerp(Color.green, Color.red, visualLevel);

                    // Use GUI.color and DrawTexture for a solid rectangle
                    GUI.color = fillColor;
                    GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

                    // Restore the original colors for other GUI elements
                    GUI.color = oldContentColor;
                    GUI.backgroundColor = oldBackgroundColor;

                    GUILayout.Label("Recording length: " + (DateTime.Now - recordingStartTime).Seconds + " seconds");
                }
                else
                {
                    // "Not Recording" box
                    GUILayout.Box("Not Recording", indicatorBoxStyle);
                }
            }
            else
            {
                // Show if no mics were found
                GUILayout.Label("No microphone devices found.");
            }

            GUILayout.EndScrollView();

            GUILayout.EndArea();

            // --- 4. RESTORE THE MATRIX ---
            GUI.matrix = oldMatrix;
        }

        /// <summary>
        /// Starts the microphone recording.
        /// </summary>
        public static void StartRecording()
        {
            if (micDevices.Length == 0) return;

            string selectedDevice = micDevices[selectedMicIndex];
            // Start recording with a 1-second looping clip
            recordingClip = Microphone.Start(selectedDevice, false, 60, 44100);
            isRecording = true;
            Debug.Log($"Started recording from: {selectedDevice}");
        }

        /// <summary>
        /// Stops the microphone recording.
        /// </summary>
        public static AudioClip StopRecording()
        {
            if (micDevices.Length == 0) return null;

            string selectedDevice = micDevices[selectedMicIndex];
            Microphone.End(selectedDevice);
            isRecording = false;
            var returnClip = recordingClip;
            recordingClip = null;
            Debug.Log($"Stopped recording from: {selectedDevice}");
            return returnClip;
        }

        /// <summary>
        /// Analyzes the last 'sampleWindow' samples and returns the RMS volume.
        /// </summary>
        /// <returns>A float (0-1) representing the average volume.</returns>
        float GetAudioLevel()
        {
            if (recordingClip == null) return 0f;

            // Get the current position in the recording clip
            int micPosition = Microphone.GetPosition(micDevices[selectedMicIndex]);

            // Read sample data from the clip
            int readPosition = micPosition - sampleWindow;
            if (readPosition < 0)
            {
                readPosition = 0;
            }

            // Get the data from the clip
            recordingClip.GetData(audioSampleData, readPosition);

            // --- Calculate RMS (Root Mean Square) ---
            float sum = 0;
            for (int i = 0; i < sampleWindow; i++)
            {
                sum += audioSampleData[i] * audioSampleData[i]; // Sum of squares
            }
            float rms = Mathf.Sqrt(sum / sampleWindow); // Square root of the average

            return rms;
        }

        /// <summary>
        /// On disable, make sure we stop recording.
        /// </summary>
        void OnDisable()
        {
            if (isRecording)
            {
                StopRecording();
            }
        }
    }
}