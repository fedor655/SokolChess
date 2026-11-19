using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Sokol.Engine;

namespace Sokol.Droid
{
    /// <summary>Theme colors, dp conversion and the bundled art (pieces, move badges).</summary>
    static class Ui
    {
        public static float Density = 1f;
        public static int Dp(float v) => (int)Math.Round(v * Density);
        public static float Dpf(float v) => v * Density;

        public static readonly Color WindowBg = Color.Rgb(48, 46, 43);
        public static readonly Color PanelBg = Color.Rgb(38, 37, 34);
        public static readonly Color CardBg = Color.Rgb(32, 31, 29);
        public static readonly Color RowHighlight = Color.Rgb(72, 70, 67);
        public static readonly Color Border = Color.Rgb(60, 58, 55);
        public static readonly Color Text = Color.Rgb(240, 240, 240);
        public static readonly Color TextDim = Color.Rgb(186, 186, 186);
        public static readonly Color TextMuted = Color.Rgb(139, 137, 135);
        public static readonly Color Accent = Color.Rgb(129, 182, 76);
        public static readonly Color AccentShadow = Color.Rgb(93, 140, 54);
        public static readonly Color Button = Color.Rgb(60, 58, 55);
        public static readonly Color LightSquare = Color.Rgb(235, 236, 208);
        public static readonly Color DarkSquare = Color.Rgb(115, 149, 82);
        public static readonly Color LightHighlight = Color.Rgb(245, 246, 130);
        public static readonly Color DarkHighlight = Color.Rgb(185, 202, 67);

        public static Color ClassColor(MoveClass c)
        {
            switch (c)
            {
                case MoveClass.Brilliant: return Color.Rgb(27, 172, 166);
                case MoveClass.Great: return Color.Rgb(92, 139, 176);
                case MoveClass.Best: return Color.Rgb(129, 182, 76);
                case MoveClass.Excellent: return Color.Rgb(129, 182, 76);
                case MoveClass.Good: return Color.Rgb(149, 183, 118);
                case MoveClass.Book: return Color.Rgb(168, 136, 101);
                case MoveClass.Inaccuracy: return Color.Rgb(247, 198, 49);
                case MoveClass.Mistake: return Color.Rgb(255, 164, 89);
                case MoveClass.Miss: return Color.Rgb(255, 119, 105);
                case MoveClass.Blunder: return Color.Rgb(250, 65, 45);
                default: return Color.Rgb(150, 150, 150);
            }
        }

        static readonly Dictionary<string, Bitmap> art = new Dictionary<string, Bitmap>();
        static Context ctx;

        public static void Init(Context context)
        {
            ctx = context;
            Density = context.Resources.DisplayMetrics.Density;
        }

        static Bitmap Load(string name)
        {
            if (art.TryGetValue(name, out var b)) return b;
            int id = ctx.Resources.GetIdentifier(name, "drawable", ctx.PackageName);
            var opts = new BitmapFactory.Options { InScaled = false };
            b = id != 0 ? BitmapFactory.DecodeResource(ctx.Resources, id, opts) : null;
            art[name] = b;
            return b;
        }

        public static Bitmap PieceBitmap(int piece)
        {
            string n = (Piece.ColorOf(piece) == Piece.White ? "w" : "b") + "pnbrqk"[Piece.TypeOf(piece)];
            return Load("piece_" + n);
        }

        public static Bitmap BadgeBitmap(MoveClass c) =>
            c == MoveClass.None ? null : Load("badge_" + c.ToString().ToLowerInvariant());

        static readonly Paint bitmapPaint = new Paint(PaintFlags.AntiAlias | PaintFlags.FilterBitmap);

        public static void DrawBitmap(Canvas canvas, Bitmap bmp, RectF dst)
        {
            if (bmp == null) return;
            canvas.DrawBitmap(bmp, null, dst, bitmapPaint);
        }

        public static void DrawBadge(Canvas canvas, MoveClass c, float cx, float cy, float d)
        {
            // The PNG has a little margin and a drop shadow: draw it slightly larger than the circle.
            float s = d * 96f / 84f;
            DrawBitmap(canvas, BadgeBitmap(c), new RectF(cx - s / 2, cy - s / 2 + d * 0.02f, cx + s / 2, cy + s / 2 + d * 0.02f));
        }

        public static Paint Fill(Color c)
        {
            var p = new Paint(PaintFlags.AntiAlias) { Color = c };
            p.SetStyle(Paint.Style.Fill);
            return p;
        }

        public static Paint TextPaint(Color c, float sp, bool bold = false)
        {
            var p = new Paint(PaintFlags.AntiAlias | PaintFlags.SubpixelText) { Color = c, TextSize = sp * Density };
            if (bold) p.SetTypeface(Typeface.Create(Typeface.Default, TypefaceStyle.Bold));
            return p;
        }

        public static Android.Graphics.Drawables.GradientDrawable Rounded(Color c, float radiusDp)
        {
            var g = new Android.Graphics.Drawables.GradientDrawable();
            g.SetColor(c);
            g.SetCornerRadius(Dpf(radiusDp));
            return g;
        }

        public static string LevelName(int elo)
        {
            if (elo < 600) return "Новичок";
            if (elo < 1000) return "Начинающий";
            if (elo < 1400) return "Любитель";
            if (elo < 1800) return "Клубный игрок";
            if (elo < 2100) return "Кандидат в мастера";
            if (elo < 2400) return "Мастер";
            if (elo < 2700) return "Гроссмейстер";
            if (elo < 3000) return "Элита";
            return "Полная сила движка";
        }
    }
}
