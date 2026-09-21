using System.Text;
using Pesky.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    /// <summary>
    /// The HUD (UI Toolkit). It is a pure view: it reads the local soul and reacts to WorldAuthority events,
    /// and never changes shared state. Shows the weapon name, HP bar, modifiers, the party key, the context
    /// prompt (possess / anvil), the IN COMBAT tag and the soul hints.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudController : MonoBehaviour
    {
        [SerializeField] UIDocument document;
        [SerializeField] WorldAuthority authority;
        [SerializeField] PlayerSpawner spawner;
        [Tooltip("Applied at runtime as well, so the HUD is styled even if the UXML's Style tag is lost.")]
        [SerializeField] StyleSheet style;

        [Header("Hints")]
        [SerializeField] string freeHint = "WASD  fly     SPACE  up     SHIFT  down     E  possess";
        [SerializeField] string possessedHint = "SPACE  launch     Q  release     (no WASD)";        [Tooltip("Shown when a free soul pushes at a magic doorway: doorways are solid to souls, only weapons travel.")]
        [SerializeField] string soulDoorHint = "A SOUL CANNOT USE A DOORWAY  -  POSSESS A WEAPON  (E)  FIRST";


        [Header("Magic door")]
        [Tooltip("Seconds the purple full-screen flash stays up after the local player goes through a magic door.")]
        [SerializeField] float magicFlashSeconds = 0.15f;

        Label _weaponName, _hpText, _modifiers, _keyTag, _combatTag, _prompt, _hint, _flash;
        VisualElement _weaponPanel, _hpFill, _anvilTrack, _anvilFill, _magicFlash;
        float _magicUntil = -1f;
        PlayerSoul _soul;
        readonly StringBuilder _sb = new StringBuilder(64);
        float _flashUntil;

        void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (spawner != null) spawner.SoulSpawned += OnSoulSpawned;
        }

        void OnEnable()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null) return;
            if (style != null && !root.styleSheets.Contains(style)) root.styleSheets.Add(style);

            _weaponPanel = root.Q<VisualElement>("weapon-panel");
            _weaponName = root.Q<Label>("weapon-name");
            _hpFill = root.Q<VisualElement>("hp-fill");
            _hpText = root.Q<Label>("hp-text");
            _modifiers = root.Q<Label>("modifiers");
            _keyTag = root.Q<Label>("key-tag");
            _combatTag = root.Q<Label>("combat-tag");
            _prompt = root.Q<Label>("prompt");
            _hint = root.Q<Label>("hint");
            _flash = root.Q<Label>("flash");
            _anvilTrack = root.Q<VisualElement>("anvil-track");
            _anvilFill = root.Q<VisualElement>("anvil-fill");
            _magicFlash = root.Q<VisualElement>("magic-flash");

            if (authority != null)
            {
                authority.KeyGained += OnKeyGained;
                authority.ModifierAttached += OnModifierAttached;
                authority.Healed += OnHealed;
                authority.WeaponBroken += OnWeaponBroken;
                authority.DoorOpened += OnDoorOpened;
                authority.PlateLatched += OnPlateLatched;
                authority.EnemyDied += OnEnemyDied;
                authority.MagicDoorTraversed += OnMagicDoorTraversed;                authority.SoulBlockedByDoor += OnSoulBlockedByDoor;

            }
            Refresh();
        }

        void OnDisable()
        {
            if (authority != null)
            {
                authority.KeyGained -= OnKeyGained;
                authority.ModifierAttached -= OnModifierAttached;
                authority.Healed -= OnHealed;
                authority.WeaponBroken -= OnWeaponBroken;
                authority.DoorOpened -= OnDoorOpened;
                authority.PlateLatched -= OnPlateLatched;
                authority.EnemyDied -= OnEnemyDied;
                authority.MagicDoorTraversed -= OnMagicDoorTraversed;                authority.SoulBlockedByDoor -= OnSoulBlockedByDoor;

            }
        }

        void OnDestroy()
        {
            if (spawner != null) spawner.SoulSpawned -= OnSoulSpawned;
        }

        void OnSoulSpawned(PlayerSoul soul)
        {
            _soul = soul;
        }

        void Update()
        {
            if (_soul == null && spawner != null) _soul = spawner.LocalSoul;
            Refresh();
        }

        void Refresh()
        {
            if (_weaponPanel == null) return;

            WeaponBody weapon = _soul != null ? _soul.Weapon : null;
            bool possessing = weapon != null;

            Show(_weaponPanel, possessing);
            if (possessing)
            {
                if (_weaponName != null)
                    _weaponName.text = weapon.Def != null ? weapon.Def.displayName : weapon.name;

                float max = Mathf.Max(1f, weapon.MaxHp);
                float pct = Mathf.Clamp01(weapon.Hp / max);
                if (_hpFill != null)
                {
                    _hpFill.style.width = Length.Percent(pct * 100f);
                    _hpFill.EnableInClassList("low", pct <= 0.33f);
                }
                if (_hpText != null) _hpText.text = Mathf.CeilToInt(weapon.Hp) + " / " + Mathf.CeilToInt(max);
                if (_modifiers != null) _modifiers.text = ModifierText(weapon);
            }

            if (_keyTag != null)
            {
                bool hasKey = authority != null && authority.HasAnyKey;
                Show(_keyTag, hasKey);
                if (hasKey) _keyTag.text = KeyText();
            }

            bool inCombat = _soul != null && _soul.InCombat;
            if (_combatTag != null) Show(_combatTag, inCombat);

            AnvilStation anvil = authority != null ? authority.NearAnvil : null;
            bool anvilReady = possessing && anvil != null && !inCombat;
            if (_anvilTrack != null) Show(_anvilTrack, anvilReady && anvil.Progress > 0.001f);
            if (_anvilFill != null && anvil != null)
                _anvilFill.style.width = Length.Percent(anvil.Progress * 100f);

            if (_prompt != null)
            {
                string text = null;
                if (possessing)
                {
                    Door nearDoor = authority != null ? authority.NearDoor : null;
                    DoorCondition nearCondition = nearDoor != null ? nearDoor.Condition : null;
                    if (nearDoor != null && !nearDoor.IsOpen && nearCondition != null && !nearCondition.IsSatisfied(authority))
                        text = nearCondition.ConditionMode == DoorCondition.Mode.PartyHasKey
                            ? "KEY " + nearCondition.KeyId + "  NEEDED"
                            : nearCondition.Describe() + "  NEEDED";
                    else if (anvil != null) text = inCombat ? "ANVIL  -  not in combat" : "HOLD  E   anvil: full HP";
                }
                else if (_soul != null && _soul.Candidate != null)
                {
                    WeaponDef d = _soul.Candidate.Def;
                    text = "E   possess " + (d != null ? d.displayName : _soul.Candidate.name);
                }
                Show(_prompt, text != null);
                if (text != null) _prompt.text = text;
            }

            if (_hint != null) _hint.text = possessing ? possessedHint : freeHint;

            if (_flash != null) Show(_flash, Time.time < _flashUntil);
            if (_magicFlash != null) Show(_magicFlash, Time.time < _magicUntil);
        }

        string ModifierText(WeaponBody weapon)
        {
            var mods = weapon.Modifiers;
            if (mods == null || mods.Count == 0) return "no modifiers";
            _sb.Length = 0;
            for (int i = 0; i < mods.Count; i++)
            {
                if (mods[i] == null) continue;
                if (_sb.Length > 0) _sb.Append("  ");
                _sb.Append(mods[i].displayName);
                if (!Mathf.Approximately(mods[i].damageMultiplier, 1f))
                    _sb.Append(" x").Append(mods[i].damageMultiplier.ToString("0.00"));
            }
            return _sb.Length > 0 ? _sb.ToString() : "no modifiers";
        }

        static void Show(VisualElement element, bool on)
        {
            if (element == null) return;
            element.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void Banner(string text, float seconds)
        {
            if (_flash == null) return;
            _flash.text = text;
            _flashUntil = Time.time + seconds;
        }

        void OnKeyGained(int keyId) { Banner("KEY " + keyId + "  TAKEN", 2.5f); }

        /// <summary>Every key the party holds, by id: "KEY 1   KEY 2".</summary>
        string KeyText()
        {
            System.Collections.Generic.IReadOnlyList<int> held = authority.HeldKeys;
            _sb.Length = 0;
            for (int i = 0; i < held.Count; i++)
            {
                if (i > 0) _sb.Append("   ");
                _sb.Append("KEY ").Append(held[i]);
            }
            return _sb.ToString();
        }

        /// <summary>The local player (as a soul or in a weapon) went through a magic door: a short purple flash.</summary>
        void OnMagicDoorTraversed(MagicDoorTraversal trip)
        {
            if (_soul == null || trip.soul != _soul) return;
            _magicUntil = Time.time + magicFlashSeconds;
        }
        void OnModifierAttached(WeaponBody w, ModifierDef m) { Banner((m != null ? m.displayName : "MODIFIER") + "  ATTACHED", 2.5f); }
        void OnHealed(WeaponBody w) { Banner("REPAIRED", 1.5f); }
        void OnWeaponBroken(WeaponBody w) { Banner("WEAPON  BROKE", 2f); }        void OnSoulBlockedByDoor(PlayerSoul s) { Banner(soulDoorHint, 3f); }

        void OnDoorOpened(Door d) { Banner("DOOR  OPEN", 2f); }
        void OnPlateLatched(PressurePlate p) { Banner("PLATE  LATCHED", 2f); }
        void OnEnemyDied(GoblinBrain g, WeaponBody by) { Banner("GOBLIN  DOWN", 1.5f); }
    }
}
