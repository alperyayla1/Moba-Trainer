using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Drives the imported hero's Animator (states: Idle, Walk, Cast). Walk playback speed follows the
    /// real ground speed so feet don't slide; abilities (Q/W/E) and auto attacks play Cast, sped up to
    /// fit short cast times. Blink plays nothing (the model just pops).
    /// </summary>
    public class HeroAnimDriver : MonoBehaviour
    {
        public Animator animator;
        [Tooltip("World distance covered by one second of the Walk clip at speed 1.")]
        public float walkClipSpeed = 1.6f;
        [Tooltip("Length of the Cast clip in seconds.")]
        public float castClipLength = 0.96f;
        [Tooltip("Shortest time the cast animation is shown.")]
        public float minCastShow = 0.35f;

        private PlayerAbilities abilities;
        private PlayerAttack attack;
        private Vector3 lastPos;
        private float speed;
        private float castTimer;
        private float castSpeed = 1f;
        private bool wasWindup;
        private string state = "";

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            abilities = GetComponentInParent<PlayerAbilities>();
            attack = GetComponentInParent<PlayerAttack>();
            if (animator != null) animator.applyRootMotion = false;
        }

        private void OnEnable()
        {
            lastPos = transform.position;
            if (abilities != null) abilities.AbilityCast += OnAbilityCast;
        }

        private void OnDisable()
        {
            if (abilities != null) abilities.AbilityCast -= OnAbilityCast;
        }

        private void OnAbilityCast(int slot, AbilityDefinition ability, Vector3 point)
        {
            if (slot > 2) return;                                    // Blink: no cast pose
            float show = Mathf.Max(minCastShow, ability != null ? ability.castTime + 0.15f : minCastShow);
            PlayCast(show);
        }

        private void PlayCast(float show)
        {
            castTimer = show;
            castSpeed = Mathf.Clamp(castClipLength / show, 1f, 3f);
            Go("Cast", 0.05f, true);
        }

        private void Update()
        {
            if (animator == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 pos = transform.position;
            Vector3 d = pos - lastPos;
            d.y = 0f;
            lastPos = pos;
            float inst = d.magnitude > 1.2f ? 0f : d.magnitude / dt;   // ignore blink teleports
            speed = Mathf.Lerp(speed, inst, 1f - Mathf.Exp(-15f * dt));

            bool windup = attack != null && attack.CurrentState == PlayerAttack.State.Windup;
            if (windup && !wasWindup) PlayCast(minCastShow);
            wasWindup = windup;

            if (castTimer > 0f)
            {
                castTimer -= dt;
                animator.speed = castSpeed;
                if (castTimer > 0f) return;
            }

            if (speed > 0.25f)
            {
                Go("Walk", 0.12f, false);
                animator.speed = Mathf.Clamp(speed / Mathf.Max(0.1f, walkClipSpeed), 0.6f, 2.2f);
            }
            else
            {
                Go("Idle", 0.2f, false);
                animator.speed = 1f;
            }
        }

        private void Go(string next, float fade, bool restart)
        {
            if (!restart && state == next) return;
            state = next;
            animator.CrossFadeInFixedTime(next, fade, 0, 0f);
        }
    }
}
