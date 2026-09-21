using System.Collections;
using System.Globalization;
using System.Text;
using Pesky.Data;
using UnityEngine;

namespace Pesky.Game.Dev
{
    /// <summary>
    /// Dev-scene telemetry for FeelBox. It drives the game through the SAME public methods the input
    /// calls (PlayerSoul.TryPossess / TryLaunch / SetRoll / SetThrust / Release) and measures the
    /// Rigidbody results against ballistics. Start a run from the context menu or from an editor
    /// script in play mode, then read <see cref="Report"/>. Not used by any gameplay code.
    /// </summary>
    public sealed class FeelProbe : MonoBehaviour
    {
        [SerializeField] PlayerSpawner spawner;
        [SerializeField] OrbitCamera orbitCamera;
        [SerializeField] LevelClock clock;
        [SerializeField] MovementTuning tuning;
        [SerializeField] WeaponBody[] weapons;
        [SerializeField] Transform launchOrigin;
        [SerializeField] Transform wallOrigin;
        [SerializeField] ModifierDef testModifier;
        [SerializeField] float launchYaw = 90f;
        [SerializeField] float wallApproachYaw = 270f;
        [SerializeField] float wallJumpYaw = 315f;
        [SerializeField] Vector3 outOfWorldPoint = new Vector3(40f, 2f, 40f);

        readonly StringBuilder _report = new StringBuilder();
        float _settleSeconds;
        bool _settled;
        bool _picked;
        bool _recovered;

        public string Report { get { return _report.ToString(); } }
        public bool Busy { get; private set; }

        PlayerSoul Soul { get { return spawner.LocalSoul; } }

        public void ClearReport() { _report.Length = 0; }

        [ContextMenu("Run All")] public void StartAll() { StartCoroutine(Guard(RunAll())); }
        [ContextMenu("Run Possess + Break")] public void StartPossessBreak() { StartCoroutine(Guard(RunPossessBreak())); }
        [ContextMenu("Run Kill-Y")] public void StartKillY() { StartCoroutine(Guard(RunKillY())); }
        [ContextMenu("Run Soul")] public void StartSoul() { StartCoroutine(Guard(RunSoul())); }
        [ContextMenu("Run Launches")] public void StartLaunches() { StartCoroutine(Guard(RunLaunches())); }
        [ContextMenu("Run Wall Jumps")] public void StartWallJumps() { StartCoroutine(Guard(RunWallJumps())); }
        [ContextMenu("Run Roll")] public void StartRoll() { StartCoroutine(Guard(RunRoll())); }

        IEnumerator Guard(IEnumerator run)
        {
            if (Busy) yield break;
            Busy = true;
            Soul.InputEnabled = false;
            orbitCamera.InputEnabled = false;
            yield return run;
            Soul.InputEnabled = true;
            orbitCamera.InputEnabled = true;
            Busy = false;
            _report.AppendLine("DONE");
        }

        IEnumerator RunAll()
        {
            yield return RunPossessBreak();
            yield return RunKillY();
            yield return RunSoul();
            yield return RunLaunches();
            yield return RunWallJumps();
            yield return RunRoll();
        }

        // ---------------------------------------------------------------- helpers

        static string F(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        static string Pct(float measured, float expected)
        {
            if (Mathf.Abs(expected) < 1e-5f) return "n/a";
            return ((measured - expected) / expected * 100f).ToString("+0.0;-0.0", CultureInfo.InvariantCulture) + "%";
        }
        static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }

        IEnumerator Settle(WeaponBody w, float timeout)
        {
            float start = Time.time;
            float calm = 0f;
            _settled = false;
            while (Time.time - start < timeout)
            {
                yield return new WaitForFixedUpdate();
                Rigidbody rb = w.Body;
                bool still = rb.IsSleeping() || (rb.linearVelocity.magnitude < 0.05f && rb.angularVelocity.magnitude < 0.2f);
                calm = still && w.IsGrounded ? calm + Time.fixedDeltaTime : 0f;
                if (calm >= 0.4f) { _settled = true; break; }
            }
            _settleSeconds = Time.time - start;
        }

        IEnumerator WaitUntilFree()
        {
            if (Soul.IsPossessing)
            {
                while (Soul.InCombat) yield return null;
                Soul.Release();
            }
            while (Soul.InCombat) yield return null;
        }

