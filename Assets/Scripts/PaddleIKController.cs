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
    
    [Header("ESP32 Gyro Integration")]
    public bool useRawAngle = false;
    public float rawAngle = 0f;
    public float rawAngleMultiplier = 1.0f;
    [Range(0f, 1f)]
    public float rawAngleSmoothing = 0.8f;
    public bool overrideWithRawAngle = true;
    
    [Header("Idle Gyro Sync")]
    public bool syncIdleWithGyro = true;
    public float idleGyroSensitivity = 1.0f;
    public float idleThreshold = 0.1f;
    public float gyroIdleSmoothing = 5f;
    public Vector3 gyroAxisMapping = new Vector3(0, 0, 1);
    
    [Header("Auto Straighten (Disabled for Gyro Mode)")]
    public bool enableAutoStraighten = false; // Default false untuk gyro mode
    public float autoStraightenDelay = 2f;
    
    [Header("Debug")]
    public bool enableDebugLogs = true;
    public bool showGizmos = true;
    
    // Pattern states
    public enum PaddlePattern { None, Alternating, ConsecutiveLeft, ConsecutiveRight, GyroIdle }
    private PaddlePattern currentPattern = PaddlePattern.None;
    private PaddlePattern previousPattern = PaddlePattern.None;
    private PaddlePattern targetPattern = PaddlePattern.None;
    
    // Animation state
    private Vector3 initialRotation;
    private float currentRotationValue;
    private float targetRotationValue;
    private float previousRotationValue;
    private float transitionProgress = 1f;
    private float swingTimer;
    private float lastPatternChangeTime;
    private float lastTurnTime;
    
    // ESP32 & Gyro smoothing
    private float smoothedRawAngle = 0f;
    private float smoothedGyroIdle = 0f;
    private bool isESP32Controlled = false;
    private ESP32GyroController esp32Controller;
    
    void Start()
    {
        InitializePaddle();
        esp32Controller = FindObjectOfType<ESP32GyroController>();
        
        // Auto-disable autoStraighten jika gyro tersedia
        if (esp32Controller != null && syncIdleWithGyro)
        {
            enableAutoStraighten = false;
            DebugLog("Auto-straighten disabled for gyro mode");
        }
    }
    
    void Update()
    {
        UpdatePaddleTransform();
        CheckESP32Control();
        
        if (useRawAngle && isESP32Controlled)
        {
            HandleESP32GyroInput();
        }
        else
        {
            DetectPaddlePattern();
            AnimatePaddle();
        }
        
        ApplyRotation();
    }
    
    private void HandleESP32GyroInput()
    {
        if (esp32Controller == null) return;
        
        bool isIdle = IsBoatIdle();
        
        if (isIdle && syncIdleWithGyro)
        {
            // Gyro idle mode - paddle mengikuti gyro langsung
            float targetGyroAngle = esp32Controller.GetSmoothedGyroValue() * idleGyroSensitivity;
            smoothedGyroIdle = Mathf.Lerp(smoothedGyroIdle, targetGyroAngle, 
                                         gyroIdleSmoothing * Time.deltaTime);
            
            targetRotationValue = initialRotation.z + smoothedGyroIdle;
            
            if (currentPattern != PaddlePattern.GyroIdle)
            {
                SetPattern(PaddlePattern.GyroIdle);
                DebugLog("Switched to Gyro Idle Mode");
            }
        }
        else
        {
            // Active mode - gunakan raw angle
            UpdateRawAngleSmoothing();
            targetRotationValue = initialRotation.z + smoothedRawAngle;
            
            if (currentPattern == PaddlePattern.GyroIdle)
            {
                DebugLog("Exited Gyro Idle Mode");
            }
        }
    }
    
    private bool IsBoatIdle()
    {
        if (boatController == null) return true;
        
        float currentSpeed = boatController.GetCurrentSpeed();
        bool isLeftPaddling = boatController.IsLeftPaddling();
        bool isRightPaddling = boatController.IsRightPaddling();
        
        return currentSpeed < idleThreshold && !isLeftPaddling && !isRightPaddling;
    }
    
    private void CheckESP32Control()
    {
        bool wasESP32Controlled = isESP32Controlled;
        isESP32Controlled = (esp32Controller != null && esp32Controller.IsConnected());
        
        if (isESP32Controlled && overrideWithRawAngle)
        {
            useRawAngle = true;
        }
        
        // Log perubahan status
        if (wasESP32Controlled != isESP32Controlled)
        {
            DebugLog($"ESP32 control: {(isESP32Controlled ? "Connected" : "Disconnected")}");
        }
    }
    
    private void UpdateRawAngleSmoothing()
    {
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
        if (boatController == null) return;
        
        // Jangan ubah pattern jika sedang dalam GyroIdle dan ESP32 aktif
        if (currentPattern == PaddlePattern.GyroIdle && isESP32Controlled && syncIdleWithGyro)
        {
            return;
        }
        
        int leftCount = boatController.GetConsecutiveLeftCount();
        int rightCount = boatController.GetConsecutiveRightCount();
        float currentSpeed = boatController.GetCurrentSpeed();
        
        PaddlePattern newPattern = DeterminePattern(leftCount, rightCount, currentSpeed);
        
        if (newPattern != targetPattern && Time.time - lastPatternChangeTime > patternChangeDelay)
        {
            SetPattern(newPattern);
            LogPatternChange(newPattern, leftCount, rightCount);
        }
    }
    
    private PaddlePattern DeterminePattern(int leftCount, int rightCount, float currentSpeed)
    {
        // Prioritas: Consecutive > Alternating > None
        if (leftCount >= minConsecutivePaddles)
            return PaddlePattern.ConsecutiveLeft;
        
        if (rightCount >= minConsecutivePaddles)
            return PaddlePattern.ConsecutiveRight;
        
        if (currentSpeed > speedThreshold)
            return PaddlePattern.Alternating;
        
        // Jika dalam ESP32 mode dan idle, pertahankan GyroIdle
        if (isESP32Controlled && syncIdleWithGyro && IsBoatIdle())
            return PaddlePattern.GyroIdle;
        
        return PaddlePattern.None;
    }
    
    private void SetPattern(PaddlePattern newPattern)
    {
        previousPattern = currentPattern;
        targetPattern = newPattern;
        lastPatternChangeTime = Time.time;
        previousRotationValue = currentRotationValue;
        transitionProgress = 0f;
    }
    
    private void AnimatePaddle()
    {
        // Update transition progress
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
        
        // Calculate target rotation based on pattern
        float patternRotation = CalculatePatternRotation();
        
        // Apply transition blending
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
    
    private float CalculatePatternRotation()
    {
        switch (targetPattern)
        {
            case PaddlePattern.Alternating:
                return CalculateAlternatingRotation();
            
            case PaddlePattern.ConsecutiveLeft:
                return CalculateLeftTurnRotation();
            
            case PaddlePattern.ConsecutiveRight:
                return CalculateRightTurnRotation();
            
            case PaddlePattern.GyroIdle:
                // Gyro idle - gunakan smoothed gyro value
                return initialRotation.z + smoothedGyroIdle;
            
            default: // None
                // Jika dalam ESP32 mode dan idle, gunakan gyro
                if (isESP32Controlled && syncIdleWithGyro && IsBoatIdle())
                {
                    return initialRotation.z + smoothedGyroIdle;
                }
                return initialRotation.z; // Default netral
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
        
        // Smooth rotation dengan check auto-straighten
        bool shouldAutoStraighten = enableAutoStraighten && 
                                   !isESP32Controlled && 
                                   currentPattern != PaddlePattern.GyroIdle &&
                                   Time.time - lastTurnTime > autoStraightenDelay;
        
        if (shouldAutoStraighten)
        {
            // Apply auto-straighten force
            float currentY = currentRotationValue;
            if (currentY > 180f) currentY -= 360f;
            float straightenForce = -currentY * 0.5f * Time.deltaTime;
            targetRotationValue += straightenForce;
        }
        
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
            case PaddlePattern.GyroIdle: return "Gyro Idle";
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
    
    private void OnDrawGizmos()
    {
        if (!showGizmos || character == null) return;
        
        Gizmos.color = Color.yellow;
        Vector3 paddlePos = character.position + character.TransformDirection(offsetFromCharacter);
        Gizmos.DrawWireSphere(paddlePos, 0.1f);
        
        if (paddle != null && Application.isPlaying)
        {
            Gizmos.color = isESP32Controlled ? Color.magenta : GetPatternColor(currentPattern);
            Gizmos.DrawWireCube(paddle.position + Vector3.up * 0.5f, Vector3.one * 0.2f);
            
            if (useRawAngle && isESP32Controlled)
            {
                Gizmos.color = currentPattern == PaddlePattern.GyroIdle ? Color.cyan : Color.green;
                Vector3 direction = Quaternion.Euler(0, character.eulerAngles.y, 
                    currentPattern == PaddlePattern.GyroIdle ? smoothedGyroIdle : smoothedRawAngle) * Vector3.forward;
                Gizmos.DrawRay(paddle.position, direction * 0.5f);
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
            case PaddlePattern.GyroIdle: return Color.cyan;
            default: return Color.white;
        }
    }
    
    // Public API
    public PaddlePattern GetCurrentPattern() => currentPattern;
    public float GetTransitionProgress() => transitionProgress;
    public bool IsAnimating() => currentPattern != PaddlePattern.None;
    public bool IsESP32Controlled() => isESP32Controlled;
    public bool IsInGyroIdleMode() => currentPattern == PaddlePattern.GyroIdle;
    
    public void SetBalancedSwing(bool enabled)
    {
        useBalancedSwing = enabled;
        DebugLog($"Balanced swing: {(enabled ? "enabled" : "disabled")}");
    }
    
    public void SetRawAngle(float angle)
    {
        useRawAngle = true;
        rawAngle = angle;
    }
    
    public void ForcePattern(int patternIndex)
    {
        if (isESP32Controlled && overrideWithRawAngle) return;
        
        useRawAngle = false;
        
        if (patternIndex >= 0 && patternIndex < System.Enum.GetValues(typeof(PaddlePattern)).Length)
        {
            SetPattern((PaddlePattern)patternIndex);
            DebugLog($"Force pattern to: {GetPatternName(targetPattern)}");
        }
    }
    
    public void SetGyroIdleSync(bool enabled)
    {
        syncIdleWithGyro = enabled;
        DebugLog($"Gyro idle sync: {(enabled ? "enabled" : "disabled")}");
    }
    
    public void SetAutoStraighten(bool enabled)
    {
        // Disable auto-straighten jika ESP32 aktif dan gyro sync enabled
        if (enabled && isESP32Controlled && syncIdleWithGyro)
        {
            DebugLog("Auto-straighten disabled - ESP32 gyro mode active");
            return;
        }
        
        enableAutoStraighten = enabled;
        DebugLog($"Auto-straighten: {(enabled ? "enabled" : "disabled")}");
    }
}