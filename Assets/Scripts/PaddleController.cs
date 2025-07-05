using System;
using System.Collections;
using UnityEngine;

public class PaddleController : MonoBehaviour
{
    [Header("Bluetooth Connection")]
    [SerializeField] private BluetoothManager bluetoothManager;
    
    [Header("Paddle Configuration")]
    [SerializeField] private float tiltThreshold = 15.0f;
    [SerializeField] private float maxTiltOutput = 45.0f;
    [SerializeField] private float stabilityThreshold = 0.5f;
    [SerializeField] private float gyroNoiseThreshold = 5.0f;
    [SerializeField] private float idleDuration = 2.0f;
    [SerializeField] private float correctionFactor = 0.2f;
    [SerializeField] private bool enableDebugLogs = true;
    
    [Header("Calibration")]
    [SerializeField] private int calibrationSamples = 50;
    [SerializeField] private bool autoCalibrate = true;
    [SerializeField] private float autoCalibrationDelay = 3.0f;
    [SerializeField] private bool useMultiPoint = true; // Default to 3-point calibration
    
    // Constants
    private const float GRAVITY = 9.8f;
    private const float RAD_TO_DEG = 180.0f / Mathf.PI;
    
    // Baseline data
    private Vector3 baselineAccel = Vector3.zero;
    private float baselineGyroZ = 0f;
    
    // Multi-point calibration data
    private Vector3[] calibrationAccel = new Vector3[3]; // neutral, left, right
    private float[] calibrationGyroZ = new float[3];
    private int currentCalibrationStep = 0;
    private bool isMultiPointCalibrating = false;
    
    // Processed data
    private float zAngle = 0f;
    private float correctedGyroZ = 0f;
    private float accelMagnitude = 0f;
    private float tiltAngleZ = 0f;
    
    // Timing data
    private float lastTime = 0f;
    private float lastMovement = 0f;
    private float idleStart = 0f;
    
    // State data
    private bool isIdleState = false;
    private bool isCalibrated = false;
    private PaddleState paddleState = PaddleState.Neutral;
    
    // Output data
    private float finalZAngle = 0f;
    private int paddleDirection = 0;
    private float tiltIntensity = 0f;
    
    // Debug data
    private float driftCorrection = 0f;
    private float stabilityScore = 0f;
    private int calibrationProgress = 0;
    private bool isCalibrating = false;
    
    // Calibration data collection
    private Vector3[] accelSamples;
    private float[] gyroZSamples;
    private int sampleIndex = 0;
    
    // Simple Calibration UI
    private bool showCalibrationUI = false;
    private string currentStepText = "";
    private GUIStyle backgroundStyle;
    private GUIStyle cardStyle;
    private GUIStyle titleStyle;
    private GUIStyle instructionStyle;
    private GUIStyle progressStyle;
    private bool uiInitialized = false;
    
    // Events
    public System.Action<float> OnZAngleChanged;
    public System.Action<PaddleState> OnPaddleStateChanged;
    public System.Action<bool> OnCalibrationStatusChanged;
    public System.Action<int, float> OnTiltDetected;
    public System.Action<string> OnCalibrationStepChanged;
    
    public enum PaddleState
    {
        Neutral,
        TiltLeft,
        TiltRight
    }
    
    void Start()
    {
        if (bluetoothManager == null)
            bluetoothManager = FindObjectOfType<BluetoothManager>();
        
        if (bluetoothManager == null)
        {
            Debug.LogError("[PaddleController] BluetoothManager not found!");
            return;
        }
        
        bluetoothManager.OnConnectionChanged += OnBluetoothConnectionChanged;
        
        accelSamples = new Vector3[calibrationSamples];
        gyroZSamples = new float[calibrationSamples];
        
        lastTime = Time.time;
        lastMovement = Time.time;
        
        InitializeCalibrationUI();
        
        Debug.Log("[PaddleController] Initialized. Auto-calibration will start when Bluetooth connects.");
    }
    
    void Update()
    {
        if (bluetoothManager != null && bluetoothManager.IsConnected())
        {
            if (isCalibrating || isMultiPointCalibrating)
            {
                if (!isMultiPointCalibrating)
                    UpdateCalibration();
            }
            else if (isCalibrated)
            {
                ProcessPaddleData();
            }
        }
    }
    
