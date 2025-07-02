using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Mediapipe.Unity;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Core;

public class MediaPipeManager : MonoBehaviour
{
    [Header("MediaPipe Configuration")]
    [SerializeField] private Mediapipe.Tasks.Vision.Core.RunningMode runningMode = Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM;
    [SerializeField] private int modelComplexity = 1; // 0=lite, 1=full, 2=heavy
    [SerializeField] private bool smoothLandmarks = true;
    [SerializeField] private bool enableSegmentation = false;
    [SerializeField] private float minDetectionConfidence = 0.7f;
    [SerializeField] private float minTrackingConfidence = 0.5f;
    
    [Header("Performance Settings")]
    [SerializeField] private int maxProcessingFPS = 30;
    [SerializeField] private bool enableGPUAcceleration = true;
    [SerializeField] private int numThreads = 4;
    [SerializeField] private bool useOptimizedModel = true;
    
    [Header("Error Handling")]
    [SerializeField] private int maxRetries = 3;
    [SerializeField] private float retryDelay = 1f;
    [SerializeField] private bool autoRestart = true;
    
    // MediaPipe components
    private PoseLandmarker poseLandmarker;
    private BaseOptions.Delegate delegateType;
    private bool isInitialized = false;
    private bool isProcessing = false;
    private int currentRetries = 0;
    
    // Processing queue
    private System.Collections.Generic.Queue<ProcessingRequest> processingQueue = 
        new System.Collections.Generic.Queue<ProcessingRequest>();
    private const int MAX_QUEUE_SIZE = 5;
    
    // Performance tracking
    private float lastProcessTime = 0f;
    private float processingInterval;
    
    // Events
    public event Action OnInitialized;
    public event Action<string> OnError;
    public event Action<IList<NormalizedLandmark>> OnPoseDetected;
    public event Action OnProcessingStarted;
    public event Action OnProcessingCompleted;
    
    // Processing request structure
    private struct ProcessingRequest
    {
        public Texture2D inputTexture;
        public float timestamp;
        public Action<IList<NormalizedLandmark>> callback;
    }
    
    private void Awake()
    {
        processingInterval = 1f / maxProcessingFPS;
        
        // Set delegate type based on platform
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        delegateType = BaseOptions.Delegate.CPU;
#else
        delegateType = enableGPUAcceleration ? BaseOptions.Delegate.GPU : BaseOptions.Delegate.CPU;
#endif
        
        InitializeMediaPipe();
    }
    
    private void InitializeMediaPipe()
    {
        StartCoroutine(InitializeCoroutine());
    }
    
    private IEnumerator InitializeCoroutine()
    {
        try
        {
            Debug.Log("[MediaPipeManager] Initializing MediaPipe...");
            
            // Create pose landmarker options
            yield return StartCoroutine(CreatePoseLandmarker());
            
            isInitialized = true;
            currentRetries = 0;
            
            Debug.Log("[MediaPipeManager] ✓ MediaPipe initialized successfully");
            OnInitialized?.Invoke();
        }
        catch (Exception e)
        {
            HandleInitializationError(e);
        }
    }
    
    private IEnumerator CreatePoseLandmarker()
    {
        try
        {
            // Get model path based on complexity
            string modelPath = GetModelPath();
            
            var options = new PoseLandmarkerOptions(
                baseOptions: new Tasks.Core.BaseOptions(delegateType, modelAssetPath: modelPath),
                runningMode: runningMode,
                numPoses: 1,
                minPoseDetectionConfidence: minDetectionConfidence,
                minPosePresenceConfidence: minTrackingConfidence,
                minTrackingConfidence: minTrackingConfidence,
                outputSegmentationMasks: enableSegmentation,
                resultCallback: runningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnPoseDetectionResult : null
            );
            
            poseLandmarker = PoseLandmarker.CreateFromOptions(options);
            
            Debug.Log("[MediaPipeManager] Pose landmarker created");
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to create pose landmarker: {e.Message}");
        }
        
        yield return null;
    }
    
    private string GetModelPath()
    {
        switch (modelComplexity)
        {
            case 0:
                return "pose_landmarker_lite.bytes";
            case 1:
                return "pose_landmarker_full.bytes";
            case 2:
                return "pose_landmarker_heavy.bytes";
            default:
                return "pose_landmarker_full.bytes";
        }
    }
    
