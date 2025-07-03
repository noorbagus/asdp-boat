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
    [SerializeField] private BoatController boatController;
    [SerializeField] private PaddleIKController paddleController;
    [SerializeField] private ESP32GyroController gyroController;
    [SerializeField] private GyroPatternDetector patternDetector;

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

    [Header("UI")]
    [SerializeField] private Text modeDisplayText;
    [SerializeField] private Text statusText;
    [SerializeField] private GameObject connectionIndicator;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

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

    void Start()
    {
        InitializeComponents();
        SetupInitialMode();
    }

    void Update()
    {
        UpdateConnectionStatus();
        UpdateInputCooldowns();
        
        if (enableModeSwitch && Input.GetKeyDown(modeSwitchKey))
            CycleInputMode();
        
        ProcessInput();
        UpdateIdleState();
        UpdateUI();
        
        patternInputCooldown -= Time.deltaTime;
    }

    #region Initialization
    private void InitializeComponents()
    {
        // Auto-find components if not assigned
        if (boatController == null) boatController = FindObjectOfType<BoatController>();
        if (paddleController == null) paddleController = FindObjectOfType<PaddleIKController>();
        if (gyroController == null) gyroController = FindObjectOfType<ESP32GyroController>();
        if (patternDetector == null) patternDetector = FindObjectOfType<GyroPatternDetector>();

        if (boatController != null)
            boatController.SetInputMode(BoatController.InputMode.BluetoothSensor);

        isInitialized = true;
        DebugLog("CentralInputProcessor initialized");
    }

    private void SetupInitialMode()
    {
        activeMode = (currentMode == InputMode.Auto) ? 
            ((gyroController != null && gyroController.IsConnected()) ? InputMode.Gyro : InputMode.Keyboard) : 
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
        if (!isGyroConnected || gyroController == null) return;

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

        float currentAngle = gyroController.GetSmoothedGyroValue();
        
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
        if (paddleController != null && gyroController != null)
        {
            float currentAngle = gyroController.GetSmoothedGyroValue();
            paddleController.SetRawAngle(currentAngle);
        }
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
        isGyroConnected = (gyroController != null && gyroController.IsConnected());

        // Auto-switch mode
        if (currentMode == InputMode.Auto && wasConnected != isGyroConnected)
        {
            SwitchToMode(isGyroConnected ? InputMode.Gyro : InputMode.Keyboard);
        }
    }

    private void UpdateIdleState()
    {
        if (activeMode != InputMode.Gyro || !isGyroConnected) return;

        float timeSinceInput = Time.time - lastInputTime;
        float currentSpeed = boatController?.GetCurrentSpeed() ?? 0f;
        float currentAngle = gyroController?.GetSmoothedGyroValue() ?? 0f;

        bool shouldBeIdle = (timeSinceInput > idleTimeout) && 
                           (currentSpeed < 0.1f) && 
                           (Mathf.Abs(currentAngle) < idleThreshold);

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
                if (isInIdleState) text += " [IDLE]";
            }
            modeDisplayText.text = text;
        }

        if (statusText != null)
        {
            string status = $"Mode: {activeMode}\n";
            
            if (activeMode == InputMode.Gyro && isGyroConnected)
            {
                float angle = gyroController?.GetSmoothedGyroValue() ?? 0f;
                status += $"Gyro: {angle:F1}°\n";
                status += $"State: {(isInIdleState ? "Idle" : "Active")}";
                
                if (usePatternDetection && patternDetector != null)
                {
                    status += $"\nPattern: {patternDetector.GetCurrentPattern()}";
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
                    color = isGyroConnected ? Color.green : Color.red;
                
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

        GUILayout.BeginArea(new Rect(10, 10, 300, 120));
        GUILayout.Label($"Mode: {activeMode}");
        GUILayout.Label($"Gyro: {(isGyroConnected ? "Connected" : "Disconnected")}");
        GUILayout.Label($"Idle: {isInIdleState}");
        GUILayout.Label($"Pattern Detection: {(usePatternDetection ? "ON" : "OFF")}");
        
        if (activeMode == InputMode.Gyro && isGyroConnected)
        {
            float angle = gyroController?.GetSmoothedGyroValue() ?? 0f;
            GUILayout.Label($"Angle: {angle:F1}°");
        }
        
        GUILayout.Label($"Switch: {modeSwitchKey}");
        GUILayout.EndArea();
    }
    #endregion
}