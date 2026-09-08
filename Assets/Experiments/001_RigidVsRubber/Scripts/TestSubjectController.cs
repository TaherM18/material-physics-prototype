using UnityEngine;
using UnityEngine.InputSystem;

namespace Experiment001
{
    /// <summary>
    /// Crude manual control for the test subject, for exploring the sandbox between measured runs.
    /// <para>
    /// This is deliberately <em>not</em> a character controller and must not become one.
    /// Experiment 001's measurements come from the scripted scenarios in
    /// <see cref="ExperimentRunner"/>, which are repeatable; this exists only so the sphere can be
    /// driven into things by hand to find behaviour worth measuring properly.
    /// </para>
    /// <para>
    /// Movement is applied as a force on the Rigidbody rather than by writing to the Transform,
    /// so that the body remains a full participant in the simulation. Driving a physics body by
    /// Transform assignment would fight the solver, and this project has
    /// <c>m_AutoSyncTransforms: 0</c>, so such writes would not even reach the physics scene until
    /// the next step.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class TestSubjectController : MonoBehaviour
    {
        [Header("Input (project Input System asset, looked up by name)")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string jumpActionName = "Jump";

        [Header("Movement")]
        [Tooltip("Continuous force while a direction is held. Newtons, applied every physics step.")]
        [SerializeField] private float moveForce = 25f;

        [Tooltip("Horizontal speed cap, so the sphere cannot be accelerated indefinitely and " +
                 "confound the collision measurements with arbitrary impact speeds.")]
        [SerializeField] private float maxHorizontalSpeed = 12f;

        [Tooltip("Instantaneous upward impulse. Applied once, not per step.")]
        [SerializeField] private float jumpImpulse = 6f;

        [Header("Ground check")]
        [SerializeField] private float groundCheckDistance = 0.15f;
        [SerializeField] private LayerMask groundLayers = ~0;

        private Rigidbody body;
        private Collider bodyCollider;

        private InputAction moveAction;
        private InputAction jumpAction;

        // Input is sampled in Update, which runs once per rendered frame, and consumed in
        // FixedUpdate, which runs at a fixed 50 Hz. Those rates are unrelated. A button press that
        // is only true for one rendered frame can therefore fall entirely between two physics
        // steps, so the press is latched here and cleared when the physics step actually uses it.
        private Vector2 moveInput;
        private bool jumpLatched;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            bodyCollider = GetComponent<Collider>();
            ResolveActions();
        }

        private void ResolveActions()
        {
            if (inputActions == null)
            {
                Debug.LogWarning($"{nameof(TestSubjectController)}: no InputActionAsset assigned. " +
                                 "Manual control is disabled; the scripted scenarios still work.", this);
                return;
            }

            InputActionMap map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
            if (map == null)
            {
                Debug.LogWarning($"{nameof(TestSubjectController)}: action map '{actionMapName}' " +
                                 "not found. Manual control is disabled.", this);
                return;
            }

            moveAction = map.FindAction(moveActionName, throwIfNotFound: false);
            jumpAction = map.FindAction(jumpActionName, throwIfNotFound: false);

            if (moveAction == null || jumpAction == null)
            {
                Debug.LogWarning($"{nameof(TestSubjectController)}: could not find '{moveActionName}' " +
                                 $"and/or '{jumpActionName}' in map '{actionMapName}'.", this);
            }
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            jumpAction?.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            jumpAction?.Disable();
        }

        private void Update()
        {
            moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

            if (jumpAction != null && jumpAction.WasPressedThisFrame())
            {
                jumpLatched = true;
            }
        }

        private void FixedUpdate()
        {
            Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y);
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            // ForceMode.Force: a continuous force, integrated over the step and scaled by mass.
            // This is the correct mode for sustained input.
            body.AddForce(direction * moveForce, ForceMode.Force);

            // Only while input is held. An unconditional clamp would also cap the rebound
            // velocity coming off the wall, which is precisely the quantity the wall test
            // exists to measure.
            if (direction.sqrMagnitude > 0f)
            {
                ClampHorizontalSpeed();
            }

            if (jumpLatched)
            {
                jumpLatched = false;

                if (IsGrounded())
                {
                    // ForceMode.Impulse: an instantaneous change in momentum, still scaled by mass,
                    // which is what a jump is. Using Force here would produce a barely perceptible
                    // nudge, since it would be integrated over a single 0.02 s step.
                    body.AddForce(Vector3.up * jumpImpulse, ForceMode.Impulse);
                }
            }
        }

        private void ClampHorizontalSpeed()
        {
            Vector3 velocity = body.linearVelocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);

            if (horizontal.magnitude <= maxHorizontalSpeed)
            {
                return;
            }

            horizontal = horizontal.normalized * maxHorizontalSpeed;
            body.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        }

        private bool IsGrounded()
        {
            float radius = bodyCollider.bounds.extents.y;

            return Physics.Raycast(
                body.position,
                Vector3.down,
                radius + groundCheckDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);
        }
    }
}
