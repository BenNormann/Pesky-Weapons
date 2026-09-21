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
    internal sealed class CompassView
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

        float _spikeLength = 0.92f;      // red spike tip, as a fraction of the radius
        float _spikeWidth = 0.16f;       // red spike half-width at its base, fraction of the radius
        float _spikeTail = 0.12f;        // how far the base sits BEHIND the centre, fraction of the radius
        float _greenLengthScale = 0.7f;  // the green spike is this much of the red one's length
        float _greenWidthScale = 0.6f;   // ... and this much of its width

        bool _green, _red;
        float _greenAngle, _redAngle;

        public CompassView(VisualElement dial)
        {
            _dial = dial;
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