    void OnBluetoothConnectionChanged(bool connected)
    {
        if (connected && autoCalibrate && !isCalibrated)
        {
            Invoke("StartAutoCalibration", autoCalibrationDelay);
        }
        
        Debug.Log($"[PaddleController] Bluetooth {(connected ? "connected" : "disconnected")}");
    }
    
    void StartAutoCalibration()
    {
        if (bluetoothManager.IsConnected() && !isCalibrated)
        {
            Debug.Log("[PaddleController] 🚀 Auto-starting calibration...");
            StartCalibration();
        }
    }
    
    #region Simple Calibration UI
    
    void InitializeCalibrationUI()
    {
        // Navy blue maritime theme
        Color navyBlue = new Color(0.1f, 0.2f, 0.4f, 0.95f);
        Color lightBlue = new Color(0.3f, 0.6f, 1f, 1f);
        Color darkCard = new Color(0.15f, 0.25f, 0.45f, 0.9f);
        
        backgroundStyle = new GUIStyle();
        backgroundStyle.normal.background = CreateColorTexture(navyBlue);
        
        cardStyle = new GUIStyle();
        cardStyle.normal.background = CreateColorTexture(darkCard);
        cardStyle.border = new RectOffset(10, 10, 10, 10);
        cardStyle.padding = new RectOffset(30, 30, 30, 30);
        
        titleStyle = new GUIStyle();
        titleStyle.fontSize = 28;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = Color.white;
        
        instructionStyle = new GUIStyle();
        instructionStyle.fontSize = 18;
        instructionStyle.alignment = TextAnchor.MiddleCenter;
        instructionStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        instructionStyle.wordWrap = true;
        
        progressStyle = new GUIStyle();
        progressStyle.normal.background = CreateColorTexture(lightBlue);
        
        uiInitialized = true;
    }
    
    Texture2D CreateColorTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
    
    void OnGUI()
    {
        if (!showCalibrationUI || !uiInitialized) return;
        
        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        float cardWidth = 500f;
        float cardHeight = 300f;
        
        // Background overlay
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", backgroundStyle);
        
        // Main calibration card
        Rect cardRect = new Rect(centerX - cardWidth * 0.5f, centerY - cardHeight * 0.5f, cardWidth, cardHeight);
        GUI.Box(cardRect, "", cardStyle);
        
        GUILayout.BeginArea(cardRect);
        
        GUILayout.Space(20);
        
        // Title
        GUILayout.Label("⚓ KALIBRASI PADDLE", titleStyle);
        GUILayout.Space(20);
        
        // Current step instruction
        if (!string.IsNullOrEmpty(currentStepText))
        {
            GUILayout.Label(currentStepText, instructionStyle);
            GUILayout.Space(15);
        }
        
        // Progress bar
        DrawProgressBar();
        
        GUILayout.Space(15);
        
        // Progress percentage
        var percentStyle = new GUIStyle(instructionStyle);
        percentStyle.fontSize = 24;
        percentStyle.fontStyle = FontStyle.Bold;
        GUILayout.Label($"{calibrationProgress}%", percentStyle);
        
        GUILayout.EndArea();
    }
    
    void DrawProgressBar()
    {
        float barWidth = 400f;
        float barHeight = 20f;
        float progress = calibrationProgress / 100f;
        
        Rect barBg = GUILayoutUtility.GetRect(barWidth, barHeight);
        barBg.x = (Screen.width - barWidth) * 0.5f - 250f; // Center in card
        
        // Background
        GUI.color = new Color(0.2f, 0.2f, 0.3f, 0.8f);
        GUI.Box(barBg, "", progressStyle);
        
        // Progress fill
        Rect fillRect = new Rect(barBg.x, barBg.y, barBg.width * progress, barBg.height);
        GUI.color = new Color(0.3f, 0.8f, 1f, 1f);
        GUI.Box(fillRect, "", progressStyle);
        
        GUI.color = Color.white;
    }
    
    #endregion
    
    #region Calibration (Enhanced with Multi-Point)
    
