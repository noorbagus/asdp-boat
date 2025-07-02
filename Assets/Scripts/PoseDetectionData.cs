using UnityEngine;
using System;

/// <summary>
/// Main data structure containing pose detection information from MediaPipe
/// </summary>
[System.Serializable]
public class PoseDetectionData
{
    [Header("Detection Status")]
    public bool isValid = false;
    public float timestamp = 0f;
    public float confidence = 0f;
    
    [Header("Hand Positions (Normalized 0-1)")]
    public Vector2 leftHand = Vector2.zero;
    public Vector2 rightHand = Vector2.zero;
    public float leftHandConfidence = 0f;
    public float rightHandConfidence = 0f;
    public bool leftHandVisible = false;
    public bool rightHandVisible = false;
    
    [Header("Shoulder Positions (Normalized 0-1)")]
    public Vector2 leftShoulder = Vector2.zero;
    public Vector2 rightShoulder = Vector2.zero;
    public float leftShoulderConfidence = 0f;
    public float rightShoulderConfidence = 0f;
    
    [Header("Additional Key Points")]
    public Vector2 nose = Vector2.zero;
    public Vector2 leftElbow = Vector2.zero;
    public Vector2 rightElbow = Vector2.zero;
    public Vector2 leftHip = Vector2.zero;
    public Vector2 rightHip = Vector2.zero;
    
    [Header("Analysis Results")]
    public float handHeightDifference = 0f; // leftHand.y - rightHand.y
    public float shoulderHeightDifference = 0f; // leftShoulder.y - rightShoulder.y
    public PaddleDirection detectedPaddleDirection = PaddleDirection.None;
    public bool usingShoulderFallback = false;
    
    /// <summary>
    /// Reset all data to default values
    /// </summary>
    public void Reset()
    {
        isValid = false;
        timestamp = 0f;
        confidence = 0f;
        
        leftHand = Vector2.zero;
        rightHand = Vector2.zero;
        leftHandConfidence = 0f;
        rightHandConfidence = 0f;
        leftHandVisible = false;
        rightHandVisible = false;
        
        leftShoulder = Vector2.zero;
        rightShoulder = Vector2.zero;
        leftShoulderConfidence = 0f;
        rightShoulderConfidence = 0f;
        
        nose = Vector2.zero;
        leftElbow = Vector2.zero;
        rightElbow = Vector2.zero;
        leftHip = Vector2.zero;
        rightHip = Vector2.zero;
        
        handHeightDifference = 0f;
        shoulderHeightDifference = 0f;
        detectedPaddleDirection = PaddleDirection.None;
        usingShoulderFallback = false;
    }
    
    /// <summary>
    /// Calculate hand height difference and update analysis
    /// </summary>
    public void UpdateAnalysis()
    {
        if (!isValid) return;
        
        handHeightDifference = leftHand.y - rightHand.y;
        shoulderHeightDifference = leftShoulder.y - rightShoulder.y;
    }
    
    /// <summary>
    /// Get the most reliable position data (hands first, shoulders as fallback)
    /// </summary>
    public (Vector2 leftPos, Vector2 rightPos, bool isUsingFallback) GetBestPositionData(float confidenceThreshold = 0.7f)
    {
        bool leftHandGood = leftHandVisible && leftHandConfidence > confidenceThreshold;
        bool rightHandGood = rightHandVisible && rightHandConfidence > confidenceThreshold;
        
        if (leftHandGood && rightHandGood)
        {
            return (leftHand, rightHand, false);
        }
        
        bool leftShoulderGood = leftShoulderConfidence > confidenceThreshold;
        bool rightShoulderGood = rightShoulderConfidence > confidenceThreshold;
        
        if (leftShoulderGood && rightShoulderGood)
        {
            return (leftShoulder, rightShoulder, true);
        }
        
        // Return invalid data
        return (Vector2.zero, Vector2.zero, false);
    }
    
    /// <summary>
    /// Check if pose data is suitable for paddle detection
    /// </summary>
    public bool IsValidForPaddleDetection(float confidenceThreshold = 0.7f)
    {
        if (!isValid || confidence < confidenceThreshold) return false;
        
        var (leftPos, rightPos, isUsingFallback) = GetBestPositionData(confidenceThreshold);
        return leftPos != Vector2.zero && rightPos != Vector2.zero;
    }
    
    /// <summary>
    /// Convert normalized coordinates to screen/texture coordinates
    /// </summary>
    public Vector2 NormalizedToScreen(Vector2 normalizedPos, int width, int height)
    {
        return new Vector2(normalizedPos.x * width, normalizedPos.y * height);
    }
    
    /// <summary>
    /// Get debug string representation
    /// </summary>
    public override string ToString()
    {
        return $"PoseData[Valid:{isValid}, Conf:{confidence:F2}, " +
               $"LH:{leftHand} ({leftHandConfidence:F2}), " +
               $"RH:{rightHand} ({rightHandConfidence:F2}), " +
               $"HeightDiff:{handHeightDifference:F3}]";
    }
}

