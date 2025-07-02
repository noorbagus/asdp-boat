using UnityEngine;
using System;
using System.Collections;
using Mediapipe;
using Mediapipe.Unity;

public class MediaPipeManager : MonoBehaviour
{
    [Header("MediaPipe Configuration")]
    [SerializeField] private RunningMode runningMode = RunningMode.Async;
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
    private PoseSolution poseSolution;
    private GpuResources gpuResources;
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
    public event Action<PoseLandmarks> OnPoseDetected;
    public event Action OnProcessingStarted;
    public event Action OnProcessingCompleted;
    
    // Processing request structure
    private struct ProcessingRequest
    {
        public Texture2D inputTexture;
        public float timestamp;
        public Action<PoseLandmarks> callback;
    }
    
    public enum RunningMode
    {
        Sync,
        Async,
        Threaded
    }
    
    private void Awake()
    {
        processingInterval = 1f / maxProcessingFPS;
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
            
            // Initialize GPU resources if enabled
            if (enableGPUAcceleration)
            {
                yield return StartCoroutine(InitializeGPUResources());
            }
            
            // Create pose solution
            yield return StartCoroutine(CreatePoseSolution());
            
            // Configure solution
            ConfigurePoseSolution();
            
            // Start the solution
            yield return StartCoroutine(StartPoseSolution());
            
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
    
    private IEnumerator InitializeGPUResources()
    {
        try
        {
            gpuResources = GpuResources.Create().Value();
            Debug.Log("[MediaPipeManager] GPU resources initialized");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MediaPipeManager] GPU initialization failed: {e.Message}, falling back to CPU");
            enableGPUAcceleration = false;
        }
        
        yield return null;
    }
    
    private IEnumerator CreatePoseSolution()
    {
        try
        {
            var config = new PoseConfig
            {
                running_mode = ConvertRunningMode(runningMode),
                num_poses = 1,
                min_pose_detection_confidence = minDetectionConfidence,
                min_pose_presence_confidence = minTrackingConfidence,
                min_tracking_confidence = minTrackingConfidence,
                output_segmentation_masks = enableSegmentation
            };
            
            poseSolution = new PoseSolution(config);
            
            // Set up callbacks
            poseSolution.OnPoseDetectionOutput += OnPoseDetectionResult;
            poseSolution.OnError += OnMediaPipeError;
            
            Debug.Log("[MediaPipeManager] Pose solution created");
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to create pose solution: {e.Message}");
        }
        
        yield return null;
    }
    
    private void ConfigurePoseSolution()
    {
        if (poseSolution == null) return;
        
        // Configure processing options
        var processingOptions = new ProcessingOptions
        {
            model_complexity = modelComplexity,
            smooth_landmarks = smoothLandmarks,
            enable_segmentation = enableSegmentation,
            smooth_segmentation = true,
            use_previous_landmarks = true
        };
        
        poseSolution.SetProcessingOptions(processingOptions);
        
        // Configure threading if supported
        if (runningMode == RunningMode.Threaded)
        {
            poseSolution.SetNumThreads(numThreads);
        }
        
        Debug.Log($"[MediaPipeManager] Pose solution configured - Complexity: {modelComplexity}, Smooth: {smoothLandmarks}");
    }
    
    private IEnumerator StartPoseSolution()
    {
        try
        {
            poseSolution.Start();
            Debug.Log("[MediaPipeManager] Pose solution started");
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to start pose solution: {e.Message}");
        }
        
        yield return new WaitForSeconds(0.1f); // Allow startup time
    }
    
