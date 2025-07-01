using UnityEngine;

/// <summary>
/// Makes objects float up and down with smooth sine/cosine interpolation
/// Includes randomizable timing parameters for organic movement
/// </summary>
public class SmoothFloatMovement : MonoBehaviour
{
    [System.Serializable]
    public struct TimeRange
    {
        public float min;
        public float max;

        public TimeRange(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        public float GetRandomValue()
        {
            return Random.Range(min, max);
        }
    }

    public enum MovementState
    {
        Waiting,
        Rising,
        Staying,
        Falling
    }

    [Header("Movement Parameters")]
    [Tooltip("Distance the object will move up and down")]
    public float movementAmplitude = 0.5f;
    [Tooltip("When true, moves relative to the object's local orientation")]
    public bool useLocalSpace = true;

    [Header("Timing Parameters")]
    [Tooltip("Time before starting the movement cycle")]
    public TimeRange startTimeRange = new TimeRange(0f, 2f);
    [Tooltip("Time taken to rise up")]
    public TimeRange riseDurationRange = new TimeRange(1f, 2f);
    [Tooltip("Time to stay at the top position")]
    public TimeRange stayDurationRange = new TimeRange(0.5f, 1.5f);
    [Tooltip("Time taken to fall down")]
    public TimeRange fallDurationRange = new TimeRange(1f, 2f);

    [Header("Movement Curve")]
    [Tooltip("Controls the movement easing (default sine curve is 0.5)")]
    [Range(0.1f, 5f)]
    public float curveExponent = 0.5f;
    [Tooltip("Flip the movement direction")]
    public bool invertDirection = false;

    [Header("Debug")]
    [SerializeField] private MovementState currentState = MovementState.Waiting;
    [SerializeField] private float stateTimeRemaining = 0f;

    // Private tracking variables
    private Vector3 startPosition;
    private Vector3 targetUpPosition;
    private float moveProgress = 0f;
    private float waitTimer = 0f;
    private float currentRiseDuration;
    private float currentStayDuration;
    private float currentFallDuration;

    private void Start()
    {
        // Store initial position
        startPosition = transform.position;
        
        // Calculate up target
        Vector3 upDirection = useLocalSpace ? transform.up : Vector3.up;
        targetUpPosition = startPosition + (upDirection * movementAmplitude);

        // Initialize with random parameters
        currentRiseDuration = riseDurationRange.GetRandomValue();
        currentStayDuration = stayDurationRange.GetRandomValue();
        currentFallDuration = fallDurationRange.GetRandomValue();
        
        // Set random start delay
        waitTimer = startTimeRange.GetRandomValue();
        stateTimeRemaining = waitTimer;
        
        if (invertDirection)
        {
            // Start at the top position if inverted
            transform.position = targetUpPosition;
        }
    }

    private void Update()
    {
        // Count down time in current state
        stateTimeRemaining -= Time.deltaTime;

        switch (currentState)
        {
            case MovementState.Waiting:
                // Wait until timer expires
                if (stateTimeRemaining <= 0f)
                {
                    // Start rising
                    currentState = invertDirection ? MovementState.Falling : MovementState.Rising;
                    stateTimeRemaining = invertDirection ? currentFallDuration : currentRiseDuration;
                    moveProgress = 0f;
                }
                break;

            case MovementState.Rising:
                // Move upward with smooth interpolation
                moveProgress = Mathf.Clamp01(1f - (stateTimeRemaining / currentRiseDuration));
                UpdatePosition(moveProgress);
                
                if (stateTimeRemaining <= 0f)
                {
                    // Start staying at top
                    currentState = MovementState.Staying;
                    stateTimeRemaining = currentStayDuration;
                }
                break;

            case MovementState.Staying:
                // Stay at the top position
                if (stateTimeRemaining <= 0f)
                {
                    // Start falling
                    currentState = MovementState.Falling;
                    stateTimeRemaining = currentFallDuration;
                    moveProgress = 0f;
                }
                break;

            case MovementState.Falling:
                // Move downward with smooth interpolation
                moveProgress = Mathf.Clamp01(1f - (stateTimeRemaining / currentFallDuration));
                UpdatePosition(1f - moveProgress);
                
                if (stateTimeRemaining <= 0f)
                {
                    // Get new random durations for next cycle
                    currentRiseDuration = riseDurationRange.GetRandomValue();
                    currentStayDuration = stayDurationRange.GetRandomValue();
                    currentFallDuration = fallDurationRange.GetRandomValue();
                    
                    // Start rising again
                    currentState = MovementState.Rising;
                    stateTimeRemaining = currentRiseDuration;
                    moveProgress = 0f;
                }
                break;
        }
    }

    private void UpdatePosition(float t)
    {
        // Apply sine/cosine curve to make movement smoother
        float curvedT = SmoothStep(t, curveExponent);
        
        // Interpolate between start and top positions
        Vector3 newPosition = Vector3.Lerp(startPosition, targetUpPosition, curvedT);
        transform.position = newPosition;
    }

    // Custom smooth step with configurable exponent
    private float SmoothStep(float t, float exponent)
    {
        // Use sine function for smooth easing (shifted to 0-1 range)
        return 0.5f + 0.5f * Mathf.Sin(Mathf.PI * (t - 0.5f) * exponent);
    }

    // Recalculate positions when object is moved in editor
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        
        startPosition = transform.position;
        Vector3 upDirection = useLocalSpace ? transform.up : Vector3.up;
        targetUpPosition = startPosition + (upDirection * movementAmplitude);
    }

    // Visualize movement path in editor
    private void OnDrawGizmosSelected()
    {
        Vector3 start = startPosition;
        if (!Application.isPlaying)
        {
            start = transform.position;
        }
        
        Vector3 upDirection = useLocalSpace ? transform.up : Vector3.up;
        Vector3 end = start + (upDirection * movementAmplitude);
        
        Gizmos.color = Color.green;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(start, 0.1f);
        Gizmos.DrawWireSphere(end, 0.1f);
    }

    // Public methods for external control
    public void ResetMovement()
    {
        transform.position = startPosition;
        currentState = MovementState.Waiting;
        stateTimeRemaining = waitTimer;
    }

    public void SetAmplitude(float newAmplitude)
    {
        movementAmplitude = newAmplitude;
        Vector3 upDirection = useLocalSpace ? transform.up : Vector3.up;
        targetUpPosition = startPosition + (upDirection * movementAmplitude);
    }
}