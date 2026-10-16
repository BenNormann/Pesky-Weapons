using UnityEngine;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    internal sealed class ExitCompassView : System.IDisposable
    {
        readonly VisualElement _dial;
        bool _visible, _arrived;
        float _angle, _opacity = 1f;

        public ExitCompassView(VisualElement dial)
        {
            _dial = dial;
            if (_dial != null) _dial.generateVisualContent += OnDraw;
        }

        public void SetExit(bool visible, float angle, bool arrived, float opacity = 1f)
        {
            if (_visible == visible && _arrived == arrived && Mathf.Abs(Mathf.DeltaAngle(_angle, angle)) < 0.02f
                && Mathf.Abs(_opacity - opacity) < 0.002f) return;
            _visible = visible;
            _angle = angle;
            _arrived = arrived;
            _opacity = opacity;
            if (_dial != null) _dial.MarkDirtyRepaint();
        }

        public void Dispose()
        {
            if (_dial != null) _dial.generateVisualContent -= OnDraw;
        }

        void OnDraw(MeshGenerationContext ctx)
        {
            Rect rect = ctx.visualElement.contentRect;
            float r = Mathf.Min(rect.width, rect.height) * 0.5f - 3f;
            if (r <= 2f) return;
            Vector2 c = rect.center;
            Painter2D p = ctx.painter2D;
            Color ink = ctx.visualElement.resolvedStyle.color;
            p.strokeColor = Tint(ink, 0.6f);
            p.lineWidth = 1.3f;
            Circle(p, c, r - 1f, false);
            p.strokeColor = Tint(ink, 0.2f);
            Circle(p, c, r * 0.68f, false);
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f * Mathf.Deg2Rad;
                Vector2 n = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                bool major = i % 6 == 0;
                p.strokeColor = Tint(ink, major ? 0.6f : 0.3f);
                p.lineWidth = major ? 2f : 1f;
                p.BeginPath();
                p.MoveTo(c + n * (r - 6f));
                p.LineTo(c + n * (r - (major ? 14f : 10f)));
                p.Stroke();
            }

            p.fillColor = ink;
            Triangle(p, c + new Vector2(0f, -r + 3f), c + new Vector2(-4f, -r - 2f), c + new Vector2(4f, -r - 2f));
            if (_arrived)
            {
                p.strokeColor = ink;
                p.lineWidth = 3f;
                p.BeginPath();
                p.MoveTo(c + new Vector2(-13f, 0f));
                p.LineTo(c + new Vector2(-3f, 10f));
                p.LineTo(c + new Vector2(17f, -12f));
                p.Stroke();
                return;
            }
            if (_visible)
            {
                float a = _angle * Mathf.Deg2Rad;
                Vector2 n = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                Vector2 side = new Vector2(-n.y, n.x);
                Vector2 tip = c + n * r * 0.76f;
                Vector2 tail = c - n * r * 0.47f;
                Vector2 left = c + side * r * 0.145f;
                Vector2 right = c - side * r * 0.145f;
                p.fillColor = Tint(ink, 0.25f * _opacity);
                Triangle(p, left, right, tail);
                p.fillColor = Tint(ink, _opacity);
                Triangle(p, tip, left, c);
                p.fillColor = Tint(ink, 0.55f * _opacity);
                Triangle(p, tip, c, right);
            }
            p.fillColor = ink;
            Circle(p, c, 3.5f, true);
        }

        static Color Tint(Color ink, float alpha)
        {
            ink.a *= alpha;
            return ink;
        }

        static void Circle(Painter2D p, Vector2 c, float radius, bool fill)
        {
            p.BeginPath();
            p.Arc(c, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            if (fill) p.Fill(); else p.Stroke();
        }

        static void Triangle(Painter2D p, Vector2 a, Vector2 b, Vector2 c)
        {
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.LineTo(c);
            p.ClosePath();
            p.Fill();
        }
    }
}
