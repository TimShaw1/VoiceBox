using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TimShaw.VoiceBox.Core
{
    public interface ITextToSpeechService
    {
        void Initialize(ScriptableObject config);
        public Task RequestAudio(string prompt, string fileName, string dir);
    }
}