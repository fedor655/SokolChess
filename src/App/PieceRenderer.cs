using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using Sokol.Engine;

namespace Sokol.App
{
    /// <summary>
    /// Renders chess pieces from the Segoe UI Symbol chess glyphs as crisp vector shapes:
    /// white pieces = ivory body + dark outline, black pieces = dark body + light detail lines.
    /// Bitmaps are cached per piece and size.
    /// </summary>
    static class PieceRenderer
    {
        static readonly Dictionary<long, Bitmap> cache = new Dictionary<long, Bitmap>();
        static FontFamily family = PickFamily(null);

        /// <summary>Switches the glyph font (used to export the Android piece set from DejaVu Sans).</summary>
        public static void UseFont(string name)
        {
            family = PickFamily(name);
            foreach (var b in cache.Values) b.Dispose();
            cache.Clear();
        }

        static FontFamily PickFamily(string preferred)
        {
            foreach (var name in new[] { preferred, "Segoe UI Symbol", "DejaVu Sans", "Arial Unicode MS" })
            {
                if (string.IsNullOrEmpty(name)) continue;
                try { return new FontFamily(name); } catch { }
            }
            return FontFamily.GenericSansSerif;
        }

        public static Bitmap Get(int piece, int size)
        {
            size = Math.Max(8, size);
            long key = ((long)piece << 32) | (uint)size;
            if (cache.TryGetValue(key, out Bitmap bmp)) return bmp;
            if (cache.Count > 200) { foreach (var b in cache.Values) b.Dispose(); cache.Clear(); }
            bmp = Render(piece, size);
            cache[key] = bmp;
            return bmp;
        }

        static Bitmap Render(int piece, int size)
        {
            var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                int type = Piece.TypeOf(piece);
                bool white = Piece.ColorOf(piece) == Piece.White;
                char outlineCh = (char)(0x2654 + new[] { 5, 4, 3, 2, 1, 0 }[type]);
                char solidCh = (char)(0x265A + new[] { 5, 4, 3, 2, 1, 0 }[type]);

                using (var outline = GlyphPath(outlineCh, size))
                using (var solid = GlyphPath(solidCh, size))
                {
                    // Fit both glyphs into the square using the outline glyph's bounds.
                    RectangleF bounds = outline.GetBounds();
                    float target = size * (type == Piece.Pawn ? 0.70f : 0.84f);
                    float k = target / Math.Max(bounds.Width, bounds.Height);
                    float dx = size / 2f - (bounds.X + bounds.Width / 2f) * k;
                    float dy = size * 0.94f - (bounds.Bottom) * k - size * (type == Piece.Pawn ? 0.08f : 0.03f);
                    var m = new Matrix();
                    m.Translate(dx, dy);
                    m.Scale(k, k);
                    outline.Transform(m);
                    solid.Transform(m);

                    // Soft shadow.
                    using (var sh = (GraphicsPath)solid.Clone())
                    {
                        var t = new Matrix();
                        t.Translate(0, size * 0.025f);
                        sh.Transform(t);
                        FillSilhouette(g, sh, Color.FromArgb(70, 0, 0, 0));
                    }

                    if (white)
                    {
                        RectangleF b2 = outline.GetBounds();
                        using (var body = new LinearGradientBrush(new PointF(b2.Left, b2.Top), new PointF(b2.Right, b2.Bottom),
                                   Color.FromArgb(255, 255, 255), Color.FromArgb(222, 220, 212)))
                            FillSilhouette(g, outline, body);
                        using (var ink = new SolidBrush(Color.FromArgb(38, 36, 33)))
                            g.FillPath(ink, outline);
                    }
                    else
                    {
                        FillSilhouette(g, solid, Color.FromArgb(225, 225, 220));
                        RectangleF b2 = solid.GetBounds();
                        using (var body = new LinearGradientBrush(new PointF(b2.Left, b2.Top), new PointF(b2.Right, b2.Bottom),
                                   Color.FromArgb(72, 70, 66), Color.FromArgb(22, 21, 20)))
                            g.FillPath(body, solid);
                        using (var pen = new Pen(Color.FromArgb(20, 20, 20), Math.Max(1f, size / 64f)))
                            g.DrawPath(pen, solid);
                    }
                }
            }
            return bmp;
        }

        static GraphicsPath GlyphPath(char ch, int size)
        {
            var p = new GraphicsPath(FillMode.Winding);
            p.AddString(ch.ToString(), family, (int)FontStyle.Regular, size, new PointF(0, 0), StringFormat.GenericTypographic);
            return p;
        }

        /// <summary>Fills every closed figure of the path on its own: the union is the solid silhouette.</summary>
        static void FillSilhouette(Graphics g, GraphicsPath path, Color color)
        {
            using (var b = new SolidBrush(color)) FillSilhouette(g, path, b);
        }

        static void FillSilhouette(Graphics g, GraphicsPath path, Brush brush)
        {
            using (var it = new GraphicsPathIterator(path))
            using (var sub = new GraphicsPath(FillMode.Winding))
            {
                it.Rewind();
                while (it.NextSubpath(sub, out bool closed) > 0)
                {
                    g.FillPath(brush, sub);
                    sub.Reset();
                }
            }
        }
    }
}
