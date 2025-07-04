using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CentralInputProcessor : MonoBehaviour
{
    public enum InputMode { Keyboard, Gyro, Auto }

    [Header("Mode Settings")]
    [SerializeField] private InputMode currentMode = InputMode.Auto;
    [SerializeField] private bool enableModeSwitch = true;
    [SerializeField] private KeyCode modeSwitchKey = KeyCode.Tab;

    [Header("Components")]
    public BoatController boatController;
    public PaddleIKController paddleController;
    public GyroPatternDetector patternDetector;
    public GyroCalibrator gyroCalibrator;

    [Header("Keyboard Input")]
    [SerializeField] private KeyCode leftPaddleKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode rightPaddleKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode forwardKey = KeyCode.UpArrow;

    [Header("Gyro Settings")]
    [SerializeField] private float gyroThreshold = 15f;
    [SerializeField] private float gyroCooldown = 0.5f;
    [SerializeField] private bool invertGyroInput = true;
    [SerializeField] private float idleThreshold = 5f;
    [SerializeField] private float idleTimeout = 2f;
    [SerializeField] private float gyroSensitivity = 1.0f;
    [SerializeField] private float deadZone = 0.1f;
    [SerializeField] private float smoothingFactor = 0.2f;

    [Header("UI")]
    [SerializeField] private Text modeDisplayText;
    [SerializeField] private Text statusText;
    [SerializeField] private GameObject connectionIndicator;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showGyroDebug = false;

    // State
    private InputMode activeMode = InputMode.Keyboard;
    private bool isInitialized = false;
    private float lastInputTime = 0f;
    
    // Connection
    private bool isGyroConnected = false;
    private bool isInIdleState = false;
    
    // Input cooldowns
    private float leftCooldownTimer = 0f;
    private float rightCooldownTimer = 0f;
    
    // Pattern integration
    private bool usePatternDetection = true;
    private float patternInputCooldown = 0f;
    private const float patternInputInterval = 0.2f;

    // Gyro processing
    private Vector3 smoothedGyro = Vector3.zero;
    private Vector3 previousGyro = Vector3.zero;
    private Vector3 gyroVelocity = Vector3.zero;
    private MonoBehaviour bluetoothManager;

    void Start()
    {
        InitializeComponents();
        SetupInitialMode();
        RegisterCalibrationEvents();
    }

    void Update()
    {
        UpdateConnectionStatus();
        UpdateInputCooldowns();
        
        if (enableModeSwitch && Input.GetKeyDown(modeSwitchKey))
            CycleInputMode();
        
        // Handle calibration
        if (gyroCalibrator != null && !gyroCalibrator.IsCalibrated() && isGyroConnected)
        {
            if (!gyroCalibrator.IsCalibrating())
            {
                gyroCalibrator.StartCalibration();
            }
            return; // Skip input processing during calibration
        }
        
        ProcessInput();
        UpdateIdleState();
        UpdateUI();
        
        patternInputCooldown -= Time.deltaTime;
    }

    #region Initialization
    private void InitializeComponents()
    {
        if (boatController == null) boatController = FindObjectOfType<BoatController>();
        if (paddleController == null) paddleController = FindObjectOfType<PaddleIKController>();
        if (patternDetector == null) patternDetector = FindObjectOfType<GyroPatternDetector>();
        if (gyroCalibrator == null) gyroCalibrator = FindObjectOfType<GyroCalibrator>();

        FindBluetoothManager();

        if (boatController != null)
            boatController.SetInputMode(BoatController.InputMode.BluetoothSensor);

        isInitialized = true;
        DebugLog("CentralInputProcessor initialized with GyroCalibrator");
    }

    private void FindBluetoothManager()
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
    }

    private void RegisterCalibrationEvents()
    {
        if (gyroCalibrator != null)
        {
            gyroCalibrator.OnCalibrationStateChanged += OnCalibrationStateChanged;
            gyroCalibrator.OnCalibrationCompleted += OnCalibrationCompleted;
        }
    }

    private void SetupInitialMode()
    {
        activeMode = (currentMode == InputMode.Auto) ? 
            ((IsGyroAvailable()) ? InputMode.Gyro : InputMode.Keyboard) : 
            currentMode;

        ConfigureForMode(activeMode);
    }
    #endregion

    #region Input Processing
    private void ProcessInput()
    {
        switch (activeMode)
        {
            case InputMode.Keyboard:
                ProcessKeyboardInput();
                break;
            case InputMode.Gyro:
                ProcessGyroInput();
                break;
        }
    }

    private void ProcessKeyboardInput()
    {
        if (Input.GetKeyDown(leftPaddleKey) && leftCooldownTimer <= 0f)
        {
            TriggerPaddle(true, "Keyboard");
            leftCooldownTimer = gyroCooldown;
        }

        if (Input.GetKeyDown(rightPaddleKey) && rightCooldownTimer <= 0f)
        {
            TriggerPaddle(false, "Keyboard");
            rightCooldownTimer = gyroCooldown;
        }

        if (Input.GetKey(forwardKey))
        {
            boatController?.AddForwardThrust(Time.deltaTime);
            lastInputTime = Time.time;
        }
    }

    private void ProcessGyroInput()
    {
        if (!isGyroConnected || gyroCalibrator == null || !gyroCalibrator.IsCalibrated()) 
            return;

        // Get and process gyro data
        Vector3 rawGyro = GetBluetoothGyroData();
        Vector3 calibratedGyro = gyroCalibrator.ApplyCalibration(rawGyro);
        
        // Apply sensitivity and deadzone
        Vector3 processedGyro = new Vector3(
            calibratedGyro.x * gyroSensitivity,
            calibratedGyro.y * gyroSensitivity,
            calibratedGyro.z * gyroSensitivity
        );

        if (processedGyro.magnitude < deadZone)
            processedGyro = Vector3.zero;

        // Smooth the data
        smoothedGyro = Vector3.Lerp(smoothedGyro, processedGyro, smoothingFactor);
        gyroVelocity = (smoothedGyro - previousGyro) / Time.deltaTime;
        previousGyro = smoothedGyro;

        if (showGyroDebug)
        {
            DebugLog($"RAW: {rawGyro.magnitude:F2} | PROCESSED: {smoothedGyro.magnitude:F2}");
        }

        if (usePatternDetection && patternDetector != null)
        {
            ProcessPatternBasedInput();
        }
        else
        {
            ProcessDirectGyroInput();
        }
    }

    private void ProcessPatternBasedInput()
    {
        if (patternInputCooldown > 0f) return;

        var pattern = patternDetector.GetCurrentPattern();
        float confidence = patternDetector.GetPatternConfidence();

        if (confidence < 0.6f) return;

        switch (pattern)
        {
            case GyroPatternDetector.MovementPattern.Forward:
                boatController?.AddForwardThrust(confidence * 0.8f);
                patternInputCooldown = patternInputInterval;
                break;
            case GyroPatternDetector.MovementPattern.TurnLeft:
                TriggerPaddle(false, "Pattern-Left");
                patternInputCooldown = patternInputInterval;
                break;
            case GyroPatternDetector.MovementPattern.TurnRight:
                TriggerPaddle(true, "Pattern-Right");
                patternInputCooldown = patternInputInterval;
                break;
        }

        lastInputTime = Time.time;
    }

    private void ProcessDirectGyroInput()
    {
        if (isInIdleState)
        {
            HandleGyroIdleMode();
            return;
        }

        float currentAngle = smoothedGyro.x;
        
        if (Mathf.Abs(currentAngle) > gyroThreshold)
        {
            bool isTiltingRight = currentAngle > 0;
            if (invertGyroInput) isTiltingRight = !isTiltingRight;

            if (isTiltingRight && rightCooldownTimer <= 0f)
            {
                TriggerPaddle(false, "Gyro-Right");
                rightCooldownTimer = gyroCooldown;
            }
            else if (!isTiltingRight && leftCooldownTimer <= 0f)
            {
                TriggerPaddle(true, "Gyro-Left");
                leftCooldownTimer = gyroCooldown;
            }

            lastInputTime = Time.time;
        }

        // Update paddle visual
        if (paddleController != null)
            paddleController.SetRawAngle(currentAngle);
    }

    private void HandleGyroIdleMode()
    {
        if (paddleController != null)
        {
            float currentAngle = smoothedGyro.x;
            paddleController.SetRawAngle(currentAngle);
        }
    }

    private Vector3 GetBluetoothGyroData()
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
    #endregion

    #region Paddle Actions
    private void TriggerPaddle(bool isLeft, string source)
    {
        if (boatController == null) return;

        if (isLeft)
            boatController.PaddleLeft();
        else
            boatController.PaddleRight();

        lastInputTime = Time.time;
        DebugLog($"{(isLeft ? "LEFT" : "RIGHT")} paddle - {source}");
    }
    #endregion

    #region State Management
    private void UpdateConnectionStatus()
    {
        bool wasConnected = isGyroConnected;
        isGyroConnected = IsGyroAvailable();

        if (currentMode == InputMode.Auto && wasConnected != isGyroConnected)
        {
            SwitchToMode(isGyroConnected ? InputMode.Gyro : InputMode.Keyboard);
        }
    }

    private bool IsGyroAvailable()
    {
        return (gyroCalibrator != null && gyroCalibrator.IsConnected());
    }

    private void UpdateIdleState()
    {
        if (activeMode != InputMode.Gyro || !isGyroConnected) return;

        float timeSinceInput = Time.time - lastInputTime;
        float currentSpeed = boatController?.GetCurrentSpeed() ?? 0f;
        float currentAngle = smoothedGyro.magnitude;

        bool shouldBeIdle = (timeSinceInput > idleTimeout) && 
                           (currentSpeed < 0.1f) && 
                           (currentAngle < idleThreshold);

        if (shouldBeIdle != isInIdleState)
        {
            isInIdleState = shouldBeIdle;
            DebugLog($"Idle state: {(isInIdleState ? "ON" : "OFF")}");
        }
    }

    private void UpdateInputCooldowns()
    {
        leftCooldownTimer = Mathf.Max(0f, leftCooldownTimer - Time.deltaTime);
        rightCooldownTimer = Mathf.Max(0f, rightCooldownTimer - Time.deltaTime);
    }
    #endregion

    #region Calibration Events
    private void OnCalibrationStateChanged(bool isCalibrating)
    {
        DebugLog($"Calibration state: {(isCalibrating ? "ACTIVE" : "IDLE")}");
    }

    private void OnCalibrationCompleted(Vector3 offset)
    {
        DebugLog($"Calibration completed with offset: {offset}");
    }
    #endregion

    #region Mode Management
    public void SwitchToMode(InputMode newMode)
    {
        if (newMode == activeMode) return;

        activeMode = newMode;
        ConfigureForMode(newMode);
        DebugLog($"Mode: {newMode}");
    }

    public void CycleInputMode()
    {
        switch (activeMode)
        {
            case InputMode.Keyboard: SwitchToMode(InputMode.Gyro); break;
            case InputMode.Gyro: SwitchToMode(InputMode.Keyboard); break;
        }
    }

    private void ConfigureForMode(InputMode mode)
    {
        switch (mode)
        {
            case InputMode.Keyboard:
                if (paddleController != null)
                {
                    paddleController.useRawAngle = false;
                    paddleController.overrideWithRawAngle = false;
                }
                usePatternDetection = false;
                break;

            case InputMode.Gyro:
                if (paddleController != null)
                {
                    paddleController.useRawAngle = true;
                    paddleController.overrideWithRawAngle = true;
                }
                usePatternDetection = (patternDetector != null);
                break;
        }
    }
    #endregion

    #region UI Updates
    private void UpdateUI()
    {
        if (modeDisplayText != null)
        {
            string text = $"Input: {activeMode}";
            if (activeMode == InputMode.Gyro)
            {
                text += isGyroConnected ? " (Connected)" : " (Disconnected)";
                if (gyroCalibrator != null && gyroCalibrator.IsCalibrating())
                    text += " [CALIBRATING]";
                else if (gyroCalibrator != null && !gyroCalibrator.IsCalibrated())
                    text += " [NOT CALIBRATED]";
                else if (isInIdleState) 
                    text += " [IDLE]";
            }
            modeDisplayText.text = text;
        }

        if (statusText != null)
        {
            string status = $"Mode: {activeMode}\n";
            
            if (activeMode == InputMode.Gyro && isGyroConnected)
            {
                if (gyroCalibrator != null && gyroCalibrator.IsCalibrating())
                {
                    status += $"Calibrating: {gyroCalibrator.GetCalibrationSampleCount()}\n";
                }
                else if (gyroCalibrator != null && gyroCalibrator.IsCalibrated())
                {
                    float angle = smoothedGyro.x;
                    status += $"Gyro: {angle:F1}°\n";
                    status += $"State: {(isInIdleState ? "Idle" : "Active")}";
                    
                    if (usePatternDetection && patternDetector != null)
                    {
                        status += $"\nPattern: {patternDetector.GetCurrentPattern()}";
                    }
                }
                else
                {
                    status += "Awaiting calibration";
                }
            }
            else
            {
                status += "Ready";
            }
            
            statusText.text = status;
        }

        if (connectionIndicator != null)
        {
            Renderer renderer = connectionIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color color = Color.blue; // Keyboard default
                if (activeMode == InputMode.Gyro)
                {
                    if (gyroCalibrator != null && gyroCalibrator.IsCalibrating())
                        color = Color.yellow;
                    else if (gyroCalibrator != null && !gyroCalibrator.IsCalibrated())
                        color = new Color(1f, 0.5f, 0f); // Orange
                    else
                        color = isGyroConnected ? Color.green : Color.red;
                }
                
                renderer.material.color = color;
            }
        }
    }
    #endregion

    #region Public API
    public InputMode GetActiveMode() => activeMode;
    public bool IsGyroConnected() => isGyroConnected;
    public bool IsInIdleState() => isInIdleState;
    public float GetTimeSinceLastInput() => Time.time - lastInputTime;
    public Vector3 GetSmoothedGyro() => smoothedGyro;

    public void ManualLeftPaddle() => TriggerPaddle(true, "Manual");
    public void ManualRightPaddle() => TriggerPaddle(false, "Manual");

    // UI button handlers
    public void OnKeyboardModeButton() => SwitchToMode(InputMode.Keyboard);
    public void OnGyroModeButton() => SwitchToMode(InputMode.Gyro);
    public void OnAutoModeButton()
    {
        currentMode = InputMode.Auto;
        UpdateConnectionStatus();
    }

    public void TogglePatternDetection()
    {
        usePatternDetection = !usePatternDetection;
        DebugLog($"Pattern detection: {(usePatternDetection ? "ON" : "OFF")}");
    }

    // Calibration controls
    public void StartCalibration()
    {
        if (gyroCalibrator != null)
            gyroCalibrator.StartCalibration();
    }

    public void ResetCalibration()
    {
        if (gyroCalibrator != null)
            gyroCalibrator.ResetCalibration();
    }
    #endregion

    #region Debug
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[CentralInput] {message}");
    }

    void OnGUI()
    {
        if (!enableDebugLogs) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 160));
        GUILayout.Label($"Mode: {activeMode}");
        GUILayout.Label($"Gyro: {(isGyroConnected ? "Connected" : "Disconnected")}");
        
        if (gyroCalibrator != null)
        {
            if (gyroCalibrator.IsCalibrating())
            {
                GUILayout.Label($"Calibrating: {gyroCalibrator.GetCalibrationSampleCount()}");
            }
            else
            {
                GUILayout.Label($"Calibrated: {gyroCalibrator.IsCalibrated()}");
            }
        }
        
        GUILayout.Label($"Idle: {isInIdleState}");
        GUILayout.Label($"Pattern Detection: {(usePatternDetection ? "ON" : "OFF")}");
        
        if (activeMode == InputMode.Gyro && isGyroConnected && gyroCalibrator != null && gyroCalibrator.IsCalibrated())
        {
            GUILayout.Label($"Angle: {smoothedGyro.x:F1}°");
        }
        
        GUILayout.Label($"Switch: {modeSwitchKey}");
        GUILayout.EndArea();
    }

    void OnDestroy()
    {
        if (gyroCalibrator != null)
        {
            gyroCalibrator.OnCalibrationStateChanged -= OnCalibrationStateChanged;
            gyroCalibrator.OnCalibrationCompleted -= OnCalibrationCompleted;
        }
    }
    #endregion
}