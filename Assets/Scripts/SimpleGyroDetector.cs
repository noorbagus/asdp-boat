using UnityEngine;
using System.Collections.Generic;

public class SimpleGyroDetector : MonoBehaviour
{
    [Header("Detection Thresholds")]
    [SerializeField] private float turnThreshold = 10f; // degrees for turn detection
    [SerializeField] private float turnStabilityTime = 2f; // seconds of stability required
    [SerializeField] private float forwardSwingThreshold = 15f; // degrees for forward detection
    [SerializeField] private float forwardTimeWindow = 1.5f; // seconds for alternating pattern
    [SerializeField] private float deadZone = 5f; // degrees - idle threshold
    
    [Header("Smoothing")]
    [SerializeField] private float smoothingFactor = 0.3f;
    [SerializeField] private bool enableSmoothing = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    public enum BoatState { Idle, Forward, TurnLeft, TurnRight }
    
    // Current state
    private BoatState currentState = BoatState.Idle;
    private BoatState previousState = BoatState.Idle;
    private float stateConfidence = 0f;
    
    // Gyro data
    private Vector3 currentGyro = Vector3.zero;
    private Vector3 smoothedGyro = Vector3.zero;
    
    // Turn stability tracking
    private float turnAngleStartTime = 0f;
    private bool isInTurnAngle = false;
    private float currentTurnAngle = 0f;
    
    // Forward pattern tracking
    private List<SwingEvent> swingHistory = new List<SwingEvent>();
    
    // Events
    public System.Action<BoatState, float> OnStateChanged;
    
    private struct SwingEvent
    {
        public float timestamp;
        public bool isLeftSwing; // true = left, false = right
        public float intensity;
        
        public SwingEvent(float time, bool left, float intensity)
        {
            timestamp = time;
            isLeftSwing = left;
            this.intensity = intensity;
        }
    }
    
    private void Start()
    {
        DebugLog("SimpleGyroDetector initialized - Fixed offset, 2s stability");
    }
    
    /// <summary>
    /// Process calibrated gyro data and detect patterns
    /// </summary>
    public void ProcessCalibratedGyro(Vector3 calibratedGyro)
    {
        currentGyro = calibratedGyro;
        
        // Apply smoothing if enabled
        if (enableSmoothing)
        {
            smoothedGyro = Vector3.Lerp(smoothedGyro, calibratedGyro, smoothingFactor);
        }
        else
        {
            smoothedGyro = calibratedGyro;
        }
        
        // Use X-axis for primary detection (roll/tilt)
        float gyroX = smoothedGyro.x;
        
        // Clean up old swing history
        CleanupSwingHistory();
        
        // Detect patterns
        BoatState newState = DetectMovementPattern(gyroX);
        
        // Update state if changed
        if (newState != currentState)
        {
            UpdateState(newState);
        }
    }
    
    private BoatState DetectMovementPattern(float gyroX)
    {
        float absGyroX = Mathf.Abs(gyroX);
        
        // Check dead zone first (idle)
        if (absGyroX < deadZone)
        {
            // Reset turn tracking when in dead zone
            isInTurnAngle = false;
            return BoatState.Idle;
        }
        
        // Check for turn patterns (require 2-second stability)
        if (absGyroX > turnThreshold)
        {
            // Determine turn direction
            bool isTurningLeft = gyroX > 0;
            
            // Track stability time
            if (!isInTurnAngle || Mathf.Sign(currentTurnAngle) != Mathf.Sign(gyroX))
            {
                // Started new turn angle or changed direction
                isInTurnAngle = true;
                currentTurnAngle = gyroX;
                turnAngleStartTime = Time.time;
                
                DebugLog($"Turn angle started: {gyroX:F1}° at time {Time.time:F2}");
            }
            else
            {
                // Continue tracking current turn angle
                currentTurnAngle = gyroX;
                
                // Check if we've been stable long enough
                float stableTime = Time.time - turnAngleStartTime;
                if (stableTime >= turnStabilityTime)
                {
                    DebugLog($"Turn confirmed after {stableTime:F1}s stability");
                    return isTurningLeft ? BoatState.TurnLeft : BoatState.TurnRight;
                }
            }
            
            // Not stable long enough yet - stay in current state or go idle
            return currentState == BoatState.TurnLeft || currentState == BoatState.TurnRight ? 
                   currentState : BoatState.Idle;
        }
        else
        {
            // Not in turn threshold - reset turn tracking
            isInTurnAngle = false;
        }
        
        // Check for forward pattern (alternating swings)
        if (absGyroX > forwardSwingThreshold)
        {
            RecordSwingEvent(gyroX);
            
            if (IsAlternatingPattern())
            {
                return BoatState.Forward;
            }
        }
        
        // Default to current state or idle
        return currentState == BoatState.Forward ? BoatState.Forward : BoatState.Idle;
    }
    
    private void RecordSwingEvent(float gyroX)
    {
        bool isLeftSwing = gyroX > 0;
        float intensity = Mathf.Abs(gyroX) / forwardSwingThreshold;
        
        swingHistory.Add(new SwingEvent(Time.time, isLeftSwing, intensity));
        
        DebugLog($"Swing recorded: {(isLeftSwing ? "LEFT" : "RIGHT")} intensity: {intensity:F2}");
    }
    
