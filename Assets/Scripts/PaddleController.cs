using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using ArduinoBluetoothAPI;

public class PaddleController : MonoBehaviour
{
    [Header("=== BLUETOOTH CONNECTION ===")]
    [Tooltip("Bluetooth device name to connect to")]
    public string deviceName = "ferizy-paddle";
    [Tooltip("Automatically connect on start")]
    public bool autoConnect = true;
    [Tooltip("Connection timeout in seconds")]
    public float connectionTimeout = 30f;

    [Header("=== CALIBRATION SETTINGS ===")]
    [Tooltip("Use 3-phase calibration (neutral→right→left) vs single-phase")]
    public bool useMultiPointCalibration = true;
    [Tooltip("Number of samples for each calibration point")]
    public int calibrationSamples = 30;
    [Tooltip("Stability threshold during calibration (degrees)")]
    public float calibrationStabilityThreshold = 3f;
    [Tooltip("Time to hold position during calibration")]
    public float calibrationHoldTime = 2f;
    [Tooltip("Enable automatic calibration on start")]
    public bool autoCalibrate = true;
    [Tooltip("Delay before starting auto-calibration")]
    public float autoCalibrationDelay = 2f;

    [Header("=== TILT DETECTION THRESHOLDS ===")]
    [Tooltip("Angle threshold for left tilt detection")]
    public float leftTiltThreshold = -60f;
    [Tooltip("Angle threshold for right tilt detection")]
    public float rightTiltThreshold = -120f;
    [Tooltip("Dead zone around neutral position")]
    public float deadZone = 25f;
    [Tooltip("Minimum angle change to register as tilt")]
    public float minTiltChange = 8f;

    [Header("=== MOVEMENT DETECTION ===")]
    [Tooltip("Minimum velocity to trigger movement (degrees/sec)")]
    public float movementVelocityThreshold = 15f;
    [Tooltip("Minimum angle change to register as movement")]
    public float movementAngleThreshold = 8f;
    [Tooltip("Time window for movement detection")]
    public float movementTimeWindow = 0.3f;
    [Tooltip("Forward swing velocity threshold")]
    public float forwardSwingThreshold = 20f;

    [Header("=== DATA SMOOTHING ===")]
    [Tooltip("Smoothing factor for angle data (0-1)")]
    [Range(0f, 1f)]
    public float angleSmoothingFactor = 0.7f;
    [Tooltip("Smoothing factor for velocity calculation")]
    [Range(0f, 1f)]
    public float velocitySmoothingFactor = 0.5f;
    [Tooltip("Enable data smoothing")]
    public bool enableSmoothing = true;

    [Header("=== STATE TRANSITION ===")]
    [Tooltip("Minimum time to stay in a state before changing")]
    public float stateChangeDelay = 0.3f;
    [Tooltip("Confidence threshold for state changes")]
    [Range(0f, 1f)]
    public float stateConfidenceThreshold = 0.7f;
    [Tooltip("Debounce time for rapid state changes")]
    public float stateDebounceTime = 0.2f;

    [Header("=== PATTERN RECOGNITION ===")]
    [Tooltip("Enable alternating pattern detection")]
    public bool enablePatternDetection = true;
    [Tooltip("Time window for alternating pattern")]
    public float alternatingTimeWindow = 1.5f;
    [Tooltip("Minimum swings for forward pattern")]
    public int minSwingsForForward = 2;

    [Header("=== DEBUG SETTINGS ===")]
    [Tooltip("Show real-time angle values")]
    public bool showAngleDebug = true;
    [Tooltip("Show calibration status")]
    public bool showCalibrationDebug = true;
    [Tooltip("Show state transitions")]
    public bool showStateDebug = true;
    [Tooltip("Log detailed movement data")]
    public bool verboseLogging = false;
    [Tooltip("Enable debug logs")]
    public bool enableDebugLogs = true;