    private void HandleInitializationError(Exception e)
    {
        currentRetries++;
        string errorMessage = $"MediaPipe initialization failed (attempt {currentRetries}/{maxRetries}): {e.Message}";
        
        Debug.LogError($"[MediaPipeManager] {errorMessage}");
        OnError?.Invoke(errorMessage);
        
        if (currentRetries < maxRetries && autoRestart)
        {
            Debug.Log($"[MediaPipeManager] Retrying initialization in {retryDelay} seconds...");
            StartCoroutine(RetryInitialization());
        }
        else
        {
            Debug.LogError("[MediaPipeManager] Max retries reached. MediaPipe initialization failed permanently.");
            isInitialized = false;
        }
    }
    
    private IEnumerator RetryInitialization()
    {
        yield return new WaitForSeconds(retryDelay);
        CleanupResources();
        InitializeMediaPipe();
    }
    
    // Public processing methods
    public void ProcessImageAsync(Texture2D inputTexture, Action<IList<NormalizedLandmark>> callback = null)
    {
        if (!isInitialized || poseLandmarker == null)
        {
            Debug.LogWarning("[MediaPipeManager] Cannot process image - MediaPipe not initialized");
            return;
        }
        
        // Throttle processing based on max FPS
        if (Time.time - lastProcessTime < processingInterval)
        {
            return;
        }
        
        var request = new ProcessingRequest
        {
            inputTexture = inputTexture,
            timestamp = Time.time,
            callback = callback
        };
        
        if (runningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM)
        {
            QueueProcessingRequest(request);
        }
        else
        {
            ProcessImageSync(request);
        }
    }
    
    private void QueueProcessingRequest(ProcessingRequest request)
    {
        // Remove old requests if queue is full
        while (processingQueue.Count >= MAX_QUEUE_SIZE)
        {
            processingQueue.Dequeue();
        }
        
        processingQueue.Enqueue(request);
        
        if (!isProcessing)
        {
            StartCoroutine(ProcessQueueCoroutine());
        }
    }
    
    private IEnumerator ProcessQueueCoroutine()
    {
        isProcessing = true;
        OnProcessingStarted?.Invoke();
        
        while (processingQueue.Count > 0)
        {
            var request = processingQueue.Dequeue();
            yield return StartCoroutine(ProcessSingleRequest(request));
            
            // Small delay to prevent overwhelming the system
            yield return null;
        }
        
        isProcessing = false;
        OnProcessingCompleted?.Invoke();
    }
    
    private IEnumerator ProcessSingleRequest(ProcessingRequest request)
    {
        try
        {
            // Convert texture to MediaPipe image format
            var image = CreateImageFromTexture(request.inputTexture);
            
            // Process with MediaPipe
            long timestampMs = (long)(request.timestamp * 1000);
            
            if (runningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM)
            {
                poseLandmarker.DetectAsync(image, timestampMs);
            }
            else if (runningMode == Tasks.Vision.Core.RunningMode.VIDEO)
            {
                var result = poseLandmarker.DetectForVideo(image, timestampMs);
                if (result.poseLandmarks != null && result.poseLandmarks.Count > 0)
                {
                    OnPoseDetected?.Invoke(result.poseLandmarks[0]);
                    request.callback?.Invoke(result.poseLandmarks[0]);
                }
            }
            else
            {
                var result = poseLandmarker.Detect(image);
                if (result.poseLandmarks != null && result.poseLandmarks.Count > 0)
                {
                    OnPoseDetected?.Invoke(result.poseLandmarks[0]);
                    request.callback?.Invoke(result.poseLandmarks[0]);
                }
            }
            
            lastProcessTime = Time.time;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MediaPipeManager] Processing error: {e.Message}");
            OnError?.Invoke($"Processing error: {e.Message}");
        }
        
