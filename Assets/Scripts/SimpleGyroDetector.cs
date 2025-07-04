using UnityEngine;
using System.Collections.Generic;

public class SimpleGyroDetector : MonoBehaviour
{
    [Header("Detection Thresholds")]
    [SerializeField] private float turnThreshold = 8f; // degrees for instant turn
    [SerializeField] private float forwardSwingThreshold = 12f; // degrees for forward detection
    [SerializeField] private float forwardTimeWindow = 1.5f; // seconds for alternating pattern
    
    [Header("Smoothing")]
    [SerializeField] private float smoothingFactor = 0.3f;
    [SerializeField] private bool enableSmoothing = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    public enum BoatState { Idle, Forward, TurnLeft, TurnRight }
    
    // Current state
    private BoatState currentState = BoatState.Idle;
    private float stateConfidence = 1f;
    
    // Gyro data
    private Vector3 currentGyro = Vector3.zero;
    private Vector3 smoothedGyro = Vector3.zero;
    
    // Forward pattern tracking
    private List<SwingEvent> swingHistory = new List<SwingEvent>();
    
    // Events
    public System.Action<BoatState, float> OnStateChanged;
    
    private struct SwingEvent
    {
        public float timestamp;
        public bool isLeftSwing;
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
        DebugLog("SimpleGyroDetector initialized - Simple instant detection");
    }
    
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
        DebugLog($"Gyro: {gyroX:F1}°");
        
        // Simple instant tilt detection
        if (gyroX > turnThreshold) 
        {
            DebugLog($"LEFT turn: {gyroX:F1}° > {turnThreshold}°");
            return BoatState.TurnLeft;
        }
        
        if (gyroX < -turnThreshold) 
        {
            DebugLog($"RIGHT turn: {gyroX:F1}° < -{turnThreshold}°");
            return BoatState.TurnRight;
        }
        
        // Forward alternating pattern
        float absGyroX = Mathf.Abs(gyroX);
        if (absGyroX > forwardSwingThreshold)
        {
            RecordSwingEvent(gyroX);
            
            if (IsAlternatingPattern())
            {
                DebugLog($"FORWARD: alternating pattern detected");
                return BoatState.Forward;
            }
        }
        
        return BoatState.Idle;
    }
    
    private void RecordSwingEvent(float gyroX)
    {
        bool isLeftSwing = gyroX > 0;
        float intensity = Mathf.Abs(gyroX) / forwardSwingThreshold;
        swingHistory.Add(new SwingEvent(Time.time, isLeftSwing, intensity));
        
        DebugLog($"Swing: {(isLeftSwing ? "LEFT" : "RIGHT")} {intensity:F2}");
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
            else break;
        }
        
        if (recentSwings.Count < 2) return false;
        
        // Check alternating pattern
        for (int i = 0; i < recentSwings.Count - 1; i++)
        {
            if (recentSwings[i].isLeftSwing == recentSwings[i + 1].isLeftSwing)
            {
                return false;
            }
        }
        
        DebugLog($"Alternating pattern: {recentSwings.Count} swings");
        return true;
    }
    
    private void CleanupSwingHistory()
    {
        float currentTime = Time.time;
        swingHistory.RemoveAll(swing => currentTime - swing.timestamp > forwardTimeWindow * 2);
    }
    
    private void UpdateState(BoatState newState)
    {
        BoatState previousState = currentState;
        currentState = newState;
        
        DebugLog($"State: {previousState} → {currentState}");
        OnStateChanged?.Invoke(currentState, stateConfidence);
    }
    
    // Public API
    public BoatState GetCurrentState() => currentState;
    public float GetStateConfidence() => stateConfidence;
    public Vector3 GetCurrentGyro() => currentGyro;
    public Vector3 GetSmoothedGyro() => smoothedGyro;
    public int GetSwingHistoryCount() => swingHistory.Count;
    
    public void ForceState(BoatState state)
    {
        UpdateState(state);
        DebugLog($"Forced: {state}");
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
        
        GUILayout.BeginArea(new Rect(10, 460, 300, 150));
        GUILayout.Box("Simple Gyro Detector");
        
        // State
        GUI.color = GetStateColor(currentState);
        GUILayout.Label($"State: {currentState}");
        GUI.color = Color.white;
        
        // Data
        GUILayout.Label($"GyroX: {smoothedGyro.x:F1}°");
        GUILayout.Label($"Turn: ±{turnThreshold}° | Forward: ±{forwardSwingThreshold}°");
        GUILayout.Label($"Swings: {swingHistory.Count}");
        
        // Force buttons
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
            default: return Color.gray;
        }
    }
}