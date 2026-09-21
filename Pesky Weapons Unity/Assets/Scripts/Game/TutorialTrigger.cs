using Pesky.Protocol;
using Pesky.Session;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// One beat of the tutorial: a trigger box that fires once, the first time the player's weapon
    /// reaches it. It switches signs and props on or off, it can wake the labyrinth HUD for the Mage
    /// lesson, and the last one of them ends the run, which is what returns the player to the menu.
    ///
    /// It decides nothing shared and it knows nothing about the labyrinth. Ending the run is the
    /// host's own <see cref="NetSession.EndRun"/> - the same call a rule makes when a crew escapes -
    /// and the tutorial is an offline session, so the host is this player.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class TutorialTrigger : MonoBehaviour
    {
        [Tooltip("Switched on the first time a weapon enters.")]
        [SerializeField] GameObject[] activate = new GameObject[0];

        [Tooltip("Switched off the first time a weapon enters.")]
        [SerializeField] GameObject[] deactivate = new GameObject[0];

        [Tooltip("Optional: the labyrinth HUD to wake up (compass, map, role reveal).")]
        [OptionalRef][SerializeField] LabyrinthHud wakeHud;

        [Tooltip("Optional: needed only by the trigger that ends the tutorial, because the host ends the run.")]
        [OptionalRef][SerializeField] SessionRunner sessionRunner;

        [Tooltip("The last beat: end the run, which sends every peer back to the menu.")]
        [SerializeField] bool endsTutorial;

        bool _fired;

        /// <summary>True once this beat has played. Nothing resets it: a tutorial is walked once.</summary>
        public bool Fired { get { return _fired; } }

        void Awake()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null) box.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_fired || !IsWeapon(other)) return;
            _fired = true;

            for (int i = 0; i < activate.Length; i++)
                if (activate[i] != null) activate[i].SetActive(true);
            for (int i = 0; i < deactivate.Length; i++)
                if (deactivate[i] != null) deactivate[i].SetActive(false);

            if (wakeHud != null) wakeHud.Wake();
            if (!endsTutorial) return;

            // Offline, this is the host: EndRun puts the sim in Ended, SessionRunner sees the phase
            // change and loads the menu scene. A level opened straight from the Editor stays put.
            NetSession session = sessionRunner != null ? sessionRunner.Session : null;
            if (session == null || !session.IsStarted || !session.IsHost) return;
            session.EndRun(SessionEndReason.Escaped);
            // And then close the room. The phase change above has already queued the trip back to the
            // menu; leaving here as well means the menu finds a session that is not started, so it shows
            // the title page instead of a room page offering to START a run on the tutorial's data.
            session.Leave();
        }

        /// <summary>Weapons only, exactly as ExitZone reads its volume: souls are on a layer that never touches Trigger.</summary>
        static bool IsWeapon(Collider other)
        {
            if (other == null) return false;
            Rigidbody rb = other.attachedRigidbody;
            WeaponBody weapon = rb != null ? rb.GetComponent<WeaponBody>() : other.GetComponentInParent<WeaponBody>();
            return weapon != null;
        }
    }
}
