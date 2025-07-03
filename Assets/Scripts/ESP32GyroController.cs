using UnityEngine;
using ArduinoBluetoothAPI;
using System;
using System.Collections;

public class ESP32GyroController : MonoBehaviour
{
    [Header("Bluetooth Settings")]
    public string deviceName = "ferizy-paddle";
    public bool autoConnect = true;
    public float connectionTimeout = 30f;
    
    [Header("Multi-Axis Data Processing")]
    public float dataSmoothing = 0.5f;
    public Vector3 axisWeights = new Vector3(0.3f, 0.2f, 0.5f); // X,Y,Z weights
    
    [Header("Thresholds")]
    public float gyroXThreshold = 15f;        // Primary detection
    public float gyroYThreshold = 10f;        // Paddle depth validation
    public float gyroZThreshold = 5f;         // Roll stability
    public float totalMovementThreshold = 25f; // Combined movement
    
    [Header("Idle Settings")]
    public bool maintainIdleAngle = true;
    [Range(0.01f, 1.0f)]
    public float idleReturnSpeed = 0.05f;
    private float idleBaseAngle = 0f;
    
    [Header("Integration")]
    public PaddleIKController paddleController;
    public GyroPatternDetector patternDetector;
    
    [Header("Debug")]
    public bool enableDebugLogs = true;
    public GameObject connectionIndicator;
    public UnityEngine.UI.Text debugText;
    
    // Connection state
    private BluetoothHelper bluetoothHelper;
    private bool isConnecting = false;
    private bool isScanning = false;
    private float connectionTimer = 0f;
    
    // Multi-axis data values
    private Vector3 currentGyro = Vector3.zero;
    private int currentAccelY = 0;
    private Vector3 smoothedGyro = Vector3.zero;
    private float combinedMovement = 0f;
    
    // Packet tracking
    private string lastPacket = "";
    private float lastDataTime = 0f;
    private int packetsReceived = 0;
    private int validPackets = 0;
    private int errorPackets = 0;
    private int heartbeatPackets = 0;
    
    // Connection recovery
    private int connectionRetries = 0;
    private const int maxConnectionRetries = 5;
    private float lastConnectionAttempt = 0f;
    private const float retryDelay = 3f;
    
    void Start()
    {
        UpdateDebugText("Starting ESP32 controller...");
        CreateIndicatorIfNeeded();
        InitializeBluetooth();
        
        if (autoConnect)
            StartCoroutine(AutoConnectRoutine());
    }
    
    void Update()
    {
        UpdateConnectionIndicator();
        
        // Update pattern detector with multi-axis data
        if (patternDetector != null && IsConnected())
        {
            patternDetector.UpdateGyroData(currentGyro, currentAccelY);
        }
        
        // Update paddle controller
        if (paddleController != null && IsConnected())
        {
            // Apply smoothing to all axes
            smoothedGyro = Vector3.Lerp(smoothedGyro, currentGyro, dataSmoothing * Time.deltaTime * 10f);
            
            // Calculate combined movement for pattern detection
            combinedMovement = (smoothedGyro.x * axisWeights.x) + 
                              (smoothedGyro.y * axisWeights.y) + 
                              (smoothedGyro.z * axisWeights.z);
            
            // Send Z-axis to paddle (primary visual axis)
            paddleController.SetRawAngle(-smoothedGyro.z); // Negate for correct direction
            
            // Handle idle angle adaptation
            if (maintainIdleAngle && IsMovementStable())
            {
                idleBaseAngle = Mathf.Lerp(idleBaseAngle, smoothedGyro.z, idleReturnSpeed);
            }
        }
        
        // Connection timeout handling
        if (isConnecting)
        {
            connectionTimer += Time.deltaTime;
            if (connectionTimer > connectionTimeout)
            {
                HandleConnectionTimeout();
            }
        }
        
        // Check data freshness
        if (IsConnected() && Time.time - lastDataTime > 5f && lastDataTime > 0f)
        {
            DebugLog("⚠️ No data for 5 seconds - connection may be lost");
            HandleConnectionLoss();
        }
    }
    
    private void CreateIndicatorIfNeeded()
    {
        if (connectionIndicator == null)
        {
            connectionIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            connectionIndicator.name = "ESP32_ConnectionIndicator";
            connectionIndicator.transform.position = Vector3.up * 2f;
            connectionIndicator.transform.localScale = Vector3.one * 0.3f;
        }
    }
    
