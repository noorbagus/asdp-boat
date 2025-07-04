using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GyroIntegratedController : MonoBehaviour
{
    [Header("Dependencies")]
    public BoatController boatController;
    public PaddleIKController paddleIKController;
    
    [Header("Gyro Settings")]
    public float gyroSensitivity = 1.0f;
    public float deadZone = 0.1f;
    public float smoothingFactor = 0.2f;
    public bool invertXAxis = false;
    public bool invertYAxis = false;
    
    [Header("Paddle Mapping")]
    [Tooltip("Threshold for detecting paddle strokes")]
    public float paddleThreshold = 15f;
    [Tooltip("Cooldown between paddle strokes")]
    public float paddleCooldown = 0.3f;
    [Tooltip("How long to maintain paddle state")]
    public float paddleStateDuration = 0.2f;
    
    [Header("Boat Control")]
    public bool enableBoatControl = true;
    public float forwardThreshold = 20f;
    public float turnThreshold = 25f;
    public float minConsecutiveForTurn = 2;
    
    [Header("Calibration")]
    public bool requireCalibration = true;
    public float calibrationDuration = 3f;
    public int minCalibrationSamples = 15;
    public float stabilityThreshold = 2f;
    
    [Header("Input Source")]
    public bool useKeyboardInput = false;
    public KeyCode leftPaddleKey = KeyCode.A;
    public KeyCode rightPaddleKey = KeyCode.D;
    
    [Header("Debug")]
    public bool enableDebugLogs = true;
    public bool showGyroDebug = false;
    
    // Calibration state - simplified
    private enum CalibrationState { Idle, Active, Completed, Failed }
    private CalibrationState calibrationState = CalibrationState.Idle;
    private Vector3 gyroOffset = Vector3.zero;
    private List<Vector3> calibrationSamples = new List<Vector3>();
    private float calibrationTimer = 0f;
    
    // State tracking
    private Vector3 smoothedGyro = Vector3.zero;
    private Vector3 previousGyro = Vector3.zero;
    private Vector3 gyroVelocity = Vector3.zero;
    
    // Paddle state
    private bool canPaddleLeft = true;
    private bool canPaddleRight = true;
    private bool isLeftActive = false;
    private bool isRightActive = false;
    
    // Pattern detection
    private int consecutiveLeft = 0;
    private int consecutiveRight = 0;
    private float lastPaddleTime = 0f;
    private bool lastWasLeft = false;
    
    // Connection state
    private bool isConnected = false;
    
    // External component references
    private MonoBehaviour bluetoothManager;
    
    void Start()
    {
        InitializeComponents();
        RegisterEvents();
        
        if (requireCalibration && !useKeyboardInput)
        {
            StartCalibration();
        }
        else
        {
            calibrationState = CalibrationState.Completed;
            isConnected = true;
        }
    }
    
    void InitializeComponents()
    {
        if (boatController == null)
            boatController = FindObjectOfType<BoatController>();
        
        if (paddleIKController == null)
            paddleIKController = FindObjectOfType<PaddleIKController>();
        
        FindOptionalComponent("BluetoothManager", ref bluetoothManager);
        
        DebugLog($"Components - Boat: {boatController != null}, PaddleIK: {paddleIKController != null}, Bluetooth: {bluetoothManager != null}");
    }
    
    void FindOptionalComponent(string typeName, ref MonoBehaviour component)
    {
        MonoBehaviour[] allComponents = FindObjectsOfType<MonoBehaviour>();
        foreach (var comp in allComponents)
        {
            if (comp.GetType().Name == typeName)
            {
                component = comp;
                break;
            }
        }
    }
    
    void RegisterEvents()
    {
        if (bluetoothManager != null)
        {
            try
            {
                var onConnEvent = bluetoothManager.GetType().GetField("OnConnectionChanged");
                if (onConnEvent != null)
                {
                    var connAction = onConnEvent.GetValue(bluetoothManager) as System.Action<bool>;
                    connAction += OnConnectionChanged;
                    onConnEvent.SetValue(bluetoothManager, connAction);
                }
            }
            catch (System.Exception e)
            {
                DebugLog($"Failed to register events: {e.Message}");
            }
        }
    }
    
    void Update()
    {
        if (useKeyboardInput)
        {
            HandleKeyboardInput();
            return;
        }
        
        if (!isConnected) return;
        
        // Handle calibration states
        switch (calibrationState)
        {
            case CalibrationState.Active:
                UpdateCalibration();
                return;
            
            case CalibrationState.Idle:
            case CalibrationState.Failed:
                return;
            
            case CalibrationState.Completed:
                break; // Continue to normal processing
        }
        
        ProcessGyroInput();
        UpdatePaddleStates();
        DetectPaddlePatterns();
        
        if (enableBoatControl)
        {
            ProcessBoatMovement();
        }
    }
    
    void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(leftPaddleKey) && canPaddleLeft)
        {
            ExecuteLeftPaddle();
        }
        
        if (Input.GetKeyDown(rightPaddleKey) && canPaddleRight)
        {
            ExecuteRightPaddle();
        }
        
        UpdateConsecutiveCounters();
        UpdatePaddleStates();
        
        if (enableBoatControl)
        {
            ProcessBoatMovement();
        }
    }
    
    void ProcessGyroInput()
    {
        if (bluetoothManager == null) return;
        
        Vector3 rawGyro = GetBluetoothGyroData();
        
        if (showGyroDebug)
        {
            DebugLog($"RAW: Mag={rawGyro.magnitude:F2} X={rawGyro.x:F2} Y={rawGyro.y:F2} Z={rawGyro.z:F2}");
        }
        
        Vector3 processedGyro = new Vector3(
            rawGyro.x * gyroSensitivity * (invertXAxis ? -1 : 1),
            rawGyro.y * gyroSensitivity * (invertYAxis ? -1 : 1),
            rawGyro.z * gyroSensitivity
        );
        
        processedGyro -= gyroOffset;
        
        if (processedGyro.magnitude < deadZone)
        {
            processedGyro = Vector3.zero;
        }
        
        smoothedGyro = Vector3.Lerp(smoothedGyro, processedGyro, smoothingFactor);
        gyroVelocity = (smoothedGyro - previousGyro) / Time.deltaTime;
        previousGyro = smoothedGyro;
        
        if (showGyroDebug)
        {
            DebugLog($"PROCESSED: Smoothed={smoothedGyro.magnitude:F2} Velocity={gyroVelocity.magnitude:F2}");
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
    
    // REFACTORED CALIBRATION METHODS
    public void StartCalibration()
    {
        DebugLog("=== STARTING CALIBRATION ===");
        DebugLog($"Duration: {calibrationDuration}s, Min samples: {minCalibrationSamples}, Threshold: {stabilityThreshold}");
        
        calibrationState = CalibrationState.Active;
        calibrationTimer = calibrationDuration;
        calibrationSamples.Clear();
        gyroOffset = Vector3.zero;
        
        // Set connected for calibration if using keyboard
        if (useKeyboardInput)
        {
            isConnected = true;
        }
    }
    
    void UpdateCalibration()
    {
        // Safety check - should never happen with state machine
        if (calibrationState != CalibrationState.Active) return;
        
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
        
        // Set state first to prevent re-entry
        if (calibrationSamples.Count < minCalibrationSamples)
        {
            calibrationState = CalibrationState.Failed;
            DebugLog($"❌ FAILED - Not enough samples ({calibrationSamples.Count}/{minCalibrationSamples}). Retrying...");
            
            // Retry after short delay
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
    }
    
    IEnumerator RetryCalibrationAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (calibrationState == CalibrationState.Failed)
        {
            StartCalibration();
        }
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
    
    void DetectPaddlePatterns()
    {
        if (useKeyboardInput) return;
        
        bool leftPaddle = smoothedGyro.x < -paddleThreshold && canPaddleLeft;
        bool rightPaddle = smoothedGyro.x > paddleThreshold && canPaddleRight;
        
        if (leftPaddle)
        {
            ExecuteLeftPaddle();
        }
        else if (rightPaddle)
        {
            ExecuteRightPaddle();
        }
        
        UpdateConsecutiveCounters();
    }
    
    void ExecuteLeftPaddle()
    {
        if (!canPaddleLeft) return;
        
        DebugLog("Left paddle");
        
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
        lastPaddleTime = Time.time;
        
        if (boatController != null)
        {
            boatController.PaddleLeft();
        }
        
        if (paddleIKController != null)
        {
            paddleIKController.SetRawAngle(-30f);
        }
        
        StartCoroutine(LeftPaddleCooldown());
    }
    
    void ExecuteRightPaddle()
    {
        if (!canPaddleRight) return;
        
        DebugLog("Right paddle");
        
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
        lastPaddleTime = Time.time;
        
        if (boatController != null)
        {
            boatController.PaddleRight();
        }
        
        if (paddleIKController != null)
        {
            paddleIKController.SetRawAngle(30f);
        }
        
        StartCoroutine(RightPaddleCooldown());
    }
    
    void ProcessBoatMovement()
    {
        if (boatController == null) return;
        
        float forwardIntensity = useKeyboardInput ? 
            (Input.GetKey(leftPaddleKey) || Input.GetKey(rightPaddleKey) ? 20f : 0f) :
            Mathf.Abs(smoothedGyro.z);
        
        if (forwardIntensity > forwardThreshold || IsAlternatingPattern())
        {
            float confidence = Mathf.Clamp01(forwardIntensity / 50f);
            boatController.AddForwardThrust(confidence);
        }
        
        if (consecutiveLeft >= minConsecutiveForTurn)
        {
            ProcessTurnMovement(1f);
        }
        else if (consecutiveRight >= minConsecutiveForTurn)
        {
            ProcessTurnMovement(-1f);
        }
    }
    
    void ProcessTurnMovement(float direction)
    {
        if (boatController == null) return;
        
        float turnIntensity = useKeyboardInput ? 0.8f : Mathf.Clamp01(smoothedGyro.magnitude / 40f);
        boatController.AddForwardThrust(turnIntensity * 0.5f);
    }
    
    bool IsAlternatingPattern()
    {
        return consecutiveLeft <= 1 && consecutiveRight <= 1 && 
               Time.time - lastPaddleTime < 1f;
    }
    
    void UpdateConsecutiveCounters()
    {
        if (Time.time - lastPaddleTime > 1.5f)
        {
            consecutiveLeft = 0;
            consecutiveRight = 0;
        }
    }
    
    void UpdatePaddleStates()
    {
        if (isLeftActive && Time.time - lastPaddleTime > paddleStateDuration)
        {
            isLeftActive = false;
            if (paddleIKController != null)
            {
                paddleIKController.SetRawAngle(0f);
            }
        }
        
        if (isRightActive && Time.time - lastPaddleTime > paddleStateDuration)
        {
            isRightActive = false;
            if (paddleIKController != null)
            {
                paddleIKController.SetRawAngle(0f);
            }
        }
    }
    
    void OnConnectionChanged(bool connected)
    {
        isConnected = connected;
        DebugLog($"Connection: {(connected ? "✓" : "✗")}");
        
        if (!connected)
        {
            smoothedGyro = Vector3.zero;
            consecutiveLeft = 0;
            consecutiveRight = 0;
        }
    }
    
    IEnumerator LeftPaddleCooldown()
    {
        canPaddleLeft = false;
        isLeftActive = true;
        yield return new WaitForSeconds(paddleCooldown);
        canPaddleLeft = true;
    }
    
    IEnumerator RightPaddleCooldown()
    {
        canPaddleRight = false;
        isRightActive = true;
        yield return new WaitForSeconds(paddleCooldown);
        canPaddleRight = true;
    }
    
    // Public API
    public bool IsConnected() => useKeyboardInput || isConnected;
    public bool IsCalibrated() => calibrationState == CalibrationState.Completed;
    public bool IsCalibrating() => calibrationState == CalibrationState.Active;
    public Vector3 GetSmoothedGyro() => smoothedGyro;
    public Vector3 GetGyroOffset() => gyroOffset;
    public int GetConsecutiveLeft() => consecutiveLeft;
    public int GetConsecutiveRight() => consecutiveRight;
    public float GetCalibrationTimer() => calibrationTimer;
    public int GetCalibrationSampleCount() => calibrationSamples.Count;
    
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
    
    void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[GyroIntegrated] {message}");
        }
    }
    
    void OnGUI()
    {
        if (!enableDebugLogs) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 220));
        
        GUI.color = IsConnected() ? Color.green : Color.red;
        GUILayout.Label($"Input: {(useKeyboardInput ? "Keyboard" : "Gyro")} {(IsConnected() ? "✓" : "✗")}");
        GUI.color = Color.white;
        
        if (!useKeyboardInput && bluetoothManager != null)
        {
            Vector3 currentGyro = GetBluetoothGyroData();
            GUILayout.Label($"Raw Gyro: {currentGyro.magnitude:F2}");
        }
        
        switch (calibrationState)
        {
            case CalibrationState.Active:
                GUI.color = Color.yellow;
                GUILayout.Label($"CALIBRATING: {calibrationTimer:F1}s");
                GUILayout.Label($"Samples: {calibrationSamples.Count}/{minCalibrationSamples}");
                GUI.color = Color.white;
                
                if (GUILayout.Button("Force Finish"))
                {
                    ForceFinishCalibration();
                }
                break;
                
            case CalibrationState.Completed:
                GUI.color = Color.green;
                GUILayout.Label("Calibrated ✓");
                GUI.color = Color.white;
                GUILayout.Label($"Offset: {gyroOffset.magnitude:F3}");
                
                if (GUILayout.Button("Recalibrate"))
                {
                    StartCalibration();
                }
                break;
                
            case CalibrationState.Failed:
                GUI.color = Color.red;
                GUILayout.Label("Calibration Failed ✗");
                GUI.color = Color.white;
                
                if (GUILayout.Button("Retry"))
                {
                    StartCalibration();
                }
                break;
                
            case CalibrationState.Idle:
                GUI.color = Color.gray;
                GUILayout.Label("Not Calibrated");
                GUI.color = Color.white;
                
                if (GUILayout.Button("Start Calibration"))
                {
                    StartCalibration();
                }
                break;
        }
        
        if (calibrationState == CalibrationState.Completed)
        {
            GUILayout.Label($"Smoothed: {smoothedGyro.magnitude:F2}");
            GUILayout.Label($"L:{consecutiveLeft} R:{consecutiveRight}");
            GUILayout.Label($"Pattern: {(IsAlternatingPattern() ? "Alt" : "Cons")}");
        }
        
        GUILayout.EndArea();
    }
}