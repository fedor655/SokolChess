using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Sokol.Engine;

namespace Sokol.App
{
    /// <summary>Colors, fonts and drawing helpers (dark theme in the spirit of chess.com).</summary>
    static class Theme
    {
        public static float Scale = 1f;
        public static int S(float px) => (int)Math.Round(px * Scale);

        public static readonly Color WindowBg = Color.FromArgb(48, 46, 43);
        public static readonly Color PanelBg = Color.FromArgb(38, 37, 34);
        public static readonly Color CardBg = Color.FromArgb(32, 31, 29);
        public static readonly Color RowAlt = Color.FromArgb(43, 41, 38);
        public static readonly Color RowHighlight = Color.FromArgb(72, 70, 67);
        public static readonly Color Border = Color.FromArgb(60, 58, 55);
        public static readonly Color Text = Color.FromArgb(240, 240, 240);
        public static readonly Color TextDim = Color.FromArgb(186, 186, 186);
        public static readonly Color TextMuted = Color.FromArgb(139, 137, 135);
        public static readonly Color Accent = Color.FromArgb(129, 182, 76);
        public static readonly Color AccentHover = Color.FromArgb(149, 200, 96);
        public static readonly Color AccentShadow = Color.FromArgb(93, 140, 54);
        public static readonly Color Button = Color.FromArgb(60, 58, 55);
        public static readonly Color ButtonHover = Color.FromArgb(76, 74, 70);
        public static readonly Color ButtonShadow = Color.FromArgb(40, 39, 36);

        public static readonly Color LightSquare = Color.FromArgb(235, 236, 208);
        public static readonly Color DarkSquare = Color.FromArgb(115, 149, 82);
        public static readonly Color LightHighlight = Color.FromArgb(245, 246, 130);
        public static readonly Color DarkHighlight = Color.FromArgb(185, 202, 67);

        public static Font UiFont, UiBold, UiSmall, UiSmallBold, UiLarge, UiTitle, MoveFont, MoveFontBold, BadgeFont, CoordFont;

        public static void Init(float scale)
        {
            Scale = scale;
            UiFont = new Font("Segoe UI", 10f);
            UiBold = new Font("Segoe UI Semibold", 10f);
            UiSmall = new Font("Segoe UI", 8.5f);
            UiSmallBold = new Font("Segoe UI Semibold", 8.5f);
            UiLarge = new Font("Segoe UI Semibold", 13f);
            UiTitle = new Font("Segoe UI Semibold", 16f);
            MoveFont = new Font("Segoe UI", 10.5f);
            MoveFontBold = new Font("Segoe UI Semibold", 10.5f);
            BadgeFont = new Font("Segoe UI Black", 8f, FontStyle.Bold);
            CoordFont = new Font("Segoe UI Semibold", 8.5f);
        }

        public static Color ClassColor(MoveClass c)
        {
            switch (c)
            {
                case MoveClass.Brilliant: return Color.FromArgb(27, 172, 166);
                case MoveClass.Great: return Color.FromArgb(92, 139, 176);
                case MoveClass.Best: return Color.FromArgb(129, 182, 76);
                case MoveClass.Excellent: return Color.FromArgb(129, 182, 76);
                case MoveClass.Good: return Color.FromArgb(149, 183, 118);
                case MoveClass.Book: return Color.FromArgb(168, 136, 101);
                case MoveClass.Inaccuracy: return Color.FromArgb(247, 198, 49);
                case MoveClass.Mistake: return Color.FromArgb(255, 164, 89);
                case MoveClass.Miss: return Color.FromArgb(255, 119, 105);
                case MoveClass.Blunder: return Color.FromArgb(250, 65, 45);
                case MoveClass.Forced: return Color.FromArgb(150, 150, 150);
                default: return Color.Gray;
            }
        }

        /// <summary>Short symbol drawn inside the round badge.</summary>
        public static string BadgeText(MoveClass c)
        {
            switch (c)
            {
                case MoveClass.Brilliant: return "!!";
                case MoveClass.Great: return "!";
                case MoveClass.Best: return "★";
                case MoveClass.Excellent: return "✔";
                case MoveClass.Good: return "✓";
                case MoveClass.Book: return "";
                case MoveClass.Inaccuracy: return "?!";
                case MoveClass.Mistake: return "?";
                case MoveClass.Miss: return "✕";
                case MoveClass.Blunder: return "??";
                case MoveClass.Forced: return "□";
                default: return "";
            }
        }

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Color c, RectangleF r, float radius)
        {
            using (var path = RoundRect(r, radius))
            using (var b = new SolidBrush(c))
                g.FillPath(b, path);
        }

        public static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        /// <summary>Draws a round classification badge centered at (cx, cy).</summary>
        public static void DrawBadge(Graphics g, MoveClass c, float cx, float cy, float diameter)
        {
            if (c == MoveClass.None) return;
            var r = new RectangleF(cx - diameter / 2, cy - diameter / 2, diameter, diameter);
            using (var shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
                g.FillEllipse(shadow, r.X, r.Y + diameter * 0.06f, r.Width, r.Height);
            using (var b = new SolidBrush(ClassColor(c)))
                g.FillEllipse(b, r);
            using (var pen = new Pen(Color.FromArgb(230, 255, 255, 255), Math.Max(1f, diameter * 0.07f)))
                g.DrawEllipse(pen, r);
            DrawBadgeSymbol(g, c, r);
        }

        static void DrawBadgeSymbol(Graphics g, MoveClass c, RectangleF r)
        {
            if (c == MoveClass.Book)
            {
                // Small open book: two pages around a spine.
                float w = r.Width, h = r.Height, cx = r.X + w / 2, top = r.Y + h * 0.30f, bot = r.Y + h * 0.70f;
                using (var b = new SolidBrush(Color.White))
                {
                    g.FillPolygon(b, new[] { new PointF(cx - w * 0.03f, top + h * 0.05f), new PointF(cx - w * 0.30f, top - h * 0.02f),
                        new PointF(cx - w * 0.30f, bot - h * 0.06f), new PointF(cx - w * 0.03f, bot) });
                    g.FillPolygon(b, new[] { new PointF(cx + w * 0.03f, top + h * 0.05f), new PointF(cx + w * 0.30f, top - h * 0.02f),
                        new PointF(cx + w * 0.30f, bot - h * 0.06f), new PointF(cx + w * 0.03f, bot) });
                }
                return;
            }
            string text = BadgeText(c);
            bool symbol = c == MoveClass.Excellent || c == MoveClass.Best || c == MoveClass.Good || c == MoveClass.Miss || c == MoveClass.Forced;
            float size = r.Height * (text.Length >= 2 ? 0.46f : symbol ? 0.58f : 0.56f);
            bool emoji = symbol;
            using (var f = new Font(emoji ? "Segoe UI Symbol" : "Segoe UI Black", Math.Max(4f, size), FontStyle.Bold, GraphicsUnit.Pixel))
            using (var path = new GraphicsPath())
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                path.AddString(text, f.FontFamily, (int)f.Style, f.Size, new RectangleF(r.X, r.Y + r.Height * 0.02f, r.Width, r.Height), sf);
                using (var b = new SolidBrush(Color.White))
                    g.FillPath(b, path);
            }
        }
    }
}
