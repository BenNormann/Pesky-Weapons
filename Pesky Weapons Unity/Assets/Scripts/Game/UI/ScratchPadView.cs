using System;
using System.Collections.Generic;
using Pesky.Protocol;
using Pesky.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    /// <summary>
    /// The PAD page of the Tab overlay: the team's shared scratch pad, a plain dark canvas the crew draws
    /// its own map on. Left mouse draws in the player's own slot colour, right mouse (or the ERASER button)
    /// rubs out, and the host alone has a CLEAR button.
    ///
    /// NO LABYRINTH INFORMATION IS DRAWN HERE. The pad is blank until somebody draws on it: that is the
    /// point. Whatever the crew knows about the maze, they have to put on it themselves.
    ///
    /// IT IS A VIEW. The strokes it paints are the sim's shared list, which every peer holds in the same
    /// order, plus this player's own PENDING strokes - the ones they just drew, painted at once so drawing
    /// feels instant, and handed over to the sim's copy when the host's echo lands. A pending stroke the
    /// host never accepts (a rate cap, a round change) simply fades out after
    /// <see cref="PendingSeconds"/>, which is the only feedback a refusal ever gives.
    ///
    /// An ERASER is not a delete: it is a stroke painted in the canvas colour on top of what is there, so
    /// replaying the same list in the same order gives every peer the same picture.
    /// </summary>
    internal sealed class ScratchPadView
    {
        /// <summary>The canvas colour. The eraser paints in exactly this, so the two can never drift apart.</summary>
        public static readonly Color Canvas = new Color(0.051f, 0.059f, 0.075f, 1f);

        /// <summary>How long an unconfirmed stroke of this player's own is drawn before it is given up on.</summary>
        public const float PendingSeconds = 3f;

        /// <summary>Pen widths as a fraction of the canvas width, by width class.</summary>
        static readonly float[] PenWidths = { 0.004f, 0.008f, 0.016f };
        static readonly float[] EraserWidths = { 0.030f, 0.055f, 0.090f };

        /// <summary>A new point is only kept once the pointer has moved this far in normalised units.</summary>
        const float MinStep = 0.004f;

        sealed class Pending
        {
            public List<Vector2> points = new List<Vector2>(PadStrokeReqMsg.MaxPoints);
            public bool erase;
            public byte width;
            public float sentAt;
        }

        /// <summary>Ask the host for a stroke: normalised u16 x,y pairs, the eraser flag, the width class.</summary>
        public Action<ushort[], bool, byte> SendStroke;

        /// <summary>The host's CLEAR button was pressed.</summary>
        public Action ClearRequested;

        readonly VisualElement _page;
        readonly VisualElement _canvas;
        readonly Button _pen;
        readonly Button _eraser;
        readonly Button _clear;
        readonly Label _hint;
        readonly VisualElement[] _widthButtons;

        ScratchPadState _pad;
        uint _seenRev;
        bool _revKnown;
        byte _localSlot = Wire.NoSlot;

        readonly List<Pending> _pending = new List<Pending>(8);
        readonly List<Vector2> _live = new List<Vector2>(PadStrokeReqMsg.MaxPoints);
        bool _drawing;
        bool _liveErase;
        int _pointer = -1;
        bool _eraseMode;
        byte _width = 1;

        public ScratchPadView(VisualElement page)
        {
            _page = page;
            _canvas = page.Q<VisualElement>("pad-canvas");
            _pen = page.Q<Button>("pad-pen");
            _eraser = page.Q<Button>("pad-eraser");
            _clear = page.Q<Button>("pad-clear");
            _hint = page.Q<Label>("pad-hint");
            _widthButtons = new VisualElement[3];
            _widthButtons[0] = page.Q<Button>("pad-thin");
            _widthButtons[1] = page.Q<Button>("pad-medium");
            _widthButtons[2] = page.Q<Button>("pad-thick");

            if (_canvas != null)
            {
                _canvas.style.backgroundColor = Canvas;
                _canvas.generateVisualContent += OnDraw;
                _canvas.RegisterCallback<PointerDownEvent>(OnPointerDown);
                _canvas.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                _canvas.RegisterCallback<PointerUpEvent>(OnPointerUp);
                _canvas.RegisterCallback<PointerCaptureOutEvent>(OnCaptureLost);
            }

            if (_pen != null) _pen.clicked += delegate { _eraseMode = false; RefreshTools(); };
            if (_eraser != null) _eraser.clicked += delegate { _eraseMode = true; RefreshTools(); };
            if (_clear != null) _clear.clicked += delegate { if (ClearRequested != null) ClearRequested(); };
            for (byte i = 0; i < _widthButtons.Length; i++)
            {
                Button b = _widthButtons[i] as Button;
                if (b == null) continue;
                byte pick = i;
                b.clicked += delegate { _width = pick; RefreshTools(); };
            }
            RefreshTools();
        }

        // ---------------------------------------------------------------- the frame loop

        /// <summary>Called once a frame while the pad page is showing.</summary>
        public void Refresh(ScratchPadState pad, byte localSlot, bool isHost)
        {
            _pad = pad;
            _localSlot = localSlot;
            if (_clear != null) _clear.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;

            bool dirty = false;
            if (pad != null && (!_revKnown || pad.Rev != _seenRev))
            {
                _revKnown = true;
                _seenRev = pad.Rev;
                dirty = true;
            }
            if (ExpirePending()) dirty = true;
            if (dirty && _canvas != null) _canvas.MarkDirtyRepaint();
        }

        /// <summary>
        /// A stroke of this player's own came back from the host and is now in the sim's list, so the
        /// oldest optimistic copy can go. They are confirmed in the order they were sent.
        /// </summary>
        public void NoteOwnStrokeConfirmed()
        {
            if (_pending.Count == 0) return;
            _pending.RemoveAt(0);
            if (_canvas != null) _canvas.MarkDirtyRepaint();
        }

        /// <summary>The pad was wiped: nothing of this player's is waiting any more either.</summary>
        public void Wiped()
        {
            _pending.Clear();
            _live.Clear();
            _drawing = false;
            if (_canvas != null) _canvas.MarkDirtyRepaint();
        }

        /// <summary>Drop a half-drawn stroke: the overlay closed, or the round ended.</summary>
        public void EndStroke()
        {
            if (_drawing) Finish();
            _drawing = false;
            _pointer = -1;
        }

        bool ExpirePending()
        {
            bool changed = false;
            float now = Time.unscaledTime;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (now < _pending[i].sentAt + PendingSeconds) continue;
                _pending.RemoveAt(i);
                changed = true;
            }
            return changed;
        }

        void RefreshTools()
        {
            if (_pen != null) _pen.EnableInClassList("is-selected", !_eraseMode);
            if (_eraser != null) _eraser.EnableInClassList("is-selected", _eraseMode);
            for (int i = 0; i < _widthButtons.Length; i++)
                if (_widthButtons[i] != null) _widthButtons[i].EnableInClassList("is-selected", i == _width);
            if (_hint != null)
                _hint.text = "left mouse draws   -   right mouse erases   -   everybody on the team sees this";
        }

        // ---------------------------------------------------------------- pointer

        bool TryNormalise(Vector3 panelPosition, out Vector2 normalised)
        {
            normalised = Vector2.zero;
            if (_canvas == null) return false;
            Rect rect = _canvas.contentRect;
            if (rect.width < 1f || rect.height < 1f) return false;
            Vector2 local = _canvas.WorldToLocal(new Vector2(panelPosition.x, panelPosition.y));
            normalised = new Vector2(Mathf.Clamp01(local.x / rect.width), Mathf.Clamp01(local.y / rect.height));
            return true;
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            Vector2 p;
            if (_drawing || !TryNormalise(evt.position, out p)) return;
            _drawing = true;
            // Right mouse always erases, whatever the toolbar says; left mouse follows the toolbar.
            _liveErase = _eraseMode || evt.button == 1;
            _pointer = evt.pointerId;
            _live.Clear();
            _live.Add(p);
            _canvas.CapturePointer(evt.pointerId);
            _canvas.MarkDirtyRepaint();
            evt.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            Vector2 p;
            if (!_drawing || evt.pointerId != _pointer || !TryNormalise(evt.position, out p)) return;
            if (_live.Count > 0 && (p - _live[_live.Count - 1]).sqrMagnitude < MinStep * MinStep) return;
            _live.Add(p);
            // A long stroke is split: send the full message and carry the joining point into the next one,
            // so the line has no gap in it.
            if (_live.Count >= PadStrokeReqMsg.MaxPoints)
            {
                Vector2 last = _live[_live.Count - 1];
                Flush();
                _live.Add(last);
            }
            _canvas.MarkDirtyRepaint();
            evt.StopPropagation();
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            if (!_drawing || evt.pointerId != _pointer) return;
            if (_canvas.HasPointerCapture(evt.pointerId)) _canvas.ReleasePointer(evt.pointerId);
            Finish();
            evt.StopPropagation();
        }

        void OnCaptureLost(PointerCaptureOutEvent evt)
        {
            if (!_drawing) return;
            Finish();
        }

        void Finish()
        {
            Flush();
            if (_canvas != null && _pointer >= 0 && _canvas.HasPointerCapture(_pointer))
                _canvas.ReleasePointer(_pointer);
            _drawing = false;
            _pointer = -1;
            if (_canvas != null) _canvas.MarkDirtyRepaint();
        }

        /// <summary>Hands the points drawn so far to the host, and keeps an optimistic copy to paint meanwhile.</summary>
        void Flush()
        {
            if (_live.Count == 0) return;
            int n = _live.Count;
            if (n > PadStrokeReqMsg.MaxPoints) n = PadStrokeReqMsg.MaxPoints;

            Pending pending = new Pending();
            pending.erase = _liveErase;
            pending.width = _width;
            pending.sentAt = Time.unscaledTime;
            ushort[] points = new ushort[n * 2];
            for (int i = 0; i < n; i++)
            {
                pending.points.Add(_live[i]);
                points[i * 2] = (ushort)Mathf.RoundToInt(Mathf.Clamp01(_live[i].x) * 65535f);
                points[i * 2 + 1] = (ushort)Mathf.RoundToInt(Mathf.Clamp01(_live[i].y) * 65535f);
            }
            _live.Clear();

            // The optimistic copy goes in FIRST, because offline the host's echo comes back inside Send.
            _pending.Add(pending);
            if (SendStroke != null) SendStroke(points, pending.erase, pending.width);
        }

        // ---------------------------------------------------------------- painting

        void OnDraw(MeshGenerationContext ctx)
        {
            VisualElement element = ctx.visualElement;
            if (element == null) return;
            Rect rect = element.contentRect;
            if (rect.width < 4f || rect.height < 4f) return;

            Painter2D painter = ctx.painter2D;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            if (_pad != null)
            {
                IReadOnlyList<PadStroke> strokes = _pad.Strokes;
                for (int i = 0; i < strokes.Count; i++)
                {
                    PadStroke s = strokes[i];
                    int n = s.PointCount;
                    if (n <= 0) continue;
                    BeginStroke(painter, rect, s.Erase, s.width, s.slot);
                    for (int p = 0; p < n; p++)
                    {
                        Vector2 point = new Vector2(rect.width * (s.points[p * 2] / 65535f),
                                                    rect.height * (s.points[p * 2 + 1] / 65535f));
                        if (p == 0) painter.MoveTo(point); else painter.LineTo(point);
                    }
                    if (n == 1) painter.LineTo(new Vector2(rect.width * (s.points[0] / 65535f) + 0.5f,
                                                           rect.height * (s.points[1] / 65535f)));
                    painter.Stroke();
                }
            }

            for (int i = 0; i < _pending.Count; i++) DrawLocal(painter, rect, _pending[i].points, _pending[i].erase, _pending[i].width);
            if (_drawing) DrawLocal(painter, rect, _live, _liveErase, _width);
        }

        void DrawLocal(Painter2D painter, Rect rect, List<Vector2> points, bool erase, byte width)
        {
            if (points == null || points.Count == 0) return;
            BeginStroke(painter, rect, erase, width, _localSlot);
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 point = new Vector2(rect.width * points[i].x, rect.height * points[i].y);
                if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
            }
            if (points.Count == 1)
                painter.LineTo(new Vector2(rect.width * points[0].x + 0.5f, rect.height * points[0].y));
            painter.Stroke();
        }

        static void BeginStroke(Painter2D painter, Rect rect, bool erase, byte width, byte slot)
        {
            int w = width > 2 ? 2 : width;
            float fraction = erase ? EraserWidths[w] : PenWidths[w];
            painter.lineWidth = Mathf.Max(1.5f, fraction * rect.width);
            painter.strokeColor = erase ? Canvas : SlotColors.For(slot == Wire.NoSlot ? 0 : slot);
            painter.BeginPath();
        }
    }
}
