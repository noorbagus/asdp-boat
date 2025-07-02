using UnityEngine;
using System.Collections.Generic;

public class ESP32BoatIntegration : MonoBehaviour
{
    [Header("References")]
    public ESP32GyroController gyroController;
    public BoatController boatController;
    public PaddleIKController paddleController;

    [Header("Gyro Control Settings")]
    [Tooltip("Enable to control boat turning with ESP32 gyro")]
    public bool enableGyroSteering = true;
    [Tooltip("Angle threshold before registering as a paddle input")]
    public float gyroThreshold = 15f;
    [Tooltip("Cooldown between paddle inputs (seconds)")]
    public float paddleCooldown = 0.5f;
    [Tooltip("Map tilt left to right paddle and vice versa")]
    public bool invertPaddles = false;

    [Header("Alternating Movement - NEW")]
    [Tooltip("Time window to detect alternating left-right pattern")]
    public float alternatingTimeWindow = 1.5f;
    [Tooltip("Minimum alternating tilts needed for forward movement")]
    public int minAlternatingTilts = 3;
    [Tooltip("Speed multiplier for alternating forward movement")]
    public float forwardSpeedMultiplier = 2.0f;
    [Tooltip("Enable alternating pattern detection")]
    public bool enableAlternatingMovement = true;

    [Header("Idle Behavior - NEW")]
    [Tooltip("Enable paddle to follow gyro when boat is idle")]
    public bool enableIdleFollow = true;
    [Tooltip("Time before entering idle state")]
    public float idleTimeout = 2.0f;
    [Tooltip("Angle threshold to consider as idle")]
    public float idleAngleThreshold = 5f;
    [Tooltip("How fast paddle follows gyro in idle")]
    public float idleFollowSmoothing = 5f;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    // Paddle state
    private float leftCooldownTime = 0f;
    private float rightCooldownTime = 0f;
    private float lastAngle = 0f;
    private float steadyTimer = 0f;
    private bool wasOverThreshold = false;

    // NEW: Alternating pattern detection
    private List<TiltEvent> tiltHistory = new List<TiltEvent>();
    private float lastTiltTime = 0f;
    private bool isInAlternatingMode = false;

    // NEW: Idle state management
    private bool isIdle = true;
    private float lastMovementTime = 0f;
    private float idleAngle = 0f;

    private struct TiltEvent
    {
        public bool isLeftTilt;
        public float timestamp;
        
        public TiltEvent(bool left, float time)
        {
            isLeftTilt = left;
            timestamp = time;
        }
    }

    void Start()
    {
        // Validate references
        if (gyroController == null)
        {
            Debug.LogError("ESP32GyroController reference not set!");
            enabled = false;
            return;
        }

        if (boatController == null)
        {
            Debug.LogError("BoatController reference not set!");
            enabled = false;
            return;
        }

        lastMovementTime = Time.time;
    }

    void Update()
    {
        if (!enableGyroSteering || !gyroController.IsConnected())
            return;

        // Get the current gyro angle
        float currentAngle = gyroController.GetSmoothedGyroValue();
        
        // Update idle state
        UpdateIdleState(currentAngle);
        
        // Handle idle paddle following
        if (isIdle && enableIdleFollow)
        {
            HandleIdlePaddleFollow(currentAngle);
            return; // Skip movement processing when idle
        }
        
        // Reduce cooldown timers
        leftCooldownTime -= Time.deltaTime;
        rightCooldownTime -= Time.deltaTime;
        
        // Clean up old tilt history
        CleanupTiltHistory();
        
        // Check if we have a large enough tilt to trigger a paddle
        if (Mathf.Abs(currentAngle) > gyroThreshold)
        {
            wasOverThreshold = true;
            steadyTimer = 0f;
            
            // Determine tilt direction
            bool isTiltingRight = currentAngle > 0;
            
            // Only trigger paddle if we're not in cooldown
            if (isTiltingRight && leftCooldownTime <= 0)
            {
                // FIXED: Correct invert paddles logic
                if (invertPaddles)
                    TriggerRightPaddle();
                else
                    TriggerLeftPaddle();
                    
                // Record tilt event for pattern detection
                RecordTiltEvent(false); // Right tilt = left paddle
                leftCooldownTime = paddleCooldown;
            }
            else if (!isTiltingRight && rightCooldownTime <= 0)
            {
                // FIXED: Correct invert paddles logic  
                if (invertPaddles)
                    TriggerLeftPaddle();
                else
                    TriggerRightPaddle();
                    
                // Record tilt event for pattern detection
                RecordTiltEvent(true); // Left tilt = right paddle
                rightCooldownTime = paddleCooldown;
            }
        }
        else if (wasOverThreshold)
        {
            // We were over threshold but now we're not - track steady time
            steadyTimer += Time.deltaTime;
            
            // If we've been steady for a while, reset the over-threshold flag
            if (steadyTimer > 0.5f)
            {
                wasOverThreshold = false;
            }
        }
        
        lastAngle = currentAngle;
    }
    