        yield return null;
    }
    
    private void ProcessImageSync(ProcessingRequest request)
    {
        try
        {
            var image = CreateImageFromTexture(request.inputTexture);
            var result = poseLandmarker.Detect(image);
            
            lastProcessTime = Time.time;
            
            if (result.poseLandmarks != null && result.poseLandmarks.Count > 0)
            {
                OnPoseDetected?.Invoke(result.poseLandmarks[0]);
                request.callback?.Invoke(result.poseLandmarks[0]);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MediaPipeManager] Sync processing error: {e.Message}");
            OnError?.Invoke($"Sync processing error: {e.Message}");
        }
    }
    
    private Mediapipe.Image CreateImageFromTexture(Texture2D texture)
    {
        // Convert Unity texture to MediaPipe Image
        var pixels = texture.GetRawTextureData();
        
        // MediaPipe expects SRGB format with width*3 stride
        return new Mediapipe.Image(ImageFormat.Srgb, texture.width, texture.height, 
                        texture.width * 3, pixels);
    }
    
    // Event handlers
    private void OnPoseDetectionResult(PoseLandmarkerResult result, Mediapipe.Image image, long timestamp)
    {
        if (result.poseLandmarks != null && result.poseLandmarks.Count > 0)
        {
            OnPoseDetected?.Invoke(result.poseLandmarks[0]);
        }
    }
    
    // Configuration methods
    public void SetModelComplexity(int complexity)
    {
        modelComplexity = Mathf.Clamp(complexity, 0, 2);
        
        if (isInitialized)
        {
            StartCoroutine(RestartWithNewSettings());
        }
    }
    
    public void SetDetectionConfidence(float confidence)
    {
        minDetectionConfidence = Mathf.Clamp01(confidence);
        
        if (isInitialized)
        {
            StartCoroutine(RestartWithNewSettings());
        }
    }
    
    public void SetTrackingConfidence(float confidence)
    {
        minTrackingConfidence = Mathf.Clamp01(confidence);
        
        if (isInitialized)
        {
            StartCoroutine(RestartWithNewSettings());
        }
    }
    
    public void SetMaxProcessingFPS(int fps)
    {
        maxProcessingFPS = Mathf.Clamp(fps, 1, 60);
        processingInterval = 1f / maxProcessingFPS;
    }
    
    private IEnumerator RestartWithNewSettings()
    {
        CleanupResources();
        yield return new WaitForSeconds(0.1f);
        InitializeMediaPipe();
    }
    
    // Status getters
    public bool IsInitialized() => isInitialized;
    public bool IsProcessing() => isProcessing;
    public int GetQueueSize() => processingQueue.Count;
    public float GetLastProcessTime() => lastProcessTime;
    public int GetModelComplexity() => modelComplexity;
    public float GetDetectionConfidence() => minDetectionConfidence;
    public float GetTrackingConfidence() => minTrackingConfidence;
    
    // Resource management
    private void CleanupResources()
    {
        try
        {
            isInitialized = false;
            isProcessing = false;
            
            processingQueue.Clear();
            
            if (poseLandmarker != null)
            {
                poseLandmarker.Dispose();
                poseLandmarker = null;
            }
            
            Debug.Log("[MediaPipeManager] Resources cleaned up");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MediaPipeManager] Cleanup error: {e.Message}");
        }
    }
    
    private void OnDestroy()
    {
        CleanupResources();
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // Pause processing
            isProcessing = false;
            processingQueue.Clear();
        }
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            // Stop processing when app loses focus
            isProcessing = false;
            processingQueue.Clear();
        }
    }
    
    // Debug and monitoring
    public string GetStatusReport()
    {
        return $"MediaPipe Status:\n" +
               $"Initialized: {isInitialized}\n" +
               $"Processing: {isProcessing}\n" +
               $"Queue Size: {processingQueue.Count}/{MAX_QUEUE_SIZE}\n" +
               $"Model Complexity: {modelComplexity}\n" +
               $"Detection Confidence: {minDetectionConfidence:F2}\n" +
               $"Tracking Confidence: {minTrackingConfidence:F2}\n" +
               $"Max FPS: {maxProcessingFPS}\n" +
               $"Last Process Time: {lastProcessTime:F2}s";
    }
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void OnGUI()
    {
        if (!Application.isEditor) return;
        
        GUILayout.BeginArea(new Rect(10, 200, 300, 200));
        GUILayout.Box("MediaPipe Manager Debug");
        
        GUILayout.Label($"Initialized: {isInitialized}");
        GUILayout.Label($"Processing: {isProcessing}");
        GUILayout.Label($"Queue: {processingQueue.Count}/{MAX_QUEUE_SIZE}");
        GUILayout.Label($"FPS Limit: {maxProcessingFPS}");
        
        if (GUILayout.Button("Restart MediaPipe"))
        {
            StartCoroutine(RestartWithNewSettings());
        }
        
        GUILayout.EndArea();
    }
}