        /// <summary>Puts the weapon at a spot, lets it settle, then possesses it through TryPossess.</summary>
        IEnumerator Prepare(WeaponBody w, Vector3 spot, float yaw)
        {
            yield return WaitUntilFree();
            while (w.IsBroken) yield return null;
            w.Teleport(spot + Vector3.up * 0.45f, Quaternion.Euler(0f, yaw, 0f));
            yield return Settle(w, 8f);

            Vector3 com = w.Body.worldCenterOfMass;
            orbitCamera.SetLook(yaw, orbitCamera.RestingPitch);
            Soul.Teleport(com - orbitCamera.Forward * 1.2f);
            orbitCamera.SetTarget(Soul.transform, true);
            yield return null;
            yield return null;
            _picked = Soul.TryPossess() && Soul.Weapon == w;
            yield return null;
        }

        // ---------------------------------------------------------------- launches

        IEnumerator RunLaunches()
        {
            _report.AppendLine("LAUNCH|weapon|lift|result|asleep|apex meas/ideal|land dist meas/ballistic|ideal flat range|tLand|rest dist|settle s|settled|midair|picked|kept after release");
            for (int i = 0; i < weapons.Length; i++)
            {
                yield return LaunchOnce(weapons[i], tuning.minLiftDeg, false);
                yield return LaunchOnce(weapons[i], 45f, true);
            }
        }

        IEnumerator LaunchOnce(WeaponBody w, float liftDeg, bool tryMidAir)
        {
            yield return Prepare(w, launchOrigin.position, launchYaw);
            float restSettle = _settleSeconds;
            bool restSettled = _settled;
            float pitch = LaunchAim.PitchForLift(liftDeg, tuning);
            orbitCamera.SetLook(launchYaw, pitch);
            yield return null;
            yield return null;
            yield return new WaitForFixedUpdate();

            Rigidbody rb = w.Body;
            bool asleep = rb.IsSleeping();
            TrajectoryPreview preview = Soul.GetComponentInChildren<TrajectoryPreview>(true);
            int previewPoints = preview != null ? preview.VisiblePoints : -1;
            Vector3 p0 = rb.worldCenterOfMass;
            float t0 = Time.fixedTime;
            LaunchResult result = Soul.TryLaunch();

            float maxY = p0.y;
            Vector3 land = p0;
            float tLand = -1f;
            string mid = "-";
            bool midTried = false;
            while (Time.fixedTime - t0 < 6f)
            {
                yield return new WaitForFixedUpdate();
                Vector3 c = rb.worldCenterOfMass;
                float t = Time.fixedTime - t0;
                if (c.y > maxY) maxY = c.y;
                if (w.LastGroundContactTime > t0 + 0.06f) { land = c; tLand = t; break; }
                if (tryMidAir && !midTried && t > tuning.launchCooldown + 0.1f && !w.IsGrounded)
                {
                    midTried = true;
                    mid = Soul.TryLaunch().ToString();
                }
            }

            yield return Settle(w, 10f);
            Vector3 rest = rb.worldCenterOfMass;

            float v = w.Def.launchSpeed;
            float g = tuning.gravity;
            float rad = liftDeg * Mathf.Deg2Rad;
            float vy = v * Mathf.Sin(rad);
            float vx = v * Mathf.Cos(rad);
            float idealApex = vy * vy / (2f * g);
            float idealRange = v * v * Mathf.Sin(2f * rad) / g;
            float disc = vy * vy - 2f * g * (land.y - p0.y);
            float ballisticDist = disc >= 0f ? vx * (vy + Mathf.Sqrt(disc)) / g : float.NaN;
            float landDist = Flat(land - p0).magnitude;
            float restDist = Flat(rest - p0).magnitude;
            float apex = maxY - p0.y;

            bool released = Soul.Release();
            bool kept = released && !w.IsBroken && w.IsFree;

            _report.AppendLine("LAUNCH|" + w.Def.displayName + "|" + F(liftDeg) + "|" + result + "|" + asleep
                + "|" + F(apex) + "/" + F(idealApex) + " (" + Pct(apex, idealApex) + ")"
                + "|" + F(landDist) + "/" + F(ballisticDist) + " (" + Pct(landDist, ballisticDist) + ")"
                + "|" + F(idealRange) + "|" + F(tLand) + "|" + F(restDist) + "|" + F(_settleSeconds) + "|" + _settled
                + "|" + mid + "|" + _picked + "|" + kept
                + "|restSettle=" + F(restSettle) + "/" + restSettled + "|preview=" + previewPoints);
        }

