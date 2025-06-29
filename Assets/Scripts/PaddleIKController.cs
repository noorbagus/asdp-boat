using UnityEngine;

public class PaddleIKController : MonoBehaviour
{
    [Header("References")]
    public Transform paddle;
    public Transform character;
    public BoatController boatController;
    
    [Header("Paddle Transform")]
    public Vector3 offsetFromCharacter = new Vector3(0, 1f, 0.5f);
    public Vector3 paddleScale = Vector3.one;
    public Vector3 rotationOffset = Vector3.zero;
    
    [Header("Paddle Animation Settings")]
    [Range(-45f, 5f)]
    public float leftPaddleAngle = -20f;
    [Range(-5f, 45f)]
    public float rightPaddleAngle = 20f;
    [Range(1f, 10f)]
    public float rotationSpeed = 5f;
    [Range(0.5f, 5f)]
    public float swingSpeed = 2f;
    [Range(5f, 100f)]
    public float swingAmplitude = 25f;
    public bool useBalancedSwing = true;
    [Header("Turning Animation")]
    [Range(0.1f, 1.0f)]
    [Tooltip("Controls how much of the swing amplitude is used during turning animations")]
    public float swingDamping = 0.6f; // For ConsecutiveLeft/Right patterns
    [Range(0.5f, 2.0f)]
    [Tooltip("Controls how fast the paddle swings during turning animations")]
    public float turnSwingSpeedMultiplier = 0.8f; // For ConsecutiveLeft/Right patterns
    
    [Header("Transition Settings")]
    public float patternChangeDelay = 0.1f;
    public float patternBlendDuration = 0.8f; // Duration of blend between pattern states
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Pattern Detection")]
    public int minConsecutivePaddles = 2;
    public float speedThreshold = 0.3f;
    
    [Header("Debug")]
    public bool enableDebugLogs = true;
    public bool showGizmos = true;
    
    // Pattern states
    public enum PaddlePattern { None, Alternating, ConsecutiveLeft, ConsecutiveRight }
    private PaddlePattern currentPattern = PaddlePattern.None;
    private PaddlePattern previousPattern = PaddlePattern.None;
    private PaddlePattern targetPattern = PaddlePattern.None;
    
    // Animation state variables
    private Vector3 initialRotation;
    private float currentRotationValue;
    private float targetRotationValue;
    private float previousRotationValue;
    private float transitionProgress = 1f; // 0 to 1, 1 = transition complete
    private float swingTimer;
    private float lastPatternChangeTime;
    
    void Start()
    {
        InitializePaddle();
    }
    
    void Update()
    {
        UpdatePaddleTransform();
        DetectPaddlePattern();
        AnimatePaddle();
        ApplyRotation();
    }
    
    private void InitializePaddle()
    {
        if (paddle == null)
        {
            DebugLog("ERROR: Paddle Transform not assigned!");
            return;
        }
        
        initialRotation = paddle.eulerAngles + rotationOffset;
        currentRotationValue = initialRotation.z;
        targetRotationValue = initialRotation.z;
        previousRotationValue = initialRotation.z;
        paddle.localScale = paddleScale;
        
        DebugLog("PaddleController initialized");
    }
    
    private void UpdatePaddleTransform()
    {
        if (paddle == null || character == null) return;
        
        paddle.position = character.position + character.TransformDirection(offsetFromCharacter);
        paddle.localScale = paddleScale;
    }
    
    private void DetectPaddlePattern()
    {
        if (boatController == null) return;
        
        int leftCount = boatController.GetConsecutiveLeftCount();
        int rightCount = boatController.GetConsecutiveRightCount();
        float currentSpeed = boatController.GetCurrentSpeed();
        
        PaddlePattern newPattern = DeterminePattern(leftCount, rightCount, currentSpeed);
        
        if (newPattern != targetPattern && Time.time - lastPatternChangeTime > patternChangeDelay)
        {
            // Begin transition to new pattern
            previousPattern = currentPattern;
            targetPattern = newPattern;
            lastPatternChangeTime = Time.time;
            
            // Store current rotation as starting point for blend
            previousRotationValue = currentRotationValue;
            
            // Start new transition
            transitionProgress = 0f;
            
            LogPatternChange(newPattern, leftCount, rightCount);
        }
    }
    
