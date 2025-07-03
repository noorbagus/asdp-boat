using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum PaddleLogic { Keyboard, Gyro }

public class BoatController : MonoBehaviour
{
    public enum InputMode
    {
        Keyboard,                // Standard keyboard testing mode
        BluetoothSensor,         // Physical paddle controller
        KeyboardWithDirectControl // Keyboard with direct movement control
    }

    [Header("Input Settings - MANAGED BY CentralInputProcessor")]
    [SerializeField] private InputMode inputMode = InputMode.BluetoothSensor;
    
    [Header("Paddle Logic")]
    [Tooltip("Keyboard: Right tilt = Left paddle (for turning right). Gyro: Right tilt = Right paddle (mirror)")]
    public PaddleLogic paddleLogic = PaddleLogic.Gyro;
    
    [Header("Pattern Integration")]
    public GyroPatternDetector patternDetector;
    [SerializeField] private float directControlForce = 2.0f;

    [Header("Rotation Settings")]
    [SerializeField] public float turnAngle = 15f;
    [SerializeField] public float rotationSpeed = 5f;
    [SerializeField] public float autoStraightenDelay = 2f;
    [SerializeField] public bool enableAutoStraighten = true;
    [SerializeField] private bool enableSmoothCurve = true;
    [SerializeField] public AnimationCurve turnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] public Vector3 rotationPivotOffset = Vector3.zero;

    [Header("Smooth Rotation")]
    [SerializeField] private bool useSmoothedRotation = true;
    [SerializeField] private float rotationSmoothing = 8f;
    [SerializeField] private float maxRotationSpeed = 120f;

    [Header("Boat Properties")]
    [SerializeField] private float forwardSpeed = 0.0f;
    [SerializeField] private float maxSpeed = 5.0f;
    [SerializeField] private float paddleForce = 0.5f;
    [SerializeField] private float turnForce = 10.0f;
    [SerializeField] private float paddleCooldown = 0.5f;
    [SerializeField] private float waterDrag = 0.2f;
    [SerializeField] private float bounceFactor = 500f;
    [SerializeField] private float minSpeedToMakeSound = 0.2f;
    [SerializeField] private bool reverseDirection = false;

    [Header("Paddle Pattern Settings")]
    [SerializeField] private float patternTimeWindow = 2.0f;
    [SerializeField] private int consecutivePaddlesForTurn = 2;
    [SerializeField] private float consecutiveTimeout = 1.5f;

    [Header("Water Current Settings")]
    [SerializeField] private bool inheritWaterCurrent = true;
    [SerializeField] private float waterCurrentInfluence = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Effects")]
    [SerializeField] private ParticleSystem waterSplashEffect;
    [SerializeField] private AudioClip paddleSound;
    [SerializeField] private AudioClip collisionSound;
    [SerializeField] private AudioClip collectSound;

    // Components
    private Rigidbody boatRb;
    private AudioSource audioSource;
    private GameManager gameManager;
    private Suimono.Core.SuimonoModule suimonoModule;

    // Paddle state
    private bool canPaddleLeft = true;
    private bool canPaddleRight = true;
    private bool isLeftPaddling = false;
    private bool isRightPaddling = false;
    
    // Paddle pattern tracking
    private List<PaddleStroke> paddleHistory = new List<PaddleStroke>();
    private float currentSpeed = 0.0f;
    
    // Real-time consecutive tracking
    private int currentConsecutiveLeft = 0;
    private int currentConsecutiveRight = 0;
    private float lastPaddleTime = 0f;
    private bool lastPaddleWasLeft = false;
    
    // Water data
    private float waterHeight = 0f;
    private float waveHeight = 0f;
    private float waterDirection = 0f;
    private float waterSpeed = 0f;
    
    // Rotation state tracking
    private float targetRotationY = 0f;
    private float currentRotationY = 0f;
    private float lastTurnTime = 0f;
    private bool isRotating = false;
    private float turnProgress = 0f;
    private float startRotationY = 0f;

    // Smooth rotation variables
    private float targetRotationVelocity = 0f;
    private float currentRotationVelocity = 0f;
    private float inputDecay = 4f;
    
    // Pattern integration
    private GyroPatternDetector.MovementPattern lastProcessedPattern = GyroPatternDetector.MovementPattern.Idle;
    private float patternProcessCooldown = 0f;
    private const float patternProcessInterval = 0.1f;
    
    private struct PaddleStroke
    {
        public bool isLeftPaddle;
        public float timestamp;
        
        public PaddleStroke(bool isLeft, float time)
        {
            isLeftPaddle = isLeft;
            timestamp = time;
        }
    }

    void Start()
    {
        transform.position = new Vector3(0, 0, 0);
        DebugLog("BoatController Start() - Enhanced pattern integration");
        
        // Initialize rotation
        currentRotationY = transform.eulerAngles.y;
        targetRotationY = currentRotationY;
        
        // Get components
        boatRb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        gameManager = FindObjectOfType<GameManager>();
        suimonoModule = FindObjectOfType<Suimono.Core.SuimonoModule>();
        
        // Auto-find pattern detector if not assigned
        if (patternDetector == null)
        {
            patternDetector = FindObjectOfType<GyroPatternDetector>();
        }
        
        DebugLog($"Components found - Rigidbody: {boatRb != null}, AudioSource: {audioSource != null}, GameManager: {gameManager != null}, Suimono: {suimonoModule != null}, PatternDetector: {patternDetector != null}");
        
        // Add audio source if missing
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
            audioSource.minDistance = 1.0f;
            audioSource.maxDistance = 20.0f;
            DebugLog("AudioSource component added");
        }
        
        // Register for pattern events
        RegisterPatternEvents();
    }

    void Update()
    {
        // Apply current speed for forward movement
        Vector3 moveDirection = reverseDirection ? Vector3.back : Vector3.forward;
        transform.Translate(moveDirection * currentSpeed * Time.deltaTime);
        
        // Handle smooth rotation
        UpdateRotation();
        
        // Reduce speed due to water resistance
        currentSpeed = Mathf.Max(0, currentSpeed - (waterDrag * Time.deltaTime));

        // Apply water drag to rigidbody
        if (boatRb != null)
        {
            boatRb.velocity *= (1 - (waterDrag * Time.deltaTime));
            boatRb.angularVelocity *= (1 - (waterDrag * Time.deltaTime));
        }

        // Process pattern-based movement
        ProcessPatternMovement();
        
        // Clean up old paddle history
        CleanupPaddleHistory();
        
        // Update consecutive counts
        UpdateConsecutiveCounts();
        
        // Update pattern processing cooldown
        patternProcessCooldown -= Time.deltaTime;
    }
    
    void FixedUpdate()
    {
        if (suimonoModule != null)
        {
            // Get water data at boat position
            Vector3 position = transform.position;
            float[] heightData = suimonoModule.SuimonoGetHeightAll(position);
            
            waterHeight = heightData[1];
            waveHeight = heightData[8];
            waterDirection = heightData[6];
            waterSpeed = heightData[7];
            
            // Apply water current to boat
            if (boatRb != null && inheritWaterCurrent && waterSpeed > 0.01f)
            {
                Vector2 waterDirVector = suimonoModule.SuimonoConvertAngleToVector(waterDirection);
                Vector3 waterForce = new Vector3(waterDirVector.x, 0, waterDirVector.y) * waterSpeed * waterCurrentInfluence;
                boatRb.AddForce(waterForce, ForceMode.Acceleration);
            }
        }
    }
    
    // Register for pattern detector events
    private void RegisterPatternEvents()
    {
        if (patternDetector != null)
        {
            DebugLog("Registering pattern event handlers");
        }
    }
    
    // Process pattern-based movement
    private void ProcessPatternMovement()
    {
        if (patternDetector == null || patternProcessCooldown > 0f) return;
        
        var currentPattern = patternDetector.GetCurrentPattern();
        float confidence = patternDetector.GetPatternConfidence();
        
        // Only process patterns with sufficient confidence
        if (confidence < 0.5f) return;
        
        // Avoid processing the same pattern repeatedly
        if (currentPattern == lastProcessedPattern) return;
        
        lastProcessedPattern = currentPattern;
        patternProcessCooldown = patternProcessInterval;
        
        switch (currentPattern)
        {
            case GyroPatternDetector.MovementPattern.Forward:
                ProcessForwardPattern(confidence);
                break;
            case GyroPatternDetector.MovementPattern.TurnLeft:
                ProcessTurnLeftPattern(confidence);
                break;
            case GyroPatternDetector.MovementPattern.TurnRight:
                ProcessTurnRightPattern(confidence);
                break;
            case GyroPatternDetector.MovementPattern.Idle:
                ProcessIdlePattern();
                break;
        }
    }
    
    // Pattern event handlers
    public void OnPatternChanged(GyroPatternDetector.MovementPattern pattern)
    {
        DebugLog($"Pattern changed to: {pattern}");
    }
    
    public void OnForwardPattern(float confidence)
    {
        DebugLog($"Forward pattern detected (confidence: {confidence:F2})");
        AddForwardThrust(confidence);
    }
    
    public void OnTurnLeftPattern(float confidence)
    {
        DebugLog($"Turn left pattern detected (confidence: {confidence:F2})");
        ExecuteTurnLeft(confidence);
    }
    
    public void OnTurnRightPattern(float confidence)
    {
        DebugLog($"Turn right pattern detected (confidence: {confidence:F2})");
        ExecuteTurnRight(confidence);
    }
    
    public void OnIdlePattern(float confidence)
    {
        DebugLog($"Idle pattern detected (confidence: {confidence:F2})");
    }
    
    public void OnGestureDetected(string gestureType)
    {
        DebugLog($"Gesture detected: {gestureType}");
        ProcessGesture(gestureType);
    }
    
    // Pattern processing methods
    private void ProcessForwardPattern(float confidence)
    {
        AddForwardThrust(confidence * 0.8f);
        PlayPaddleEffects(Random.value > 0.5f); // Random side for forward
    }
    
    private void ProcessTurnLeftPattern(float confidence)
    {
        ExecuteTurnLeft(confidence);
        AddForwardThrust(confidence * 0.4f);
        PlayPaddleEffects(false); // Right paddle for left turn
    }
    
    private void ProcessTurnRightPattern(float confidence)
    {
        ExecuteTurnRight(confidence);
        AddForwardThrust(confidence * 0.4f);
        PlayPaddleEffects(true); // Left paddle for right turn
    }
    
    private void ProcessIdlePattern()
    {
        if (enableAutoStraighten && Time.time - lastTurnTime > autoStraightenDelay)
        {
            AutoStraighten();
        }
    }
    
    private void ExecuteTurnLeft(float confidence)
    {
        if (useSmoothedRotation)
        {
            targetRotationVelocity = -maxRotationSpeed * confidence;
            lastTurnTime = Time.time;
        }
        else
        {
            TurnLeft();
        }
    }
    
    private void ExecuteTurnRight(float confidence)
    {
        if (useSmoothedRotation)
        {
            targetRotationVelocity = maxRotationSpeed * confidence;
            lastTurnTime = Time.time;
        }
        else
        {
            TurnRight();
        }
    }
    
    private void AutoStraighten()
    {
        float currentY = transform.eulerAngles.y;
        if (currentY > 180f) currentY -= 360f;
        
        float straightenForce = -currentY * 0.5f;
        targetRotationVelocity += straightenForce * Time.deltaTime;
    }
    
    private void ProcessGesture(string gestureType)
    {
        switch (gestureType)
        {
            case "START_GAME":
                if (gameManager != null)
                {
                    // gameManager.StartGame();
                    DebugLog("START_GAME gesture - GameManager method not available");
                }
                break;
            case "RESTART_GAME":
                if (gameManager != null)
                {
                    // gameManager.RestartGame();
                    DebugLog("RESTART_GAME gesture - GameManager method not available");
                }
                break;
        }
    }
    
    // Clean up paddle history
    private void CleanupPaddleHistory()
    {
        float currentTime = Time.time;
        while (paddleHistory.Count > 0 && currentTime - paddleHistory[0].timestamp > patternTimeWindow)
        {
            paddleHistory.RemoveAt(0);
        }
    }

    // Update consecutive counts
    private void UpdateConsecutiveCounts()
    {
        if (Time.time - lastPaddleTime > consecutiveTimeout)
        {
            if (currentConsecutiveLeft > 0 || currentConsecutiveRight > 0)
            {
                DebugLog("Resetting consecutive counts due to timeout");
                currentConsecutiveLeft = 0;
                currentConsecutiveRight = 0;
            }
        }
    }

    // PUBLIC METHODS - Called by CentralInputProcessor
    public void PaddleLeft()
    {
        DebugLog("PaddleLeft() called by CentralInputProcessor");
        
        if (!canPaddleLeft) return;

        if (useSmoothedRotation)
        {
            targetRotationVelocity = -maxRotationSpeed;
            lastTurnTime = Time.time;
        }

        RecordPaddleStroke(true);
        isLeftPaddling = true;
        isRightPaddling = false;
        
        AnalyzePaddlePattern();
        PlayPaddleEffects(true);
        StartCoroutine(LeftPaddleCooldown());
    }

    public void PaddleRight()
    {
        DebugLog("PaddleRight() called by CentralInputProcessor");
        
        if (!canPaddleRight) return;

        if (useSmoothedRotation)
        {
            targetRotationVelocity = maxRotationSpeed;
            lastTurnTime = Time.time;
        }

        RecordPaddleStroke(false);
        isRightPaddling = true;
        isLeftPaddling = false;
        
        AnalyzePaddlePattern();
        PlayPaddleEffects(false);
        StartCoroutine(RightPaddleCooldown());
    }
    
    // Record paddle stroke
    private void RecordPaddleStroke(bool isLeftPaddle)
    {
        paddleHistory.Add(new PaddleStroke(isLeftPaddle, Time.time));
        
        // Update consecutive counters
        if (isLeftPaddle)
        {
            if (lastPaddleWasLeft)
            {
                currentConsecutiveLeft++;
                currentConsecutiveRight = 0;
            }
            else
            {
                currentConsecutiveLeft = 1;
                currentConsecutiveRight = 0;
            }
            lastPaddleWasLeft = true;
        }
        else
        {
            if (!lastPaddleWasLeft)
            {
                currentConsecutiveRight++;
                currentConsecutiveLeft = 0;
            }
            else
            {
                currentConsecutiveRight = 1;
                currentConsecutiveLeft = 0;
            }
            lastPaddleWasLeft = false;
        }
        
        lastPaddleTime = Time.time;
    }
    
    // Analyze paddle pattern
    private void AnalyzePaddlePattern()
    {
        // Check for alternating pattern (forward movement)
        if (paddleHistory.Count >= 2)
        {
            bool lastWasLeft = paddleHistory[paddleHistory.Count - 1].isLeftPaddle;
            bool secondLastWasLeft = paddleHistory[paddleHistory.Count - 2].isLeftPaddle;
            
            if (lastWasLeft != secondLastWasLeft)
            {
                AddForwardThrust();
                return;
            }
        }
        
        // Apply turn effect if enough consecutive paddles
        if (currentConsecutiveLeft >= consecutivePaddlesForTurn)
        {
            TurnRight();
        }
        else if (currentConsecutiveRight >= consecutivePaddlesForTurn)
        {
            TurnLeft();
        }
        else
        {
            AddForwardThrust(0.5f);
        }
    }
    
    // Add forward thrust
    public void AddForwardThrust(float multiplier = 1.0f)
    {
        float oldSpeed = currentSpeed;
        currentSpeed = Mathf.Min(maxSpeed, currentSpeed + (paddleForce * multiplier));
        DebugLog($"Forward thrust: {oldSpeed:F2} -> {currentSpeed:F2} (x{multiplier:F2})");
    }
    
    // Rotation handling
    private void UpdateRotation()
    {
        if (useSmoothedRotation)
        {
            targetRotationVelocity = Mathf.Lerp(targetRotationVelocity, 0f, inputDecay * Time.deltaTime);
            currentRotationVelocity = Mathf.Lerp(currentRotationVelocity, targetRotationVelocity, rotationSmoothing * Time.deltaTime);
            
            float rotationThisFrame = currentRotationVelocity * Time.deltaTime;
            if (rotationPivotOffset != Vector3.zero)
            {
                Vector3 pivotWorldPos = transform.position + transform.TransformDirection(rotationPivotOffset);
                transform.RotateAround(pivotWorldPos, Vector3.up, rotationThisFrame);
            }
            else
            {
                transform.Rotate(0, rotationThisFrame, 0);
            }
            
            currentRotationY = transform.eulerAngles.y;
            
            if (enableAutoStraighten && Mathf.Abs(targetRotationVelocity) < 5f && Time.time - lastTurnTime > autoStraightenDelay)
            {
                float currentY = transform.eulerAngles.y;
                if (currentY > 180f) currentY -= 360f;
                float straightenForce = -currentY * 0.8f;
                targetRotationVelocity += straightenForce * Time.deltaTime;
            }
        }
        else
        {
            // Original rotation handling
            if (isRotating)
            {
                turnProgress += rotationSpeed * Time.deltaTime;
                turnProgress = Mathf.Clamp01(turnProgress);
                
                if (enableSmoothCurve)
                {
                    float curveValue = turnCurve.Evaluate(turnProgress);
                    currentRotationY = Mathf.LerpAngle(startRotationY, targetRotationY, curveValue);
                }
                else
                {
                    currentRotationY = Mathf.LerpAngle(currentRotationY, targetRotationY, rotationSpeed * Time.deltaTime);
                }
                
                ApplyRotationAroundPivot();
                
                if (turnProgress >= 1f || Mathf.Abs(Mathf.DeltaAngle(currentRotationY, targetRotationY)) < 1f)
                {
                    isRotating = false;
                    currentRotationY = targetRotationY;
                    ApplyRotationAroundPivot();
                }
            }
        }
    }

    private void ApplyRotationAroundPivot()
    {
        Vector3 pivotWorldPos = transform.position + transform.TransformDirection(rotationPivotOffset);
        transform.RotateAround(pivotWorldPos, Vector3.up, currentRotationY - transform.eulerAngles.y);
    }
    
    // Turn methods
    private void TurnLeft()
    {
        startRotationY = currentRotationY;
        targetRotationY -= turnAngle;
        lastTurnTime = Time.time;
        isRotating = true;
        turnProgress = 0f;
        
        if (boatRb != null)
        {
            boatRb.AddTorque(Vector3.up * -turnForce * 0.3f, ForceMode.Impulse);
        }
    }
    
    private void TurnRight()
    {
        startRotationY = currentRotationY;
        targetRotationY += turnAngle;
        lastTurnTime = Time.time;
        isRotating = true;
        turnProgress = 0f;
        
        if (boatRb != null)
        {
            boatRb.AddTorque(Vector3.up * turnForce * 0.3f, ForceMode.Impulse);
        }
    }

    // Effects
    private void PlayPaddleEffects(bool isLeft)
    {
        Vector3 splashPos = transform.position + (isLeft ? -transform.right : transform.right) * 1.5f;
        splashPos.y = transform.position.y - 0.2f;
        
        if (waterSplashEffect != null)
        {
            ParticleSystem splash = Instantiate(waterSplashEffect, splashPos, Quaternion.identity);
            Destroy(splash.gameObject, 2f);
        }

        if (audioSource != null && paddleSound != null && currentSpeed >= minSpeedToMakeSound)
        {
            float intensityFactor = Mathf.Clamp01(currentSpeed / maxSpeed);
            float volume = 0.5f + (intensityFactor * 0.5f);
            float pitch = Random.Range(0.9f, 1.1f);
            
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(paddleSound, volume);
        }
    }

    // Cooldown coroutines
    private IEnumerator LeftPaddleCooldown()
    {
        canPaddleLeft = false;
        yield return new WaitForSeconds(paddleCooldown);
        canPaddleLeft = true;
        isLeftPaddling = false;
    }

    private IEnumerator RightPaddleCooldown()
    {
        canPaddleRight = false;
        yield return new WaitForSeconds(paddleCooldown);
        canPaddleRight = true;
        isRightPaddling = false;
    }

    // Collision handling
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            ObstacleBase obstacle = collision.gameObject.GetComponent<ObstacleBase>();
            int damageAmount = (obstacle != null) ? obstacle.GetDamageAmount() : 10;
            
            if (gameManager != null)
            {
                gameManager.TakeDamage(damageAmount);
            }

            float collisionIntensity = Mathf.Clamp01(currentSpeed / maxSpeed);
            Vector3 bounceDir = transform.position - collision.transform.position;
            bounceDir.y = 0;
            boatRb.AddForce(bounceDir.normalized * bounceFactor * collisionIntensity, ForceMode.Impulse);
            
            currentSpeed *= (1.0f - (collisionIntensity * 0.5f));

            if (audioSource != null && collisionSound != null)
            {
                audioSource.pitch = Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(collisionSound, collisionIntensity);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Award"))
        {
            TreasureBox treasureBox = other.GetComponent<TreasureBox>();
            if (treasureBox != null && gameManager != null)
            {
                gameManager.AddScore(treasureBox.pointValue);
            }

            if (audioSource != null && collectSound != null)
            {
                audioSource.PlayOneShot(collectSound);
            }
        }
        else if (other.CompareTag("FinishLine"))
        {
            if (gameManager != null)
            {
                gameManager.LevelComplete();
            }
        }
    }

    // Public accessors
    public void SetInputMode(InputMode mode)
    {
        inputMode = mode;
    }
    
    public float GetCurrentSpeed() => currentSpeed;
    public float GetMaxSpeed() => maxSpeed;
    public bool IsLeftPaddling() => isLeftPaddling;
    public bool IsRightPaddling() => isRightPaddling;
    public InputMode GetInputMode() => inputMode;
    public int GetConsecutiveLeftCount() => currentConsecutiveLeft;
    public int GetConsecutiveRightCount() => currentConsecutiveRight;
    
    public List<bool> GetPaddleHistory()
    {
        List<bool> history = new List<bool>();
        foreach (PaddleStroke stroke in paddleHistory)
        {
            history.Add(stroke.isLeftPaddle);
        }
        return history;
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[BoatController] {message}");
        }
    }
}