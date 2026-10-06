using UnityEngine;

/// <summary>
/// Drives Nabil's body model (the Mixamo character) from how fast the player is really moving:
/// idle when standing, walk when moving, with the walk speeding up the faster the player goes so
/// the feet do not skate. Also hides the head, so the first person camera never looks out from
/// inside it (the body, arms and legs stay visible when looking down).
///
/// The Animator needs a float parameter 'Speed' (metres per second) and a float 'WalkRate'
/// (playback speed multiplier of the walk state) - the Nabil controller already has both.
/// </summary>
[DisallowMultipleComponent]
public class PlayerBodyAnimator : MonoBehaviour
{
    [Tooltip("The body's Animator. Found automatically in the children if left empty.")]
    [SerializeField] private Animator animator;

    [Tooltip("Speed (m/s) at which the walk animation plays at its normal rate.")]
    [SerializeField] private float walkAnimSpeed = 1.6f;

    [Tooltip("The walk animation never plays faster than this multiple of its normal rate.")]
    [SerializeField] private float maxWalkRate = 2.2f;

    [Tooltip("Hide the head so the camera is not inside it.")]
    [SerializeField] private bool hideHead = true;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int WalkRateParam = Animator.StringToHash("WalkRate");

    private Transform head;
    private Vector3 lastPosition;
    private bool hasLast;
    private float smoothSpeed;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null) return;

        foreach (Transform t in animator.GetComponentsInChildren<Transform>(true))
        {
            // Mixamo names the bone 'mixamorigN:Head' (the number changes per download).
            if (t.name == "Head" || t.name.EndsWith(":Head"))
            {
                head = t;
                break;
            }
        }
    }

    private void Update()
    {
        if (animator == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;   // paused

        Vector3 position = transform.position;
        if (!hasLast)
        {
            lastPosition = position;
            hasLast = true;
            return;
        }

        Vector3 delta = position - lastPosition;
        delta.y = 0f;
        lastPosition = position;

        float moved = delta.magnitude;
        float speed = moved > 1.5f ? 0f : moved / dt;   // a teleport is not walking

        smoothSpeed = Mathf.Lerp(smoothSpeed, speed, 1f - Mathf.Exp(-12f * dt));
        animator.SetFloat(SpeedParam, smoothSpeed);
        animator.SetFloat(WalkRateParam, Mathf.Clamp(smoothSpeed / walkAnimSpeed, 1f, maxWalkRate));
    }

    // After the Animator has posed the skeleton for this frame.
    private void LateUpdate()
    {
        if (hideHead && head != null) head.localScale = Vector3.zero;
    }
}
