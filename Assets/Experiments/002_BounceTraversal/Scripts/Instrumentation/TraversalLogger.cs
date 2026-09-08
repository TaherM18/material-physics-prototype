using System.Collections.Generic;
using System.Text;
using Experiment001;
using UnityEngine;

namespace Experiment002.Instrumentation
{
    /// <summary>
    /// Records each impact during a traversal attempt and prints one summary when the attempt ends.
    /// <para>
    /// Separate from Experiment 001's BounceLogger rather than an extension of it: that one is
    /// built around a vertical drop, where apex height is the measurement and the impact is always
    /// head-on. Here the interesting quantities are the <em>angles</em> and the split between
    /// horizontal and vertical velocity across an impact, because the mechanic being tested is the
    /// conversion of cheap horizontal speed into expensive height.
    /// </para>
    /// <para>
    /// Observation only. It subscribes to <see cref="TraversalRunner.AttemptStarted"/> and to each
    /// <see cref="BounceTarget"/>, and reads the Rigidbody. Nothing holds a reference back to it,
    /// so this folder can be deleted without touching the experiment.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TraversalLogger : MonoBehaviour
    {
        private sealed class ImpactRecord
        {
            public int Index;
            public float Time;
            public PhysicalState State;
            public string Surface;

            public float SpeedIn;
            public float SpeedOut;

            /// <summary>Angle between the incoming velocity and the contact normal, in degrees.</summary>
            public float AngleIn;
            public float AngleOut;

            public float VerticalIn;
            public float VerticalOut;
            public float HorizontalIn;
            public float HorizontalOut;

            public float HeightAtImpact;
            public bool HasApex;
            public float ApexHeight;
        }

        [Header("Wiring (all optional)")]
        [SerializeField] private TraversalRunner runner;
        [SerializeField] private PhysicsStateSwitcher stateSwitcher;
        [SerializeField] private BounceTarget[] targets = new BounceTarget[0];

        [Header("Measurement")]
        [SerializeField] private float floorY;

        [Tooltip("An attempt is summarised once the body has been slow and grounded for this long.")]
        [SerializeField] private float restSettleSeconds = 1.5f;

        [SerializeField] private float restSpeedThreshold = 0.5f;
        [SerializeField] private float maxAttemptSeconds = 60f;

        [Header("Output")]
        [SerializeField] private bool showHud = true;

        private Rigidbody body;
        private Collider bodyCollider;

        private readonly List<ImpactRecord> impacts = new List<ImpactRecord>();
        private readonly List<string> reachedTargets = new List<string>();

        private bool attemptActive;
        private int attemptNumber;
        private RestitutionVariant variant;
        private float attemptStartTime;
        private float bestHeight;

        private float previousVerticalVelocity;
        private float belowRestSince = -1f;

        private bool awaitingPostImpact;
        private Vector3 pendingNormal;
        private ImpactRecord pendingRecord;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            bodyCollider = GetComponent<Collider>();

            if (runner == null) runner = FindAnyObjectByType<TraversalRunner>();
            if (stateSwitcher == null) stateSwitcher = GetComponent<PhysicsStateSwitcher>();
        }

        private void OnEnable()
        {
            if (runner != null)
            {
                runner.AttemptStarted += HandleAttemptStarted;
            }

            foreach (BounceTarget target in targets)
            {
                if (target != null)
                {
                    target.Reached += HandleTargetReached;
                }
            }
        }

        private void OnDisable()
        {
            if (runner != null)
            {
                runner.AttemptStarted -= HandleAttemptStarted;
            }

            foreach (BounceTarget target in targets)
            {
                if (target != null)
                {
                    target.Reached -= HandleTargetReached;
                }
            }
        }

