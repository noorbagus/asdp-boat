using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GyroCalibrator : MonoBehaviour
{
    [Header("Calibration Settings")]
    public float calibrationDuration = 3f;
    public int minCalibrationSamples = 15;
    public float stabilityThreshold = 2f;
    
    [Header("Debug")]
    public bool enableDebugLogs = true;
    
    // Calibration state
    private enum CalibrationState { Idle, Active, Completed, Failed }
    private CalibrationState calibrationState = CalibrationState.Idle;
    private Vector3 gyroOffset = Vector3.zero;
    private List<Vector3> calibrationSamples = new List<Vector3>();
    private float calibrationTimer = 0f;
    
    // External references
    private MonoBehaviour bluetoothManager;
    
    // Events
    public System.Action<bool> OnCalibrationStateChanged;
    public System.Action<Vector3> OnCalibrationCompleted;
    
    void Start()
    {
        FindBluetoothManager();
    }
    
    void FindBluetoothManager()
    {
        MonoBehaviour[] allComponents = FindObjectsOfType<MonoBehaviour>();
        foreach (var comp in allComponents)
        {
            if (comp.GetType().Name == "BluetoothManager")
            {
                bluetoothManager = comp;
                break;
            }
        }
        
        DebugLog($"BluetoothManager found: {bluetoothManager != null}");
    }
    
    void Update()
    {
        if (calibrationState == CalibrationState.Active)
        {
            UpdateCalibration();
        }
    }
    
    public void StartCalibration()
    {
        DebugLog("=== STARTING CALIBRATION ===");
        DebugLog($"Duration: {calibrationDuration}s, Min samples: {minCalibrationSamples}, Threshold: {stabilityThreshold}");
        
        calibrationState = CalibrationState.Active;
        calibrationTimer = calibrationDuration;
        calibrationSamples.Clear();
        gyroOffset = Vector3.zero;
        
        OnCalibrationStateChanged?.Invoke(true);
    }
    
    void UpdateCalibration()
    {
        calibrationTimer -= Time.deltaTime;
        
        if (bluetoothManager != null)
        {
            Vector3 currentReading = GetBluetoothGyroData();
            float gyroMagnitude = currentReading.magnitude;
            bool isStable = gyroMagnitude < stabilityThreshold;
            
            if (isStable)
            {
                calibrationSamples.Add(currentReading);
                DebugLog($"✓ Sample {calibrationSamples.Count}: Mag={gyroMagnitude:F3}");
            }
            else
            {
                DebugLog($"✗ Rejected: Mag={gyroMagnitude:F3} > {stabilityThreshold}");
            }
        }
        
        // Check completion conditions
        bool timeUp = calibrationTimer <= 0f;
        bool enoughSamples = calibrationSamples.Count >= minCalibrationSamples;
        
        if (timeUp || enoughSamples)
        {
            CompleteCalibration();
        }
    }
    
    void CompleteCalibration()
    {
        DebugLog($"=== CALIBRATION COMPLETE ===");
        DebugLog($"Samples collected: {calibrationSamples.Count}/{minCalibrationSamples}");
        
        if (calibrationSamples.Count < minCalibrationSamples)
        {
            calibrationState = CalibrationState.Failed;
            DebugLog($"❌ FAILED - Not enough samples ({calibrationSamples.Count}/{minCalibrationSamples}). Retrying...");
            
            OnCalibrationStateChanged?.Invoke(false);
            StartCoroutine(RetryCalibrationAfterDelay(1f));
            return;
        }
        
        // Calculate offset
        Vector3 totalOffset = Vector3.zero;
        float maxMag = 0f, minMag = float.MaxValue;
        
        foreach (Vector3 sample in calibrationSamples)
        {
            totalOffset += sample;
            float mag = sample.magnitude;
            if (mag > maxMag) maxMag = mag;
            if (mag < minMag) minMag = mag;
        }
        
        gyroOffset = totalOffset / calibrationSamples.Count;
        calibrationState = CalibrationState.Completed;
        
        DebugLog($"✅ SUCCESS!");
        DebugLog($"Offset: {gyroOffset} (Mag: {gyroOffset.magnitude:F3})");
        DebugLog($"Range: {minMag:F3} - {maxMag:F3}");
        
        OnCalibrationStateChanged?.Invoke(false);
        OnCalibrationCompleted?.Invoke(gyroOffset);
    }
    
    IEnumerator RetryCalibrationAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (calibrationState == CalibrationState.Failed)
        {
            StartCalibration();
        }
    }
    
    Vector3 GetBluetoothGyroData()
    {
        if (bluetoothManager == null) return Vector3.zero;
        
        try
        {
            var velocityMethod = bluetoothManager.GetType().GetMethod("GetFilteredVelocity");
            if (velocityMethod != null)
            {
                return (Vector3)velocityMethod.Invoke(bluetoothManager, null);
            }
            
            var anglesMethod = bluetoothManager.GetType().GetMethod("GetGyroAngles");
            if (anglesMethod != null)
            {
                return (Vector3)anglesMethod.Invoke(bluetoothManager, null);
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Failed to get gyro data: {e.Message}");
        }
        
        return Vector3.zero;
    }
    
    bool GetBluetoothConnectionStatus()
    {
        if (bluetoothManager == null) return false;
        
        try
        {
            var method = bluetoothManager.GetType().GetMethod("IsConnected");
            if (method != null)
            {
                return (bool)method.Invoke(bluetoothManager, null);
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Connection check failed: {e.Message}");
        }
        
        return false;
    }
    
    // Public API
    public bool IsCalibrated() => calibrationState == CalibrationState.Completed;
    public bool IsCalibrating() => calibrationState == CalibrationState.Active;
    public Vector3 GetGyroOffset() => gyroOffset;
    public float GetCalibrationTimer() => calibrationTimer;
    public int GetCalibrationSampleCount() => calibrationSamples.Count;
    public bool IsConnected() => GetBluetoothConnectionStatus();
    
    public void ForceFinishCalibration()
    {
        if (calibrationState == CalibrationState.Active && calibrationSamples.Count > 0)
        {
            DebugLog("Force finishing calibration");
            CompleteCalibration();
        }
    }
    
    public void ResetCalibration()
    {
        calibrationState = CalibrationState.Idle;
        calibrationSamples.Clear();
        gyroOffset = Vector3.zero;
        DebugLog("Calibration reset");
    }
    
    // Apply calibration to raw gyro data
    public Vector3 ApplyCalibration(Vector3 rawGyro)
    {
        if (!IsCalibrated()) return rawGyro;
        return rawGyro - gyroOffset;
    }
    
    void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[GyroCalibrator] {message}");
        }
    }
}