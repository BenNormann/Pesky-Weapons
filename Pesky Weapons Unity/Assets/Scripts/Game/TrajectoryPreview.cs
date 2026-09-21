using Pesky.Data;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// Draws about one second of the ballistic arc the driven weapon would fly if it launched now
    /// (g from MovementTuning = 20). Hidden while a launch would be refused. Cut short at World geometry.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class TrajectoryPreview : MonoBehaviour
    {
        [SerializeField] LineRenderer line;
        [SerializeField] MovementTuning tuning;
        [Tooltip("Layers the arc is cast against. Overridden by MovementTuning.previewBlockMask when that is non-zero.")]
        [SerializeField] LayerMask blockMask = 2304;
        [Tooltip("Which of those layers count as a hostile mob (draws the red X instead of the landing circle).")]
        [SerializeField] LayerMask enemyMask = 2048;

        [Header("Markers")]
        [Tooltip("Opaque disc drawn flat on the predicted landing surface.")]
        [SerializeField] Transform landingMarker;
        [Tooltip("Opaque red X drawn when the predicted hit is a mob. Always faces the camera.")]
        [SerializeField] Transform enemyMarker;

        WeaponMotor _motor;
        Vector3[] _points;
        Camera _view;
        WorldAuthority _authority;

        public int VisiblePoints { get { return line != null && line.enabled ? line.positionCount : 0; } }

        void Reset()
        {
            line = GetComponent<LineRenderer>();
        }

        void OnValidate()
        {
            if (line == null) line = GetComponent<LineRenderer>();
        }

        void Awake()
        {
            _points = new Vector3[Mathf.Max(2, tuning.previewPoints)];
            line.useWorldSpace = true;
            HideMarkers();
            line.enabled = false;
        }

        public void SetMotor(WeaponMotor motor)
        {
            _motor = motor;
            if (_motor == null)
            {
                line.enabled = false;
                HideMarkers();
            }
        }

        /// <summary>The camera the red X faces. The PlayerSpawner hands it over through PlayerSoul.Init.</summary>
        public void SetCamera(Camera camera)
        {
            _view = camera;
        }

/// <summary>Where the magic doors are listed. The arc simply ends at an open magic door's plane.</summary>
        public void SetAuthority(WorldAuthority authority)
        {
            _authority = authority;
        }

        bool HitsMagicDoor(Vector3 a, Vector3 b, out Vector3 point)
        {
            point = b;
            if (_authority == null) return false;
            System.Collections.Generic.IReadOnlyList<MagicDoor> doors = _authority.MagicDoors;
            for (int i = 0; i < doors.Count; i++)
            {
                MagicDoor door = doors[i];
                if (door != null && door.IsPassable && door.SegmentCrosses(a, b, out point)) return true;
            }
            return false;
        }


        void HideMarkers()
        {
            if (landingMarker != null && landingMarker.gameObject.activeSelf) landingMarker.gameObject.SetActive(false);
            if (enemyMarker != null && enemyMarker.gameObject.activeSelf) enemyMarker.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (_motor == null) return;

            Vector3 velocity;
            LaunchResult result = _motor.Evaluate(out velocity);
            if (result != LaunchResult.Launched && result != LaunchResult.WallJumped)
            {
                line.enabled = false;
                HideMarkers();
                return;
            }

            int mask = tuning.previewBlockMask.value != 0 ? tuning.previewBlockMask.value : blockMask.value;
            Vector3 origin = _motor.Weapon.Body.worldCenterOfMass;
            Vector3 g = Vector3.down * tuning.gravity;
            int n = _points.Length;
            int used = n;
            float step = tuning.previewSeconds / (n - 1);
            _points[0] = origin;
            bool blocked = false;
            RaycastHit hit = new RaycastHit();
            for (int i = 1; i < n; i++)
            {
                float t = step * i;
                _points[i] = origin + velocity * t + 0.5f * t * t * g;
                Vector3 doorPoint;
                if (HitsMagicDoor(_points[i - 1], _points[i], out doorPoint)
                    && !Physics.Linecast(_points[i - 1], doorPoint, mask, QueryTriggerInteraction.Ignore))
                {
                    // The arc ends at the plane; where it comes out is the far room's business.
                    _points[i] = doorPoint;
                    used = i + 1;
                    break;
                }
                if (Physics.Linecast(_points[i - 1], _points[i], out hit, mask, QueryTriggerInteraction.Ignore))
                {
                    _points[i] = hit.point;
                    used = i + 1;
                    blocked = true;
                    break;
                }
            }

            line.positionCount = used;
            for (int i = 0; i < used; i++) line.SetPosition(i, _points[i]);
            line.enabled = true;

            if (!blocked || hit.collider == null)
            {
                HideMarkers();
                return;
            }

            bool mob = ((1 << hit.collider.gameObject.layer) & enemyMask.value) != 0;
            float d = tuning.previewMarkerRadius * 2f;
            Vector3 at = hit.point + hit.normal * tuning.previewMarkerLift;

            if (mob)
            {
                if (landingMarker != null && landingMarker.gameObject.activeSelf) landingMarker.gameObject.SetActive(false);
                if (enemyMarker == null) return;
                if (!enemyMarker.gameObject.activeSelf) enemyMarker.gameObject.SetActive(true);
                // Stand the X clear of the body, toward the camera, or half of it sinks into the mob.
                enemyMarker.position = _view != null
                    ? hit.point + (_view.transform.position - hit.point).normalized * tuning.previewMarkerRadius
                    : at;
                if (_view != null)
                    enemyMarker.rotation = Quaternion.LookRotation(at - _view.transform.position, Vector3.up);
                enemyMarker.localScale = new Vector3(d, d, d);
                return;
            }

            if (enemyMarker != null && enemyMarker.gameObject.activeSelf) enemyMarker.gameObject.SetActive(false);
            if (landingMarker == null) return;
            if (!landingMarker.gameObject.activeSelf) landingMarker.gameObject.SetActive(true);
            landingMarker.position = at;
            landingMarker.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            landingMarker.localScale = new Vector3(d, 0.01f, d);
        }
    }
}