    private bool IsAlternatingPattern()
    {
        if (swingHistory.Count < 2) return false;
        
        // Check recent swings within time window
        var recentSwings = new List<SwingEvent>();
        float currentTime = Time.time;
        
        for (int i = swingHistory.Count - 1; i >= 0; i--)
        {
            if (currentTime - swingHistory[i].timestamp <= forwardTimeWindow)
            {
                recentSwings.Add(swingHistory[i]);
            }
            else
            {
                break; // History is sorted by time
            }
        }
        
        // Need at least 2 swings for alternating pattern
        if (recentSwings.Count < 2) return false;
        
        // Check if swings alternate (left-right-left or right-left-right)
        bool isAlternating = true;
        for (int i = 0; i < recentSwings.Count - 1; i++)
        {
            if (recentSwings[i].isLeftSwing == recentSwings[i + 1].isLeftSwing)
            {
                isAlternating = false;
                break;
            }
        }
        
        if (isAlternating)
        {
            DebugLog($"Alternating pattern detected with {recentSwings.Count} swings");
        }
        
        return isAlternating;
    }
    
    private void CleanupSwingHistory()
    {
        float currentTime = Time.time;
        swingHistory.RemoveAll(swing => currentTime - swing.timestamp > forwardTimeWindow * 2);
    }
    
    private void UpdateState(BoatState newState)
    {
        previousState = currentState;
        currentState = newState;
        
        // Calculate confidence based on state
        stateConfidence = CalculateStateConfidence();
        
        DebugLog($"State: {previousState} → {currentState} (confidence: {stateConfidence:F2})");
        
        // Notify listeners
        OnStateChanged?.Invoke(currentState, stateConfidence);
    }
    
    private float CalculateStateConfidence()
    {
        switch (currentState)
        {
            case BoatState.TurnLeft:
            case BoatState.TurnRight:
                // High confidence for sustained turns
                float stableTime = Time.time - turnAngleStartTime;
                return Mathf.Clamp01(stableTime / turnStabilityTime);
                
            case BoatState.Forward:
                // Confidence based on swing consistency
                return swingHistory.Count > 0 ? 
                       Mathf.Clamp01(swingHistory.Count / 4f) : 0.5f;
                
            case BoatState.Idle:
                return 1.0f;
                
            default:
                return 0.5f;
        }
    }
    
    // Public getters
    public BoatState GetCurrentState() => currentState;
    public float GetStateConfidence() => stateConfidence;
    public Vector3 GetCurrentGyro() => currentGyro;
    public Vector3 GetSmoothedGyro() => smoothedGyro;
    public bool IsInTurnAngle() => isInTurnAngle;
    public float GetTurnStabilityTime() => isInTurnAngle ? Time.time - turnAngleStartTime : 0f;
    public int GetSwingHistoryCount() => swingHistory.Count;
    
    // Force state for testing
    public void ForceState(BoatState state)
    {
        UpdateState(state);
        DebugLog($"State forced to: {state}");
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[SimpleGyroDetector] {message}");
        }
    }
    
    // Debug GUI
    void OnGUI()
    {
        if (!enableDebugLogs) return;
        
        GUILayout.BeginArea(new Rect(10, 460, 350, 200));
        GUILayout.Box("Simple Gyro Detector");
        
        // Current state
        GUI.color = GetStateColor(currentState);
        GUILayout.Label($"State: {currentState} ({stateConfidence:F2})");
        GUI.color = Color.white;
        
        // Gyro data
        GUILayout.Label($"Raw Gyro: {currentGyro}");
        GUILayout.Label($"Smoothed: {smoothedGyro}");
        GUILayout.Label($"GyroX: {smoothedGyro.x:F1}° (deadzone: ±{deadZone}°)");
        
        // Turn tracking
        if (isInTurnAngle)
        {
            float stability = GetTurnStabilityTime();
            GUI.color = stability >= turnStabilityTime ? Color.green : Color.yellow;
            GUILayout.Label($"Turn stability: {stability:F1}s / {turnStabilityTime}s");
            GUI.color = Color.white;
        }
        
        // Forward tracking
        if (swingHistory.Count > 0)
        {
            GUILayout.Label($"Swings: {swingHistory.Count} (window: {forwardTimeWindow}s)");
        }
        
        // Thresholds
        GUILayout.Label($"Turn: ±{turnThreshold}° | Forward: ±{forwardSwingThreshold}°");
        
        // Force state buttons
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Idle")) ForceState(BoatState.Idle);
        if (GUILayout.Button("Forward")) ForceState(BoatState.Forward);
        if (GUILayout.Button("Left")) ForceState(BoatState.TurnLeft);
        if (GUILayout.Button("Right")) ForceState(BoatState.TurnRight);
        GUILayout.EndHorizontal();
        
        GUILayout.EndArea();
    }
    
    private Color GetStateColor(BoatState state)
    {
        switch (state)
        {
            case BoatState.Forward: return Color.green;
            case BoatState.TurnLeft: return Color.red;
            case BoatState.TurnRight: return Color.blue;
            case BoatState.Idle: return Color.gray;
            default: return Color.white;
        }
    }
}