using UnityEngine;

public class PaddleIKController : MonoBehaviour
{
    [Header("References")]
    public Transform paddle;
    public Transform character;
    
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
    
    [Header("Raw Angle Input")]
    public bool useRawAngle = true;
    public float rawAngleMultiplier = 1.0f;
    [Range(0f, 1f)] public float rawAngleSmoothing = 0.8f;
    
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
    
    // Raw angle input
    private float smoothedRawAngle = 0f;
    private float currentRawAngle = 0f;
    
    void Start()
    {
        InitializePaddle();
    }
    
    void Update()
    {
        UpdatePaddleTransform();
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
    
    private void ApplyRotation()
    {
        if (paddle == null || character == null) return;
        
        if (useRawAngle && currentPattern != PaddlePattern.None)
        {
            // Use raw angle from PaddleInputManager
            smoothedRawAngle = Mathf.Lerp(smoothedRawAngle, currentRawAngle * rawAngleMultiplier, 
                                         (1f - rawAngleSmoothing) * Time.deltaTime * 10f);
            targetRotationValue = initialRotation.z + smoothedRawAngle;
        }
        else
        {
            // Use pattern-based animation
            AnimatePattern(currentPattern);
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
    
    private void AnimatePattern(PaddlePattern pattern)
    {
        swingTimer += Time.deltaTime * swingSpeed;
        
        switch (pattern)
        {
            case PaddlePattern.Alternating:
                targetRotationValue = initialRotation.z + CalculateAlternatingSwing();
                break;
            case PaddlePattern.ConsecutiveLeft:
                targetRotationValue = initialRotation.z + (leftPaddleAngle + CalculateSwing() * 0.6f);
                break;
            case PaddlePattern.ConsecutiveRight:
                targetRotationValue = initialRotation.z + (rightPaddleAngle + CalculateSwing() * 0.6f);
                break;
            case PaddlePattern.GyroIdle:
                // Keep current raw angle
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
    
    // PUBLIC API - Called by PaddleInputManager
    public void SetRawAngle(float angle)
    {
        currentRawAngle = angle;
        currentPattern = PaddlePattern.GyroIdle;
        
        if (enableDebugLogs)
            DebugLog($"Raw angle set: {angle:F1}°");
    }
    
    public void SetPattern(PaddlePattern pattern)
    {
        if (pattern != currentPattern)
        {
            currentPattern = pattern;
            DebugLog($"Pattern: {pattern}");
        }
    }
    
    public void ForcePattern(int patternIndex)
    {
        if (patternIndex >= 0 && patternIndex < System.Enum.GetValues(typeof(PaddlePattern)).Length)
        {
            SetPattern((PaddlePattern)patternIndex);
        }
    }
    
    // Getters
    public PaddlePattern GetCurrentPattern() => currentPattern;
    public float GetCurrentAngle() => currentRotationValue - initialRotation.z;
    public bool IsUsingRawAngle() => useRawAngle && currentPattern == PaddlePattern.GyroIdle;
    
    void OnDrawGizmos()
    {
        if (!showGizmos || character == null) return;
        
        Gizmos.color = Color.yellow;
        Vector3 paddlePos = character.position + character.TransformDirection(offsetFromCharacter);
        Gizmos.DrawWireSphere(paddlePos, 0.1f);
        
        if (paddle != null && Application.isPlaying)
        {
            Color patternColor = GetPatternColor();
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
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PaddleIK] {message}");
    }
}