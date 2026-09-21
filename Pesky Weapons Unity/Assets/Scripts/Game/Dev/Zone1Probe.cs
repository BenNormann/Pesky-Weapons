using System.Collections;
using System.Text;
using Pesky.Data;
using UnityEngine;

namespace Pesky.Game.Dev
{
    /// <summary>
    /// DEV ONLY. Drives the whole SLICE-1 run in play mode through the same public methods the input
    /// actions call (Possess / TryLaunch / Release) and through the WorldAuthority requests, and writes a
    /// pipe separated report. It is dropped onto the Boot scene's bootstrap for a run and removed after;
    /// it is not part of the shipped scenes. It uses runtime lookups because it outlives the scene load.
    /// Take-off points and lift angles are SOLVED from the weapon's own launchSpeed and the authored
    /// geometry, so the run proves the level rather than a hand-picked arc.
    /// </summary>
    public sealed class Zone1Probe : MonoBehaviour
    {
        public string Report = "";
        public bool Finished;

        readonly StringBuilder _sb = new StringBuilder(8192);
        WorldAuthority _auth;
        PlayerSpawner _spawner;
        PlayerSoul _soul;
        OrbitCamera _cam;
        MovementTuning _tune;

        int _enemyDamaged, _enemyDied, _weaponDamaged, _weaponBroken, _weaponRespawned;
        int _pickups, _platesLatched, _doorsOpened, _keysGained, _modifiers, _heals;
        int _jumpsOk, _jumpsFail;

        void Awake() { DontDestroyOnLoad(gameObject); }
        void Start() { StartCoroutine(Run()); }
        void Line(string s) { _sb.Append(s).Append("\n"); Report = _sb.ToString(); }

        // axis(0=x,1=z), dirSign, face coord, lateral coord, take-off top, rise, run, landing depth, landing top
        static readonly float[,] R1 = new float[,]
        {
            { 1f,  1f,   9f,    0f, -2.5f, 0.8f,  8.5f, 3.0f, -1.7f },
            { 0f, -1f,  -3f,   14f, -4.5f, 1.0f, 10.5f, 5.0f, -3.5f },
            { 0f,  1f,   2f,   20f, -2.5f, 1.2f,  9.5f, 6.0f, -1.3f },
            { 0f, -1f,   2f, 22.5f, -1.3f, 1.3f,  5.5f, 9.5f,  0.0f }
        };

        static readonly float[,] R3 = new float[,]
        {
            { 0f, -1f,   -2f,   64f, 0.0f, 1.6f, 8.5f, 4.5f, 1.6f },
            { 1f,  1f,   62f, -4.5f, 1.6f, 1.6f, 1.9f, 5.5f, 3.2f },
            { 1f,  1f, 64.5f, -4.5f, 3.2f, 1.8f, 2.4f, 3.2f, 5.0f }
        };

