using System.Collections.Generic;
using Pesky.Data;
using Pesky.Protocol;
using Pesky.Session;
using Pesky.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pesky.Game
{
    /// <summary>
    /// The run's own HUD layer (docs/RUN.md), above the weapon HUD: the TIMER everybody sees, the role reveal
    /// at round start, the result banner, the victim's CURSE line, the BLINDNESS overlay, the GHOST line of a
    /// banished player (docs/VOTING.md), and the ABILITY BAR at the bottom of the screen: everybody's VOTE
    /// slot, plus - for a Mage alone - the nudge and the five curses, built from the <see cref="abilities"/>
    /// list by <see cref="AbilityBar"/>, with the quiet refusal line. Nothing sits by the crosshair. There is
    /// no Tab overlay, no map, no pad and no compass here: those are the labyrinth's (LabyrinthHud), set aside.
    /// The vote screen (VoteScreen) shares this document and lives under vote-root.
    ///
    /// SECRETS. This peer's role lives in its own sim and nowhere else. The Mage slots and the refusal line
    /// are built only while this player is a Mage and are display:none otherwise; the curse line shows the
    /// victim what is on them and never who did it, because nothing on this machine knows. A banished
    /// player's role is public (VOTE_END revealed it) and is said on the ghost line and the result banner.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class RunHud : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] UIDocument document;
        [Tooltip("Applied at runtime as well, so the page is styled even if the UXML's Style tag is lost.")]
        [SerializeField] StyleSheet style;
        [SerializeField] WorldAuthority authority;
        [SerializeField] SessionRunner sessionRunner;
        [Tooltip("The numbers when the session has no GameData (opening the scene straight from the Editor).")]
        [OptionalRef][SerializeField] RunDef fallbackDef;

        [Header("Timing")]
        [Tooltip("How long after the round opens the role reveal appears at the earliest. It waits for this round's ROLE_ASSIGN (every player gets one, Weapon or Mage).")]
        [SerializeField] float roleRevealDelay = 1.5f;
        [Tooltip("If this round's ROLE_ASSIGN has still not arrived this long after roleRevealDelay, reveal anyway (as a weapon, which is what an untold player is).")]
        [SerializeField] float roleWaitSeconds = 3f;
        [SerializeField] float roleRevealSeconds = 6f;
        [SerializeField] float noteSeconds = 1.6f;

        [Header("Text")]
        [SerializeField] string weaponTitle = "YOU ARE A WEAPON";
        [SerializeField] string weaponSub = "five rooms, one exit, five minutes. one of you is not helping. V calls a vote.";
        [SerializeField] string mageTitle = "YOU ARE A FRAGMENT OF THE ARCH MAGE";
        [SerializeField] string mageSub = "1-5 curse whoever you look at. click nudges them in the air. do not get caught: a vote banishes you.";
        [SerializeField] string escapedTitle = "THE WEAPONS ESCAPED";
        [SerializeField] string timedOutTitle = "TIME IS UP - THE ARCH MAGE WINS";
        [SerializeField] string resurrectedTitle = "THE ARCH MAGE WINS";
        [SerializeField] string weaponsGoneTitle = "EVERY WEAPON WAS BANISHED - THE ARCH MAGE WINS";
        [SerializeField] string ghostMageText = "BANISHED - YOU WERE THE ARCH MAGE. SPECTATE AS A GHOST: FLY, PASS DOORS, TOUCH NOTHING.";
        [SerializeField] string ghostWeaponText = "BANISHED - YOU WERE A WEAPON. SPECTATE AS A GHOST: FLY, PASS DOORS, TOUCH NOTHING.";

        [Header("Tutorial")]
        [Tooltip("Draw nothing at all until Wake() is called. The tutorial's Mage room wakes it; a real round leaves this off.")]
        [SerializeField] bool startAsleep;

        [Header("Ability bar")]
        [Tooltip("The ability bar at the bottom of the screen, left to right. Putting a power on it is adding an entry here (docs/RUN.md 6.5); an entry's cooldown source must be one some script pushes with SetCooldown. mageOnly entries show for a Mage alone.")]
        [SerializeField] List<AbilitySlotDef> abilities = AbilityBar.DefaultSlots();
        [Tooltip("How long a slot pulses after its power is used.")]
        [SerializeField] float abilityPulseSeconds = 0.18f;

        // the page
        Label _timer, _roleTitle, _roleSub, _resultTitle, _resultSub, _curseName, _curseTime, _note, _ghost;
        VisualElement _rolePanel, _resultPanel, _cursePanel, _barRoot, _blind;
        AbilityBar _bar;

        NetSession _session;
        bool _awake = true;
        bool _roleShown;
        bool _shownMage;
        float _roundSeenAt = -1f;
        float _roleUntil = -1f;
        bool _roundOver;
        float _noteUntil = -1f;
        bool _barShown;
        bool _ghostShown;
        bool _ghostMageShown;
        bool _timerWarning;
        string _timerText = "";

        // local cooldown guides by AbilityCooldownSource, pushed by MageNudge / MageCurse / VoteCaller. The host owns the real ones.
        readonly float[] _readyAt = new float[AbilityBar.SourceCount];
        readonly float[] _cooldownSeconds = new float[AbilityBar.SourceCount];

        // the curse line, only rewritten when something changed
        bool _curseShown;
        CurseKind _curseKindShown = CurseKind.None;
        int _curseSecondsShown = -1;

        // blindness
        bool _blindOn;
        float _blindRadius = 0.12f;
        float _blindOpacity = 0.96f;

        /// <summary>There is no overlay in the run: a click is always a click in the world.</summary>
        public bool OverlayOpen { get { return false; } }

        /// <summary>False while the tutorial keeps this page asleep (before the Mage room).</summary>
        public bool IsAwake { get { return _awake; } }

        /// <summary>MageNudge's local cooldown guide: the Nudge / Pull slot of the ability bar, Mage only.</summary>
        public void SetNudgeCooldown(float readyAt, float seconds)
        {
            SetCooldown(AbilityCooldownSource.Nudge, readyAt, seconds);
        }

        /// <summary>MageCurse's local cooldown guide (one for all five keys): darkens every curse slot of the bar, Mage only.</summary>
        public void SetCurseCooldown(float readyAt, float seconds)
        {
            SetCooldown(AbilityCooldownSource.Curse, readyAt, seconds);
        }

        /// <summary>Any power's local cooldown guide: when it is ready again (Time.time) and its full length. Every bar slot on that source follows it.</summary>
        public void SetCooldown(AbilityCooldownSource source, float readyAt, float seconds)
        {
            int i = (int)source;
            if (i <= 0 || i >= _readyAt.Length) return;
            _readyAt[i] = readyAt;
            _cooldownSeconds[i] = seconds;
        }

        /// <summary>A word on a source's slots saying why the power cannot be used right now (USED, WAIT, PAUSED, OUT); empty clears it.</summary>
        public void SetAbilityLock(AbilityCooldownSource source, string text)
        {
            if (_bar != null) _bar.SetLock(source, text);
        }

        /// <summary>A power was just used: its slot pulses briefly. For a curse, only that curse's slot.</summary>
        public void PulseAbility(AbilityCooldownSource source, CurseKind curse)
        {
            if (_bar != null && _barShown) _bar.Pulse(source, curse, Time.time, abilityPulseSeconds);
        }

        /// <summary>A short, quiet line under the middle of the screen: why a nudge, a curse or a vote call did not happen. Only this player's own actions ever cause one.</summary>
        public void ShowNote(string text)
        {
            if (_note == null || !_awake) return;
            _note.text = text;
            Show(_note, true);
            _noteUntil = Time.time + noteSeconds;
        }

        /// <summary>The curse on THIS player, pushed every frame by CurseEffects: its name and the seconds left. None hides the line.</summary>
        public void SetCurse(CurseKind kind, float secondsLeft)
        {
            bool on = kind != CurseKind.None && secondsLeft > 0f && _awake;
            if (on != _curseShown)
            {
                _curseShown = on;
                Show(_cursePanel, on);
            }
            if (!on) return;
            if (kind != _curseKindShown)
            {
                _curseKindShown = kind;
                if (_curseName != null) _curseName.text = "CURSED: " + RunDef.CurseName((int)kind);
            }
            int whole = Mathf.CeilToInt(secondsLeft);
            if (whole != _curseSecondsShown)
            {
                _curseSecondsShown = whole;
                if (_curseTime != null) _curseTime.text = whole.ToString() + " s";
            }
        }

        /// <summary>The BLINDNESS curse: darken everything but a circle in the middle. Radius is a fraction of the screen height.</summary>
        public void SetBlindness(bool on, float clearRadius, float opacity)
        {
            _blindOn = on && _awake;
            _blindRadius = clearRadius;
            _blindOpacity = opacity;
            Show(_blind, _blindOn);
            if (_blind != null) _blind.MarkDirtyRepaint();
        }

        void Awake()
        {
            if (document == null) document = GetComponent<UIDocument>();
            _session = sessionRunner != null ? sessionRunner.Session : null;
        }

        void OnEnable()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root == null) return;
            if (style != null && !root.styleSheets.Contains(style)) root.styleSheets.Add(style);

            _timer = root.Q<Label>("timer");
            _rolePanel = root.Q<VisualElement>("role-reveal");
            _roleTitle = root.Q<Label>("role-title");
            _roleSub = root.Q<Label>("role-sub");
            _resultPanel = root.Q<VisualElement>("result-banner");
            _resultTitle = root.Q<Label>("result-title");
            _resultSub = root.Q<Label>("result-sub");
            _cursePanel = root.Q<VisualElement>("curse-status");
            _curseName = root.Q<Label>("curse-name");
            _curseTime = root.Q<Label>("curse-time");
            _note = root.Q<Label>("note");
            _ghost = root.Q<Label>("ghost-status");
            _barRoot = root.Q<VisualElement>("ability-bar");
            _bar = new AbilityBar(_barRoot, abilities);
            _blind = root.Q<VisualElement>("blind-overlay");
            if (_blind != null) _blind.generateVisualContent += OnDrawBlind;

            Show(_rolePanel, false);
            Show(_resultPanel, false);
            Show(_cursePanel, false);
            Show(_note, false);
            Show(_ghost, false);
            Show(_barRoot, false);
            Show(_blind, false);
            Show(_timer, false);
            _barShown = false;
            _ghostShown = false;
            _awake = !startAsleep;

            if (authority != null)
            {
                authority.RoleLearned += OnRoleLearned;
                authority.RoundEnded += OnRoundEnded;
            }
        }

        void OnDisable()
        {
            if (authority != null)
            {
                authority.RoleLearned -= OnRoleLearned;
                authority.RoundEnded -= OnRoundEnded;
            }
            if (_blind != null) _blind.generateVisualContent -= OnDrawBlind;
        }

        void Update()
        {
            if (_session == null && sessionRunner != null) _session = sessionRunner.Session;
            if (!_awake) return;
            RefreshTimer();
            RefreshRole();
            RefreshBar();
            RefreshGhost();
            if (_noteUntil > 0f && Time.time >= _noteUntil)
            {
                _noteUntil = -1f;
                Show(_note, false);
            }
        }

        static void Show(VisualElement element, bool on)
        {
            if (element == null) return;
            element.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        RunDef Def
        {
            get
            {
                GameData data = _session != null ? _session.Data : null;
                if (data != null && data.run != null) return data.run;
                return fallbackDef;
            }
        }

        /// <summary>This round's role, from this peer's own sim. It is Weapon-and-unknown from the round start until this round's ROLE_ASSIGN lands, so nothing of a previous round can show here (round 10).</summary>
        bool IsMage { get { return authority != null && authority.LocalRole == LabyrinthRole.Mage; } }

        bool RoleKnown { get { return authority != null && authority.LocalRoleKnown; } }

        /// <summary>This player was banished: a ghost. Public knowledge (VOTE_END).</summary>
        bool IsGhost { get { return authority != null && authority.LocalIsGhost; } }

        // ---------------------------------------------------------------- the timer

        /// <summary>
        /// mm:ss for everybody. Before the first player leaves the start room it shows the full timer,
        /// dimmed; then it counts down on GAME time (room time with the pauses taken out: SessionRunner.GameMs),
        /// so it stands still through a vote, and turns red inside warningSeconds. Hidden once the round is over.
        /// </summary>
        void RefreshTimer()
        {
            if (_timer == null) return;
            WorldSim sim = _session != null ? _session.Sim : null;
            if (sim == null || sim.Phase != SessionPhase.Playing || _roundOver)
            {
                Show(_timer, false);
                return;
            }
            RunDef def = Def;
            RunState run = sim.Run;
            // No timer until the run's rooms are known (the tutorial's practice hall has no run at all).
            if (run == null || run.Count == 0)
            {
                Show(_timer, false);
                return;
            }
            float total = def != null ? def.timerSeconds : 300f;
            float warning = def != null ? def.warningSeconds : 30f;
            float left;
            bool idle;
            if (run.Started)
            {
                long nowMs = sessionRunner != null && (_session.IsHost || _session.Clock.HasEstimate)
                    ? sessionRunner.GameMs
                    : Protocol.Tick.ToMs(sim.Pause.GameTick(sim.Tick));
                long leftMs = Protocol.Tick.ToMs(run.DeadlineTick) - nowMs;
                left = leftMs > 0 ? leftMs / 1000f : 0f;
                idle = false;
            }
            else
            {
                left = total;
                idle = true;
            }
            int whole = Mathf.CeilToInt(left);
            string text = (whole / 60).ToString() + ":" + (whole % 60).ToString("00");
            if (text != _timerText)
            {
                _timerText = text;
                _timer.text = text;
            }
            bool warn = !idle && left <= warning;
            if (warn != _timerWarning)
            {
                _timerWarning = warn;
                _timer.EnableInClassList("is-warning", warn);
            }
            _timer.EnableInClassList("is-idle", idle);
            Show(_timer, true);
        }

        // ---------------------------------------------------------------- the ability bar

        /// <summary>Everybody's bar while the round runs (the VOTE slot); the Mage slots for a Mage who is not banished. Cooldowns are local guides; the host decides.</summary>
        void RefreshBar()
        {
            WorldSim sim = _session != null ? _session.Sim : null;
            bool show = sim != null && sim.Phase == SessionPhase.Playing && !_roundOver;
            if (show != _barShown)
            {
                _barShown = show;
                Show(_barRoot, show);
            }
            if (!show || _bar == null) return;
            _bar.SetMageSlots(IsMage && !IsGhost);
            _bar.Refresh(Time.time, _readyAt, _cooldownSeconds);
        }

        // ---------------------------------------------------------------- the ghost line

        void RefreshGhost()
        {
            bool ghost = IsGhost && !_roundOver;
            bool mage = ghost && authority.RevealedRole(authority.LocalSlot) == LabyrinthRole.Mage;
            if (ghost == _ghostShown && mage == _ghostMageShown) return;
            _ghostShown = ghost;
            _ghostMageShown = mage;
            if (_ghost != null) _ghost.text = mage ? ghostMageText : ghostWeaponText;
            Show(_ghost, ghost);
        }

        // ---------------------------------------------------------------- blindness

        /// <summary>A dark sheet with a round hole in the middle: the outer rectangle and the circle in one odd-even fill.</summary>
        void OnDrawBlind(MeshGenerationContext ctx)
        {
            if (!_blindOn) return;
            VisualElement element = ctx.visualElement;
            if (element == null) return;
            Rect rect = element.contentRect;
            if (rect.width < 2f || rect.height < 2f) return;
            float radius = Mathf.Max(4f, rect.height * _blindRadius);
            Painter2D painter = ctx.painter2D;
            painter.fillColor = new Color(0f, 0f, 0f, Mathf.Clamp01(_blindOpacity));
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.MoveTo(new Vector2(rect.center.x + radius, rect.center.y));
            painter.Arc(rect.center, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            painter.ClosePath();
            painter.Fill(FillRule.OddEven);
        }

        // ---------------------------------------------------------------- role and result

        void RefreshRole()
        {
            WorldSim sim = _session != null ? _session.Sim : null;
            if (_roundSeenAt < 0f && sim != null && sim.Phase == SessionPhase.Playing) _roundSeenAt = Time.time;

            if (!_roleShown && _roundSeenAt >= 0f)
            {
                // Never before roleRevealDelay, and then as soon as this round's role is known; a role that
                // never comes is shown as a weapon after roleWaitSeconds more.
                float since = Time.time - _roundSeenAt;
                if (since >= roleRevealDelay && (RoleKnown || since >= roleRevealDelay + roleWaitSeconds)) ShowRole();
            }

            if (_roleUntil > 0f && Time.time >= _roleUntil)
            {
                _roleUntil = -1f;
                Show(_rolePanel, false);
            }
        }

        void ShowRole()
        {
            if (!_awake) return;
            _roleShown = true;
            bool mage = IsMage;
            _shownMage = mage;
            if (_roleTitle != null) _roleTitle.text = mage ? mageTitle : weaponTitle;
            if (_roleSub != null) _roleSub.text = mage ? mageSub : weaponSub;
            if (_rolePanel != null) _rolePanel.EnableInClassList("is-mage", mage);
            Show(_rolePanel, true);
            _roleUntil = Time.time + roleRevealSeconds;
        }

        /// <summary>Start drawing. The tutorial's Mage room calls it; the reveal is timed from here.</summary>
        public void Wake()
        {
            if (_awake) return;
            _awake = true;
            _roundSeenAt = -1f;
            _roleShown = false;
        }

        /// <summary>This round's ROLE_ASSIGN arrived. If the reveal already said something else, say it again; otherwise RefreshRole shows it when it is due.</summary>
        void OnRoleLearned()
        {
            if (_roleShown && _shownMage != IsMage) ShowRole();
        }

        void OnRoundEnded(RoundOutcome outcome, byte escapedMask, byte mageMask)
        {
            _roundOver = true;
            Show(_timer, false);
            Show(_barRoot, false);
            Show(_cursePanel, false);
            Show(_blind, false);
            Show(_ghost, false);
            _barShown = false;
            _ghostShown = false;
            if (_resultTitle != null)
            {
                string title = resurrectedTitle;
                if (outcome == RoundOutcome.Escaped) title = escapedTitle;
                else if (outcome == RoundOutcome.TimedOut) title = timedOutTitle;
                else if (outcome == RoundOutcome.WeaponsGone) title = weaponsGoneTitle;
                _resultTitle.text = title;
            }
            if (_resultSub != null) _resultSub.text = MageNames(mageMask) + BanishedNames();
            Show(_resultPanel, true);
        }

        /// <summary>ROUND_RESULT is the one message that ever names the fragments, so this is the one place they can be said.</summary>
        string MageNames(byte mageMask)
        {
            PlayerTable players = _session != null && _session.Sim != null ? _session.Sim.Players : null;
            if (players == null) return string.Empty;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(64);
            for (int i = 0; i < players.Count && i < Wire.MaxPlayers; i++)
            {
                if ((mageMask & (1 << i)) == 0) continue;
                PlayerState p = players[i];
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(p != null && !string.IsNullOrEmpty(p.name) ? p.name : "P" + i);
            }
            if (sb.Length == 0) return "no fragment was drawn";
            sb.Insert(0, "the arch mage was:   ");
            return sb.ToString();
        }

        /// <summary>Who was banished this run, with the role each reveal named (public since its VOTE_END).</summary>
        string BanishedNames()
        {
            WorldSim sim = _session != null ? _session.Sim : null;
            if (sim == null) return string.Empty;
            VoteState vote = sim.Vote;
            PlayerTable players = sim.Players;
            System.Text.StringBuilder sb = new System.Text.StringBuilder(64);
            for (int i = 0; i < Wire.MaxPlayers; i++)
            {
                if (!vote.IsBanished(i)) continue;
                PlayerState p = players[i];
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(p != null && !string.IsNullOrEmpty(p.name) ? p.name : "P" + i);
                sb.Append(vote.RevealedRole(i) == LabyrinthRole.Mage ? " (the mage)" : " (a weapon)");
            }
            if (sb.Length == 0) return string.Empty;
            sb.Insert(0, "\nbanished:   ");
            return sb.ToString();
        }
    }
}
