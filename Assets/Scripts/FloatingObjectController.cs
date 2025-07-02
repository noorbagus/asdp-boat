using UnityEngine;

public class FloatingObjectController : MonoBehaviour
{
    public enum FloatingAnimationType
    {
        PingPong,   // Smooth up/down with holds
        Loop,       // Reset to bottom instantly
        Sine,       // Continuous sine wave
        Random      // Random Y positions
    }

    [Header("Position Settings")]
    [SerializeField] private float startPositionY = 0f;

    [Header("Speed Settings - Range for Randomization")]
    [SerializeField] private Vector2 riseSpeedRange = new Vector2(0.5f, 2f);
    [SerializeField] private Vector2 fallSpeedRange = new Vector2(0.5f, 2f);

    [Header("Height Settings - Range for Randomization")]
    [SerializeField] private Vector2 floatHeightRange = new Vector2(1f, 3f);

    [Header("Hold Time Settings - Range for Randomization")]
    [SerializeField] private Vector2 holdTimeTopRange = new Vector2(0.5f, 2f);
    [SerializeField] private Vector2 holdTimeBottomRange = new Vector2(0.5f, 2f);

    [Header("Animation")]
    [SerializeField] private FloatingAnimationType animationType = FloatingAnimationType.PingPong;
    [SerializeField] private float globalSpeedMultiplier = 1f;

    [Header("Start Time Settings - Range for Randomization")]
    [SerializeField] private Vector2 startDelayRange = new Vector2(0f, 3f);
    [SerializeField] private float randomChangeInterval = 2f;
    [SerializeField] private float randomSmoothness = 2f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // Private variables (randomized at start)
    private float riseSpeed;
    private float fallSpeed;
    private float floatHeight;
    private float holdTimeTop;
    private float holdTimeBottom;

    private enum FloatingState { Rising, HoldingTop, Falling, HoldingBottom }

    private FloatingState currentState = FloatingState.Rising;
    private float currentTime = 0f;
    private float holdTimer = 0f;
    private Vector3 initialPosition;
    private float targetY;
    private float randomTimer = 0f;
    private float randomTargetY;

    private void Start()
    {
        initialPosition = transform.position;
        startPositionY = initialPosition.y;

        // Randomize values from ranges
        floatHeight = Random.Range(floatHeightRange.x, floatHeightRange.y);
        riseSpeed = Random.Range(riseSpeedRange.x, riseSpeedRange.y);
        fallSpeed = Random.Range(fallSpeedRange.x, fallSpeedRange.y);
        holdTimeTop = Random.Range(holdTimeTopRange.x, holdTimeTopRange.y);
        holdTimeBottom = Random.Range(holdTimeBottomRange.x, holdTimeBottomRange.y);

        // Random start delay
        float startDelay = Random.Range(startDelayRange.x, startDelayRange.y);
        currentTime = -startDelay; // Negative time = delay

        targetY = startPositionY;
        randomTargetY = startPositionY;

        if (showDebugInfo)
            Debug.Log($"[FloatingObject] {gameObject.name} - Height:{floatHeight:F2}, Rise:{riseSpeed:F2}, Fall:{fallSpeed:F2}, Delay:{startDelay:F2}s");
    }

    private void Update()
    {
        currentTime += Time.deltaTime * globalSpeedMultiplier;

        float newY = CalculateYPosition();

        Vector3 newPosition = transform.position;
        newPosition.y = newY;
        transform.position = newPosition;
    }

    private void LateUpdate()
    {
        // Override Animation Controller by reapplying Y position
        float newY = CalculateYPosition();
        Vector3 pos = transform.position;
        pos.y = newY;
        transform.position = pos;
    }

    private float CalculateYPosition()
    {
        // Don't move during delay period
        if (currentTime < 0) return startPositionY;

        switch (animationType)
        {
            case FloatingAnimationType.PingPong:
                return CalculatePingPongMovement();

            case FloatingAnimationType.Loop:
                return CalculateLoopMovement();

            case FloatingAnimationType.Sine:
                return CalculateSineMovement();

            case FloatingAnimationType.Random:
                return CalculateRandomMovement();

            default:
                return startPositionY;
        }
    }

