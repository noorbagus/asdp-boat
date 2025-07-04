using UnityEngine;

public class GestureDetector : MonoBehaviour
{
    [Header("Gesture Thresholds")]
    [SerializeField] private int restartGameThreshold = 8000; // Sharp downward motion
    [SerializeField] private float gestureCooldown = 2f; // Prevent spam
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // Events
    public System.Action<string> OnGestureDetected;
    
    // State tracking
    private int currentAccelY = 0;
    private float lastGestureTime = 0f;
    
    private void Start()
    {
        DebugLog("GestureDetector initialized - AccelY threshold for restart");
    }
    
    /// <summary>
    /// Process accelerometer Y data for gesture detection
    /// </summary>
    public void ProcessAccelData(int accelY)
    {
        currentAccelY = accelY;
        
        // Check gesture cooldown
        if (Time.time - lastGestureTime < gestureCooldown)
        {
            return;
        }
        
        // Detect restart gesture (sharp downward motion)
        if (accelY < -restartGameThreshold)
        {
            DetectRestartGesture();
        }
    }
    
    private void DetectRestartGesture()
    {
        lastGestureTime = Time.time;
        
        DebugLog($"RESTART gesture detected! AccelY: {currentAccelY}");
        
        // Notify listeners
        OnGestureDetected?.Invoke("RESTART_GAME");
        
        // Direct GameManager call as backup
        ExecuteRestart();
    }
    
    private void ExecuteRestart()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.RestartLevel();
            DebugLog("Game restarted via GameManager");
        }
        else
        {
            // Fallback to scene reload
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            DebugLog("Game restarted via scene reload");
        }
    }
    
    // Public API
    public int GetCurrentAccelY() => currentAccelY;
    public bool IsInCooldown() => Time.time - lastGestureTime < gestureCooldown;
    public float GetTimeSinceLastGesture() => Time.time - lastGestureTime;
    
    // Force gesture for testing
    [ContextMenu("Force Restart Gesture")]
    public void ForceRestartGesture()
    {
        DetectRestartGesture();
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[GestureDetector] {message}");
        }
    }
    
    // Debug GUI
    void OnGUI()
    {
        if (!enableDebugLogs) return;
        
        GUILayout.BeginArea(new Rect(10, 150, 300, 120));
        GUILayout.Box("Gesture Detector");
        
        GUILayout.Label($"AccelY: {currentAccelY}");
        GUILayout.Label($"Restart Threshold: {-restartGameThreshold}");
        
        // Cooldown indicator
        if (IsInCooldown())
        {
            GUI.color = Color.red;
            float remaining = gestureCooldown - GetTimeSinceLastGesture();
            GUILayout.Label($"Cooldown: {remaining:F1}s");
            GUI.color = Color.white;
        }
        else
        {
            GUI.color = Color.green;
            GUILayout.Label("Ready for gesture");
            GUI.color = Color.white;
        }
        
        // Visual threshold indicator
        if (currentAccelY < -restartGameThreshold)
        {
            GUI.color = Color.yellow;
            GUILayout.Label("RESTART GESTURE ACTIVE!");
            GUI.color = Color.white;
        }
        
        // Manual test button
        if (GUILayout.Button("Test Restart"))
        {
            ForceRestartGesture();
        }
        
        GUILayout.EndArea();
    }
}