        IEnumerator Run()
        {
            float t0 = Time.time;
            while (_spawner == null && Time.time - t0 < 20f) { _spawner = FindFirstObjectByType<PlayerSpawner>(); yield return null; }
            if (_spawner == null) { Line("FATAL no PlayerSpawner"); Finished = true; yield break; }
            _auth = FindFirstObjectByType<WorldAuthority>();
            _cam = FindFirstObjectByType<OrbitCamera>();
            while (_spawner.LocalSoul == null) yield return null;
            _soul = _spawner.LocalSoul;
            _soul.InputEnabled = false;
            if (_cam != null) _cam.InputEnabled = false;
            Hook();
            Line("SCENE|" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name +
                 "|soul=" + (_soul != null) + "|authority=" + (_auth != null) + "|camera=" + (_cam != null));
            yield return new WaitForSeconds(1.5f);

            WeaponBody sword = _auth.GetWeapon(101);
            WeaponBody dagger = _auth.GetWeapon(102);
            WeaponBody hammer = _auth.GetWeapon(106);
            _tune = sword.Tuning;

            yield return Grab(sword);
            Line("STEP1 possess Sword|possessing=" + (_soul.Weapon == sword) + "|name=" + Name(sword) + "|hp=" + sword.Hp);
            yield return Chain("R1(Sword)", sword, R1);
            yield return Gap("R1(Sword) Step->cross 4.0m pit", sword, 12f, 0f, -1.7f, -0.8f, 4.0f, 7.5f, -2.5f);
            yield return Hop("R1(Sword) ExitLedge->Hallway_1", sword, new Vector3(0f, 0.25f, 22.6f), 0f, 20f, 0f);

            GoblinBrain g1 = _auth.GetEnemy(501);
            Door d1 = _auth.GetDoor(401);
            sword.Teleport(new Vector3(0f, 0.4f, 32f), Quaternion.identity);
            yield return new WaitForSeconds(0.6f);
            Line("STEP3 entered Room2|goblinAwake=" + g1.IsAwake + "|hp=" + g1.Hp + "|doorOpen=" + d1.IsOpen);
            int swings = 0;
            while (!g1.IsDead && swings < 14) { swings++; yield return Strike(sword, g1); }
            yield return new WaitForSeconds(0.8f);
            Line("STEP3 goblin|attempts=" + swings + "|dead=" + g1.IsDead + "|hp=" + g1.Hp + "|state=" + g1.Current +
                 "|EnemyDamaged=" + _enemyDamaged + "|EnemyDied=" + _enemyDied +
                 "|door401Open=" + d1.IsOpen + "|slide=" + d1.Openness.ToString("0.00"));

            ClockMover mover = FindFirstObjectByType<ClockMover>();
            KeyPickup key = FindFirstObjectByType<KeyPickup>();
            yield return Ride("Sword", sword, mover);
            yield return Chain("R3(Sword)", sword, R3);
            yield return new WaitForSeconds(0.8f);
            Line("STEP4 key|taken=" + key.Taken + "|partyHasKey=" + _auth.HasKey(1) + "|KeyGained=" + _keysGained + "|PickupTaken=" + _pickups);

            yield return Grab(dagger);
            yield return Chain("R1(Dagger)", dagger, R1);
            yield return Gap("R1(Dagger) Step->cross 4.0m pit", dagger, 12f, 0f, -1.7f, -0.8f, 4.0f, 7.5f, -2.5f);
            yield return Chain("R3(Dagger)", dagger, R3);

            yield return Grab(hammer);
            yield return Chain("R1(Hammer)", hammer, R1);
            yield return Gap("R1(Hammer) Step->cross 4.0m pit", hammer, 12f, 0f, -1.7f, -0.8f, 4.0f, 7.5f, -2.5f);

            yield return Traverse("HAMMER Hallway_1 -> Room3 south", hammer, new Vector3(0f, 0.3f, 26f), 52.5f, 26);
            yield return Ride("Hammer", hammer, mover);
            yield return Traverse("HAMMER Room3 north -> Room4 plate", hammer, new Vector3(0f, 0.3f, 61f), 78f, 30);
            PressurePlate plate = FindFirstObjectByType<PressurePlate>();
            Door d4 = _auth.GetDoor(402);
            yield return new WaitForSeconds(0.6f);
            Line("STEP5 plate fly-through|latched=" + plate.Latched + "|mass=" + plate.Mass.ToString("0.0"));
            hammer.Teleport(new Vector3(0f, 0.7f, 81f), Quaternion.identity);
            yield return new WaitForSeconds(2.0f);
            Line("STEP5 plate (Room1 Hammer mass " + hammer.Body.mass + ")|mass=" + plate.Mass.ToString("0.0") +
                 "|threshold=" + plate.MassThreshold + "|latched=" + plate.Latched + "|PlateLatched=" + _platesLatched +
                 "|door402Open=" + d4.IsOpen + "|slide=" + d4.Openness.ToString("0.00"));

            WeaponBody hammer2 = _auth.GetWeapon(108);
            hammer.Teleport(new Vector3(3.5f, 0.6f, 77f), Quaternion.identity);
            yield return new WaitForSeconds(0.5f);
            bool released = _soul.Release();
            yield return new WaitForSeconds(0.4f);
            Line("STEP6 release out of combat|released=" + released + "|broken=" + hammer.IsBroken + "|hp=" + hammer.Hp +
                 "|soulFree=" + !_soul.IsPossessing);
            yield return Grab(hammer2);
            Door dk = _auth.GetDoor(403);
            Line("STEP6 key door|possessing=" + Name(hammer2) + "|open=" + dk.IsOpen + "|slide=" + dk.Openness.ToString("0.00") +
                 "|condition=" + dk.Condition.ConditionMode + "|DoorOpened total=" + _doorsOpened);

            GoblinBrain g2 = _auth.GetEnemy(502), g3 = _auth.GetEnemy(503), g4 = _auth.GetEnemy(504);
            hammer2.Teleport(new Vector3(0f, 0.6f, 94f), Quaternion.identity);
            yield return new WaitForSeconds(0.8f);
            Line("STEP7 entered Room5|woken=" + g2.IsAwake + "," + g3.IsAwake + "," + g4.IsAwake + "|inCombat=" + _soul.InCombat);
            hammer2.Teleport(new Vector3(6f, 0.6f, 102.5f), Quaternion.identity);
            float hpStart = hammer2.Hp, waited = 0f;
            while (hammer2.Hp >= hpStart && waited < 10f) { waited += Time.deltaTime; yield return null; }
            Line("STEP7 took damage|hp=" + hammer2.Hp + "/" + hammer2.MaxHp + "|WeaponDamaged=" + _weaponDamaged +
                 "|inCombat=" + _soul.InCombat + "|goblinState=" + g3.Current + "|after=" + waited.ToString("0.0") + "s");
            bool inCombatAtRelease = _soul.InCombat;
            bool anvilRefused = !_auth.RequestAnvilUse(901, hammer2);
            _soul.Release();
            yield return new WaitForSeconds(0.4f);
            Line("STEP7 release IN COMBAT|inCombat=" + inCombatAtRelease + "|broken=" + hammer2.IsBroken +
                 "|WeaponBroken=" + _weaponBroken + "|soulFree=" + !_soul.IsPossessing + "|anvilRefusedInCombat=" + anvilRefused);

            WeaponBody mace = _auth.GetWeapon(105);
            mace.Teleport(new Vector3(8f, 0.6f, 94f), Quaternion.identity);
            yield return new WaitForSeconds(0.4f);
            yield return Grab(mace);
            _auth.RequestDamageWeapon(mace, 60f, null);
            float hurt = mace.Hp;
            AnvilStation anvil = FindFirstObjectByType<AnvilStation>();
            float w2 = 0f;
            while (_soul.InCombat && w2 < 12f) { w2 += Time.deltaTime; yield return null; }
            float w3 = 0f; bool healed = false;
            while (!healed && w3 < 6f) { _auth.RequestAnvilUse(901, mace); healed = mace.Hp >= mace.MaxHp; w3 += Time.deltaTime; yield return null; }
            Line("STEP8 anvil|nearAnvil=" + (_auth.NearAnvil == anvil) + "|hpBefore=" + hurt + "|hpAfter=" + mace.Hp + "/" + mace.MaxHp +
                 "|Healed=" + _heals + "|outOfCombatAfter=" + w2.ToString("0.0") + "s|chargeSeconds=" + anvil.ChargeSeconds);
            yield return HoldEAtAnvil(mace, anvil);

            RunePickup rune = FindFirstObjectByType<RunePickup>();
            Line("STEP9 rune before|available=" + rune.IsAvailable + "|roomCleared=" + _auth.GetRoom(305).Cleared);
            GoblinBrain[] three = new GoblinBrain[] { g2, g3, g4 };
            for (int i = 0; i < 3; i++)
            {
                int tries = 0;
                while (!three[i].IsDead && tries < 14) { tries++; yield return Strike(mace, three[i]); }
                Line("STEP9 goblin" + (502 + i) + "|dead=" + three[i].IsDead + "|hp=" + three[i].Hp + "|attempts=" + tries);
            }
            yield return new WaitForSeconds(0.5f);
            float dmgBefore = _auth.ImpactDamage(mace.Def.damage, 12f) * mace.DamageMultiplier;
            Line("STEP9 rune after clear|available=" + rune.IsAvailable + "|roomCleared=" + _auth.GetRoom(305).Cleared);
            mace.Teleport(new Vector3(0f, 0.6f, 96f), Quaternion.identity);
            yield return new WaitForSeconds(0.9f);
            float dmgAfter = _auth.ImpactDamage(mace.Def.damage, 12f) * mace.DamageMultiplier;
            Line("STEP9 rune|taken=" + rune.Taken + "|modifiers=" + mace.Modifiers.Count + "|ModifierAttached=" + _modifiers +
                 "|impactDamage@12m/s before=" + dmgBefore.ToString("0.00") + " after=" + dmgAfter.ToString("0.00") +
                 "|multiplier=" + mace.DamageMultiplier.ToString("0.00"));

            _auth.RequestDamageWeapon(mace, 9999f, null);
            yield return new WaitForSeconds(0.5f);
            Line("STEP10 died|broken=" + mace.IsBroken + "|modifiersLost=" + mace.Modifiers.Count + "|soulFree=" + !_soul.IsPossessing +
                 "|WeaponBroken=" + _weaponBroken);
            WeaponBody staff = _auth.GetWeapon(103);
            _soul.Teleport(staff.Body.worldCenterOfMass + Vector3.up * 0.55f);
            yield return new WaitForSeconds(0.4f);
            bool re = _soul.TryPossess();
            yield return new WaitForSeconds(0.3f);
            Line("STEP10 re-possess at the rack|tryPossess=" + re + "|weapon=" + (_soul.Weapon != null ? Name(_soul.Weapon) : "none") +
                 "|partyStillHasKey=" + _auth.HasKey(1));

            float w4 = 0f;
            while (mace.IsBroken && w4 < 13f) { w4 += Time.deltaTime; yield return null; }
            Line("STEP11 respawn|broken=" + mace.IsBroken + "|after=" + w4.ToString("0.0") + "s|hp=" + mace.Hp + "/" + mace.MaxHp +
                 "|distToSlot=" + (mace.HomeSlot != null ? Vector3.Distance(mace.Body.position, mace.HomeSlot.Position).ToString("0.00") : "-") +
                 "|WeaponRespawned=" + _weaponRespawned);

            Line("JUMPS|ok=" + _jumpsOk + "|fail=" + _jumpsFail);
            Line("EVENTS|EnemyDamaged=" + _enemyDamaged + "|EnemyDied=" + _enemyDied + "|WeaponDamaged=" + _weaponDamaged +
                 "|WeaponBroken=" + _weaponBroken + "|WeaponRespawned=" + _weaponRespawned + "|PickupTaken=" + _pickups +
                 "|PlateLatched=" + _platesLatched + "|DoorOpened=" + _doorsOpened + "|KeyGained=" + _keysGained +
                 "|ModifierAttached=" + _modifiers + "|Healed=" + _heals);
            Line("DONE");
            Finished = true;
        }

