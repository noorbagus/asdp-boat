using UnityEngine;
using System.Collections.Generic;

public class PeakDetectionGyro : MonoBehaviour
{
    [Header("Peak Detection")]
    public float peakThreshold = 8f;        // Minimum untuk dianggap peak
    public float peakDecayTime = 1f;        // Berapa lama peak disimpan
    public float directionChangeThreshold = 5f; // Minimum untuk deteksi arah balik
    
    [Header("Movement Settings")]
    public float movementTimeout = 2f;      // Reset jika tidak ada gerakan
    public float minimumSwingRange = 10f;   // Range minimum untuk dianggap swing
    
    [Header("Debug")]
    public bool enableDebugLogs = true;
    
    // Peak tracking
    private struct PeakData
    {
        public float angle;
        public float timestamp;
        public bool isLeft;
        
        public PeakData(float angle, float time, bool left)
        {
            this.angle = angle;
            this.timestamp = time;
            this.isLeft = left;
        }
    }
    
    private PeakData? currentPeak = null;
    private PeakData? lastProcessedPeak = null;
    
    // State tracking
    private float lastAngle = 0f;
    private float lastSignificantAngle = 0f;
    private float lastMovementTime = 0f;
    private bool isInMovement = false;
    
    // Events
    public System.Action<string> OnPaddleDetected;
    public System.Action<string> OnSwingCompleted;
    
    void Update()
    {
        CleanupExpiredPeaks();
    }
    
    public void ProcessGyroAngle(float angle)
    {
        float currentTime = Time.time;
        bool hasMovement = Mathf.Abs(angle) > 3f; // Threshold untuk deteksi gerakan
        
        if (hasMovement)
        {
            lastMovementTime = currentTime;
            isInMovement = true;
            
            // Deteksi peak baru
            DetectPeak(angle, currentTime);
            
            // Deteksi direction change
            DetectDirectionChange(angle, currentTime);
        }
        else if (isInMovement && (currentTime - lastMovementTime > movementTimeout))
        {
            // Movement selesai - reset state
            ResetMovementState();
        }
        
        lastAngle = angle;
        
        // Update significant angle jika ada gerakan besar
        if (Mathf.Abs(angle) > directionChangeThreshold)
        {
            lastSignificantAngle = angle;
        }
    }
    
    private void DetectPeak(float angle, float currentTime)
    {
        float magnitude = Mathf.Abs(angle);
        
        // Cek apakah ini peak baru
        if (magnitude > peakThreshold)
        {
            bool isLeft = angle > 0;
            
            // Jika tidak ada peak atau peak baru lebih besar
            if (!currentPeak.HasValue || magnitude > Mathf.Abs(currentPeak.Value.angle))
            {
                currentPeak = new PeakData(angle, currentTime, isLeft);
                DebugLog($"New PEAK: {angle:F1}° ({(isLeft ? "LEFT" : "RIGHT")})");
            }
        }
    }
    
    private void DetectDirectionChange(float angle, float currentTime)
    {
        if (!currentPeak.HasValue) return;
        
        PeakData peak = currentPeak.Value;
        bool currentIsLeft = angle > 0;
        bool peakWasLeft = peak.isLeft;
        
        // Deteksi direction change signifikan
        if (currentIsLeft != peakWasLeft && Mathf.Abs(angle) > directionChangeThreshold)
        {
            // Hitung swing range
            float swingRange = Mathf.Abs(peak.angle - angle);
            
            if (swingRange > minimumSwingRange)
            {
                // Swing completed!
                string swingDirection = peakWasLeft ? "LEFT" : "RIGHT";
                ProcessSwingCompletion(peak, angle, swingRange);
                
                // Mark peak sebagai processed
                lastProcessedPeak = peak;
                currentPeak = null; // Reset untuk deteksi swing berikutnya
                
                DebugLog($"SWING COMPLETED: {swingDirection} (range: {swingRange:F1}°)");
            }
        }
    }
    
    private void ProcessSwingCompletion(PeakData peak, float currentAngle, float swingRange)
    {
        string direction = peak.isLeft ? "LEFT" : "RIGHT";
        
        // Trigger paddle action
        OnPaddleDetected?.Invoke(direction);
        
        // Broadcast ke boat controller
        BoatController boat = FindObjectOfType<BoatController>();
        if (boat != null)
        {
            if (peak.isLeft)
                boat.PaddleLeft();
            else
                boat.PaddleRight();
        }
        
        // Broadcast swing completion
        OnSwingCompleted?.Invoke($"{direction}_{swingRange:F1}");
        
        DebugLog($"🏓 PADDLE {direction}: Peak {peak.angle:F1}° → Current {currentAngle:F1}° (Range: {swingRange:F1}°)");
    }
    
    private void CleanupExpiredPeaks()
    {
        float currentTime = Time.time;
        
        // Hapus peak yang expired
        if (currentPeak.HasValue && (currentTime - currentPeak.Value.timestamp > peakDecayTime))
        {
            DebugLog($"Peak expired: {currentPeak.Value.angle:F1}°");
            currentPeak = null;
        }
    }
    
    private void ResetMovementState()
    {
        isInMovement = false;
        currentPeak = null;
        DebugLog("Movement state reset");
    }
    
    // Public API
    public bool HasActivePeak() => currentPeak.HasValue;
    public float GetCurrentPeakAngle() => currentPeak?.angle ?? 0f;
    public bool IsInActiveMovement() => isInMovement;
    
    // Force trigger untuk testing
    public void ForceTriggerPaddle(string direction)
    {
        OnPaddleDetected?.Invoke(direction);
        DebugLog($"FORCED paddle: {direction}");
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PeakDetection] {message}");
        }
    }
    
    // Debug display
    void OnGUI()
    {
        if (!enableDebugLogs) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Current Angle: {lastAngle:F1}°");
        GUILayout.Label($"In Movement: {isInMovement}");
        
        if (currentPeak.HasValue)
        {
            PeakData peak = currentPeak.Value;
            GUILayout.Label($"Active Peak: {peak.angle:F1}° ({(peak.isLeft ? "LEFT" : "RIGHT")})");
            GUILayout.Label($"Peak Age: {Time.time - peak.timestamp:F1}s");
        }
        else
        {
            GUILayout.Label("No Active Peak");
        }
        
        if (lastProcessedPeak.HasValue)
        {
            PeakData last = lastProcessedPeak.Value;
            GUILayout.Label($"Last Swing: {last.angle:F1}° ({(last.isLeft ? "LEFT" : "RIGHT")})");
        }
        
        GUILayout.EndArea();
    }
}