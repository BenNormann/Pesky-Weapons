using Pesky.Protocol;
using Pesky.Sim;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The Mage's aim, shared by the nudge (MageNudge) and the curses (MageCurse): no crosshair, the target
    /// is the other player's body (or the tutorial dummy) nearest the centre of the screen inside a cone.
    /// Pure: it reads the sim's player rows and the scene's remote views and changes nothing.
    /// </summary>
    public static class MageAim
    {
        /// <summary>The body nearest the middle of the screen inside the cone: another player's weapon, or the tutorial dummy. False when nothing is in the cone.</summary>
        public static bool FindTarget(WorldAuthority authority, Transform eye, float coneDegrees, out byte slot, out Vector3 at)
        {
            slot = Wire.NoSlot;
            at = Vector3.zero;
            if (authority == null || eye == null) return false;
            Vector3 forward = eye.forward;
            float bestDot = Mathf.Cos(coneDegrees * Mathf.Deg2Rad);
            bool found = false;

            PlayerTable players = authority.Session != null && authority.Session.Sim != null ? authority.Session.Sim.Players : null;
            byte local = authority.LocalSlot;
            for (int i = 0; players != null && i < players.Count && i < Wire.MaxPlayers; i++)
            {
                if (i == local) continue;
                PlayerState p = players[i];
                if (p == null || !p.present || p.weaponId == Wire.NoId) continue;
                WeaponBody w = authority.RemoteWeaponOf((byte)i);
                if (w == null || w.Body == null) continue;
                Vector3 pos = w.Body.position;
                Vector3 to = pos - eye.position;
                if (to.sqrMagnitude < 1e-4f) continue;
                float dot = Vector3.Dot(forward, to.normalized);
                if (dot <= bestDot) continue;
                bestDot = dot;
                slot = (byte)i;
                at = pos;
                found = true;
            }

            PracticeDummy dummy = authority.PracticeDummy;
            if (dummy != null && dummy.isActiveAndEnabled)
            {
                Vector3 pos = dummy.Centre;
                Vector3 to = pos - eye.position;
                if (to.sqrMagnitude > 1e-4f && Vector3.Dot(forward, to.normalized) > bestDot)
                {
                    slot = NudgeReqMsg.PracticeTarget;
                    at = pos;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>The local player's own body: the possessed weapon, else the soul. False without a soul.</summary>
        public static bool TryBody(PlayerSpawner spawner, out Vector3 position)
        {
            position = Vector3.zero;
            PlayerSoul soul = spawner != null ? spawner.LocalSoul : null;
            if (soul == null) return false;
            WeaponBody weapon = soul.Weapon;
            if (weapon != null && weapon.Body != null) position = weapon.Body.position;
            else position = soul.Body != null ? soul.Body.position : soul.transform.position;
            return true;
        }
    }
}
