using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ArduinoBluetoothAPI;
using System;

public class ESP32BluetoothController : MonoBehaviour
{
    [Header("Bluetooth Settings")]
    [SerializeField] private string deviceName = "ferizy";
    [SerializeField] private bool autoConnect = true;
    [SerializeField] private float connectionTimeout = 10f;
    
    [Header("Paddle Detection")]
    [SerializeField] private float leftThreshold = -15f;
    [SerializeField] private float rightThreshold = 15f;
    [SerializeField] private float neutralThreshold = 5f;
    [SerializeField] private float debounceTime = 0.3f;
    
    [Header("References")]
    [SerializeField] private BoatController boatController;
    [SerializeField] private UIManager uiManager;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    // Bluetooth helper instance
    private BluetoothHelper helper;
    
    // Status tracking
    public bool isConnected = false;
    public float currentGyro = 0f;
    public int currentAccel = 0;
    
    // Paddle state
    private bool isLeftPaddle = false;
    private bool isRightPaddle = false;
    private float lastLeftTime = 0f;
    private float lastRightTime = 0f;
    
    // Connection management
    private bool isConnecting = false;
    private bool isScanning = false;
    private float connectionStartTime = 0f;
    private LinkedList<BluetoothDevice> discoveredDevices;
    private int connectionRetryCount = 0;
    private const int maxRetries = 3;
    
    private void Start()
    {
        InitializeBluetooth();
    }
    
    private void InitializeBluetooth()
    {
        try
        {
            DebugLog("Initializing Bluetooth for ESP32 Classic Bluetooth...");
            
            // Configure for Classic Bluetooth (not BLE)
            BluetoothHelper.BLE = false;
            BluetoothHelper.ASYNC_EVENTS = true;
            BluetoothHelper.BLUETOOTH_SIMULATION = false;
            
            helper = BluetoothHelper.GetInstance();
            
            if (helper == null)
            {
                DebugLog("ERROR: Failed to get BluetoothHelper instance");
                UpdateConnectionStatus("Bluetooth initialization failed");
                return;
            }
            
            // Subscribe to events
            helper.OnConnected += OnBluetoothConnected;
            helper.OnConnectionFailed += OnBluetoothConnectionFailed;
            helper.OnDataReceived += OnBluetoothDataReceived;
            helper.OnScanEnded += OnScanEnded;
            
            // Configure for line-based communication
            helper.setTerminatorBasedStream("\n");
            helper.setLengthBasedStream();
            
            UpdateConnectionStatus("Bluetooth initialized. Searching for ESP32...");
            DebugLog("Bluetooth helper initialized successfully");
            
            if (autoConnect)
            {
                StartCoroutine(StartConnectionProcess());
            }
        }
        catch (Exception e)
        {
            DebugLog($"Bluetooth initialization failed: {e.Message}");
            UpdateConnectionStatus($"Init failed: {e.Message}");
            EnableKeyboardFallback();
        }
    }
    
    private IEnumerator StartConnectionProcess()
    {
        yield return new WaitForSeconds(1f);
        
        // Try direct connection first
        yield return StartCoroutine(TryDirectConnection());
        
        if (!isConnected)
        {
            // Then scan for devices
            yield return StartCoroutine(ScanAndConnect());
        }
    }
    
    private IEnumerator TryDirectConnection()
    {
        DebugLog("Attempting direct connection to paired device...");
        UpdateConnectionStatus("Connecting to paired ESP32...");
        
        bool connectionFailed = false;
        
        try
        {
            helper.setDeviceName(deviceName);
            helper.Connect();
            
            isConnecting = true;
            connectionStartTime = Time.time;
        }
        catch (Exception e)
        {
            DebugLog($"Direct connection failed: {e.Message}");
            isConnecting = false;
            connectionFailed = true;
        }
        
        if (!connectionFailed)
        {
            // Wait for connection result
            while (isConnecting && (Time.time - connectionStartTime) < connectionTimeout)
            {
                yield return new WaitForSeconds(0.1f);
            }
            
            if (!isConnected)
            {
                DebugLog("Direct connection timed out");
                isConnecting = false;
            }
        }
    }
    