        // ---------------------------------------------------------------- wall jumps

        IEnumerator RunWallJumps()
        {
            _report.AppendLine("WALL|weapon|approach|wall jump|normal|dot(v,n)|used after|second wall touch|used after landing|grounded relaunch");
            for (int i = 0; i < weapons.Length; i++) yield return WallJumpOnce(weapons[i]);
        }

        IEnumerator WallJumpOnce(WeaponBody w)
        {
            yield return Prepare(w, wallOrigin.position, wallApproachYaw);
            float pitch = LaunchAim.PitchForLift(45f, tuning);
            orbitCamera.SetLook(wallApproachYaw, pitch);
            yield return null;
            yield return null;
            yield return new WaitForFixedUpdate();

            Rigidbody rb = w.Body;
            WeaponMotor motor = Soul.Motor;
            float t0 = Time.fixedTime;
            LaunchResult approach = Soul.TryLaunch();

            string wallJump = "no wall contact";
            string second = "no second wall contact";
            string normal = "-";
            string dot = "-";
            string usedAfter = "-";
            float tJump = -1f;
            int phase = 0;
            while (Time.fixedTime - t0 < 8f)
            {
                yield return new WaitForFixedUpdate();
                float now = Time.fixedTime;
                bool airborne = !w.IsGrounded;
                if (phase == 0)
                {
                    if (now - t0 > tuning.launchCooldown + 0.02f && airborne && w.IsTouchingWall)
                    {
                        orbitCamera.SetLook(wallJumpYaw, pitch);
                        motor.SetAim(wallJumpYaw, pitch);
                        Vector3 n = w.WallNormal;
                        LaunchResult r = Soul.TryLaunch();
                        wallJump = r.ToString();
                        normal = F(n.x) + "," + F(n.y) + "," + F(n.z);
                        dot = F(Vector3.Dot(rb.linearVelocity.normalized, n));
                        usedAfter = motor.WallJumpsUsed.ToString();
                        tJump = now;
                        phase = 1;
                    }
                    else if (w.LastGroundContactTime > t0 + 0.06f) break;
                }
                else if (phase == 1)
                {
                    if (now - tJump > tuning.launchCooldown + 0.02f && airborne && w.IsTouchingWall)
                    {
                        second = Soul.TryLaunch().ToString();
                        phase = 2;
                    }
                    else if (w.LastGroundContactTime > tJump + 0.06f) break;
                }
                else if (w.LastGroundContactTime > tJump + 0.06f) break;
            }

            yield return Settle(w, 10f);
            string usedLanded = motor != null ? motor.WallJumpsUsed.ToString() : "-";
            orbitCamera.SetLook(90f, pitch);
            yield return null;
            yield return null;
            string relaunch = Soul.TryLaunch().ToString();
            yield return new WaitForSeconds(0.5f);
            yield return Settle(w, 10f);
            Soul.Release();

            _report.AppendLine("WALL|" + w.Def.displayName + "|" + approach + "|" + wallJump + "|" + normal + "|" + dot
                + "|" + usedAfter + "|" + second + "|" + usedLanded + "|" + relaunch);
        }

        // ---------------------------------------------------------------- roll

        IEnumerator RunRoll()
        {
            _report.AppendLine("ROLL|weapon|canRoll|forward m in 1.5 s|speed m/s|right m in 1.0 s");
            yield return RollOnce(weapons[3]);
            yield return RollOnce(weapons[0]);
        }

