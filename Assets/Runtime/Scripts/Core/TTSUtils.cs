using System.Collections.Generic;

namespace TimShaw.VoiceBox.Core
{
    public readonly struct AudioFileSource
    {
        public IEnumerable<string> Paths { get; }

        public AudioFileSource(string path) => Paths = new[] { path };

        public AudioFileSource(IEnumerable<string> paths) => Paths = paths;

        public static implicit operator AudioFileSource(string path) => new AudioFileSource(path);

        public static implicit operator AudioFileSource(List<string> paths) => new AudioFileSource(paths);
        public static implicit operator AudioFileSource(string[] paths) => new AudioFileSource(paths);
    }

    public readonly struct AudioDataSource
    {
        public IEnumerable<byte[]> Data { get; }

        public AudioDataSource(byte[] singleItem) => Data = new[] { singleItem };
        public AudioDataSource(IEnumerable<byte[]> collection) => Data = collection;

        public static implicit operator AudioDataSource(byte[] data) => new AudioDataSource(data);
        public static implicit operator AudioDataSource(List<byte[]> data) => new AudioDataSource(data);
        // Note: byte[][] is an array of arrays, handled here:
        public static implicit operator AudioDataSource(byte[][] data) => new AudioDataSource(data);
    }
}