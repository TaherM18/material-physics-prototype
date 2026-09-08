using Experiment001;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Experiment002
{
    /// <summary>How the rubber state is driven from input.</summary>
    public enum RubberInputMode
    {
        /// <summary>Hold the key to be rubber, release to return to rigid.</summary>
        Hold,

        /// <summary>Press to flip between the two states.</summary>
        Toggle
    }

    /// <summary>
    /// Movement and state input for the traversal chamber.
    /// <para>
    /// This is a separate controller from Experiment 001's rather than an edit of it, so that
    /// Experiment 001 stays reproducible. It differs in three ways that matter to the questions
    /// being asked here:
    /// </para>
    /// <list type="number">
    /// <item>Ground and air steering forces are tuned independently, because "can the player steer
    /// during a bounce" is one of the things under test and it needs its own dial.</item>
    /// <item>The horizontal speed clamp applies only while grounded. In Experiment 001 an
    /// unconditional clamp would have capped rebound velocity; here it would silently destroy the
    /// launch speed coming off a ramp, which is the entire mechanic.</item>
    /// <item>Rubber defaults to hold-to-activate rather than toggle, because the open question is
    /// whether switching state <em>at a particular moment</em> matters, and a hold is the only
    /// input shape that lets that be attempted precisely.</item>
    /// </list>
    /// <para>
    /// It reuses <see cref="PhysicsStateSwitcher"/> from Experiment 001 unchanged. That is a real
    /// dependency across experiment folders, taken deliberately: the component is exactly the same
    /// idea here, and copying it would mean two definitions of "what rubber means" drifting apart.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(PhysicsStateSwitcher))]
    public sealed class BounceTraversalController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string jumpActionName = "Jump";

        [Header("Rubber state input")]
        [SerializeField] private RubberInputMode rubberInputMode = RubberInputMode.Hold;

        [Header("Ground movement")]
        [SerializeField] private float groundMoveForce = 25f;
        [SerializeField] private float maxGroundSpeed = 12f;

        [Tooltip("Impulse, so this is a change in momentum rather than a force integrated over " +
                 "one 0.02 s step. 6 on a 1 kg body reaches roughly 1.8 m, which every target in " +
                 "the chamber is deliberately placed above.")]
        [SerializeField] private float jumpImpulse = 6f;

        [Header("Air steering")]
        [Tooltip("Deliberately weaker than ground movement. If air control is strong enough to " +
                 "fly, the chamber stops testing bouncing and starts testing thrust.")]
        [SerializeField] private float airMoveForce = 10f;

        [Header("Ground check")]
        [SerializeField] private float groundCheckDistance = 0.15f;
        [SerializeField] private LayerMask groundLayers = ~0;

        private Rigidbody body;
        private Collider bodyCollider;
        private PhysicsStateSwitcher stateSwitcher;

        private InputAction moveAction;
        private InputAction jumpAction;

        private Vector2 moveInput;
        private bool jumpLatched;

        public bool IsGrounded { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            bodyCollider = GetComponent<Collider>();
            stateSwitcher = GetComponent<PhysicsStateSwitcher>();
            ResolveActions();
        }

        private void ResolveActions()
        {
            if (inputActions == null)
            {
                Debug.LogWarning($"{nameof(BounceTraversalController)}: no InputActionAsset assigned.", this);
                return;
            }

            InputActionMap map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
            if (map == null)
            {
                Debug.LogWarning($"{nameof(BounceTraversalController)}: action map '{actionMapName}' not found.", this);
                return;
            }

            moveAction = map.FindAction(moveActionName, throwIfNotFound: false);
            jumpAction = map.FindAction(jumpActionName, throwIfNotFound: false);
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

            ReadRubberInput();
        }

        private void ReadRubberInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (rubberInputMode == RubberInputMode.Hold)
            {
                // Read every frame rather than on the press edge, so that releasing the key mid-air
                // returns the body to rigid. That is what makes "be rubber only at the moment of
                // impact" an attemptable technique rather than a mode you are stuck in.
                PhysicalState desired = keyboard.leftShiftKey.isPressed
                    ? PhysicalState.Rubber
                    : PhysicalState.Rigid;

                if (stateSwitcher.CurrentState != desired)
                {
                    stateSwitcher.SetState(desired);
                }

                return;
            }

            if (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame)
            {
                stateSwitcher.Toggle();
            }
        }

        private void FixedUpdate()
        {
            IsGrounded = CheckGrounded();

            Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y);
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            float force = IsGrounded ? groundMoveForce : airMoveForce;
            body.AddForce(direction * force, ForceMode.Force);

            // Grounded only. Clamping in the air would cap the horizontal speed carried out of a
            // ramp launch, which is the quantity the whole chamber is built to exploit.
            if (IsGrounded && direction.sqrMagnitude > 0f)
            {
                ClampGroundSpeed();
            }

            if (jumpLatched)
            {
                jumpLatched = false;

                if (IsGrounded)
                {
                    body.AddForce(Vector3.up * jumpImpulse, ForceMode.Impulse);
                }
            }
        }

        private void ClampGroundSpeed()
        {
            Vector3 velocity = body.linearVelocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);

            if (horizontal.magnitude <= maxGroundSpeed)
            {
                return;
            }

            horizontal = horizontal.normalized * maxGroundSpeed;
            body.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        }

        private bool CheckGrounded()
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
