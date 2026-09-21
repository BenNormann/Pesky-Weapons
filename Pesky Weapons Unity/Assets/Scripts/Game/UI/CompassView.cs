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
    /// There is nothing else on the compass: no letters, no room name, no distance.
    /// </summary>
    internal sealed class CompassView
    {
        static readonly Color Ring = new Color(0.36f, 0.38f, 0.45f, 0.9f);
        static readonly Color Ground = new Color(0.055f, 0.063f, 0.078f, 0.72f);
        static readonly Color Green = new Color(0.36f, 0.86f, 0.45f);
        static readonly Color Red = new Color(0.92f, 0.32f, 0.28f);

        readonly VisualElement _dial;

        bool _green, _red;
        float _greenAngle, _redAngle;

        public CompassView(VisualElement dial)
        {
            _dial = dial;
            if (_dial == null) return;
            _dial.pickingMode = PickingMode.Ignore;
            _dial.generateVisualContent += OnDraw;
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

            // Red first, so the green one is on top when the two happen to line up.
            if (_red) Spike(painter, centre, radius, _redAngle, Red);
            if (_green) Spike(painter, centre, radius, _greenAngle, Green);
        }

        /// <summary>One spike: a narrow triangle from the middle of the dial out to the ring.</summary>
        static void Spike(Painter2D painter, Vector2 centre, float radius, float degrees, Color colour)
        {
            float a = degrees * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            Vector2 side = new Vector2(-forward.y, forward.x);
            float half = Mathf.Max(3f, radius * 0.16f);

            Vector2 tip = centre + forward * (radius * 0.92f);
            Vector2 left = centre + side * half - forward * (radius * 0.12f);
            Vector2 right = centre - side * half - forward * (radius * 0.12f);

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