        private void HandleAttemptStarted(AttemptContext context)
        {
            // A new attempt discards the previous one without printing. Summaries are printed when
            // an attempt ends on its own, so that repeatedly pressing R while experimenting does
            // not bury the console.
            impacts.Clear();
            reachedTargets.Clear();

            awaitingPostImpact = false;
            pendingRecord = null;
            belowRestSince = -1f;

            attemptActive = true;
            attemptNumber = context.AttemptNumber;
            variant = context.Variant;
            attemptStartTime = Time.time;
            bestHeight = HeightAboveFloor();
            previousVerticalVelocity = body.linearVelocity.y;
        }

        private void HandleTargetReached(BounceTarget target)
        {
            if (!attemptActive)
            {
                return;
            }

            reachedTargets.Add(target.TargetName);

            Debug.Log($"[attempt {attemptNumber}/{variant}] reached {target.TargetName} " +
                      $"({target.HeightAboveFloor:F1} m) after {impacts.Count} impacts, " +
                      $"t={Time.time - attemptStartTime:F2}s");
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!attemptActive || collision.contactCount == 0)
            {
                return;
            }

            pendingNormal = collision.GetContact(0).normal;

            Vector3 velocityIn = -collision.relativeVelocity;
            float speedIn = velocityIn.magnitude;

            pendingRecord = new ImpactRecord
            {
                Index = impacts.Count + 1,
                Time = Time.time - attemptStartTime,
                State = stateSwitcher != null ? stateSwitcher.CurrentState : PhysicalState.Rigid,
                Surface = collision.collider != null ? collision.collider.name : "?",
                SpeedIn = speedIn,
                AngleIn = speedIn > 0.001f ? Vector3.Angle(-velocityIn, pendingNormal) : 0f,
                VerticalIn = velocityIn.y,
                HorizontalIn = new Vector3(velocityIn.x, 0f, velocityIn.z).magnitude,
                HeightAtImpact = HeightAboveFloor(),
            };

