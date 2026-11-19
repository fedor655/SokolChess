using System;

namespace Sokol.Shared
{
    /// <summary>Synthesizes the game sounds as 16-bit mono PCM (44.1 kHz). No sound files needed.</summary>
    public static class SoundSynth
    {
        public const int Rate = 44100;

        public static short[] Move() => ToPcm(Thock(0, 1.0, 190, 1.0));
        public static short[] Capture() => ToPcm(Mix(Thock(0, 1.0, 230, 1.1), Thock(0.018, 0.8, 160, 1.0)));
        public static short[] Check() => ToPcm(Mix(Thock(0, 0.9, 200, 1.0), Tone(0.02, 0.35, 880, 0.12)));
        public static short[] End() => ToPcm(Mix(Tone(0, 0.45, 523.25, 0.25), Tone(0.16, 0.45, 659.25, 0.35)));

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

        static short[] ToPcm(double[] samples)
        {
            var pcm = new short[samples.Length];
            for (int i = 0; i < samples.Length; i++)
                pcm[i] = (short)(Math.Max(-1, Math.Min(1, samples[i])) * 30000);
            return pcm;
        }
    }
}
