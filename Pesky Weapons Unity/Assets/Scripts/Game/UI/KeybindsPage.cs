using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    /// <summary>
    /// The KEYBINDS page of the settings screen (docs/SETTINGS.md): one row per rebindable Gameplay binding
    /// (Keybinds.Collect), its name and a key button. Clicking a key starts the Input System's interactive
    /// rebind: the button reads PRESS A KEY until a key or mouse button lands, and Escape keeps the old one.
    /// Every change is saved at once (Keybinds.Save); a changed key wears an amber border; RESET ALL drops
    /// every override. The rows are built the first time the page shows. Ported from ATCK's KeybindsPage.
    /// </summary>
    public sealed class KeybindsPage
    {
        public const string WaitingWord = "PRESS A KEY";
        const string WaitingClass = "keys-key--waiting";
        const string ChangedClass = "keys-key--changed";

        readonly VisualElement _rows;
        readonly Label _status;
        readonly InputActionAsset _asset;
        readonly List<Keybinds.Entry> _entries = new List<Keybinds.Entry>(32);
        readonly List<Button> _keys = new List<Button>(32);
        InputActionRebindingExtensions.RebindingOperation _op;
        int _rebinding = -1;
        int _cancelledFrame = -1;
        bool _built;

        public KeybindsPage(VisualElement page, InputActionAsset asset)
        {
            _asset = asset;
            if (page == null) return;
            _rows = page.Q<VisualElement>("keys-rows");
            _status = page.Q<Label>("keys-status");
            Button reset = page.Q<Button>("keys-reset");
            if (reset != null) reset.clicked += ResetAll;
        }

        /// <summary>True while a rebind is listening for a key.</summary>
        public bool IsRebinding { get { return _op != null; } }

        /// <summary>True while a rebind listens, and on the frame Escape cancelled one, so the settings screen leaves that Escape alone.</summary>
        public bool SwallowsEscape { get { return _op != null || _cancelledFrame == Time.frameCount; } }

        public void Show()
        {
            if (!_built) Build();
            SetStatus("");
            Refresh();
        }

        void Build()
        {
            if (_asset == null || _rows == null)
            {
                SetStatus("no controls asset: nothing to rebind");
                return;
            }
            _built = true;
            _rows.Clear();
            _keys.Clear();
            int count = Keybinds.Collect(_asset, _entries);
            for (int i = 0; i < count; i++)
            {
                VisualElement row = new VisualElement();
                row.AddToClassList("keys-row");
                Label name = new Label(_entries[i].label);
                name.AddToClassList("keys-name");
                row.Add(name);
                int index = i;
                Button key = new Button(() => StartRebind(index));
                key.AddToClassList("keys-key");
                row.Add(key);
                _rows.Add(row);
                _keys.Add(key);
            }
        }

        void Refresh()
        {
            for (int i = 0; i < _keys.Count; i++)
            {
                bool waiting = i == _rebinding;
                _keys[i].text = waiting ? WaitingWord : Keybinds.Display(_entries[i]);
                _keys[i].EnableInClassList(WaitingClass, waiting);
                _keys[i].EnableInClassList(ChangedClass, !waiting && Keybinds.IsOverridden(_entries[i]));
            }
        }

        void StartRebind(int index)
        {
            if (_asset == null || index < 0 || index >= _entries.Count) return;
            Cancel();
            _rebinding = index;
            Refresh();
            SetStatus("press the new key or mouse button for " + _entries[index].label + ".   ESC keeps the old one.");
            _op = Keybinds.Rebind(_entries[index], OnComplete, OnCancel);
        }

        void OnComplete(InputActionRebindingExtensions.RebindingOperation op)
        {
            Finish(op);
            Keybinds.Save(_asset);
            SetStatus("saved");
        }

        void OnCancel(InputActionRebindingExtensions.RebindingOperation op)
        {
            _cancelledFrame = Time.frameCount;
            Finish(op);
            SetStatus("kept the old key");
        }

        void Finish(InputActionRebindingExtensions.RebindingOperation op)
        {
            if (op != null) op.Dispose();
            _op = null;
            _rebinding = -1;
            Refresh();
        }

        /// <summary>Abandons a rebind in progress (the page or the screen closing, another key clicked). The old key stays.</summary>
        public void Cancel()
        {
            if (_op == null) return;
            InputActionRebindingExtensions.RebindingOperation op = _op;
            _op = null;
            _rebinding = -1;
            op.Cancel(); // OnCancel enables the action again, disposes the operation and refreshes the rows
        }

        void ResetAll()
        {
            Cancel();
            Keybinds.ResetAll(_asset);
            Refresh();
            SetStatus("every key is back to its default");
        }

        void SetStatus(string text)
        {
            if (_status != null) _status.text = text;
        }
    }
}
