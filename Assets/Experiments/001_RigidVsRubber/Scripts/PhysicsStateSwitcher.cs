using System;
using UnityEngine;

namespace Experiment001
{
    public enum PhysicalState
    {
        Rigid,
        Rubber
    }

    /// <summary>
    /// Everything that "being in a state" means, for Experiment 001.
    /// <para>
    /// This is deliberately a plain serializable class living on one component, not a
    /// ScriptableObject and not a MaterialDefinition asset. There are exactly two states, they
    /// exist on exactly one object, and nothing else in the project reads them. An asset-based
    /// definition would add indirection without removing any duplication.
    /// </para>
    /// </summary>
    [Serializable]
    public class PhysicalStateConfig
    {
        [Tooltip("The Physics Material asset applied to the collider. This is the thing under test.")]
        public PhysicsMaterial physicsMaterial;

        [Tooltip("Held at 1 in both states for the controlled comparison. Change one run at a time.")]
        [Min(0.0001f)]
        public float mass = 1f;

        [Tooltip("Rigidbody linear damping. 0 in both states so that air resistance does not " +
                 "quietly become a second independent variable.")]
        [Min(0f)]
        public float linearDamping;

        [Min(0f)]
        public float angularDamping = 0.05f;

        [Tooltip("Optional, purely visual, so the current state is readable on screen.")]
        public Material visualMaterial;
    }

    /// <summary>
    /// Applies one of two physical configurations to this Rigidbody and Collider.
    /// <para>
    /// This component owns the entire definition of "rigid" and "rubber" for Experiment 001.
    /// It does not decide <em>when</em> to switch: that is the caller's job, which keeps the
    /// question of physical behaviour (this experiment) separate from the question of player
    /// control (Experiment 002).
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class PhysicsStateSwitcher : MonoBehaviour
    {
        [SerializeField] private PhysicalStateConfig rigidState = new PhysicalStateConfig();
        [SerializeField] private PhysicalStateConfig rubberState = new PhysicalStateConfig();
        [SerializeField] private PhysicalState startingState = PhysicalState.Rigid;

        private Rigidbody body;
        private Collider bodyCollider;
        private Renderer bodyRenderer;

        public PhysicalState CurrentState { get; private set; }

        /// <summary>
        /// Raised after a state has been applied. Instrumentation subscribes to this; no gameplay
        /// code is required to. Deleting every listener leaves this component fully functional.
        /// </summary>
        public event Action<PhysicalState> StateApplied;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            bodyCollider = GetComponent<Collider>();
            bodyRenderer = GetComponent<Renderer>();

            Apply(startingState);
        }

        public PhysicalStateConfig ConfigFor(PhysicalState state)
            => state == PhysicalState.Rigid ? rigidState : rubberState;

        public void SetState(PhysicalState state) => Apply(state);

        public void Toggle()
            => Apply(CurrentState == PhysicalState.Rigid ? PhysicalState.Rubber : PhysicalState.Rigid);

        private void Apply(PhysicalState state)
        {
            PhysicalStateConfig config = ConfigFor(state);

            // sharedMaterial, never material. Reading Collider.material clones the asset and
            // returns the clone, which would mean the numbers we measure no longer correspond to
            // the asset we edited in the inspector, and would leak one copy per access.
            bodyCollider.sharedMaterial = config.physicsMaterial;

            // Mass is changed here for completeness, but both states use 1 kg in the default
            // configuration. Note that assigning mass mid-flight preserves linear velocity, so it
            // silently changes the body's momentum. That is a real gameplay lever later; for now
            // it is held constant precisely so it cannot confound the restitution measurement.
            body.mass = config.mass;
            body.linearDamping = config.linearDamping;
            body.angularDamping = config.angularDamping;

            // A sleeping Rigidbody will not re-evaluate contacts, so a state change applied while
            // asleep would appear to do nothing until something else disturbed the body.
            if (body.IsSleeping())
            {
                body.WakeUp();
            }

            if (bodyRenderer != null && config.visualMaterial != null)
            {
                bodyRenderer.sharedMaterial = config.visualMaterial;
            }

            CurrentState = state;
            StateApplied?.Invoke(state);
        }
    }
}
