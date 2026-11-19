using System.IO;
using System.Media;
using Sokol.Shared;

namespace Sokol.App
{
    /// <summary>Game sounds for Windows: shared PCM synthesis wrapped into in-memory WAV files.</summary>
    static class Sounds
    {
        public static bool Enabled = true;
        static SoundPlayer move, capture, check, end;

        public static void Init()
        {
            try
            {
                move = Make(SoundSynth.Move());
                capture = Make(SoundSynth.Capture());
                check = Make(SoundSynth.Check());
                end = Make(SoundSynth.End());
            }
            catch { Enabled = false; }
        }

        public static void Move() => Play(move);
        public static void Capture() => Play(capture);
        public static void Check() => Play(check);
        public static void End() => Play(end);

        static void Play(SoundPlayer p)
        {
            if (!Enabled || p == null) return;
            try { p.Play(); } catch { }
        }

        static SoundPlayer Make(short[] pcm)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            int rate = SoundSynth.Rate;
            int dataLen = pcm.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataLen);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataLen);
            foreach (short v in pcm) w.Write(v);
            w.Flush();
            ms.Position = 0;
            var p = new SoundPlayer(ms);
            p.Load();
            return p;
        }
    }
}
