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
    
    [Header("Debug")]
    public bool enableDebugLogs = true;
    public bool showGizmos = true;
    
    // Pattern states
    public enum PaddlePattern { None, Alternating, ConsecutiveLeft, ConsecutiveRight, GyroIdle }
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
    
    // ESP32 & Gyro smoothing
    private float smoothedRawAngle = 0f;
    private float smoothedGyroIdle = 0f;
    private bool isESP32Controlled = false;
    private ESP32GyroController esp32Controller;
    
    void Start()
    {
        InitializePaddle();
        esp32Controller = FindObjectOfType<ESP32GyroController>();
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
            float targetGyroAngle = esp32Controller.GetSmoothedGyroValue() * idleGyroSensitivity;
            smoothedGyroIdle = Mathf.Lerp(smoothedGyroIdle, targetGyroAngle, 
                                         gyroIdleSmoothing * Time.deltaTime);
            
            targetRotationValue = initialRotation.z + smoothedGyroIdle;
            
            if (enableDebugLogs && currentPattern != PaddlePattern.GyroIdle)
            {
                Debug.Log("[PaddleController] Switched to Gyro Idle Mode");
                currentPattern = PaddlePattern.GyroIdle;
            }
        }
        else
        {
            UpdateRawAngleSmoothing();
            targetRotationValue = initialRotation.z + smoothedRawAngle;
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
        isESP32Controlled = (esp32Controller != null && esp32Controller.IsConnected());
        
        if (isESP32Controlled && overrideWithRawAngle)
        {
            useRawAngle = true;
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
            return PaddlePattern.ConsecutiveLeft;
        
        if (rightCount >= minConsecutivePaddles)
            return PaddlePattern.ConsecutiveRight;
        
        if (currentSpeed > speedThreshold)
            return PaddlePattern.Alternating;
        
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
                Vector3 rawDir = Quaternion.Euler(0, character.eulerAngles.y, 
                    currentPattern == PaddlePattern.GyroIdle ? smoothedGyroIdle : smoothedRawAngle) * Vector3.forward;
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
            case PaddlePattern.GyroIdle: return Color.cyan;
            default: return Color.white;
        }
    }
    
    // Public methods
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
            previousPattern = currentPattern;
            targetPattern = (PaddlePattern)patternIndex;
            transitionProgress = 0f;
            previousRotationValue = currentRotationValue;
            DebugLog($"Force pattern to: {GetPatternName(targetPattern)}");
        }
    }
    
    public void SetGyroIdleSync(bool enabled)
    {
        syncIdleWithGyro = enabled;
        DebugLog($"Gyro idle sync: {(enabled ? "enabled" : "disabled")}");
    }
}