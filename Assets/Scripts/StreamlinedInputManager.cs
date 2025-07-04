using UnityEngine;
using System.Collections;

public class StreamlinedInputManager : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private BluetoothManager bluetoothManager;
    [SerializeField] private GravityCalibrator calibrator;
    [SerializeField] private SimpleGyroDetector gyroDetector;
    [SerializeField] private GestureDetector gestureDetector;
    [SerializeField] private CalibrationUI calibrationUI;
    
    [Header("Game Controllers")]
    [SerializeField] private BoatController boatController;
    [SerializeField] private PaddleIKController paddleController;
    
    [Header("Auto Start")]
    [SerializeField] private bool autoStartCalibration = true;
    [SerializeField] private float startDelay = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // State tracking
    private bool isSystemReady = false;
    private bool isCalibrationComplete = false;
    private SimpleGyroDetector.BoatState lastBoatState = SimpleGyroDetector.BoatState.Idle;
    
    private void Start()
    {
        DebugLog("StreamlinedInputManager initializing...");
        
        InitializeComponents();
        SetupEventListeners();
        
        if (autoStartCalibration)
        {
            StartCoroutine(StartCalibrationAfterDelay());
        }
    }
    
    private void InitializeComponents()
    {
        // Auto-find components if not assigned
        if (bluetoothManager == null) bluetoothManager = FindObjectOfType<BluetoothManager>();
        if (calibrator == null) calibrator = FindObjectOfType<GravityCalibrator>();
        if (gyroDetector == null) gyroDetector = FindObjectOfType<SimpleGyroDetector>();
        if (gestureDetector == null) gestureDetector = FindObjectOfType<GestureDetector>();
        if (calibrationUI == null) calibrationUI = FindObjectOfType<CalibrationUI>();
        if (boatController == null) boatController = FindObjectOfType<BoatController>();
        if (paddleController == null) paddleController = FindObjectOfType<PaddleIKController>();
        
        DebugLog($"Components found - Bluetooth:{bluetoothManager != null}, Calibrator:{calibrator != null}, " +
                $"Detector:{gyroDetector != null}, UI:{calibrationUI != null}, Boat:{boatController != null}");
    }
    
    private void SetupEventListeners()
    {
        // BluetoothManager events
        if (bluetoothManager != null)
        {
            bluetoothManager.OnGyroDataReceived += OnBluetoothGyroReceived;
            bluetoothManager.OnConnectionChanged += OnBluetoothConnectionChanged;
        }
        
        // Calibrator events
        if (calibrator != null)
        {
            calibrator.OnCalibrationComplete += OnCalibrationComplete;
        }
        
        // Gyro detector events
        if (gyroDetector != null)
        {
            gyroDetector.OnStateChanged += OnBoatStateChanged;
        }
        
        // Gesture detector events
        if (gestureDetector != null)
        {
            gestureDetector.OnGestureDetected += OnGestureDetected;
        }
    }
    
    private IEnumerator StartCalibrationAfterDelay()
    {
        yield return new WaitForSeconds(startDelay);
        
        if (calibrator != null && !calibrator.IsCalibrated())
        {
            DebugLog("Auto-starting calibration");
            calibrator.StartCalibration();
        }
    }
    
    private void OnBluetoothGyroReceived(Vector3 filteredVelocity)
    {
        // Get raw gyro angles from BluetoothManager
        if (bluetoothManager != null && calibrator != null)
        {
            Vector3 rawGyro = bluetoothManager.GetGyroAngles();
            
            // Pass raw gyro to calibrator
            calibrator.ProcessGyroData(rawGyro);
            
            // If calibrated, pass calibrated data to detector
            if (calibrator.IsCalibrated() && gyroDetector != null)
            {
                Vector3 calibratedGyro = calibrator.GetCalibratedGyro();
                gyroDetector.ProcessCalibratedGyro(calibratedGyro);
                
                // Update paddle visual with Z-axis
                if (paddleController != null)
                {
                    paddleController.SetRawAngle(-calibratedGyro.z); // Negate for correct direction
                }
            }
            
            // Pass accelerometer Y to gesture detector
            if (gestureDetector != null)
            {
                Vector3Int rawAccel = bluetoothManager.GetRawAccel();
                gestureDetector.ProcessAccelData(rawAccel.y);
            }
        }
    }
    
    private void OnBluetoothConnectionChanged(bool connected)
    {
        DebugLog($"Bluetooth connection: {(connected ? "Connected" : "Disconnected")}");
        
        if (!connected)
        {
            isSystemReady = false;
        }
    }
    
    private void OnCalibrationComplete(Vector3 offset)
    {
        isCalibrationComplete = true;
        isSystemReady = true;
        
        DebugLog($"Calibration complete - Static offset: {offset}");
        DebugLog("System ready for input processing");
    }
    
    private void OnBoatStateChanged(SimpleGyroDetector.BoatState newState, float confidence)
    {
        if (!isSystemReady) return;
        
        lastBoatState = newState;
        
        DebugLog($"Boat state: {newState} (confidence: {confidence:F2})");
        
        // Route to boat controller
        if (boatController != null)
        {
            switch (newState)
            {
                case SimpleGyroDetector.BoatState.Forward:
                    boatController.AddForwardThrust(confidence);
                    break;
                    
                case SimpleGyroDetector.BoatState.TurnLeft:
                    boatController.PaddleRight(); // Right paddle for left turn
                    break;
                    
                case SimpleGyroDetector.BoatState.TurnRight:
                    boatController.PaddleLeft(); // Left paddle for right turn
                    break;
                    
                case SimpleGyroDetector.BoatState.Idle:
                    // No action needed for idle
                    break;
            }
        }
    }
    
    private void OnGestureDetected(string gestureType)
    {
        DebugLog($"Gesture detected: {gestureType}");
        
        switch (gestureType)
        {
            case "RESTART_GAME":
                RestartGame();
                break;
                
            default:
                DebugLog($"Unknown gesture: {gestureType}");
                break;
        }
    }
    
    private void RestartGame()
    {
        DebugLog("Restarting game via gesture");
        
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.RestartLevel();
        }
        else
        {
            // Fallback to scene reload
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
    
    // Public API
    public bool IsSystemReady() => isSystemReady;
    public bool IsCalibrationComplete() => isCalibrationComplete;
    public SimpleGyroDetector.BoatState GetCurrentBoatState() => lastBoatState;
    
    public void StartCalibration()
    {
        if (calibrator != null)
        {
            DebugLog("Manual calibration start");
            calibrator.StartCalibration();
        }
    }
    
    public void ResetSystem()
    {
        DebugLog("Resetting system for new session");
        
        isSystemReady = false;
        isCalibrationComplete = false;
        lastBoatState = SimpleGyroDetector.BoatState.Idle;
        
        if (calibrator != null)
        {
            calibrator.ResetCalibration();
        }
        
        if (autoStartCalibration)
        {
            StartCoroutine(StartCalibrationAfterDelay());
        }
    }
    
    // Force states for testing
    public void ForceBoatState(int stateIndex)
    {
        if (gyroDetector != null && stateIndex >= 0 && stateIndex < 4)
        {
            SimpleGyroDetector.BoatState state = (SimpleGyroDetector.BoatState)stateIndex;
            gyroDetector.ForceState(state);
            DebugLog($"Forced boat state to: {state}");
        }
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[StreamlinedInputManager] {message}");
        }
    }
    
    private void OnDestroy()
    {
        // Cleanup event listeners
        if (bluetoothManager != null)
        {
            bluetoothManager.OnGyroDataReceived -= OnBluetoothGyroReceived;
            bluetoothManager.OnConnectionChanged -= OnBluetoothConnectionChanged;
        }
        
        if (calibrator != null)
        {
            calibrator.OnCalibrationComplete -= OnCalibrationComplete;
        }
        
        if (gyroDetector != null)
        {
            gyroDetector.OnStateChanged -= OnBoatStateChanged;
        }
        
        if (gestureDetector != null)
        {
            gestureDetector.OnGestureDetected -= OnGestureDetected;
        }
    }
    
    // Debug GUI
    void OnGUI()
    {
        if (!enableDebugLogs) return;
        
        GUILayout.BeginArea(new Rect(10, 670, 350, 150));
        GUILayout.Box("Streamlined Input Manager");
        
        // System status
        GUI.color = isSystemReady ? Color.green : Color.red;
        GUILayout.Label($"System: {(isSystemReady ? "READY" : "NOT READY")}");
        GUI.color = Color.white;
        
        GUILayout.Label($"Calibration: {(isCalibrationComplete ? "✓" : "✗")}");
        GUILayout.Label($"Boat State: {lastBoatState}");
        
        // Manual controls
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Start Cal") && calibrator != null)
        {
            calibrator.StartCalibration();
        }
        if (GUILayout.Button("Reset"))
        {
            ResetSystem();
        }
        GUILayout.EndHorizontal();
        
        // Force state buttons
        GUILayout.Label("Force States:");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Idle")) ForceBoatState(0);
        if (GUILayout.Button("Fwd")) ForceBoatState(1);
        if (GUILayout.Button("L")) ForceBoatState(2);
        if (GUILayout.Button("R")) ForceBoatState(3);
        GUILayout.EndHorizontal();
        
        GUILayout.EndArea();
    }
}