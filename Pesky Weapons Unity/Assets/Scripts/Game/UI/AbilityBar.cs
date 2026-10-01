using System.Collections.Generic;
using Pesky.Protocol;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    /// <summary>
    /// Where an ability slot reads its cooldown from. A new power with its own cooldown adds ONE value here
    /// and pushes its cooldown with <see cref="RunHud.SetCooldown"/>; everything else is data.
    /// </summary>
    public enum AbilityCooldownSource
    {
        None = 0,
        /// <summary>The Mage's one shared curse cooldown (MageCurse).</summary>
        Curse = 1,
        /// <summary>The nudge / pull cooldown (MageNudge).</summary>
        Nudge = 2,
        /// <summary>The vote call: the group cooldown or the "no votes yet" wait, and the USED / PAUSED / OUT locks (VoteCaller, docs/VOTING.md).</summary>
        Vote = 3,
    }

    /// <summary>
    /// One slot of the ability bar (docs/RUN.md 6.5). Data only: putting a power on the bar is adding one of
    /// these to <c>RunHud.abilities</c> in the Inspector.
    /// </summary>
    [System.Serializable]
    public sealed class AbilitySlotDef
    {
        [Tooltip("Short stable id, e.g. \"magnetic\". The slot's element is named ability-<id>.")]
        public string id = "";

        [Tooltip("The key printed in the slot's top-left corner: \"1\" .. \"5\", \"LMB / RMB\", \"V\".")]
        public string keyLabel = "";

        [Tooltip("The name under the slot.")]
        public string displayName = "";

        [Tooltip("Placeholder icon: one to three characters on a flat grey tile. No art yet.")]
        public string iconGlyph = "";

        [Tooltip("The placeholder tile's colour.")]
        public Color iconTint = new Color(0.34f, 0.36f, 0.41f, 1f);

        [Tooltip("Which cooldown darkens this slot. None: never.")]
        public AbilityCooldownSource cooldown = AbilityCooldownSource.None;

        [Tooltip("Curse slots only: the curse whose cast makes this slot pulse. A slot on any other source pulses on every use of that source.")]
        public CurseKind curse = CurseKind.None;

        [Tooltip("A wider gap before this slot, to set a group apart (the five curses after the nudge).")]
        public bool gapBefore;

        [Tooltip("Shown to a Mage alone (the nudge, the curses). Off: everybody's slot (the vote). A banished Mage keeps only the latter.")]
        public bool mageOnly = true;
    }

    /// <summary>
    /// The ability bar: a row of square slots centred at the bottom of the screen, built at runtime from a
    /// list of <see cref="AbilitySlotDef"/> into Run.uxml's <c>ability-bar</c> container and styled by Run.uss
    /// (<c>.ability*</c>). Everybody has it (round 12): the VOTE slot first; a Mage also sees the nudge and the
    /// five curses (<see cref="SetMageSlots"/>). Each slot is a grey-box tile with a glyph, its key in the
    /// corner and its name underneath. While its cooldown runs the slot darkens from the top (the shade
    /// shrinks upward as the power comes back) and shows the whole seconds left; a LOCKED slot (a word from
    /// <see cref="SetLock"/>: USED, WAIT, PAUSED, OUT) darkens fully and shows the word; the slot of the power
    /// just used pulses.
    ///
    /// Plain C#, owned by RunHud, which decides when it is shown and feeds it the cooldowns and locks.
    /// </summary>
    public sealed class AbilityBar
    {
        /// <summary>One more than the largest <see cref="AbilityCooldownSource"/> value: the size of RunHud's cooldown tables.</summary>
        public static readonly int SourceCount = ComputeSourceCount();

        sealed class Slot
        {
            public AbilitySlotDef def;
            public VisualElement root;
            public VisualElement shade;
            public Label time;
            public bool cooling;
            public bool locked;
            public string lockShown = "";
            public int secondsShown = -1;
            public float fillShown = -1f;
            public float pulseUntil = -1f;
        }

        readonly List<Slot> _slots = new List<Slot>();
        readonly string[] _lock = new string[SourceCount];
        bool _mageSlots = true;

        /// <summary>Clears the container and builds one slot per entry, left to right. A null container builds nothing.</summary>
        public AbilityBar(VisualElement container, IList<AbilitySlotDef> defs)
        {
            for (int i = 0; i < _lock.Length; i++) _lock[i] = "";
            if (container == null) return;
            container.Clear();
            if (defs == null) return;
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i] == null) continue;
                Slot slot = Build(defs[i]);
                container.Add(slot.root);
                _slots.Add(slot);
            }
        }

        public int Count { get { return _slots.Count; } }

        /// <summary>The vote first (everybody's), then the nudge / pull (LMB / RMB) and the five curses in key order, set a little apart (Mage only).</summary>
        public static List<AbilitySlotDef> DefaultSlots()
        {
            AbilitySlotDef vote = new AbilitySlotDef
            {
                id = "vote",
                keyLabel = "V",
                displayName = "Call vote",
                iconGlyph = "V",
                iconTint = new Color(0.46f, 0.36f, 0.58f, 1f),
                cooldown = AbilityCooldownSource.Vote,
                mageOnly = false,
            };
            AbilitySlotDef nudge = new AbilitySlotDef
            {
                id = "nudge",
                keyLabel = "LMB / RMB",
                displayName = "Nudge / Pull",
                iconGlyph = "N/P",
                cooldown = AbilityCooldownSource.Nudge,
                gapBefore = true,
            };
            return new List<AbilitySlotDef>
            {
                vote,
                nudge,
                CurseSlot("magnetic", "1", "Magnetic", "M", CurseKind.Magnetic, true),
                CurseSlot("nausea", "2", "Nausea", "N", CurseKind.Nausea, false),
                CurseSlot("slippery", "3", "Slippery", "S", CurseKind.Slippery, false),
                CurseSlot("blindness", "4", "Blindness", "B", CurseKind.Blindness, false),
                CurseSlot("heavy", "5", "Heavy", "H", CurseKind.Heavy, false),
            };
        }

        static AbilitySlotDef CurseSlot(string id, string key, string name, string glyph, CurseKind curse, bool gap)
        {
            return new AbilitySlotDef
            {
                id = id,
                keyLabel = key,
                displayName = name,
                iconGlyph = glyph,
                cooldown = AbilityCooldownSource.Curse,
                curse = curse,
                gapBefore = gap,
            };
        }

        static int ComputeSourceCount()
        {
            int max = 0;
            foreach (AbilityCooldownSource v in System.Enum.GetValues(typeof(AbilityCooldownSource)))
                if ((int)v > max) max = (int)v;
            return max + 1;
        }

        static Slot Build(AbilitySlotDef def)
        {
            Slot slot = new Slot { def = def };

            slot.root = new VisualElement { name = "ability-" + def.id, pickingMode = PickingMode.Ignore };
            slot.root.AddToClassList("ability");
            if (def.gapBefore) slot.root.AddToClassList("ability--gap");

            VisualElement frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.AddToClassList("ability-frame");

            VisualElement icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("ability-icon");
            icon.style.backgroundColor = def.iconTint;
            Label glyph = new Label(def.iconGlyph ?? "") { pickingMode = PickingMode.Ignore, enableRichText = false };
            glyph.AddToClassList("ability-glyph");
            icon.Add(glyph);
            frame.Add(icon);

            slot.shade = new VisualElement { pickingMode = PickingMode.Ignore };
            slot.shade.AddToClassList("ability-shade");
            slot.shade.style.height = Length.Percent(0f);
            frame.Add(slot.shade);

            slot.time = new Label("") { pickingMode = PickingMode.Ignore, enableRichText = false };
            slot.time.AddToClassList("ability-time");
            frame.Add(slot.time);

            string keyText = def.keyLabel ?? "";
            Label key = new Label(keyText) { pickingMode = PickingMode.Ignore, enableRichText = false };
            key.AddToClassList("ability-key");
            if (keyText.Length > 2) key.AddToClassList("ability-key--long");
            frame.Add(key);

            slot.root.Add(frame);

            Label name = new Label(def.displayName ?? "") { pickingMode = PickingMode.Ignore, enableRichText = false };
            name.AddToClassList("ability-name");
            slot.root.Add(name);
            return slot;
        }

        /// <summary>Shows or hides every mageOnly slot (a weapon, or a banished Mage, keeps the rest).</summary>
        public void SetMageSlots(bool on)
        {
            if (_mageSlots == on) return;
            _mageSlots = on;
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot s = _slots[i];
                if (s.def.mageOnly) s.root.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>A word on every slot of a source saying why it cannot be used (USED, WAIT, PAUSED, OUT); empty = no lock. A running cooldown shows its seconds instead.</summary>
        public void SetLock(AbilityCooldownSource source, string text)
        {
            int i = (int)source;
            if (i <= 0 || i >= _lock.Length) return;
            _lock[i] = text ?? "";
        }

        /// <summary>
        /// Once a frame while shown. <paramref name="readyAt"/> and <paramref name="seconds"/> are indexed by
        /// <see cref="AbilityCooldownSource"/>: when each source is ready again (Time.time) and how long its
        /// full cooldown is.
        /// </summary>
        public void Refresh(float now, float[] readyAt, float[] seconds)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot s = _slots[i];
                int src = (int)s.def.cooldown;
                float left = 0f;
                float total = 0f;
                string lockText = "";
                if (src > 0 && readyAt != null && seconds != null && src < readyAt.Length && src < seconds.Length)
                {
                    left = readyAt[src] - now;
                    total = seconds[src];
                }
                if (src > 0 && src < _lock.Length) lockText = _lock[src];
                float fill = total > 0.01f && left > 0f ? Mathf.Clamp01(left / total) : 0f;
                bool cooling = fill > 0f;
                bool locked = !cooling && lockText.Length > 0;
                if (locked) fill = 1f;

                if (cooling != s.cooling)
                {
                    s.cooling = cooling;
                    s.root.EnableInClassList("is-cooling", cooling);
                    if (!cooling)
                    {
                        s.secondsShown = -1;
                        s.time.text = "";
                    }
                }
                if (locked != s.locked)
                {
                    s.locked = locked;
                    s.root.EnableInClassList("is-locked", locked);
                    s.time.EnableInClassList("ability-time--text", locked);
                    if (!locked)
                    {
                        s.lockShown = "";
                        if (!cooling) s.time.text = "";
                    }
                }
                if (fill != s.fillShown)
                {
                    s.fillShown = fill;
                    s.shade.style.height = Length.Percent(fill * 100f);
                }
                if (cooling)
                {
                    int whole = Mathf.CeilToInt(left);
                    if (whole != s.secondsShown)
                    {
                        s.secondsShown = whole;
                        s.time.text = whole.ToString();
                    }
                }
                else if (locked && lockText != s.lockShown)
                {
                    s.lockShown = lockText;
                    s.time.text = lockText;
                }
                if (s.pulseUntil > 0f && now >= s.pulseUntil)
                {
                    s.pulseUntil = -1f;
                    s.root.RemoveFromClassList("is-pulse");
                }
            }
        }

        /// <summary>
        /// The power was just used: its slot pulses for <paramref name="seconds"/>. A Curse source pulses only
        /// the slot of that curse; any other source pulses every slot on it.
        /// </summary>
        public void Pulse(AbilityCooldownSource source, CurseKind curse, float now, float seconds)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot s = _slots[i];
                if (s.def.cooldown != source) continue;
                if (source == AbilityCooldownSource.Curse && s.def.curse != curse) continue;
                s.root.AddToClassList("is-pulse");
                s.pulseUntil = now + Mathf.Max(0.05f, seconds);
            }
        }
    }
}
