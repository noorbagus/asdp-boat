using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GyroPatternDetector : MonoBehaviour
{
    public enum MovementPattern { Idle, Forward, TurnLeft, TurnRight, Gesture }
    
    [Header("Pattern Detection Settings")]
    public float alternatingTimeWindow = 1.5f;
    public int consecutiveStrokesForTurn = 2;
    public float idleTimeout = 3f;
    public float patternConfidenceThreshold = 0.7f;
    
    [Header("Multi-Axis Thresholds")]
    public float gyroXThreshold = 15f;
    public float gyroYThreshold = 10f;
    public float gyroZThreshold = 5f;
    public float totalMovementThreshold = 25f;
    
    [Header("Gesture Detection")]
    public float startGameAccelY = 2000f;
    public float restartGameAccelY = -2000f;
    public float gestureTimeout = 1f;
    
    [Header("Debug")]
    public bool enableDebugLogs = true;
    public bool showPatternHistory = true;
    
    // Pattern state
    private MovementPattern currentPattern = MovementPattern.Idle;
    private MovementPattern previousPattern = MovementPattern.Idle;
    private float patternConfidence = 0f;
    private float lastPatternChangeTime = 0f;
    
    // Movement history
    private List<MovementEvent> movementHistory = new List<MovementEvent>();
    private float lastMovementTime = 0f;
    private bool wasOverThreshold = false;
    
    // Current data
    private Vector3 currentGyro = Vector3.zero;
    private int currentAccelY = 0;
    private float combinedMovement = 0f;
    
    // Consecutive tracking
    private int consecutiveLeft = 0;
    private int consecutiveRight = 0;
    private bool lastWasLeft = false;
    private float lastStrokeTime = 0f;
    
    // Gesture tracking
    private float lastGestureTime = 0f;
    private bool gestureProcessed = false;
    
    private struct MovementEvent
    {
        public bool isLeftMovement;
        public float timestamp;
        public Vector3 gyroData;
        public float intensity;
        
        public MovementEvent(bool left, float time, Vector3 gyro, float intensity)
        {
            isLeftMovement = left;
            timestamp = time;
            gyroData = gyro;
            this.intensity = intensity;
        }
    }
    
    void Start()
    {
        DebugLog("GyroPatternDetector initialized");
        lastMovementTime = Time.time;
    }
    
    void Update()
    {
        CleanupHistory();
        // UpdatePatternDetection();
        UpdateConsecutiveTracking();
    }
    
    public void UpdateGyroData(Vector3 gyroData, int accelY)
    {
        currentGyro = gyroData;
        currentAccelY = accelY;
        combinedMovement = CalculateCombinedMovement(gyroData);
        
        // Check for gestures first
        if (CheckForGestures())
        {
            return;
        }
        
        // Process movement patterns
        ProcessMovementData();
    }
    
    private float CalculateCombinedMovement(Vector3 gyro)
    {
        return Mathf.Abs(gyro.x) + Mathf.Abs(gyro.y) + Mathf.Abs(gyro.z);
    }
    
    private bool CheckForGestures()
    {
        if (Time.time - lastGestureTime < gestureTimeout)
        {
            return gestureProcessed;
        }
        
        // Start game gesture (sharp upward movement)
        if (currentAccelY > startGameAccelY)
        {
            SetPattern(MovementPattern.Gesture, 1.0f);
            BroadcastGesture("START_GAME");
            lastGestureTime = Time.time;
            gestureProcessed = true;
            DebugLog($"START GAME gesture detected: {currentAccelY}");
            return true;
        }
        
        // Restart game gesture (sharp downward movement)
        if (currentAccelY < restartGameAccelY)
        {
            SetPattern(MovementPattern.Gesture, 1.0f);
            BroadcastGesture("RESTART_GAME");
            lastGestureTime = Time.time;
            gestureProcessed = true;
            DebugLog($"RESTART GAME gesture detected: {currentAccelY}");
            return true;
        }
        
        gestureProcessed = false;
        return false;
    }
    
    private void ProcessMovementData()
    {
        bool hasSignificantMovement = combinedMovement > totalMovementThreshold;
        
        if (hasSignificantMovement)
        {
            wasOverThreshold = true;
            lastMovementTime = Time.time;
            
            // Determine movement direction
            bool isLeftMovement = DetermineMovementDirection();
            
            // Record movement event
            RecordMovementEvent(isLeftMovement);
            
            // Analyze pattern
            AnalyzeMovementPattern();
        }
        else if (wasOverThreshold)
        {
            // Movement ended, finalize pattern
            FinalizeCurrentPattern();
            wasOverThreshold = false;
        }
        
        // Check for idle state
        if (Time.time - lastMovementTime > idleTimeout)
        {
            SetPattern(MovementPattern.Idle, 1.0f);
        }
    }
    
    private bool DetermineMovementDirection()
    {
        // Primary detection using Z-axis (roll)
        if (Mathf.Abs(currentGyro.z) > gyroZThreshold)
        {
            return currentGyro.z < 0; // Left tilt = left movement
        }
        
        // Secondary detection using X-axis
        if (Mathf.Abs(currentGyro.x) > gyroXThreshold)
        {
            return currentGyro.x < 0; // Left movement
        }
        
        // Fallback to last known direction
        return lastWasLeft;
    }
    
    private void RecordMovementEvent(bool isLeftMovement)
    {
        float intensity = combinedMovement / totalMovementThreshold;
        movementHistory.Add(new MovementEvent(isLeftMovement, Time.time, currentGyro, intensity));
        
        // Update consecutive tracking
        if (isLeftMovement)
        {
            if (lastWasLeft)
            {
                consecutiveLeft++;
                consecutiveRight = 0;
            }
            else
            {
                consecutiveLeft = 1;
                consecutiveRight = 0;
            }
            lastWasLeft = true;
        }
        else
        {
            if (!lastWasLeft)
            {
                consecutiveRight++;
                consecutiveLeft = 0;
            }
            else
            {
                consecutiveRight = 1;
                consecutiveLeft = 0;
            }
            lastWasLeft = false;
        }
        
        lastStrokeTime = Time.time;
        
        DebugLog($"Movement recorded: {(isLeftMovement ? "LEFT" : "RIGHT")} " +
                $"Consecutive L:{consecutiveLeft} R:{consecutiveRight} " +
                $"Intensity:{intensity:F2}");
    }
    
    private void AnalyzeMovementPattern()
    {
        if (movementHistory.Count < 2) return;
        
        // Check for consecutive patterns (higher priority)
        if (consecutiveLeft >= consecutiveStrokesForTurn)
        {
            SetPattern(MovementPattern.TurnLeft, CalculatePatternConfidence());
            return;
        }
        
        if (consecutiveRight >= consecutiveStrokesForTurn)
        {
            SetPattern(MovementPattern.TurnRight, CalculatePatternConfidence());
            return;
        }
        
        // Check for alternating pattern
        if (IsAlternatingPattern())
        {
            SetPattern(MovementPattern.Forward, CalculatePatternConfidence());
            return;
        }
    }
    
    private bool IsAlternatingPattern()
    {
        if (movementHistory.Count < 2) return false;
        
        var recentEvents = movementHistory.Where(e => Time.time - e.timestamp < alternatingTimeWindow).ToList();
        if (recentEvents.Count < 2) return false;
        
        // Check if movements alternate
        bool alternating = true;
        for (int i = 1; i < recentEvents.Count; i++)
        {
            if (recentEvents[i].isLeftMovement == recentEvents[i-1].isLeftMovement)
            {
                alternating = false;
                break;
            }
        }
        
        return alternating;
    }
    
    private float CalculatePatternConfidence()
    {
        float confidence = 0f;
        
        // Base confidence on movement intensity
        float avgIntensity = movementHistory.Count > 0 ? 
            movementHistory.Average(e => e.intensity) : 0f;
        confidence += avgIntensity * 0.4f;
        
        // Add confidence based on pattern consistency
        if (consecutiveLeft >= consecutiveStrokesForTurn || 
            consecutiveRight >= consecutiveStrokesForTurn)
        {
            confidence += 0.4f;
        }
        
        // Add confidence based on recent activity
        float timeSinceLastMove = Time.time - lastMovementTime;
        if (timeSinceLastMove < 1f)
        {
            confidence += 0.2f;
        }
        
        return Mathf.Clamp01(confidence);
    }
    
    private void FinalizeCurrentPattern()
    {
        // Ensure pattern confidence is above threshold
        if (patternConfidence < patternConfidenceThreshold)
        {
            SetPattern(MovementPattern.Idle, 1.0f);
        }
    }
    
    private void SetPattern(MovementPattern pattern, float confidence)
    {
        if (pattern != currentPattern)
        {
            previousPattern = currentPattern;
            currentPattern = pattern;
            patternConfidence = confidence;
            lastPatternChangeTime = Time.time;
            
            BroadcastPatternChange();
            
            DebugLog($"Pattern changed: {previousPattern} → {currentPattern} " +
                    $"(confidence: {confidence:F2})");
        }
        else
        {
            // Update confidence
            patternConfidence = confidence;
        }
    }
    
    private void BroadcastPatternChange()
    {
        // Send pattern change to interested components
        SendMessage("OnPatternChanged", currentPattern, SendMessageOptions.DontRequireReceiver);
        
        // Send specific pattern notifications
        switch (currentPattern)
        {
            case MovementPattern.Forward:
                SendMessage("OnForwardPattern", patternConfidence, SendMessageOptions.DontRequireReceiver);
                break;
            case MovementPattern.TurnLeft:
                SendMessage("OnTurnLeftPattern", patternConfidence, SendMessageOptions.DontRequireReceiver);
                break;
            case MovementPattern.TurnRight:
                SendMessage("OnTurnRightPattern", patternConfidence, SendMessageOptions.DontRequireReceiver);
                break;
            case MovementPattern.Idle:
                SendMessage("OnIdlePattern", patternConfidence, SendMessageOptions.DontRequireReceiver);
                break;
        }
    }
    
    private void BroadcastGesture(string gestureType)
    {
        SendMessage("OnGestureDetected", gestureType, SendMessageOptions.DontRequireReceiver);
    }
    
    private void CleanupHistory()
    {
        float currentTime = Time.time;
        movementHistory.RemoveAll(e => currentTime - e.timestamp > alternatingTimeWindow * 2);
    }
    
    private void UpdateConsecutiveTracking()
    {
        // Reset consecutive counts if too much time has passed
        if (Time.time - lastStrokeTime > alternatingTimeWindow)
        {
            if (consecutiveLeft > 0 || consecutiveRight > 0)
            {
                consecutiveLeft = 0;
                consecutiveRight = 0;
                DebugLog("Consecutive counts reset due to timeout");
            }
        }
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[GyroPattern] {message}");
        }
    }
    
    // Public API
    public MovementPattern GetCurrentPattern() => currentPattern;
    public MovementPattern GetPreviousPattern() => previousPattern;
    public float GetPatternConfidence() => patternConfidence;
    public int GetConsecutiveLeft() => consecutiveLeft;
    public int GetConsecutiveRight() => consecutiveRight;
    public float GetCombinedMovement() => combinedMovement;
    public int GetMovementHistoryCount() => movementHistory.Count;
    
    // Force pattern for testing
    public void ForcePattern(MovementPattern pattern)
    {
        SetPattern(pattern, 1.0f);
        DebugLog($"Pattern forced to: {pattern}");
    }
    
    // Reset detection state
    public void ResetDetection()
    {
        movementHistory.Clear();
        consecutiveLeft = 0;
        consecutiveRight = 0;
        currentPattern = MovementPattern.Idle;
        patternConfidence = 0f;
        DebugLog("Pattern detection reset");
    }
    
    void OnGUI()
    {
        if (!showPatternHistory || !enableDebugLogs) return;
        
        GUILayout.BeginArea(new Rect(10, 200, 300, 200));
        GUILayout.Label($"Pattern: {currentPattern} ({patternConfidence:F2})");
        GUILayout.Label($"Consecutive L:{consecutiveLeft} R:{consecutiveRight}");
        GUILayout.Label($"Combined Movement: {combinedMovement:F1}");
        GUILayout.Label($"History Count: {movementHistory.Count}");
        GUILayout.EndArea();
    }
}