        // ------------------------------------------------------------------ jump solving

        bool Solve(float v, float rise, float run, float depth, out float lift, out float back)
        {
            lift = 0f; back = 0f;
            float g = _tune != null ? _tune.gravity : 20f;
            float comp = 0.5f * g * Time.fixedDeltaTime;
            float bestM = -1f;
            for (float a = 15f; a <= 80.01f; a += 0.25f)
            {
                float rad = a * Mathf.Deg2Rad;
                float vx = v * Mathf.Cos(rad), vy = v * Mathf.Sin(rad) + comp;
                float disc = vy * vy - 2f * g * rise;
                if (disc <= 0.02f) continue;
                float s = Mathf.Sqrt(disc);
                float xN = vx * (vy - s) / g, xL = vx * (vy + s) / g;
                if (xN > run) continue;
                for (float d = Mathf.Max(xN, 0.1f); d <= run + 0.001f; d += 0.1f)
                {
                    float tF = d / vx;
                    float yF = vy * tF - 0.5f * g * tF * tF;
                    if (yF < rise) continue;
                    float over = xL - d;
                    if (over > depth) continue;
                    float m = Mathf.Min(yF - rise, depth - over);
                    if (m > bestM) { bestM = m; lift = a; back = d; }
                }
            }
            return bestM >= 0f;
        }

