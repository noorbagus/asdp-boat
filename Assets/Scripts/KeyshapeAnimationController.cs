using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controller for managing Keyshape animations on a model
/// Supports multiple animation components with individual loop settings
/// </summary>
public class KeyshapeAnimationController : MonoBehaviour
{
    [System.Serializable]
    public class KeyshapeAnimationInfo
    {
        public string animationName;
        public SkinnedMeshRenderer targetRenderer;
        public int blendShapeIndex;
        public float animationDuration = 1f;
        public bool loop = true;
        public AnimationCurve animationCurve = AnimationCurve.Linear(0, 0, 1, 100);
        public bool playOnStart = false;
        public float delayBeforeStart = 0f;
        public float delayAfterEnd = 0f;
        public bool pingPong = false;
        public bool reverse = false;
        
        [HideInInspector]
        public bool isPlaying = false;
        [HideInInspector]
        public float currentTime = 0f;
        [HideInInspector]
        public bool isReversing = false;
    }
    
    [Header("Animation Settings")]
    public List<KeyshapeAnimationInfo> animations = new List<KeyshapeAnimationInfo>();
    
    [Header("Global Settings")]
    public bool pauseAllOnDisable = true;
    public bool playAllOnEnable = false;
    public float globalAnimationSpeed = 1f;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    public int selectedAnimation = -1;
    
    // Private variables for tracking animation state
    private Dictionary<string, int> animationNameToIndex = new Dictionary<string, int>();
    
    private void Start()
    {
        // Build name lookup dictionary
        RebuildAnimationDictionary();
        
        // Auto-start animations marked for playOnStart
        for (int i = 0; i < animations.Count; i++)
        {
            KeyshapeAnimationInfo anim = animations[i];
            if (anim.playOnStart)
            {
                if (anim.delayBeforeStart > 0)
                {
                    StartCoroutine(PlayAnimationDelayed(i, anim.delayBeforeStart));
                }
                else
                {
                    PlayAnimation(i);
                }
            }
        }
    }
    
    private void OnEnable()
    {
        if (playAllOnEnable)
        {
            PlayAllAnimations();
        }
    }
    
    private void OnDisable()
    {
        if (pauseAllOnDisable)
        {
            PauseAllAnimations();
        }
    }
    
    private void Update()
    {
        // Update all active animations
        for (int i = 0; i < animations.Count; i++)
        {
            KeyshapeAnimationInfo anim = animations[i];
            
            if (anim.isPlaying && anim.targetRenderer != null)
            {
                // Validate blendshape index
                if (anim.blendShapeIndex >= 0 && anim.blendShapeIndex < anim.targetRenderer.sharedMesh.blendShapeCount)
                {
                    UpdateAnimation(i);
                }
                else if (showDebugInfo)
                {
                    Debug.LogWarning($"Invalid blendshape index {anim.blendShapeIndex} for animation {anim.animationName}");
                    anim.isPlaying = false;
                }
            }
        }
    }
    
    private void UpdateAnimation(int index)
    {
        KeyshapeAnimationInfo anim = animations[index];
        
        // Update animation time
        float direction = anim.isReversing ? -1 : 1;
        if (anim.reverse) direction *= -1;
        
        anim.currentTime += Time.deltaTime * globalAnimationSpeed * direction;
        
        // Handle animation completion or loop
        if (anim.currentTime >= anim.animationDuration)
        {
            if (anim.pingPong)
            {
                anim.isReversing = true;
                anim.currentTime = anim.animationDuration;
            }
            else if (anim.loop)
            {
                anim.currentTime = 0f;
            }
            else
            {
                anim.currentTime = anim.animationDuration;
                anim.isPlaying = false;
                
                if (showDebugInfo)
                {
                    Debug.Log($"Animation {anim.animationName} completed");
                }
                
                // Handle delay after end before checking completion
                if (anim.delayAfterEnd > 0)
                {
                    StartCoroutine(AnimationCompletedDelayed(index, anim.delayAfterEnd));
                }
                else
                {
                    OnAnimationCompleted(index);
                }
                
                return;
            }
        }
        else if (anim.currentTime <= 0 && anim.isReversing)
        {
            if (anim.pingPong)
            {
                anim.isReversing = false;
                anim.currentTime = 0f;
            }
            else if (anim.loop)
            {
                anim.currentTime = anim.animationDuration;
            }
            else
            {
                anim.currentTime = 0f;
                anim.isPlaying = false;
                
                if (showDebugInfo)
                {
                    Debug.Log($"Animation {anim.animationName} completed (reverse)");
                }
                
                // Handle delay after end
                if (anim.delayAfterEnd > 0)
                {
                    StartCoroutine(AnimationCompletedDelayed(index, anim.delayAfterEnd));
                }
                else
                {
                    OnAnimationCompleted(index);
                }
                
                return;
            }
        }
        
        // Calculate blend shape weight
        float normalizedTime = anim.currentTime / anim.animationDuration;
        float weight = anim.animationCurve.Evaluate(normalizedTime);
        
        // Apply to blend shape
        anim.targetRenderer.SetBlendShapeWeight(anim.blendShapeIndex, weight);
        
        if (showDebugInfo && index == selectedAnimation)
        {
            Debug.Log($"Animation {anim.animationName}: Time={anim.currentTime:F2}/{anim.animationDuration:F2}, Weight={weight:F2}");
        }
    }
    
