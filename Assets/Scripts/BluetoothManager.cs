using System;
using System.Collections.Generic;
using UnityEngine;
using ArduinoBluetoothAPI;

public class BluetoothManager : MonoBehaviour
{
    [Header("Device Settings")]
    [Tooltip("List of ESP32 device names to try connecting to")]
    public List<string> deviceNames = new List<string> 
    { 
        "ferizy-paddle", 
        "ferizy-paddle-2" 
    };
    
    [Header("Connection Settings")]
    public bool autoConnect = true;
    public bool autoSwitchOnDisconnect = true;
    public float reconnectDelay = 2f;
    public int maxReconnectAttempts = 3;
    public bool enableDebugLogs = false;
    
    [Header("Data Scaling")]
    [Tooltip("Raw gyro values are divided by this factor")]
    public float gyroScale = 65.5f;
    [Tooltip("Raw accel values are divided by this factor")]
    public float accelScale = 8192f;
    [Tooltip("Factor applied to smooth gyro data")]
    [Range(0.01f, 1f)]
    public float smoothingFactor = 0.2f;
    
    [Header("Data Format")]
    [Tooltip("Expected start character for data packets")]
    public string packetStartChar = "R";
    [Tooltip("Expected end character for data packets")]
    public string packetEndChar = "N";
    
    // Bluetooth
    private BluetoothHelper bluetoothHelper;
    private bool isConnected = false;
    private int currentDeviceIndex = 0;
    private string connectedDeviceName = "";
    private int reconnectAttempts = 0;
    private bool isReconnecting = false;
    
    // Data tracking
    private string lastRawPacket = "";
    private int totalPackets = 0;
    private int validPackets = 0;
    private int errorPackets = 0;
    
    // Raw sensor data
    private Vector3Int rawGyro = Vector3Int.zero;
    private Vector3Int rawAccel = Vector3Int.zero;
    private Vector3 gyroAngles = Vector3.zero;
    private Vector3 previousGyroAngles = Vector3.zero;
    private Vector3 filteredVelocity = Vector3.zero;
    
    // Events
    public System.Action<Vector3> OnGyroDataReceived;
    public System.Action<bool> OnConnectionChanged;
    public System.Action<string> OnDeviceChanged;
    
    void Start()
    {
        if (deviceNames.Count == 0)
        {
            deviceNames.Add("ferizy-paddle");
            Debug.LogWarning("[Bluetooth] No device names configured, using default");
        }
        
        if (autoConnect)
        {
            Invoke("ConnectToNextDevice", 1f);
        }
    }
    
    void InitializeBluetooth(string deviceName)
    {
        try
        {
            // Clean up existing connection
            if (bluetoothHelper != null)
            {
                bluetoothHelper.Disconnect();
                bluetoothHelper = null;
            }
            
            bluetoothHelper = BluetoothHelper.GetInstance(deviceName);
            bluetoothHelper.OnConnected += OnConnected;
            bluetoothHelper.OnConnectionFailed += OnConnectionFailed;
            bluetoothHelper.OnDataReceived += OnDataReceived;
            bluetoothHelper.setTerminatorBasedStream("\n");
            
            if (enableDebugLogs)
                Debug.Log($"[Bluetooth] Initialized for device: {deviceName}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Bluetooth] Init failed for {deviceName}: {ex.Message}");
        }
    }
    
    public void ConnectToNextDevice()
    {
        if (deviceNames.Count == 0) return;
        
        string deviceName = deviceNames[currentDeviceIndex];
        ConnectToDevice(deviceName);
    }
    
    public void ConnectToDevice(string deviceName)
    {
        if (isReconnecting) return;
        
        if (enableDebugLogs)
            Debug.Log($"[Bluetooth] Attempting to connect to: {deviceName}");
        
        InitializeBluetooth(deviceName);
        
        if (bluetoothHelper != null && !bluetoothHelper.isConnected())
        {
            bluetoothHelper.Connect();
        }
    }
    