        IEnumerator Chain(string tag, WeaponBody w, float[,] spec)
        {
            for (int i = 0; i < spec.GetLength(0); i++)
            {
                int axis = (int)spec[i, 0];
                float dir = spec[i, 1], face = spec[i, 2], lateral = spec[i, 3], startTop = spec[i, 4];
                float rise = spec[i, 5], run = spec[i, 6], depth = spec[i, 7], landTop = spec[i, 8];
                float lift, back;
                if (!Solve(w.Def.launchSpeed, rise, run, depth, out lift, out back))
                {
                    _jumpsFail++;
                    Line("JUMP " + tag + " #" + i + "|NO SOLUTION|rise=" + rise + "|run=" + run + "|depth=" + depth);
                    continue;
                }
                float takeoff = face - dir * back;
                Vector3 from = axis == 0 ? new Vector3(takeoff, startTop + 0.25f, lateral)
                                         : new Vector3(lateral, startTop + 0.25f, takeoff);
                float yaw = axis == 0 ? (dir > 0f ? 90f : 270f) : (dir > 0f ? 0f : 180f);
                yield return Hop(tag + " #" + i + " rise" + rise.ToString("0.0") + " back" + back.ToString("0.0") +
                                 " lift" + lift.ToString("0"), w, from, yaw, lift, landTop);
            }
        }

