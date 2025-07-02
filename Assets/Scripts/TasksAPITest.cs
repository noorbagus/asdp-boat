using UnityEngine;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Core;

public class TasksAPITest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Testing MediaPipe Tasks API access");
        
        try {
            var options = new PoseLandmarkerOptions(
                new BaseOptions(BaseOptions.Delegate.CPU),
                runningMode: Mediapipe.Tasks.Vision.Core.RunningMode.IMAGE
            );
            
            Debug.Log("Tasks API classes accessible successfully");
        }
        catch (System.Exception e) {
            Debug.LogError($"Tasks API Error: {e.Message}");
        }
    }
}