using System;
using UnityEngine;
using ArduinoBluetoothAPI;

public class BluetoothManager : MonoBehaviour
{
    [Header("Connection Settings")]
    public string deviceName = "ferizy-paddle"; // Name of the Bluetooth device
    public bool autoConnect = true;
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
    
    [Header("Multiple Device Support")]
    [Tooltip("List of device names to try connecting to")]
    public string[] deviceNames = { "ferizy-paddle", "ferizy-dayung", "ferizy-paddle-2", "ferizy-dayung-2" };
    
    // Bluetooth
    private BluetoothHelper bluetoothHelper;
    private bool isConnected = false;
    private int currentDeviceIndex = 0;
    private string connectedDeviceName = "";
    
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
    
    void Start()
    {
        // Initialize with first device in list if available
        if (deviceNames.Length > 0)
        {
            deviceName = deviceNames[0];
        }
        
        InitializeBluetooth();
        if (autoConnect)
        {
            Invoke("Connect", 1f);
        }
    }
    
    void InitializeBluetooth()
    {
        try
        {
            bluetoothHelper = BluetoothHelper.GetInstance(deviceName);
            bluetoothHelper.OnConnected += OnConnected;
            bluetoothHelper.OnConnectionFailed += OnConnectionFailed;
            bluetoothHelper.OnDataReceived += OnDataReceived;
            bluetoothHelper.setTerminatorBasedStream("\n");
            
            if (enableDebugLogs)
                Debug.Log("[Bluetooth] Initialized");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Bluetooth] Init failed: {ex.Message}");
        }
    }
    
    public void Connect()
    {
        if (bluetoothHelper != null && !bluetoothHelper.isConnected())
        {
            bluetoothHelper.Connect();
        }
    }
    
    public void ConnectToNextDevice()
    {
        if (deviceNames.Length == 0)
        {
            Connect();
            return;
        }
        
        // Try next device in the list
        currentDeviceIndex = (currentDeviceIndex + 1) % deviceNames.Length;
        deviceName = deviceNames[currentDeviceIndex];
        
        if (enableDebugLogs)
            Debug.Log($"[Bluetooth] Trying device: {deviceName}");
        
        // Disconnect current if connected
        if (isConnected)
        {
            Disconnect();
        }
        
        // Reinitialize with new device
        InitializeBluetooth();
        Connect();
    }
    
    public void TryNextDevice()
    {
        ConnectToNextDevice();
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
        connectedDeviceName = deviceName;
        helper.StartListening();
        OnConnectionChanged?.Invoke(true);
        
        if (enableDebugLogs)
            Debug.Log($"[Bluetooth] ✓ Connected to {deviceName}");
    }
    
    void OnConnectionFailed(BluetoothHelper helper)
    {
        isConnected = false;
        connectedDeviceName = "";
        OnConnectionChanged?.Invoke(false);
        Debug.LogError($"[Bluetooth] ✗ Connection failed to {deviceName}");
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
        }
    }
    
    bool ParseRawPacket(string data)
    {
        if (string.IsNullOrEmpty(data) || !data.StartsWith(packetStartChar) || !data.EndsWith(packetEndChar))
            return false;
        
        try
        {
            string values = data.Substring(1, data.Length - 2);
            string[] parts = values.Split(',');
            
            if (parts.Length != 6) return false;
            
            // Clean any non-numeric characters except minus sign
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = CleanNumericString(parts[i]);
            }
            
            rawGyro.x = int.Parse(parts[0]);
            rawGyro.y = int.Parse(parts[1]);
            rawGyro.z = int.Parse(parts[2]);
            rawAccel.x = int.Parse(parts[3]);
            rawAccel.y = int.Parse(parts[4]);
            rawAccel.z = int.Parse(parts[5]);
            
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
    
    // Helper to clean up potentially problematic strings
    private string CleanNumericString(string input)
    {
        // Keep only digits, minus sign, and decimal point
        string result = "";
        bool hasMinusSign = false;
        
        foreach (char c in input)
        {
            if (c == '-' && !hasMinusSign)
            {
                result += c;
                hasMinusSign = true;
            }
            else if (char.IsDigit(c) || c == '.')
            {
                result += c;
            }
        }
        
        return result;
    }
    
    void ProcessGyroData()
    {
        if (Time.deltaTime > 0)
        {
            // Calculate velocity
            Vector3 gyroVelocity = new Vector3(
                (gyroAngles.x - previousGyroAngles.x) / Time.deltaTime,
                (gyroAngles.y - previousGyroAngles.y) / Time.deltaTime,
                (gyroAngles.z - previousGyroAngles.z) / Time.deltaTime
            );
            
            // Apply smoothing
            filteredVelocity = Vector3.Lerp(filteredVelocity, gyroVelocity, smoothingFactor);
            
            // Send to listeners
            OnGyroDataReceived?.Invoke(filteredVelocity);
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Gyro] X={rawGyro.x:+0000} Y={rawGyro.y:+0000} Z={rawGyro.z:+0000} → vel: {filteredVelocity.magnitude:F1}°/s");
            Debug.Log($"[Accel] X={rawAccel.x:+0000} Y={rawAccel.y:+0000} Z={rawAccel.z:+0000} → mag: {GetRawAccelMagnitude():F1}");
        }
    }
    
    // Public API
    public bool IsConnected() => isConnected;
    public string GetConnectedDevice() => isConnected ? connectedDeviceName : "Not connected";
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
    
    void OnDestroy()
    {
        Disconnect();
    }
    
    void OnGUI()
    {
        if (!enableDebugLogs) return;
        
        GUILayout.BeginArea(new Rect(10, Screen.height - 150, 300, 150));
        
        GUI.color = isConnected ? Color.green : Color.red;
        GUILayout.Label($"Bluetooth: {(isConnected ? "✓ Connected" : "✗ Disconnected")}");
        GUI.color = Color.white;
        
        float validRatio = GetValidPacketRatio() * 100f;
        GUILayout.Label($"Packets: {validPackets}/{totalPackets} ({validRatio:F1}%)");
        GUILayout.Label($"Device: {deviceName}");
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Connect")) Connect();
        if (GUILayout.Button("Disconnect")) Disconnect();
        if (GUILayout.Button("Next Device")) TryNextDevice();
        GUILayout.EndHorizontal();
        
        GUILayout.EndArea();
    }
}