        IEnumerator Gap(string label, WeaponBody w, float face, float lateral, float startTop, float rise, float gap, float depth, float landTop)
        {
            float g = _tune != null ? _tune.gravity : 20f;
            float comp = 0.5f * g * Time.fixedDeltaTime;
            float bestM = -1f, lift = 45f;
            for (float a = 15f; a <= 80.01f; a += 0.25f)
            {
                float rad = a * Mathf.Deg2Rad;
                float vx = w.Def.launchSpeed * Mathf.Cos(rad), vy = w.Def.launchSpeed * Mathf.Sin(rad) + comp;
                float tF = gap / vx;
                float yF = vy * tF - 0.5f * g * tF * tF;
                if (yF < rise) continue;
                float disc = vy * vy - 2f * g * rise;
                if (disc <= 0f) continue;
                float xL = vx * (vy + Mathf.Sqrt(disc)) / g;
                if (xL > gap + depth) continue;
                float m = Mathf.Min(yF - rise, gap + depth - xL);
                if (m > bestM) { bestM = m; lift = a; }
            }
            if (bestM < 0f) { _jumpsFail++; Line("JUMP " + label + "|NO SOLUTION"); yield break; }
            yield return Hop(label + " lift" + lift.ToString("0"), w, new Vector3(lateral, startTop + 0.25f, face - 0.4f), 0f, lift, landTop);
        }

