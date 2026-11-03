using UnityEngine;

namespace TowerDefense
{
    /// <summary>Ordered waypoints that enemies follow from first to last.</summary>
    public class EnemyPath : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints;

        public int Count => waypoints.Length;

        public Vector3 GetPoint(int index) => waypoints[index].position;

        /// <summary>Shortest ground-plane distance from a point to any path segment.</summary>
        public float DistanceTo(Vector3 point)
        {
            Vector2 p = new Vector2(point.x, point.z);
            float best = float.PositiveInfinity;
            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                Vector3 from = waypoints[i].position;
                Vector3 to = waypoints[i + 1].position;
                Vector2 a = new Vector2(from.x, from.z);
                Vector2 b = new Vector2(to.x, to.z);
                Vector2 segment = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, segment) / Mathf.Max(segment.sqrMagnitude, 0.0001f));
                best = Mathf.Min(best, Vector2.Distance(p, a + segment * t));
            }
            return best;
        }

        public void SetWaypoints(Transform[] points) => waypoints = points;

        private void OnDrawGizmos()
        {
            if (waypoints == null) return;
            Gizmos.color = Color.yellow;
            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                if (waypoints[i] != null && waypoints[i + 1] != null)
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
        }
    }
}
