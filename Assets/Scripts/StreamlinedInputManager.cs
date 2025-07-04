using UnityEngine;

public class StreamlinedInputManager : MonoBehaviour
{
    [Header("Bluetooth Connection")]
    public BluetoothManager bluetoothManager;
    public GravityCalibrator gravityCalibrator;
    
    [Header("Gyro Detection Systems")]
    public PeakDetectionGyro peakDetector;
    public SimpleGyroDetector simpleDetector;
    [SerializeField] private bool usePeakDetection = true;
    
    [Header("Game Controllers")]
    public BoatController boatController;
    public PaddleIKController paddleIKController;
    
    [Header("Gesture Detection")]
    public GestureDetector gestureDetector;
    
    [Header("Debug")]
    public bool enableDebugLogs = true;
    
    // Connection state
    private bool isBluetoothConnected = false;
    private bool isCalibrated = false;
    
    void Start()
    {
        InitializeComponents();
        RegisterEventHandlers();
        
        DebugLog("StreamlinedInputManager started with dual detection system");
    }
    
    void InitializeComponents()
    {
        // Auto-find components if not assigned
        if (bluetoothManager == null)
            bluetoothManager = FindObjectOfType<BluetoothManager>();
        if (gravityCalibrator == null)
            gravityCalibrator = FindObjectOfType<GravityCalibrator>();
        if (boatController == null)
            boatController = FindObjectOfType<BoatController>();
        if (paddleIKController == null)
            paddleIKController = FindObjectOfType<PaddleIKController>();
        if (gestureDetector == null)
            gestureDetector = FindObjectOfType<GestureDetector>();
        
        // Configure detection systems
        ConfigureDetectionSystems();
    }
    
    void ConfigureDetectionSystems()
    {
        if (usePeakDetection && peakDetector != null)
        {
            DebugLog("Configuring PEAK DETECTION mode");
            
            // Peak detection handles boat control
            peakDetector.OnPaddleDetected += HandlePeakPaddleDetected;
            
            // Simple detector only for visual/analysis (no boat control)
            if (simpleDetector != null)
            {
                simpleDetector.enableBoatControl = false;
                simpleDetector.OnTurnDetected += HandleVisualTurn; // Visual feedback only
            }
        }
        else if (simpleDetector != null)
        {
            DebugLog("Configuring SIMPLE DETECTION mode");
            
            // Simple detector handles boat control
            simpleDetector.enableBoatControl = true;
            simpleDetector.OnTurnDetected += HandleSimpleTurn;
        }
    }
    
    void RegisterEventHandlers()
    {
        // Bluetooth events
        if (bluetoothManager != null)
        {
            bluetoothManager.OnGyroDataReceived += OnBluetoothGyroReceived;
            bluetoothManager.OnConnectionChanged += OnBluetoothConnectionChanged;
        }
        
        // Calibration events
        if (gravityCalibrator != null)
        {
            // Check if calibration events exist, otherwise monitor IsCalibrated()
            DebugLog("Gravity calibrator found - monitoring calibration status");
        }
        
        // Gesture events
        if (gestureDetector != null)
        {
            gestureDetector.OnGestureDetected += HandleGestureDetected;
        }
    }
    
    void OnBluetoothGyroReceived(Vector3 gyroData)
    {
        // Check calibration status
        if (gravityCalibrator != null && !gravityCalibrator.IsCalibrated())
        {
            isCalibrated = false;
            return;
        }
        
        isCalibrated = true;
        
        // Apply calibration if available
        Vector3 calibratedGyro = gyroData;
        if (gravityCalibrator != null && gravityCalibrator.IsCalibrated())
        {
            // Use the actual method name from your GravityCalibrator
            Vector3 offset = gravityCalibrator.GetGyroOffset();
            calibratedGyro = gyroData - offset;
        }
        
        // Feed to detection systems based on mode
        if (usePeakDetection && peakDetector != null)
        {
            // Primary: Peak detection for boat control
            peakDetector.ProcessGyroAngle(calibratedGyro.x);
            
            // Secondary: Simple detector for visual only
            if (simpleDetector != null)
            {
                simpleDetector.ProcessCalibratedGyro(calibratedGyro);
            }
        }
        else if (simpleDetector != null)
        {
            // Fallback: Simple detection for everything
            simpleDetector.ProcessCalibratedGyro(calibratedGyro);
        }
        
        // Visual feedback always active
        UpdatePaddleVisual(calibratedGyro.x);
        
        // Gesture detection
        if (gestureDetector != null)
        {
            gestureDetector.ProcessAccelData(gyroData); // Assuming gesture uses accel
        }
    }
    