/// <summary>
/// Direction of detected paddle motion
/// </summary>
public enum PaddleDirection
{
    None,
    Left,   // Left hand/shoulder lower - paddle left
    Right   // Right hand/shoulder lower - paddle right
}

/// <summary>
/// Configuration settings for pose detection
/// </summary>
[System.Serializable]
public class PoseDetectionSettings
{
    [Header("Detection Thresholds")]
    [Range(0.01f, 0.2f)]
    public float handPaddleThreshold = 0.05f;
    
    [Range(0.01f, 0.3f)]
    public float shoulderPaddleThreshold = 0.08f;
    
    [Range(0.1f, 0.9f)]
    public float confidenceThreshold = 0.7f;
    
    [Header("Timing Settings")]
    [Range(0.1f, 2f)]
    public float debounceTime = 0.3f;
    
    [Range(0.01f, 0.5f)]
    public float detectionCooldown = 0.1f;
    
    [Header("Fallback Options")]
    public bool enableShoulderFallback = true;
    public bool enableElbowFallback = false;
    
    [Header("Filtering")]
    public bool enablePositionSmoothing = true;
    
    [Range(0.1f, 0.9f)]
    public float smoothingFactor = 0.3f;
    
    public bool enableOutlierFiltering = true;
    
    [Range(0.05f, 0.5f)]
    public float outlierThreshold = 0.1f;
    
    /// <summary>
    /// Get default settings
    /// </summary>
    public static PoseDetectionSettings GetDefault()
    {
        return new PoseDetectionSettings
        {
            handPaddleThreshold = 0.05f,
            shoulderPaddleThreshold = 0.08f,
            confidenceThreshold = 0.7f,
            debounceTime = 0.3f,
            detectionCooldown = 0.1f,
            enableShoulderFallback = true,
            enableElbowFallback = false,
            enablePositionSmoothing = true,
            smoothingFactor = 0.3f,
            enableOutlierFiltering = true,
            outlierThreshold = 0.1f
        };
    }
    
    /// <summary>
    /// Validate settings and clamp to safe ranges
    /// </summary>
    public void ValidateAndClamp()
    {
        handPaddleThreshold = Mathf.Clamp(handPaddleThreshold, 0.01f, 0.2f);
        shoulderPaddleThreshold = Mathf.Clamp(shoulderPaddleThreshold, 0.01f, 0.3f);
        confidenceThreshold = Mathf.Clamp(confidenceThreshold, 0.1f, 0.9f);
        debounceTime = Mathf.Clamp(debounceTime, 0.1f, 2f);
        detectionCooldown = Mathf.Clamp(detectionCooldown, 0.01f, 0.5f);
        smoothingFactor = Mathf.Clamp(smoothingFactor, 0.1f, 0.9f);
        outlierThreshold = Mathf.Clamp(outlierThreshold, 0.05f, 0.5f);
    }
}

/// <summary>
/// Historical pose data for smoothing and analysis
/// </summary>
[System.Serializable]
public class PoseDataHistory
{
    [Header("History Settings")]
    public int maxHistorySize = 10;
    public bool enableSmoothing = true;
    
    private System.Collections.Generic.Queue<PoseDetectionData> history = 
        new System.Collections.Generic.Queue<PoseDetectionData>();
    
    /// <summary>
    /// Add new pose data to history
    /// </summary>
    public void AddData(PoseDetectionData data)
    {
        history.Enqueue(data);
        
        while (history.Count > maxHistorySize)
        {
            history.Dequeue();
        }
    }
    
    /// <summary>
    /// Get smoothed pose data from history
    /// </summary>
    public PoseDetectionData GetSmoothedData()
    {
        if (history.Count == 0) return new PoseDetectionData();
        
        if (!enableSmoothing || history.Count == 1)
        {
            return history.ToArray()[history.Count - 1];
        }
        
        // Calculate weighted average of recent frames
        var smoothedData = new PoseDetectionData();
        float totalWeight = 0f;
        var historyArray = history.ToArray();
        
        for (int i = 0; i < historyArray.Length; i++)
        {
            // More recent frames have higher weight
            float weight = (float)(i + 1) / historyArray.Length;
            var data = historyArray[i];
            
            if (data.isValid)
            {
                smoothedData.leftHand += data.leftHand * weight;
                smoothedData.rightHand += data.rightHand * weight;
                smoothedData.leftShoulder += data.leftShoulder * weight;
                smoothedData.rightShoulder += data.rightShoulder * weight;
                smoothedData.confidence += data.confidence * weight;
                totalWeight += weight;
            }
        }
        
        if (totalWeight > 0f)
        {
            smoothedData.leftHand /= totalWeight;
            smoothedData.rightHand /= totalWeight;
            smoothedData.leftShoulder /= totalWeight;
            smoothedData.rightShoulder /= totalWeight;
            smoothedData.confidence /= totalWeight;
            
            // Copy other properties from most recent frame
            var latest = historyArray[historyArray.Length - 1];
            smoothedData.isValid = latest.isValid;
            smoothedData.timestamp = latest.timestamp;
            smoothedData.leftHandVisible = latest.leftHandVisible;
            smoothedData.rightHandVisible = latest.rightHandVisible;
            smoothedData.leftHandConfidence = latest.leftHandConfidence;
            smoothedData.rightHandConfidence = latest.rightHandConfidence;
            
            smoothedData.UpdateAnalysis();
        }
        
        return smoothedData;
    }
    