    // Paddle states
    public enum PaddleState
    {
        Idle,
        TiltLeft,
        TiltRight,
        SwingForward,
        Calibrating,
        Disconnected
    }

    // Events
    public Action<PaddleState> OnStateChanged;
    public Action<int, float> OnTiltDetected; // direction (-1,1), intensity
    public Action<float> OnForwardSwing; // velocity
    public Action<bool> OnCalibrationComplete; // success

    // Current state
    private PaddleState currentState = PaddleState.Disconnected;
    private PaddleState previousState = PaddleState.Disconnected;
    private float stateConfidence = 0f;
    private float lastStateChangeTime = 0f;

    // Bluetooth connection
    private BluetoothHelper bluetoothHelper;
    private bool isConnected = false;
    private string lastPacket = "";
    private int packetsReceived = 0;
    private int validPackets = 0;

    // Angle data
    private float currentAngle = 0f;
    private float smoothedAngle = 0f;
    private float previousAngle = 0f;
    private float angleVelocity = 0f;
    private float smoothedVelocity = 0f;

    // Calibration - Fixed 3-phase system
    private bool isCalibrated = false;
    private bool isCalibrating = false;
    private float leftAngle = 0f;
    private float rightAngle = 0f;
    private float neutralAngle = 0f;
    
    // 3-phase calibration data
    private List<float> neutralSamples = new List<float>();
    private List<float> leftSamples = new List<float>();
    private List<float> rightSamples = new List<float>();
    private int currentCalibrationPhase = 0; // 0=neutral, 1=right, 2=left
    private string[] calibrationPhaseNames = { "Neutral", "Right", "Left" };

    // Pattern detection
    private List<SwingData> swingHistory = new List<SwingData>();
    private float lastSignificantMove = 0f;

    private struct SwingData
    {
        public float timestamp;
        public float angle;
        public float velocity;
        public bool isLeftSwing;
    }

    void Start()
    {
        DebugLog("PaddleController starting with fixed 3-phase calibration");
        InitializeBluetooth();
        
        if (autoConnect)
        {
            StartCoroutine(AutoConnectRoutine());
        }
    }

    void Update()
    {
        if (isConnected)
        {
            ProcessPaddleData();
            UpdatePaddleState();
            CleanupSwingHistory();
        }
    }

    #region Bluetooth Connection
    void InitializeBluetooth()
    {
        try
        {
            BluetoothHelper.BLE = false;
            bluetoothHelper = BluetoothHelper.GetInstance(deviceName);
            bluetoothHelper.OnConnected += OnBluetoothConnected;
            bluetoothHelper.OnConnectionFailed += OnBluetoothConnectionFailed;
            bluetoothHelper.OnDataReceived += OnBluetoothDataReceived;
            bluetoothHelper.setTerminatorBasedStream("\n");
            
            DebugLog("Bluetooth initialized");
        }
        catch (Exception ex)
        {
            DebugLog($"Bluetooth init error: {ex.Message}");
        }
    }

    IEnumerator AutoConnectRoutine()
    {
        yield return new WaitForSeconds(1f);
        ConnectToBluetooth();
    }

    public void ConnectToBluetooth()
    {
        if (bluetoothHelper != null && !bluetoothHelper.isConnected())
        {
            bluetoothHelper.Connect();
            DebugLog("Connecting to Bluetooth...");
        }
    }

    void OnBluetoothConnected(BluetoothHelper helper)
    {
        isConnected = true;
        helper.StartListening();
        DebugLog($"✓ Connected to {deviceName}");
        
        if (autoCalibrate && !isCalibrated)
        {
            StartCoroutine(AutoCalibrationRoutine());
        }
        
        UpdatePaddleState();
    }

    void OnBluetoothConnectionFailed(BluetoothHelper helper)
    {
        isConnected = false;
        DebugLog("✗ Connection failed");
        UpdatePaddleState();
    }

    void OnBluetoothDataReceived(BluetoothHelper helper)
    {
        string data = helper.Read().Trim();
        lastPacket = data;
        packetsReceived++;
        
        if (ParseBluetoothData(data))
        {
            validPackets++;
        }
    }

