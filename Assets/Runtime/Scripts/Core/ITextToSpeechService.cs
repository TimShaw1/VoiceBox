using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TimShaw.VoiceBox.Core
{
    public interface ITextToSpeechService
    {
        void Initialize(ScriptableObject config);
        public Task RequestAudioFile(string prompt, string fileName, string dir);

        public Task<AudioClip> RequestAudioClip(string prompt);

        public Task ConnectAndStream(string text, WebSocket _webSocket, StreamingMp3Decoder _mp3Decoder, CancellationToken token);
    }
}