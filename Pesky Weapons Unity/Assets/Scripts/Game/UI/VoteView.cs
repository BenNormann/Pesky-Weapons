using System;
using System.Collections.Generic;
using Pesky.Protocol;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    /// <summary>
    /// The vote screen's elements (docs/VOTING.md), in Run.uxml under <c>vote-root</c>: the header, the
    /// countdown, a two-column grid of candidate cards built here, SKIP VOTE, CONFIRM, a status line and the
    /// result panel. Like SettingsView: queries by name, shows and fills, raises plain events, decides nothing.
    /// VoteScreen owns it.
    /// </summary>
    public sealed class VoteView
    {
        /// <summary>One card of the grid: a player slot, or the tutorial dummy (VoteTarget.Dummy).</summary>
        public struct Candidate
        {
            public byte target;
            public string name;
            public Color colour;
            public bool you;
            public bool banished;
            public bool selectable;
        }

        sealed class Card
        {
            public byte target;
            public Button root;
            public VisualElement chips;
        }

        public event Action<byte> CandidateClicked;
        public event Action SkipClicked;
        public event Action ConfirmClicked;

        readonly VisualElement _root;
        readonly Label _title, _timer, _status, _resultTitle, _resultSub;
        readonly VisualElement _grid, _skipRow, _skipChips, _result;
        readonly Button _skip, _confirm;
        readonly List<Card> _cards = new List<Card>();

        public bool IsValid { get { return _root != null; } }

        public VoteView(VisualElement documentRoot)
        {
            _root = documentRoot != null ? documentRoot.Q<VisualElement>("vote-root") : null;
            if (_root == null) return;
            _title = _root.Q<Label>("vote-title");
            _timer = _root.Q<Label>("vote-timer");
            _grid = _root.Q<VisualElement>("vote-grid");
            _skipRow = _root.Q<VisualElement>("vote-skip-row");
            _skip = _root.Q<Button>("vote-skip");
            _skipChips = _root.Q<VisualElement>("vote-skip-chips");
            _confirm = _root.Q<Button>("vote-confirm");
            _status = _root.Q<Label>("vote-status");
            _result = _root.Q<VisualElement>("vote-result");
            _resultTitle = _root.Q<Label>("vote-result-title");
            _resultSub = _root.Q<Label>("vote-result-sub");
            if (_skip != null) _skip.clicked += () => { if (SkipClicked != null) SkipClicked(); };
            if (_confirm != null) _confirm.clicked += () => { if (ConfirmClicked != null) ConfirmClicked(); };
        }

        // ---------------------------------------------------------------- showing

        public void SetVisible(bool on)
        {
            if (_root == null) return;
            _root.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            // A hidden button must not keep the focus: Space would "click" it in the middle of a run.
            if (!on && _root.focusController != null && _root.focusController.focusedElement is VisualElement focused) focused.Blur();
        }

        public void SetHeader(string text)
        {
            if (_title != null) _title.text = text;
        }

        public void SetTimer(int seconds)
        {
            if (_timer == null) return;
            _timer.text = seconds.ToString();
            _timer.EnableInClassList("is-warning", seconds <= 5);
        }

        public void SetStatus(string text)
        {
            if (_status == null) return;
            _status.text = text ?? "";
            Show(_status, !string.IsNullOrEmpty(text));
        }

        // ---------------------------------------------------------------- the grid

        /// <summary>Rebuilds the two-column grid, in list order. Banished cards are greyed and never click.</summary>
        public void BuildCandidates(IList<Candidate> list)
        {
            _cards.Clear();
            if (_grid == null) return;
            _grid.Clear();
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                Candidate c = list[i];
                Card card = new Card();
                card.target = c.target;
                card.root = new Button { name = "vote-card-" + c.target };
                card.root.AddToClassList("vote-card");
                card.root.text = "";
                if (c.banished) card.root.AddToClassList("is-banished");
                if (!c.selectable) card.root.AddToClassList("is-unselectable");

                VisualElement row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("vote-card-row");
                VisualElement swatch = new VisualElement { pickingMode = PickingMode.Ignore };
                swatch.AddToClassList("vote-swatch");
                swatch.style.backgroundColor = c.colour;
                row.Add(swatch);
                Label name = new Label(c.name ?? "") { pickingMode = PickingMode.Ignore, enableRichText = false };
                name.AddToClassList("vote-name");
                row.Add(name);
                if (c.you)
                {
                    Label you = new Label("YOU") { pickingMode = PickingMode.Ignore, enableRichText = false };
                    you.AddToClassList("vote-you");
                    row.Add(you);
                }
                if (c.banished)
                {
                    Label gone = new Label("BANISHED") { pickingMode = PickingMode.Ignore, enableRichText = false };
                    gone.AddToClassList("vote-gone");
                    row.Add(gone);
                }
                card.root.Add(row);

                card.chips = new VisualElement { pickingMode = PickingMode.Ignore };
                card.chips.AddToClassList("vote-chips");
                card.root.Add(card.chips);

                byte target = c.target;
                bool selectable = c.selectable && !c.banished;
                if (selectable) card.root.clicked += () => { if (CandidateClicked != null) CandidateClicked(target); };
                _grid.Add(card.root);
                _cards.Add(card);
            }
        }

        /// <summary>Highlights one choice (a target, VoteTarget.Skip, or VoteTarget.None for nothing). locked = confirmed: the others dim and CONFIRM goes.</summary>
        public void SetSelection(byte target, bool locked)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                Card c = _cards[i];
                bool on = c.target == target;
                c.root.EnableInClassList("is-selected", on);
                c.root.EnableInClassList("is-locked", locked && !on);
            }
            if (_skip != null)
            {
                _skip.EnableInClassList("is-selected", target == VoteTarget.Skip);
                _skip.EnableInClassList("is-locked", locked && target != VoteTarget.Skip);
            }
            if (_confirm != null)
            {
                _confirm.SetEnabled(!locked && target != VoteTarget.None);
                Show(_confirm, !locked);
            }
        }

        /// <summary>Lets a voter press anything at all (false for a ghost or a late joiner, who only watches).</summary>
        public void SetInteractive(bool on)
        {
            for (int i = 0; i < _cards.Count; i++) _cards[i].root.SetEnabled(on);
            if (_skip != null) _skip.SetEnabled(on);
            if (_confirm != null) _confirm.SetEnabled(on && _confirm.enabledSelf);
        }

        /// <summary>
        /// Every voter's chip beside what they voted for: <paramref name="voteOf"/> by voter slot (VoteTarget.None
        /// for no vote yet), with the voters' names and colours by slot.
        /// </summary>
        public void SetVotes(byte[] voteOf, string[] names, Color[] colours)
        {
            for (int i = 0; i < _cards.Count; i++) _cards[i].chips.Clear();
            if (_skipChips != null) _skipChips.Clear();
            if (voteOf == null) return;
            for (int voter = 0; voter < voteOf.Length && voter < Wire.MaxPlayers; voter++)
            {
                byte t = voteOf[voter];
                if (t == VoteTarget.None) continue;
                VisualElement into = null;
                if (t == VoteTarget.Skip) into = _skipChips;
                else
                {
                    for (int c = 0; c < _cards.Count; c++) if (_cards[c].target == t) { into = _cards[c].chips; break; }
                }
                if (into == null) continue;
                VisualElement chip = new VisualElement { pickingMode = PickingMode.Ignore };
                chip.AddToClassList("vote-chip");
                chip.style.backgroundColor = colours != null && voter < colours.Length ? colours[voter] : Color.grey;
                string n = names != null && voter < names.Length && !string.IsNullOrEmpty(names[voter]) ? names[voter] : "P" + (voter + 1);
                Label tag = new Label(n.Length > 3 ? n.Substring(0, 3).ToUpperInvariant() : n.ToUpperInvariant()) { pickingMode = PickingMode.Ignore, enableRichText = false };
                tag.AddToClassList("vote-chip-text");
                chip.Add(tag);
                into.Add(chip);
            }
        }

        // ---------------------------------------------------------------- the result

        public void ShowResult(string title, string sub)
        {
            Show(_grid, false);
            Show(_skipRow, false);
            Show(_confirm, false);
            Show(_status, false);
            Show(_timer, false);
            if (_resultTitle != null) _resultTitle.text = title ?? "";
            if (_resultSub != null) _resultSub.text = sub ?? "";
            Show(_result, true);
        }

        /// <summary>Back to the voting layout (a new meeting).</summary>
        public void ShowVoting()
        {
            Show(_result, false);
            Show(_grid, true);
            Show(_skipRow, true);
            Show(_confirm, true);
            Show(_timer, true);
        }

        static void Show(VisualElement element, bool on)
        {
            if (element == null) return;
            element.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
