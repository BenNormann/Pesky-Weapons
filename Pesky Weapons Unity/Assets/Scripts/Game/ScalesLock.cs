using System;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>Which weight class a scales pan is asking for.</summary>
    public enum MassClass { Light = 0, Medium = 1, Heavy = 2 }

    /// <summary>
    /// Three pans that each want a weight class at the same time. With the slice-1 masses:
    ///   Light  &lt;= 1.5 : Banana 0.5, Dagger 1
    ///   Medium 2 to 5  : Staff 2.5, Sword 3, Orb 4
    ///   Heavy  &gt;= 8   : Mace 8, Hammer 14
    /// The moment all three are satisfied at once the lock LATCHES, so the weapons can be picked up again
    /// afterwards and the door stays open. A pan sinks with the mass on it, and turns green when happy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScalesLock : MonoBehaviour, ISceneId
    {
        [Serializable]
        public struct PanSlot
        {
            public MassPan pan;
            public MassClass want;
        }

        [Header("Identity")]
        [SerializeField] int id;
        [SerializeField] WorldAuthority authority;

        [Header("Pans")]
        [SerializeField] PanSlot[] pans = new PanSlot[3];

        [Header("Mass classes")]
        [SerializeField] float lightMax = 1.5f;
        [SerializeField] float mediumMin = 2f;
        [SerializeField] float mediumMax = 5f;
        [SerializeField] float heavyMin = 8f;

        [Header("Look")]
        [Tooltip("Metres a fully loaded pan sinks.")]
        [SerializeField] float sinkDepth = 0.35f;
        [SerializeField] Color wrongColor = new Color(0.63f, 0.63f, 0.67f, 1f);
        [SerializeField] Color rightColor = new Color(0.24f, 0.63f, 0.27f, 1f);
        [SerializeField] Color latchedColor = new Color(0.40f, 0.86f, 0.45f, 1f);

        bool _latched;

        public int SceneId { get { return id; } }
        public bool Latched { get { return _latched; } }
        public int PanCount { get { return pans != null ? pans.Length : 0; } }

        public MassPan Pan(int index) { return pans != null && index >= 0 && index < pans.Length ? pans[index].pan : null; }
        public MassClass Want(int index) { return pans != null && index >= 0 && index < pans.Length ? pans[index].want : MassClass.Light; }

        /// <summary>Pure: does this mass belong to that class?</summary>
        public bool Fits(float mass, MassClass want)
        {
            switch (want)
            {
                case MassClass.Light: return mass > 0f && mass <= lightMax;
                case MassClass.Medium: return mass >= mediumMin && mass <= mediumMax;
                default: return mass >= heavyMin;
            }
        }

        public bool Satisfied(int index)
        {
            MassPan pan = Pan(index);
            return pan != null && Fits(pan.Mass, Want(index));
        }

        /// <summary>True while every pan holds the class it wants. The authority turns this into the latch.</summary>
        public bool AllSatisfied
        {
            get
            {
                if (pans == null || pans.Length == 0) return false;
                for (int i = 0; i < pans.Length; i++)
                {
                    if (pans[i].pan == null) return false;
                    if (!Fits(pans[i].pan.Mass, pans[i].want)) return false;
                }
                return true;
            }
        }

        /// <summary>Authority only. Latching: the door it feeds stays open once this has happened.</summary>
        public void ApplyLatched()
        {
            if (_latched) return;
            _latched = true;
            for (int i = 0; i < pans.Length; i++)
                if (pans[i].pan != null) pans[i].pan.Tint(latchedColor);
        }

        void FixedUpdate()
        {
            if (pans == null) return;
            for (int i = 0; i < pans.Length; i++)
            {
                MassPan pan = pans[i].pan;
                if (pan == null) continue;
                float mass = pan.Measure();
                float sink = heavyMin > 0.01f ? Mathf.Clamp01(mass / heavyMin) * sinkDepth : 0f;
                pan.MoveTo(pan.BaseY - sink);
                if (!_latched) pan.Tint(Fits(mass, pans[i].want) ? rightColor : wrongColor);
            }
            if (authority != null) authority.ReportScales(id);
        }
    }
}
