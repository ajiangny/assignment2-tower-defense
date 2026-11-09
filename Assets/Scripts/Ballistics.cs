using UnityEngine;

namespace TowerDefense
{
    public static class Ballistics
    {
        /// <summary>
        /// Finds where a straight-flying projectile can meet a target moving at constant velocity.
        /// Returns false when the target can't be caught; aimPoint is then its current position.
        /// </summary>
        public static bool TryIntercept(Vector3 shooter, Vector3 target, Vector3 targetVelocity, float projectileSpeed, out Vector3 aimPoint)
        {
            aimPoint = target;
            Vector3 toTarget = target - shooter;

            // |toTarget + velocity * t| = speed * t, rearranged into a*t^2 + b*t + c = 0.
            float a = Vector3.Dot(targetVelocity, targetVelocity) - projectileSpeed * projectileSpeed;
            float b = 2f * Vector3.Dot(toTarget, targetVelocity);
            float c = Vector3.Dot(toTarget, toTarget);

            float time;
            if (Mathf.Abs(a) < 0.0001f)
            {
                if (Mathf.Abs(b) < 0.0001f) return false;
                time = -c / b;
            }
            else
            {
                float discriminant = b * b - 4f * a * c;
                if (discriminant < 0f) return false;
                float root = Mathf.Sqrt(discriminant);
                float t1 = (-b - root) / (2f * a);
                float t2 = (-b + root) / (2f * a);
                float early = Mathf.Min(t1, t2);
                float late = Mathf.Max(t1, t2);
                time = early > 0f ? early : late;
            }

            if (time <= 0f) return false;
            aimPoint = target + targetVelocity * time;
            return true;
        }
    }
}