    public void TryNextDevice()
    {
        if (isReconnecting) return;
        
        currentDeviceIndex = (currentDeviceIndex + 1) % deviceNames.Count;
        reconnectAttempts = 0;
        
        if (enableDebugLogs)
            Debug.Log($"[Bluetooth] Trying next device: {deviceNames[currentDeviceIndex]}");
        
        Invoke("ConnectToNextDevice", 1f);
    }
    
    public void Disconnect()
    {
        if (bluetoothHelper != null)
        {
            bluetoothHelper.Disconnect();
            isConnected = false;
            connectedDeviceName = "";
            OnConnectionChanged?.Invoke(false);
        }
    }
    
    void OnConnected(BluetoothHelper helper)
    {
        isConnected = true;
        connectedDeviceName = deviceNames[currentDeviceIndex];
        reconnectAttempts = 0;
        isReconnecting = false;
        
        helper.StartListening();
        OnConnectionChanged?.Invoke(true);
        OnDeviceChanged?.Invoke(connectedDeviceName);
        
        if (enableDebugLogs)
            Debug.Log($"[Bluetooth] ✓ Connected to: {connectedDeviceName}");
    }
    
    void OnConnectionFailed(BluetoothHelper helper)
    {
        isConnected = false;
        OnConnectionChanged?.Invoke(false);
        
        if (enableDebugLogs)
            Debug.Log($"[Bluetooth] ✗ Connection failed to: {deviceNames[currentDeviceIndex]}");
        
        // Try reconnecting or switch device
        if (autoSwitchOnDisconnect)
        {
            HandleConnectionFailure();
        }
    }
    
    void HandleConnectionFailure()
    {
        if (isReconnecting) return;
        
        reconnectAttempts++;
        
        if (reconnectAttempts >= maxReconnectAttempts)
        {
            // Switch to next device
            if (enableDebugLogs)
                Debug.Log($"[Bluetooth] Max reconnect attempts reached, switching device");
            
            TryNextDevice();
        }
        else
        {
            // Retry current device
            if (enableDebugLogs)
                Debug.Log($"[Bluetooth] Reconnect attempt {reconnectAttempts}/{maxReconnectAttempts}");
            
            isReconnecting = true;
            Invoke("RetryConnection", reconnectDelay);
        }
    }
    
    void RetryConnection()
    {
        isReconnecting = false;
        ConnectToNextDevice();
    }
    
    void OnDataReceived(BluetoothHelper helper)
    {
        try
        {
            string data = helper.Read().Trim();
            lastRawPacket = data;
            totalPackets++;
            
            if (ParseRawPacket(data))
            {
                validPackets++;
                ProcessGyroData();
            }
            else
            {
                errorPackets++;
                if (enableDebugLogs)
                    Debug.LogWarning($"[Bluetooth] Invalid packet: {data}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Bluetooth] Data error: {ex.Message}");
            errorPackets++;
            
            // Check if connection is still alive
            if (!bluetoothHelper.isConnected() && autoSwitchOnDisconnect)
            {
                HandleConnectionFailure();
            }
        }
    }
    
    bool ParseRawPacket(string data)
    {
        if (string.IsNullOrEmpty(data) || !data.StartsWith(packetStartChar) || !data.EndsWith(packetEndChar))
            return false;
        
        try
        {
            // Remove R and N characters: R+2695,+0105,+0097,-7680,+0190,-1250N
            string values = data.Substring(1, data.Length - 2);
            string[] parts = values.Split(',');
            
            if (parts.Length != 6) return false;
            
            // Parse raw integer values
            rawGyro.x = int.Parse(parts[0]);
            rawGyro.y = int.Parse(parts[1]);
            rawGyro.z = int.Parse(parts[2]);
            rawAccel.x = int.Parse(parts[3]);
            rawAccel.y = int.Parse(parts[4]);
            rawAccel.z = int.Parse(parts[5]);
            
            // Convert to degrees
            previousGyroAngles = gyroAngles;
            gyroAngles.x = rawGyro.x / gyroScale;
            gyroAngles.y = rawGyro.y / gyroScale;
            gyroAngles.z = rawGyro.z / gyroScale;
            
            return true;
        }
        catch (Exception ex)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[Bluetooth] Parse error: {ex.Message}");
            return false;
        }
    }
    
