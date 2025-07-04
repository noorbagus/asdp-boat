using UnityEngine;
using System.Collections.Generic;

public class GravityCalibrator : MonoBehaviour
{
    [Header("Static Calibration Settings")]
    [SerializeField] private int requiredSamples = 100;
    [SerializeField] private float stabilityThreshold = 3f; // degrees magnitude
    [SerializeField] private float sampleRate = 20f; // Hz
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // Calibration state
    private bool isCalibrating = false;
    private bool isCalibrated = false;
    private Vector3 staticOffset = Vector3.zero;
    
    // Sample collection
    private List<Vector3> calibrationSamples = new List<Vector3>();
    private int currentSampleCount = 0;
    
    // Events for UI integration
    public System.Action<int, int> OnProgress; // current, total
    public System.Action<Vector3> OnCalibrationComplete;
    public System.Action<bool> OnCalibrationStateChanged;
    
    // Current gyro data
    private Vector3 currentRawGyro = Vector3.zero;
    private Vector3 lastCalibratedGyro = Vector3.zero;
    
    private void Start()
    {
        DebugLog("GravityCalibrator initialized - Static zero calibration only");
    }
    
    /// <summary>
    /// Start static zero calibration process
    /// </summary>
    public void StartCalibration()
    {
        if (isCalibrating || isCalibrated) return;
        
        DebugLog($"Starting static calibration - Need {requiredSamples} stable samples");
        
        isCalibrating = true;
        isCalibrated = false;
        staticOffset = Vector3.zero;
        calibrationSamples.Clear();
        currentSampleCount = 0;
        
        OnCalibrationStateChanged?.Invoke(true);
        OnProgress?.Invoke(0, requiredSamples);
    }
    
    /// <summary>
    /// Process new gyro data for calibration or normal operation
    /// </summary>
    public void ProcessGyroData(Vector3 rawGyro)
    {
        currentRawGyro = rawGyro;
        
        if (isCalibrating)
        {
            ProcessCalibrationSample(rawGyro);
        }
        else if (isCalibrated)
        {
            // Apply static offset
            lastCalibratedGyro = rawGyro - staticOffset;
        }
        else
        {
            // Not calibrated yet - pass through raw data
            lastCalibratedGyro = rawGyro;
        }
    }
    
    private void ProcessCalibrationSample(Vector3 rawGyro)
    {
        float magnitude = rawGyro.magnitude;
        
        // Only collect samples when paddle is stable (magnitude < threshold)
        if (magnitude < stabilityThreshold)
        {
            calibrationSamples.Add(rawGyro);
            currentSampleCount++;
            
            DebugLog($"Calibration sample {currentSampleCount}/{requiredSamples} - magnitude: {magnitude:F2}°");
            
            // Update UI progress
            OnProgress?.Invoke(currentSampleCount, requiredSamples);
            
            // Check if we have enough samples
            if (currentSampleCount >= requiredSamples)
            {
                CompleteCalibration();
            }
        }
        else
        {
            DebugLog($"Sample rejected - too much movement: {magnitude:F2}° > {stabilityThreshold}°");
        }
    }
    
    private void CompleteCalibration()
    {
        if (calibrationSamples.Count < requiredSamples)
        {
            DebugLog($"ERROR: Not enough samples collected: {calibrationSamples.Count}/{requiredSamples}");
            return;
        }
        
        // Calculate static offset as average of all samples
        Vector3 totalOffset = Vector3.zero;
        foreach (Vector3 sample in calibrationSamples)
        {
            totalOffset += sample;
        }
        
        staticOffset = totalOffset / calibrationSamples.Count;
        
        isCalibrating = false;
        isCalibrated = true;
        
        DebugLog($"✓ Static calibration complete!");
        DebugLog($"  Samples collected: {calibrationSamples.Count}");
        DebugLog($"  Static offset: {staticOffset} (magnitude: {staticOffset.magnitude:F3}°)");
        DebugLog($"  This offset will be applied permanently for this session");
        
        // Clear samples to save memory
        calibrationSamples.Clear();
        
        // Notify listeners
        OnCalibrationStateChanged?.Invoke(false);
        OnCalibrationComplete?.Invoke(staticOffset);
    }
    
    /// <summary>
    /// Get calibrated gyro data (raw - static offset)
    /// </summary>
    public Vector3 GetCalibratedGyro()
    {
        return lastCalibratedGyro;
    }
    
    /// <summary>
    /// Reset calibration (for new game session)
    /// </summary>
    public void ResetCalibration()
    {
        DebugLog("Resetting calibration for new session");
        
        isCalibrating = false;
        isCalibrated = false;
        staticOffset = Vector3.zero;
        calibrationSamples.Clear();
        currentSampleCount = 0;
        lastCalibratedGyro = Vector3.zero;
        
        OnCalibrationStateChanged?.Invoke(false);
    }
    
    // Public getters
    public bool IsCalibrating() => isCalibrating;
    public bool IsCalibrated() => isCalibrated;
    public Vector3 GetStaticOffset() => staticOffset;
    public int GetCurrentSampleCount() => currentSampleCount;
    public int GetRequiredSamples() => requiredSamples;
    public float GetCalibrationProgress() => (float)currentSampleCount / requiredSamples;
    
    // Force completion for testing
    [ContextMenu("Force Complete Calibration")]
    public void ForceCompleteCalibration()
    {
        if (isCalibrating && calibrationSamples.Count > 10)
        {
            requiredSamples = calibrationSamples.Count;
            CompleteCalibration();
        }
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[GravityCalibrator] {message}");
        }
    }
    
    // Debug GUI
    void OnGUI()
    {
        if (!enableDebugLogs) return;
        
        GUILayout.BeginArea(new Rect(10, 300, 350, 150));
        GUILayout.Box("Gravity Calibrator (Static Zero)");
        
        if (isCalibrating)
        {
            GUI.color = Color.yellow;
            GUILayout.Label($"CALIBRATING: {currentSampleCount}/{requiredSamples}");
            GUILayout.Label($"Progress: {GetCalibrationProgress() * 100:F1}%");
            GUILayout.Label($"Current magnitude: {currentRawGyro.magnitude:F2}°");
            GUILayout.Label($"Stability threshold: {stabilityThreshold}°");
            GUI.color = Color.white;
            
            if (GUILayout.Button("Force Complete"))
            {
                ForceCompleteCalibration();
            }
        }
        else if (isCalibrated)
        {
            GUI.color = Color.green;
            GUILayout.Label("CALIBRATED ✓");
            GUI.color = Color.white;
            GUILayout.Label($"Static offset: {staticOffset}");
            GUILayout.Label($"Magnitude: {staticOffset.magnitude:F3}°");
            GUILayout.Label($"Calibrated gyro: {lastCalibratedGyro}");
            
            if (GUILayout.Button("Reset"))
            {
                ResetCalibration();
            }
        }
        else
        {
            GUI.color = Color.gray;
            GUILayout.Label("NOT CALIBRATED");
            GUI.color = Color.white;
            GUILayout.Label("Call StartCalibration() to begin");
            
            if (GUILayout.Button("Start Calibration"))
            {
                StartCalibration();
            }
        }
        
        GUILayout.EndArea();
    }
}