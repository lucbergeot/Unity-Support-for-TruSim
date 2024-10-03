using Convai.Scripts.Runtime.Features;
using Convai.Scripts.Runtime.LoggerSystem;
using UnityEngine;

namespace Convai.Scripts.Runtime.Core
{
    /// <summary>
    ///     This class provides head tracking functionalities for an object (like a character) with an Animator.
    ///     It requires the Animator component to be attached to the same GameObject.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    [AddComponentMenu("Convai/Character Head & Eye Tracking")]
    public class ConvaiHeadTracking : MonoBehaviour
    {
        private const float POSITION_UPDATE_DELAY = 0.5f; // Reduced for smoother updates

        [field: Header("Tracking Properties")]
        [Tooltip("The object that the head should track.")]
        [field: SerializeField]
        public Transform TargetObject { get; set; }

        [Tooltip("Possible targets the character can look at.")]
        [SerializeField]
        private Transform[] possibleTargets;

        [Tooltip("Default target if no other target is selected.")]
        [SerializeField]
        private Transform defaultTarget;

        [Range(0.0f, 100.0f)] [Tooltip("The maximum distance at which the head must still track target.")] [SerializeField]
        private float trackingDistance = 10f;

        [Tooltip("Speed at which character turns towards the target.")] [Range(1f, 10f)] [SerializeField]
        private float turnSpeed = 10f;

        [Header("Look At Weights")]
        [Range(0f, 1f)]
        [Tooltip(
            "Controls the amount of rotation applied to the body to achieve the 'Look At' target. The closer to 1, the more the body will rotate to follow the target.")]
        [SerializeField]
        private float bodyLookAtWeight = 1f;

        [Range(0f, 1f)]
        [Tooltip(
            "Controls the amount of rotation applied to the head to achieve the 'Look At' target. The closer to 1, the more the head will rotate to follow the target.")]
        [SerializeField]
        private float headLookAtWeight = 1f;

        [Range(0f, 1f)]
        [Tooltip(
            "Controls the amount of rotation applied to the eyes to achieve the 'Look At' target. The closer to 1, the more the eyes will rotate to follow the target.")]
        [SerializeField]
        private float eyesLookAtWeight = 1f;

        [Space(10)]
        [Tooltip(
            "Set this to true if you want the character to look away randomly, false to always look at the target")]
        [SerializeField]
        private bool lookAway = true;

        private Animator _animator;
        private float _appliedBodyLookAtWeight;
        private ConvaiActionsHandler _convaiActionsHandler;
        private float _currentLookAtWeight;
        private float _desiredLookAtWeight = 1f;
        private Transform _headPivot;
        private bool _isActionRunning;
        private float _nextBlinkTime;
        private bool _isBlinking;

        private void Start()
        {
            InitializeComponents();
            InitializeHeadPivot();
            InvokeRepeating(nameof(UpdateTarget), 0, POSITION_UPDATE_DELAY);
        }

        private void OnDisable()
        {
            if (_convaiActionsHandler != null)
                _convaiActionsHandler.UnregisterForActionEvents(ConvaiActionsHandler_OnActionStarted, ConvaiActionsHandler_OnActionEnded);
        }

        /// <summary>
        ///     Unity's built-in method called during the IK pass.
        /// </summary>
        public void OnAnimatorIK(int layerIndex)
        {
            PerformHeadTracking();
        }

        private void InitializeComponents()
        {
            if (!_animator) _animator = GetComponent<Animator>();
            InitializeTargetObject();

            if (TryGetComponent(out _convaiActionsHandler))
                _convaiActionsHandler.RegisterForActionEvents(ConvaiActionsHandler_OnActionStarted, ConvaiActionsHandler_OnActionEnded);
        }

        private void ConvaiActionsHandler_OnActionStarted(string action, GameObject target)
        {
            SetActionRunning(true);
        }

        private void ConvaiActionsHandler_OnActionEnded(string action, GameObject target)
        {
            SetActionRunning(false);
        }

        private void InitializeHeadPivot()
        {
            // Check if the pivot already exists
            if (_headPivot) return;

            // Create a new GameObject for the pivot
            _headPivot = new GameObject("HeadPivot").transform;

            // Set the new GameObject as a child of this character object
            _headPivot.transform.parent = transform;

            // Position the pivot appropriately
            _headPivot.localPosition = new Vector3(0, 1.6f, 0);
        }