    private System.Collections.IEnumerator PlayAnimationDelayed(int index, float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayAnimation(index);
    }
    
    private System.Collections.IEnumerator AnimationCompletedDelayed(int index, float delay)
    {
        yield return new WaitForSeconds(delay);
        OnAnimationCompleted(index);
    }
    
    // Called when an animation completes
    private void OnAnimationCompleted(int index)
    {
        // Override this method in derived classes to handle animation completion events
        if (showDebugInfo)
        {
            Debug.Log($"Animation {animations[index].animationName} completed");
        }
        
        // Animation completed event can be added here
        // For example: animationCompletedEvent.Invoke(animations[index].animationName);
    }
    
    // Rebuild the name-to-index dictionary
    private void RebuildAnimationDictionary()
    {
        animationNameToIndex.Clear();
        for (int i = 0; i < animations.Count; i++)
        {
            if (!string.IsNullOrEmpty(animations[i].animationName))
            {
                animationNameToIndex[animations[i].animationName] = i;
            }
        }
    }
    
    #region Public Control Methods
    
    /// <summary>
    /// Play animation by index
    /// </summary>
    public void PlayAnimation(int index)
    {
        if (index >= 0 && index < animations.Count)
        {
            KeyshapeAnimationInfo anim = animations[index];
            anim.isPlaying = true;
            
            // Reset animation state if needed
            if (anim.reverse)
            {
                if (!anim.isReversing)
                    anim.currentTime = anim.animationDuration;
            }
            else
            {
                if (anim.isReversing)
                    anim.currentTime = 0;
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"Playing animation {anim.animationName}");
            }
        }
    }
    
    /// <summary>
    /// Play animation by name
    /// </summary>
    public void PlayAnimation(string animationName)
    {
        if (animationNameToIndex.TryGetValue(animationName, out int index))
        {
            PlayAnimation(index);
        }
        else if (showDebugInfo)
        {
            Debug.LogWarning($"Animation {animationName} not found");
        }
    }
    
    /// <summary>
    /// Pause animation by index
    /// </summary>
    public void PauseAnimation(int index)
    {
        if (index >= 0 && index < animations.Count)
        {
            animations[index].isPlaying = false;
            
            if (showDebugInfo)
            {
                Debug.Log($"Paused animation {animations[index].animationName}");
            }
        }
    }
    
    /// <summary>
    /// Pause animation by name
    /// </summary>
    public void PauseAnimation(string animationName)
    {
        if (animationNameToIndex.TryGetValue(animationName, out int index))
        {
            PauseAnimation(index);
        }
        else if (showDebugInfo)
        {
            Debug.LogWarning($"Animation {animationName} not found");
        }
    }
    
    /// <summary>
    /// Stop animation and reset to initial state
    /// </summary>
    public void StopAnimation(int index)
    {
        if (index >= 0 && index < animations.Count)
        {
            KeyshapeAnimationInfo anim = animations[index];
            anim.isPlaying = false;
            
            // Reset to initial state
            if (anim.reverse)
            {
                anim.currentTime = anim.animationDuration;
                anim.targetRenderer.SetBlendShapeWeight(anim.blendShapeIndex, 
                    anim.animationCurve.Evaluate(1));
            }
            else
            {
                anim.currentTime = 0;
                anim.targetRenderer.SetBlendShapeWeight(anim.blendShapeIndex, 
                    anim.animationCurve.Evaluate(0));
            }
            
            anim.isReversing = false;
            
            if (showDebugInfo)
            {
                Debug.Log($"Stopped animation {anim.animationName}");
            }
        }
    }
    
    /// <summary>
    /// Stop animation by name
    /// </summary>
    public void StopAnimation(string animationName)
    {
        if (animationNameToIndex.TryGetValue(animationName, out int index))
        {
            StopAnimation(index);
        }
        else if (showDebugInfo)
        {
            Debug.LogWarning($"Animation {animationName} not found");
        }
    }
    
    /// <summary>
    /// Toggle animation playback state
    /// </summary>
    public void ToggleAnimation(int index)
    {
        if (index >= 0 && index < animations.Count)
        {
            if (animations[index].isPlaying)
            {
                PauseAnimation(index);
            }
            else
            {
                PlayAnimation(index);
            }
        }
    }
    
    /// <summary>
    /// Toggle animation by name
    /// </summary>
    public void ToggleAnimation(string animationName)
    {
        if (animationNameToIndex.TryGetValue(animationName, out int index))
        {
            ToggleAnimation(index);
        }
        else if (showDebugInfo)
        {
            Debug.LogWarning($"Animation {animationName} not found");
        }
    }
    
    /// <summary>
    /// Play all animations
    /// </summary>
    public void PlayAllAnimations()
    {
        for (int i = 0; i < animations.Count; i++)
        {
            PlayAnimation(i);
        }
    }
    
    /// <summary>
    /// Pause all animations
    /// </summary>
    public void PauseAllAnimations()
    {
        for (int i = 0; i < animations.Count; i++)
        {
            PauseAnimation(i);
        }
    }
    
    /// <summary>
    /// Stop all animations
    /// </summary>
    public void StopAllAnimations()
    {
        for (int i = 0; i < animations.Count; i++)
        {
            StopAnimation(i);
        }
    }
    
    /// <summary>
    /// Set animation looping
    /// </summary>
    public void SetAnimationLooping(int index, bool loop)
    {
        if (index >= 0 && index < animations.Count)
        {
            animations[index].loop = loop;
        }
    }
    
    /// <summary>
    /// Set animation looping by name
    /// </summary>
    public void SetAnimationLooping(string animationName, bool loop)
    {
        if (animationNameToIndex.TryGetValue(animationName, out int index))
        {
            SetAnimationLooping(index, loop);
        }
    }
    
    /// <summary>
    /// Set animation duration
    /// </summary>
    public void SetAnimationDuration(int index, float duration)
    {
        if (index >= 0 && index < animations.Count)
        {
            animations[index].animationDuration = Mathf.Max(0.001f, duration);
        }
    }
    
    /// <summary>
    /// Set animation duration by name
    /// </summary>
    public void SetAnimationDuration(string animationName, float duration)
    {
        if (animationNameToIndex.TryGetValue(animationName, out int index))
        {
            SetAnimationDuration(index, duration);
        }
    }
    
    /// <summary>
    /// Set animation speed multiplier (affects all animations)
    /// </summary>
    public void SetGlobalAnimationSpeed(float speedMultiplier)
    {
        globalAnimationSpeed = Mathf.Max(0.001f, speedMultiplier);
    }
    
    /// <summary>
    /// Check if animation is playing
    /// </summary>
    public bool IsAnimationPlaying(int index)
    {
        if (index >= 0 && index < animations.Count)
        {
            return animations[index].isPlaying;
        }
        return false;
    }
    
    /// <summary>
    /// Check if animation is playing by name
    /// </summary>
    public bool IsAnimationPlaying(string animationName)
    {
        if (animationNameToIndex.TryGetValue(animationName, out int index))
        {
            return IsAnimationPlaying(index);
        }
        return false;
    }
    
    /// <summary>
    /// Set animation progress directly (0-1)
    /// </summary>
    public void SetAnimationProgress(int index, float normalizedProgress)
    {
        if (index >= 0 && index < animations.Count)
        {
            KeyshapeAnimationInfo anim = animations[index];
            
            // Clamp progress to valid range
            normalizedProgress = Mathf.Clamp01(normalizedProgress);
            
            // Set current time based on normalized progress
            anim.currentTime = normalizedProgress * anim.animationDuration;
            
            // Apply blend shape weight directly
            float weight = anim.animationCurve.Evaluate(normalizedProgress);
            anim.targetRenderer.SetBlendShapeWeight(anim.blendShapeIndex, weight);
        }
    }
    
    /// <summary>
    /// Set animation progress by name
    /// </summary>
    public void SetAnimationProgress(string animationName, float normalizedProgress)
    {
        if (animationNameToIndex.TryGetValue(animationName, out int index))
        {
            SetAnimationProgress(index, normalizedProgress);
        }
    }
    
    /// <summary>
    /// Get animation progress (0-1)
    /// </summary>
    public float GetAnimationProgress(int index)
    {
        if (index >= 0 && index < animations.Count)
        {
            KeyshapeAnimationInfo anim = animations[index];
            return anim.currentTime / anim.animationDuration;
        }
        return 0f;
    }
    
    /// <summary>
    /// Get animation progress by name
    /// </summary>
    public float GetAnimationProgress(string animationName)
    {
        if (animationNameToIndex.TryGetValue(animationName, out int index))
        {
            return GetAnimationProgress(index);
        }
        return 0f;
    }
    
    #endregion
    
    #region Editor Utilities
    
    // Add new animation to the list
    public void AddAnimation()
    {
        KeyshapeAnimationInfo newAnim = new KeyshapeAnimationInfo();
        newAnim.animationName = "Animation " + (animations.Count + 1);
        animations.Add(newAnim);
        
        // Update dictionary
        RebuildAnimationDictionary();
    }
    
    // Remove animation at index
    public void RemoveAnimation(int index)
    {
        if (index >= 0 && index < animations.Count)
        {
            animations.RemoveAt(index);
            
            // Update dictionary
            RebuildAnimationDictionary();
        }
    }
    
    #endregion
}