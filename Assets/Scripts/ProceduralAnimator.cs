using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Hit squash and death for an enemy model; sits on the model child (pivot at the feet) under an Enemy root.
    /// Rigged models: drives the Animator's walk speed and plays its "Die" state.
    /// Unrigged models: fakes a waddle and topples over on death.
    /// </summary>
    public class ProceduralAnimator : MonoBehaviour
    {
        [Header("Walk")]
        [Tooltip("Stride length as a fraction of model height. Shorter strides waddle faster.")]
        [SerializeField] private float strideFraction = 0.55f;
        [SerializeField] private float bobFraction = 0.05f;
        [SerializeField] private float rollDegrees = 8f;
        [SerializeField] private float leanDegrees = 10f;

        [Header("Hit")]
        [SerializeField] private float hitSquash = 0.22f;
        [SerializeField] private float hitDuration = 0.15f;

        [Header("Rigged")]
        [Tooltip("Animator state played on death, when the model has an Animator.")]
        [SerializeField] private string dieState = "Die";

        [Header("Death")]
        [SerializeField] private float deathDuration = 0.45f;
        [SerializeField] private float corpseLinger = 0.6f;

        private Enemy enemy;
        private Animator animator;
        private float referenceSpeed = 1f;
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private Vector3 baseScale;
        private float height = 1f;
        private float phase;
        private float moving;
        private float hitTimer;
        private float deathTimer = -1f;
        private Quaternion deathStart;
        private Quaternion deathEnd;

        private void Awake()
        {
            enemy = GetComponentInParent<Enemy>();
            animator = GetComponentInChildren<Animator>();
            if (enemy != null && enemy.Stats != null) referenceSpeed = Mathf.Max(0.1f, enemy.Stats.speed);
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            baseScale = transform.localScale;
            phase = Random.value * Mathf.PI * 2f;

            Renderer body = GetComponentInChildren<Renderer>();
            if (body != null) height = Mathf.Max(0.1f, body.bounds.size.y);

            if (enemy != null)
            {
                enemy.Damaged += OnDamaged;
                enemy.Died += OnDied;
            }
        }

        private void OnDestroy()
        {
            if (enemy == null) return;
            enemy.Damaged -= OnDamaged;
            enemy.Died -= OnDied;
        }

        private void OnDamaged() => hitTimer = hitDuration;

        private void OnDied()
        {
            enemy.Damaged -= OnDamaged;
            enemy.Died -= OnDied;
            enemy = null;

            // Leave the root (it is destroyed this frame) and die where we stand.
            transform.SetParent(null, true);
            transform.localScale = baseScale;
            hitTimer = 0f;

            if (animator != null)
            {
                animator.speed = 1f;
                animator.Play(dieState, 0, 0f);
                float clipLength = DeathClipLength();
                Destroy(gameObject, clipLength + corpseLinger);
                enabled = false;
                return;
            }

            deathTimer = 0f;
            deathStart = transform.rotation;
            float side = Random.value < 0.5f ? -1f : 1f;
            deathEnd = deathStart * Quaternion.Euler(-80f, 0f, 20f * side);
            Destroy(gameObject, deathDuration + corpseLinger);
        }

        private float DeathClipLength()
        {
            if (animator.runtimeAnimatorController == null) return deathDuration;
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name == dieState) return clip.length;
            return deathDuration;
        }

        private void Update()
        {
            if (deathTimer >= 0f)
            {
                deathTimer += Time.deltaTime;
                float t = Mathf.Clamp01(deathTimer / deathDuration);
                transform.rotation = Quaternion.Slerp(deathStart, deathEnd, t * t);
                transform.localScale = baseScale * Mathf.Lerp(1f, 0.85f, t);
                return;
            }

            float speed = enemy != null ? enemy.Velocity.magnitude : 0f;
            moving = Mathf.MoveTowards(moving, speed > 0.01f ? 1f : 0f, Time.deltaTime * 4f);

            if (animator != null)
            {
                // The walk clip carries the motion; keep its pace tied to how fast we move.
                animator.speed = Mathf.Lerp(0.15f, speed / referenceSpeed, moving);
                ApplySquash();
                return;
            }

            phase += speed / (height * strideFraction) * Mathf.PI * Time.deltaTime;

            float step = Mathf.Sin(phase);
            float bob = Mathf.Abs(step) * bobFraction * height * moving;
            float roll = step * rollDegrees * moving;
            float lean = leanDegrees * moving;

            transform.localPosition = basePosition + Vector3.up * bob;
            transform.localRotation = baseRotation * Quaternion.Euler(lean, 0f, roll);

            ApplySquash();
        }

        private void ApplySquash()
        {
            float squash = 0f;
            if (hitTimer > 0f)
            {
                hitTimer -= Time.deltaTime;
                squash = hitSquash * Mathf.Clamp01(hitTimer / hitDuration);
            }
            transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + squash * 0.5f, 1f - squash, 1f + squash * 0.5f));
        }
    }
}