        IEnumerator Hop(string label, WeaponBody w, Vector3 from, float yaw, float lift, float expectTop)
        {
            w.Teleport(from, Quaternion.identity);
            float settle = 0f;
            while (settle < 1.2f && !(w.IsGrounded && w.Body.linearVelocity.magnitude < 0.6f)) { settle += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.1f);
            Aim(yaw, lift);
            yield return null;
            LaunchResult res = _soul.TryLaunch();
            float peak = w.Body.position.y, t = 0f;
            yield return new WaitForSeconds(0.2f);
            while (t < 6f)
            {
                t += Time.deltaTime;
                if (w.Body.position.y > peak) peak = w.Body.position.y;
                if (w.IsGrounded && w.Body.linearVelocity.magnitude < 0.8f && t > 0.25f) break;
                yield return null;
            }
            Vector3 p = w.Body.position;
            bool made = p.y > expectTop - 0.35f;
            if (made) _jumpsOk++; else _jumpsFail++;
            Line("JUMP " + label + "|" + res + "|apexY=" + peak.ToString("0.00") + "|landY=" + p.y.ToString("0.00") +
                 "|expectTop=" + expectTop.ToString("0.00") + "|landXZ=" + p.x.ToString("0.0") + "," + p.z.ToString("0.0") +
                 "|MADE=" + made);
        }

        IEnumerator Traverse(string label, WeaponBody w, Vector3 from, float targetZ, int maxLaunches)
        {
            w.Teleport(from, Quaternion.identity);
            yield return new WaitForSeconds(0.5f);
            int n = 0; float startZ = w.Body.position.z;
            while (n < maxLaunches && w.Body.position.z < targetZ)
            {
                n++;
                Aim(0f, 40f);
                yield return null;
                _soul.TryLaunch();
                float t = 0f;
                yield return new WaitForSeconds(0.2f);
                while (t < 4f) { t += Time.deltaTime; if (w.IsGrounded && w.Body.linearVelocity.magnitude < 0.8f && t > 0.25f) break; yield return null; }
                Vector3 q = w.Body.position;
                if (Mathf.Abs(q.x) > 1.2f) w.Teleport(new Vector3(0f, q.y + 0.2f, q.z), Quaternion.identity);
            }
            Vector3 e = w.Body.position;
            Line("TRAVERSE " + label + "|launches=" + n + "|z " + startZ.ToString("0.0") + "->" + e.z.ToString("0.0") +
                 "|y=" + e.y.ToString("0.00") + "|reached=" + (e.z >= targetZ - 0.5f));
        }

        IEnumerator Ride(string label, WeaponBody w, ClockMover mover)
        {
            float wait = 0f;
            while (mover.transform.position.z > 55.9f && wait < 12f) { wait += Time.deltaTime; yield return null; }
            w.Teleport(mover.transform.position + new Vector3(0f, 0.35f, 0f), Quaternion.identity);
            yield return new WaitForSeconds(0.4f);
            Vector3 w0 = w.Body.position, p0 = mover.transform.position;
            float t = 0f;
            while (t < 3.5f) { t += Time.deltaTime; yield return null; }
            Vector3 w1 = w.Body.position, p1 = mover.transform.position;
            Line("RIDE " + label + "|platformDz=" + (p1.z - p0.z).ToString("0.000") + "|riderDz=" + (w1.z - w0.z).ToString("0.000") +
                 "|carryError=" + Mathf.Abs((w1.z - w0.z) - (p1.z - p0.z)).ToString("0.000") +
                 "|riderZ=" + w1.z.ToString("0.0") + "|platformZ=" + p1.z.ToString("0.0") +
                 "|stillOnTop=" + (Mathf.Abs(w1.z - p1.z) < 1.8f && w1.y > -0.4f));
            float wait2 = 0f;
            while (mover.transform.position.z < 58.2f && wait2 < 10f) { wait2 += Time.deltaTime; yield return null; }
            Aim(0f, 30f);
            yield return null;
            _soul.TryLaunch();
            float s = 0f;
            yield return new WaitForSeconds(0.2f);
            while (s < 4f) { s += Time.deltaTime; if (w.IsGrounded && w.Body.linearVelocity.magnitude < 0.8f && s > 0.25f) break; yield return null; }
            Line("RIDE " + label + " off|pos=" + w.Body.position.z.ToString("0.0") + "," + w.Body.position.y.ToString("0.00") +
                 "|crossedPit=" + (w.Body.position.z > 60f));
        }