    bool ParseBluetoothData(string data)
    {
        try
        {
            if (data.StartsWith("G:") && data.Contains(",A:"))
            {
                string[] parts = data.Split(',');
                if (parts.Length >= 1)
                {
                    string gyroStr = parts[0].Substring(2);
                    if (float.TryParse(gyroStr, out float gyro))
                    {
                        currentAngle = gyro;
                        return true;
                    }
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
    #endregion

    #region Data Processing
    void ProcessPaddleData()
    {
        if (!isConnected) return;

        // Store previous values
        previousAngle = smoothedAngle;
        
        // Apply smoothing
        if (enableSmoothing)
        {
            smoothedAngle = Mathf.Lerp(smoothedAngle, currentAngle, angleSmoothingFactor);
        }
        else
        {
            smoothedAngle = currentAngle;
        }

        // Calculate velocity
        if (Time.deltaTime > 0)
        {
            float rawVelocity = (smoothedAngle - previousAngle) / Time.deltaTime;
            angleVelocity = enableSmoothing ? 
                Mathf.Lerp(angleVelocity, rawVelocity, velocitySmoothingFactor) : 
                rawVelocity;
        }

        // Smooth velocity
        smoothedVelocity = Mathf.Lerp(smoothedVelocity, angleVelocity, velocitySmoothingFactor);

        // Record significant movements
        if (Mathf.Abs(angleVelocity) > movementVelocityThreshold)
        {
            RecordSwingData();
            lastSignificantMove = Time.time;
        }

        if (verboseLogging)
        {
            DebugLog($"Angle: {smoothedAngle:F1}°, Velocity: {angleVelocity:F1}°/s");
        }
    }

    void RecordSwingData()
    {
        SwingData swing = new SwingData
        {
            timestamp = Time.time,
            angle = smoothedAngle,
            velocity = angleVelocity,
            isLeftSwing = angleVelocity > 0
        };
        
        swingHistory.Add(swing);
    }

    void CleanupSwingHistory()
    {
        float currentTime = Time.time;
        swingHistory.RemoveAll(s => currentTime - s.timestamp > alternatingTimeWindow);
    }
    #endregion

    #region State Management
    void UpdatePaddleState()
    {
        PaddleState newState = DetermineCurrentState();
        
        // Apply state change delay
        if (newState != currentState && Time.time - lastStateChangeTime < stateChangeDelay)
        {
            return;
        }

        if (newState != currentState)
        {
            previousState = currentState;
            currentState = newState;
            lastStateChangeTime = Time.time;
            stateConfidence = CalculateStateConfidence();
            
            if (showStateDebug)
            {
                DebugLog($"State: {previousState} → {currentState} (confidence: {stateConfidence:F2})");
            }
            
            OnStateChanged?.Invoke(currentState);
            
            // Trigger specific actions
            HandleStateActions();
        }
    }

    PaddleState DetermineCurrentState()
    {
        if (!isConnected) return PaddleState.Disconnected;
        if (isCalibrating) return PaddleState.Calibrating;
        if (!isCalibrated) return PaddleState.Idle;

        // Check for forward swing pattern
        if (enablePatternDetection && IsAlternatingPattern())
        {
            return PaddleState.SwingForward;
        }

        // Check for tilts relative to neutral
        float angleFromNeutral = smoothedAngle - neutralAngle;
        
        if (Mathf.Abs(angleFromNeutral) < deadZone)
        {
            return PaddleState.Idle;
        }

        // Check tilt thresholds - FIXED: use relative to neutral
        if (angleFromNeutral < (leftAngle - neutralAngle) * 0.7f) // 70% of full left range
        {
            return PaddleState.TiltLeft;
        }
        else if (angleFromNeutral > (rightAngle - neutralAngle) * 0.7f) // 70% of full right range
        {
            return PaddleState.TiltRight;
        }

        return PaddleState.Idle;
    }

    bool IsAlternatingPattern()
    {
        if (swingHistory.Count < minSwingsForForward) return false;

        // Check if recent swings alternate direction
        bool hasAlternating = false;
        for (int i = 1; i < swingHistory.Count; i++)
        {
            if (swingHistory[i].isLeftSwing != swingHistory[i-1].isLeftSwing)
            {
                hasAlternating = true;
                break;
            }
        }

        return hasAlternating && Mathf.Abs(smoothedVelocity) > forwardSwingThreshold;
    }

    float CalculateStateConfidence()
    {
        switch (currentState)
        {
            case PaddleState.TiltLeft:
            case PaddleState.TiltRight:
                float angleFromNeutral = Mathf.Abs(smoothedAngle - neutralAngle);
                return Mathf.Clamp01(angleFromNeutral / 45f);
                
            case PaddleState.SwingForward:
                return Mathf.Clamp01(Mathf.Abs(smoothedVelocity) / forwardSwingThreshold);
                
            case PaddleState.Idle:
                return 1f - Mathf.Clamp01(Mathf.Abs(angleVelocity) / movementVelocityThreshold);
                
            default:
                return 1f;
        }
    }

    void HandleStateActions()
    {
        switch (currentState)
        {
            case PaddleState.TiltLeft:
                OnTiltDetected?.Invoke(-1, stateConfidence);
                break;
                
            case PaddleState.TiltRight:
                OnTiltDetected?.Invoke(1, stateConfidence);
                break;
                
            case PaddleState.SwingForward:
                OnForwardSwing?.Invoke(smoothedVelocity);
                break;
        }
    }
    #endregion

    #region Fixed 3-Phase Calibration
    IEnumerator AutoCalibrationRoutine()
    {
        yield return new WaitForSeconds(autoCalibrationDelay);
        
        if (useMultiPointCalibration)
        {
            StartThreePhaseCalibration();
        }
        else
        {
            StartSinglePhaseCalibration();
        }
    }

    public void StartThreePhaseCalibration()
    {
        if (isCalibrating) return;
        
        DebugLog("Starting 3-phase calibration: Neutral → Right → Left");
        StartCoroutine(ThreePhaseCalibrationRoutine());
    }

    public void StartSinglePhaseCalibration()
    {
        if (isCalibrating) return;
        
        DebugLog("Starting single-phase calibration (legacy)");
        StartCoroutine(SinglePhaseCalibrationRoutine());
    }

    IEnumerator ThreePhaseCalibrationRoutine()
    {
        isCalibrating = true;
        
        // Clear all calibration data
        neutralSamples.Clear();
        leftSamples.Clear();
        rightSamples.Clear();
        
        // Phase 0: Neutral
        currentCalibrationPhase = 0;
        yield return StartCoroutine(CollectCalibrationPhase(neutralSamples, "Hold paddle FLAT/NEUTRAL"));
        
        // Phase 1: Right
        currentCalibrationPhase = 1;
        yield return StartCoroutine(CollectCalibrationPhase(rightSamples, "Tilt paddle to RIGHT"));
        
        // Phase 2: Left
        currentCalibrationPhase = 2;
        yield return StartCoroutine(CollectCalibrationPhase(leftSamples, "Tilt paddle to LEFT"));
        
        FinishThreePhaseCalibration();
    }

    IEnumerator CollectCalibrationPhase(List<float> sampleList, string instruction)
    {
        if (showCalibrationDebug)
        {
            DebugLog($"Phase {currentCalibrationPhase}: {instruction}");
        }
        
        float startTime = Time.time;
        float sampleInterval = calibrationHoldTime / calibrationSamples;
        
        while (sampleList.Count < calibrationSamples && 
               Time.time - startTime < calibrationHoldTime * 2)
        {
            if (Mathf.Abs(angleVelocity) < calibrationStabilityThreshold)
            {
                sampleList.Add(currentAngle);
                if (showCalibrationDebug)
                {
                    DebugLog($"Phase {currentCalibrationPhase} sample {sampleList.Count}: {currentAngle:F1}°");
                }
            }
            
            yield return new WaitForSeconds(sampleInterval);
        }
        
        if (showCalibrationDebug)
        {
            DebugLog($"Phase {currentCalibrationPhase} complete: {sampleList.Count} samples collected");
        }
    }

    void FinishThreePhaseCalibration()
    {
        isCalibrating = false;
        
        // Validate sample counts
        if (neutralSamples.Count < calibrationSamples / 2 ||
            leftSamples.Count < calibrationSamples / 2 ||
            rightSamples.Count < calibrationSamples / 2)
        {
            DebugLog("❌ 3-phase calibration failed - insufficient samples");
            OnCalibrationComplete?.Invoke(false);
            return;
        }
        
        // Calculate averages for each phase - FIXED LOGIC
        neutralAngle = CalculateAverage(neutralSamples);
        rightAngle = CalculateAverage(rightSamples);
        leftAngle = CalculateAverage(leftSamples);
        
        isCalibrated = true;
        
        DebugLog($"✅ 3-phase calibration complete!");
        DebugLog($"Neutral: {neutralAngle:F1}°, Left: {leftAngle:F1}°, Right: {rightAngle:F1}°");
        DebugLog($"Samples: Neutral={neutralSamples.Count}, Left={leftSamples.Count}, Right={rightSamples.Count}");
        
        OnCalibrationComplete?.Invoke(true);
    }

    IEnumerator SinglePhaseCalibrationRoutine()
    {
        isCalibrating = true;
        List<float> allSamples = new List<float>();
        
        float startTime = Time.time;
        float sampleInterval = calibrationHoldTime / calibrationSamples;
        
        while (allSamples.Count < calibrationSamples && 
               Time.time - startTime < calibrationHoldTime * 2)
        {
            if (Mathf.Abs(angleVelocity) < calibrationStabilityThreshold)
            {
                allSamples.Add(currentAngle);
            }
            
            yield return new WaitForSeconds(sampleInterval);
        }
        
        FinishSinglePhaseCalibration(allSamples);
    }

    void FinishSinglePhaseCalibration(List<float> samples)
    {
        isCalibrating = false;
        
        if (samples.Count < calibrationSamples / 2)
        {
            DebugLog("❌ Single-phase calibration failed");
            OnCalibrationComplete?.Invoke(false);
            return;
        }
        
        // Legacy logic for single-phase
        float minAngle = float.MaxValue;
        float maxAngle = float.MinValue;
        
        foreach (float angle in samples)
        {
            if (angle < minAngle) minAngle = angle;
            if (angle > maxAngle) maxAngle = angle;
        }
        
        leftAngle = minAngle;
        rightAngle = maxAngle;
        neutralAngle = (minAngle + maxAngle) / 2f;
        
        isCalibrated = true;
        
        DebugLog($"✅ Single-phase calibration complete!");
        DebugLog($"Left: {leftAngle:F1}°, Right: {rightAngle:F1}°, Neutral: {neutralAngle:F1}°");
        
        OnCalibrationComplete?.Invoke(true);
    }

    float CalculateAverage(List<float> samples)
    {
        if (samples.Count == 0) return 0f;
        
        float sum = 0f;
        foreach (float sample in samples)
        {
            sum += sample;
        }
        return sum / samples.Count;
    }

    public void ResetCalibration()
    {
        isCalibrated = false;
        isCalibrating = false;
        neutralSamples.Clear();
        leftSamples.Clear();
        rightSamples.Clear();
        currentCalibrationPhase = 0;
        DebugLog("Calibration reset");
    }
    #endregion

    #region Public API
    public bool IsConnected() => isConnected;
    public bool IsCalibrated() => isCalibrated;
    public bool IsCalibrating() => isCalibrating;
    public PaddleState GetCurrentState() => currentState;
    public float GetCurrentAngle() => smoothedAngle;
    public float GetAngleVelocity() => smoothedVelocity;
    public float GetStateConfidence() => stateConfidence;
    public string GetLastPacket() => lastPacket;
    public float GetValidPacketRatio() => packetsReceived > 0 ? (float)validPackets / packetsReceived : 0f;
    public int GetCurrentCalibrationPhase() => currentCalibrationPhase;
    public string GetCalibrationPhaseNames() => currentCalibrationPhase < calibrationPhaseNames.Length ? calibrationPhaseNames[currentCalibrationPhase] : "Unknown";
    
    [ContextMenu("Start 3-Phase Calibration")]
    public void ManualThreePhaseCalibration() => StartThreePhaseCalibration();
    
    [ContextMenu("Start Single-Phase Calibration")]
    public void ManualSinglePhaseCalibration() => StartSinglePhaseCalibration();
    
    [ContextMenu("Reset Calibration")]
    public void ManualResetCalibration() => ResetCalibration();
    
    [ContextMenu("Connect Bluetooth")]
    public void ManualConnect() => ConnectToBluetooth();
    #endregion

    #region Debug
    void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PaddleController] {message}");
        }
    }

    void OnGUI()
    {
        if (!showAngleDebug) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 400, 250));
        GUILayout.Box("Paddle Controller Debug - Fixed 3-Phase");
        
