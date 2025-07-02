using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO.Ports;

public class ESP32GyroController : MonoBehaviour
{
    [Header("Connection Settings")]
    [SerializeField] private int baudRate = 115200;
    [SerializeField] private string comPort = "COM9";
    [SerializeField] private bool autoDetect = true;
    [SerializeField] private float reconnectInterval = 5f;
    
    [Header("Gyro Thresholds")]
    [SerializeField] private float neutralZone = 10f;
    [SerializeField] private float paddleThreshold = 25f;
    [SerializeField] private float swingDetectionTime = 1.0f;
    
    [Header("Accelerometer Game Controls")]
    [SerializeField] private float shakeThreshold = 8000f;
    [SerializeField] private float pauseThreshold = -8000f;
    [SerializeField] private float gameActionCooldown = 2f;
    
    [Header("Idle Detection")]
    [SerializeField] private float idleTimeout = 3f;
    [SerializeField] private float idleThreshold = 5f;
    
    [Header("Paddle IK Mapping")]
    [SerializeField] private float idlePaddleAngle = 0f;
    [SerializeField] private float maxPaddleRotation = 45f;
    [SerializeField] private bool invertPaddleRotation = false;
    
    [Header("Pattern Detection")]
    [SerializeField] private int minSwingsForPattern = 3;
    [SerializeField] private float patternTimeout = 2f;
    [SerializeField] private int maxHistorySize = 50;
    
    [Header("References")]
    [SerializeField] private BoatController boatController;
    [SerializeField] private PaddleIKController paddleController;
    [SerializeField] private GameManager gameManager;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showVisualization = true;
    [SerializeField] private bool enableSpamReduction = true;
    
    // Connection
    private SerialPort serialPort;
    private bool isConnected = false;
    private bool isReconnecting = false;
    
    // Sensor data
    private float currentGyroAngle = 0f;
    private float baselineAngle = 0f;
    private float lastValidAngle = 0f;
    private float currentAccelY = 0f;
    private float lastGameActionTime = 0f;
    
    // Pattern detection
    private List<SwingData> swingHistory = new List<SwingData>();
    private SwingDirection lastSwingDirection = SwingDirection.None;
    private float lastSwingTime = 0f;
    private float lastMovementTime = 0f;
    private bool isCalibrating = false;
    
    // States
    private PaddlePattern currentPattern = PaddlePattern.Idle;
    private PaddlePattern lastLoggedPattern = PaddlePattern.Idle;
    private float lastPatternTime = 0f;
    
    public enum SwingDirection { Left, Right, None }
    public enum PaddlePattern { Idle, Forward, TurnLeft, TurnRight }
    
    private struct SwingData
    {
        public SwingDirection direction;
        public float timestamp;
        public float amplitude;
        
        public SwingData(SwingDirection dir, float time, float amp)
        {
            direction = dir;
            timestamp = time;
            amplitude = amp;
        }
    }
    
    void Start()
    {
        InitializeConnection();
        baselineAngle = 0f;
        lastMovementTime = Time.time;
        
        if (gameManager == null)
            gameManager = GameManager.Instance ?? FindObjectOfType<GameManager>();
        
        StartCoroutine(ReadSerialDataCoroutine());
    }
    
    void Update()
    {
        DetectIdleCalibration();
        DetectSwingPattern();
        UpdatePaddleController();
        CleanupHistory();
    }
    
    void InitializeConnection()
    {
        if (autoDetect)
            StartCoroutine(FindESP32Coroutine());
        else
            ConnectToPort(comPort);
    }
    
    IEnumerator FindESP32Coroutine()
    {
        string[] ports = SerialPort.GetPortNames();
        
        foreach (string port in ports)
        {
            if (port == "COM1" || port == "COM2") continue;
            
            if (TestPort(port))
            {
                DebugLog($"ESP32 found on {port}");
                ConnectToPort(port);
                yield break;
            }
            
            yield return new WaitForSeconds(0.1f);
        }
        
        DebugLog("ESP32 not found, retrying...");
        yield return new WaitForSeconds(reconnectInterval);
        
        if (!isConnected)
            StartCoroutine(FindESP32Coroutine());
    }
    