        // ------------------------------------------------------------------ helpers

        static string Name(WeaponBody w) { return w != null && w.Def != null ? w.Def.displayName : (w != null ? w.name : "none"); }

        IEnumerator Grab(WeaponBody w)
        {
            if (_soul.IsPossessing) _soul.Release();
            yield return null;
            _soul.Teleport(w.Body.worldCenterOfMass + Vector3.up * 0.5f);
            yield return null;
            _soul.Possess(w);
            yield return null;
        }

        void Aim(float yaw, float lift)
        {
            if (_cam != null) _cam.SetLook(yaw, LaunchAim.PitchForLift(lift, _tune));
        }

        IEnumerator Strike(WeaponBody w, GoblinBrain enemy)
        {
            if (enemy.IsDead) yield break;
            Vector3 e = enemy.transform.position;
            Vector3 dir = new Vector3(0.6f, 0f, 0.8f).normalized;
            w.Teleport(e - dir * 3.2f + Vector3.up * 0.9f, Quaternion.identity);
            yield return new WaitForSeconds(0.35f);
            Vector3 to = enemy.Centre - w.Body.worldCenterOfMass;
            Aim(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 15f);
            yield return null;
            _soul.TryLaunch();
            float t = 0f;
            while (t < 1.2f && !enemy.IsDead) { t += Time.deltaTime; yield return null; }
        }

        /// <summary>Damages the weapon again and repairs it by really HOLDING the Possess key at the anvil.</summary>
        IEnumerator HoldEAtAnvil(WeaponBody w, AnvilStation anvil)
        {
            _auth.RequestDamageWeapon(w, 50f, null);
            float before = w.Hp;
            float wait = 0f;
            while (_soul.InCombat && wait < 12f) { wait += Time.deltaTime; yield return null; }
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) { Line("STEP8b hold E|NO KEYBOARD DEVICE - skipped"); yield break; }
            // The Editor is not focused while the probe drives the player loop, so the Input System would
            // normally disable the keyboard as a non-background device and swallow the queued events.
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior =
                UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode =
                UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            if (!kb.enabled) UnityEngine.InputSystem.InputSystem.EnableDevice(kb);
            yield return null;
            _soul.InputEnabled = true;
            float held = 0f, peak = 0f;
            while (held < 3.2f && w.Hp < w.MaxHp)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.E));
                held += Time.deltaTime;
                if (anvil.Progress > peak) peak = anvil.Progress;
                yield return null;
            }
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return null;
            _soul.InputEnabled = false;
            Line("STEP8b hold E at the anvil|possessHeldWorked=" + (peak > 0.05f) + "|peakProgress=" + peak.ToString("0.00") +
                 "|heldSeconds=" + held.ToString("0.00") + "|hp " + before + "->" + w.Hp + "/" + w.MaxHp + "|Healed=" + _heals);
        }


        void Hook()
        {
            _auth.EnemyDamaged += delegate(GoblinBrain a, WeaponBody b, float c, Vector3 d) { _enemyDamaged++; };
            _auth.EnemyDied += delegate(GoblinBrain a, WeaponBody b) { _enemyDied++; };
            _auth.WeaponDamaged += delegate(WeaponBody a, float b) { _weaponDamaged++; };
            _auth.WeaponBroken += delegate(WeaponBody a) { _weaponBroken++; };
            _auth.WeaponRespawned += delegate(WeaponBody a) { _weaponRespawned++; };
            _auth.PickupTaken += delegate(int a, WeaponBody b) { _pickups++; };
            _auth.PlateLatched += delegate(PressurePlate a) { _platesLatched++; };
            _auth.DoorOpened += delegate(Door a) { _doorsOpened++; };
            _auth.KeyGained += delegate(int a) { _keysGained++; };
            _auth.ModifierAttached += delegate(WeaponBody a, ModifierDef b) { _modifiers++; };
            _auth.Healed += delegate(WeaponBody a) { _heals++; };
        }
    }
}