    void ProcessGyroData()
    {
        if (Time.deltaTime > 0)
        {
            Vector3 gyroVelocity = new Vector3(
                (gyroAngles.x - previousGyroAngles.x) / Time.deltaTime,
                (gyroAngles.y - previousGyroAngles.y) / Time.deltaTime,
                (gyroAngles.z - previousGyroAngles.z) / Time.deltaTime
            );
            
            filteredVelocity = Vector3.Lerp(filteredVelocity, gyroVelocity, smoothingFactor);
            OnGyroDataReceived?.Invoke(filteredVelocity);
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Gyro] Raw: X={rawGyro.x} Y={rawGyro.y} Z={rawGyro.z} → Angles: X={gyroAngles.x:F1}° Y={gyroAngles.y:F1}° Z={gyroAngles.z:F1}°");
        }
    }
    
    // Public API
    public bool IsConnected() => isConnected;
    public string GetConnectedDevice() => connectedDeviceName;
    public Vector3Int GetRawGyro() => rawGyro;
    public Vector3Int GetRawAccel() => rawAccel;
    public Vector3 GetScaledAccel() => new Vector3(
        rawAccel.x / accelScale,
        rawAccel.y / accelScale,
        rawAccel.z / accelScale
    );
    public float GetRawAccelMagnitude() => new Vector3(rawAccel.x, rawAccel.y, rawAccel.z).magnitude;
    public Vector3 GetGyroAngles() => gyroAngles;
    public Vector3 GetFilteredVelocity() => filteredVelocity;
    public string GetLastPacket() => lastRawPacket;
    public float GetValidPacketRatio() => totalPackets > 0 ? (float)validPackets / totalPackets : 0f;
    public int GetTotalPackets() => totalPackets;
    public int GetValidPackets() => validPackets;
    public int GetErrorPackets() => errorPackets;
    
    // Manual device switching
    public void SwitchToDevice(int index)
    {
        if (index >= 0 && index < deviceNames.Count)
        {
            currentDeviceIndex = index;
            reconnectAttempts = 0;
            
            if (isConnected)
            {
                Disconnect();
                Invoke("ConnectToNextDevice", 1f);
            }
            else
            {
                ConnectToNextDevice();
            }
        }
    }
    
    public void AddDevice(string deviceName)
    {
        if (!deviceNames.Contains(deviceName))
        {
            deviceNames.Add(deviceName);
            if (enableDebugLogs)
                Debug.Log($"[Bluetooth] Added device: {deviceName}");
        }
    }
    
    void OnDestroy()
    {
        Disconnect();
    }
    
    void OnGUI()
    {
        if (!enableDebugLogs) return;
        
        GUILayout.BeginArea(new Rect(10, Screen.height - 180, 300, 180));
        
        GUI.color = isConnected ? Color.green : Color.red;
        GUILayout.Label($"Bluetooth: {(isConnected ? "✓ Connected" : "✗ Disconnected")}");
        GUI.color = Color.white;
        
        if (isConnected)
        {
            GUILayout.Label($"Device: {connectedDeviceName}");
            GUILayout.Label($"Raw Gyro: X={rawGyro.x} Y={rawGyro.y} Z={rawGyro.z}");
            GUILayout.Label($"Angles: X={gyroAngles.x:F1}° Y={gyroAngles.y:F1}° Z={gyroAngles.z:F1}°");
        }
        else
        {
            GUILayout.Label($"Trying: {deviceNames[currentDeviceIndex]} ({reconnectAttempts}/{maxReconnectAttempts})");
        }
        
        float validRatio = GetValidPacketRatio() * 100f;
        GUILayout.Label($"Packets: {validPackets}/{totalPackets} ({validRatio:F1}%)");
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Connect")) ConnectToNextDevice();
        if (GUILayout.Button("Next Device")) TryNextDevice();
        if (GUILayout.Button("Disconnect")) Disconnect();
        GUILayout.EndHorizontal();
        
        GUILayout.EndArea();
    }
}