    bool TestPort(string port)
    {
        SerialPort test = null;
        try
        {
            test = new SerialPort(port, baudRate);
            test.ReadTimeout = 1000;
            test.WriteTimeout = 1000;
            test.Open();
            
            System.Threading.Thread.Sleep(500);
            
            if (test.BytesToRead > 0)
            {
                string data = test.ReadExisting();
                return data.Contains("G:") && data.Contains("A:");
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Test port {port} failed: {e.Message}");
        }
        finally
        {
            try { test?.Close(); } catch { }
        }
        return false;
    }
    
    void ConnectToPort(string port)
    {
        try
        {
            DisconnectSerial();
            
            serialPort = new SerialPort(port, baudRate);
            serialPort.ReadTimeout = 100;
            serialPort.WriteTimeout = 100;
            serialPort.Open();
            
            isConnected = true;
            isReconnecting = false;
            DebugLog($"Connected to ESP32 on {port}");
        }
        catch (System.Exception e)
        {
            DebugLog($"Connection failed: {e.Message}");
            isConnected = false;
            
            if (!isReconnecting)
                StartCoroutine(ReconnectCoroutine());
        }
    }
    
    IEnumerator ReconnectCoroutine()
    {
        isReconnecting = true;
        yield return new WaitForSeconds(reconnectInterval);
        
        if (!isConnected)
        {
            DebugLog("Attempting to reconnect...");
            InitializeConnection();
        }
    }
    
    IEnumerator ReadSerialDataCoroutine()
    {
        while (true)
        {
            if (isConnected && serialPort != null && serialPort.IsOpen)
            {
                try
                {
                    if (serialPort.BytesToRead > 0)
                    {
                        string data = serialPort.ReadExisting();
                        ParseSimplifiedData(data);
                    }
                }
                catch (System.Exception e)
                {
                    DebugLog($"Read error: {e.Message}");
                    isConnected = false;
                    
                    if (!isReconnecting)
                        StartCoroutine(ReconnectCoroutine());
                }
            }
            
            yield return new WaitForEndOfFrame();
        }
    }
    
    void ParseSimplifiedData(string data)
    {
        if (string.IsNullOrEmpty(data)) return;
        
        string[] lines = data.Split('\n');
        
        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            
            // Parse simplified format: G:-6.9,A:2267
            if (trimmed.StartsWith("G:"))
            {
                ParseSimplifiedFormat(trimmed);
            }
        }
    }
    
