using Pesky.Data;
using Pesky.Protocol;
using Pesky.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pesky.Game
{
    /// <summary>
    /// The Mage's mid-air power, on the client, and the soul's wisp.
    ///
    /// AIMING. No crosshair: the target is the player body (or the tutorial dummy) nearest the centre of the
    /// screen inside LabyrinthDef.nudgeAimCone. While it is eligible - in the air, within nudgeRange of the
    /// Mage's own body, and in his line of sight by a local World raycast - a small marker floats over it,
    /// on the Mage's machine alone. LEFT click (Nudge) pushes it along his view, RIGHT click (Pull) draws it
    /// toward him. The host checks everything again (LabyrinthRule) and refusals come back to him alone as
    /// a short, quiet line on his screen.
    ///
    /// NOT A TELL. For a weapon these clicks do nothing and send nothing, exactly like a Mage clicking at
    /// empty air. The clicks count only while the pointer is locked to the game and the Tab overlay is shut,
    /// and were so on the previous frame too, so the click that locks the pointer (everybody's), and every
    /// click on the map or the scratch pad, can never become a nudge. E / Q / Space are untouched.
    ///
    /// THE WISP. Every NUDGE_EVENT reaches every peer. A peer whose player is a free soul right then shows a
    /// faint streak at the target for about a second; a weapon sees nothing. Nothing says who did it.
    ///
    /// The cooldown shows inside the Mage's MAP tab in the labyrinth (LabyrinthHud) and as the Nudge / Pull
    /// slot of the ability bar at the bottom of the screen in the run (RunHud); never by the crosshair.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MageNudge : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] WorldAuthority authority;
        [SerializeField] PlayerSpawner spawner;
        [Tooltip("Where the tunables come from when the session has no GameData (LabyrinthDef). The session's own data is read first.")]
        [OptionalRef][SerializeField] LabyrinthDirector labyrinth;
        [Tooltip("Labyrinth mode: tells us whether the Tab overlay is open, shows the refusal line and the MAP tab's cooldown ring. Empty in the run.")]
        [OptionalRef][SerializeField] LabyrinthHud hud;
        [Tooltip("Run mode: shows the refusal line and the Nudge / Pull slot of the ability bar. Empty in the labyrinth.")]
        [OptionalRef][SerializeField] RunHud runHud;
        [SerializeField] OrbitCamera orbitCamera;

        [Header("Input")]
        [SerializeField] InputActionAsset controls;
        [SerializeField] string actionMap = "Gameplay";
        [SerializeField] string nudgeActionName = "Nudge";
        [SerializeField] string pullActionName = "Pull";

        [Header("Look")]
        [Tooltip("The Mage-only marker over an eligible target (no collider).")]
        [SerializeField] GameObject markerPrefab;
        [Tooltip("The soul-only streak left where a nudge landed.")]
        [SerializeField] NudgeWisp wispPrefab;
        [SerializeField] float markerHeight = 1.1f;
        [SerializeField] float markerSpinDegreesPerSecond = 90f;
        [Tooltip("What blocks the local line-of-sight pre-check: World only, like the host's.")]
        [SerializeField] LayerMask sightMask = 256;

        InputAction _nudge;
        InputAction _pull;
        bool _armedLastFrame;
        float _readyAt;
        GameObject _marker;
        readonly NudgeWisp[] _wisps = new NudgeWisp[4];

        static readonly Vector3 Lift = new Vector3(0f, 0.3f, 0f);

        void OnEnable()
        {
            if (controls != null)
            {
                InputActionMap map = controls.FindActionMap(actionMap, false);
                _nudge = map != null ? map.FindAction(nudgeActionName, false) : null;
                _pull = map != null ? map.FindAction(pullActionName, false) : null;
                if (_nudge != null) _nudge.Enable();
                if (_pull != null) _pull.Enable();
            }
            if (authority != null)
            {
                authority.Nudged += OnNudged;
                authority.NudgeRefused += OnRefused;
            }
        }

        void OnDisable()
        {
            if (authority != null)
            {
                authority.Nudged -= OnNudged;
                authority.NudgeRefused -= OnRefused;
            }
            if (_marker != null) _marker.SetActive(false);
            _armedLastFrame = false;
        }

        LabyrinthDef Def
        {
            get
            {
                GameData data = authority != null && authority.Session != null ? authority.Session.Data : null;
                if (data != null && data.labyrinth != null) return data.labyrinth;
                return labyrinth != null ? labyrinth.Def : null;
            }
        }

        bool HudAwake { get { return hud != null ? hud.IsAwake : (runHud == null || runHud.IsAwake); } }

        bool HudOverlayOpen { get { return hud != null && hud.OverlayOpen; } }

        float Cooldown { get { LabyrinthDef d = Def; return d != null ? d.nudgeCooldown : 8f; } }

        bool IsMage
        {
            get
            {
                return authority != null && authority.LocalRole == LabyrinthRole.Mage && HudAwake;
            }
        }

        /// <summary>The pointer belongs to the game and the overlay is shut: a click here is a click in the world.</summary>
        bool PointerInWorld
        {
            get
            {
                return orbitCamera != null && orbitCamera.InputEnabled && orbitCamera.PointerLocked && !HudOverlayOpen;
            }
        }

        void Update()
        {
            bool mage = IsMage;
            bool armed = mage && PointerInWorld;

            byte slot = Wire.NoSlot;
            Vector3 at = Vector3.zero;
            NudgeRefusal why = NudgeRefusal.NoTarget;
            bool found = armed && FindTarget(out slot, out at);
            bool eligible = found && Eligible(slot, at, out why);
            bool ready = Time.time >= _readyAt;
            ShowMarker(found && eligible && ready, at);

            if (armed && _armedLastFrame)
            {
                bool nudge = _nudge != null && _nudge.WasPressedThisFrame();
                bool pull = !nudge && _pull != null && _pull.WasPressedThisFrame();
                if (nudge || pull) Fire(nudge ? NudgeMode.Nudge : NudgeMode.Pull, found, slot, eligible, why);
            }

            if (mage && hud != null) hud.SetNudgeCooldown(_readyAt, Cooldown);
            else if (mage && runHud != null) runHud.SetNudgeCooldown(_readyAt, Cooldown);
        }

        void LateUpdate()
        {
            // Sampled at the END of the frame: a click that only just locked the pointer, or only just shut
            // the overlay, was not a click in the world.
            _armedLastFrame = IsMage && PointerInWorld;
        }

        // ---------------------------------------------------------------- aiming

        /// <summary>The Mage's own body: his weapon, or his soul.</summary>
        bool TryBody(out Vector3 position)
        {
            return MageAim.TryBody(spawner, out position);
        }

        /// <summary>The body nearest the middle of the screen inside the aim cone: another player's weapon, or the tutorial dummy.</summary>
        bool FindTarget(out byte slot, out Vector3 at)
        {
            LabyrinthDef def = Def;
            return MageAim.FindTarget(authority, orbitCamera != null ? orbitCamera.transform : null,
                def != null ? def.nudgeAimCone : 8f, out slot, out at);
        }

        /// <summary>The local pre-check of the host's rule. The host still decides.</summary>
        bool Eligible(byte slot, Vector3 at, out NudgeRefusal why)
        {
            why = NudgeRefusal.NoTarget;
            Vector3 body;
            if (!TryBody(out body)) return false;
            LabyrinthDef def = Def;

            bool airborne;
            if (slot == NudgeReqMsg.PracticeTarget)
            {
                PracticeDummy dummy = authority.PracticeDummy;
                airborne = dummy != null && dummy.IsAirborne;
            }
            else
            {
                PlayerState p = authority.Session != null && authority.Session.Sim != null ? authority.Session.Sim.Players[slot] : null;
                airborne = p != null && (p.poseFlags & PoseFlags.Airborne) != 0;
            }
            if ((def == null || def.requireAirborne) && !airborne) { why = NudgeRefusal.NotAirborne; return false; }

            float range = def != null ? def.nudgeRange : 12f;
            if ((at - body).sqrMagnitude > range * range) { why = NudgeRefusal.OutOfRange; return false; }

            if (Physics.Linecast(body + Lift, at + Lift, sightMask, QueryTriggerInteraction.Ignore))
            {
                why = NudgeRefusal.NoLineOfSight;
                return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- the click

        void Fire(NudgeMode mode, bool found, byte slot, bool eligible, NudgeRefusal why)
        {
            // Nothing in the cone: nothing happens and nothing is sent, as for anybody else's click.
            if (!found) return;
            float left = _readyAt - Time.time;
            if (left > 0f)
            {
                Note(Text(NudgeRefusal.Cooldown, left));
                DebugGate.Log("nudge: refused locally - recharging, " + left.ToString("0.0") + " s left");
                return;
            }
            if (!eligible)
            {
                Note(Text(why, 0f));
                DebugGate.Log("nudge: refused locally - " + why);
                return;
            }
            Vector3 view = orbitCamera != null ? orbitCamera.transform.forward : Vector3.forward;
            if (!authority.RequestNudge(slot, mode, view)) return;
            // Optimistic, like the bend ring: it starts on the ASK. A refusal puts it right.
            _readyAt = Time.time + Cooldown;
            if (runHud != null) runHud.PulseAbility(AbilityCooldownSource.Nudge, CurseKind.None);
            DebugGate.Log("nudge: " + mode + " requested on "
                + (slot == NudgeReqMsg.PracticeTarget ? "the practice dummy" : "slot " + slot));
        }

        void OnRefused(NudgeRefusal reason, byte targetSlot, float secondsLeft)
        {
            // Refused for anything but the cooldown, the host did not spend it; on the cooldown, it says how long.
            _readyAt = reason == NudgeRefusal.Cooldown ? Time.time + secondsLeft : Time.time;
            Note(Text(reason, secondsLeft));
        }

        static string Text(NudgeRefusal reason, float secondsLeft)
        {
            switch (reason)
            {
                case NudgeRefusal.NotAirborne: return "only while they are in the air";
                case NudgeRefusal.OutOfRange: return "too far";
                case NudgeRefusal.NoLineOfSight: return "you cannot see them";
                case NudgeRefusal.Cooldown: return "recharging " + Mathf.CeilToInt(Mathf.Max(0.1f, secondsLeft)) + " s";
                default: return "nothing to nudge there";
            }
        }

        void Note(string text)
        {
            if (hud != null) hud.ShowNudgeNote(text);
            else if (runHud != null) runHud.ShowNote(text);
        }

        // ---------------------------------------------------------------- what is drawn

        void ShowMarker(bool on, Vector3 at)
        {
            if (!on)
            {
                if (_marker != null && _marker.activeSelf) _marker.SetActive(false);
                return;
            }
            if (_marker == null)
            {
                if (markerPrefab == null) return;
                _marker = Instantiate(markerPrefab, transform);
                _marker.name = markerPrefab.name;
            }
            _marker.transform.position = at + Vector3.up * markerHeight;
            _marker.transform.Rotate(0f, markerSpinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
            if (!_marker.activeSelf) _marker.SetActive(true);
        }

        /// <summary>Every peer hears every nudge. Only a free soul sees anything, and nobody learns who.</summary>
        void OnNudged(byte targetSlot, NudgeMode mode, Vector3 velocityChange)
        {
            PlayerSoul soul = spawner != null ? spawner.LocalSoul : null;
            if (soul == null || soul.IsPossessing || wispPrefab == null) return;
            Vector3 at;
            if (!TargetPosition(targetSlot, out at)) return;
            NudgeWisp wisp = FreeWisp();
            if (wisp != null) wisp.Play(at, velocityChange);
        }

        bool TargetPosition(byte slot, out Vector3 at)
        {
            at = Vector3.zero;
            if (authority == null) return false;
            if (slot == NudgeReqMsg.PracticeTarget)
            {
                PracticeDummy dummy = authority.PracticeDummy;
                if (dummy == null) return false;
                at = dummy.Centre;
                return true;
            }
            WeaponBody w = authority.RemoteWeaponOf(slot);
            if (w != null && w.Body != null)
            {
                at = w.Body.position;
                return true;
            }
            PlayerState p = authority.Session != null && authority.Session.Sim != null ? authority.Session.Sim.Players[slot] : null;
            if (p == null || !p.present || p.poseCount == 0) return false;
            at = p.pos;
            return true;
        }

        NudgeWisp FreeWisp()
        {
            for (int i = 0; i < _wisps.Length; i++)
            {
                if (_wisps[i] == null)
                {
                    _wisps[i] = Instantiate(wispPrefab, transform);
                    _wisps[i].name = wispPrefab.name;
                    _wisps[i].gameObject.SetActive(false);
                    return _wisps[i];
                }
                if (!_wisps[i].Playing) return _wisps[i];
            }
            return _wisps[0];
        }
    }
}
