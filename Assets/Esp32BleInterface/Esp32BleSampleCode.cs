
//using System;
using System.Collections;
using System.Collections.Generic;
//using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class Esp32BleSampleCode : MonoBehaviour
{
    public float accelx;
    public float accely;
    public float accelz;

    private Esp32BleLib m_Esp32BleLib;

    TMP_InputField inputFieldno0;
    TMP_InputField inputFieldno1;

    void Start()
    {
        this.transform.localScale = new Vector3(2, 2, 2);

        UnityEngine.Debug.LogWarning("start");
        m_Esp32BleLib = gameObject.AddComponent<Esp32BleLib>();
        m_Esp32BleLib.Esp32BleLibStart();

        inputFieldno0 = GameObject.Find("InputFieldNo0").GetComponent<TMP_InputField>();
        inputFieldno1 = GameObject.Find("InputFieldNo1").GetComponent<TMP_InputField>();
        inputFieldno0.text = "1";
        inputFieldno1.text = "2";
    }

    // Update is called once per frame
    void Update()
    {
        byte[] readdata = new byte[] { };
        //UnityEngine.Debug.LogWarning("Update");
        if (!m_Esp32BleLib.UpdateRead(ref readdata))
        {
            return;
        }
        UnityEngine.Debug.LogWarning(" Read1: " + readdata[0] + " " + readdata[1] + " " + readdata[2]); 
        UnityEngine.Debug.LogWarning(" Read2: " + readdata.Length);

        string text = System.Text.Encoding.UTF8.GetString(readdata);
        UnityEngine.Debug.LogWarning(" Read3: " + text);
        string[] arr = text.Split(',');
        float[] acceldata = new float[3];
        acceldata[0] = float.Parse(arr[0]);
        acceldata[1] = float.Parse(arr[1]);
        acceldata[2] = float.Parse(arr[2]);


        UnityEngine.Debug.LogWarning(" Update: " + acceldata[0] + " " + acceldata[1] + " " + acceldata[2]);

        accelx = acceldata[0] * 100;
        accely = acceldata[1] * 100;
        accelz = acceldata[2] * 100;

        transform.rotation = Quaternion.AngleAxis(accelx, Vector3.up) * Quaternion.AngleAxis(accely, Vector3.right);
    }

    private void OnApplicationQuit()
    {
        UnityEngine.Debug.LogWarning("OnApplicationQuit");
        m_Esp32BleLib.Quit();
    }

    public void ButtonClick()
    {
        UnityEngine.Debug.LogWarning("ButtonClick: ");

        byte[] writedata = new byte[2] { byte.Parse(inputFieldno0.text) , byte.Parse(inputFieldno1.text) };
        UnityEngine.Debug.LogWarning(writedata[0] + " " + writedata[1]);
        m_Esp32BleLib.Command(writedata);
    }

}
