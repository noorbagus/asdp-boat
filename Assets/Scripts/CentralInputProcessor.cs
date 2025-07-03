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

    [Header("Component References")]
    [SerializeField] private BoatController boatController;
    [SerializeField] private PaddleIKController paddleController;
    [SerializeField] private ESP32GyroController gyroController;

    [Header("Keyboard Settings")]
    [SerializeField] private KeyCode leftPaddleKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode rightPaddleKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode forwardKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode backwardKey = KeyCode.DownArrow;

    [Header("Gyro Settings")]
    [SerializeField] private float gyroThreshold = 15f;
    [SerializeField] private float gyroCooldown = 0.5f;
    [SerializeField] private bool invertGyroInput = true;
    [SerializeField] private float idleThreshold = 5f;
    [SerializeField] private float idleTimeout = 2f;

    [Header("UI Elements")]
    [SerializeField] private Text modeDisplayText;
    [SerializeField] private Text statusText;
    [SerializeField] private GameObject connectionIndicator;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showInputFeedback = true;

    // State tracking
    private InputMode activeMode = InputMode.Keyboard;
    private bool isInitialized = false;
    private float lastInputTime = 0f;
    private float lastModeCheckTime = 0f;
    
    // Gyro state
    private bool isGyroConnected = false;
    private float lastGyroAngle = 0f;
    private bool isInIdleState = false;
    private float idleStateStartTime = 0f;
    
    // Input cooldowns
    private float leftCooldownTimer = 0f;
    private float rightCooldownTimer = 0f;

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
        {
            CycleInputMode();
        }
        
        // Process input based on active mode
        switch (activeMode)
        {
            case InputMode.Keyboard:
                ProcessKeyboardInput();
                break;
            case InputMode.Gyro:
                ProcessGyroInput();
                break;
        }
        
        UpdateIdleState();
        UpdateUI();
    }

    #region Initialization
    private void InitializeComponents()
    {
        // Validate references
        if (boatController == null)
        {
            boatController = FindObjectOfType<BoatController>();
            if (boatController == null)
            {
                DebugLog("ERROR: BoatController not found!");
                return;
            }
        }

        if (paddleController == null)
        {
            paddleController = FindObjectOfType<PaddleIKController>();
        }

        if (gyroController == null)
        {
            gyroController = FindObjectOfType<ESP32GyroController>();
        }

        // REMOVED: esp32Integration reference - component deleted

        // Configure boat controller for central input
        if (boatController != null)
        {
            boatController.SetInputMode(BoatController.InputMode.BluetoothSensor);
        }

        isInitialized = true;
        DebugLog("CentralInputProcessor initialized");
    }

    private void SetupInitialMode()
    {
        if (currentMode == InputMode.Auto)
        {
            // Auto-detect based on ESP32 connection
            activeMode = (gyroController != null && gyroController.IsConnected()) ? 
                        InputMode.Gyro : InputMode.Keyboard;
        }
        else
        {
            activeMode = currentMode;
        }

        DebugLog($"Initial mode set to: {activeMode}");
    }
    #endregion

    #region Input Processing
    private void ProcessKeyboardInput()
    {
        // Left paddle
        if (Input.GetKeyDown(leftPaddleKey) && leftCooldownTimer <= 0f)
        {
            TriggerLeftPaddle("Keyboard");
            leftCooldownTimer = gyroCooldown;
        }

        // Right paddle
        if (Input.GetKeyDown(rightPaddleKey) && rightCooldownTimer <= 0f)
        {
            TriggerRightPaddle("Keyboard");
            rightCooldownTimer = gyroCooldown;
        }

        // Direct movement (if supported)
        if (Input.GetKey(forwardKey))
        {
            // Use reflection to access AddForwardThrust
            var method = boatController.GetType().GetMethod("AddForwardThrust", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(boatController, new object[] { 1.0f * Time.deltaTime });
            
            lastInputTime = Time.time;
        }

        if (Input.GetKey(backwardKey))
        {
            // Slow down boat
            float currentSpeed = boatController.GetCurrentSpeed();
            // Use reflection to set speed if needed
            lastInputTime = Time.time;
        }
    }

    private void ProcessGyroInput()
    {
        if (!isGyroConnected || gyroController == null)
        {
            return;
        }

        float currentAngle = gyroController.GetSmoothedGyroValue();
        
        // Handle idle state
        if (isInIdleState)
        {
            HandleGyroIdleMode(currentAngle);
            return;
        }

        // Process active gyro input
        if (Mathf.Abs(currentAngle) > gyroThreshold)
        {
            bool isTiltingRight = currentAngle > 0;
            
            // Apply inversion if needed (FIXED: no automatic inversion)
            if (invertGyroInput)
            {
                isTiltingRight = !isTiltingRight;
            }

            // Trigger appropriate paddle with cooldown
            if (isTiltingRight && rightCooldownTimer <= 0f)
            {
                TriggerRightPaddle("Gyro");
                rightCooldownTimer = gyroCooldown;
            }
            else if (!isTiltingRight && leftCooldownTimer <= 0f)
            {
                TriggerLeftPaddle("Gyro");
                leftCooldownTimer = gyroCooldown;
            }

            lastInputTime = Time.time;
        }

        // Update paddle visual to follow gyro smoothly
        if (paddleController != null)
        {
            paddleController.SetRawAngle(currentAngle);
        }

        lastGyroAngle = currentAngle;
    }

    private void HandleGyroIdleMode(float currentAngle)
    {
        // In idle mode, paddle follows gyro smoothly without triggering movement
        if (paddleController != null)
        {
            paddleController.SetRawAngle(currentAngle);
            
            // Enable gyro idle sync if available
            if (paddleController.syncIdleWithGyro)
            {
                // Already handled by PaddleIKController
            }
        }

        DebugLog($"Gyro Idle Mode: Angle {currentAngle:F1}°");
    }
    #endregion

    #region Paddle Actions
    private void TriggerLeftPaddle(string source)
    {
        if (boatController != null)
        {
            boatController.PaddleLeft();
            lastInputTime = Time.time;
            
            if (showInputFeedback)
            {
                DebugLog($"LEFT PADDLE triggered by {source}");
            }
        }
    }

    private void TriggerRightPaddle(string source)
    {
        if (boatController != null)
        {
            boatController.PaddleRight();
            lastInputTime = Time.time;
            
            if (showInputFeedback)
            {
                DebugLog($"RIGHT PADDLE triggered by {source}");
            }
        }
    }
    #endregion

    #region State Management
    private void UpdateConnectionStatus()
    {
        bool wasConnected = isGyroConnected;
        isGyroConnected = (gyroController != null && gyroController.IsConnected());

        // Auto-switch mode based on connection
        if (currentMode == InputMode.Auto)
        {
            InputMode newMode = isGyroConnected ? InputMode.Gyro : InputMode.Keyboard;
            
            if (newMode != activeMode)
            {
                SwitchToMode(newMode);
            }
        }

        // Log connection changes
        if (wasConnected != isGyroConnected)
        {
            DebugLog($"ESP32 connection: {(isGyroConnected ? "CONNECTED" : "DISCONNECTED")}");
        }
    }

    private void UpdateIdleState()
    {
        bool shouldBeIdle = false;

        if (activeMode == InputMode.Gyro && isGyroConnected)
        {
            // Check conditions for idle state
            float timeSinceLastInput = Time.time - lastInputTime;
            float currentSpeed = boatController != null ? boatController.GetCurrentSpeed() : 0f;
            float currentAngle = gyroController != null ? gyroController.GetSmoothedGyroValue() : 0f;

            shouldBeIdle = (timeSinceLastInput > idleTimeout) && 
                          (currentSpeed < 0.1f) && 
                          (Mathf.Abs(currentAngle) < idleThreshold);
        }

        // Update idle state
        if (shouldBeIdle && !isInIdleState)
        {
            isInIdleState = true;
            idleStateStartTime = Time.time;
            DebugLog("Entered IDLE state");
        }
        else if (!shouldBeIdle && isInIdleState)
        {
            isInIdleState = false;
            DebugLog("Exited IDLE state");
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

        InputMode previousMode = activeMode;
        activeMode = newMode;

        // Configure components for new mode
        ConfigureForMode(newMode);

        DebugLog($"Input mode switched: {previousMode} → {newMode}");
    }

    public void CycleInputMode()
    {
        switch (activeMode)
        {
            case InputMode.Keyboard:
                SwitchToMode(InputMode.Gyro);
                break;
            case InputMode.Gyro:
                SwitchToMode(InputMode.Keyboard);
                break;
        }
    }

    private void ConfigureForMode(InputMode mode)
    {
        switch (mode)
        {
            case InputMode.Keyboard:
                // REMOVED: ESP32 integration disable - component deleted
                
                // Set paddle to keyboard mode
                if (paddleController != null)
                {
                    paddleController.useRawAngle = false;
                    paddleController.overrideWithRawAngle = false;
                }
                break;

            case InputMode.Gyro:
                // REMOVED: ESP32 integration enable - handled directly here
                
                // Set paddle to gyro mode
                if (paddleController != null)
                {
                    paddleController.useRawAngle = true;
                    paddleController.overrideWithRawAngle = true;
                }
                break;
        }
    }
    #endregion

    #region UI Updates
    private void UpdateUI()
    {
        // Update mode display
        if (modeDisplayText != null)
        {
            string modeText = $"Input: {activeMode}";
            if (activeMode == InputMode.Gyro)
            {
                modeText += isGyroConnected ? " (Connected)" : " (Disconnected)";
                if (isInIdleState)
                {
                    modeText += " [IDLE]";
                }
            }
            modeDisplayText.text = modeText;
        }

        // Update status text
        if (statusText != null)
        {
            string status = $"Mode: {activeMode}\n";
            
            if (activeMode == InputMode.Gyro && isGyroConnected)
            {
                float angle = gyroController.GetSmoothedGyroValue();
                status += $"Gyro: {angle:F1}°\n";
                status += $"State: {(isInIdleState ? "Idle" : "Active")}";
            }
            else
            {
                status += "Ready for input";
            }
            
            statusText.text = status;
        }

        // Update connection indicator
        if (connectionIndicator != null)
        {
            Renderer renderer = connectionIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (activeMode == InputMode.Gyro)
                {
                    renderer.material.color = isGyroConnected ? Color.green : Color.red;
                }
                else
                {
                    renderer.material.color = Color.blue; // Keyboard mode
                }
            }
        }
    }
    #endregion

    #region Public Interface
    public InputMode GetActiveMode() => activeMode;
    public bool IsGyroConnected() => isGyroConnected;
    public bool IsInIdleState() => isInIdleState;
    public float GetTimeSinceLastInput() => Time.time - lastInputTime;

    // Manual paddle triggers (for external use)
    public void ManualLeftPaddle()
    {
        TriggerLeftPaddle("Manual");
    }

    public void ManualRightPaddle()
    {
        TriggerRightPaddle("Manual");
    }

    // UI button handlers
    public void OnKeyboardModeButton()
    {
        SwitchToMode(InputMode.Keyboard);
    }

    public void OnGyroModeButton()
    {
        SwitchToMode(InputMode.Gyro);
    }

    public void OnAutoModeButton()
    {
        currentMode = InputMode.Auto;
        UpdateConnectionStatus(); // This will auto-switch
    }
    #endregion

    #region Debug
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[CentralInput] {message}");
        }
    }

    void OnGUI()
    {
        if (!enableDebugLogs) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        GUILayout.Label($"Input Mode: {activeMode}");
        GUILayout.Label($"Gyro Connected: {isGyroConnected}");
        GUILayout.Label($"Idle State: {isInIdleState}");
        
        if (activeMode == InputMode.Gyro && isGyroConnected)
        {
            float angle = gyroController?.GetSmoothedGyroValue() ?? 0f;
            GUILayout.Label($"Gyro Angle: {angle:F1}°");
        }
        
        GUILayout.Label($"Press {modeSwitchKey} to switch mode");
        GUILayout.EndArea();
    }
    #endregion
}