    /// <summary>
    /// Clear history
    /// </summary>
    public void Clear()
    {
        history.Clear();
    }
    
    /// <summary>
    /// Get current history size
    /// </summary>
    public int GetHistorySize()
    {
        return history.Count;
    }
    
    /// <summary>
    /// Check if enough data is available for reliable detection
    /// </summary>
    public bool HasSufficientData()
    {
        return history.Count >= Mathf.Min(3, maxHistorySize / 2);
    }
}

/// <summary>
/// Performance metrics for pose detection system
/// </summary>
[System.Serializable]
public class PoseDetectionMetrics
{
    [Header("Frame Rate")]
    public float currentFPS = 0f;
    public float averageFPS = 0f;
    public float targetFPS = 30f;
    
    [Header("Processing Time")]
    public float lastFrameTime = 0f;
    public float averageFrameTime = 0f;
    public float maxFrameTime = 0f;
    
    [Header("Detection Quality")]
    public float averageConfidence = 0f;
    public int validFramesCount = 0;
    public int totalFramesProcessed = 0;
    public float detectionSuccessRate = 0f;
    
    [Header("Paddle Triggers")]
    public int leftPaddleCount = 0;
    public int rightPaddleCount = 0;
    public float lastPaddleTime = 0f;
    
    private System.Collections.Generic.Queue<float> frameTimeHistory = 
        new System.Collections.Generic.Queue<float>();
    private System.Collections.Generic.Queue<float> confidenceHistory = 
        new System.Collections.Generic.Queue<float>();
    
    private const int MAX_HISTORY = 60; // Keep 2 seconds of data at 30fps
    
    /// <summary>
    /// Update metrics with new frame data
    /// </summary>
    public void UpdateFrameMetrics(float frameTime, float confidence, bool isValidFrame)
    {
        // Update frame timing
        lastFrameTime = frameTime;
        frameTimeHistory.Enqueue(frameTime);
        
        while (frameTimeHistory.Count > MAX_HISTORY)
        {
            frameTimeHistory.Dequeue();
        }
        
        // Calculate averages
        float totalFrameTime = 0f;
        foreach (float time in frameTimeHistory)
        {
            totalFrameTime += time;
            if (time > maxFrameTime) maxFrameTime = time;
        }
        averageFrameTime = totalFrameTime / frameTimeHistory.Count;
        currentFPS = frameTimeHistory.Count > 0 ? 1f / averageFrameTime : 0f;
        
        // Update detection quality
        totalFramesProcessed++;
        if (isValidFrame)
        {
            validFramesCount++;
            confidenceHistory.Enqueue(confidence);
            
            while (confidenceHistory.Count > MAX_HISTORY)
            {
                confidenceHistory.Dequeue();
            }
            
            float totalConfidence = 0f;
            foreach (float conf in confidenceHistory)
            {
                totalConfidence += conf;
            }
            averageConfidence = totalConfidence / confidenceHistory.Count;
        }
        
        detectionSuccessRate = (float)validFramesCount / totalFramesProcessed;
    }
    
    /// <summary>
    /// Record paddle trigger
    /// </summary>
    public void RecordPaddleTrigger(PaddleDirection direction)
    {
        lastPaddleTime = Time.time;
        
        if (direction == PaddleDirection.Left)
            leftPaddleCount++;
        else if (direction == PaddleDirection.Right)
            rightPaddleCount++;
    }
    
    /// <summary>
    /// Reset all metrics
    /// </summary>
    public void Reset()
    {
        currentFPS = 0f;
        averageFPS = 0f;
        lastFrameTime = 0f;
        averageFrameTime = 0f;
        maxFrameTime = 0f;
        averageConfidence = 0f;
        validFramesCount = 0;
        totalFramesProcessed = 0;
        detectionSuccessRate = 0f;
        leftPaddleCount = 0;
        rightPaddleCount = 0;
        lastPaddleTime = 0f;
        
        frameTimeHistory.Clear();
        confidenceHistory.Clear();
    }
    
    /// <summary>
    /// Get performance summary string
    /// </summary>
    public string GetPerformanceSummary()
    {
        return $"FPS: {currentFPS:F1}/{targetFPS:F0}, " +
               $"Frame: {averageFrameTime * 1000:F1}ms, " +
               $"Success: {detectionSuccessRate * 100:F1}%, " +
               $"Conf: {averageConfidence:F2}, " +
               $"Paddles: L{leftPaddleCount}/R{rightPaddleCount}";
    }
}