    void HandlePeakPaddleDetected(string direction)
    {
        DebugLog($"🏓 PEAK paddle: {direction}");
        
        // Trigger boat movement
        if (boatController != null)
        {
            if (direction == "LEFT")
                boatController.PaddleLeft();
            else if (direction == "RIGHT")
                boatController.PaddleRight();
        }
        
        // Update paddle visual
        if (paddleIKController != null)
        {
            float angle = direction == "LEFT" ? 30f : -30f;
            paddleIKController.SetRawAngle(angle);
        }
    }
    
    void HandleSimpleTurn(string direction)
    {
        DebugLog($"🚤 SIMPLE turn: {direction}");
        
        // Trigger boat movement (when simple detection is primary)
        if (!usePeakDetection && boatController != null)
        {
            if (direction == "LEFT")
                boatController.PaddleLeft();
            else if (direction == "RIGHT")
                boatController.PaddleRight();
        }
    }
    
    void HandleVisualTurn(string direction)
    {
        // Only visual feedback when peak detection is primary
        DebugLog($"👁️ VISUAL turn: {direction}");
        
        if (paddleIKController != null)
        {
            float angle = direction == "LEFT" ? 20f : -20f;
            paddleIKController.SetRawAngle(angle);
        }
    }
    
    void UpdatePaddleVisual(float gyroAngle)
    {
        if (paddleIKController != null)
        {
            // Continuous visual update based on gyro angle
            paddleIKController.SetRawAngle(-gyroAngle); // Negate for correct direction
        }
    }
    
    void HandleGestureDetected(string gestureType)
    {
        DebugLog($"✋ Gesture: {gestureType}");
        
        switch (gestureType)
        {
            case "RESTART_GAME":
                // Handle restart gesture
                GameManager gameManager = FindObjectOfType<GameManager>();
                if (gameManager != null)
                {
                    gameManager.RestartLevel();
                }
                break;
                
            case "START_GAME":
                // Handle start gesture
                break;
        }
    }
    
    void OnBluetoothConnectionChanged(bool connected)
    {
        isBluetoothConnected = connected;
        DebugLog($"Bluetooth: {(connected ? "Connected" : "Disconnected")}");
        
        if (!connected)
        {
            isCalibrated = false;
        }
    }
    
    void OnCalibrationCompleted(Vector3 offset)
    {
        isCalibrated = true;
        DebugLog($"Calibration completed: {offset}");
    }
    
    // Public API for runtime switching
    public void SwitchToDetectionMode(bool usePeak)
    {
        usePeakDetection = usePeak;
        ConfigureDetectionSystems();
        DebugLog($"Switched to {(usePeak ? "PEAK" : "SIMPLE")} detection");
    }
    
    public bool IsConnected() => isBluetoothConnected;
    public bool IsCalibrated() => isCalibrated;
    public bool IsUsingPeakDetection() => usePeakDetection;
    
    // Manual trigger for testing
    public void TestLeftPaddle()
    {
        if (usePeakDetection)
            HandlePeakPaddleDetected("LEFT");
        else
            HandleSimpleTurn("LEFT");
    }
    
    public void TestRightPaddle()
    {
        if (usePeakDetection)
            HandlePeakPaddleDetected("RIGHT");
        else
            HandleSimpleTurn("RIGHT");
    }
    
    void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[StreamlinedInput] {message}");
        }
    }
    
    void OnGUI()
    {
        if (!enableDebugLogs) return;
        
        GUILayout.BeginArea(new Rect(10, Screen.height - 150, 350, 150));
        
        // Connection status
        GUI.color = isBluetoothConnected ? Color.green : Color.red;
        GUILayout.Label($"Bluetooth: {(isBluetoothConnected ? "✓ Connected" : "✗ Disconnected")}");
        
        // Calibration status
        GUI.color = isCalibrated ? Color.green : Color.yellow;
        GUILayout.Label($"Calibration: {(isCalibrated ? "✓ Ready" : "⚠ Waiting")}");
        
        GUI.color = Color.white;
        
        // Detection mode
        GUI.color = usePeakDetection ? Color.cyan : Color.white;
        GUILayout.Label($"Detection: {(usePeakDetection ? "PEAK" : "SIMPLE")}");
        GUI.color = Color.white;
        
        // Toggle button
        if (GUILayout.Button("Switch Detection Mode"))
        {
            SwitchToDetectionMode(!usePeakDetection);
        }
        
        // Test buttons
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Test L")) TestLeftPaddle();
        if (GUILayout.Button("Test R")) TestRightPaddle();
        GUILayout.EndHorizontal();
        
        GUILayout.EndArea();
    }
}