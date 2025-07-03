using UnityEngine;

public class PaddleIKController : MonoBehaviour
{
    [Header("References")]
    public Transform paddle;
    public Transform character;
    public BoatController boatController;
    public GyroPatternDetector patternDetector;
    
    [Header("Transform Settings")]
    public Vector3 offsetFromCharacter = new Vector3(0, 1f, 0.5f);
    public Vector3 paddleScale = Vector3.one;
    public Vector3 rotationOffset = Vector3.zero;
    
    [Header("Animation Settings")]
    [Range(-45f, 5f)] public float leftPaddleAngle = -20f;
    [Range(-5f, 45f)] public float rightPaddleAngle = 20f;
    [Range(1f, 10f)] public float rotationSpeed = 5f;
    [Range(0.5f, 5f)] public float swingSpeed = 2f;
    [Range(5f, 100f)] public float swingAmplitude = 25f;
    public bool useBalancedSwing = true;
    
    [Header("Pattern Detection")]
    public int minConsecutivePaddles = 2;
    public float speedThreshold = 0.3f;
    public float patternChangeDelay = 0.1f;
    public float patternBlendDuration = 0.8f;
    
    [Header("ESP32 Integration")]
    public bool useRawAngle = false;
    public float rawAngleMultiplier = 1.0f;
    [Range(0f, 1f)] public float rawAngleSmoothing = 0.8f;
    public bool overrideWithRawAngle = true;
    
    [Header("Gyro Idle Mode")]
    public bool syncIdleWithGyro = true;
    public float idleGyroSensitivity = 1.0f;
    public float idleThreshold = 0.1f;
    public float gyroIdleSmoothing = 5f;
    
    [Header("Auto Straighten")]
    public bool enableAutoStraighten = false;
    public float autoStraightenDelay = 2f;
    
    [Header("Debug")]
    public bool enableDebugLogs = true;
    public bool showGizmos = true;
    
    public enum PaddlePattern { None, Alternating, ConsecutiveLeft, ConsecutiveRight, GyroIdle }
    
    // State
    private PaddlePattern currentPattern = PaddlePattern.None;
    private Vector3 initialRotation;
    private float currentRotationValue;
    private float targetRotationValue;
    private float swingTimer;
    private float lastPatternChangeTime;
    
    // ESP32 & Gyro
    private float smoothedRawAngle = 0f;
    private float smoothedGyroIdle = 0f;
    private bool isESP32Controlled = false;
    private ESP32GyroController esp32Controller;
    
    // Pattern integration
    private float patternProcessCooldown = 0f;
    private const float patternProcessInterval = 0.1f;
    
    void Start()
    {
        InitializePaddle();
        esp32Controller = FindObjectOfType<ESP32GyroController>();
        
        if (patternDetector == null)
            patternDetector = FindObjectOfType<GyroPatternDetector>();
        
        if (esp32Controller != null && syncIdleWithGyro)
            enableAutoStraighten = false;
    }
    
    void Update()
    {
        UpdatePaddleTransform();
        CheckESP32Control();
        
        patternProcessCooldown -= Time.deltaTime;
        
        // Priority: Pattern detector > ESP32 > Legacy detection
        if (patternDetector != null && patternProcessCooldown <= 0f)
        {
            HandlePatternDetection();
        }
        else if (useRawAngle && isESP32Controlled)
        {
            HandleESP32Input();
        }
        else
        {
            HandleLegacyDetection();
        }
        
        ApplyRotation();
    }
    
    private void InitializePaddle()
    {
        if (paddle == null) return;
        
        initialRotation = paddle.eulerAngles + rotationOffset;
        currentRotationValue = initialRotation.z;
        targetRotationValue = initialRotation.z;
        paddle.localScale = paddleScale;
    }
    
    private void UpdatePaddleTransform()
    {
        if (paddle == null || character == null) return;
        
        paddle.position = character.position + character.TransformDirection(offsetFromCharacter);
        paddle.localScale = paddleScale;
    }
    
