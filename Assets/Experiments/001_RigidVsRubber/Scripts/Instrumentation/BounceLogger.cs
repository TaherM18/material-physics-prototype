using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Experiment001.Instrumentation
{
    /// <summary>
    /// Records what actually happened during a run: impact speeds, rebound speeds, and bounce
    /// apex heights, then prints one summary table when the body comes to rest.
    /// <para>
    /// This is instrumentation, not gameplay. It only ever observes: it subscribes to
    /// <see cref="ExperimentRunner.RunStarted"/> and reads the Rigidbody. Nothing in
    /// <see cref="PhysicsStateSwitcher"/>, <see cref="TestSubjectController"/> or
    /// <see cref="ExperimentRunner"/> holds a reference to it, so this whole folder can be deleted
    /// without touching a line of the experiment code.
    /// </para>
    /// <para>
    /// Console discipline: per-event logging is off by default. A run produces exactly one log
    /// entry, the summary. The live HUD covers the "what is happening right now" case that
    /// per-frame logging is usually abused for.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class BounceLogger : MonoBehaviour
    {
        private sealed class BounceRecord
        {
            public int Index;
            public float Time;

            /// <summary>Closing speed of the two bodies before the solver responded.</summary>
            public float ImpactSpeed;

            /// <summary>Component of that closing speed along the contact normal.</summary>
            public float ImpactNormalSpeed;

            /// <summary>Speed along the normal read inside OnCollisionEnter itself.</summary>
            public float NormalSpeedInCallback;

            /// <summary>Speed along the normal read on the following physics step.</summary>
            public float PostImpactNormalSpeed;

            public bool HasApex;
            public float ApexHeight;
            public float ApexTime;
        }

        [Header("Wiring (all optional)")]
        [SerializeField] private ExperimentRunner runner;
        [SerializeField] private PhysicsStateSwitcher stateSwitcher;

        [Header("Measurement")]
        [Tooltip("World Y of the top surface of the ground. Apex height is reported as the " +
                 "clearance of the bottom of the collider above this plane.")]
        [SerializeField] private float groundSurfaceY;

        [Tooltip("A run is considered finished once the body has stayed below this speed for " +
                 "restSettleSeconds, or once the Rigidbody falls asleep.")]
        [SerializeField] private float restSpeedThreshold = 0.05f;

        [SerializeField] private float restSettleSeconds = 1.0f;

        [Tooltip("Safety net so a run that never settles still reports.")]
        [SerializeField] private float maxRunSeconds = 30f;

        [Header("Output")]
        [SerializeField] private bool logEachEvent;
        [SerializeField] private bool showHud = true;

        private Rigidbody body;
        private Collider bodyCollider;

        private readonly List<BounceRecord> records = new List<BounceRecord>();

        private bool runActive;
        private string runLabel = "(none)";
        private PhysicalState runState;
        private float runStartTime;
        private float runStartHeight;

        private float previousVerticalVelocity;
        private float belowRestSince = -1f;

        private bool awaitingPostImpact;
        private Vector3 pendingNormal;
        private BounceRecord pendingRecord;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            bodyCollider = GetComponent<Collider>();

            if (runner == null) runner = FindAnyObjectByType<ExperimentRunner>();
            if (stateSwitcher == null) stateSwitcher = GetComponent<PhysicsStateSwitcher>();
        }

        private void OnEnable()
        {
            if (runner != null)
            {
                runner.RunStarted += HandleRunStarted;
            }
        }

        private void OnDisable()
        {
            if (runner != null)
            {
                runner.RunStarted -= HandleRunStarted;
            }
        }

        private void HandleRunStarted(RunContext context)
        {
            records.Clear();
            awaitingPostImpact = false;
            pendingRecord = null;
            belowRestSince = -1f;

            runActive = true;
            runLabel = context.ScenarioName;
            runState = context.State;
            runStartTime = Time.time;
            runStartHeight = HeightAboveGround();
            previousVerticalVelocity = body.linearVelocity.y;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!runActive || collision.contactCount == 0)
            {
                return;
            }

            // relativeVelocity is documented as the closing velocity of the two bodies, and is the
            // only pre-response quantity available here. The velocity read from the Rigidbody in
            // this same callback is recorded separately, because whether it is pre- or post-solve
            // is exactly the kind of thing that should be measured rather than assumed.
            pendingNormal = collision.GetContact(0).normal;

            pendingRecord = new BounceRecord
            {
                Index = records.Count + 1,
                Time = Time.time - runStartTime,
                ImpactSpeed = collision.relativeVelocity.magnitude,
                ImpactNormalSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, pendingNormal)),
                NormalSpeedInCallback = Vector3.Dot(body.linearVelocity, pendingNormal),
            };

            awaitingPostImpact = true;
        }

        private void FixedUpdate()
        {
            if (!runActive)
            {
                return;
            }

            CapturePostImpactVelocity();
            DetectApex();
            DetectRest();

            previousVerticalVelocity = body.linearVelocity.y;
        }

        private void CapturePostImpactVelocity()
        {
            if (!awaitingPostImpact || pendingRecord == null)
            {
                return;
            }

            pendingRecord.PostImpactNormalSpeed = Vector3.Dot(body.linearVelocity, pendingNormal);
            records.Add(pendingRecord);

            if (logEachEvent)
            {
                Debug.Log($"[{runLabel}/{runState}] impact #{pendingRecord.Index} " +
                          $"closing {pendingRecord.ImpactNormalSpeed:F3} m/s -> " +
                          $"rebound {pendingRecord.PostImpactNormalSpeed:F3} m/s");
            }

            pendingRecord = null;
            awaitingPostImpact = false;
        }

        private void DetectApex()
        {
            // An apex is the frame on which vertical velocity stops being positive. Sampling this
            // in FixedUpdate rather than Update matters: velocity is only meaningful at physics
            // step boundaries, and Update runs at an unrelated rate.
            float verticalVelocity = body.linearVelocity.y;
            bool crossedApex = previousVerticalVelocity > 0f && verticalVelocity <= 0f;

            if (!crossedApex || records.Count == 0)
            {
                return;
            }

            BounceRecord last = records[records.Count - 1];
            if (last.HasApex)
            {
                return;
            }

            last.HasApex = true;
            last.ApexHeight = HeightAboveGround();
            last.ApexTime = Time.time - runStartTime;

            if (logEachEvent)
            {
                Debug.Log($"[{runLabel}/{runState}] apex after bounce #{last.Index}: {last.ApexHeight:F3} m");
            }
        }

        private void DetectRest()
        {
            float elapsed = Time.time - runStartTime;
            bool asleep = body.IsSleeping();
            bool slow = body.linearVelocity.magnitude < restSpeedThreshold;

            if (slow || asleep)
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

            if (settled || elapsed >= maxRunSeconds)
            {
                runActive = false;
                Debug.Log(BuildSummary(elapsed, asleep, elapsed >= maxRunSeconds));
            }
        }

        private float HeightAboveGround() => body.position.y - bodyCollider.bounds.extents.y - groundSurfaceY;

        private string BuildSummary(float elapsed, bool asleep, bool timedOut)
        {
            PhysicalStateConfig config = stateSwitcher != null ? stateSwitcher.ConfigFor(runState) : null;
            PhysicsMaterial material = config != null ? config.physicsMaterial : null;

            StringBuilder text = new StringBuilder();

            text.AppendLine($"=== Experiment 001 | {runLabel} | {runState} ===");
            text.AppendLine(
                $"material={(material != null ? material.name : "none")} " +
                $"bounciness={(material != null ? material.bounciness.ToString("F2") : "n/a")} " +
                $"dynFriction={(material != null ? material.dynamicFriction.ToString("F2") : "n/a")} " +
                $"bounceCombine={(material != null ? material.bounceCombine.ToString() : "n/a")}");
            text.AppendLine(
                $"mass={body.mass:F2} kg  linearDamping={body.linearDamping:F2}  " +
                $"interpolation={body.interpolation}  collisionDetection={body.collisionDetectionMode}");
            text.AppendLine(
                $"Physics.bounceThreshold={Physics.bounceThreshold:F2}  " +
                $"fixedDeltaTime={Time.fixedDeltaTime:F4}  gravity={Physics.gravity.y:F2}");
            text.AppendLine($"start height={runStartHeight:F3} m   bounces={records.Count}   " +
                            $"time to rest={elapsed:F2} s   {(asleep ? "(rigidbody asleep)" : string.Empty)}" +
                            $"{(timedOut ? "(TIMED OUT)" : string.Empty)}");
            text.AppendLine();
            text.AppendLine("  #     t(s)   closing   inCallback   rebound   e(impulse)     apex   e(apex)");

            for (int i = 0; i < records.Count; i++)
            {
                BounceRecord record = records[i];

                float restitution = record.ImpactNormalSpeed > 0.0001f
                    ? Mathf.Abs(record.PostImpactNormalSpeed) / record.ImpactNormalSpeed
                    : 0f;

                // Restitution can also be recovered from the heights alone: a bounce that returns
                // e times the impact speed reaches e squared times the height. If these two columns
                // disagree, something other than restitution is removing energy.
                string apexRestitution = "-";
                if (record.HasApex && i > 0 && records[i - 1].HasApex && records[i - 1].ApexHeight > 0.0001f)
                {
                    apexRestitution = Mathf.Sqrt(record.ApexHeight / records[i - 1].ApexHeight).ToString("F3");
                }

                text.AppendLine(
                    $"{record.Index,3}  {record.Time,7:F2}  {record.ImpactNormalSpeed,8:F3}  " +
                    $"{record.NormalSpeedInCallback,11:F3}  {record.PostImpactNormalSpeed,8:F3}  " +
                    $"{restitution,10:F3}  {(record.HasApex ? record.ApexHeight.ToString("F3") : "-"),7}  " +
                    $"{apexRestitution,8}");
            }

            if (records.Count > 0)
            {
                BounceRecord last = records[records.Count - 1];
                text.AppendLine();
                text.AppendLine(
                    $"last impact closing speed = {last.ImpactNormalSpeed:F3} m/s vs " +
                    $"bounceThreshold = {Physics.bounceThreshold:F2} m/s");
                text.AppendLine(
                    $"last recorded apex = {(last.HasApex ? last.ApexHeight.ToString("F3") + " m" : "none")}");
            }

            return text.ToString();
        }

        private void OnGUI()
        {
            if (!showHud)
            {
                return;
            }

            PhysicalState state = stateSwitcher != null ? stateSwitcher.CurrentState : runState;
            float lastImpact = records.Count > 0 ? records[records.Count - 1].ImpactNormalSpeed : 0f;
            float lastApex = 0f;
            for (int i = records.Count - 1; i >= 0; i--)
            {
                if (records[i].HasApex)
                {
                    lastApex = records[i].ApexHeight;
                    break;
                }
            }

            GUILayout.BeginArea(new Rect(10f, 10f, 380f, 210f), GUI.skin.box);
            GUILayout.Label($"STATE: {state}");
            GUILayout.Label($"run: {runLabel}{(runActive ? " (recording)" : " (idle)")}");
            GUILayout.Label($"height: {HeightAboveGround():F2} m    speed: {body.linearVelocity.magnitude:F2} m/s");
            GUILayout.Label($"bounces: {records.Count}    last impact: {lastImpact:F2} m/s");
            GUILayout.Label($"last apex: {lastApex:F2} m");
            GUILayout.Label($"bounceThreshold: {Physics.bounceThreshold:F2}");
            GUILayout.Space(6f);
            GUILayout.Label("1/2/3 = drop / wall / slope    R = repeat    Tab = toggle state");
            GUILayout.Label("WASD = move    Space = jump");
            GUILayout.EndArea();
        }
    }
}
