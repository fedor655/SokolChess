using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Sokol.Engine;

namespace Sokol.App
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static int Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => ShowError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowError(e.ExceptionObject as Exception);

            float scale;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
            Theme.Init(scale);
            Sounds.Init();

            if (args.Length >= 2 && args[0] == "--icon")
            {
                AppIcon.SaveIco(args[1]);
                return 0;
            }
            if (args.Length >= 2 && args[0] == "--android-assets")
            {
                ExportAndroidAssets(args[1]);
                return 0;
            }

            var form = new MainForm();
            if (args.Length >= 2 && args[0] == "--selftest") form.ConfigureSelfTest(Path.GetFullPath(args[1]));
            if (args.Length >= 2 && args[0] == "--snapshot")
            {
                string moves = "", size = "";
                bool review = false;
                int view = -1;
                for (int i = 2; i < args.Length; i++)
                {
                    if (args[i] == "--moves" && i + 1 < args.Length) moves = args[++i];
                    else if (args[i] == "--review") review = true;
                    else if (args[i] == "--view" && i + 1 < args.Length) view = int.Parse(args[++i]);
                    else if (args[i] == "--size" && i + 1 < args.Length) size = args[++i];
                }
                if (size.Length > 0)
                {
                    var p = size.Split('x');
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(0, 0);
                    form.Size = new Size(int.Parse(p[0]), int.Parse(p[1]));
                }
                form.ConfigureSnapshot(Path.GetFullPath(args[1]), moves, review, view);
            }
            Application.Run(form);
            return 0;
        }

        /// <summary>
        /// Renders the Android art (pieces, move badges, launcher icons) from DejaVu Sans,
        /// a freely redistributable font, so the phone app looks like the desktop one.
        /// </summary>
        static void ExportAndroidAssets(string resDir)
        {
            PieceRenderer.UseFont("DejaVu Sans");
            Theme.BadgeSymbolFont = "DejaVu Sans";
            Theme.BadgeTextFont = "DejaVu Sans";
            string drawable = Path.Combine(resDir, "drawable-nodpi");
            Directory.CreateDirectory(drawable);
            string[] names = { "p", "n", "b", "r", "q", "k" };
            for (int c = 0; c < 2; c++)
                for (int t = 0; t < 6; t++)
                    using (var src = PieceRenderer.Get(Piece.Make(c, t), 256))
                    using (var bmp = new Bitmap(src))
                        bmp.Save(Path.Combine(drawable, $"piece_{(c == 0 ? "w" : "b")}{names[t]}.png"), System.Drawing.Imaging.ImageFormat.Png);

            foreach (MoveClass mc in Enum.GetValues(typeof(MoveClass)))
            {
                if (mc == MoveClass.None) continue;
                using (var bmp = new Bitmap(96, 96, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                        Theme.DrawBadge(g, mc, 48, 46, 84);
                    }
                    bmp.Save(Path.Combine(drawable, $"badge_{mc.ToString().ToLowerInvariant()}.png"), System.Drawing.Imaging.ImageFormat.Png);
                }
            }

            var icons = new[] { ("mdpi", 48), ("hdpi", 72), ("xhdpi", 96), ("xxhdpi", 144), ("xxxhdpi", 192) };
            foreach (var (dpi, size) in icons)
            {
                string dir = Path.Combine(resDir, "mipmap-" + dpi);
                Directory.CreateDirectory(dir);
                using (var bmp = AppIcon.Render(size))
                    bmp.Save(Path.Combine(dir, "ic_launcher.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        static void ShowError(Exception ex)
        {
            try
            {
                string log = Path.Combine(Path.GetTempPath(), "SokolChess-error.txt");
                File.AppendAllText(log, DateTime.Now + "\r\n" + ex + "\r\n\r\n");
                MessageBox.Show("Произошла ошибка:\r\n" + ex?.Message + "\r\n\r\nПодробности: " + log, "Sokol Chess",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }

    /// <summary>Application icon drawn at runtime (knight on a green rounded square).</summary>
    static class AppIcon
    {
        public static Bitmap Render(int size)
        {
            var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                Theme.FillRound(g, Theme.Accent, new RectangleF(0, 0, size - 1, size - 1), size * 0.2f);
                Theme.FillRound(g, Theme.AccentShadow, new RectangleF(0, size * 0.88f, size - 1, size * 0.12f - 1), size * 0.06f);
                Theme.FillRound(g, Theme.Accent, new RectangleF(0, 0, size - 1, size * 0.9f), size * 0.2f);
                var piece = PieceRenderer.Get(Piece.Make(Piece.White, Piece.Knight), (int)(size * 0.9f));
                g.DrawImage(piece, size * 0.05f, size * 0.02f, size * 0.9f, size * 0.9f);
            }
            return bmp;
        }

        public static Icon Create()
        {
            using (var ms = new MemoryStream())
            {
                WriteIco(ms, new[] { 16, 32, 48, 256 });
                ms.Position = 0;
                return new Icon(ms);
            }
        }

        public static void SaveIco(string path)
        {
            using (var fs = File.Create(path)) WriteIco(fs, new[] { 16, 24, 32, 48, 64, 128, 256 });
        }

        static void WriteIco(Stream s, int[] sizes)
        {
            var pngs = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++)
                using (var bmp = Render(sizes[i]))
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    pngs[i] = ms.ToArray();
                }
            var w = new BinaryWriter(s);
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(pngs[i].Length);
                w.Write(offset);
                offset += pngs[i].Length;
            }
            foreach (var p in pngs) w.Write(p);
            w.Flush();
        }
    }
}