    private PaddlePattern DeterminePattern(int leftCount, int rightCount, float currentSpeed)
    {
        // Priority 1: Turning patterns
        if (leftCount >= minConsecutivePaddles)
        {
            return PaddlePattern.ConsecutiveLeft;
        }
        
        if (rightCount >= minConsecutivePaddles)
        {
            return PaddlePattern.ConsecutiveRight;
        }
        
        // Priority 2: Forward movement
        if (currentSpeed > speedThreshold)
        {
            return PaddlePattern.Alternating;
        }
        
        return PaddlePattern.None;
    }
    
    private void AnimatePaddle()
    {
        // Update transition progress
        if (transitionProgress < 1f)
        {
            transitionProgress += Time.deltaTime / patternBlendDuration;
            transitionProgress = Mathf.Clamp01(transitionProgress);
            
            // Update current pattern based on transition progress
            if (transitionProgress >= 1f)
            {
                currentPattern = targetPattern;
            }
        }
        
        // Advance swing timer
        swingTimer += Time.deltaTime * swingSpeed;
        
        // Calculate target rotation for each pattern
        float patternRotation;
        
        switch (targetPattern)
        {
            case PaddlePattern.Alternating:
                patternRotation = CalculateAlternatingRotation();
                break;
                
            case PaddlePattern.ConsecutiveLeft:
                patternRotation = CalculateLeftTurnRotation();
                break;
                
            case PaddlePattern.ConsecutiveRight:
                patternRotation = CalculateRightTurnRotation();
                break;
                
            default:
                patternRotation = initialRotation.z;
                break;
        }
        
        // Apply eased transition between rotation values
        if (transitionProgress < 1f)
        {
            float t = transitionCurve.Evaluate(transitionProgress);
            targetRotationValue = Mathf.LerpAngle(previousRotationValue, patternRotation, t);
            DebugLog($"Transitioning: {transitionProgress:F2}, Blend: {t:F2}, From: {previousRotationValue:F1}° To: {patternRotation:F1}°");
        }
        else
        {
            targetRotationValue = patternRotation;
        }
    }
    
    private float CalculateAlternatingRotation()
    {
        if (useBalancedSwing)
        {
            // Balanced left-right swing like React demo
            float swing = Mathf.Sin(swingTimer) * swingAmplitude;
            return initialRotation.z + swing;
        }
        else
        {
            // Upward-only swing (original style)
            float swing = Mathf.Abs(Mathf.Sin(swingTimer)) * swingAmplitude;
            return initialRotation.z - swing;
        }
    }
    
    private float CalculateLeftTurnRotation()
    {
        // Smooth oscillation for left turn - using same math as React component
        float swing = Mathf.Abs(Mathf.Sin(swingTimer * turnSwingSpeedMultiplier)) * swingAmplitude * swingDamping;
        return initialRotation.z + leftPaddleAngle - swing;
    }
    
    private float CalculateRightTurnRotation()
    {
        // Smooth oscillation for right turn - using same math as React component
        float swing = Mathf.Abs(Mathf.Sin(swingTimer * turnSwingSpeedMultiplier)) * swingAmplitude * swingDamping;
        return initialRotation.z + rightPaddleAngle + swing;
    }
    
    private void ApplyRotation()
    {
        if (paddle == null || character == null) return;
        
        // Smooth rotation toward target value
        currentRotationValue = Mathf.LerpAngle(currentRotationValue, targetRotationValue, rotationSpeed * Time.deltaTime);
        
        // Apply rotation to paddle
        paddle.eulerAngles = new Vector3(
            initialRotation.x,
            character.eulerAngles.y + rotationOffset.y,
            currentRotationValue
        );
    }
    
