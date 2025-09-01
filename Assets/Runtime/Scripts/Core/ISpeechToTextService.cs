using Microsoft.CognitiveServices.Speech;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TimShaw.VoiceBox.Core
{
    public interface ISpeechToTextService
    {
        void Initialize(ScriptableObject config);

        Task TranscribeAudioFromMic(CancellationToken token);
    }
}