    private IEnumerator ScanAndConnect()
    {
        DebugLog("Starting device scan...");
        UpdateConnectionStatus("Scanning for ESP32 devices...");
        
        bool scanFailed = false;
        
        try
        {
            isScanning = true;
            bool scanStarted = helper.ScanNearbyDevices();
            
            if (!scanStarted)
            {
                DebugLog("Failed to start scan");
                isScanning = false;
                scanFailed = true;
            }
        }
        catch (Exception e)
        {
            DebugLog($"Scan failed: {e.Message}");
            isScanning = false;
            scanFailed = true;
        }
        
        if (scanFailed)
        {
            yield return StartCoroutine(RetryConnectionCoroutine());
            yield break;
        }
        
        // Wait for scan to complete
        float scanStartTime = Time.time;
        while (isScanning && (Time.time - scanStartTime) < 15f)
        {
            yield return new WaitForSeconds(0.5f);
        }
        
        if (isScanning)
        {
            DebugLog("Scan timed out");
            isScanning = false;
            yield return StartCoroutine(RetryConnectionCoroutine());
        }
    }
    
    private void OnScanEnded(BluetoothHelper helper, LinkedList<BluetoothDevice> foundDevices)
    {
        isScanning = false;
        discoveredDevices = foundDevices;
        
        DebugLog($"Scan completed. Found {foundDevices.Count} devices");
        
        // Look for our ESP32 device
        BluetoothDevice targetDevice = null;
        LinkedListNode<BluetoothDevice> node = foundDevices.First;
        
        while (node != null)
        {
            DebugLog($"Found device: '{node.Value.DeviceName}' (RSSI: {node.Value.Rssi})");
            
            if (node.Value.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase) ||
                node.Value.DeviceName.Contains(deviceName))
            {
                targetDevice = node.Value;
                DebugLog($"✓ Target device found: {node.Value.DeviceName}");
                break;
            }
            
            node = node.Next;
        }
        