    private void InitializeBluetooth()
    {
        try
        {
            DebugLog("Initializing Bluetooth for ESP32 30-char format...");
            
            BluetoothHelper.BLE = false; // Classic Bluetooth for ESP32
            bluetoothHelper = BluetoothHelper.GetInstance(deviceName);
            
            // Register event handlers
            bluetoothHelper.OnConnected += OnConnected;
            bluetoothHelper.OnConnectionFailed += OnConnectionFailed;
            bluetoothHelper.OnDataReceived += OnDataReceived;
            bluetoothHelper.OnScanEnded += OnScanEnded;
            
            // Configure for ESP32 data format with \n terminator
            bluetoothHelper.setTerminatorBasedStream("\n");
            
            DebugLog("✓ Bluetooth initialized for ESP32 format");
        }
        catch (Exception ex)
        {
            DebugLog($"✗ Bluetooth init error: {ex.Message}");
            UpdateDebugText($"Init Error: {ex.Message}");
        }
    }
    
    private IEnumerator AutoConnectRoutine()
    {
        yield return new WaitForSeconds(1f);
        ConnectToDevice();
    }
    
    public void ConnectToDevice()
    {
        if (bluetoothHelper == null || isConnecting) return;
        
        if (bluetoothHelper.isConnected())
        {
            DebugLog("Already connected");
            return;
        }
        
        if (connectionRetries >= maxConnectionRetries)
        {
            DebugLog($"Max connection retries ({maxConnectionRetries}) reached");
            return;
        }
        
        if (Time.time - lastConnectionAttempt < retryDelay)
        {
            DebugLog("Waiting for retry delay...");
            return;
        }
        
        try
        {
            DebugLog($"Connecting to {deviceName} (attempt {connectionRetries + 1})...");
            lastConnectionAttempt = Time.time;
            
            isScanning = bluetoothHelper.ScanNearbyDevices();
            
            if (!isScanning)
            {
                DebugLog("Direct connection attempt...");
                isConnecting = true;
                connectionTimer = 0f;
                bluetoothHelper.Connect();
            }
            
            connectionRetries++;
        }
        catch (Exception ex)
        {
            DebugLog($"Connection error: {ex.Message}");
            HandleConnectionError();
        }
    }
    
    public void DisconnectBluetooth()
    {
        if (bluetoothHelper != null && bluetoothHelper.isConnected())
        {
            bluetoothHelper.Disconnect();
            DebugLog("Manually disconnected");
        }
        
        ResetConnectionState();
    }
    
    private void OnScanEnded(BluetoothHelper helper, System.Collections.Generic.LinkedList<BluetoothDevice> devices)
    {
        DebugLog($"Scan ended - {devices.Count} devices found");
        isScanning = false;
        
        if (helper.isDevicePaired())
        {
            DebugLog("Device paired - connecting...");
            isConnecting = true;
            connectionTimer = 0f;
            helper.Connect();
        }
        else
        {
            DebugLog("Device not paired - retrying...");
            StartCoroutine(RetryScanAfterDelay());
        }
    }
    
    private IEnumerator RetryScanAfterDelay()
    {
        yield return new WaitForSeconds(retryDelay);
        if (!bluetoothHelper.isConnected() && connectionRetries < maxConnectionRetries)
        {
            ConnectToDevice();
        }
    }
    
    private void OnConnected(BluetoothHelper helper)
    {
        DebugLog($"✓ Connected to {deviceName}");
        isConnecting = false;
        connectionRetries = 0; // Reset retry counter
        
        try
        {
            helper.StartListening();
            DebugLog("Listening for ESP32 30-char packets...");
            UpdateDebugText("Connected - Listening for data");
        }
        catch (Exception ex)
        {
            DebugLog($"Error starting listener: {ex.Message}");
        }
    }
    
    private void OnConnectionFailed(BluetoothHelper helper)
    {
        DebugLog("✗ Connection failed");
        HandleConnectionError();
    }
    
    private void OnDataReceived(BluetoothHelper helper)
    {
        string data = helper.Read();
        lastPacket = data.Trim(); // Remove whitespace
        lastDataTime = Time.time;
        packetsReceived++;
        
        ProcessESP32Data(lastPacket);
    }
    