    public void StartCalibration()
    {
        if (!bluetoothManager.IsConnected())
        {
            Debug.LogWarning("[PaddleController] Cannot calibrate - Bluetooth not connected!");
            return;
        }
        
        isCalibrated = false;
        calibrationProgress = 0;
        showCalibrationUI = true;
        
        OnCalibrationStatusChanged?.Invoke(true);
        
        if (useMultiPoint)
        {
            StartMultiPointCalibration();
        }
        else
        {
            StartSinglePointCalibration();
        }
    }
    
    void StartSinglePointCalibration()
    {
        isCalibrating = true;
        sampleIndex = 0;
        currentStepText = "Tahan paddle di posisi NETRAL";
        
        Debug.Log("[PaddleController] 🎯 Single-point calibration started...");
        OnCalibrationStepChanged?.Invoke("Keep paddle in NEUTRAL position");
    }
    
    void StartMultiPointCalibration()
    {
        isMultiPointCalibrating = true;
        currentCalibrationStep = 0;
        currentStepText = "Langkah 1/3: Tahan paddle di posisi NETRAL";
        
        Debug.Log("[PaddleController] 🎯 3-point calibration started...");
        OnCalibrationStepChanged?.Invoke("Step 1/3: Hold paddle in NEUTRAL position");
        
        StartCoroutine(MultiPointCalibrationRoutine());
    }
    
    IEnumerator MultiPointCalibrationRoutine()
    {
        string[] stepNames = { "NETRAL", "KIRI (-30°)", "KANAN (+30°)" };
        string[] stepInstructions = { 
            "Langkah 1/3: Tahan paddle di posisi NETRAL",
            "Langkah 2/3: Miringkan paddle ke KIRI (-30°)", 
            "Langkah 3/3: Miringkan paddle ke KANAN (+30°)"
        };
        
        for (int step = 0; step < 3; step++)
        {
            currentCalibrationStep = step;
            currentStepText = stepInstructions[step];
            
            Debug.Log($"[PaddleController] Step {step + 1}/3: Position paddle {stepNames[step]}");
            OnCalibrationStepChanged?.Invoke($"Step {step + 1}/3: Hold paddle in {stepNames[step]} position");
            
            yield return new WaitForSeconds(2f);
            
            Vector3 accelSum = Vector3.zero;
            float gyroZSum = 0f;
            
            for (int i = 0; i < calibrationSamples; i++)
            {
                Vector3 accel = bluetoothManager.GetScaledAccel();
                Vector3 gyro = bluetoothManager.GetGyroAngles();
                
                accelSum += accel;
                gyroZSum += gyro.z;
                
                calibrationProgress = Mathf.RoundToInt((float)(step * calibrationSamples + i) / (3 * calibrationSamples) * 100);
                
                yield return new WaitForSeconds(0.02f);
            }
            
            calibrationAccel[step] = accelSum / calibrationSamples;
            calibrationGyroZ[step] = gyroZSum / calibrationSamples;
            
            Debug.Log($"[PaddleController] ✅ Step {step + 1} completed");
        }
        
        FinishMultiPointCalibration();
    }
    
    void UpdateCalibration()
    {
        Vector3 rawAccel = bluetoothManager.GetScaledAccel();
        Vector3 rawGyro = bluetoothManager.GetGyroAngles();
        
        accelSamples[sampleIndex] = rawAccel;
        gyroZSamples[sampleIndex] = rawGyro.z;
        
        sampleIndex++;
        calibrationProgress = Mathf.RoundToInt((float)sampleIndex / calibrationSamples * 100);
        
        if (enableDebugLogs && sampleIndex % 10 == 0)
            Debug.Log($"[PaddleController] Calibration progress: {calibrationProgress}%");
        
        if (sampleIndex >= calibrationSamples)
        {
            FinishSinglePointCalibration();
        }
    }
    
    void FinishSinglePointCalibration()
    {
        Vector3 accelSum = Vector3.zero;
        float gyroZSum = 0f;
        
        for (int i = 0; i < calibrationSamples; i++)
        {
            accelSum += accelSamples[i];
            gyroZSum += gyroZSamples[i];
        }
        
        baselineAccel = accelSum / calibrationSamples;
        baselineGyroZ = gyroZSum / calibrationSamples;
        
        FinishCalibration();
    }
    
