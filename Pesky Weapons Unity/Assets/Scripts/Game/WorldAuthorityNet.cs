using System;
using System.Collections.Generic;
using Pesky.Data;
using Pesky.Protocol;
using Pesky.Session;
using Pesky.Sim;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>A remote player's stand-in as the possessor of a weapon, so IsFree / IsPossessed stay true to the session.</summary>
    public sealed class RemotePossessor : IWeaponPossessor
    {
        public readonly byte slot;
        public RemotePossessor(byte slot) { this.slot = slot; }
        public void NotifyCombat() { }
    }

    /// <summary>
    /// THE BRIDGE between the scene and the NetSession (docs/NETCODE-STATUS.md, stage 2).
    ///
    /// WorldAuthority stays the Game-side facade and keeps every C# event, because every view already
    /// listens to them. What changed is where a Request* goes:
    /// * On the HOST (offline is a host on a loopback session) a Request* validates against the scene and
    ///   then publishes: an intent straight into the host's own validator (possess, release, hit, bat), or a
    ///   host fact through NetSession.HostEmit (weapon damage, kit state). The session applies it to the
    ///   WorldSim and hands it back through EventApplied, and ONLY THERE is the scene changed and the C#
    ///   event raised. Offline that round trip is synchronous, inside the Request* call.
    /// * On a CLIENT a Request* sends the intent (or does nothing, for host decided things) and the very
    ///   same handler fires when the host's event arrives.
    /// Who may report a physical contact: the peer that SIMULATES the weapon. That is the player holding
    /// it, or the host while it is loose. A remote driven (kinematic) copy never reports anything.
    /// </summary>
    public sealed partial class WorldAuthority : IHostWorld
    {
        const float KitClaimRange = 8f;

        [Header("Netcode")]
        [Tooltip("The scene's SessionRunner. Every gameplay scene runs inside a NetSession (offline = loopback host).")]
        [SerializeField] SessionRunner sessionRunner;
        [Tooltip("This scene's labyrinth, when it has one. Empty in Zone1 and the FeelBox, which leaves every labyrinth rule idle.")]
        [OptionalRef][SerializeField] LabyrinthDirector labyrinth;


        readonly RemotePossessor[] _remote = new RemotePossessor[Wire.MaxPlayers];
        readonly List<ModifierDef> _modScratch = new List<ModifierDef>(4);
        NetSession _session;
        uint _seenRev;
        bool _revKnown;
        float _lastBatTime = -999f;

        /// <summary>(fromSlot, targetSlot, velocityChange, point) a validated bat, on every peer.</summary>
        public event Action<byte, byte, Vector3, Vector3> Batted;
        /// <summary>(cellA, cellB) two rooms changed places. Nothing physical moved: the doorways now lead elsewhere.</summary>
        public event Action<int, int> RoomsSwapped;

        /// <summary>(outcome, escaped slot bits, Mage slot bits) the round is over and the Mages are named.</summary>
        public event Action<RoundOutcome, byte, byte> RoundEnded;

        /// <summary>(slot, respawnTick) a player is out of the round for a while.</summary>
        public event Action<byte, uint> PlayerWentDown;

        /// <summary>(slot, anchorSlot, cell) a player is back beside anchorSlot. NoSlot and -1 when there was nobody to join.</summary>
        public event Action<byte, byte, int> PlayerRespawned;

        /// <summary>(slot, legend) somebody's legend changed.</summary>
        public event Action<byte, int> LegendChanged;

        /// <summary>This peer's own compass target changed. It never says who changed it, or that anybody did.</summary>
        public event Action CompassRetargeted;
        /// <summary>(slot) a stroke the host accepted landed on the shared scratch pad, and the sim already holds it.</summary>
        public event Action<byte> PadStroked;

        /// <summary>The shared scratch pad was wiped (a new round, or the host's CLEAR button).</summary>
        public event Action PadCleared;


        /// <summary>This peer learned its own role. Raised on a Mage's client alone.</summary>
        public event Action RoleLearned;


        public NetSession Session { get { return _session; } }
        /// <summary>True on the host and offline: this peer decides shared state.</summary>
        public bool IsHostPeer { get { return _session == null || _session.IsHost; } }
        public byte LocalSlot { get { return _session != null ? _session.LocalSlot : Wire.NoSlot; } }
        /// <summary>The one PlayerSoul on this machine (remote players have views, not souls).</summary>
        public PlayerSoul LocalSoul { get { return _souls.Count > 0 ? _souls[0] : null; } }

        /// <summary>This peer simulates the weapon's physics, so it is the one that reports its contacts.</summary>
        public bool Simulates(WeaponBody weapon)
        {
            if (weapon == null || weapon.IsRemoteDriven) return false;
            return weapon.IsLocallyPossessed || (IsHostPeer && !weapon.IsPossessed);
        }

        /// <summary>The sim's row for an enemy (what a puppet goblin draws), or null.</summary>
        public EnemyState EnemyRow(int enemyId)
        {
            return _session != null && _session.Sim != null ? _session.Sim.Enemies.Get((ushort)enemyId) : null;
        }

        /// <summary>The weapon a remote slot is driving right now, or null.</summary>
        public WeaponBody RemoteWeaponOf(byte slot)
        {
            if (_session == null || _session.Sim == null) return null;
            PlayerState p = _session.Sim.Players[slot];
            if (p == null || p.weaponId == Wire.NoId) return null;
            WeaponBody w = GetWeapon(p.weaponId);
            return w != null && w.IsRemoteDriven ? w : null;
        }

        /// <summary>Where every other player's body is (for anything that must stay live around them). Returns the count.</summary>
        public int CopyRemotePoints(List<Vector3> into)
        {
            into.Clear();
            if (_session == null || _session.Sim == null || !_session.Slots.HasLocalSlot) return 0;
            byte local = _session.LocalSlot;
            PlayerTable players = _session.Sim.Players;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerState p = players[i];
                if (p.present && i != local && p.poseCount > 0) into.Add(p.pos);
            }
            return into.Count;
        }

        // ------------------------------------------------------------------ lifecycle

        /// <summary>Called from Awake. The SessionRunner runs at -1000, so its session already exists.</summary>
        void AwakeNet()
        {
            for (int i = 0; i < _remote.Length; i++) _remote[i] = new RemotePossessor((byte)i);
            _session = sessionRunner != null ? sessionRunner.Session : null;
            if (_session == null)
            {
                Debug.LogError("WorldAuthority has no SessionRunner: nothing shared will change in this scene.", this);
                return;
            }
            _session.EventApplied += OnNetEvent;
            _session.ReplyReceived += OnNetReply;
            _session.HostWorld = this;
            for (int i = 0; i < weapons.Length; i++)
                if (weapons[i] != null) weapons[i].SetNetDriven(this);
            if (_session.IsHost) return;
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i] != null) enemies[i].SetPuppet(true);
        }

        /// <summary>Called from OnDestroy.</summary>
        void OnDestroyNet()
        {
            if (_session == null) return;
            _session.EventApplied -= OnNetEvent;
            _session.ReplyReceived -= OnNetReply;
            if (ReferenceEquals(_session.HostWorld, this)) _session.HostWorld = null;
        }

        /// <summary>Called first in Start: the host tells the session what this level contains.</summary>
        void StartNet()
        {
            if (_session == null || !_session.IsHost) return;

            WorldResetMsg reset = new WorldResetMsg();
            reset.sceneHash = (uint)Animator.StringToHash(gameObject.scene.name);
            _session.HostEmit(reset.Encode());

            WeaponStateMsg msg = new WeaponStateMsg();
            msg.rows = new List<WeaponStateMsg.Row>(weapons.Length);
            for (int i = 0; i < weapons.Length; i++)
            {
                WeaponBody w = weapons[i];
                if (w == null) continue;
                WeaponStateMsg.Row row = new WeaponStateMsg.Row();
                row.weaponId = (ushort)w.Id;
                row.defId = (byte)(w.Def != null ? w.Def.id : 0);
                row.flags = WeaponFlags.None;
                row.ownerSlot = Wire.NoSlot;
                row.hp = w.MaxHp;
                row.maxHp = w.MaxHp;
                row.homeSlot = w.HomeSlot != null ? (ushort)w.HomeSlot.Id : Wire.NoId;
                IReadOnlyList<ModifierDef> mods = w.Modifiers;
                row.mods = new byte[mods.Count];
                for (int m = 0; m < mods.Count; m++) row.mods[m] = (byte)(mods[m] != null ? mods[m].id : 0);
                msg.rows.Add(row);
                if (msg.rows.Count == Wire.MaxBatch)
                {
                    _session.HostEmit(msg.Encode());
                    msg.rows.Clear();
                }
            }
            if (msg.rows.Count > 0) _session.HostEmit(msg.Encode());
        }

        void Update()
        {
            if (_session == null || _session.Sim == null) return;
            uint rev = _session.Sim.Rev;
            if (_revKnown && rev == _seenRev) return;
            _revKnown = true;
            _seenRev = rev;
            if (!_session.IsHost) SyncFromSim();
        }

        /// <summary>
        /// Client only: the tables were replaced (a snapshot, a resync, the host loading the level), so make
        /// the scene agree with the rows. Every step is idempotent, and goblins read their own rows.
        /// </summary>
        void SyncFromSim()
        {
            WorldSim sim = _session.Sim;
            for (int i = 0; i < weapons.Length; i++)
            {
                WeaponBody w = weapons[i];
                if (w == null) continue;
                WeaponState row = sim.Weapons.Get((ushort)w.Id);
                if (row == null) continue;
                if (row.broken)
                {
                    if (!w.IsBroken) { ClearRemote(w); w.Break(); }
                    continue;
                }
                if (w.IsBroken) w.ApplyRespawn();
                w.SetHpFromNet(row.hp);
                _modScratch.Clear();
                GameData data = _session.Data;
                for (int m = 0; data != null && m < row.mods.Count; m++)
                {
                    ModifierDef mod = data.Modifier(row.mods[m]);
                    if (mod != null) _modScratch.Add(mod);
                }
                if (data != null) w.SetModifiersFromNet(_modScratch);
                bool wasFree = w.IsFree;
                ReconcileOwner(w, row.ownerSlot);
                if (row.ownerSlot == Wire.NoSlot && row.hasRest && wasFree)
                    w.PlaceAtRest(row.restPos, row.restRot, Vector3.zero);
            }

            IReadOnlyList<KitEntry> kit = sim.Kit.Rows;
            for (int i = 0; i < kit.Count; i++)
            {
                KitEntry k = kit[i];
                ApplyKit(k.kind, k.pieceId, k.state, k.actor, k.value, k.clockMs, false);
            }
        }

        // ------------------------------------------------------------------ the session's events, on every peer

        void OnNetEvent(byte id, byte[] payload)
        {
            switch (id)
            {
                case MsgId.WeaponOwner: { WeaponOwnerMsg m; if (WeaponOwnerMsg.TryDecode(payload, out m)) OnWeaponOwner(m); break; }
                case MsgId.WeaponBroken: { WeaponBrokenMsg m; if (WeaponBrokenMsg.TryDecode(payload, out m)) OnWeaponBrokenNet(m); break; }
                case MsgId.WeaponRespawned: { WeaponRespawnedMsg m; if (WeaponRespawnedMsg.TryDecode(payload, out m)) OnWeaponRespawnedNet(m); break; }
                case MsgId.WeaponDamaged: { WeaponDamagedMsg m; if (WeaponDamagedMsg.TryDecode(payload, out m)) OnWeaponDamagedNet(m); break; }
                case MsgId.EnemyHealth: { EnemyHealthMsg m; if (EnemyHealthMsg.TryDecode(payload, out m)) OnEnemyHealth(m); break; }
                case MsgId.BatEvent: { BatEventMsg m; if (BatEventMsg.TryDecode(payload, out m)) OnBatEvent(m); break; }
                case MsgId.KitState:
                {
                    KitStateMsg m;
                    if (KitStateMsg.TryDecode(payload, out m)) ApplyKit(m.kind, m.pieceId, m.state, m.actor, m.value, m.clockMs, true);
                    break;
                }
                case MsgId.LabSwapped: { LabSwappedMsg m; if (LabSwappedMsg.TryDecode(payload, out m)) OnLabSwapped(m); break; }
                case MsgId.RoundResult: { RoundResultMsg m; if (RoundResultMsg.TryDecode(payload, out m)) OnRoundResult(m); break; }
                case MsgId.PlayerDown: { PlayerDownMsg m; if (PlayerDownMsg.TryDecode(payload, out m)) OnPlayerDown(m); break; }
                case MsgId.PlayerRespawn: { PlayerRespawnMsg m; if (PlayerRespawnMsg.TryDecode(payload, out m)) OnPlayerRespawn(m); break; }
                case MsgId.Legend: { LegendMsg m; if (LegendMsg.TryDecode(payload, out m)) OnLegend(m); break; }                case MsgId.PadStroke: { PadStrokeMsg m; if (PadStrokeMsg.TryDecode(payload, out m)) OnPadStroke(m); break; }
                case MsgId.PadClear: { if (PadCleared != null) PadCleared(); break; }

            }
        }

        // ------------------------------------------------------------------ possession

        bool NetPossess(PlayerSoul soul, WeaponBody weapon)
        {
            if (soul == null || weapon == null || _session == null) return false;
            if (soul.IsPossessing || !weapon.IsFree) return false;
            PossessReqMsg req = new PossessReqMsg();
            req.weaponId = (ushort)weapon.Id;
            _session.Send(req.Encode());
            // True at once on the host (the round trip is synchronous); a client hears back about a round trip later.
            return soul.IsPossessing;
        }

        bool NetRelease(PlayerSoul soul)
        {
            if (soul == null || !soul.IsPossessing || _session == null) return false;
            WeaponBody weapon = soul.Weapon;
            Rigidbody rb = weapon.Body;
            ReleaseReqMsg req = new ReleaseReqMsg();
            req.weaponId = (ushort)weapon.Id;
            req.pos = rb.position;
            req.rot = rb.rotation;
            req.vel = rb.linearVelocity;
            _session.Send(req.Encode());
            return true;
        }

        /// <summary>Host only: break a weapon outright.</summary>
        bool NetBreak(WeaponBody weapon)
        {
            if (weapon == null || weapon.IsBroken || _session == null || !_session.IsHost) return false;
            WeaponBrokenMsg msg = new WeaponBrokenMsg();
            msg.header = _session.HostHeader();
            msg.weaponId = (ushort)weapon.Id;
            msg.cause = BreakCause.HostForced;
            msg.respawnTick = msg.header.tick + WorldSim.RespawnDelayTicks;
            return _session.HostEmit(msg.Encode());
        }

        void OnWeaponOwner(WeaponOwnerMsg msg)
        {
            WeaponBody weapon = GetWeapon(msg.weaponId);
            if (weapon == null || weapon.IsBroken) return;
            bool wasMine = weapon.IsLocallyPossessed;
            ReconcileOwner(weapon, msg.ownerSlot);
            // A weapon somebody ELSE let go: put it where they left it and let local physics have it.
            if (msg.ownerSlot == Wire.NoSlot && msg.hasPose && !wasMine) weapon.PlaceAtRest(msg.pos, msg.rot, msg.vel);
        }

        /// <summary>Makes the scene agree with "this weapon is held by that slot". Idempotent.</summary>
        void ReconcileOwner(WeaponBody weapon, byte ownerSlot)
        {
            PlayerSoul local = LocalSoul;
            bool mine = ownerSlot != Wire.NoSlot && ownerSlot == LocalSlot;
            if (mine)
            {
                if (local == null || local.Weapon == weapon) return;
                if (local.IsPossessing) ReleaseLocal(local);
                ClearRemote(weapon);
                if (!local.ApplyPossess(weapon)) return;
                if (!_possessed.Contains(weapon)) _possessed.Add(weapon);
                _soulOf[weapon] = local;
                if (Possessed != null) Possessed(local, weapon);
                return;
            }

            if (local != null && local.Weapon == weapon) ReleaseLocal(local);
            if (ownerSlot == Wire.NoSlot || ownerSlot >= _remote.Length)
            {
                ClearRemote(weapon);
                return;
            }

            RemotePossessor holder = _remote[ownerSlot];
            if (ReferenceEquals(weapon.Possessor, holder)) return;
            ClearRemote(weapon);
            for (int i = 0; i < weapons.Length; i++)
                if (weapons[i] != null && ReferenceEquals(weapons[i].Possessor, holder)) ClearRemote(weapons[i]);
            if (!weapon.SetPossessor(holder)) return;
            weapon.SetRemoteDriven(true);
            if (!_possessed.Contains(weapon)) _possessed.Add(weapon);
        }

        void ReleaseLocal(PlayerSoul local)
        {
            WeaponBody weapon = local.Weapon;
            if (weapon == null) return;
            local.ApplyRelease();
            _possessed.Remove(weapon);
            _soulOf.Remove(weapon);
            if (ReleasedWeapon != null) ReleasedWeapon(local, weapon, false);
        }

        void ClearRemote(WeaponBody weapon)
        {
            RemotePossessor holder = weapon.Possessor as RemotePossessor;
            if (holder == null) return;
            weapon.SetRemoteDriven(false);
            weapon.ClearPossessor(holder);
            _possessed.Remove(weapon);
        }

        void OnWeaponBrokenNet(WeaponBrokenMsg msg)
        {
            WeaponBody weapon = GetWeapon(msg.weaponId);
            if (weapon == null || weapon.IsBroken) return;
            weapon.SetRemoteDriven(false);
            // Break raises WeaponBody.Broken: OnWeaponBroken turns it into WeaponBroken + ReleasedWeapon, and the local soul pops out.
            weapon.Break();
        }

        void OnWeaponRespawnedNet(WeaponRespawnedMsg msg)
        {
            WeaponBody weapon = GetWeapon(msg.weaponId);
            if (weapon != null) weapon.ApplyRespawn();
        }

        // ------------------------------------------------------------------ damage

        bool NetHitEnemy(int weaponId, int enemyId, float relativeSpeed, Vector3 point)
        {
            WeaponBody weapon = GetWeapon(weaponId);
            GoblinBrain enemy = GetEnemy(enemyId);
            if (weapon == null || enemy == null || _session == null) return false;
            if (weapon.IsBroken || enemy.IsDead || !Simulates(weapon)) return false;

            // The same two filters the host applies, run here first so a gentle touch or a rattle never becomes a claim.
            if (ImpactDamage(weapon.Def != null ? weapon.Def.damage : 0f, relativeSpeed) <= 0f) return false;
            long key = ((long)weaponId << 32) ^ (uint)enemyId;
            float last;
            if (_hitTimes.TryGetValue(key, out last) && Time.time - last < hitCooldown) return false;
            _hitTimes[key] = Time.time;

            HitClaimMsg claim = new HitClaimMsg();
            claim.enemyId = (ushort)enemyId;
            claim.weaponId = (ushort)weaponId;
            claim.relativeSpeed = relativeSpeed;
            claim.point = point;
            _session.Send(claim.Encode());
            return true;
        }

        void OnEnemyHealth(EnemyHealthMsg msg)
        {
            GoblinBrain enemy = GetEnemy(msg.enemyId);
            if (enemy == null) return;
            WeaponBody weapon = GetWeapon(msg.weaponId);

            GoblinBrain.HitOutcome outcome = new GoblinBrain.HitOutcome();
            outcome.damage = msg.damage;
            outcome.newHp = msg.newHp;
            outcome.newShieldHp = msg.newShieldHp;
            outcome.hitShield = (msg.flags & HitFlags.HitShield) != 0;
            outcome.shieldBroke = (msg.flags & HitFlags.ShieldBroke) != 0;
            outcome.blocked = (msg.flags & HitFlags.Blocked) != 0;

            enemy.ApplyHit(outcome, msg.knock, weapon);
            if (weapon != null) weapon.NotifyCombat();
            if (EnemyDamaged != null) EnemyDamaged(enemy, weapon, outcome.damage, msg.point);
            if (outcome.shieldBroke) RaiseShieldBroken(enemy);

            if ((msg.flags & HitFlags.Died) == 0) return;
            SetTargeting(enemy, null);
            enemy.Kill();
            if (EnemyDied != null) EnemyDied(enemy, weapon);
            EvaluateDoors();
        }

        /// <summary>A goblin's strike landed (host brains only): the damage and the shove travel as one WEAPON_DAMAGED.</summary>
        public bool RequestEnemyAttack(GoblinBrain source, WeaponBody weapon, float damage, Vector3 knock)
        {
            return NetDamageWeapon(weapon, damage, source, knock);
        }

        /// <summary>Host only. Hit points are a host fact; the new value is worked out from the sim's row.</summary>
        bool NetDamageWeapon(WeaponBody weapon, float amount, GoblinBrain source, Vector3 knock)
        {
            if (weapon == null || weapon.IsBroken || amount <= 0f) return false;
            if (_session == null || !_session.IsHost) return false;
            WeaponState row = _session.Sim.Weapons.Get((ushort)weapon.Id);
            float hp = row != null ? row.hp : weapon.Hp;

            WeaponDamagedMsg msg = new WeaponDamagedMsg();
            msg.header = _session.HostHeader();
            msg.weaponId = (ushort)weapon.Id;
            msg.enemyId = source != null ? (ushort)source.SceneId : Wire.NoId;
            msg.damage = amount;
            msg.newHp = Mathf.Max(0f, hp - amount);
            msg.knock = knock;
            return _session.HostEmit(msg.Encode());
        }

        void OnWeaponDamagedNet(WeaponDamagedMsg msg)
        {
            WeaponBody weapon = GetWeapon(msg.weaponId);
            if (weapon == null || weapon.IsBroken) return;
            weapon.SetHpFromNet(msg.newHp);
            if (WeaponDamaged != null) WeaponDamaged(weapon, msg.damage);
            // The shove is applied by whoever simulates the weapon: its owner, or the host while it is loose.
            if (msg.knock.sqrMagnitude > 0.0001f && Simulates(weapon)) weapon.Knockback(msg.knock);
        }

        // ------------------------------------------------------------------ friend ballistics

        /// <summary>
        /// My weapon struck another player's remote view. The shove is the relative velocity scaled by my
        /// share of the two masses (momentum transfer), made a little bouncy, and clamped. The host checks
        /// it and the target's own machine applies it.
        /// </summary>
        public bool RequestBat(WeaponBody mine, WeaponBody theirs, Vector3 relativeVelocity, Vector3 point)
        {
            if (_session == null || mine == null || theirs == null || mine.Body == null || theirs.Body == null) return false;
            RemotePossessor holder = theirs.Possessor as RemotePossessor;
            if (holder == null || !mine.IsLocallyPossessed) return false;

            GameData data = _session.Data;
            float minSpeed = data != null ? data.batMinSpeed : 4f;
            float maxSpeed = data != null ? data.batMaxSpeed : 18f;
            float bounce = data != null ? data.batRestitution : 0.5f;
            if (relativeVelocity.magnitude < minSpeed) return false;
            if (Time.time - _lastBatTime < 0.25f) return false;
            _lastBatTime = Time.time;

            float mA = Mathf.Max(0.01f, mine.Body.mass);
            float mB = Mathf.Max(0.01f, theirs.Body.mass);
            Vector3 dv = relativeVelocity * ((1f + bounce) * mA / (mA + mB));

            BatClaimMsg claim = new BatClaimMsg();
            claim.targetSlot = holder.slot;
            claim.velocityChange = Vector3.ClampMagnitude(dv, maxSpeed);
            claim.point = point;
            _session.Send(claim.Encode());
            return true;
        }

        void OnBatEvent(BatEventMsg msg)
        {
            if (msg.targetSlot == LocalSlot)
            {
                PlayerSoul local = LocalSoul;
                if (local != null && local.IsPossessing) local.Weapon.Knockback(msg.velocityChange);
            }
            if (Batted != null) Batted(msg.fromSlot, msg.targetSlot, msg.velocityChange, msg.point);
        }

        // ------------------------------------------------------------------ kit: one generic path

        /// <summary>
        /// The one way a kit piece changes. Host: fill in the extras and publish KIT_STATE. Client: send
        /// KIT_REQ and wait for the host's KIT_STATE. The caller has already checked the piece's own rule.
        /// </summary>
        bool SubmitKit(KitKind kind, int pieceId, byte state, WeaponBody actor, float speed)
        {
            if (_session == null) return false;
            ushort actorId = actor != null ? (ushort)actor.Id : Wire.NoId;
            if (_session.IsHost)
            {
                KitStateMsg msg = new KitStateMsg();
                msg.header = _session.HostHeader();
                msg.kind = kind;
                msg.pieceId = (ushort)pieceId;
                msg.state = state;
                msg.actor = actorId;
                FillKit(ref msg);
                return _session.HostEmit(msg.Encode());
            }

            KitReqMsg req = new KitReqMsg();
            req.kind = kind;
            req.pieceId = (ushort)pieceId;
            req.state = state;
            req.actor = actorId;
            req.speed = speed;
            _session.Send(req.Encode());
            return true;
        }

        /// <summary>Host only: the extras a KIT_STATE carries that only the scene knows.</summary>
        void FillKit(ref KitStateMsg msg)
        {
            switch (msg.kind)
            {
                case KitKind.Rune:
                {
                    RunePickup rune;
                    if (_runeById.TryGetValue(msg.pieceId, out rune) && rune.Modifier != null) msg.value = rune.Modifier.id;
                    break;
                }
                case KitKind.Rope:
                {
                    Rope rope = GetRope(msg.pieceId);
                    if (rope != null && rope.Clock != null) msg.clockMs = (uint)Math.Max(0L, rope.Clock.Ms);
                    break;
                }
                case KitKind.Lift:
                {
                    Lift lift = GetLift(msg.pieceId);
                    if (lift != null && lift.Clock != null) msg.clockMs = (uint)Math.Max(0L, lift.Clock.Ms);
                    break;
                }
            }
        }

        bool Near(WeaponBody actor, Component piece)
        {
            if (actor == null || actor.Body == null || piece == null) return false;
            return (actor.Body.position - piece.transform.position).sqrMagnitude <= KitClaimRange * KitClaimRange;
        }

        // ---- the labyrinth ----

        /// <summary>This scene's labyrinth, or null in a scene that has none.</summary>
        public LabyrinthDirector Labyrinth { get { return labyrinth; } }

        /// <summary>This peer's OWN secret role. It exists nowhere else on this machine and is never sent on.</summary>
        public LabyrinthRole LocalRole
        {
            get { return _session != null && _session.Sim != null ? _session.Sim.Labyrinth.LocalRole : LabyrinthRole.Weapon; }
        }

        /// <summary>The Mage's map drag: exchange the rooms in two orthogonally adjacent cells. The host drops it in silence when this peer is not a Mage, so a weapon learns nothing by trying.</summary>
        public void RequestRoomSwap(int cellA, int cellB)
        {
            if (_session == null || cellA < 0 || cellB < 0) return;
            SwapReqMsg req = new SwapReqMsg();
            req.cellA = (ushort)cellA;
            req.cellB = (ushort)cellB;
            _session.Send(req.Encode());
        }

        /// <summary>
        /// The Mage's lie: point these players' compasses somewhere else. slotMask is one bit per player
        /// slot and GoodEnd un-bends them. The host enforces the cooldown and the minority limit, and
        /// answers only the players themselves - never this peer, which is why there is no result here.
        /// </summary>
        public void RequestCompassBend(byte slotMask, CompassTargetKind target, int cell)
        {
            if (_session == null) return;
            CompassBendReqMsg req = new CompassBendReqMsg();
            req.slots = slotMask;
            req.target = target;
            req.cell = cell >= 0 ? (ushort)cell : (ushort)0;
            _session.Send(req.Encode());
        }

        /// <summary>Host only: a player is out of the round. It costs them all their legend and starts the respawn wait; the host rule brings them back beside the group.</summary>
        public bool ReportPlayerDown(byte slot)
        {
            if (_session == null || !_session.IsHost || slot >= Wire.MaxPlayers) return false;
            if (_session.Sim != null && _session.Sim.Labyrinth.IsDown(slot)) return false;
            LabyrinthDef def = _session.Data != null ? _session.Data.labyrinth : null;
            _session.HostEmit(Pesky.Session.Rules.LabyrinthRule.DownPayload(_session.HostHeader(), def, slot));
            _session.HostEmit(Pesky.Session.Rules.LabyrinthRule.LegendPayload(_session.HostHeader(), slot,
                def != null ? def.legendOnDeath : 0));
            return true;
        }

        /// <summary>Host only: set a player's legend outright. Absolute, never a delta, so a dropped event cannot drift.</summary>
        public bool ReportLegend(byte slot, int legend)
        {
            if (_session == null || !_session.IsHost || slot >= Wire.MaxPlayers) return false;
            return _session.HostEmit(Pesky.Session.Rules.LabyrinthRule.LegendPayload(_session.HostHeader(), slot, legend));
        }

        void OnLabSwapped(in LabSwappedMsg msg)
        {
            if (RoomsSwapped != null) RoomsSwapped(msg.cellA, msg.cellB);
        }

        void OnRoundResult(in RoundResultMsg msg)
        {
            if (RoundEnded != null) RoundEnded(msg.outcome, msg.escapedMask, msg.mageMask);
        }

        void OnPlayerDown(in PlayerDownMsg msg)
        {
            if (PlayerWentDown != null) PlayerWentDown(msg.slot, msg.respawnTick);
        }

        void OnPlayerRespawn(in PlayerRespawnMsg msg)
        {
            int cell = msg.cell == Wire.NoId ? -1 : msg.cell;
            if (PlayerRespawned != null) PlayerRespawned(msg.slot, msg.anchorSlot, cell);
        }

        void OnLegend(in LegendMsg msg)
        {
            if (LegendChanged != null) LegendChanged(msg.slot, msg.legend);
        }
        // ---- the shared scratch pad ----

        /// <summary>The team's shared drawing, as this peer has it. Null outside a session.</summary>
        public ScratchPadState Pad
        {
            get { return _session != null && _session.Sim != null ? _session.Sim.Pad : null; }
        }

        /// <summary>
        /// Ask the host to add one stroke to the shared pad. Points are normalised canvas coordinates as
        /// u16 (x, y pairs) and at most PadStrokeReqMsg.MaxPoints of them ride one message, so a long
        /// stroke is split by the caller into several that share their joining point. The host checks the
        /// size and the rate and drops the rest in silence.
        /// </summary>
        public void RequestPadStroke(ushort[] points, bool erase, byte width)
        {
            if (_session == null || points == null || points.Length < 2) return;
            PadStrokeReqMsg req = new PadStrokeReqMsg();
            req.flags = erase ? PadFlags.Erase : PadFlags.None;
            req.width = width > PadStrokeReqMsg.MaxWidth ? PadStrokeReqMsg.MaxWidth : width;
            req.points = points;
            _session.Send(req.Encode());
        }

        /// <summary>Host only: wipe the shared pad for everybody. A client's CLEAR button is not shown at all.</summary>
        public bool RequestPadClear()
        {
            if (_session == null || !_session.IsHost) return false;
            return _session.HostEmit(Pesky.Session.Rules.PadRule.ClearPayload(_session.HostHeader()));
        }

        void OnPadStroke(in PadStrokeMsg msg)
        {
            if (PadStroked != null) PadStroked(msg.slot);
        }


        /// <summary>
        /// A reply the host addressed to this peer alone. The session has already applied it to this sim;
        /// this only lets the HUD notice. Nothing here is forwarded, echoed or logged: it is a secret.
        /// </summary>
        void OnNetReply(byte id, byte[] payload)
        {
            if (id == MsgId.RoleAssign) { if (RoleLearned != null) RoleLearned(); }
            else if (id == MsgId.CompassTargets) { if (CompassRetargeted != null) CompassRetargeted(); }
        }

        // ---- IHostWorld ----

        /// <summary>Host only: where every player is in the labyrinth, answered by this scene's LabyrinthDirector. False in a scene without one, which leaves every labyrinth round rule idle.</summary>
        public bool CollectPlayerCells(int[] cellBySlot, bool[] atExitBySlot)
        {
            return labyrinth != null && labyrinth.CollectPlayerCells(cellBySlot, atExitBySlot);
        }

        /// <summary>Host only: a client's KIT_REQ, checked against this scene with the claimant's weapon as the host sees it.</summary>
        public bool ValidateKit(byte fromSlot, float speed, ref KitStateMsg proposal)
        {
            WeaponBody actor = GetWeapon(proposal.actor);
            int id = proposal.pieceId;
            switch (proposal.kind)
            {
                case KitKind.Key:
                {
                    KeyPickup key;
                    if (!_keyById.TryGetValue(id, out key) || key.Taken || !Near(actor, key)) return false;
                    proposal.state = 1;
                    break;
                }
                case KitKind.Rune:
                {
                    RunePickup rune;
                    if (!_runeById.TryGetValue(id, out rune) || rune.Taken || !rune.IsAvailable || rune.Modifier == null) return false;
                    if (!Near(actor, rune) || actor.IsBroken) return false;
                    proposal.state = 1;
                    break;
                }
                case KitKind.Anvil:
                {
                    AnvilStation anvil;
                    if (!_anvilById.TryGetValue(id, out anvil) || !Near(actor, anvil) || actor.IsBroken) return false;
                    if (_session.Sim.InCombat(proposal.actor)) return false;
                    proposal.state = 1;
                    break;
                }
                case KitKind.Rope:
                {
                    Rope rope = GetRope(id);
                    if (rope == null || !Near(actor, rope) || !rope.CanCut(actor, speed)) return false;
                    proposal.state = 1;
                    break;
                }
                case KitKind.Pot:
                {
                    Pot pot = GetPot(id);
                    if (pot == null || !Near(actor, pot) || !pot.CanSmash(actor, speed)) return false;
                    proposal.state = 1;
                    break;
                }
                case KitKind.Lever:
                {
                    ImpactLever lever = GetLever(id);
                    if (lever == null || !Near(actor, lever) || !lever.CanFlip(actor, speed)) return false;
                    proposal.state = (byte)(lever.IsOn ? 0 : 1);
                    break;
                }
                case KitKind.CrackedWall:
                {
                    CrackedWall wall = GetCrackedWall(id);
                    if (wall == null || wall.IsBroken || !Near(actor, wall) || !wall.CanBreak(actor, speed)) return false;
                    proposal.state = 1;
                    break;
                }
                default:
                    // Doors, plates, magnets, lifts, counterweights, scales, porter gates, lightning and the
                    // porter's carry are decided by the host alone; a client never asks for them.
                    return false;
            }
            FillKit(ref proposal);
            return true;
        }

        /// <summary>Host only: the goblin brains' rows for ENEMY_STATE.</summary>
        public bool CollectEnemyRows(List<EnemyStateMsg.Row> into, bool intervalDue)
        {
            bool dirty = false;
            for (int i = 0; i < enemies.Length; i++)
            {
                GoblinBrain g = enemies[i];
                if (g == null || g.IsPuppet) continue;
                EnemyStateMsg.Row row;
                bool changed;
                if (!g.NetCollect(intervalDue, out row, out changed)) continue;
                into.Add(row);
                dirty |= changed;
            }
            return dirty;
        }

        /// <summary>
        /// KIT_STATE on every peer: change the piece, raise the C# event. Every case is idempotent (a piece
        /// already in that state is left alone) so a snapshot can be replayed through it. live = false
        /// skips the purely cosmetic kinds.
        /// </summary>
        void ApplyKit(KitKind kind, int id, byte state, ushort actorId, int value, uint clockMs, bool live)
        {
            WeaponBody actor = GetWeapon(actorId);
            bool on = state != 0;
            switch (kind)
            {
                case KitKind.Door:
                {
                    Door door = GetDoor(id);
                    if (door == null || door.IsOpen) return;
                    door.ApplyOpen();
                    if (DoorOpened != null) DoorOpened(door);
                    return;
                }
                case KitKind.Plate:
                {
                    PressurePlate plate;
                    if (!_plateById.TryGetValue(id, out plate) || plate.Latched) return;
                    plate.ApplyMass(Mathf.Max(plate.Mass, plate.MassThreshold));
                    if (PlateLatched != null) PlateLatched(plate);
                    EvaluateDoors();
                    return;
                }
                case KitKind.Key:
                {
                    KeyPickup key;
                    if (!_keyById.TryGetValue(id, out key) || key.Taken) return;
                    key.ApplyTaken();
                    bool isNew = _partyKeys.Add(key.KeyId);
                    if (PickupTaken != null) PickupTaken(id, actor);
                    if (isNew && KeyGained != null) KeyGained(key.KeyId);
                    EvaluateDoors();
                    return;
                }
                case KitKind.Rune:
                {
                    RunePickup rune;
                    if (!_runeById.TryGetValue(id, out rune) || rune.Taken) return;
                    rune.ApplyTaken();
                    if (PickupTaken != null) PickupTaken(id, actor);
                    if (actor == null || rune.Modifier == null || !live) return;
                    actor.AddModifier(rune.Modifier);
                    if (ModifierAttached != null) ModifierAttached(actor, rune.Modifier);
                    return;
                }
                case KitKind.Anvil:
                {
                    if (!live || actor == null || actor.IsBroken) return;
                    actor.RestoreFullHp();
                    if (Healed != null) Healed(actor);
                    return;
                }
                case KitKind.Magnet:
                {
                    MagnetZone magnet = GetMagnet(id);
                    if (magnet == null || magnet.IsOn == on) return;
                    magnet.ApplyOn(on);
                    if (MagnetChanged != null) MagnetChanged(magnet, on);
                    return;
                }
                case KitKind.Lift:
                {
                    Lift lift = GetLift(id);
                    if (lift == null || lift.IsOn == on) return;
                    lift.ApplyPower(on, clockMs);
                    if (LiftChanged != null) LiftChanged(lift, on);
                    return;
                }
                case KitKind.Rope:
                {
                    Rope rope = GetRope(id);
                    if (rope == null || rope.IsCut) return;
                    rope.ApplyCut(clockMs);
                    if (RopeCut != null) RopeCut(rope, actor);
                    return;
                }
                case KitKind.Pot:
                {
                    Pot pot = GetPot(id);
                    if (pot == null || pot.IsSmashed) return;
                    pot.ApplySmashed();
                    if (PotSmashed != null) PotSmashed(pot, actor);
                    EvaluateDoors();
                    return;
                }
                case KitKind.Lever:
                {
                    ImpactLever lever = GetLever(id);
                    if (lever == null || lever.IsOn == on) return;
                    lever.ApplySet(on);
                    if (LeverChanged != null) LeverChanged(lever, on);
                    // The Winch variant: turning the lever on switches its Lift on for good (host decides, everyone hears).
                    if (on && lever.Lift != null) RequestSetLift(lever.Lift.SceneId, true);
                    EvaluateDoors();
                    return;
                }
                case KitKind.Counterweight:
                {
                    CounterweightPair pair = GetCounterweight(id);
                    if (pair == null) return;
                    float target = value * 0.001f;
                    if (Mathf.Abs(target - pair.TargetOffset) < 0.0005f) return;
                    pair.ApplyTarget(target, clockMs);
                    if (CounterweightChanged != null) CounterweightChanged(pair, target);
                    return;
                }
                case KitKind.Scales:
                {
                    ScalesLock scalesLock = GetScales(id);
                    if (scalesLock == null || scalesLock.Latched) return;
                    scalesLock.ApplyLatched();
                    if (ScalesLatched != null) ScalesLatched(scalesLock);
                    EvaluateDoors();
                    return;
                }
                case KitKind.CrackedWall:
                {
                    CrackedWall wall = GetCrackedWall(id);
                    if (wall == null || wall.IsBroken) return;
                    wall.ApplyBroken();
                    if (WallBroken != null) WallBroken(wall, actor);
                    EvaluateDoors();
                    return;
                }
                case KitKind.PorterGate:
                {
                    PorterGate gate = GetPorterGate(id);
                    if (gate == null || gate.IsOpen == on) return;
                    gate.ApplyOpen(on);
                    if (PorterGateChanged != null) PorterGateChanged(gate, on);
                    return;
                }
                case KitKind.Lightning:
                {
                    LightningField field = GetLightningField(id);
                    if (!live || field == null || actor == null) return;
                    Vector3 point = actor.Body != null ? actor.Body.worldCenterOfMass : actor.transform.position;
                    if (!IsHostPeer) field.ShowBolt(point);
                    if (LightningStruck != null) LightningStruck(field, actor, point);
                    return;
                }
                case KitKind.PorterCarry:
                {
                    GoblinBrain porter = GetEnemy(id);
                    if (porter == null) return;
                    if (state == 1)
                    {
                        if (actor == null || porter.Carried == actor) return;
                        porter.ApplyCarry(actor);
                        if (PorterPickedUp != null) PorterPickedUp(porter, actor);
                        return;
                    }
                    WeaponBody carried = porter.Carried;
                    if (carried == null) return;
                    porter.ApplyDrop(state == 2);
                    if (PorterDropped != null) PorterDropped(porter, carried, state == 2);
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ the kit requests (called by the thin Request* methods)

        bool NetPickup(int pickupId, WeaponBody taker)
        {
            KeyPickup key;
            if (_keyById.TryGetValue(pickupId, out key))
            {
                if (key.Taken || !Simulates(taker)) return false;
                return SubmitKit(KitKind.Key, pickupId, 1, taker, 0f);
            }
            RunePickup rune;
            if (_runeById.TryGetValue(pickupId, out rune))
            {
                if (rune.Taken || !rune.IsAvailable || taker == null || taker.IsBroken || rune.Modifier == null) return false;
                if (!Simulates(taker)) return false;
                return SubmitKit(KitKind.Rune, pickupId, 1, taker, 0f);
            }
            return false;
        }

        void NetPlateMass(int plateId, float mass)
        {
            PressurePlate plate;
            if (!_plateById.TryGetValue(plateId, out plate) || !IsHostPeer) return;
            if (plate.Latched || mass < plate.MassThreshold) return;
            SubmitKit(KitKind.Plate, plateId, 1, null, 0f);
        }

        bool NetAnvil(int anvilId, WeaponBody weapon)
        {
            AnvilStation anvil;
            if (!_anvilById.TryGetValue(anvilId, out anvil)) return false;
            if (weapon == null || weapon.IsBroken || !Simulates(weapon)) return false;
            PlayerSoul soul = weapon.Possessor as PlayerSoul;
            if (soul == null || soul.InCombat) return false;
            if (weapon.Hp >= weapon.MaxHp) return false;
            if (IsHostPeer && _session != null && _session.Sim.InCombat((ushort)weapon.Id)) return false;
            return SubmitKit(KitKind.Anvil, anvilId, 1, weapon, 0f);
        }

        bool NetDoorCheck(Door door)
        {
            if (door == null || door.IsOpen || !IsHostPeer) return false;
            if (!door.ConditionSatisfied(this)) return false;
            return SubmitKit(KitKind.Door, door.SceneId, 1, null, 0f);
        }

        /// <summary>Host decided on/off pieces (magnet, lift, porter gate, lever set by script).</summary>
        bool NetHostSwitch(KitKind kind, int pieceId, bool on)
        {
            if (!IsHostPeer) return false;
            return SubmitKit(kind, pieceId, (byte)(on ? 1 : 0), null, 0f);
        }

        /// <summary>A contact my weapon made with a one-shot piece (rope, pot, cracked wall) or a lever.</summary>
        bool NetImpact(KitKind kind, int pieceId, byte state, WeaponBody weapon, float speed)
        {
            if (!Simulates(weapon)) return false;
            return SubmitKit(kind, pieceId, state, weapon, speed);
        }

        void NetCounterweight(CounterweightPair pair, float want, long ms)
        {
            if (!IsHostPeer || _session == null) return;
            KitStateMsg msg = new KitStateMsg();
            msg.header = _session.HostHeader();
            msg.kind = KitKind.Counterweight;
            msg.pieceId = (ushort)pair.SceneId;
            msg.state = 1;
            msg.actor = Wire.NoId;
            msg.value = Mathf.RoundToInt(want * 1000f);
            msg.clockMs = (uint)Math.Max(0L, ms);
            _session.HostEmit(msg.Encode());
        }

        bool NetLightning(LightningField field, WeaponBody weapon)
        {
            if (!IsHostPeer) return false;
            if (!NetDamageWeapon(weapon, field.StrikeDamage, null, Vector3.zero)) return false;
            SubmitKit(KitKind.Lightning, field.SceneId, 1, weapon, 0f);
            return true;
        }

        bool NetPorterCarry(GoblinBrain porter, WeaponBody weapon, byte state)
        {
            if (!IsHostPeer || porter == null) return false;
            return SubmitKit(KitKind.PorterCarry, porter.SceneId, state, weapon, 0f);
        }
    }
}