    private void ProcessESP32Data(string data)
    {
        try
        {
            // Validate packet length - ESP32 sends exactly 30 chars
            if (string.IsNullOrEmpty(data))
            {
                errorPackets++;
                DebugLog("Empty packet received");
                return;
            }
            
            // Handle length variations due to network issues
            if (data.Length < 29 || data.Length > 31)
            {
                errorPackets++;
                DebugLog($"Invalid packet length: '{data}' (len={data.Length}, expected 30)");
                return;
            }
            
            // Trim to exactly 30 chars if needed
            if (data.Length > 30)
            {
                data = data.Substring(0, 30);
            }
            
            // Basic format validation for ESP32 format: G+012.3X+045.6Y-023.1A+1234C9N
            if (!data.StartsWith("G") || !data.Contains("X") || !data.Contains("Y") || 
                !data.Contains("A") || !data.Contains("C") || !data.EndsWith("N"))
            {
                errorPackets++;
                DebugLog($"Invalid packet format: '{data}'");
                return;
            }
            
            // Extract and parse gyro data
            if (!ExtractESP32GyroData(data))
            {
                errorPackets++;
                return;
            }
            
            validPackets++;
            
            // Detect heartbeat (all values near zero)
            bool isHeartbeat = (Mathf.Abs(currentGyro.x) < 0.1f && 
                               Mathf.Abs(currentGyro.y) < 0.1f && 
                               Mathf.Abs(currentGyro.z) < 0.1f && 
                               Mathf.Abs(currentAccelY) < 10);
            
            if (isHeartbeat)
            {
                heartbeatPackets++;
                DebugLog("💓 Heartbeat detected");
            }
            
            // Update debug display
            UpdateDebugText($"Gyro X:{currentGyro.x:F1}° Y:{currentGyro.y:F1}° Z:{currentGyro.z:F1}°\n" +
                           $"AccelY:{currentAccelY}\n" +
                           $"Combined:{combinedMovement:F1}°\n" +
                           $"Packets: ✅{validPackets}/{packetsReceived} ❌{errorPackets} 💓{heartbeatPackets}");
            
        }
        catch (Exception ex)
        {
            errorPackets++;
            DebugLog($"Data processing error: {ex.Message}");
        }
    }
    
    private bool ExtractESP32GyroData(string data)
    {
        try
        {
            // Parse ESP32 format: G+012.3X+045.6Y-023.1A+1234C9N
            //                     0123456789012345678901234567890
            
            if (data.Length != 30)
            {
                DebugLog($"Exact length mismatch: {data.Length} != 30");
                return false;
            }
            
            // Extract components with bounds checking
            string gyroXStr = SafeSubstring(data, 1, 6);   // +012.3
            string gyroYStr = SafeSubstring(data, 8, 6);   // +045.6
            string gyroZStr = SafeSubstring(data, 15, 6);  // -023.1
            string accelYStr = SafeSubstring(data, 22, 5); // +1234
            char checksumChar = data.Length > 28 ? data[28] : '0';
            
            if (string.IsNullOrEmpty(gyroXStr) || string.IsNullOrEmpty(gyroYStr) || 
                string.IsNullOrEmpty(gyroZStr) || string.IsNullOrEmpty(accelYStr))
            {
                DebugLog("Failed to extract data components");
                return false;
            }
            
            // Parse values with error handling
            if (!float.TryParse(gyroXStr, out float gyroX) ||
                !float.TryParse(gyroYStr, out float gyroY) ||
                !float.TryParse(gyroZStr, out float gyroZ) ||
                !int.TryParse(accelYStr, out int accelY))
            {
                DebugLog($"Parse failed - X:'{gyroXStr}' Y:'{gyroYStr}' Z:'{gyroZStr}' A:'{accelYStr}'");
                return false;
            }
            
            // Optional checksum validation
            if (!ValidateChecksum(data, checksumChar))
            {
                DebugLog($"Checksum warning for: '{data}'");
                // Don't fail - just log warning
            }
            
            // Update current values
            currentGyro = new Vector3(gyroX, gyroY, gyroZ);
            currentAccelY = accelY;
            
            return true;
        }
        catch (Exception ex)
        {
            DebugLog($"Parsing error: {ex.Message}");
            return false;
        }
    }
    
    private string SafeSubstring(string str, int startIndex, int length)
    {
        if (str == null || startIndex < 0 || startIndex >= str.Length)
            return "";
        
        int actualLength = Mathf.Min(length, str.Length - startIndex);
        return str.Substring(startIndex, actualLength);
    }
    
    private bool ValidateChecksum(string data, char checksumChar)
    {
        try
        {
            // Calculate checksum for first 27 characters (ESP32 compatible)
            int calculatedSum = 0;
            for (int i = 0; i < 27 && i < data.Length; i++)
            {
                calculatedSum += data[i];
            }
            
            int expectedChecksum = calculatedSum % 10;
            
            if (!int.TryParse(checksumChar.ToString(), out int receivedChecksum))
            {
                return false;
            }
            
            return expectedChecksum == receivedChecksum;
        }
        catch
        {
            return false;
        }
    }
    
