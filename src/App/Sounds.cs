using System;
using System.IO;
using System.Media;

namespace Sokol.App
{
    /// <summary>Small synthesized sounds (no external files): move, capture, check, game end.</summary>
    static class Sounds
    {
        public static bool Enabled = true;
        static SoundPlayer move, capture, check, end;
        const int Rate = 44100;

        public static void Init()
        {
            try
            {
                move = Make(Thock(0, 1.0, 190, 1.0));
                capture = Make(Mix(Thock(0, 1.0, 230, 1.1), Thock(0.018, 0.8, 160, 1.0)));
                check = Make(Mix(Thock(0, 0.9, 200, 1.0), Tone(0.02, 0.35, 880, 0.12)));
                end = Make(Mix(Tone(0, 0.45, 523.25, 0.25), Tone(0.16, 0.45, 659.25, 0.35)));
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

        /// <summary>Wooden "thock": decaying low tone plus a short filtered noise transient.</summary>
        static double[] Thock(double delay, double amp, double freq, double length)
        {
            int n = (int)(Rate * (delay + 0.14 * length));
            var s = new double[n];
            var rng = new Random(7);
            double lp = 0;
            int start = (int)(delay * Rate);
            for (int i = start; i < n; i++)
            {
                double t = (i - start) / (double)Rate;
                double noise = rng.NextDouble() * 2 - 1;
                lp += (noise - lp) * 0.35;
                double body = Math.Sin(2 * Math.PI * freq * t) * Math.Exp(-t / 0.022);
                double body2 = Math.Sin(2 * Math.PI * freq * 2.7 * t) * Math.Exp(-t / 0.008) * 0.4;
                double click = lp * Math.Exp(-t / 0.0035) * 1.6;
                s[i] = amp * (0.55 * body + body2 + click) * 0.5;
            }
            return s;
        }

        static double[] Tone(double delay, double amp, double freq, double length)
        {
            int n = (int)(Rate * (delay + length));
            var s = new double[n];
            int start = (int)(delay * Rate);
            for (int i = start; i < n; i++)
            {
                double t = (i - start) / (double)Rate;
                double env = Math.Min(1, t / 0.004) * Math.Exp(-t / (length * 0.35));
                s[i] = amp * env * (Math.Sin(2 * Math.PI * freq * t) + 0.25 * Math.Sin(4 * Math.PI * freq * t)) * 0.45;
            }
            return s;
        }

        static double[] Mix(double[] a, double[] b)
        {
            var r = new double[Math.Max(a.Length, b.Length)];
            for (int i = 0; i < r.Length; i++) r[i] = (i < a.Length ? a[i] : 0) + (i < b.Length ? b[i] : 0);
            return r;
        }

        static SoundPlayer Make(double[] samples)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            int dataLen = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataLen);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataLen);
            foreach (double v in samples)
                w.Write((short)(Math.Max(-1, Math.Min(1, v)) * 30000));
            w.Flush();
            ms.Position = 0;
            var p = new SoundPlayer(ms);
            p.Load();
            return p;
        }
    }
}
