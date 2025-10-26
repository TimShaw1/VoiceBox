

using Microsoft.CognitiveServices.Speech;

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
    }
}