    private bool IsMovementStable()
    {
        return Vector3.Magnitude(currentGyro) < 5.0f;
    }
    
    private void HandleConnectionTimeout()
    {
        DebugLog("Connection timeout");
        HandleConnectionError();
    }
    
    private void HandleConnectionLoss()
    {
        DebugLog("Connection lost - attempting reconnection");
        ResetConnectionState();
        StartCoroutine(ReconnectAfterDelay());
    }
    
    private void HandleConnectionError()
    {
        isConnecting = false;
        StartCoroutine(ReconnectAfterDelay());
    }
    
    private IEnumerator ReconnectAfterDelay()
    {
        yield return new WaitForSeconds(retryDelay);
        if (!bluetoothHelper.isConnected() && connectionRetries < maxConnectionRetries)
        {
            ConnectToDevice();
        }
    }
    
    private void ResetConnectionState()
    {
        isConnecting = false;
        isScanning = false;
        connectionTimer = 0f;
    }
    
    private void UpdateConnectionIndicator()
    {
        if (connectionIndicator == null) return;
        
        Renderer renderer = connectionIndicator.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (IsConnected())
            {
                renderer.material.color = Color.green;
            }
            else if (isConnecting || isScanning)
            {
                renderer.material.color = Color.yellow;
            }
            else
            {
                renderer.material.color = Color.red;
            }
        }
    }
    
    private void UpdateDebugText(string text)
    {
        if (debugText != null)
        {
            debugText.text = text;
        }
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ESP32] {message}");
        }
    }
    
    // Public API
    public bool IsConnected() => bluetoothHelper != null && bluetoothHelper.isConnected();
    public Vector3 GetCurrentGyro() => currentGyro;
    public Vector3 GetSmoothedGyro() => smoothedGyro;
    public int GetCurrentAccelY() => currentAccelY;
    public float GetCombinedMovement() => combinedMovement;
    public string GetLastPacket() => lastPacket;
    public int GetPacketsReceived() => packetsReceived;
    public int GetValidPackets() => validPackets;
    public int GetErrorPackets() => errorPackets;
    public int GetHeartbeatPackets() => heartbeatPackets;
    public float GetValidPacketRatio() => packetsReceived > 0 ? (float)validPackets / packetsReceived : 0f;
    
    // Legacy compatibility (returns Z-axis)
    public float GetGyroValue() => currentGyro.z;
    public float GetSmoothedGyroValue() => smoothedGyro.z;
    
    void OnDestroy()
    {
        if (bluetoothHelper != null)
        {
            bluetoothHelper.Disconnect();
        }
    }
    
    void OnGUI()
    {
        // Bluetooth helper GUI
        if (bluetoothHelper != null)
            bluetoothHelper.DrawGUI();
        
        // Manual connection buttons
        if (!IsConnected() && GUI.Button(new Rect(10, Screen.height - 60, 120, 25), "Manual Connect"))
        {
            connectionRetries = 0;
            ConnectToDevice();
        }
        
        if (IsConnected() && GUI.Button(new Rect(140, Screen.height - 60, 120, 25), "Disconnect"))
        {
            DisconnectBluetooth();
        }
        
        // Enhanced status display
        if (IsConnected())
        {
            string statusColor = IsConnected() ? "🟢" : isConnecting ? "🟡" : "🔴";
            float validRatio = packetsReceived > 0 ? (float)validPackets / packetsReceived * 100f : 0f;
            float timeSinceData = Time.time - lastDataTime;
            
            GUI.Label(new Rect(10, 10, 400, 20), $"ESP32 Status: {statusColor} Connected");
            GUI.Label(new Rect(10, 30, 400, 20), $"Packets: ✅{validPackets}/{packetsReceived} ({validRatio:F1}%) | ❌{errorPackets} | 💓{heartbeatPackets}");
            GUI.Label(new Rect(10, 50, 400, 20), $"Last data: {timeSinceData:F1}s ago");
            
            if (validPackets > 0)
            {
                GUI.Label(new Rect(10, 70, 400, 20), $"Gyro: X={currentGyro.x:F1}° Y={currentGyro.y:F1}° Z={currentGyro.z:F1}°");
                GUI.Label(new Rect(10, 90, 400, 20), $"AccelY: {currentAccelY}");
                GUI.Label(new Rect(10, 110, 300, 20), $"Raw: '{lastPacket}'");
            }
        }
        else
        {
            GUI.Label(new Rect(10, 10, 400, 20), $"ESP32 Status: Disconnected (Retry: {connectionRetries}/{maxConnectionRetries})");
        }
    }
}