using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DebateAI
{
    // Animation state of the NOVA figure, same behaviour as overlay/nova.html (pure logic, testable).
    internal sealed class OverlayMotion
    {
        public double Level, Glow = 0.25, Spin;
        public int Frame;

        public void Step(OverlayState state)
        {
            string phase = state == null ? "idle" : state.Phase ?? "idle";
            bool speaking = phase == "speaking";
            bool thinking = phase == "thinking" || phase == "connecting";
            bool listening = phase == "guest" || phase == "listening";
            double target = speaking ? Math.Max(0.08, state.Level) : 0;
            Level += (target - Level) * (target > Level ? 0.55 : 0.18);
            Glow += ((speaking ? 1 : thinking ? 0.7 : listening ? 0.45 : 0.25) - Glow) * 0.08;
            Spin += speaking ? 0.035 + Level * 0.05 : thinking ? 0.06 : 0.012;
            Frame++;
        }

        public bool Listening(OverlayState state) { return state != null && (state.Phase == "guest" || state.Phase == "listening"); }
    }

    // Draws the 9:16 scene "DU GEGEN NOVA" natively (GDI+), in a 1080 × 1920 coordinate system.
    // Replaces the LIVE Studio "Link" source, which LIVE Studio 1.40.2 rejects for local addresses.
    internal static class OverlayPainter
    {
        public const string Label = "NOVA · KI-Debattenpartnerin · synthetische Stimme";
        private static readonly Color Cyan = Color.FromArgb(111, 243, 255), Violet = Color.FromArgb(170, 70, 255), Lilac = Color.FromArgb(201, 184, 255);
        private static readonly Color Back = Color.FromArgb(5, 6, 15);

        public static string StatusText(string phase)
        {
            switch (phase)
            {
                case "speaking": return "spricht";
                case "thinking": return "denkt nach …";
                case "guest": return "hört dir zu";
                case "listening": return "hört zu";
                case "connecting": return "verbindet …";
                case "paused": return "Pause";
                default: return "bereit";
            }
        }

        public static void Paint(Graphics g, Size target, OverlayState state, OverlayMotion motion)
        {
            state = state ?? new OverlayState();
            motion = motion ?? new OverlayMotion();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.ResetTransform();
            g.Clear(Back);
            g.ScaleTransform(target.Width / 1080f, target.Height / 1920f);

            Glow(g, 216, 346, 460, Color.FromArgb(56, 0, 229, 255));
            Glow(g, 886, 768, 500, Color.FromArgb(61, 170, 70, 255));
            Glow(g, 540, 1824, 560, Color.FromArgb(31, 0, 229, 255));

            string name = string.IsNullOrEmpty((state.Name ?? "").Trim()) ? "Nova" : state.Name.Trim();
            GlowText(g, "DU GEGEN NOVA", 104, FontStyle.Bold, Cyan, new RectangleF(0, 60, 1080, 140));
            Text(g, "Debattiere LIVE mit einer KI", 40, FontStyle.Regular, Lilac, new RectangleF(0, 200, 1080, 56));

            Orb(g, state, motion);

            GlowText(g, name.ToUpperInvariant(), 76, FontStyle.Bold, Color.White, new RectangleF(0, 888, 1080, 96));
            Text(g, name + " " + StatusText(state.Phase), 40, FontStyle.Regular, Cyan, new RectangleF(0, 985, 1080, 56));

            using (GraphicsPath pill = Rounded(new RectangleF(90, 1052, 900, 66), 33))
            using (var fill = new SolidBrush(Color.FromArgb(209, 10, 12, 30)))
            using (var border = new Pen(Color.FromArgb(178, 111, 243, 255), 2))
            {
                g.FillPath(fill, pill); g.DrawPath(border, pill);
            }
            Text(g, Label, 33, FontStyle.Regular, Color.FromArgb(234, 246, 255), new RectangleF(90, 1052, 900, 66));

            string topic = (state.Topic ?? "").Trim();
            if (topic.Length != 0)
            {
                Text(g, "T H E M A", 26, FontStyle.Regular, Lilac, new RectangleF(70, 1132, 940, 36));
                Text(g, topic, 42, FontStyle.Bold, Color.White, new RectangleF(70, 1164, 940, 88));
            }

            using (GraphicsPath frame = Rounded(new RectangleF(60, 1290, 952, 572), 36))
            {
                using (var shadow = new Pen(Color.FromArgb(60, 0, 229, 255), 14)) g.DrawPath(shadow, frame);
                using (var pen = new Pen(Color.FromArgb(166, 111, 243, 255), 4)) g.DrawPath(pen, frame);
            }
            using (GraphicsPath tag = Rounded(new RectangleF(100, 1258, 176, 60), 20))
            using (var violet = new SolidBrush(Violet)) g.FillPath(violet, tag);
            Text(g, "GAST", 32, FontStyle.Bold, Color.White, new RectangleF(100, 1258, 176, 60));
        }

        private static void Orb(Graphics g, OverlayState state, OverlayMotion m)
        {
            const float cx = 540, cy = 570;
            float core = (float)(150 + m.Level * 40);
            Glow(g, cx, cy, 300, Color.FromArgb(Alpha(0.55 * m.Glow + 0.15), Cyan));
            Glow(g, cx, cy, 240, Color.FromArgb(Alpha(0.35 * m.Glow + 0.08), Violet));
            Ring(g, cx, cy, 245, 6, m.Spin, 2.2, Color.FromArgb(217, Cyan));
            Ring(g, cx, cy, 245, 6, m.Spin + 3.3, 1.4, Color.FromArgb(217, Violet));
            Ring(g, cx, cy, 275, 3, -m.Spin * 0.7, 3.6, Color.FromArgb(140, Violet));
            Ring(g, cx, cy, 212, 3, -m.Spin * 1.3 + 1, 2.6, Color.FromArgb(128, Cyan));
            using (var body = new GraphicsPath())
            {
                body.AddEllipse(cx - core, cy - core, core * 2, core * 2);
                using (var brush = new PathGradientBrush(body))
                {
                    brush.CenterPoint = new PointF(cx - 30, cy - 40);
                    brush.InterpolationColors = new ColorBlend
                    {
                        Colors = new[] { Color.FromArgb(26, 16, 64), Color.FromArgb(106, 56, 216), Color.FromArgb(95, 233, 255), Color.FromArgb(233, 253, 255) },
                        Positions = new[] { 0f, 0.2f, 0.65f, 1f }
                    };
                    g.FillPath(brush, body);
                }
            }
            // Abstract face: round eyes (blink now and then) and a mouth that opens with NOVA's real output level.
            float open = (m.Frame % 140) < 4 ? 0.2f : 1f;
            using (var face = new SolidBrush(Color.FromArgb(199, 5, 6, 15)))
            {
                Oval(g, face, cx - 38, cy - 25, 18, 18 * open);
                Oval(g, face, cx + 38, cy - 25, 18, 18 * open);
                Oval(g, face, cx, cy + 40, 46, (float)(5 + m.Level * 26));
            }
            if (m.Listening(state))
                using (var pen = new Pen(Color.FromArgb(Alpha(0.25 + 0.2 * Math.Sin(m.Spin * 8)), Color.White), 4))
                    g.DrawEllipse(pen, cx - core - 18, cy - core - 18, (core + 18) * 2, (core + 18) * 2);
        }

        private static int Alpha(double value) { return Math.Max(0, Math.Min(255, (int)(value * 255))); }

        private static void Glow(Graphics g, float cx, float cy, float radius, Color center)
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(cx - radius, cy - radius, radius * 2, radius * 2);
                using (var brush = new PathGradientBrush(path) { CenterColor = center, SurroundColors = new[] { Color.FromArgb(0, center) } })
                    g.FillPath(brush, path);
            }
        }

        private static void Ring(Graphics g, float cx, float cy, float radius, float width, double start, double length, Color color)
        {
            using (var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(pen, cx - radius, cy - radius, radius * 2, radius * 2, (float)(start * 180 / Math.PI), (float)(length * 180 / Math.PI));
        }

        private static void Oval(Graphics g, Brush brush, float cx, float cy, float rx, float ry)
        {
            ry = Math.Max(1, ry);
            g.FillEllipse(brush, cx - rx, cy - ry, rx * 2, ry * 2);
        }

        private static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // Centered text; long text (names, topics) shrinks instead of being cut off.
        private static void Text(Graphics g, string text, float size, FontStyle style, Color color, RectangleF box)
        {
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord })
            using (var brush = new SolidBrush(color))
            using (Font font = Fit(g, text, size, style, box, format))
                g.DrawString(text, font, brush, box, format);
        }

        private static void GlowText(Graphics g, string text, float size, FontStyle style, Color color, RectangleF box)
        {
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (Font font = Fit(g, text, size, style, box, format))
            {
                using (var glow = new SolidBrush(Color.FromArgb(70, Violet)))
                    foreach (var offset in new[] { new PointF(-4, 0), new PointF(4, 0), new PointF(0, -4), new PointF(0, 4) })
                        g.DrawString(text, font, glow, new RectangleF(box.X + offset.X, box.Y + offset.Y, box.Width, box.Height), format);
                using (var brush = new SolidBrush(color)) g.DrawString(text, font, brush, box, format);
            }
        }

        private static Font Fit(Graphics g, string text, float size, FontStyle style, RectangleF box, StringFormat format)
        {
            for (float current = size; ; current -= 2)
            {
                var font = new Font("Segoe UI", current, style, GraphicsUnit.Pixel);
                SizeF needed = g.MeasureString(text, font, new SizeF(box.Width, 10000), format);
                if (current <= 18 || (needed.Width <= box.Width - 20 && needed.Height <= box.Height + 2)) return font;
                font.Dispose();
            }
        }
    }

    // Borderless 9:16 window for TikTok LIVE Studio ("Quelle hinzufügen → Fenster").
    // Must stay open and not minimized; it may lie behind other windows.
    internal sealed class OverlayWindow : Form
    {
        public const string WindowTitle = "NOVA LIVE (für TikTok)";
        private readonly OverlayMotion motion = new OverlayMotion();
        private readonly Timer timer = new Timer { Interval = 33 };
        private OverlayState state = new OverlayState();

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr first, IntPtr second);

        public OverlayWindow(Func<OverlayState> source)
        {
            Text = WindowTitle;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(5, 6, 15);
            MinimizeBox = false;
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            int height = Math.Max(480, Math.Min(960, area.Height - 40));
            ClientSize = new Size(height * 9 / 16, height);
            Location = new Point(Math.Max(area.Left, area.Right - ClientSize.Width - 20), area.Top + 20);

            var menu = new ContextMenuStrip();
            foreach (int size in new[] { 960, 1280, 1920 })
            {
                int h = size;
                menu.Items.Add("Größe " + (h * 9 / 16) + " × " + h, null, delegate { ClientSize = new Size(h * 9 / 16, h); });
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Fenster schließen", null, delegate { Close(); });
            ContextMenuStrip = menu;

            MouseDown += (sender, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                ReleaseCapture();
                SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero); // drag the borderless window
            };
            timer.Tick += delegate
            {
                OverlayState next = source == null ? null : source();
                if (next != null) state = next;
                motion.Step(state);
                Invalidate();
            };
            timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            OverlayPainter.Paint(e.Graphics, ClientSize, state, motion);
        }

        // A minimized window delivers no picture to LIVE Studio.
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Stop(); timer.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
