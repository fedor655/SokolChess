using System;
using Android.Media;
using Sokol.Shared;

namespace Sokol.Droid
{
    /// <summary>Game sounds via AudioTrack (static mode) from the shared PCM synthesizer.</summary>
    static class AndroidSounds
    {
        public static bool Enabled = true;
        static AudioTrack move, capture, check, end;

        public static void Init()
        {
            try
            {
                move = Make(SoundSynth.Move());
                capture = Make(SoundSynth.Capture());
                check = Make(SoundSynth.Check());
                end = Make(SoundSynth.End());
            }
            catch (Exception) { move = capture = check = end = null; }
        }

        public static void Move() => Play(move);
        public static void Capture() => Play(capture);
        public static void Check() => Play(check);
        public static void End() => Play(end);

        static AudioTrack Make(short[] pcm)
        {
            var track = new AudioTrack.Builder()
                .SetAudioAttributes(new AudioAttributes.Builder()
                    .SetUsage(AudioUsageKind.Game)
                    .SetContentType(AudioContentType.Sonification)
                    .Build())
                .SetAudioFormat(new AudioFormat.Builder()
                    .SetEncoding(Encoding.Pcm16bit)
                    .SetSampleRate(SoundSynth.Rate)
                    .SetChannelMask(ChannelOut.Mono)
                    .Build())
                .SetTransferMode(AudioTrackMode.Static)
                .SetBufferSizeInBytes(pcm.Length * 2)
                .Build();
            track.Write(pcm, 0, pcm.Length);
            return track;
        }

        static void Play(AudioTrack t)
        {
            if (!Enabled || t == null) return;
            try
            {
                if (t.PlayState == PlayState.Playing) t.Stop();
                t.ReloadStaticData();
                t.Play();
            }
            catch (Exception) { }
        }

        public static void Release()
        {
            foreach (var t in new[] { move, capture, check, end })
                try { t?.Release(); } catch (Exception) { }
        }
    }
}