    void FinishMultiPointCalibration()
    {
        baselineAccel = calibrationAccel[0];
        baselineGyroZ = calibrationGyroZ[0];
        
        Vector3 leftDiff = calibrationAccel[1] - calibrationAccel[0];
        Vector3 rightDiff = calibrationAccel[2] - calibrationAccel[0];
        
        float leftAngle = Mathf.Atan2(leftDiff.x, leftDiff.y) * RAD_TO_DEG;
        float rightAngle = Mathf.Atan2(rightDiff.x, rightDiff.y) * RAD_TO_DEG;
        
        Debug.Log($"[PaddleController] Left angle: {leftAngle:F1}°, Right angle: {rightAngle:F1}°");
        
        FinishCalibration();
    }
    
    void FinishCalibration()
    {
        zAngle = 0f;
        isCalibrated = true;
        isCalibrating = false;
        isMultiPointCalibrating = false;
        showCalibrationUI = false;
        currentStepText = "Kalibrasi selesai!";
        
        OnCalibrationStatusChanged?.Invoke(false);
        OnCalibrationStepChanged?.Invoke("Calibration completed!");
        
        Debug.Log("[PaddleController] ✅ Calibration completed!");
        Debug.Log($"[PaddleController] 📐 Baseline Accel: {baselineAccel}");
        Debug.Log($"[PaddleController] 🌀 Baseline Gyro Z: {baselineGyroZ:F3}");
        
        float magnitude = baselineAccel.magnitude;
        if (Mathf.Abs(magnitude - GRAVITY) > 2.0f)
        {
            Debug.LogWarning("[PaddleController] ⚠️ Unusual gravity reading. Check sensor orientation!");
        }
    }
    
    #endregion
    
    #region Motion Detection
    
    bool IsSensorStable()
    {
        Vector3 accel = bluetoothManager.GetScaledAccel();
        Vector3 gyro = bluetoothManager.GetGyroAngles();
        
        accelMagnitude = accel.magnitude;
        bool accelStable = (Mathf.Abs(accelMagnitude - GRAVITY) < stabilityThreshold);
        
        bool gyroStable = (Mathf.Abs(gyro.x) < gyroNoiseThreshold && 
                          Mathf.Abs(gyro.y) < gyroNoiseThreshold && 
                          Mathf.Abs(gyro.z) < gyroNoiseThreshold);
        
        float accelScore = Mathf.Max(0f, 1f - Mathf.Abs(accelMagnitude - GRAVITY) / 2f);
        float gyroScore = Mathf.Max(0f, 1f - Mathf.Abs(gyro.z) / 20f);
        stabilityScore = (accelScore + gyroScore) / 2f;
        
        return accelStable && gyroStable;
    }
    
    bool IsIdle()
    {
        if (IsSensorStable())
        {
            if (!isIdleState)
            {
                idleStart = Time.time;
                isIdleState = true;
            }
            return (Time.time - idleStart > idleDuration);
        }
        else
        {
            isIdleState = false;
            lastMovement = Time.time;
            return false;
        }
    }
    
    #endregion
    
    #region Data Processing
    
    void ProcessPaddleData()
    {
        if (IsIdle())
        {
            CorrectDriftWithAccel();
        }
        else
        {
            UpdateAngle();
        }
        
        UpdatePaddleState();
        
        if (enableDebugLogs)
            PrintDebugInfo();
    }
    