        IEnumerator RollOnce(WeaponBody w)
        {
            yield return Prepare(w, launchOrigin.position, launchYaw);
            orbitCamera.SetLook(launchYaw, orbitCamera.RestingPitch);
            yield return null;
            yield return null;
            Rigidbody rb = w.Body;
            Vector3 p0 = rb.worldCenterOfMass;
            Soul.SetRoll(new Vector2(0f, 1f));
            yield return new WaitForSeconds(1.5f);
            Vector3 p1 = rb.worldCenterOfMass;
            float speed = Flat(rb.linearVelocity).magnitude;
            Soul.SetRoll(new Vector2(1f, 0f));
            yield return new WaitForSeconds(1.0f);
            Vector3 p2 = rb.worldCenterOfMass;
            Soul.SetRoll(Vector2.zero);
            yield return Settle(w, 10f);
            Soul.Release();
            float forward = Vector3.Dot(p1 - p0, LaunchAim.Heading(launchYaw));
            float right = Vector3.Dot(p2 - p1, LaunchAim.Right(launchYaw));
            _report.AppendLine("ROLL|" + w.Def.displayName + "|" + w.Def.canRoll + "|" + F(forward) + "|" + F(speed) + "|" + F(right) + "|settle=" + F(_settleSeconds) + "/" + _settled);
        }

        // ---------------------------------------------------------------- soul

        IEnumerator RunSoul()
        {
            yield return WaitUntilFree();
            PlayerSoul soul = Soul;
            long ms0 = clock != null ? clock.Ms : 0;
            float time0 = Time.time;

            soul.Teleport(new Vector3(0f, 3f, 0f));
            orbitCamera.SetTarget(soul.transform, true);
            orbitCamera.SetLook(30f, -30f);
            yield return new WaitForFixedUpdate();
            Vector3 p0 = soul.Body.position;
            soul.SetThrust(true);
            yield return new WaitForSeconds(0.2f);
            float speedEarly = soul.Body.linearVelocity.magnitude;
            yield return new WaitForSeconds(0.8f);
            float speedMax = soul.Body.linearVelocity.magnitude;
            float along = Vector3.Dot((soul.Body.position - p0).normalized, orbitCamera.Forward);
            soul.SetThrust(false);
            yield return new WaitForSeconds(0.5f);
            float speedHalf = soul.Body.linearVelocity.magnitude;
            yield return new WaitForSeconds(1.5f);
            float speedEnd = soul.Body.linearVelocity.magnitude;
            _report.AppendLine("SOUL|thrust|speed@0.2s=" + F(speedEarly) + "|speed@1.0s=" + F(speedMax) + "|dot(move,camForward)=" + F(along)
                + "|released speed@0.5s=" + F(speedHalf) + "|@2.0s=" + F(speedEnd));

            // World blocks the soul: thrust straight down into the floor.
            soul.Teleport(new Vector3(3f, 2f, 0f));
            orbitCamera.SetLook(0f, 75f);
            soul.SetThrust(true);
            yield return new WaitForSeconds(1.5f);
            float floorY = soul.Body.position.y;
            soul.SetThrust(false);
            yield return new WaitForSeconds(1.0f);
            _report.AppendLine("SOUL|floor|y after 1.5 s of downward thrust=" + F(floorY) + " (radius 0.25, floor top 0)");

            long ms1 = clock != null ? clock.Ms : 0;
            _report.AppendLine("CLOCK|LevelClock delta ms=" + (ms1 - ms0) + "|Time.time delta ms=" + F((Time.time - time0) * 1000f));
        }

        // ---------------------------------------------------------------- possess, release, break, respawn