    private void CheckESP32Control()
    {
        bool wasControlled = isESP32Controlled;
        isESP32Controlled = (esp32Controller != null && esp32Controller.IsConnected());
        
        if (isESP32Controlled && overrideWithRawAngle)
            useRawAngle = true;
        
        if (wasControlled != isESP32Controlled)
            DebugLog($"ESP32 control: {(isESP32Controlled ? "ON" : "OFF")}");
    }
    
    private void HandlePatternDetection()
    {
        var detectedPattern = patternDetector.GetCurrentPattern();
        float confidence = patternDetector.GetPatternConfidence();
        
        if (confidence < 0.5f) return;
        
        PaddlePattern mappedPattern = MapPattern(detectedPattern);
        
        if (mappedPattern != currentPattern)
        {
            SetPattern(mappedPattern);
            patternProcessCooldown = patternProcessInterval;
        }
        
        AnimatePattern(mappedPattern, confidence);
    }
    
    private PaddlePattern MapPattern(GyroPatternDetector.MovementPattern detected)
    {
        switch (detected)
        {
            case GyroPatternDetector.MovementPattern.Forward:
                return PaddlePattern.Alternating;
            case GyroPatternDetector.MovementPattern.TurnLeft:
                return PaddlePattern.ConsecutiveRight;
            case GyroPatternDetector.MovementPattern.TurnRight:
                return PaddlePattern.ConsecutiveLeft;
            case GyroPatternDetector.MovementPattern.Idle:
                return (isESP32Controlled && syncIdleWithGyro) ? PaddlePattern.GyroIdle : PaddlePattern.None;
            default:
                return PaddlePattern.None;
        }
    }
    
    private void HandleESP32Input()
    {
        if (esp32Controller == null) return;
        
        bool isIdle = IsBoatIdle();
        
        if (isIdle && syncIdleWithGyro)
        {
            // Gyro idle mode
            float targetAngle = esp32Controller.GetSmoothedGyroValue() * idleGyroSensitivity;
            smoothedGyroIdle = Mathf.Lerp(smoothedGyroIdle, targetAngle, gyroIdleSmoothing * Time.deltaTime);
            targetRotationValue = initialRotation.z + smoothedGyroIdle;
            
            if (currentPattern != PaddlePattern.GyroIdle)
                SetPattern(PaddlePattern.GyroIdle);
        }
        else
        {
            // Active mode
            smoothedRawAngle = Mathf.Lerp(smoothedRawAngle, esp32Controller.GetSmoothedGyroValue() * rawAngleMultiplier, 
                                         (1f - rawAngleSmoothing) * Time.deltaTime * 10f);
            targetRotationValue = initialRotation.z + smoothedRawAngle;
        }
    }
    
    private void HandleLegacyDetection()
    {
        if (boatController == null) return;
        
        int leftCount = boatController.GetConsecutiveLeftCount();
        int rightCount = boatController.GetConsecutiveRightCount();
        float currentSpeed = boatController.GetCurrentSpeed();
        
        PaddlePattern newPattern = DeterminePattern(leftCount, rightCount, currentSpeed);
        
        if (newPattern != currentPattern && Time.time - lastPatternChangeTime > patternChangeDelay)
        {
            SetPattern(newPattern);
        }
        
        AnimatePattern(currentPattern, 1f);
    }
    
    private PaddlePattern DeterminePattern(int leftCount, int rightCount, float speed)
    {
        if (leftCount >= minConsecutivePaddles) return PaddlePattern.ConsecutiveLeft;
        if (rightCount >= minConsecutivePaddles) return PaddlePattern.ConsecutiveRight;
        if (speed > speedThreshold) return PaddlePattern.Alternating;
        
        return (isESP32Controlled && syncIdleWithGyro && IsBoatIdle()) ? PaddlePattern.GyroIdle : PaddlePattern.None;
    }
    