    void CorrectDriftWithAccel()
    {
        Vector3 accel = bluetoothManager.GetScaledAccel();
        
        Vector3 accelDiff = accel - baselineAccel;
        tiltAngleZ = Mathf.Atan2(accelDiff.x, accelDiff.y) * RAD_TO_DEG;
        
        driftCorrection = tiltAngleZ - zAngle;
        zAngle = zAngle + (driftCorrection * correctionFactor);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[PaddleController] 🔄 Drift corrected: {driftCorrection:F2}° → Z angle: {zAngle:F2}°");
        }
    }
    
    void UpdateAngle()
    {
        Vector3 gyro = bluetoothManager.GetGyroAngles();
        float currentTime = Time.time;
        float dt = currentTime - lastTime;
        
        correctedGyroZ = gyro.z - baselineGyroZ;
        
        zAngle += correctedGyroZ * dt;
        
        if (zAngle > 180f) zAngle -= 360f;
        if (zAngle < -180f) zAngle += 360f;
        
        lastTime = currentTime;
    }
    
    void UpdatePaddleState()
    {
        finalZAngle = Mathf.Clamp(zAngle, -maxTiltOutput, maxTiltOutput);
        
        PaddleState newState = PaddleState.Neutral;
        int newDirection = 0;
        float newIntensity = 0f;
        
        if (finalZAngle > tiltThreshold)
        {
            newState = PaddleState.TiltRight;
            newDirection = 1;
            newIntensity = Mathf.Min(1f, Mathf.Abs(finalZAngle) / maxTiltOutput);
        }
        else if (finalZAngle < -tiltThreshold)
        {
            newState = PaddleState.TiltLeft;
            newDirection = -1;
            newIntensity = Mathf.Min(1f, Mathf.Abs(finalZAngle) / maxTiltOutput);
        }
        
        if (paddleState != newState)
        {
            paddleState = newState;
            OnPaddleStateChanged?.Invoke(paddleState);
        }
        
        if (paddleDirection != newDirection)
        {
            paddleDirection = newDirection;
            OnTiltDetected?.Invoke(paddleDirection, newIntensity);
        }
        
        tiltIntensity = newIntensity;
        
        OnZAngleChanged?.Invoke(finalZAngle);
    }
    
    #endregion
    
    #region Debug
    
    void PrintDebugInfo()
    {
        if (Time.time % 0.5f < Time.deltaTime)
        {
            Vector3 rawAccel = bluetoothManager.GetScaledAccel();
            Vector3 rawGyro = bluetoothManager.GetGyroAngles();
            
            Debug.Log("═══════════════════════════════════════");
            Debug.Log($"[PaddleController] 📊 Raw Data - Accel: {rawAccel} | Gyro Z: {correctedGyroZ:F2}");
            Debug.Log($"[PaddleController] 📐 Angles - Raw Z: {zAngle:F2}° | Final Z: {finalZAngle:F2}° | Tilt: {tiltAngleZ:F2}°");
            Debug.Log($"[PaddleController] 🎯 State: {paddleState} | Direction: {paddleDirection} | Intensity: {tiltIntensity:F2}");
            Debug.Log($"[PaddleController] 🔧 Status - Idle: {(isIdleState ? "YES" : "NO")} | Stability: {stabilityScore:F2} | Magnitude: {accelMagnitude:F2}");
        }
    }
    
    #endregion
    
    #region Public API
    
    public float GetZAngle() => finalZAngle;
    public int GetPaddleDirection() => paddleDirection;
    public float GetTiltIntensity() => tiltIntensity;
    public PaddleState GetPaddleState() => paddleState;
    public bool IsReady() => isCalibrated && bluetoothManager.IsConnected();
    public bool IsCalibrating() => isCalibrating || isMultiPointCalibrating;
    public int GetCalibrationProgress() => calibrationProgress;
    public float GetStabilityScore() => stabilityScore;
    
    public void SetTiltThreshold(float threshold) => tiltThreshold = threshold;
    public void SetCorrectionFactor(float factor) => correctionFactor = Mathf.Clamp(factor, 0.1f, 0.5f);
    public void SetIdleDuration(float duration) => idleDuration = duration;
    public void SetMaxTiltOutput(float max) => maxTiltOutput = max;
    
    #endregion
    
    #region Unity Inspector Buttons
    
    [ContextMenu("Start Single Point Calibration")]
    public void StartSinglePointCalibrationFromMenu()
    {
        useMultiPoint = false;
        StartCalibration();
    }
    
    [ContextMenu("Start Multi Point Calibration")]
    public void StartMultiPointCalibrationFromMenu()
    {
        useMultiPoint = true;
        StartCalibration();
    }
    
    [ContextMenu("Reset Configuration")]
    public void ResetConfiguration()
    {
        tiltThreshold = 15.0f;
        maxTiltOutput = 45.0f;
        stabilityThreshold = 0.5f;
        gyroNoiseThreshold = 5.0f;
        idleDuration = 2.0f;
        correctionFactor = 0.2f;
        
        Debug.Log("[PaddleController] Configuration reset to defaults");
    }
    
    #endregion
    
    void OnDestroy()
    {
        if (bluetoothManager != null)
        {
            bluetoothManager.OnConnectionChanged -= OnBluetoothConnectionChanged;
        }
    }
}