    private float CalculatePingPongMovement()
    {
        switch (currentState)
        {
            case FloatingState.Rising:
                targetY += riseSpeed * Time.deltaTime * globalSpeedMultiplier;
                if (targetY >= startPositionY + floatHeight)
                {
                    targetY = startPositionY + floatHeight;
                    currentState = FloatingState.HoldingTop;
                    holdTimer = 0f;
                }
                break;

            case FloatingState.HoldingTop:
                holdTimer += Time.deltaTime;
                if (holdTimer >= holdTimeTop)
                {
                    currentState = FloatingState.Falling;
                }
                break;

            case FloatingState.Falling:
                targetY -= fallSpeed * Time.deltaTime * globalSpeedMultiplier;
                if (targetY <= startPositionY)
                {
                    targetY = startPositionY;
                    currentState = FloatingState.HoldingBottom;
                    holdTimer = 0f;
                }
                break;

            case FloatingState.HoldingBottom:
                holdTimer += Time.deltaTime;
                if (holdTimer >= holdTimeBottom)
                {
                    currentState = FloatingState.Rising;
                }
                break;
        }

        return targetY;
    }

    private float CalculateLoopMovement()
    {
        float cycle = currentTime * riseSpeed;
        float progress = cycle % 1f;

        if (progress > 0.8f) // Quick reset
        {
            return Mathf.Lerp(startPositionY + floatHeight, startPositionY, (progress - 0.8f) / 0.2f);
        }
        else
        {
            return Mathf.Lerp(startPositionY, startPositionY + floatHeight, progress / 0.8f);
        }
    }

    private float CalculateSineMovement()
    {
        float sineValue = Mathf.Sin(currentTime * riseSpeed) * 0.5f + 0.5f; // 0-1 range
        return startPositionY + (sineValue * floatHeight);
    }

    private float CalculateRandomMovement()
    {
        randomTimer += Time.deltaTime;

        if (randomTimer >= randomChangeInterval)
        {
            randomTargetY = Random.Range(startPositionY, startPositionY + floatHeight);
            randomTimer = 0f;

            if (showDebugInfo)
                Debug.Log($"[FloatingObject] New random target: {randomTargetY:F2}");
        }

        return Mathf.Lerp(targetY, randomTargetY, Time.deltaTime * randomSmoothness);
    }

    // Public control methods
    public void SetAnimationType(FloatingAnimationType type)
    {
        animationType = type;
        ResetAnimation();
    }

    public void SetFloatHeight(float height)
    {
        floatHeight = height;
    }

    public void SetSpeeds(float rise, float fall)
    {
        riseSpeed = rise;
        fallSpeed = fall;
    }

    public void SetHoldTimes(float top, float bottom)
    {
        holdTimeTop = top;
        holdTimeBottom = bottom;
    }

    public void ResetAnimation()
    {
        currentState = FloatingState.Rising;
        currentTime = 0f;
        holdTimer = 0f;
        targetY = startPositionY;
        randomTimer = 0f;
    }

    public void SetGlobalSpeed(float multiplier)
    {
        globalSpeedMultiplier = multiplier;
    }

    // Manual control
    [ContextMenu("Reset to Start Position")]
    public void ResetToStart()
    {
        transform.position = new Vector3(transform.position.x, startPositionY, transform.position.z);
        ResetAnimation();
    }

    [ContextMenu("Jump to Top")]
    public void JumpToTop()
    {
        transform.position = new Vector3(transform.position.x, startPositionY + floatHeight, transform.position.z);
        currentState = FloatingState.HoldingTop;
        holdTimer = 0f;
    }

    // Debug info
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            Vector3 pos = transform.position;

            // Draw start position
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(new Vector3(pos.x, startPositionY, pos.z), 0.2f);

            // Draw float range
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(new Vector3(pos.x, startPositionY + floatHeight, pos.z), 0.2f);

            // Draw connection line
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(
                new Vector3(pos.x, startPositionY, pos.z),
                new Vector3(pos.x, startPositionY + floatHeight, pos.z)
            );
        }
    }
}