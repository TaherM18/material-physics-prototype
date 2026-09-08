using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Experiment001
{
    /// <summary>
    /// One repeatable test: where the subject starts, how fast it is moving when released, and
    /// which physical state it is in.
    /// </summary>
    [Serializable]
    public class Scenario
    {
        public string name = "Scenario";

        [Tooltip("Where the subject is placed. For the slope test this is a child of the ramp, so " +
                 "the ramp can be re-angled without recomputing the start position by hand.")]
        public Transform startPoint;

        [Tooltip("Velocity applied immediately after teleporting. Zero means release from rest.")]
        public Vector3 initialVelocity;

        [Tooltip("If set, the subject is forced into stateOnStart before the run begins, so a run " +
                 "cannot accidentally be recorded in the wrong state.")]
        public bool forceState = true;

        public PhysicalState stateOnStart = PhysicalState.Rigid;
    }

    /// <summary>Describes the run that has just started. Passed to whatever is observing.</summary>
    public readonly struct RunContext
    {
        public readonly string ScenarioName;
        public readonly PhysicalState State;
        public readonly Vector3 StartPosition;
        public readonly Vector3 InitialVelocity;

        public RunContext(string scenarioName, PhysicalState state, Vector3 startPosition, Vector3 initialVelocity)
        {
            ScenarioName = scenarioName;
            State = state;
            StartPosition = startPosition;
            InitialVelocity = initialVelocity;
        }
    }

    /// <summary>
    /// Drives repeatable runs of the Experiment 001 scenarios and provides the hotkeys.
    /// <para>
    /// The point of this component is that a run always begins from an identical physical state:
    /// same position, same rotation, zero residual linear and angular velocity. Without that,
    /// comparing "rigid" against "rubber" compares two runs that did not start the same way, and
    /// every number in the log is worthless.
    /// </para>
    /// </summary>
    public sealed class ExperimentRunner : MonoBehaviour
    {
        [Header("Subject")]
        [SerializeField] private Rigidbody subject;
        [SerializeField] private PhysicsStateSwitcher stateSwitcher;

        [Header("Scenarios (hotkeys 1, 2, 3)")]
        [SerializeField] private Scenario[] scenarios = Array.Empty<Scenario>();

        [Header("Experimental override (leave off for the controlled first pass)")]
        [Tooltip("Overrides Physics.bounceThreshold at runtime only. The project asset is never " +
                 "modified, and the original value is restored when this component is disabled. " +
                 "This exists for the deliberate sweep in the experiment document, not to make " +
                 "the experiment 'work'.")]
        [SerializeField] private bool overrideBounceThreshold;

        [SerializeField] private float bounceThresholdOverride = 2f;

        private int lastScenarioIndex = -1;
        private float originalBounceThreshold;

        // A teleport with interpolation enabled would be rendered as a smear from the old position
        // to the new one, because interpolation blends between the two most recent physics poses.
        // Interpolation is therefore suppressed across the teleport and restored a couple of steps
        // later, once the body has two consistent poses to blend between again.
        private RigidbodyInterpolation interpolationToRestore;
        private int interpolationRestoreCountdown;

        /// <summary>
        /// Raised when a run begins. Instrumentation subscribes to this. Nothing here depends on
        /// anything subscribing, so the logger can be deleted without touching this file.
        /// </summary>
        public event Action<RunContext> RunStarted;

        private void OnEnable()
        {
            originalBounceThreshold = Physics.bounceThreshold;
        }

        private void OnDisable()
        {
            // Never leave a modified global behind. Physics.bounceThreshold persists across play
            // sessions in the editor, so failing to restore it would silently poison later runs.
            Physics.bounceThreshold = originalBounceThreshold;
        }

        private void Update()
        {
            ApplyBounceThresholdOverride();
            ReadHotkeys();
        }

        private void FixedUpdate()
        {
            if (interpolationRestoreCountdown > 0)
            {
                interpolationRestoreCountdown--;
                if (interpolationRestoreCountdown == 0 && subject != null)
                {
                    subject.interpolation = interpolationToRestore;
                }
            }
        }

        private void ApplyBounceThresholdOverride()
        {
            // Applied every frame rather than once, so the value can be swept from the inspector
            // during play mode without leaving and re-entering play.
            if (overrideBounceThreshold)
            {
                if (!Mathf.Approximately(Physics.bounceThreshold, bounceThresholdOverride))
                {
                    Physics.bounceThreshold = bounceThresholdOverride;
                }
            }
            else if (!Mathf.Approximately(Physics.bounceThreshold, originalBounceThreshold))
            {
                Physics.bounceThreshold = originalBounceThreshold;
            }
        }

        private void ReadHotkeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame) Run(0);
            if (keyboard.digit2Key.wasPressedThisFrame) Run(1);
            if (keyboard.digit3Key.wasPressedThisFrame) Run(2);

            if (keyboard.rKey.wasPressedThisFrame && lastScenarioIndex >= 0)
            {
                Run(lastScenarioIndex);
            }

            if (keyboard.tabKey.wasPressedThisFrame && stateSwitcher != null)
            {
                stateSwitcher.Toggle();
            }
        }

        public void Run(int index)
        {
            if (!IsValidIndex(index) || subject == null)
            {
                return;
            }

            Scenario scenario = scenarios[index];
            if (scenario.startPoint == null)
            {
                Debug.LogWarning($"Scenario '{scenario.name}' has no start point assigned.", this);
                return;
            }

            lastScenarioIndex = index;

            if (scenario.forceState && stateSwitcher != null)
            {
                stateSwitcher.SetState(scenario.stateOnStart);
            }

            Teleport(scenario.startPoint.position, scenario.initialVelocity);

            PhysicalState state = stateSwitcher != null ? stateSwitcher.CurrentState : PhysicalState.Rigid;
            RunStarted?.Invoke(new RunContext(scenario.name, state, scenario.startPoint.position, scenario.initialVelocity));
        }

        private void Teleport(Vector3 position, Vector3 velocity)
        {
            interpolationToRestore = subject.interpolation;
            subject.interpolation = RigidbodyInterpolation.None;
            interpolationRestoreCountdown = 2;

            // Zeroed before the move so no residual spin or drift survives from the previous run.
            subject.linearVelocity = Vector3.zero;
            subject.angularVelocity = Vector3.zero;

            // Writing Rigidbody.position/rotation teleports the body in the physics scene.
            // The Transform is set as well because this project has m_AutoSyncTransforms disabled,
            // so the two representations do not reconcile until the next simulation step or an
            // explicit sync, and the render position would otherwise lag by one frame.
            subject.position = position;
            subject.rotation = Quaternion.identity;
            subject.transform.SetPositionAndRotation(position, Quaternion.identity);
            Physics.SyncTransforms();

            subject.linearVelocity = velocity;

            if (subject.IsSleeping())
            {
                subject.WakeUp();
            }
        }

        private bool IsValidIndex(int index) => index >= 0 && index < scenarios.Length;
    }
}