    private void LogPatternChange(PaddlePattern newPattern, int leftCount, int rightCount)
    {
        if (!enableDebugLogs) return;
        
        Debug.Log($"[PaddleController] Pattern: {GetPatternName(previousPattern)} → {GetPatternName(newPattern)}, " +
                  $"Counts - L:{leftCount}, R:{rightCount}, Speed:{boatController?.GetCurrentSpeed():F2}");
    }
    
    private string GetPatternName(PaddlePattern pattern)
    {
        switch (pattern)
        {
            case PaddlePattern.Alternating: return useBalancedSwing ? "Balanced Forward" : "Upward Forward";
            case PaddlePattern.ConsecutiveLeft: return "Turn Right";
            case PaddlePattern.ConsecutiveRight: return "Turn Left";
            case PaddlePattern.None: return "Neutral";
            default: return "Unknown";
        }
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PaddleController] {message}");
        }
    }
    
    // Visual debugging
    private void OnDrawGizmos()
    {
        if (!showGizmos || character == null) return;
        
        // Draw paddle position
        Gizmos.color = Color.yellow;
        Vector3 paddlePos = character.position + character.TransformDirection(offsetFromCharacter);
        Gizmos.DrawWireSphere(paddlePos, 0.1f);
        
        if (paddle != null && Application.isPlaying)
        {
            // Main pattern indicator
            Gizmos.color = GetPatternColor(currentPattern);
            Gizmos.DrawWireCube(paddle.position + Vector3.up * 0.5f, Vector3.one * 0.2f);
            
            // Draw transition indicator if in progress
            if (transitionProgress < 1f)
            {
                Gizmos.color = Color.Lerp(GetPatternColor(previousPattern), GetPatternColor(targetPattern), transitionProgress);
                Gizmos.DrawWireSphere(paddle.position + Vector3.up * 0.5f, 0.15f);
            }
            
            // Show swing amplitude visualization
            if (currentPattern == PaddlePattern.Alternating && useBalancedSwing)
            {
                // Left limit
                Vector3 leftDir = Quaternion.Euler(0, character.eulerAngles.y, -swingAmplitude) * Vector3.forward;
                Gizmos.color = new Color(1, 0, 0, 0.3f); // Red with transparency
                Gizmos.DrawRay(paddle.position, leftDir * 0.5f);
                
                // Right limit
                Vector3 rightDir = Quaternion.Euler(0, character.eulerAngles.y, swingAmplitude) * Vector3.forward;
                Gizmos.color = new Color(0, 0, 1, 0.3f); // Blue with transparency
                Gizmos.DrawRay(paddle.position, rightDir * 0.5f);
            }
        }
    }
    
    private Color GetPatternColor(PaddlePattern pattern)
    {
        switch (pattern)
        {
            case PaddlePattern.Alternating: return useBalancedSwing ? Color.green : Color.cyan;
            case PaddlePattern.ConsecutiveLeft: return Color.red;
            case PaddlePattern.ConsecutiveRight: return Color.blue;
            default: return Color.white;
        }
    }
    
    // Public methods for external access
    public PaddlePattern GetCurrentPattern() => currentPattern;
    public float GetTransitionProgress() => transitionProgress;
    public bool IsAnimating() => currentPattern != PaddlePattern.None;
    
    public void SetBalancedSwing(bool enabled)
    {
        useBalancedSwing = enabled;
        DebugLog($"Balanced swing: {(enabled ? "enabled" : "disabled")}");
    }
    
    // For testing
    public void ForcePattern(int patternIndex)
    {
        if (patternIndex >= 0 && patternIndex < System.Enum.GetValues(typeof(PaddlePattern)).Length)
        {
            previousPattern = currentPattern;
            targetPattern = (PaddlePattern)patternIndex;
            transitionProgress = 0f;
            previousRotationValue = currentRotationValue;
            DebugLog($"Force pattern to: {GetPatternName(targetPattern)}");
        }
    }
}