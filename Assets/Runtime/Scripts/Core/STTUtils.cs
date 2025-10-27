

using Microsoft.CognitiveServices.Speech;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Collections.Generic;
using System.Linq;

namespace TimShaw.VoiceBox.Core
{
    public class STTUtils
    {
        public class RecognitionResult
        {
            public ResultReason Reason { get; set; }
            
            public string Text { get; set; }
        }

        public class VoiceBoxSpeechRecognitionEventArgs
        {
            public RecognitionResult Result { get; set; }

            public VoiceBoxSpeechRecognitionEventArgs()
            {
                Result = new RecognitionResult();
            }

            public VoiceBoxSpeechRecognitionEventArgs(ResultReason reason, string text)
            {
                Result = new RecognitionResult();
                Result.Reason = reason;
                Result.Text = text;
            }

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