    // NEW: Update idle state based on movement and angle
    private void UpdateIdleState(float currentAngle)
    {
        bool wasIdle = isIdle;
        
        // Check if we're in movement or idle
        if (Mathf.Abs(currentAngle) < idleAngleThreshold && 
            (boatController.GetCurrentSpeed() < 0.1f || Time.time - lastMovementTime > idleTimeout))
        {
            isIdle = true;
            idleAngle = currentAngle;
        }
        else
        {
            isIdle = false;
            lastMovementTime = Time.time;
        }
        
        // Log state changes
        if (wasIdle != isIdle)
        {
            DebugLog($"State changed to: {(isIdle ? "IDLE" : "ACTIVE")}");
        }
    }
    
    // NEW: Handle paddle following gyro when idle
    private void HandleIdlePaddleFollow(float currentAngle)
    {
        if (paddleController != null)
        {
            // Smooth follow the gyro angle
            float smoothedAngle = Mathf.Lerp(idleAngle, currentAngle, idleFollowSmoothing * Time.deltaTime);
            paddleController.SetRawAngle(smoothedAngle);
            idleAngle = smoothedAngle;
        }
    }
    
    // NEW: Record tilt events for pattern detection
    private void RecordTiltEvent(bool isLeftPaddle)
    {
        tiltHistory.Add(new TiltEvent(isLeftPaddle, Time.time));
        lastTiltTime = Time.time;
        
        // Analyze pattern
        if (enableAlternatingMovement)
        {
            AnalyzeAlternatingPattern();
        }
        
        DebugLog($"Tilt recorded: {(isLeftPaddle ? "LEFT" : "RIGHT")} - History count: {tiltHistory.Count}");
    }
    
    // NEW: Analyze for alternating left-right pattern
    private void AnalyzeAlternatingPattern()
    {
        if (tiltHistory.Count < minAlternatingTilts)
            return;
            
        // Check last few tilts for alternating pattern
        bool isAlternating = true;
        for (int i = tiltHistory.Count - 1; i > tiltHistory.Count - minAlternatingTilts; i--)
        {
            if (i > 0 && tiltHistory[i].isLeftTilt == tiltHistory[i-1].isLeftTilt)
            {
                isAlternating = false;
                break;
            }
        }
        
        if (isAlternating)
        {
            // Alternating pattern detected - trigger forward movement
            TriggerForwardMovement();
            isInAlternatingMode = true;
            
            // Clear history to prevent spam
            tiltHistory.Clear();
            
            DebugLog("ALTERNATING PATTERN DETECTED - Forward movement triggered!");
        }
    }
    
    // NEW: Trigger forward movement for alternating pattern
    private void TriggerForwardMovement()
    {
        if (boatController != null)
        {
            // Use reflection to call AddForwardThrust with multiplier
            var method = boatController.GetType().GetMethod("AddForwardThrust", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (method != null)
            {
                method.Invoke(boatController, new object[] { forwardSpeedMultiplier });
            }
            else
            {
                // Fallback: trigger alternating paddles quickly
                boatController.PaddleLeft();
                boatController.PaddleRight();
            }
        }
    }
    
    // Clean up old tilt history
    private void CleanupTiltHistory()
    {
        float currentTime = Time.time;
        
        while (tiltHistory.Count > 0 && currentTime - tiltHistory[0].timestamp > alternatingTimeWindow)
        {
            tiltHistory.RemoveAt(0);
        }
    }
    
    private void TriggerLeftPaddle()
    {
        boatController.PaddleLeft();
        lastMovementTime = Time.time;
        DebugLog("ESP32: Left paddle triggered");
    }
    
    private void TriggerRightPaddle()
    {
        boatController.PaddleRight();
        lastMovementTime = Time.time;
        DebugLog("ESP32: Right paddle triggered");
    }
    
    // Debug logging
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ESP32Boat] {message}");
        }
    }
    
    // Public methods to toggle gyro steering
    public void EnableGyroSteering(bool enable)
    {
        enableGyroSteering = enable;
    }
    
    public void ToggleGyroSteering()
    {
        enableGyroSteering = !enableGyroSteering;
    }
    
    // NEW: Public getters for debugging
    public bool IsIdle() => isIdle;
    public bool IsInAlternatingMode() => isInAlternatingMode;
    public int GetTiltHistoryCount() => tiltHistory.Count;
    public float GetIdleAngle() => idleAngle;
    
    // NEW: Force idle state for testing
    public void ForceIdleState(bool idle)
    {
        isIdle = idle;
        if (idle)
        {
            lastMovementTime = Time.time - idleTimeout - 1f;
        }
        else
        {
            lastMovementTime = Time.time;
        }
    }
}