    private RunningMode ConvertRunningMode(RunningMode mode)
    {
        switch (mode)
        {
            case RunningMode.Sync: return RunningMode.Sync;
            case RunningMode.Async: return RunningMode.Async;
            case RunningMode.Threaded: return RunningMode.Async; // Use async for threaded
            default: return RunningMode.Async;
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
    public void ProcessImageAsync(Texture2D inputTexture, Action<PoseLandmarks> callback = null)
    {
        if (!isInitialized || poseSolution == null)
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
        
        if (runningMode == RunningMode.Async || runningMode == RunningMode.Threaded)
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
            var imageFrame = CreateImageFrame(request.inputTexture);
            
            // Process with MediaPipe
            var result = poseSolution.Process(imageFrame);
            
            // Wait for async result if needed
            if (runningMode == RunningMode.Async)
            {
                yield return new WaitUntil(() => result.HasValue);
            }
            
            lastProcessTime = Time.time;
            
            // Handle result
            if (result.HasValue && result.Value.pose_landmarks != null)
            {
                var landmarks = result.Value.pose_landmarks;
                
                // Trigger callbacks
                OnPoseDetected?.Invoke(landmarks);
                request.callback?.Invoke(landmarks);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MediaPipeManager] Processing error: {e.Message}");
            OnError?.Invoke($"Processing error: {e.Message}");
        }
    }
    
    private void ProcessImageSync(ProcessingRequest request)
    {
        try
        {
            var imageFrame = CreateImageFrame(request.inputTexture);
            var result = poseSolution.Process(imageFrame);
            
            lastProcessTime = Time.time;
            
            if (result.HasValue && result.Value.pose_landmarks != null)
            {
                var landmarks = result.Value.pose_landmarks;
                OnPoseDetected?.Invoke(landmarks);
                request.callback?.Invoke(landmarks);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[MediaPipeManager] Sync processing error: {e.Message}");
            OnError?.Invoke($"Sync processing error: {e.Message}");
        }
    }
    
    private ImageFrame CreateImageFrame(Texture2D texture)
    {
        // Convert Unity texture to MediaPipe ImageFrame
        var pixels = texture.GetPixels32();
        var width = texture.width;
        var height = texture.height;
        
        // Create RGB byte array
        byte[] rgbData = new byte[width * height * 3];
        int rgbIndex = 0;
        
        // Convert from Unity's bottom-left origin to top-left origin
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = pixels[y * width + x];
                rgbData[rgbIndex++] = pixel.r;
                rgbData[rgbIndex++] = pixel.g;
                rgbData[rgbIndex++] = pixel.b;
            }
        }
        
        return new ImageFrame(ImageFormat.Srgb, width, height, width * 3, rgbData);
    }
    
    // Event handlers
    private void OnPoseDetectionResult(PoseDetectionResult result)
    {
        if (result.pose_landmarks != null)
        {
            OnPoseDetected?.Invoke(result.pose_landmarks);
        }
    }
    
    private void OnMediaPipeError(Status status)
    {
        string errorMessage = $"MediaPipe error: {status.ToString()}";
        Debug.LogError($"[MediaPipeManager] {errorMessage}");
        OnError?.Invoke(errorMessage);
        
        if (autoRestart && status.Code == StatusCode.Internal)
        {
            StartCoroutine(RestartMediaPipe());
        }
    }
    
    private IEnumerator RestartMediaPipe()
    {
        Debug.Log("[MediaPipeManager] Attempting to restart MediaPipe...");
        
        CleanupResources();
        yield return new WaitForSeconds(retryDelay);
        InitializeMediaPipe();
    }
    
    // Configuration methods
    public void SetModelComplexity(int complexity)
    {
        modelComplexity = Mathf.Clamp(complexity, 0, 2);
        
        if (isInitialized)
        {
            ConfigurePoseSolution();
        }
    }
    
    public void SetDetectionConfidence(float confidence)
    {
        minDetectionConfidence = Mathf.Clamp01(confidence);
        
        if (isInitialized)
        {
            ConfigurePoseSolution();
        }
    }
    
    public void SetTrackingConfidence(float confidence)
    {
        minTrackingConfidence = Mathf.Clamp01(confidence);
        
        if (isInitialized)
        {
            ConfigurePoseSolution();
        }
    }
    
    public void SetMaxProcessingFPS(int fps)
    {
        maxProcessingFPS = Mathf.Clamp(fps, 1, 60);
        processingInterval = 1f / maxProcessingFPS;
    }
    
    public void SetSmoothLandmarks(bool smooth)
    {
        smoothLandmarks = smooth;
        
        if (isInitialized)
        {
            ConfigurePoseSolution();
        }
    }
    
    // Status getters
    public bool IsInitialized() => isInitialized;
    public bool IsProcessing() => isProcessing;
    public int GetQueueSize() => processingQueue.Count;
    public float GetLastProcessTime() => lastProcessTime;
    public int GetModelComplexity() => modelComplexity;
    public float GetDetectionConfidence() => minDetectionConfidence;
    public float GetTrackingConfidence() => minTrackingConfidence;
    public bool IsGPUAccelerated() => enableGPUAcceleration && gpuResources != null;
    
    // Resource management
    private void CleanupResources()
    {
        try
        {
            isInitialized = false;
            isProcessing = false;
            
            processingQueue.Clear();
            
            if (poseSolution != null)
            {
                poseSolution.Stop();
                poseSolution.Close();
                poseSolution = null;
            }
            
            if (gpuResources != null)
            {
                gpuResources.Dispose();
                gpuResources = null;
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
        else if (isInitialized)
        {
            // Resume processing
            // MediaPipe should automatically resume
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
               $"GPU Accelerated: {IsGPUAccelerated()}\n" +
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
        GUILayout.Label($"GPU: {IsGPUAccelerated()}");
        GUILayout.Label($"FPS Limit: {maxProcessingFPS}");
        
        if (GUILayout.Button("Restart MediaPipe"))
        {
            StartCoroutine(RestartMediaPipe());
        }
        
        GUILayout.EndArea();
    }
}