        IEnumerator RunPossessBreak()
        {
            yield return WaitUntilFree();
            PlayerSoul soul = Soul;
            WeaponBody w = weapons[1];
            yield return Settle(w, 8f);
            w.AddModifier(testModifier);
            w.ApplyDamage(20f);
            float hpBefore = w.Hp;

            orbitCamera.SetLook(180f, orbitCamera.RestingPitch);
            soul.Teleport(w.Body.worldCenterOfMass - orbitCamera.Forward * 1.2f);
            orbitCamera.SetTarget(soul.transform, true);
            yield return null;
            yield return null;
            yield return null;
            string candidate = soul.Candidate != null ? soul.Candidate.Def.displayName : "none";
            bool possessed = soul.TryPossess();
            bool right = soul.Weapon == w;
            bool hidden = !soul.VisualVisible;
            bool cameraOnWeapon = orbitCamera.Target == w.transform;
            _report.AppendLine("POSSESS|candidate=" + candidate + "|TryPossess=" + possessed + "|picked target=" + right
                + "|soul visual hidden=" + hidden + "|camera on weapon=" + cameraOnWeapon + "|weapon.IsPossessed=" + w.IsPossessed);

            yield return new WaitForSeconds(0.3f);
            bool inCombat = soul.InCombat;
            soul.Release();
            _report.AppendLine("RELEASE out of combat|inCombat=" + inCombat + "|broken=" + w.IsBroken + "|hp=" + F(w.Hp) + "/" + F(hpBefore)
                + "|modifiers=" + w.Modifiers.Count + "|free=" + w.IsFree + "|soul visible=" + soul.VisualVisible
                + "|soul above weapon=" + F(soul.transform.position.y - w.Body.worldCenterOfMass.y));

            yield return new WaitForSeconds(0.3f);
            bool again = soul.Possess(w);
            w.NotifyCombat();
            bool combatNow = soul.InCombat;
            Vector3 breakPos = w.Body.worldCenterOfMass;
            soul.Release();
            float tBreak = Time.time;
            _report.AppendLine("RELEASE in combat|repossessed=" + again + "|inCombat=" + combatNow + "|broken=" + w.IsBroken
                + "|modifiers=" + w.Modifiers.Count + "|soul free=" + !soul.IsPossessing + "|soul visible=" + soul.VisualVisible
                + "|soul dist from break point=" + F(Vector3.Distance(soul.transform.position, breakPos)));

            yield return new WaitForSeconds(9f);
            bool stillBrokenAt9 = w.IsBroken;
            while (w.IsBroken && Time.time - tBreak < 15f) yield return null;
            float tRespawn = Time.time - tBreak;
            yield return Settle(w, 8f);
            float slotDist = w.HomeSlot != null ? Flat(w.Body.position - w.HomeSlot.Position).magnitude : -1f;
            _report.AppendLine("RESPAWN|broken at 9 s=" + stillBrokenAt9 + "|respawned after s=" + F(tRespawn) + "|flat dist to slot=" + F(slotDist)
                + "|hp=" + F(w.Hp) + "/" + F(w.MaxHp) + "|modifiers=" + w.Modifiers.Count + "|free=" + w.IsFree + "|settled=" + _settled);

            // HP 0: the weapon breaks and the soul pops out where it died.
            WeaponBody b = weapons[6];
            while (soul.InCombat) yield return null;
            soul.Teleport(b.Body.worldCenterOfMass + Vector3.up);
            yield return null;
            bool got = soul.Possess(b);
            yield return new WaitForSeconds(0.2f);
            Vector3 deathPos = b.Body.worldCenterOfMass;
            b.ApplyDamage(9999f);
            _report.AppendLine("HP ZERO|possessed=" + got + "|broken=" + b.IsBroken + "|soul free=" + !soul.IsPossessing + "|soul visible=" + soul.VisualVisible
                + "|soul dist from death point=" + F(Vector3.Distance(soul.transform.position, deathPos)) + "|inCombat=" + soul.InCombat);
            while (b.IsBroken) yield return null;
            yield return Settle(b, 8f);
            _report.AppendLine("HP ZERO respawn|hp=" + F(b.Hp) + "/" + F(b.MaxHp) + "|flat dist to slot=" + F(Flat(b.Body.position - b.HomeSlot.Position).magnitude));
        }

        // ---------------------------------------------------------------- kill-Y

        IEnumerator RunKillY()
        {
            WeaponBody w = weapons[2];
            yield return Settle(w, 8f);
            Vector3 safe = w.LastSafePosition;
            _recovered = false;
            w.Recovered += OnRecovered;
            w.Teleport(outOfWorldPoint, Quaternion.identity);
            float start = Time.time;
            float minY = w.Body.position.y;
            while (!_recovered && Time.time - start < 8f)
            {
                yield return new WaitForFixedUpdate();
                minY = Mathf.Min(minY, w.Body.position.y);
            }
            w.Recovered -= OnRecovered;
            float fallSeconds = Time.time - start;
            yield return Settle(w, 8f);
            _report.AppendLine("KILLY|recovered=" + _recovered + "|fall s=" + F(fallSeconds) + "|lowest y=" + F(minY)
                + "|dist from last safe pos=" + F(Vector3.Distance(w.Body.position, safe)) + "|settled=" + _settled + "|hp=" + F(w.Hp) + "/" + F(w.MaxHp));
        }

        void OnRecovered(WeaponBody w) { _recovered = true; }
    }
}
