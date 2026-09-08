using System;
using UnityEngine;

namespace Experiment002
{
    /// <summary>
    /// A trigger volume that records the first time the subject reaches it during an attempt.
    /// <para>
    /// This is the experiment's only success criterion, and it is deliberately binary: reached or
    /// not reached, per attempt. No score, no timer, no progression. "Did the player get there"
    /// is the question; anything more would start measuring a game instead of a mechanic.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class BounceTarget : MonoBehaviour
    {
        [SerializeField] private string targetName = "Target";

        [Tooltip("Height above the chamber floor, recorded in the log so an attempt can be read " +
                 "without opening the scene.")]
        [SerializeField] private float heightAboveFloor;

        public string TargetName => targetName;
        public float HeightAboveFloor => heightAboveFloor;
        public bool ReachedThisAttempt { get; private set; }

        public event Action<BounceTarget> Reached;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        public void ResetForNewAttempt() => ReachedThisAttempt = false;

        private void OnTriggerEnter(Collider other)
        {
            if (ReachedThisAttempt)
            {
                return;
            }

            // Identified by component rather than by tag, so the chamber needs no tag setup and
            // cannot be tripped by a stray prop rolling through.
            Rigidbody visitor = other.attachedRigidbody;
            if (visitor == null || visitor.GetComponent<BounceTraversalController>() == null)
            {
                return;
            }

            ReachedThisAttempt = true;
            Reached?.Invoke(this);
        }
    }
}