    private void AnimatePattern(PaddlePattern pattern, float confidence)
    {
        swingTimer += Time.deltaTime * swingSpeed * confidence;
        
        switch (pattern)
        {
            case PaddlePattern.Alternating:
                targetRotationValue = initialRotation.z + CalculateAlternatingSwing() * confidence;
                break;
            case PaddlePattern.ConsecutiveLeft:
                targetRotationValue = initialRotation.z + (leftPaddleAngle + CalculateSwing() * 0.6f) * confidence;
                break;
            case PaddlePattern.ConsecutiveRight:
                targetRotationValue = initialRotation.z + (rightPaddleAngle + CalculateSwing() * 0.6f) * confidence;
                break;
            case PaddlePattern.GyroIdle:
                if (isESP32Controlled)
                    targetRotationValue = initialRotation.z + smoothedGyroIdle;
                break;
            default:
                targetRotationValue = initialRotation.z;
                break;
        }
    }
    
    private float CalculateAlternatingSwing()
    {
        return useBalancedSwing ? 
            Mathf.Sin(swingTimer) * swingAmplitude : 
            -Mathf.Abs(Mathf.Sin(swingTimer)) * swingAmplitude;
    }
    
    private float CalculateSwing()
    {
        return Mathf.Abs(Mathf.Sin(swingTimer * 0.8f)) * swingAmplitude;
    }
    
    private void ApplyRotation()
    {
        if (paddle == null || character == null) return;
        
        // Auto-straighten check
        if (enableAutoStraighten && !isESP32Controlled && 
            currentPattern == PaddlePattern.None && 
            Time.time - lastPatternChangeTime > autoStraightenDelay)
        {
            float currentY = currentRotationValue;
            if (currentY > 180f) currentY -= 360f;
            targetRotationValue += -currentY * 0.5f * Time.deltaTime;
        }
        
        // Smooth rotation
        currentRotationValue = Mathf.LerpAngle(currentRotationValue, targetRotationValue, rotationSpeed * Time.deltaTime);
        
        // Apply rotation
        paddle.eulerAngles = new Vector3(
            initialRotation.x,
            character.eulerAngles.y + rotationOffset.y,
            currentRotationValue
        );
    }
    
    private void SetPattern(PaddlePattern pattern)
    {
        currentPattern = pattern;
        lastPatternChangeTime = Time.time;
        DebugLog($"Pattern: {pattern}");
    }
    
    private bool IsBoatIdle()
    {
        if (boatController == null) return true;
        return boatController.GetCurrentSpeed() < idleThreshold && 
               !boatController.IsLeftPaddling() && 
               !boatController.IsRightPaddling();
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PaddleIK] {message}");
    }
    
    void OnDrawGizmos()
    {
        if (!showGizmos || character == null) return;
        
        Gizmos.color = Color.yellow;
        Vector3 paddlePos = character.position + character.TransformDirection(offsetFromCharacter);
        Gizmos.DrawWireSphere(paddlePos, 0.1f);
        
        if (paddle != null && Application.isPlaying)
        {
            Color patternColor = isESP32Controlled ? Color.magenta : GetPatternColor();
            Gizmos.color = patternColor;
            Gizmos.DrawWireCube(paddle.position + Vector3.up * 0.5f, Vector3.one * 0.2f);
        }
    }
    
    private Color GetPatternColor()
    {
        switch (currentPattern)
        {
            case PaddlePattern.Alternating: return Color.green;
            case PaddlePattern.ConsecutiveLeft: return Color.red;
            case PaddlePattern.ConsecutiveRight: return Color.blue;
            case PaddlePattern.GyroIdle: return Color.cyan;
            default: return Color.white;
        }
    }
    
    // Public API
    public PaddlePattern GetCurrentPattern() => currentPattern;
    public bool IsESP32Controlled() => isESP32Controlled;
    public bool IsInGyroIdleMode() => currentPattern == PaddlePattern.GyroIdle;
    
    public void SetRawAngle(float angle)
    {
        useRawAngle = true;
        smoothedRawAngle = angle;
    }
    
    public void SetBalancedSwing(bool enabled)
    {
        useBalancedSwing = enabled;
    }
    
    public void ForcePattern(int patternIndex)
    {
        if (isESP32Controlled && overrideWithRawAngle) return;
        
        useRawAngle = false;
        if (patternIndex >= 0 && patternIndex < System.Enum.GetValues(typeof(PaddlePattern)).Length)
        {
            SetPattern((PaddlePattern)patternIndex);
        }
    }
}