        private void RotateCharacterTowardsTarget()
        {
            Vector3 toTarget = TargetObject.position - transform.position;
            float distance = toTarget.magnitude;

            // Calculate the angle difference between the character's forward direction and the direction towards the target.
            float angleDifference = Vector3.Angle(transform.forward, toTarget);

            // Adjust turn speed based on distance to target.
            float adjustedTurnSpeed = turnSpeed * 4 * (1f / distance);

            // If the angle difference exceeds the limit, we turn the character smoothly towards the target.
            if (Mathf.Abs(angleDifference) > 0.65f)
            {
                Vector3 targetDirection = toTarget.normalized;

                // Zero out the y-component (up-down direction) to only rotate on the horizontal plane.
                targetDirection.y = 0;

                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation,
                    adjustedTurnSpeed * Time.deltaTime);

                // Ensure that the character doesn't tilt on the X and Z axis.
                transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);
            }
        }

        private void InitializeTargetObject()
        {
            if (TargetObject != null) return;

            ConvaiLogger.Warn("No target object set for head tracking. Setting default target as main camera",
                ConvaiLogger.LogCategory.Character);
            if (Camera.main != null) TargetObject = Camera.main.transform;
        }

        /// <summary>
        ///     Updates the target weight for the look-at.
        /// </summary>
        private void UpdateTarget()
        {
            if (TargetObject != null && TargetObject.name == "twitch camera")
            {
                _desiredLookAtWeight = 1f; // Immediately look at Twitch camera when active
            }
            else
            {
                // Determine the chance to switch targets based on whether the NPC is talking
                float switchChance = _isActionRunning ? 0.9f : 0.4f; // Higher chance when talking

                if (lookAway && possibleTargets.Length > 0 && Random.value < switchChance)
                {
                    // Randomly pick an active target from possibleTargets
                    Transform newTarget = possibleTargets[Random.Range(0, possibleTargets.Length)];

                    // Check if the new target is active
                    if (newTarget.gameObject.activeInHierarchy)
                    {
                        TargetObject = newTarget;
                    }
                    else
                    {
                        // If not active, revert to default target
                        TargetObject = defaultTarget;
                    }
                }
                else
                {
                    // Revert to default target
                    TargetObject = defaultTarget;
                }

                // Increase desired look-at weight when talking
                _desiredLookAtWeight = _isActionRunning ? Random.Range(0.8f, 1.0f) : Random.Range(0.4f, 0.8f);
            }

            // Schedule the next blink
            if (Time.time >= _nextBlinkTime)
            {
                StartCoroutine(Blink());
                _nextBlinkTime = Time.time + Random.Range(3f, 7f); // Randomize blink intervals
            }
        }

        /// <summary>
        ///     Performs the head tracking towards the target object.
        /// </summary>
        private void PerformHeadTracking()
        {
            float distance = Vector3.Distance(transform.position, TargetObject.position);
            DrawRayToTarget();

            // only perform head tracking if within threshold distance
            if (!(distance < trackingDistance))
            {
                _desiredLookAtWeight = 0;
                if (_currentLookAtWeight > 0)
                    SetCurrentLookAtWeight();
            }

            SetCurrentLookAtWeight();
            _headPivot.transform.LookAt(TargetObject); // orient the pivot towards the target object

            // limit the head rotation
            float headRotation = _headPivot.localRotation.y;
            if (Mathf.Abs(headRotation) > 0.70f)
            {
                // clamp rotation if more than 80 degrees
                headRotation = Mathf.Sign(headRotation) * 0.70f;
                Quaternion localRotation = _headPivot.localRotation;
                localRotation.y = headRotation;
                _headPivot.localRotation = localRotation;
            }

            // adjust body rotation weight based on how much the head is rotated
            float targetBodyLookAtWeight = Mathf.Abs(_headPivot.localRotation.y) > 0.45f
                ? bodyLookAtWeight / 3f
                : 0f;

            // smooth transition between current and target body rotation weight
            _appliedBodyLookAtWeight = Mathf.Lerp(_appliedBodyLookAtWeight, targetBodyLookAtWeight, Time.deltaTime);

            // Apply rotation weights to the Animator
            RotateCharacterTowardsTarget();
            AdjustAnimatorLookAt();
        }

        /// <summary>
        ///     Method to set the current look at weight based on the desired look at weight.
        /// </summary>
        private void SetCurrentLookAtWeight()
        {
            float angleDifference = _headPivot.localRotation.y;

            // Lerp the currentLookAtWeight towards the desiredLookAtWeight or towards 0 if above a certain threshold.
            _currentLookAtWeight = Mathf.Abs(angleDifference) < 0.55f
                ? Mathf.Lerp(_currentLookAtWeight, _desiredLookAtWeight, Time.deltaTime * 2f)
                : Mathf.Lerp(_currentLookAtWeight, 0, Time.deltaTime * 2f);
        }

        /// <summary>
        ///     Method to apply rotation weights to the Animator
        /// </summary>
        private void AdjustAnimatorLookAt()
        {
            // Check if Animator or TargetObject are null
            if (!_animator || TargetObject == null)
            {
                // If either is null, set the look-at weight to 0 and return
                _animator.SetLookAtWeight(0);
                return;
            }

            // Apply blinking by adjusting eyes weight
            float eyesWeight = _isBlinking ? 0f : Mathf.Clamp(eyesLookAtWeight, 0, 1);

            // Set the look-at weights in the Animator.
            _animator.SetLookAtWeight(
                Mathf.Clamp(_currentLookAtWeight, 0, 1),
                Mathf.Clamp(_appliedBodyLookAtWeight, 0, .5f),
                Mathf.Clamp(headLookAtWeight / 1.25f, 0, .8f),
                eyesWeight
            );

            // Set the look-at position for the Animator
            _animator.SetLookAtPosition(TargetObject.position);
        }

        /// <summary>
        ///     DebugLog utility to visualize the tracking mechanism
        /// </summary>
        private void DrawRayToTarget()
        {
            Vector3 pos = transform.position;
            Debug.DrawRay(pos,
                (TargetObject.position - pos).normalized * trackingDistance / 2, Color.red);
        }

        public void ForceImmediateLookAt()
        {
            // Immediately set the desired look-at weight to maximum
            _currentLookAtWeight = 1f;
            _desiredLookAtWeight = 1f;

            // Immediately adjust the Animator weights
            AdjustAnimatorLookAt();
        }

        public void SetActionRunning(bool newValue)
        {
            _isActionRunning = newValue;
        }

        private System.Collections.IEnumerator Blink()
        {
            _isBlinking = true;
            yield return new WaitForSeconds(0.1f); // Blink duration
            _isBlinking = false;
        }
    }
}