            awaitingPostImpact = true;
        }

        private void FixedUpdate()
        {
            if (!attemptActive)
            {
                return;
            }

            CapturePostImpact();
            TrackApexAndPeak();
            DetectEndOfAttempt();

            previousVerticalVelocity = body.linearVelocity.y;
        }

        private void CapturePostImpact()
        {
            if (!awaitingPostImpact || pendingRecord == null)
            {
                return;
            }

            Vector3 velocityOut = body.linearVelocity;

            pendingRecord.SpeedOut = velocityOut.magnitude;
            pendingRecord.AngleOut = pendingRecord.SpeedOut > 0.001f
                ? Vector3.Angle(velocityOut, pendingNormal)
                : 0f;
            pendingRecord.VerticalOut = velocityOut.y;
            pendingRecord.HorizontalOut = new Vector3(velocityOut.x, 0f, velocityOut.z).magnitude;

            impacts.Add(pendingRecord);
            pendingRecord = null;
            awaitingPostImpact = false;
        }

        private void TrackApexAndPeak()
        {
            float height = HeightAboveFloor();
            if (height > bestHeight)
            {
                bestHeight = height;
            }

            float verticalVelocity = body.linearVelocity.y;
            bool crossedApex = previousVerticalVelocity > 0f && verticalVelocity <= 0f;

            if (!crossedApex || impacts.Count == 0)
            {
                return;
            }

            ImpactRecord last = impacts[impacts.Count - 1];
            if (last.HasApex)
            {
                return;
            }

            last.HasApex = true;
            last.ApexHeight = height;
        }

        private void DetectEndOfAttempt()
        {
            float elapsed = Time.time - attemptStartTime;
            bool slow = body.linearVelocity.magnitude < restSpeedThreshold;

            if (slow || body.IsSleeping())
            {
                if (belowRestSince < 0f)
                {
                    belowRestSince = Time.time;
                }
            }
            else
            {
                belowRestSince = -1f;
            }

            bool settled = belowRestSince >= 0f && Time.time - belowRestSince >= restSettleSeconds;

            if (settled || elapsed >= maxAttemptSeconds)
            {
                attemptActive = false;
                Debug.Log(BuildSummary(elapsed, elapsed >= maxAttemptSeconds));
            }
        }

        private float HeightAboveFloor() => body.position.y - bodyCollider.bounds.extents.y - floorY;

        private string BuildSummary(float elapsed, bool timedOut)
        {
            StringBuilder text = new StringBuilder();

            text.AppendLine($"=== Experiment 002 | attempt {attemptNumber} | {variant} ===");

            PhysicsMaterial rubber = stateSwitcher != null
                ? stateSwitcher.ConfigFor(PhysicalState.Rubber).physicsMaterial
                : null;

            text.AppendLine(
                $"rubber material={(rubber != null ? rubber.name : "none")} " +
                $"bounciness={(rubber != null ? rubber.bounciness.ToString("F2") : "n/a")} " +
                $"bounceCombine={(rubber != null ? rubber.bounceCombine.ToString() : "n/a")} " +
                $"dynFriction={(rubber != null ? rubber.dynamicFriction.ToString("F2") : "n/a")}");

            text.AppendLine(
                $"Physics.bounceThreshold={Physics.bounceThreshold:F2}  " +
                $"fixedDeltaTime={Time.fixedDeltaTime:F4}  " +
                $"collisionDetection={body.collisionDetectionMode}");

            text.AppendLine(
                $"impacts={impacts.Count}  peak height={bestHeight:F2} m  duration={elapsed:F2}s" +
                $"{(timedOut ? "  (TIMED OUT)" : string.Empty)}");

            text.AppendLine($"targets reached: {(reachedTargets.Count > 0 ? string.Join(", ", reachedTargets) : "none")}");
            text.AppendLine();
            text.AppendLine("  #     t(s)  state   surface        spd_in  ang_in   spd_out  ang_out    h_in   h_out    v_in   v_out    apex");

            foreach (ImpactRecord impact in impacts)
            {
                string surface = impact.Surface.Length > 12 ? impact.Surface.Substring(0, 12) : impact.Surface;

                text.AppendLine(
                    $"{impact.Index,3}  {impact.Time,7:F2}  {impact.State.ToString().Substring(0, 5),-6} " +
                    $"{surface,-13} {impact.SpeedIn,7:F2} {impact.AngleIn,7:F1} " +
                    $"{impact.SpeedOut,9:F2} {impact.AngleOut,8:F1} " +
                    $"{impact.HorizontalIn,7:F2} {impact.HorizontalOut,7:F2} " +
                    $"{impact.VerticalIn,7:F2} {impact.VerticalOut,7:F2} " +
                    $"{(impact.HasApex ? impact.ApexHeight.ToString("F2") : "-"),7}");
            }

            text.AppendLine();
            text.AppendLine("h_ = horizontal speed, v_ = vertical speed, ang_ = degrees from the contact normal.");
            text.AppendLine("A conversion is working when h_in is large, v_out is large, and apex exceeds h_in.");

            return text.ToString();
        }

        private void OnGUI()
        {
            if (!showHud)
            {
                return;
            }

            PhysicalState state = stateSwitcher != null ? stateSwitcher.CurrentState : PhysicalState.Rigid;
            Vector3 velocity = body.linearVelocity;

            GUILayout.BeginArea(new Rect(10f, 10f, 400f, 240f), GUI.skin.box);
            GUILayout.Label($"STATE: {state}          variant: {variant}");
            GUILayout.Label($"attempt {attemptNumber}{(attemptActive ? "" : "  (ended)")}");
            GUILayout.Label($"height {HeightAboveFloor():F2} m    peak {bestHeight:F2} m");
            GUILayout.Label($"speed  h {new Vector3(velocity.x, 0f, velocity.z).magnitude:F2}   v {velocity.y:F2}");
            GUILayout.Label($"impacts: {impacts.Count}");
            GUILayout.Label($"reached: {(reachedTargets.Count > 0 ? string.Join(", ", reachedTargets) : "-")}");
            GUILayout.Space(6f);
            GUILayout.Label("WASD move    Space jump    HOLD L-Shift = rubber");
            GUILayout.Label("R = reset attempt    C = swap combine-mode variant");
            GUILayout.EndArea();
        }
    }
}