        if (targetDevice != null)
        {
            StartCoroutine(ConnectToFoundDevice(targetDevice));
        }
        else
        {
            DebugLog($"ESP32 device '{deviceName}' not found in scan results");
            UpdateConnectionStatus($"ESP32 '{deviceName}' not found");
            StartCoroutine(RetryConnectionCoroutine());
        }
    }
    
    private IEnumerator ConnectToFoundDevice(BluetoothDevice device)
    {
        DebugLog($"Connecting to {device.DeviceName}...");
        UpdateConnectionStatus($"Connecting to {device.DeviceName}...");
        
        bool connectionFailed = false;
        
        try
        {
            helper.setDeviceName(device.DeviceName);
            helper.Connect();
            
            isConnecting = true;
            connectionStartTime = Time.time;
        }
        catch (Exception e)
        {
            DebugLog($"Connection to {device.DeviceName} failed: {e.Message}");
            isConnecting = false;
            connectionFailed = true;
        }
        
        if (connectionFailed)
        {
            yield return StartCoroutine(RetryConnectionCoroutine());
            yield break;
        }
        
        // Wait for connection result
        while (isConnecting && (Time.time - connectionStartTime) < connectionTimeout)
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        if (!isConnected)
        {
            DebugLog($"Connection to {device.DeviceName} timed out");
            isConnecting = false;
            yield return StartCoroutine(RetryConnectionCoroutine());
        }
    }
    
    private IEnumerator RetryConnectionCoroutine()
    {
        connectionRetryCount++;
        
        if (connectionRetryCount >= maxRetries)
        {
            DebugLog("Max connection retries reached. Enabling keyboard fallback.");
            UpdateConnectionStatus("ESP32 not found. Using keyboard controls.");
            EnableKeyboardFallback();
            yield break;
        }
        
        DebugLog($"Retrying connection in 3 seconds... (Attempt {connectionRetryCount}/{maxRetries})");
        UpdateConnectionStatus($"Retrying connection ({connectionRetryCount}/{maxRetries})...");
        
        yield return new WaitForSeconds(3f);
        yield return StartCoroutine(StartConnectionProcess());
    }
    
    private void OnBluetoothConnected(BluetoothHelper helper)
    {
        isConnected = true;
        isConnecting = false;
        connectionRetryCount = 0;
        
        UpdateConnectionStatus("✓ Connected to ESP32!");
        DebugLog("✓ Successfully connected to ESP32");
        
        // Disable keyboard fallback
        if (boatController != null)
        {
            boatController.SetInputMode(BoatController.InputMode.BluetoothSensor);
        }
        
        // Start listening for data
        try
        {
            helper.StartListening();
            DebugLog("Started listening for data from ESP32");
        }
        catch (Exception e)
        {
            DebugLog($"Failed to start listening: {e.Message}");
        }
    }
    
    private void OnBluetoothConnectionFailed(BluetoothHelper helper)
    {
        isConnected = false;
        isConnecting = false;
        
        DebugLog("✗ Connection to ESP32 failed");
        UpdateConnectionStatus("Connection failed");
    }
    
    private void OnBluetoothDataReceived(BluetoothHelper helper)
    {
        try
        {
            string receivedData = helper.Read();
            
            if (!string.IsNullOrEmpty(receivedData))
            {
                receivedData = receivedData.Trim();
                ProcessReceivedData(receivedData);
            }
        }
        catch (Exception e)
        {
            DebugLog($"Error reading data: {e.Message}");
        }
    }
    
    private void ProcessReceivedData(string data)
    {
        if (string.IsNullOrEmpty(data)) return;
        
        DebugLog($"Received: '{data}'");
        
        try
        {
            // Expected format: "G:-7.1,A:6296"
            if (data.Contains("G:") && data.Contains("A:"))
            {
                string[] parts = data.Split(',');
                
                bool gyroUpdated = false;
                
                foreach (string part in parts)
                {
                    string trimmedPart = part.Trim();
                    
                    if (trimmedPart.StartsWith("G:"))
                    {
                        string gyroStr = trimmedPart.Substring(2);
                        if (float.TryParse(gyroStr, out float gyro))
                        {
                            currentGyro = gyro;
                            gyroUpdated = true;
                        }
                    }
                    else if (trimmedPart.StartsWith("A:"))
                    {
                        string accelStr = trimmedPart.Substring(2);
                        if (int.TryParse(accelStr, out int accel))
                        {
                            currentAccel = accel;
                        }
                    }
                }
                
                if (gyroUpdated)
                {
                    DetectPaddleFromGyro(currentGyro);
                    DebugLog($"Gyro: {currentGyro:F1}°, Accel: {currentAccel}");
                }
            }
            else
            {
                DebugLog($"Unexpected data format: {data}");
            }
        }
        catch (Exception e)
        {
            DebugLog($"Data parsing error: {e.Message}");
        }
    }
    
    private void DetectPaddleFromGyro(float gyroValue)
    {
        float currentTime = Time.time;
        
        // Left paddle detection
        if (gyroValue < leftThreshold && !isLeftPaddle && 
            currentTime - lastLeftTime > debounceTime)
        {
            isLeftPaddle = true;
            isRightPaddle = false;
            TriggerLeftPaddle();
            lastLeftTime = currentTime;
            DebugLog($"LEFT PADDLE detected! (Gyro: {gyroValue:F1}°)");
        }
        // Right paddle detection  
        else if (gyroValue > rightThreshold && !isRightPaddle && 
                 currentTime - lastRightTime > debounceTime)
        {
            isRightPaddle = true;
            isLeftPaddle = false;
            TriggerRightPaddle();
            lastRightTime = currentTime;
            DebugLog($"RIGHT PADDLE detected! (Gyro: {gyroValue:F1}°)");
        }
        // Neutral position
        else if (Mathf.Abs(gyroValue) < neutralThreshold)
        {
            if (isLeftPaddle || isRightPaddle)
            {
                ResetPaddleState();
                DebugLog("Paddle returned to neutral");
            }
        }
    }
    
    private void TriggerLeftPaddle()
    {
        if (boatController != null)
        {
            boatController.PaddleLeft();
        }
    }
    
    private void TriggerRightPaddle()
    {
        if (boatController != null)
        {
            boatController.PaddleRight();
        }
    }
    
    private void ResetPaddleState()
    {
        isLeftPaddle = false;
        isRightPaddle = false;
    }
    
    private void EnableKeyboardFallback()
    {
        if (boatController != null)
        {
            boatController.SetInputMode(BoatController.InputMode.Keyboard);
            DebugLog("Keyboard fallback enabled");
        }
    }
    
    private void UpdateConnectionStatus(string status)
    {
        if (uiManager != null)
        {
            uiManager.UpdateConnectionStatus(status);
        }
    }
    
    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[ESP32Bluetooth] {message}");
        }
    }
    
    // Manual controls
    [ContextMenu("Manual Connect")]
    public void ManualConnect()
    {
        if (!isConnected && !isConnecting)
        {
            connectionRetryCount = 0;
            StartCoroutine(StartConnectionProcess());
        }
    }
    
    [ContextMenu("Manual Disconnect")]
    public void ManualDisconnect()
    {
        if (helper != null && isConnected)
        {
            try
            {
                helper.Disconnect();
                isConnected = false;
                UpdateConnectionStatus("Manually disconnected");
                DebugLog("Manually disconnected from ESP32");
            }
            catch (Exception e)
            {
                DebugLog($"Disconnect error: {e.Message}");
            }
        }
    }
    
    [ContextMenu("Start Scan")]
    public void ManualStartScan()
    {
        if (helper != null && !isScanning && !isConnected)
        {
            StartCoroutine(ScanAndConnect());
        }
    }
    
    [ContextMenu("Test Left Paddle")]
    public void TestLeftPaddle()
    {
        TriggerLeftPaddle();
    }
    
    [ContextMenu("Test Right Paddle")]
    public void TestRightPaddle()
    {
        TriggerRightPaddle();
    }
    
    // Send command to ESP32
    public void SendCommand(string command)
    {
        if (helper != null && isConnected)
        {
            try
            {
                helper.SendData(command);
                DebugLog($"Sent to ESP32: {command}");
            }
            catch (Exception e)
            {
                DebugLog($"Send error: {e.Message}");
            }
        }
    }
    
    private void OnDestroy()
    {
        CleanupBluetooth();
    }
    
    private void OnApplicationQuit()
    {
        CleanupBluetooth();
    }
    
    private void CleanupBluetooth()
    {
        if (helper != null)
        {
            try
            {
                if (isConnected)
                {
                    helper.Disconnect();
                }
                
                // Unsubscribe from events
                helper.OnConnected -= OnBluetoothConnected;
                helper.OnConnectionFailed -= OnBluetoothConnectionFailed;
                helper.OnDataReceived -= OnBluetoothDataReceived;
                helper.OnScanEnded -= OnScanEnded;
                
                DebugLog("Bluetooth cleanup completed");
            }
            catch (Exception e)
            {
                DebugLog($"Cleanup error: {e.Message}");
            }
        }
    }
    
    // Debug GUI
    private void OnGUI()
    {
        if (!showDebugLogs) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 250));
        GUILayout.Box("ESP32 Bluetooth Debug");
        
        GUILayout.Label($"Status: {(isConnected ? "CONNECTED" : isConnecting ? "CONNECTING" : isScanning ? "SCANNING" : "DISCONNECTED")}");
        GUILayout.Label($"Device: {deviceName}");
        GUILayout.Label($"Gyro: {currentGyro:F1}°");
        GUILayout.Label($"Accel: {currentAccel}");
        GUILayout.Label($"Paddle: {(isLeftPaddle ? "LEFT" : isRightPaddle ? "RIGHT" : "NEUTRAL")}");
        GUILayout.Label($"Retries: {connectionRetryCount}/{maxRetries}");
        
        GUILayout.Space(10);
        
        // Manual controls
        if (!isConnected && !isConnecting && !isScanning)
        {
            if (GUILayout.Button("Connect"))
            {
                ManualConnect();
            }
        }
        else if (isConnected)
        {
            if (GUILayout.Button("Disconnect"))
            {
                ManualDisconnect();
            }
        }
        else
        {
            GUILayout.Label(isConnecting ? "Connecting..." : "Scanning...");
        }
        
        // Test buttons
        if (GUILayout.Button("Test L"))
        {
            TestLeftPaddle();
        }
        if (GUILayout.Button("Test R"))
        {
            TestRightPaddle();
        }
        
        GUILayout.EndArea();
        
        // Show discovered devices
        if (discoveredDevices != null && discoveredDevices.Count > 0 && !isConnected)
        {
            DrawDeviceList();
        }
    }
    
    private void DrawDeviceList()
    {
        GUILayout.BeginArea(new Rect(320, 10, 300, 400));
        GUILayout.Box("Discovered Devices");
        
        LinkedListNode<BluetoothDevice> node = discoveredDevices.First;
        int count = 0;
        
        while (node != null && count < 15)
        {
            string deviceInfo = $"{node.Value.DeviceName}";
            if (string.IsNullOrEmpty(deviceInfo.Trim()))
            {
                deviceInfo = "[Unknown Device]";
            }
            deviceInfo += $" ({node.Value.Rssi}dBm)";
            
            // Highlight target device
            if (node.Value.DeviceName.Contains(deviceName))
            {
                GUI.color = Color.green;
            }
            
            if (GUILayout.Button(deviceInfo))
            {
                StartCoroutine(ConnectToFoundDevice(node.Value));
            }
            
            GUI.color = Color.white;
            node = node.Next;
            count++;
        }
        
        GUILayout.EndArea();
    }
}