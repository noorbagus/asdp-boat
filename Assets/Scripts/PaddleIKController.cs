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
    public float swingDamping = 0.6f;
    [Range(0.5f, 2.0f)]
    public float turnSwingSpeedMultiplier = 0.8f;
    
    [Header("Transition Settings")]
    public float patternChangeDelay = 0.1f;
    public float patternBlendDuration = 0.8f;
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Pattern Detection")]
    public int minConsecutivePaddles = 2;
    public float speedThreshold = 0.3f;
    
    [Header("ESP32 Integration - UPDATED")]
    public bool useRawAngle = false;
    public float rawAngle = 0f;
    public float rawAngleMultiplier = 1.0f;
    [Range(0f, 1f)]
    public float rawAngleSmoothing = 0.8f; // NEW: Smoothing for ESP32 input
    public bool overrideWithRawAngle = true; // NEW: ESP32 override pattern animations
    
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
    private float transitionProgress = 1f;
    private float swingTimer;
    private float lastPatternChangeTime;
    
    // NEW: ESP32 smoothing
    private float smoothedRawAngle = 0f;
    private bool isESP32Controlled = false;
    
    void Start()
    {
        InitializePaddle();
    }
    
    void Update()
    {
        UpdatePaddleTransform();
        
        // Check if controlled by ESP32
        CheckESP32Control();
        
        if (useRawAngle && isESP32Controlled)
        {
            // ESP32 mode: Use smoothed raw angle
            UpdateRawAngleSmoothing();
            targetRotationValue = initialRotation.z + smoothedRawAngle;
        }
        else
        {
            // Normal mode: Pattern-based animation
            DetectPaddlePattern();
            AnimatePaddle();
        }
        
        ApplyRotation();
    }
    
    private void CheckESP32Control()
    {
        // Auto-detect if ESP32 is controlling
        ESP32GyroController esp32 = FindObjectOfType<ESP32GyroController>();
        isESP32Controlled = (esp32 != null && esp32.IsConnected());
        
        // Override pattern animations when ESP32 is active
        if (isESP32Controlled && overrideWithRawAngle)
        {
            useRawAngle = true;
        }
    }
    
    private void UpdateRawAngleSmoothing()
    {
        // Smooth the raw angle input for better visual result
        smoothedRawAngle = Mathf.Lerp(smoothedRawAngle, rawAngle * rawAngleMultiplier, 
                                     (1f - rawAngleSmoothing) * Time.deltaTime * 10f);
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
        if (boatController == null || isESP32Controlled) return;
        
        int leftCount = boatController.GetConsecutiveLeftCount();
        int rightCount = boatController.GetConsecutiveRightCount();
        float currentSpeed = boatController.GetCurrentSpeed();
        
        PaddlePattern newPattern = DeterminePattern(leftCount, rightCount, currentSpeed);
        
        if (newPattern != targetPattern && Time.time - lastPatternChangeTime > patternChangeDelay)
        {
            previousPattern = currentPattern;
            targetPattern = newPattern;
            lastPatternChangeTime = Time.time;
            previousRotationValue = currentRotationValue;
            transitionProgress = 0f;
            
            LogPatternChange(newPattern, leftCount, rightCount);
        }
    }
    
    private PaddlePattern DeterminePattern(int leftCount, int rightCount, float currentSpeed)
    {
        if (leftCount >= minConsecutivePaddles)
        {
            return PaddlePattern.ConsecutiveLeft;
        }
        
        if (rightCount >= minConsecutivePaddles)
        {
            return PaddlePattern.ConsecutiveRight;
        }
        
        if (currentSpeed > speedThreshold)
        {
            return PaddlePattern.Alternating;
        }
        
        return PaddlePattern.None;
    }
    
    private void AnimatePaddle()
    {
        if (transitionProgress < 1f)
        {
            transitionProgress += Time.deltaTime / patternBlendDuration;
            transitionProgress = Mathf.Clamp01(transitionProgress);
            
            if (transitionProgress >= 1f)
            {
                currentPattern = targetPattern;
            }
        }
        
        swingTimer += Time.deltaTime * swingSpeed;
        
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
        
        if (transitionProgress < 1f)
        {
            float t = transitionCurve.Evaluate(transitionProgress);
            targetRotationValue = Mathf.LerpAngle(previousRotationValue, patternRotation, t);
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
            float swing = Mathf.Sin(swingTimer) * swingAmplitude;
            return initialRotation.z + swing;
        }
        else
        {
            float swing = Mathf.Abs(Mathf.Sin(swingTimer)) * swingAmplitude;
            return initialRotation.z - swing;
        }
    }
    
    private float CalculateLeftTurnRotation()
    {
        float swing = Mathf.Abs(Mathf.Sin(swingTimer * turnSwingSpeedMultiplier)) * swingAmplitude * swingDamping;
        return initialRotation.z + leftPaddleAngle - swing;
    }
    
    private float CalculateRightTurnRotation()
    {
        float swing = Mathf.Abs(Mathf.Sin(swingTimer * turnSwingSpeedMultiplier)) * swingAmplitude * swingDamping;
        return initialRotation.z + rightPaddleAngle + swing;
    }
    
    private void ApplyRotation()
    {
        if (paddle == null || character == null) return;
        
        currentRotationValue = Mathf.LerpAngle(currentRotationValue, targetRotationValue, rotationSpeed * Time.deltaTime);
        
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
        
        Gizmos.color = Color.yellow;
        Vector3 paddlePos = character.position + character.TransformDirection(offsetFromCharacter);
        Gizmos.DrawWireSphere(paddlePos, 0.1f);
        
        if (paddle != null && Application.isPlaying)
        {
            // ESP32 vs Pattern mode indicator
            Gizmos.color = isESP32Controlled ? Color.magenta : GetPatternColor(currentPattern);
            Gizmos.DrawWireCube(paddle.position + Vector3.up * 0.5f, Vector3.one * 0.2f);
            
            if (transitionProgress < 1f && !isESP32Controlled)
            {
                Gizmos.color = Color.Lerp(GetPatternColor(previousPattern), GetPatternColor(targetPattern), transitionProgress);
                Gizmos.DrawWireSphere(paddle.position + Vector3.up * 0.5f, 0.15f);
            }
            
            // Show ESP32 angle visualization
            if (useRawAngle && isESP32Controlled)
            {
                Gizmos.color = Color.cyan;
                Vector3 rawDir = Quaternion.Euler(0, character.eulerAngles.y, smoothedRawAngle) * Vector3.forward;
                Gizmos.DrawRay(paddle.position, rawDir * 0.5f);
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
    
    // Public methods
    public PaddlePattern GetCurrentPattern() => currentPattern;
    public float GetTransitionProgress() => transitionProgress;
    public bool IsAnimating() => currentPattern != PaddlePattern.None;
    public bool IsESP32Controlled() => isESP32Controlled;
    
    public void SetBalancedSwing(bool enabled)
    {
        useBalancedSwing = enabled;
        DebugLog($"Balanced swing: {(enabled ? "enabled" : "disabled")}");
    }
    
    // ESP32 integration - IMPROVED
    public void SetRawAngle(float angle)
    {
        useRawAngle = true;
        rawAngle = angle;
    }
    
    // Force pattern (used by ESP32GyroController)
    public void ForcePattern(int patternIndex)
    {
        // Only apply if not overridden by ESP32
        if (isESP32Controlled && overrideWithRawAngle) return;
        
        useRawAngle = false;
        
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