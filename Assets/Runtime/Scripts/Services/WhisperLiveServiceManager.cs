using Microsoft.CognitiveServices.Speech;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimShaw.VoiceBox.Generics;
using Unity.VisualScripting.Antlr3.Runtime;
using static TimShaw.VoiceBox.Core.STTUtils;

namespace TimShaw.VoiceBox.Core
{
    public class Segment
    {
        public double start { get; set; }
        public double end { get; set; }
        public string text { get; set; }
        public bool completed { get; set; }
    }

    public class Client : IDisposable
    {
        public const string END_OF_AUDIO = "END_OF_AUDIO";
        public static readonly Dictionary<string, Client> INSTANCES = new();

        private readonly ClientWebSocket _socket = new ClientWebSocket();
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly string _uid = Guid.NewGuid().ToString();

        // config
        public bool Recording { get; private set; }
        public bool Waiting { get; private set; }
        public string Language { get; private set; }
        public string Model { get; }
        public string ServerBackend { get; private set; }
        public bool ServerError { get; private set; }
        public string SrtFilePath { get; }
        public bool UseVAD { get; }
        public bool LogTranscription { get; }
        public int SendLastNSegments { get; }
        public double NoSpeechThresh { get; }
        public bool ClipAudio { get; }
        public int SameOutputThreshold { get; }
        public string TargetLanguage { get; }
        private readonly bool _enableTranslation;

        private DateTime? _lastResponseReceived;
        private Segment _lastSegment;
        private string _lastReceivedSegmentText;
        private string _lastJoinedText;

        private readonly List<Segment> _transcript = new();
        private readonly List<Segment> _translatedTranscript = new();

        public event Action<string, List<Segment>> OnTranscription;
        public event Action<string, List<Segment>> OnTranslation;

        public double DisconnectIfNoResponseFor { get; set; } = 15.0;

        public Client(
            string host,
            int port,
            string lang = null,
            bool translate = false,
            string model = "small",
            string srtFilePath = "output.srt",
            bool useVAD = true,
            bool useWss = false,
            bool logTranscription = true,
            int sendLastNSegments = 10,
            double noSpeechThresh = 0.45,
            bool clipAudio = false,
            int sameOutputThreshold = 10,
            bool enableTranslation = false,
            string targetLanguage = "fr",
            CancellationToken token = default)
        {
            Language = lang;
            Model = model;
            SrtFilePath = srtFilePath;
            UseVAD = useVAD;
            LogTranscription = logTranscription;
            SendLastNSegments = sendLastNSegments;
            NoSpeechThresh = noSpeechThresh;
            ClipAudio = clipAudio;
            SameOutputThreshold = sameOutputThreshold;
            _enableTranslation = enableTranslation;
            TargetLanguage = targetLanguage;

            INSTANCES[_uid] = this;

            var scheme = useWss ? "wss" : "ws";
            var uri = new Uri($"{scheme}://{host}:{port}");

            _cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, token);
            Task.Run(() => ConnectAndRun(uri), _cts.Token);
        }

