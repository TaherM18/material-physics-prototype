using System;
using Experiment001;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Experiment002
{
    /// <summary>Which rubber material variant is currently loaded into the state switcher.</summary>
    public enum RestitutionVariant
    {
        /// <summary>bounceCombine = Average. Effective restitution against a bounciness-0 surface is halved.</summary>
        AverageCombine,

        /// <summary>bounceCombine = Maximum. The bouncier of the two surfaces governs the bounce.</summary>
        MaximumCombine
    }

    public readonly struct AttemptContext
    {
        public readonly int AttemptNumber;
        public readonly RestitutionVariant Variant;

        public AttemptContext(int attemptNumber, RestitutionVariant variant)
        {
            AttemptNumber = attemptNumber;
            Variant = variant;
        }
    }

    /// <summary>
    /// Attempt lifecycle for the traversal chamber: reset to the start, clear the targets, and
    /// switch between the two rubber materials under test.
    /// <para>
    /// The variant swap is the first controlled variable of this experiment. Experiment 001 ran
    /// with every combine mode at <c>Average</c>, which meant a rubber material marked 0.85
    /// actually produced a restitution of 0.425 against the bounciness-0 environment, and permitted
    /// exactly two bounces from 5 m. Before player skill can be assessed at all, it has to be
    /// established whether the observed chaining ceiling was that arithmetic or a limit of control.
    /// Swapping the material live, rather than editing a value, keeps the comparison honest: the
    /// two assets differ in one field and nothing else.
    /// </para>
    /// </summary>
    public sealed class TraversalRunner : MonoBehaviour
    {
        [Header("Subject")]
        [SerializeField] private Rigidbody subject;
        [SerializeField] private PhysicsStateSwitcher stateSwitcher;
        [SerializeField] private Transform startPoint;

        [Header("Targets")]
        [SerializeField] private BounceTarget[] targets = Array.Empty<BounceTarget>();

        [Header("Restitution variant under test")]
        [SerializeField] private RestitutionVariant variant = RestitutionVariant.AverageCombine;
        [SerializeField] private PhysicsMaterial rubberAverageCombine;
        [SerializeField] private PhysicsMaterial rubberMaximumCombine;

        private RigidbodyInterpolation interpolationToRestore;
        private int interpolationRestoreCountdown;

        public int AttemptNumber { get; private set; }
        public RestitutionVariant Variant => variant;

        /// <summary>Raised when an attempt begins. Instrumentation listens; nothing here requires it.</summary>
        public event Action<AttemptContext> AttemptStarted;

        private void Start()
        {
            ApplyVariant();
            BeginAttempt();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                BeginAttempt();
            }

            if (keyboard.cKey.wasPressedThisFrame)
            {
                variant = variant == RestitutionVariant.AverageCombine
                    ? RestitutionVariant.MaximumCombine
                    : RestitutionVariant.AverageCombine;

                ApplyVariant();
                BeginAttempt();
            }
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

        private void ApplyVariant()
        {
            if (stateSwitcher == null)
            {
                return;
            }

            PhysicsMaterial material = variant == RestitutionVariant.AverageCombine
                ? rubberAverageCombine
                : rubberMaximumCombine;

            if (material == null)
            {
                Debug.LogWarning($"No rubber material assigned for variant {variant}.", this);
                return;
            }

            stateSwitcher.OverrideMaterial(PhysicalState.Rubber, material);
        }

        public void BeginAttempt()
        {
            if (subject == null || startPoint == null)
            {
                return;
            }

            AttemptNumber++;

            foreach (BounceTarget target in targets)
            {
                if (target != null)
                {
                    target.ResetForNewAttempt();
                }
            }

            Teleport(startPoint.position);
            AttemptStarted?.Invoke(new AttemptContext(AttemptNumber, variant));
        }

        private void Teleport(Vector3 position)
        {
            interpolationToRestore = subject.interpolation;
            subject.interpolation = RigidbodyInterpolation.None;
            interpolationRestoreCountdown = 2;

            subject.linearVelocity = Vector3.zero;
            subject.angularVelocity = Vector3.zero;
            subject.position = position;
            subject.rotation = Quaternion.identity;
            subject.transform.SetPositionAndRotation(position, Quaternion.identity);

            // This project has m_AutoSyncTransforms disabled, so the Transform write above would
            // not otherwise reach the physics scene until the next simulation step.
            Physics.SyncTransforms();

            if (subject.IsSleeping())
            {
                subject.WakeUp();
            }
        }
    }
}
