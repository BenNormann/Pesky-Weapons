using Pesky.Data;
using Pesky.Protocol;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pesky.Game
{
    /// <summary>
    /// The Mage's five curses on the client (docs/RUN.md): keys 1-5 cast on the player under the crosshair
    /// (the body nearest the centre of the screen inside RunDef.curseAimCone, or the tutorial dummy), within
    /// RunDef.curseRange of the Mage's own body, on ONE shared cooldown. The host checks everything again
    /// (RunRule) and refusals come back to this peer alone as a short, quiet line.
    ///
    /// NOT A TELL. For a weapon the keys do nothing and send nothing: WorldAuthority.RequestCurse refuses to
    /// send unless this peer's own role is Mage, so a weapon's key press is not even a packet. A key counts
    /// only while the pointer is locked to the game and the camera takes input.
    ///
    /// The cooldown shows on RunHud's ability bar at the bottom of the screen (all five curse slots darken
    /// together; the one just cast pulses), drawn for a Mage alone.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MageCurse : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] WorldAuthority authority;
        [SerializeField] PlayerSpawner spawner;
        [SerializeField] OrbitCamera orbitCamera;
        [Tooltip("Shows the cooldown on the ability bar and the refusal line. Empty in a scene without a run HUD.")]
        [OptionalRef][SerializeField] RunHud runHud;
        [Tooltip("The numbers when the session has no GameData (opening the scene straight from the Editor).")]
        [OptionalRef][SerializeField] RunDef fallbackDef;

        [Header("Input")]
        [SerializeField] InputActionAsset controls;
        [SerializeField] string actionMap = "Gameplay";
        [Tooltip("The five actions, in curse order 1-5: Magnetic, Nausea, Slippery, Blindness, Heavy.")]
        [SerializeField] string[] curseActionNames = { "Curse1", "Curse2", "Curse3", "Curse4", "Curse5" };

        readonly InputAction[] _actions = new InputAction[5];
        float _readyAt;

        RunDef Def
        {
            get
            {
                GameData data = authority != null && authority.Session != null ? authority.Session.Data : null;
                if (data != null && data.run != null) return data.run;
                return fallbackDef;
            }
        }

        float Cooldown { get { RunDef d = Def; return d != null ? d.curseCooldown : 30f; } }

        bool IsMage
        {
            get
            {
                return authority != null && authority.LocalRole == LabyrinthRole.Mage
                    && (runHud == null || runHud.IsAwake);
            }
        }

        /// <summary>The pointer belongs to the game: a key here is a key in the world, not in a menu.</summary>
        bool PointerInWorld
        {
            get { return orbitCamera != null && orbitCamera.InputEnabled && orbitCamera.PointerLocked; }
        }

        void OnEnable()
        {
            if (controls != null)
            {
                InputActionMap map = controls.FindActionMap(actionMap, false);
                for (int i = 0; i < _actions.Length; i++)
                {
                    string name = curseActionNames != null && i < curseActionNames.Length ? curseActionNames[i] : null;
                    _actions[i] = map != null && !string.IsNullOrEmpty(name) ? map.FindAction(name, false) : null;
                    if (_actions[i] != null) _actions[i].Enable();
                }
            }
            if (authority != null) authority.CurseRefused += OnRefused;
        }

        void OnDisable()
        {
            if (authority != null) authority.CurseRefused -= OnRefused;
        }

        void Update()
        {
            bool mage = IsMage;
            if (mage && runHud != null) runHud.SetCurseCooldown(_readyAt, Cooldown);
            if (!mage || !PointerInWorld) return;

            for (int i = 0; i < _actions.Length; i++)
            {
                if (_actions[i] == null || !_actions[i].WasPressedThisFrame()) continue;
                Fire((CurseKind)(i + 1));
                return;
            }
        }

        // ---------------------------------------------------------------- the key

        void Fire(CurseKind kind)
        {
            byte slot;
            Vector3 at;
            RunDef def = Def;
            float cone = def != null ? def.curseAimCone : 8f;
            // Nothing in the cone: nothing happens and nothing is sent, as for anybody else's key.
            if (!MageAim.FindTarget(authority, orbitCamera != null ? orbitCamera.transform : null, cone, out slot, out at)) return;

            float left = _readyAt - Time.time;
            if (left > 0f)
            {
                Note(Text(CurseRefusal.Cooldown, left));
                DebugGate.Log("curse: refused locally - recharging, " + left.ToString("0.0") + " s left");
                return;
            }
            Vector3 body;
            float range = def != null ? def.curseRange : 15f;
            if (MageAim.TryBody(spawner, out body) && (at - body).sqrMagnitude > range * range)
            {
                Note(Text(CurseRefusal.OutOfRange, 0f));
                DebugGate.Log("curse: refused locally - OutOfRange");
                return;
            }
            if (!authority.RequestCurse(slot, kind)) return;
            // Optimistic: the bar's cooldown starts on the ASK. A refusal puts it right.
            _readyAt = Time.time + Cooldown;
            if (runHud != null) runHud.PulseAbility(AbilityCooldownSource.Curse, kind);
            DebugGate.Log("curse: " + kind + " requested on " + (slot == NudgeReqMsg.PracticeTarget ? "the practice dummy" : "slot " + slot));
        }

        void OnRefused(CurseRefusal reason, byte targetSlot, float secondsLeft)
        {
            // Refused for anything but the cooldown, the host did not spend it; on the cooldown, it says how long.
            _readyAt = reason == CurseRefusal.Cooldown ? Time.time + secondsLeft : Time.time;
            Note(Text(reason, secondsLeft));
        }

        static string Text(CurseRefusal reason, float secondsLeft)
        {
            switch (reason)
            {
                case CurseRefusal.OutOfRange: return "too far";
                case CurseRefusal.Cooldown: return "recharging " + Mathf.CeilToInt(Mathf.Max(0.1f, secondsLeft)) + " s";
                case CurseRefusal.NoSuchCurse: return "no such curse";
                default: return "nothing to curse there";
            }
        }

        void Note(string text)
        {
            if (runHud != null) runHud.ShowNote(text);
        }
    }
}
