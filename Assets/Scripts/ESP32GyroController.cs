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
    
    [Header("Data Processing")]
    public float dataSmoothing = 0.5f;
    public float gyroMultiplier = 2.0f;
    
    [Header("Idle Settings")]
    public bool maintainIdleAngle = true;
    [Range(0.01f, 1.0f)]
    public float idleReturnSpeed = 0.05f;
    private float idleBaseAngle = 0f;
    
    [Header("Paddle Integration")]
    public PaddleIKController paddleController;
    
    [Header("Debug")]
    public bool enableDebugLogs = true;
    public GameObject connectionIndicator;
    public UnityEngine.UI.Text debugText;
    
    // Connection state
    private BluetoothHelper bluetoothHelper;
    private bool isConnecting = false;
    private bool isScanning = false;
    private float connectionTimer = 0f;
    private string lastPacket = "";
    
    // Data values
    private float currentGyroX = 0f;
    private int currentAccelY = 0;
    private float smoothedGyroX = 0f;
    
    // Monitoring
    private float lastDataTime = 0f;
    private int packetsReceived = 0;
    private int errorPackets = 0;
    
    void Start()
    {
        if (debugText != null)
            debugText.text = "Starting...";
        
        InitializeBluetooth();
        
        if (autoConnect)
            StartCoroutine(AutoConnectRoutine());
    }
    
    void Update()
    {
        // Update connection indicator color based on state
        UpdateConnectionIndicator();
        
        // Update paddle controller with gyro data
        if (paddleController != null && bluetoothHelper != null && bluetoothHelper.isConnected())
        {
            // Apply smoothing to gyro value
            smoothedGyroX = Mathf.Lerp(smoothedGyroX, (currentGyroX - idleBaseAngle) * gyroMultiplier, dataSmoothing * Time.deltaTime * 10f);
            
            // Send the inverted value to paddle controller (negate to fix direction)
            paddleController.SetRawAngle(-smoothedGyroX);
            
            // Monitor data freshness - if no data for 5 seconds, something's wrong
            if (Time.time - lastDataTime > 5f)
            {
                DebugLog("⚠️ No data received for 5 seconds!");
                // Consider reconnecting here
            }
        }
        
        // Connection timeout handling
        if (isConnecting)
        {
            connectionTimer += Time.deltaTime;
            if (connectionTimer > connectionTimeout)
            {
                DebugLog("Connection timeout - reconnecting");
                DisconnectBluetooth();
                StartCoroutine(AutoConnectRoutine());
            }
        }
    }
    
    private void InitializeBluetooth()
    {
        try
        {
            DebugLog("Initializing Bluetooth...");
            
            // Configure Bluetooth settings for ESP32
            BluetoothHelper.BLE = false; // Using classic Bluetooth for ESP32
            
            // Create Bluetooth helper instance
            bluetoothHelper = BluetoothHelper.GetInstance(deviceName);
            
            // Register event handlers
            bluetoothHelper.OnConnected += OnConnected;
            bluetoothHelper.OnConnectionFailed += OnConnectionFailed;
            bluetoothHelper.OnDataReceived += OnDataReceived;
            bluetoothHelper.OnScanEnded += OnScanEnded;
            
            // Configure data stream format
            bluetoothHelper.setTerminatorBasedStream("\n");
            
            DebugLog("Bluetooth initialized successfully");
        }
        catch (Exception ex)
        {
            DebugLog("ERROR: " + ex.Message);
        }
    }
    
    // Automatically connect to the device
    private IEnumerator AutoConnectRoutine()
    {
        yield return new WaitForSeconds(1f);
        ConnectToDevice();
    }
    
    // Connect to the ESP32 device
    public void ConnectToDevice()
    {
        if (bluetoothHelper == null)
        {
            DebugLog("ERROR: Bluetooth helper not initialized");
            return;
        }
        
        if (bluetoothHelper.isConnected())
        {
            DebugLog("Already connected");
            return;
        }
        
        try
        {
            DebugLog("Scanning for device: " + deviceName);
            isScanning = bluetoothHelper.ScanNearbyDevices();
            
            if (!isScanning)
            {
                // If scanning didn't start, try direct connection
                DebugLog("Trying direct connection...");
                isConnecting = true;
                connectionTimer = 0f;
                bluetoothHelper.Connect();
            }
        }
        catch (Exception ex)
        {
            DebugLog("Connection error: " + ex.Message);
            isConnecting = false;
        }
    }
    
    // Disconnect from the device
    public void DisconnectBluetooth()
    {
        if (bluetoothHelper != null && bluetoothHelper.isConnected())
        {
            bluetoothHelper.Disconnect();
            DebugLog("Disconnected from device");
        }
        
        isConnecting = false;
        isScanning = false;
    }
    
    // Handle scan completion event
    private void OnScanEnded(BluetoothHelper helper, System.Collections.Generic.LinkedList<BluetoothDevice> devices)
    {
        DebugLog($"Scan ended - found {devices.Count} devices");
        isScanning = false;
        
        if (helper.isDevicePaired())
        {
            DebugLog("Device found, connecting...");
            isConnecting = true;
            connectionTimer = 0f;
            helper.Connect();
        }
        else
        {
            DebugLog("Device not found, rescanning...");
            // Wait before rescanning to avoid flooding
            StartCoroutine(RetryScan());
        }
    }
    
    private IEnumerator RetryScan()
    {
        yield return new WaitForSeconds(3f);
        if (!bluetoothHelper.isConnected())
        {
            isScanning = bluetoothHelper.ScanNearbyDevices();
        }
    }
    
    // Handle successful connection event
    private void OnConnected(BluetoothHelper helper)
    {
        DebugLog("✓ Connected to " + deviceName);
        isConnecting = false;
        
        try
        {
            helper.StartListening();
            DebugLog("Listening for data...");
        }
        catch (Exception ex)
        {
            DebugLog("Error starting listener: " + ex.Message);
        }
    }
    
    // Handle failed connection event
    private void OnConnectionFailed(BluetoothHelper helper)
    {
        DebugLog("✗ Connection failed or lost");
        isConnecting = false;
        
        // Auto-reconnect after delay
        StartCoroutine(ReconnectAfterDelay());
    }
    
    private IEnumerator ReconnectAfterDelay()
    {
        yield return new WaitForSeconds(3f);
        if (!bluetoothHelper.isConnected())
        {
            ConnectToDevice();
        }
    }
    
    // Handle data received event
    private void OnDataReceived(BluetoothHelper helper)
    {
        string data = helper.Read();
        lastPacket = data;
        lastDataTime = Time.time;
        packetsReceived++;
        
        // Process the received data
        ProcessData(data);
    }
    
    // Process data received from ESP32
    private void ProcessData(string data)
    {
        try
        {
            // Check for heartbeat packet
            if (data.StartsWith("HALIVE"))
            {
                // This is just a keepalive packet
                return;
            }
            
            // Process gyro/accel data packet (format: G+000.0A+00000C0X)
            if (data.Length >= 15 && data.StartsWith("G") && data.Contains("A"))
            {
                // Extract gyro value
                int aPos = data.IndexOf('A');
                if (aPos >= 2 && aPos <= 8)
                {
                    string gyroStr = data.Substring(1, aPos - 1);
                    float gyroValue;
                    if (float.TryParse(gyroStr, out gyroValue))
                    {
                        currentGyroX = gyroValue;
                        
                        // If maintainIdleAngle is enabled and the gyro value is stable, update idle base angle
                        if (maintainIdleAngle && Mathf.Abs(currentGyroX) < 5.0f)
                        {
                            // Slowly adapt idle angle to current stable position
                            idleBaseAngle = Mathf.Lerp(idleBaseAngle, currentGyroX, idleReturnSpeed);
                        }
                        
                        // Display debug info
                        if (debugText != null)
                        {
                            debugText.text = $"Gyro: {currentGyroX:F1}°\nIdle: {idleBaseAngle:F1}°\nPackets: {packetsReceived}\nErrors: {errorPackets}";
                        }
                    }
                }
            }
            else
            {
                // Invalid packet format
                errorPackets++;
                DebugLog($"Invalid packet: {data}");
            }
        }
        catch (Exception ex)
        {
            errorPackets++;
            DebugLog($"Error parsing data: {ex.Message}");
        }
    }
    
    // Update connection indicator object color
    private void UpdateConnectionIndicator()
    {
        if (connectionIndicator == null) return;
        
        Renderer renderer = connectionIndicator.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (bluetoothHelper != null && bluetoothHelper.isConnected())
            {
                // Connected - green
                renderer.material.color = Color.green;
            }
            else if (isConnecting || isScanning)
            {
                // Connecting - yellow
                renderer.material.color = Color.yellow;
            }
            else
            {
                // Disconnected - red
                renderer.material.color = Color.red;
            }
        }
    }
    
    // Debug log helper
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ESP32Gyro] {message}");
        }
    }
    
    // Check if ESP32 is connected
    public bool IsConnected()
    {
        return bluetoothHelper != null && bluetoothHelper.isConnected();
    }
    
    // Get the current gyro value
    public float GetGyroValue()
    {
        return currentGyroX;
    }
    
    // Get the smoothed gyro value
    public float GetSmoothedGyroValue()
    {
        return smoothedGyroX;
    }
    
    // Get the last received packet
    public string GetLastPacket()
    {
        return lastPacket;
    }
    
    void OnDestroy()
    {
        if (bluetoothHelper != null)
        {
            bluetoothHelper.Disconnect();
        }
    }
    
    void OnGUI()
    {
        if (bluetoothHelper != null)
            bluetoothHelper.DrawGUI();
    }
}