    void ParseSimplifiedFormat(string dataLine)
    {
        try
        {
            // Split by comma: G:-6.9,A:2267
            string[] parts = dataLine.Split(',');
            
            if (parts.Length >= 2)
            {
                // Extract Gyro X
                if (parts[0].StartsWith("G:"))
                {
                    string gyroStr = parts[0].Substring(2);
                    if (float.TryParse(gyroStr, out float gyroX))
                    {
                        ProcessNewAngle(gyroX);
                        lastValidAngle = gyroX;
                    }
                }
                
                // Extract Accel Y
                if (parts[1].StartsWith("A:"))
                {
                    string accelStr = parts[1].Substring(2);
                    if (float.TryParse(accelStr, out float accelY))
                    {
                        ProcessAccelerometerY(accelY);
                        currentAccelY = accelY;
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Parse error: {e.Message}");
        }
    }
    
    void ProcessNewAngle(float angle)
    {
        currentGyroAngle = angle;
        
        float relativeAngle = angle - baselineAngle;
        
        while (relativeAngle > 180f) relativeAngle -= 360f;
        while (relativeAngle < -180f) relativeAngle += 360f;
        
        if (Mathf.Abs(relativeAngle) > idleThreshold)
            lastMovementTime = Time.time;
        
        DetectSwing(relativeAngle);
        
        DebugLog($"Gyro: {angle:F1}°, Relative: {relativeAngle:F1}°");
    }
    
    void ProcessAccelerometerY(float accelY)
    {
        if (Time.time - lastGameActionTime < gameActionCooldown) return;
        
        if (accelY > shakeThreshold)
        {
            if (gameManager != null)
            {
                gameManager.RestartLevel();
                lastGameActionTime = Time.time;
                DebugLog($"Game restart: {accelY:F0}");
            }
        }
        else if (accelY < pauseThreshold)
        {
            if (gameManager != null)
            {
                gameManager.PauseGame();
                lastGameActionTime = Time.time;
                DebugLog($"Game pause: {accelY:F0}");
            }
        }
    }
    
    void DetectIdleCalibration()
    {
        float timeSinceMovement = Time.time - lastMovementTime;
        
        if (timeSinceMovement >= idleTimeout && !isCalibrating)
            StartCalibration();
    }
    
    void StartCalibration()
    {
        isCalibrating = true;
        baselineAngle = lastValidAngle;
        
        DebugLog($"Baseline calibrated: {baselineAngle:F1}°");
        
        currentPattern = PaddlePattern.Idle;
        swingHistory.Clear();
        
        if (this != null && gameObject.activeInHierarchy)
            StartCoroutine(EndCalibrationCoroutine());
    }
    
    IEnumerator EndCalibrationCoroutine()
    {
        yield return new WaitForSeconds(0.5f);
        isCalibrating = false;
    }
    
    void DetectSwing(float relativeAngle)
    {
        if (isCalibrating) return;
        
        SwingDirection currentDirection = SwingDirection.None;
        
        if (relativeAngle > paddleThreshold)
            currentDirection = SwingDirection.Right;
        else if (relativeAngle < -paddleThreshold)
            currentDirection = SwingDirection.Left;
        
        if (currentDirection != SwingDirection.None && currentDirection != lastSwingDirection)
        {
            swingHistory.Add(new SwingData(currentDirection, Time.time, Mathf.Abs(relativeAngle)));
            lastSwingDirection = currentDirection;
            lastSwingTime = Time.time;
            
            DebugLog($"Swing: {currentDirection}, Amp: {Mathf.Abs(relativeAngle):F1}°");
        }
    }
    
    void DetectSwingPattern()
    {
        if (swingHistory.Count < 2) return;
        
        int leftSwings = 0;
        int rightSwings = 0;
        int alternatingSwings = 0;
        
        float currentTime = Time.time;
        List<SwingData> recentSwings = new List<SwingData>();
        
        foreach (var swing in swingHistory)
        {
            if (currentTime - swing.timestamp <= swingDetectionTime)
                recentSwings.Add(swing);
        }
        
        if (recentSwings.Count < 2) return;
        
        for (int i = 0; i < recentSwings.Count; i++)
        {
            if (recentSwings[i].direction == SwingDirection.Left) leftSwings++;
            if (recentSwings[i].direction == SwingDirection.Right) rightSwings++;
            
            if (i > 0 && recentSwings[i].direction != recentSwings[i-1].direction)
                alternatingSwings++;
        }
        
        PaddlePattern newPattern = DeterminePattern(leftSwings, rightSwings, alternatingSwings, recentSwings.Count);
        
        if (newPattern != currentPattern)
        {
            currentPattern = newPattern;
            lastPatternTime = Time.time;
            
            DebugLog($"Pattern: {currentPattern}");
            TriggerPaddleAction(newPattern);
        }
    }
    
    PaddlePattern DeterminePattern(int leftSwings, int rightSwings, int alternatingSwings, int totalSwings)
    {
        if (alternatingSwings >= minSwingsForPattern && totalSwings >= minSwingsForPattern)
            return PaddlePattern.Forward;
        
        if (leftSwings >= minSwingsForPattern && rightSwings <= 1)
            return PaddlePattern.TurnRight;
        
        if (rightSwings >= minSwingsForPattern && leftSwings <= 1)
            return PaddlePattern.TurnLeft;
        
        return PaddlePattern.Idle;
    }
    
    void TriggerPaddleAction(PaddlePattern pattern)
    {
        if (boatController == null) return;
        
        switch (pattern)
        {
            case PaddlePattern.Forward:
                if (lastSwingDirection == SwingDirection.Left)
                    boatController.PaddleLeft();
                else
                    boatController.PaddleRight();
                break;
                
            case PaddlePattern.TurnLeft:
                boatController.PaddleRight();
                break;
                
            case PaddlePattern.TurnRight:
                boatController.PaddleLeft();
                break;
        }
    }
    
    void UpdatePaddleController()
    {
        if (paddleController == null) return;
        
        float relativeAngle = currentGyroAngle - baselineAngle;
        
        while (relativeAngle > 180f) relativeAngle -= 360f;
        while (relativeAngle < -180f) relativeAngle += 360f;
        
        float mappedAngle = MapToPaddleRange(relativeAngle);
        paddleController.SetRawAngle(mappedAngle);
        
        // Only update pattern when changed
        if (currentPattern != lastLoggedPattern || !enableSpamReduction)
        {
            switch (currentPattern)
            {
                case PaddlePattern.Forward:
                    paddleController.ForcePattern((int)PaddleIKController.PaddlePattern.Alternating);
                    break;
                    
                case PaddlePattern.TurnLeft:
                    paddleController.ForcePattern((int)PaddleIKController.PaddlePattern.ConsecutiveRight);
                    break;
                    
                case PaddlePattern.TurnRight:
                    paddleController.ForcePattern((int)PaddleIKController.PaddlePattern.ConsecutiveLeft);
                    break;
                    
                default:
                    paddleController.ForcePattern((int)PaddleIKController.PaddlePattern.None);
                    break;
            }
            
            lastLoggedPattern = currentPattern;
        }
    }
    
    float MapToPaddleRange(float gyroAngle)
    {
        float clampedAngle = Mathf.Clamp(gyroAngle, -90f, 90f);
        float mappedAngle = (clampedAngle / 90f) * maxPaddleRotation;
        
        if (invertPaddleRotation)
            mappedAngle = -mappedAngle;
        
        return mappedAngle + idlePaddleAngle;
    }
    
    void CleanupHistory()
    {
        float currentTime = Time.time;
        
        swingHistory.RemoveAll(swing => currentTime - swing.timestamp > patternTimeout);
        
        if (swingHistory.Count > maxHistorySize)
            swingHistory.RemoveRange(0, swingHistory.Count - maxHistorySize);
        
        if (currentTime - lastSwingTime > patternTimeout && currentPattern != PaddlePattern.Idle)
        {
            currentPattern = PaddlePattern.Idle;
            DebugLog("Pattern reset to Idle");
        }
    }
    
    void DisconnectSerial()
    {
        try
        {
            if (serialPort != null && serialPort.IsOpen)
                serialPort.Close();
        }
        catch (System.Exception e)
        {
            DebugLog($"Disconnect error: {e.Message}");
        }
        finally
        {
            serialPort = null;
            isConnected = false;
        }
    }
    
    void OnApplicationQuit()
    {
        StopAllCoroutines();
        DisconnectSerial();
    }
    
    void OnDestroy()
    {
        StopAllCoroutines();
        DisconnectSerial();
    }
    
    // Public methods
    [ContextMenu("Calibrate Baseline")]
    public void ManualCalibrate() => StartCalibration();
    
    [ContextMenu("Reset Pattern")]
    public void ResetPattern()
    {
        currentPattern = PaddlePattern.Idle;
        swingHistory.Clear();
        lastSwingDirection = SwingDirection.None;
    }
    
    [ContextMenu("Reconnect")]
    public void ManualReconnect()
    {
        DisconnectSerial();
        InitializeConnection();
    }
    
    // Getters
    public bool IsConnected() => isConnected;
    public float GetCurrentAngle() => currentGyroAngle;
    public float GetBaselineAngle() => baselineAngle;
    public float GetCurrentAccelY() => currentAccelY;
    public PaddlePattern GetCurrentPattern() => currentPattern;
    public int GetSwingCount() => swingHistory.Count;
    
    void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[ESP32GyroController] {message}");
    }
    
    // GUI
    void OnGUI()
    {
        if (!showVisualization) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 350, 300));
        GUILayout.Box("ESP32 Gyro Controller");
        
        GUILayout.Label($"Connected: {isConnected}");
        GUILayout.Label($"Gyro: {currentGyroAngle:F1}°");
        GUILayout.Label($"Baseline: {baselineAngle:F1}°");
        
        float relativeAngle = currentGyroAngle - baselineAngle;
        while (relativeAngle > 180f) relativeAngle -= 360f;
        while (relativeAngle < -180f) relativeAngle += 360f;
        
        GUILayout.Label($"Relative: {relativeAngle:F1}°");
        GUILayout.Label($"Paddle: {MapToPaddleRange(relativeAngle):F1}°");
        GUILayout.Label($"Accel Y: {currentAccelY:F0}");
        GUILayout.Label($"Pattern: {currentPattern}");
        GUILayout.Label($"Swings: {swingHistory.Count}");
        
        if (GUILayout.Button("Calibrate")) ManualCalibrate();
        if (GUILayout.Button("Reconnect")) ManualReconnect();
        if (GUILayout.Button("Test Restart")) ProcessAccelerometerY(shakeThreshold + 100);
        
        GUILayout.EndArea();
    }
}