        // Connection status
        GUI.color = isConnected ? Color.green : Color.red;
        GUILayout.Label($"Status: {(isConnected ? "Connected" : "Disconnected")}");
        GUI.color = Color.white;
        
        // Calibration status
        if (isCalibrating)
        {
            GUI.color = Color.yellow;
            string phaseName = currentCalibrationPhase < calibrationPhaseNames.Length ? 
                calibrationPhaseNames[currentCalibrationPhase] : "Unknown";
            GUILayout.Label($"Calibrating Phase {currentCalibrationPhase}: {phaseName}");
            
            // Show sample counts per phase
            GUILayout.Label($"Samples: N={neutralSamples.Count} R={rightSamples.Count} L={leftSamples.Count}");
        }
        else if (isCalibrated)
        {
            GUI.color = Color.green;
            GUILayout.Label($"✓ Calibrated ({(useMultiPointCalibration ? "3-Phase" : "Single")})");
        }
        else
        {
            GUI.color = Color.red;
            GUILayout.Label("Not Calibrated");
        }
        GUI.color = Color.white;
        
        // Current data
        GUILayout.Label($"Angle: {smoothedAngle:F1}° (Raw: {currentAngle:F1}°)");
        GUILayout.Label($"Velocity: {smoothedVelocity:F1}°/s");
        GUILayout.Label($"State: {currentState} ({stateConfidence:F2})");
        
        if (isCalibrated)
        {
            GUILayout.Label($"Calibrated Angles:");
            GUILayout.Label($"  Neutral: {neutralAngle:F1}° (±{deadZone}°)");
            GUILayout.Label($"  Left: {leftAngle:F1}°, Right: {rightAngle:F1}°");
            
            // Show relative position
            float relativeToNeutral = smoothedAngle - neutralAngle;
            GUILayout.Label($"Relative to Neutral: {relativeToNeutral:F1}°");
        }
        
        // Packet info
        float validRatio = GetValidPacketRatio() * 100f;
        GUILayout.Label($"Packets: {validPackets}/{packetsReceived} ({validRatio:F1}%)");
        
        // Manual controls
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("3-Phase Cal")) ManualThreePhaseCalibration();
        if (GUILayout.Button("Single Cal")) ManualSinglePhaseCalibration();
        if (GUILayout.Button("Reset")) ManualResetCalibration();
        GUILayout.EndHorizontal();
        
        GUILayout.EndArea();
    }

    void OnDestroy()
    {
        if (bluetoothHelper != null)
        {
            bluetoothHelper.Disconnect();
        }
    }
    #endregion
}