

using Microsoft.CognitiveServices.Speech;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Collections.Generic;
using System.Linq;

namespace TimShaw.VoiceBox.Core
{
    /// <summary>
    /// Utilities for Speech-To-Text services
    /// </summary>
    public class STTUtils
    {
        /// <summary>
        /// Represents a speech recognition with a <see cref="ResultReason"/> and text
        /// </summary>
        public class RecognitionResult
        {
            /// <summary>
            /// Describes a recognition result
            /// </summary>
            public ResultReason Reason { get; set; }
            
            /// <summary>
            /// The recognized text provided by the STT service
            /// </summary>
            public string Text { get; set; }
        }

        /// <summary>
        /// Represents various arguments for a speech recognition event.
        /// </summary>
        public class VoiceBoxSpeechRecognitionEventArgs
        {
            /// <summary>
            /// The recognition result. Provides a <see cref="ResultReason"/> and the recognized text
            /// </summary>
            public RecognitionResult Result { get; set; }

            /// <summary>
            /// The recognized text
            /// </summary>
            public string Text { get => Result.Text; }

            /// <summary>
            /// 
            /// </summary>
            public VoiceBoxSpeechRecognitionEventArgs()
            {
                Result = new RecognitionResult();
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="reason"></param>
            /// <param name="text"></param>
            public VoiceBoxSpeechRecognitionEventArgs(ResultReason reason, string text)
            {
                Result = new RecognitionResult();
                Result.Reason = reason;
                Result.Text = text;
            }

            /// <summary>
            /// Enables conversion from <see cref="SpeechRecognitionEventArgs"/> to <see cref="VoiceBoxSpeechRecognitionEventArgs"/> for usage with Azure STT
            /// </summary>
            /// <param name="args"></param>
            public static explicit operator VoiceBoxSpeechRecognitionEventArgs(SpeechRecognitionEventArgs args) => new VoiceBoxSpeechRecognitionEventArgs(args.Result.Reason, args.Result.Text);
        }

        /// <summary>
        /// Gets a dictionary of available audio input endpoints.
        /// </summary>
        /// <returns>A dictionary where the key is the friendly name of the device and the value is the device ID.</returns>
        public static Dictionary<string, string> GetAudioInputEndpoints()
        {
            var deviceList = new Dictionary<string, string>();
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();

            deviceList.Add("Default", "");

            foreach (var device in devices)
            {
                deviceList.Add(device.FriendlyName, device.ID);
            }

            return deviceList;
        }

        /// <summary>
        /// Gets the device number for a provided device name
        /// </summary>
        /// <param name="deviceName">The name of the device</param>
        /// <param name="audioInputEndpoints">The list of audio input endpoints</param>
        /// <returns>The device number of the audio device, if found. Otherwise, -1</returns>
        public static int GetAudioInputDeviceNum(string deviceName, Dictionary<string, string> audioInputEndpoints)
        {
            if (deviceName == "Default") return 0;

            int waveInDevices = WaveIn.DeviceCount;
            for (int waveInDeviceNum = 0; waveInDeviceNum < waveInDevices; waveInDeviceNum++)
            {
                WaveInCapabilities deviceInfo = WaveIn.GetCapabilities(waveInDeviceNum);
                foreach (string devName in audioInputEndpoints.Keys)
                {
                    if (devName.StartsWith(deviceInfo.ProductName) && devName == deviceName)
                    {
                        return waveInDeviceNum;
                    }
                }
            }

            UnityEngine.Debug.LogWarning("Device " + deviceName + " not found.");
            return -1;
        }
    }
}