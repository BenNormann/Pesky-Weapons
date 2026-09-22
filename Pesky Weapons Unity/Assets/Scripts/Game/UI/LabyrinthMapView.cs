using System;
using System.Text;
using Pesky.Data;
using Pesky.Protocol;
using Pesky.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    /// <summary>
    /// The map page of the labyrinth HUD: the grid drawn as tiles, and - for a Mage alone - the drag that
    /// asks the host to swap two rooms and the chips that ask it to bend a player's compass.
    ///
    /// It is a VIEW. It never touches the table: it draws what the sim says and raises a request. Nothing
    /// Mage-specific exists outside the overlay, and the overlay is hidden (display:none, so it cannot even
    /// be picked) whenever the map is closed.
    ///
    /// Cooldowns shown here are a LOCAL GUIDE, not the truth: the host owns the real ones and refuses in
    /// silence, so this ring is only ever as generous as the host.
    /// </summary>
    internal sealed class LabyrinthMapView
    {
        /// <summary>The Mage dropped a room on a neighbour: ask the host to exchange the two cells.</summary>
        public Action<int, int> SwapRequested;

        /// <summary>The Mage pointed a player's compass somewhere: slot mask, what kind of target, which cell.</summary>
        public Action<byte, CompassTargetKind, int> BendRequested;

        /// <summary>
        /// A stand-in crew member on the Mage bar, for the tutorial: somebody to practise bending when
        /// there is nobody else in the room. Empty in a real round. Nothing it does leaves this machine -
        /// no request is sent for it and no other peer knows it exists.
        /// </summary>
        public string PracticeName = "";

        /// <summary>The chip slot the stand-in uses: out of the eight real ones, so no player can share it.</summary>
        const int PracticeSlot = 8;

        /// <summary>The practice stand-in's compass as the Mage set it: bent at all, to what kind of target, to which cell. Read by the tutorial's PracticeDummy.</summary>
        public bool PracticeBent { get { return _practiceBent; } }
        public CompassTargetKind PracticeKind { get { return _practiceKind; } }
        public int PracticeCell { get { return _practiceCell; } }

        const float GhostHalf = 34f;

        readonly VisualElement _overlay;
        readonly VisualElement _gridRoot;
        readonly VisualElement _mageBar;
        readonly VisualElement _chipRow;
        readonly Label _hint;
        readonly Label _bentCount;
        readonly VisualElement _swapRing;
        readonly VisualElement _bendRing;
        readonly Label _swapRingText;
        readonly Label _bendRingText;
        readonly StringBuilder _sb = new StringBuilder(96);

        VisualElement[] _tiles = new VisualElement[0];
        Label[] _glyphs = new Label[0];
        Label[] _names = new Label[0];
        Label[] _marks = new Label[0];
        bool[] _visited = new bool[0];
        bool[] _fixedCell = new bool[0];
        VisualElement _ghost;
        Label _ghostLabel;

        LabyrinthGrid _grid;
        LabyrinthDef _def;
        int _width, _height;
        bool _mage;
        bool _canSwap;
        int _dragFrom = -1;
        int _dragPointer = -1;
        int _selectedSlot = -1;
        byte _bentMask;
        string _chipSignature = "";
        bool _practiceBent;
        CompassTargetKind _practiceKind = CompassTargetKind.GoodEnd;
        int _practiceCell = -1;
        float _swapFill, _bendFill;        readonly string[] _chipName = new string[9];
        readonly CompassTargetKind[] _slotKind = new CompassTargetKind[9];
        readonly int[] _slotCell = new int[9];
        int _nonMages;
        int _maxBent;
        int _bentNow;
        string _refusal = "";
        float _refusalUntil;


        public LabyrinthMapView(VisualElement overlay)
        {
            _overlay = overlay;
            _gridRoot = overlay.Q<VisualElement>("map-grid");
            _mageBar = overlay.Q<VisualElement>("mage-bar");
            _chipRow = overlay.Q<VisualElement>("player-chips");
            _hint = overlay.Q<Label>("mage-hint");
            _bentCount = overlay.Q<Label>("bent-count");
            _swapRing = overlay.Q<VisualElement>("swap-ring");
            _bendRing = overlay.Q<VisualElement>("bend-ring");
            _swapRingText = overlay.Q<Label>("swap-ring-text");
            _bendRingText = overlay.Q<Label>("bend-ring-text");

            Button bad = overlay.Q<Button>("bend-badend");
            if (bad != null) bad.clicked += delegate { Bend(CompassTargetKind.BadEnd, 0); };
            Button clear = overlay.Q<Button>("bend-clear");
            if (clear != null) clear.clicked += delegate { Bend(CompassTargetKind.GoodEnd, 0); };

            if (_swapRing != null)
            {
                _swapRing.generateVisualContent += OnDrawSwapRing;
                _swapRing.pickingMode = PickingMode.Ignore;
            }
            if (_bendRing != null)
            {
                _bendRing.generateVisualContent += OnDrawBendRing;
                _bendRing.pickingMode = PickingMode.Ignore;
            }
        }

        // ---------------------------------------------------------------- building

        void Build(int width, int height)
        {
            _width = width;
            _height = height;
            int count = width * height;

            if (_gridRoot == null) return;
            _gridRoot.Clear();
            _tiles = new VisualElement[count];
            _glyphs = new Label[count];
            _names = new Label[count];
            _marks = new Label[count];
            _fixedCell = new bool[count];
            if (_visited.Length != count) _visited = new bool[count];

            for (int row = 0; row < height; row++)
            {
                VisualElement rowElement = new VisualElement();
                rowElement.AddToClassList("map-row");
                _gridRoot.Add(rowElement);
                for (int column = 0; column < width; column++)
                {
                    int cell = row * width + column;
                    VisualElement tile = new VisualElement();
                    tile.AddToClassList("map-tile");
                    tile.userData = cell;

                    Label mark = new Label(string.Empty);
                    mark.AddToClassList("map-mark");
                    mark.pickingMode = PickingMode.Ignore;
                    Label glyph = new Label(string.Empty);
                    glyph.AddToClassList("map-glyph");
                    glyph.pickingMode = PickingMode.Ignore;
                    Label name = new Label(string.Empty);
                    name.AddToClassList("map-name");
                    name.pickingMode = PickingMode.Ignore;

                    tile.Add(mark);
                    tile.Add(glyph);
                    tile.Add(name);
                    tile.RegisterCallback<PointerDownEvent>(OnTilePointerDown);
                    tile.RegisterCallback<PointerMoveEvent>(OnTilePointerMove);
                    tile.RegisterCallback<PointerUpEvent>(OnTilePointerUp);

                    rowElement.Add(tile);
                    _tiles[cell] = tile;
                    _glyphs[cell] = glyph;
                    _names[cell] = name;
                    _marks[cell] = mark;
                }
            }

            if (_ghost == null)
            {
                _ghost = new VisualElement();
                _ghost.AddToClassList("map-ghost");
                _ghost.pickingMode = PickingMode.Ignore;
                _ghostLabel = new Label(string.Empty);
                _ghostLabel.AddToClassList("map-glyph");
                _ghostLabel.pickingMode = PickingMode.Ignore;
                _ghost.Add(_ghostLabel);
                _overlay.Add(_ghost);
            }
            _ghost.style.display = DisplayStyle.None;
        }

        // ---------------------------------------------------------------- drawing

        /// <summary>Redraw the whole page. Called once a frame while the map is open.</summary>
        public void Refresh(LabyrinthGrid grid, LabyrinthDef def, int localCell, PlayerTable players,
                            byte localSlot, bool mage, bool canSwap)
        {
            _grid = grid;
            _def = def;
            _mage = mage;
            _canSwap = canSwap && mage;

            if (grid == null)
            {
                if (_gridRoot != null && _tiles.Length == 0) return;
                for (int i = 0; i < _tiles.Length; i++)
                {
                    if (_glyphs[i] != null) _glyphs[i].text = "?";
                    if (_names[i] != null) _names[i].text = string.Empty;
                }
                ShowMageBar(false);
                return;
            }

            if (_width != grid.Width || _height != grid.Height || _tiles.Length != grid.CellCount)
                Build(grid.Width, grid.Height);

            if (localCell >= 0 && localCell < _visited.Length) _visited[localCell] = true;

            for (int cell = 0; cell < _tiles.Length; cell++)
            {
                VisualElement tile = _tiles[cell];
                if (tile == null) continue;

                int roomId = grid.RoomAt(cell);
                bool fixedCell = grid.IsFixedCell(cell);
                _fixedCell[cell] = fixedCell;

                if (_glyphs[cell] != null)
                    _glyphs[cell].text = def != null ? def.GlyphOf(roomId) : roomId.ToString();
                if (_names[cell] != null)
                    _names[cell].text = def != null ? def.LabelOf(roomId) : "Room " + roomId;

                bool here = cell == localCell;
                bool start = cell == grid.StartCell;
                bool bad = cell == grid.BadEndCell;
                bool good = cell == grid.GoodEndCell;

                if (_marks[cell] != null)
                {
                    string mark = here ? "YOU" : good ? "EXIT " + DirLetter((int)grid.ExitDir) : start ? "START" : string.Empty;
                    if (_marks[cell].text != mark) _marks[cell].text = mark;
                }

                tile.EnableInClassList("is-here", here);
                tile.EnableInClassList("is-visited", cell < _visited.Length && _visited[cell]);
                tile.EnableInClassList("is-start", start);
                tile.EnableInClassList("is-bad", bad);
                tile.EnableInClassList("is-good", good);
                tile.EnableInClassList("is-fixed", fixedCell);
                tile.EnableInClassList("is-drag", _dragFrom == cell);

                bool drop = _dragFrom >= 0 && cell != _dragFrom &&
                            grid.CanSwap(_dragFrom, cell, def != null && def.swapAcrossWrap);
                tile.EnableInClassList("is-drop", drop);
                tile.EnableInClassList("is-pick", _selectedSlot >= 0 && !fixedCell);
            }

            ShowMageBar(mage);
            if (!mage) return;

            RefreshChips(players, localSlot);
            RefreshHint(players, localSlot);
        }

        void ShowMageBar(bool on)
        {
            if (_mageBar == null) return;
            _mageBar.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static string DirLetter(int dir)
        {
            if (dir == (int)Heading.North) return "N";
            if (dir == (int)Heading.East) return "E";
            if (dir == (int)Heading.South) return "S";
            if (dir == (int)Heading.West) return "W";
            return "?";
        }

        // ---------------------------------------------------------------- the Mage's chips

        void RefreshChips(PlayerTable players, byte localSlot)
        {
            if (_chipRow == null || players == null) return;

            _sb.Length = 0;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerState p = players[i];
                if (p == null || !p.present) continue;
                _sb.Append(i).Append(':').Append(p.name).Append('|');
            }
            _sb.Append(PracticeName);
            string signature = _sb.ToString();
            if (signature != _chipSignature)
            {
                _chipSignature = signature;
                _chipRow.Clear();
                for (int i = 0; i < players.Count; i++)
                {
                    PlayerState p = players[i];
                    if (p == null || !p.present || i == localSlot) continue;
                    AddChip(i, string.IsNullOrEmpty(p.name) ? ("P" + i) : p.name);
                }
                if (!string.IsNullOrEmpty(PracticeName)) AddChip(PracticeSlot, PracticeName);
            }

            for (int i = 0; i < _chipRow.childCount; i++)
            {
                VisualElement chip = _chipRow.ElementAt(i);
                int slot = chip.userData is int ? (int)chip.userData : -1;
                chip.EnableInClassList("is-selected", slot >= 0 && slot == _selectedSlot);
                bool bent = slot == PracticeSlot
                    ? _practiceBent
                    : slot >= 0 && slot < 8 && (_bentMask & (1 << slot)) != 0;
                chip.EnableInClassList("is-bent", bent);
                // The Mage's own record of where he sent each compass, so the lie is visible to its author.
                if (slot >= 0 && slot < _chipName.Length)
                {
                    Button b = chip as Button;
                    string name = _chipName[slot];
                    if (b != null && !string.IsNullOrEmpty(name))
                    {
                        CompassTargetKind k = slot == PracticeSlot ? _practiceKind : _slotKind[slot];
                        int c = slot == PracticeSlot ? _practiceCell : _slotCell[slot];
                        b.text = bent ? name + "  >  " + TargetLabel(k, c) : name;
                    }
                }
            }
        }

        void AddChip(int slot, string label)
        {
            Button chip = new Button();
            chip.AddToClassList("chip");
            chip.AddToClassList("chip--player");
            chip.text = label;
            Color colour = SlotColors.For(slot & 7);
            chip.style.borderLeftColor = colour;
            chip.style.borderRightColor = colour;
            chip.style.borderTopColor = colour;
            chip.style.borderBottomColor = colour;
            if (slot >= 0 && slot < _chipName.Length) _chipName[slot] = label;
            chip.userData = slot;
            chip.clicked += delegate { _selectedSlot = _selectedSlot == slot ? -1 : slot; };
            _chipRow.Add(chip);
        }

        void RefreshHint(PlayerTable players, byte localSlot)
        {
            int present = 0;
            if (players != null)
                for (int i = 0; i < players.Count; i++)
                    if (players[i] != null && players[i].present) present++;

            int mages = _def != null ? _def.MageCountFor(present) : 1;
            int nonMages = present - mages;
            if (nonMages < 0) nonMages = 0;
            // The practice stand-in is a crew member as far as the lesson is concerned, so a solo tutorial
            // still has somebody who may be bent.
            if (!string.IsNullOrEmpty(PracticeName)) nonMages += 1;
            _nonMages = nonMages;
            int maxBent = _def != null ? _def.MaxBentFor(nonMages) : (nonMages > 0 ? 1 : 0);
            _maxBent = maxBent;
            _bentNow = BitCount(_bentMask) + (_practiceBent ? 1 : 0);

            if (_hint != null)
            {
                _hint.text = Time.realtimeSinceStartup < _refusalUntil && !string.IsNullOrEmpty(_refusal)
                    ? _refusal
                    : _selectedSlot >= 0
                    ? "PICK A ROOM ON THE MAP, OR BAD END, FOR THAT PLAYER'S COMPASS"
                    : "DRAG A ROOM ONTO A NEIGHBOUR TO SWAP   -   PICK A PLAYER TO BEND THEIR COMPASS";
            }

            if (_bentCount == null) return;

            if (!string.IsNullOrEmpty(PracticeName))
            {
                // The stand-in shares no limit with anybody: it is not really in the room.
                _sb.Length = 0;
                _sb.Append(PracticeName.ToUpperInvariant());
                if (!_practiceBent) _sb.Append(": COMPASS TRUE");
                else if (_practiceKind == CompassTargetKind.BadEnd) _sb.Append(": SENT TO THE RESURRECTION ROOM");
                else _sb.Append(": SENT TO ").Append(LabelOfCell(_practiceCell).ToUpperInvariant());
                _bentCount.text = _sb.ToString();
                return;
            }

            _sb.Length = 0;
            _sb.Append("BENT ").Append(BitCount(_bentMask)).Append(" / ").Append(maxBent);
            _sb.Append("   (both fragments share the limit)");
            _bentCount.text = _sb.ToString();
        }

        string LabelOfCell(int cell)
        {
            if (_grid == null || !_grid.InRange(cell)) return "somewhere else";
            int roomId = _grid.RoomAt(cell);
            return _def != null ? _def.LabelOf(roomId) : "room " + roomId;
        }

        /// <summary>The largest number of bent players that is still STRICTLY fewer than the fraction of the non-Mages.</summary>
        /// <summary>Where a bent compass is being sent, for the Mage's own chip row.</summary>
        string TargetLabel(CompassTargetKind kind, int cell)
        {
            if (kind == CompassTargetKind.BadEnd) return "RESURRECTION ROOM";
            if (kind == CompassTargetKind.Cell) return LabelOfCell(cell).ToUpperInvariant();
            return "TRUE";
        }

        /// <summary>A refusal the Mage can read, instead of a bend that simply never happens.</summary>
void Refuse(string why)
        {
            DebugGate.Log("map: refused locally - " + why);
            _refusal = why;
            _refusalUntil = Time.realtimeSinceStartup + 4f;
            _selectedSlot = -1;
        }

        static int BitCount(byte mask)
        {
            int n = 0;
            for (int i = 0; i < 8; i++) if ((mask & (1 << i)) != 0) n++;
            return n;
        }

        void Bend(CompassTargetKind kind, int cell)
        {
            if (!_mage || _selectedSlot < 0) return;
            int slot = _selectedSlot;
            bool undo = kind == CompassTargetKind.GoodEnd;
            bool already = slot == PracticeSlot
                ? _practiceBent
                : slot >= 0 && slot < 8 && (_bentMask & (1 << slot)) != 0;

            // Every refusal the host can make is made here FIRST and said out loud. A bend that vanishes in
            // silence was the bug: with one or two crew the strict minority is zero and nothing ever happened.
            if (_bendFill > 0f) { Refuse("THE BEND IS STILL RECHARGING"); return; }
            if (!undo && !already && _bentNow >= _maxBent)
            {
                Refuse(_maxBent <= 0
                    ? "THERE IS NOBODY TO BEND"
                    : "ALREADY BENT " + _bentNow + " OF " + _maxBent + "  -  SET ONE BACK TO TRUE FIRST");
                return;
            }

            if (slot >= 0 && slot < _slotKind.Length)
            {
                _slotKind[slot] = kind;
                _slotCell[slot] = kind == CompassTargetKind.Cell ? cell : -1;
            }
            _refusal = "";
            _selectedSlot = -1;

            if (slot == PracticeSlot)
            {
                // The stand-in is not a peer, so there is nobody for the host to reply to - but it passes
                // the same cooldown and the same limit as a real crew member and reports the same way.
                _practiceKind = kind;
                _practiceBent = !undo;
                _practiceCell = kind == CompassTargetKind.Cell ? cell : -1;
                if (BendRequested != null) BendRequested(0, kind, cell);
                return;
            }
            if (slot >= 8) return;
            byte mask = (byte)(1 << slot);
            if (undo) _bentMask = (byte)(_bentMask & ~mask);
            else _bentMask |= mask;
            if (BendRequested != null) BendRequested(mask, kind, cell);
        }

        // ---------------------------------------------------------------- the Mage's drag

        static int CellOf(VisualElement element)
        {
            return element != null && element.userData is int ? (int)element.userData : -1;
        }

        void OnTilePointerDown(PointerDownEvent evt)
        {
            if (!_mage) return;
            VisualElement tile = evt.currentTarget as VisualElement;
            int cell = CellOf(tile);
            if (cell < 0) return;

            if (_selectedSlot >= 0)
            {
                Bend(CompassTargetKind.Cell, cell);
                evt.StopPropagation();
                return;
            }

            if (!_canSwap || (cell < _fixedCell.Length && _fixedCell[cell])) return;

            _dragFrom = cell;
            _dragPointer = evt.pointerId;
            tile.CapturePointer(evt.pointerId);
            if (_ghost != null)
            {
                if (_ghostLabel != null && _glyphs[cell] != null) _ghostLabel.text = _glyphs[cell].text;
                _ghost.style.display = DisplayStyle.Flex;
                MoveGhost(evt.position);
            }
            evt.StopPropagation();
        }

        void OnTilePointerMove(PointerMoveEvent evt)
        {
            if (_dragFrom < 0 || evt.pointerId != _dragPointer) return;
            MoveGhost(evt.position);
            evt.StopPropagation();
        }

        void OnTilePointerUp(PointerUpEvent evt)
        {
            if (_dragFrom < 0 || evt.pointerId != _dragPointer) return;
            VisualElement tile = evt.currentTarget as VisualElement;
            if (tile != null && tile.HasPointerCapture(evt.pointerId)) tile.ReleasePointer(evt.pointerId);

            int from = _dragFrom;
            EndDrag();

            int to = TileUnder(evt.position);
            if (to >= 0 && to != from && _grid != null &&
                _grid.CanSwap(from, to, _def != null && _def.swapAcrossWrap) &&
                SwapRequested != null)
                SwapRequested(from, to);

            evt.StopPropagation();
        }

        /// <summary>Drop the drag without asking for anything: the map closed, or the round ended.</summary>
        public void EndDrag()
        {
            _dragFrom = -1;
            _dragPointer = -1;
            if (_ghost != null) _ghost.style.display = DisplayStyle.None;
        }

        /// <summary>Forget the selected player chip as well. Called when the map closes.</summary>
        public void ClearSelection()
        {
            EndDrag();
            _selectedSlot = -1;
        }

        int TileUnder(Vector3 panelPosition)
        {
            Vector2 point = new Vector2(panelPosition.x, panelPosition.y);
            for (int cell = 0; cell < _tiles.Length; cell++)
                if (_tiles[cell] != null && _tiles[cell].worldBound.Contains(point)) return cell;
            return -1;
        }

        void MoveGhost(Vector3 panelPosition)
        {
            if (_ghost == null || _overlay == null) return;
            Vector2 local = _overlay.WorldToLocal(new Vector2(panelPosition.x, panelPosition.y));
            _ghost.style.left = local.x - GhostHalf;
            _ghost.style.top = local.y - GhostHalf;
        }

        // ---------------------------------------------------------------- cooldown rings

        /// <summary>0 = ready, 1 = the whole wait still to go. A local guide only; the host owns the truth.</summary>
        public void SetCooldowns(float swapRemaining01, float swapSeconds, float bendRemaining01, float bendSeconds)
        {
            _swapFill = Mathf.Clamp01(swapRemaining01);
            _bendFill = Mathf.Clamp01(bendRemaining01);
            if (_swapRing != null) _swapRing.MarkDirtyRepaint();
            if (_bendRing != null) _bendRing.MarkDirtyRepaint();
            if (_swapRingText != null)
                _swapRingText.text = _swapFill <= 0f ? "SWAP" : Mathf.CeilToInt(_swapFill * swapSeconds).ToString();
            if (_bendRingText != null)
                _bendRingText.text = _bendFill <= 0f ? "BEND" : Mathf.CeilToInt(_bendFill * bendSeconds).ToString();
        }

        void OnDrawSwapRing(MeshGenerationContext ctx)
        {
            DrawRing(ctx, _swapFill, new Color(0.93f, 0.76f, 0.31f));
        }

        void OnDrawBendRing(MeshGenerationContext ctx)
        {
            DrawRing(ctx, _bendFill, new Color(0.71f, 0.55f, 0.90f));
        }

        static void DrawRing(MeshGenerationContext ctx, float fill, Color colour)
        {
            VisualElement element = ctx.visualElement;
            if (element == null) return;
            Rect rect = element.contentRect;
            if (rect.width < 8f || rect.height < 8f) return;

            Vector2 centre = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f - 3f;
            if (radius <= 1f) return;

            Painter2D painter = ctx.painter2D;
            painter.lineWidth = 4f;
            painter.strokeColor = new Color(1f, 1f, 1f, 0.12f);
            painter.BeginPath();
            painter.Arc(centre, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            painter.Stroke();

            if (fill <= 0f) return;
            painter.strokeColor = colour;
            painter.BeginPath();
            painter.Arc(centre, radius, new Angle(-90f, AngleUnit.Degree),
                        new Angle(-90f + 360f * Mathf.Clamp01(fill), AngleUnit.Degree));
            painter.Stroke();
        }
    }
}
