

using Microsoft.CognitiveServices.Speech;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using static TimShaw.VoiceBox.Core.STTUtils;

namespace TimShaw.VoiceBox.Core
{
    /// <summary>
    /// Utilities for Speech-To-Text services
    /// </summary>
    public class STTUtils
    {
        public enum VoiceBoxResultReason
        {
            NoMatch,
            Canceled,
            RecognizingSpeech,
            RecognizedSpeech,
            RecognizingIntent,
            RecognizedIntent,
            TranslatingSpeech,
            TranslatedSpeech,
            SynthesizingAudio,
            SynthesizingAudioCompleted,
            RecognizingKeyword,
            RecognizedKeyword,
            SynthesizingAudioStarted,
            TranslatingParticipantSpeech,
            TranslatedParticipantSpeech,
            TranslatedInstantMessage,
            TranslatedParticipantInstantMessage,
            EnrollingVoiceProfile,
            EnrolledVoiceProfile,
            RecognizedSpeakers,
            RecognizedSpeaker,
            ResetVoiceProfile,
            DeletedVoiceProfile,
            VoicesListRetrieved,
            RecognizedSpeechWithTimestamps
        }

        /// <summary>
        /// Represents a speech recognition with a <see cref="VoiceBoxResultReason"/> and text
        /// </summary>
        public class RecognitionResult
        {
            /// <summary>
            /// Describes a recognition result
            /// </summary>
            public VoiceBoxResultReason Reason { get; set; }
            
            /// <summary>
            /// The recognized text provided by the STT service
            /// </summary>
            public string Text { get; set; }

            /// <summary>
            /// The duration of the result in ticks
            /// </summary>
            public TimeSpan Duration { get; set; }

            /// <summary>
            /// The offset of the result in ticks
            /// </summary>
            public long OffsetInTicks {  get; set; }
        }

        /// <summary>
        /// Represents various arguments for a speech recognition event.
        /// </summary>
        public class VoiceBoxSpeechRecognitionEventArgs
        {
            /// <summary>
            /// The recognition result. Provides a <see cref="VoiceBoxResultReason"/> and the recognized text
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
            public VoiceBoxSpeechRecognitionEventArgs(VoiceBoxResultReason reason, string text, TimeSpan duration, long offsetInTicks)
            {
                Result = new RecognitionResult();
                Result.Reason = reason;
                Result.Text = text;
                Result.Duration = duration;
                Result.OffsetInTicks = offsetInTicks;
            }

            /// <summary>
            /// Enables conversion from <see cref="SpeechRecognitionEventArgs"/> to <see cref="VoiceBoxSpeechRecognitionEventArgs"/> for usage with Azure STT
            /// </summary>
            /// <param name="args"></param>
            public static explicit operator VoiceBoxSpeechRecognitionEventArgs(SpeechRecognitionEventArgs args) => new VoiceBoxSpeechRecognitionEventArgs((VoiceBoxResultReason)args.Result.Reason, args.Result.Text, args.Result.Duration, args.Result.OffsetInTicks);
        }

        public class VoiceBoxSpeechRecognitionCanceledEventArgs : EventArgs
        {
            /// <summary>
            /// The high-level reason why the recognition was canceled.
            /// </summary>
            public CancellationReason Reason { get; private set; }

            /// <summary>
            /// The specific error code provided by the service (e.g. "auth_error", "quota_exceeded", "401").
            /// </summary>
            public string ErrorCode { get; private set; }

            /// <summary>
            /// A human-readable message describing the error or cancellation details.
            /// </summary>
            public string ErrorDetails { get; private set; }

            public VoiceBoxSpeechRecognitionCanceledEventArgs(CancellationReason reason, string errorCode, string errorDetails)
            {
                Reason = reason;
                ErrorCode = errorCode;
                ErrorDetails = errorDetails;
            }

            public static explicit operator VoiceBoxSpeechRecognitionCanceledEventArgs(SpeechRecognitionCanceledEventArgs args)
            {
                if (args == null) return null;

                // 1. Map the Reason Enum
                CancellationReason customReason = CancellationReason.Error; // Default
                switch (args.Reason)
                {
                    case Microsoft.CognitiveServices.Speech.CancellationReason.Error:
                        customReason = CancellationReason.Error;
                        break;
                    case Microsoft.CognitiveServices.Speech.CancellationReason.EndOfStream:
                        customReason = CancellationReason.EndOfStream;
                        break;
                    case Microsoft.CognitiveServices.Speech.CancellationReason.CancelledByUser:
                        customReason = CancellationReason.User;
                        break;
                }

                // 2. Map Error Code and Details
                // The Microsoft args.ErrorCode is an Enum, so we convert it to string.
                string errorCodeStr = args.ErrorCode.ToString();

                return new VoiceBoxSpeechRecognitionCanceledEventArgs(
                    customReason,
                    errorCodeStr,
                    args.ErrorDetails
                );
            }
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
        /// Defines why the recognition result was canceled.
        /// </summary>
        public enum CancellationReason
        {
            /// <summary>
            /// The service encountered an error (network, auth, timeout, etc.).
            /// </summary>
            Error,

            /// <summary>
            /// The user or client application manually requested cancellation.
            /// </summary>
            EndOfStream,

            /// <summary>
            /// The operation was canceled by the user explicitly.
            /// </summary>
            User
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