        public void _setCTS(CancellationToken token) { _cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, token); }

        private async Task ConnectAndRun(Uri uri)
        {
            try
            {
                await _socket.ConnectAsync(uri, _cts.Token);
                UnityEngine.Debug.Log("[INFO]: WebSocket connected.");

                var payload = new
                {
                    uid = _uid,
                    language = Language,
                    task = _enableTranslation ? "translate" : "transcribe",
                    model = Model,
                    use_vad = UseVAD,
                    send_last_n_segments = SendLastNSegments,
                    no_speech_thresh = NoSpeechThresh,
                    clip_audio = ClipAudio,
                    same_output_threshold = SameOutputThreshold,
                    enable_translation = _enableTranslation,
                    target_language = TargetLanguage
                };
                await SendJsonAsync(payload);

                _ = Task.Run(ReceiveLoop);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.Log($"[ERROR]: Failed to connect: {ex.Message}");
                ServerError = true;
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[8192];
            try
            {
                while (!_cts.IsCancellationRequested && _socket.State == WebSocketState.Open)
                {
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", _cts.Token);
                        break;
                    }

                    var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    HandleMessage(msg);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                UnityEngine.Debug.Log($"[ERROR] ReceiveLoop: {ex.Message}");
                ServerError = true;
            }
        }

        private void HandleMessage(string msg)
        {
            try
            {
                var json = JObject.Parse(msg);

                if (json["uid"] != null && json["uid"].Value<string>() != _uid)
                    return;

                if (json["status"] != null)
                {
                    HandleStatus(json);
                    return;
                }

                if (json["message"]?.Value<string>() == "DISCONNECT")
                {
                    UnityEngine.Debug.Log("[INFO]: Server disconnected due to timeout.");
                    Recording = false;
                    return;
                }

                if (json["message"]?.Value<string>() == "SERVER_READY")
                {
                    _lastResponseReceived = DateTime.UtcNow;
                    Recording = true;
                    ServerBackend = json["backend"]?.Value<string>();
                    UnityEngine.Debug.Log($"[INFO]: Server backend: {ServerBackend}");
                    return;
                }

                if (json["language"] != null)
                {
                    Language = json["language"].Value<string>();
                    UnityEngine.Debug.Log($"[INFO]: Detected language {Language}");
                }

                if (json["segments"] != null)
                    ProcessSegments(json["segments"].ToObject<List<Segment>>(), false);

                if (json["translated_segments"] != null)
                    ProcessSegments(json["translated_segments"].ToObject<List<Segment>>(), true);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.Log($"[ERROR] HandleMessage: {ex.Message}");
            }
        }

        private void HandleStatus(JObject msg)
        {
            var status = msg["status"]?.Value<string>();
            if (status == "WAIT")
            {
                Waiting = true;
                var est = msg["message"]?.Value<double>() ?? 0;
                UnityEngine.Debug.Log($"[INFO]: Server full. Est wait {Math.Round(est)} min");
            }
            else if (status == "ERROR" || status == "WARNING")
            {
                UnityEngine.Debug.Log($"[SERVER {status}]: {msg["message"]}");
                if (status == "ERROR") ServerError = true;
            }
        }

        private void ProcessSegments(List<Segment> segs, bool translated)
        {
            if (segs == null || segs.Count == 0) return;

            foreach (var s in segs)
            {
                if (translated)
                    _translatedTranscript.Add(s);
                else
                    _transcript.Add(s);
            }

            _lastResponseReceived = DateTime.UtcNow;

            var joined = string.Join(" ", segs.Select(s => s.text.Trim()));
            if (joined == _lastJoinedText)
                return;

            _lastJoinedText = joined;
            if (translated)
                OnTranslation?.Invoke(joined, segs);
            else
                OnTranscription?.Invoke(joined, segs);
        }

        public async Task SendJsonAsync(object obj)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj));
            await SendAsync(bytes, WebSocketMessageType.Text);
        }

        public async Task SendAsync(byte[] data, WebSocketMessageType type = WebSocketMessageType.Binary)
        {
            if (_socket.State != WebSocketState.Open) return;
            await _sendLock.WaitAsync();
            try
            {
                await _socket.SendAsync(new ArraySegment<byte>(data), type, true, _cts.Token);
            }
            finally { _sendLock.Release(); }
        }

        public void WriteSrtFile(string path = null)
        {
            if (path == null) path = SrtFilePath;
            SrtUtils.CreateSrtFile(_transcript, path);
            if (_enableTranslation)
                SrtUtils.CreateSrtFile(_translatedTranscript, "translated_" + path);
        }

        public void WaitBeforeDisconnect()
        {
            if (!_lastResponseReceived.HasValue) return;
            while ((DateTime.UtcNow - _lastResponseReceived.Value).TotalSeconds < DisconnectIfNoResponseFor)
                Thread.Sleep(100);
        }

        public async Task CloseAsync()
        {
            try
            {
                _cts.Cancel();
                if (_socket.State == WebSocketState.Open)
                    await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            catch { }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _socket.Dispose();
            _sendLock.Dispose();
        }

        public string Uid => _uid;
    }

    public static class SrtUtils
    {
        public static void CreateSrtFile(IEnumerable<Segment> segments, string path)
        {
            var list = segments.ToList();
            using var sw = new StreamWriter(path, false, new UTF8Encoding(false));
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                sw.WriteLine(i + 1);
                sw.WriteLine($"{Fmt(s.start)} --> {Fmt(s.end)}");
                sw.WriteLine(s.text);
                sw.WriteLine();
            }
            UnityEngine.Debug.Log($"[INFO]: Wrote SRT {path}");
        }

        private static string Fmt(double sec)
        {
            var t = TimeSpan.FromSeconds(sec);
            return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00},{t.Milliseconds:000}";
        }
    }

    public class TranscriptionTeeClient
    {
        protected readonly List<Client> Clients;
        protected WaveFormat Format = new(16000, 16, 1);
        protected int Chunk = 4096;

        private WaveInEvent _waveIn;

        public TranscriptionTeeClient(List<Client> clients)
        {
            Clients = clients;
        }

        public void StartRecording()
        {
            _waveIn = new WaveInEvent
            {
                WaveFormat = Format,
                BufferMilliseconds = (int)Math.Round(Chunk / (double)Format.SampleRate * 1000)
            };
            _waveIn.DataAvailable += (s, e) =>
            {
                var bytes = Int16ToFloatBytes(e.Buffer, e.BytesRecorded);
                foreach (var c in Clients)
                    if (c.Recording)
                        _ = c.SendAsync(bytes);
            };
            _waveIn.StartRecording();
        }

        public void StopRecording()
        {
            _waveIn?.StopRecording();
            _waveIn?.Dispose();
            foreach (var c in Clients)
            {
                c.WaitBeforeDisconnect();
                _ = c.SendAsync(Encoding.UTF8.GetBytes(Client.END_OF_AUDIO), WebSocketMessageType.Text);
                //c.WriteSrtFile();
                _ = c.CloseAsync();
            }
        }

        private static byte[] Int16ToFloatBytes(byte[] pcm, int len)
        {
            int samples = len / 2;
            var bytes = new byte[samples * 4];
            for (int i = 0; i < samples; i++)
            {
                short s = BitConverter.ToInt16(pcm, i * 2);
                float f = s / 32768f;
                Array.Copy(BitConverter.GetBytes(f), 0, bytes, i * 4, 4);
            }
            return bytes;
        }
    }

    public class TranscriptionClient : TranscriptionTeeClient
    {
        public Client Client { get; }

        public TranscriptionClient(
            string host, int port,
            string lang = null,
            bool translate = false,
            bool enableTranslation = false,
            string targetLanguage = "en",
            CancellationToken token = default)
            : base(new List<Client>())
        {
            Client = new Client(host, port, lang, translate, enableTranslation: enableTranslation, targetLanguage: targetLanguage, token: token);
            Clients.Add(Client);
        }

        public void RunMic()
        {
            while (!Client.Recording)
            {
                if (Client.Waiting || Client.ServerError)
                    return;
                Thread.Sleep(50);
            }
            UnityEngine.Debug.Log("[INFO]: Server ready, starting mic...");
            StartRecording();
        }
    }

    public class WhisperLiveServiceManager : ISpeechToTextService
    {
        private TranscriptionClient _client;

        public event EventHandler<VoiceBoxSpeechRecognitionEventArgs> OnRecognizing;
        public event EventHandler<VoiceBoxSpeechRecognitionEventArgs> OnRecognized;
        public event EventHandler<SpeechRecognitionCanceledEventArgs> OnCanceled;
        public event EventHandler<SessionEventArgs> OnSessionStarted;
        public event EventHandler<SessionEventArgs> OnSessionStopped;
        public event EventHandler<RecognitionEventArgs> OnSpeechStartDetected;
        public event EventHandler<RecognitionEventArgs> OnSpeechEndDetected;

        public void Initialize(GenericSTTServiceConfig config)
        {
            _client = new TranscriptionClient("localhost", 9090, lang: "en");
        }

        public async Task TranscribeAudioFromMic(CancellationToken token)
        {
            var stopRecognition = new TaskCompletionSource<int>();
            token.Register(() => stopRecognition.TrySetResult(0));

            _client.Client._setCTS(token);
            _client.Client.OnTranscription += (text, segs) =>
            {
                OnRecognized.Invoke(this, new VoiceBoxSpeechRecognitionEventArgs(ResultReason.RecognizedSpeech, text));
            };

            _client.RunMic();

            await stopRecognition.Task;
            
            _client.StopRecording();

            UnityEngine.Debug.Log("WhisperLive Service Manager: Transcription stopped.");
        }
    }
}
