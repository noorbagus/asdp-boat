using UnityEngine;
using ArduinoBluetoothAPI;
using UnityEngine.UI;

public class BluetoothDebug : MonoBehaviour
{
    public string deviceName = "ferizy-paddle";
    public Text statusText;
    public Text dataText;
    
    private BluetoothHelper bt;
    private bool isScanning = false;
    
    void Start()
    {
        statusText.text = "Initializing...";
        
        try
        {
            BluetoothHelper.BLE = false;
            bt = BluetoothHelper.GetInstance(deviceName);
            bt.OnConnected += OnConnected;
            bt.OnConnectionFailed += OnFailed;
            bt.OnDataReceived += OnData;
            bt.OnScanEnded += OnScanEnd;
            bt.setTerminatorBasedStream("\n");
            
            statusText.text = "Auto-connecting...";
            Invoke("Connect", 2f);
        }
        catch (System.Exception e)
        {
            statusText.text = "Error: " + e.Message;
        }
    }
    
    public void Connect()
    {
        if (bt == null) return;
        
        statusText.text = "Scanning...";
        isScanning = bt.ScanNearbyDevices();
        
        if (!isScanning)
        {
            statusText.text = "Trying direct connect...";
            bt.Connect();
        }
    }
    
    void OnScanEnd(BluetoothHelper helper, System.Collections.Generic.LinkedList<BluetoothDevice> devices)
    {
        statusText.text = $"Found {devices.Count} devices";
        isScanning = false;
        
        if (helper.isDevicePaired())
        {
            statusText.text = "Device found. Connecting...";
            helper.Connect();
        }
        else
        {
            statusText.text = "Device not found!";
        }
    }
    
    void OnConnected(BluetoothHelper helper)
    {
        statusText.text = "✓ Connected!";
        helper.StartListening();
    }
    
    void OnFailed(BluetoothHelper helper)
    {
        statusText.text = "✗ Connection failed";
    }
    
    void OnData(BluetoothHelper helper)
    {
        string data = helper.Read();
        dataText.text = data;
        Debug.Log("Data: " + data);
    }
    
    void OnGUI()
    {
        if (bt != null)
            bt.DrawGUI();
    }
    
    void OnDestroy()
    {
        if (bt != null)
            bt.Disconnect();
    }
}