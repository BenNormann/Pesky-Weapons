using UnityEngine;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    /// <summary>
    /// The HUD compass, painted with Painter2D: a thin ring and one or two SPIKES from the middle.
    ///
    /// GREEN is the doorway to take. Every player has it, and a player whose compass a Mage bent has a
    /// green spike too - it simply points the wrong way, and nothing on this machine can tell.
    /// RED is drawn on a Mage's machine alone and points at the Resurrection Room.
    /// A spike SPINS while the player is inside that spike's target room.
    ///
    /// The GREEN spike is drawn SMALLER than the red one (shorter and thinner) and LAST, so it sits on
    /// top: when a Mage's two readings point the same way he can still see both. All five numbers are
    /// USS custom properties on the dial (`--spike-length`, `--spike-width`, `--spike-tail`,
    /// `--green-length-scale`, `--green-width-scale`) and live in Assets/UI/Labyrinth.uss.
    ///
    /// There is nothing else on the compass: no letters, no room name, no distance.
    /// </summary>
    internal sealed class CompassView : System.IDisposable
    {
        static readonly Color Ring = new Color(0.36f, 0.38f, 0.45f, 0.9f);
        static readonly Color Ground = new Color(0.055f, 0.063f, 0.078f, 0.72f);
        static readonly Color Green = new Color(0.36f, 0.86f, 0.45f);
        static readonly Color Red = new Color(0.92f, 0.32f, 0.28f);

        // Every number the spikes are drawn from is a USS custom property on the dial element, so the
        // shapes can be retuned in Labyrinth.uss without touching this file. The values here are only
        // the fallbacks used when the stylesheet says nothing. Lengths and widths are FRACTIONS OF THE
        // DIAL RADIUS; the two green scales are fractions of the red spike.
        static readonly CustomStyleProperty<float> SpikeLengthProp = new CustomStyleProperty<float>("--spike-length");
        static readonly CustomStyleProperty<float> SpikeWidthProp = new CustomStyleProperty<float>("--spike-width");
        static readonly CustomStyleProperty<float> SpikeTailProp = new CustomStyleProperty<float>("--spike-tail");
        static readonly CustomStyleProperty<float> GreenLengthScaleProp = new CustomStyleProperty<float>("--green-length-scale");
        static readonly CustomStyleProperty<float> GreenWidthScaleProp = new CustomStyleProperty<float>("--green-width-scale");

        readonly VisualElement _dial;
        readonly bool _exitStyle;
        bool _arrived;
        float _needleOpacity = 1f;

        float _spikeLength = 0.92f;      // red spike tip, as a fraction of the radius
        float _spikeWidth = 0.16f;       // red spike half-width at its base, fraction of the radius
        float _spikeTail = 0.12f;        // how far the base sits BEHIND the centre, fraction of the radius
        float _greenLengthScale = 0.7f;  // the green spike is this much of the red one's length
        float _greenWidthScale = 0.6f;   // ... and this much of its width

        bool _green, _red;
        float _greenAngle, _redAngle;

        public CompassView(VisualElement dial, bool exitStyle = false)
        {
            _dial = dial;
            _exitStyle = exitStyle;
            if (_dial == null) return;
            _dial.pickingMode = PickingMode.Ignore;
            _dial.generateVisualContent += OnDraw;
            _dial.RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyle);
        }

        /// <summary>Angles are screen degrees: 0 points up the screen, positive turns clockwise.</summary>
        public void Set(bool green, float greenAngle, bool red, float redAngle)
        {
            _green = green;
            _greenAngle = greenAngle;
            _red = red;
            _redAngle = redAngle;
            if (_dial != null) _dial.MarkDirtyRepaint();
        }

        public void SetExit(bool visible, float angle, bool arrived, float opacity = 1f)
        {
            if (_green == visible && _arrived == arrived && Mathf.Abs(Mathf.DeltaAngle(_greenAngle, angle)) < 0.02f
                && Mathf.Abs(_needleOpacity - opacity) < 0.002f) return;
            _green = visible;
            _greenAngle = angle;
            _arrived = arrived;
            _needleOpacity = opacity;
            if (_dial != null) _dial.MarkDirtyRepaint();
        }

        public void Dispose()
        {
            if (_dial == null) return;
            _dial.generateVisualContent -= OnDraw;
            _dial.UnregisterCallback<CustomStyleResolvedEvent>(OnCustomStyle);
        }

        /// <summary>Pick the spike shape up from USS. Anything the stylesheet leaves out keeps its fallback.</summary>
        void OnCustomStyle(CustomStyleResolvedEvent e)
        {
            ICustomStyle s = e.customStyle;
            float v;
            if (s.TryGetValue(SpikeLengthProp, out v)) _spikeLength = Mathf.Clamp(v, 0.05f, 1f);
            if (s.TryGetValue(SpikeWidthProp, out v)) _spikeWidth = Mathf.Clamp(v, 0.01f, 1f);
            if (s.TryGetValue(SpikeTailProp, out v)) _spikeTail = Mathf.Clamp(v, 0f, 0.9f);
            if (s.TryGetValue(GreenLengthScaleProp, out v)) _greenLengthScale = Mathf.Clamp(v, 0.05f, 2f);
            if (s.TryGetValue(GreenWidthScaleProp, out v)) _greenWidthScale = Mathf.Clamp(v, 0.05f, 2f);
            if (_dial != null) _dial.MarkDirtyRepaint();
        }

        void OnDraw(MeshGenerationContext ctx)
        {
            VisualElement element = ctx.visualElement;
            if (element == null) return;
            Rect rect = element.contentRect;
            if (rect.width < 12f || rect.height < 12f) return;

            Vector2 centre = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f - 3f;
            if (radius <= 2f) return;

            Painter2D painter = ctx.painter2D;

            if (_exitStyle)
            {
                DrawExit(painter, centre, radius);
                return;
            }

            painter.fillColor = Ground;
            painter.BeginPath();
            painter.Arc(centre, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            painter.Fill();

            painter.lineWidth = 2f;
            painter.strokeColor = Ring;
            painter.BeginPath();
            painter.Arc(centre, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            painter.Stroke();

            // Red first and at full size, green second and SMALLER, so that when a Mage's two readings
            // line up the green one sits on top of the red and both are still visible.
            if (_red) Spike(painter, centre, radius, _redAngle, Red, 1f, 1f);
            if (_green) Spike(painter, centre, radius, _greenAngle, Green, _greenLengthScale, _greenWidthScale);
        }

        void DrawExit(Painter2D p, Vector2 c, float r)
        {
            Color rim = new Color(0.72f, 0.74f, 0.78f, 0.8f);
            Color tipColor = new Color(0.93f, 0.93f, 0.92f, _needleOpacity);
            p.fillColor = new Color(0.07f, 0.08f, 0.1f, 0.85f);
            Circle(p, c, r, true);
            p.strokeColor = rim;
            p.lineWidth = 1.3f;
            Circle(p, c, r - 1f, false);
            p.strokeColor = new Color(0.35f, 0.42f, 0.47f, 0.5f);
            Circle(p, c, r * 0.68f, false);
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f * Mathf.Deg2Rad;
                Vector2 n = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                bool major = i % 6 == 0;
                p.strokeColor = major ? rim : new Color(0.39f, 0.45f, 0.49f, 0.65f);
                p.lineWidth = major ? 2f : 1f;
                p.BeginPath();
                p.MoveTo(c + n * (r - 6f));
                p.LineTo(c + n * (r - (major ? 14f : 10f)));
                p.Stroke();
            }

            p.fillColor = new Color(0.87f, 0.89f, 0.91f);
            Triangle(p, c + new Vector2(0f, -r + 3f), c + new Vector2(-4f, -r - 2f), c + new Vector2(4f, -r - 2f));
            if (_arrived)
            {
                p.strokeColor = new Color(0.6f, 0.94f, 0.81f);
                p.lineWidth = 3f;
                p.BeginPath();
                p.MoveTo(c + new Vector2(-13f, 0f));
                p.LineTo(c + new Vector2(-3f, 10f));
                p.LineTo(c + new Vector2(17f, -12f));
                p.Stroke();
                return;
            }
            if (_green)
            {
                float a = _greenAngle * Mathf.Deg2Rad;
                Vector2 n = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                Vector2 side = new Vector2(-n.y, n.x);
                Vector2 tip = c + n * r * 0.76f;
                Vector2 tail = c - n * r * 0.47f;
                Vector2 left = c + side * r * 0.145f;
                Vector2 right = c - side * r * 0.145f;
                p.fillColor = new Color(0.3f, 0.32f, 0.36f, _needleOpacity);
                Triangle(p, left, right, tail);
                p.fillColor = tipColor;
                Triangle(p, tip, left, c);
                p.fillColor = new Color(0.62f, 0.65f, 0.7f, _needleOpacity);
                Triangle(p, tip, c, right);
            }
            p.fillColor = rim;
            Circle(p, c, 3.5f, true);
            p.fillColor = new Color(0.06f, 0.09f, 0.12f);
            Circle(p, c, 1.5f, true);
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

        /// <summary>One spike: a narrow triangle from the middle of the dial out towards the ring.</summary>
        void Spike(Painter2D painter, Vector2 centre, float radius, float degrees, Color colour,
                   float lengthScale, float widthScale)
        {
            float a = degrees * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            Vector2 side = new Vector2(-forward.y, forward.x);

            float reach = radius * _spikeLength * lengthScale;
            float half = Mathf.Max(2f, radius * _spikeWidth * widthScale);
            float tail = radius * _spikeTail * widthScale;

            Vector2 tip = centre + forward * reach;
            Vector2 left = centre + side * half - forward * tail;
            Vector2 right = centre - side * half - forward * tail;

            painter.fillColor = colour;
            painter.BeginPath();
            painter.MoveTo(tip);
            painter.LineTo(left);
            painter.LineTo(right);
            painter.ClosePath();
            painter.Fill();
        }
    }
}
