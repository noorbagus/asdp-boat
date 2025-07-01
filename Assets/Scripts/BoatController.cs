using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BoatController : MonoBehaviour
{
    public enum InputMode
    {
        Keyboard,                // Standard keyboard testing mode
        BluetoothSensor,         // Physical paddle controller
        KeyboardWithDirectControl // Keyboard with direct movement control
    }

    [Header("Input Settings")]
    [SerializeField] private InputMode inputMode = InputMode.Keyboard;
    [SerializeField] private KeyCode leftPaddleKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode rightPaddleKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode forwardKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode backwardKey = KeyCode.DownArrow;
    [SerializeField] private float directControlForce = 2.0f;

    [Header("Rotation Settings")]
    [SerializeField] private float turnAngle = 15f;         // Degrees to turn per paddle
    [SerializeField] private float rotationSpeed = 5f;      // Speed of rotation
    [SerializeField] private float autoStraightenDelay = 2f; // Time before auto-straighten
    [SerializeField] private bool enableAutoStraighten = true;
    [SerializeField] private bool enableSmoothCurve = true;  // Enable smooth curve interpolation
    [SerializeField] private AnimationCurve turnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Vector3 rotationPivotOffset = Vector3.zero; // Adjust rotation center

    [Header("Smooth Rotation")]
    [SerializeField] private bool useSmoothedRotation = true;
    [SerializeField] private float rotationSmoothing = 8f;  // Higher = more responsive
    [SerializeField] private float maxRotationSpeed = 120f; // Max degrees/second

    [Header("Boat Properties")]
    [SerializeField] private float forwardSpeed = 0.0f;      // Initial speed (now 0)
    [SerializeField] private float maxSpeed = 5.0f;          // Maximum speed
    [SerializeField] private float paddleForce = 0.5f;       // Forward thrust per paddle
    [SerializeField] private float turnForce = 10.0f;        // Turn force
    [SerializeField] private float paddleCooldown = 0.5f;    // Cooldown between paddles
    [SerializeField] private float waterDrag = 0.2f;         // Water resistance
    [SerializeField] private float bounceFactor = 500f;      // Collision bounce force
    [SerializeField] private float minSpeedToMakeSound = 0.2f; // Min speed for sound
    [SerializeField] private bool reverseDirection = false;  // Reverse movement direction

    [Header("Paddle Pattern Settings")]
    [SerializeField] private float patternTimeWindow = 2.0f;  // Time window for paddle pattern
    [SerializeField] private int consecutivePaddlesForTurn = 2; // Consecutive paddles needed for turn
    [SerializeField] private float consecutiveTimeout = 1.5f; // Timeout for consecutive count reset

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
    
    // Real-time consecutive tracking - FIXED
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
    private float turnProgress = 0f;      // Progress of current turn (0-1)
    private float startRotationY = 0f;    // Starting rotation for smooth curve

    // Private variables untuk smooth rotation
    private float targetRotationVelocity = 0f;
    private float currentRotationVelocity = 0f;
    private float inputDecay = 4f; // How fast input fades
    
    // Paddle stroke structure
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

    private void Start()
    {
        transform.position = new Vector3(0, 0, 0);
        DebugLog("BoatController Start() called");
        
        // Initialize rotation
        currentRotationY = transform.eulerAngles.y;
        targetRotationY = currentRotationY;
        
        // Get components
        boatRb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        gameManager = FindObjectOfType<GameManager>();
        suimonoModule = FindObjectOfType<Suimono.Core.SuimonoModule>();
        
        DebugLog($"Components found - Rigidbody: {boatRb != null}, AudioSource: {audioSource != null}, GameManager: {gameManager != null}, Suimono: {suimonoModule != null}");
        DebugLog($"Input Mode set to: {inputMode}");
        DebugLog($"Left Paddle Key: {leftPaddleKey}, Right Paddle Key: {rightPaddleKey}");
        
        // Add audio source if missing
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // 3D sound
            audioSource.minDistance = 1.0f;
            audioSource.maxDistance = 20.0f;
            DebugLog("AudioSource component added");
        }
    }

    private void Update()
    {
        // Apply current speed for forward movement with direction control
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

        // Handle input based on mode
        switch (inputMode)
        {
            case InputMode.Keyboard:
                CheckKeyboardInput();
                break;

            case InputMode.KeyboardWithDirectControl:
                CheckDirectControlInput();
                break;

            case InputMode.BluetoothSensor:
                // Handled by BluetoothReceiver
                break;
        }
        
        // Clean up old paddle history
        CleanupPaddleHistory();
        
        // Update consecutive counts - FIXED
        UpdateConsecutiveCounts();
    }
    
    private void FixedUpdate()
    {
        if (suimonoModule != null)
        {
            // Get water data at boat position
            Vector3 position = transform.position;
            float[] heightData = suimonoModule.SuimonoGetHeightAll(position);
            
            waterHeight = heightData[1]; // surfaceLevel
            waveHeight = heightData[8];  // wave height
            waterDirection = heightData[6]; // direction
            waterSpeed = heightData[7];  // speed
            
            // Apply water current to boat (if enabled)
            if (boatRb != null && inheritWaterCurrent && waterSpeed > 0.01f)
            {
                // Convert water direction to vector
                Vector2 waterDirVector = suimonoModule.SuimonoConvertAngleToVector(waterDirection);
                Vector3 waterForce = new Vector3(waterDirVector.x, 0, waterDirVector.y) * waterSpeed * waterCurrentInfluence;
                
                // Apply water current force
                boatRb.AddForce(waterForce, ForceMode.Acceleration);
            }
        }
    }
    
    // Clean up paddle history older than time window
    private void CleanupPaddleHistory()
    {
        float currentTime = Time.time;
        
        while (paddleHistory.Count > 0 && currentTime - paddleHistory[0].timestamp > patternTimeWindow)
        {
            paddleHistory.RemoveAt(0);
        }
    }

    // FIXED: Update consecutive counts with timeout
    private void UpdateConsecutiveCounts()
    {
        // Reset consecutive counts if too much time has passed since last paddle
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

    // OnGUI input detection - bypasses Suimono interference
    private void OnGUI()
    {
        Event e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            DebugLog($"OnGUI: Key pressed - {e.keyCode}");
            
            if (e.keyCode == leftPaddleKey && canPaddleLeft)
            {
                DebugLog("OnGUI: Left paddle triggered");
                PaddleLeft();
            }
            
            if (e.keyCode == rightPaddleKey && canPaddleRight)
            {
                DebugLog("OnGUI: Right paddle triggered");
                PaddleRight();
            }
            
            // For direct control mode
            if (inputMode == InputMode.KeyboardWithDirectControl)
            {
                if (e.keyCode == forwardKey)
                {
                    DebugLog("OnGUI: Forward key triggered");
                    AddForwardThrust(directControlForce * 0.1f);
                }
                
                if (e.keyCode == backwardKey)
                {
                    DebugLog("OnGUI: Backward key triggered");
                    currentSpeed = Mathf.Max(0, currentSpeed - (directControlForce * 0.1f));
                }
            }
        }
    }

    // Standard keyboard input - using paddle pattern (DISABLED - use OnGUI instead)
    private void CheckKeyboardInput()
    {
        // DISABLED - OnGUI method used instead to bypass Suimono interference
    }

    // Direct control keyboard input - bypass paddle pattern
    private void CheckDirectControlInput()
    {
        DebugLog("CheckDirectControlInput() called");
        
        // Standard paddle inputs
        if (Input.GetKeyDown(leftPaddleKey) && canPaddleLeft)
        {
            DebugLog("Direct Control: Left paddle triggered");
            PaddleLeft();
        }
        
        if (Input.GetKeyDown(rightPaddleKey) && canPaddleRight)
        {
            DebugLog("Direct Control: Right paddle triggered");
            PaddleRight();
        }
        
        // Direct movement control
        if (Input.GetKey(forwardKey))
        {
            DebugLog("Direct Control: Forward key pressed");
            AddForwardThrust(directControlForce * Time.deltaTime);
        }
        
        if (Input.GetKey(backwardKey))
        {
            DebugLog("Direct Control: Backward key pressed");
            // Slow down boat
            currentSpeed = Mathf.Max(0, currentSpeed - (directControlForce * Time.deltaTime * 2));
        }
        
        // Direct turning
        if (Input.GetKey(leftPaddleKey) && !Input.GetKey(rightPaddleKey))
        {
            DebugLog("Direct Control: Left turn");
            if (boatRb != null)
            {
                boatRb.AddTorque(Vector3.up * -turnForce * 0.2f * Time.deltaTime, ForceMode.Acceleration);
            }
        }
        
        if (Input.GetKey(rightPaddleKey) && !Input.GetKey(leftPaddleKey))
        {
            DebugLog("Direct Control: Right turn");
            if (boatRb != null)
            {
                boatRb.AddTorque(Vector3.up * turnForce * 0.2f * Time.deltaTime, ForceMode.Acceleration);
            }
        }
    }

    // Called from BluetoothReceiver when "L:1" is received or from keyboard input
    public void PaddleLeft()
    {
        DebugLog("PaddleLeft() called!");
        
        if (!canPaddleLeft)
        {
            DebugLog("PaddleLeft() aborted - cooldown active");
            return;
        }

        // Set smooth rotation velocity
        if (useSmoothedRotation)
        {
            targetRotationVelocity = -maxRotationSpeed; // Negative = left turn
            lastTurnTime = Time.time;
        }

        // Record this paddle stroke
        RecordPaddleStroke(true);
        DebugLog("Left paddle stroke recorded");
        
        // Update paddling state
        isLeftPaddling = true;
        isRightPaddling = false;
        
        // Analyze paddle pattern and apply effects
        AnalyzePaddlePattern();

        // Play effects
        PlayPaddleEffects(true);

        // Cooldown to prevent spam
        StartCoroutine(LeftPaddleCooldown());
    }


    // Called from BluetoothReceiver when "R:1" is received or from keyboard input
    public void PaddleRight()
    {
        DebugLog("PaddleRight() called!");
        
        if (!canPaddleRight)
        {
            DebugLog("PaddleRight() aborted - cooldown active");
            return;
        }

        // Set smooth rotation velocity
        if (useSmoothedRotation)
        {
            targetRotationVelocity = maxRotationSpeed; // Positive = right turn
            lastTurnTime = Time.time;
        }

        // Record this paddle stroke
        RecordPaddleStroke(false);
        DebugLog("Right paddle stroke recorded");
        
        // Update paddling state
        isRightPaddling = true;
        isLeftPaddling = false;
        
        // Analyze paddle pattern and apply effects
        AnalyzePaddlePattern();

        // Play effects
        PlayPaddleEffects(false);

        // Cooldown to prevent spam
        StartCoroutine(RightPaddleCooldown());
    }
    
    // FIXED: Record new paddle stroke with real-time consecutive tracking
    private void RecordPaddleStroke(bool isLeftPaddle)
    {
        paddleHistory.Add(new PaddleStroke(isLeftPaddle, Time.time));
        DebugLog($"Paddle stroke recorded: {(isLeftPaddle ? "LEFT" : "RIGHT")} at time {Time.time}");
        
        // Update consecutive counters in real-time
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
        else // Right paddle
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
        
        DebugLog($"Updated consecutive counts - Left: {currentConsecutiveLeft}, Right: {currentConsecutiveRight}");
    }
    
    // Analyze paddle pattern and apply appropriate effect
    private void AnalyzePaddlePattern()
    {
        DebugLog("AnalyzePaddlePattern() called");
        
        // Count consecutive left and right paddles
        int consecutiveLeft = 0;
        int consecutiveRight = 0;
        bool alternatingPattern = false;
        
        // Check for alternating pattern (forward movement)
        if (paddleHistory.Count >= 2)
        {
            bool lastWasLeft = paddleHistory[paddleHistory.Count - 1].isLeftPaddle;
            bool secondLastWasLeft = paddleHistory[paddleHistory.Count - 2].isLeftPaddle;
            
            if (lastWasLeft != secondLastWasLeft)
            {
                // Alternating pattern detected
                alternatingPattern = true;
                DebugLog("ALTERNATING PATTERN detected - adding forward thrust");
                AddForwardThrust();
            }
        }
        
        // Count consecutive paddles
        for (int i = paddleHistory.Count - 1; i >= 0; i--)
        {
            if (paddleHistory[i].isLeftPaddle)
            {
                if (consecutiveRight > 0) break; // Sequence changed
                consecutiveLeft++;
            }
            else // Right paddle
            {
                if (consecutiveLeft > 0) break; // Sequence changed
                consecutiveRight++;
            }
        }
        
        DebugLog($"Consecutive Left: {consecutiveLeft}, Consecutive Right: {consecutiveRight}");
        
        // Apply turn effect if enough consecutive paddles
        if (consecutiveLeft >= consecutivePaddlesForTurn)
        {
            // Consecutive left paddles: turn right
            DebugLog("TURN RIGHT pattern detected");
            TurnRight();
        }
        else if (consecutiveRight >= consecutivePaddlesForTurn)
        {
            // Consecutive right paddles: turn left
            DebugLog("TURN LEFT pattern detected");
            TurnLeft();
        }
        
        // If no alternating pattern and not enough consecutive paddles, 
        // still add a small forward thrust
        if (!alternatingPattern && consecutiveLeft < consecutivePaddlesForTurn && 
            consecutiveRight < consecutivePaddlesForTurn)
        {
            DebugLog("Single paddle - adding half forward thrust");
            AddForwardThrust(0.5f); // Half strength
        }
    }
    
    // Add forward thrust
    private void AddForwardThrust(float multiplier = 1.0f)
    {
        float oldSpeed = currentSpeed;
        // Add forward speed
        currentSpeed = Mathf.Min(maxSpeed, currentSpeed + (paddleForce * multiplier));
        DebugLog($"Forward thrust added: {oldSpeed:F2} -> {currentSpeed:F2} (multiplier: {multiplier:F2})");
    }
    
    // Smooth rotation handling
    private void UpdateRotation()
    {
        if (useSmoothedRotation)
        {
            // Decay input over time
            targetRotationVelocity = Mathf.Lerp(targetRotationVelocity, 0f, inputDecay * Time.deltaTime);
            
            // Smooth the velocity
            currentRotationVelocity = Mathf.Lerp(currentRotationVelocity, targetRotationVelocity, 
                                                rotationSmoothing * Time.deltaTime);
            
            // Apply rotation with pivot support
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
            
            // Update tracking
            currentRotationY = transform.eulerAngles.y;
            
            // Auto-straighten when no input and after delay
            if (enableAutoStraighten && Mathf.Abs(targetRotationVelocity) < 5f && 
                Time.time - lastTurnTime > autoStraightenDelay)
            {
                float currentY = transform.eulerAngles.y;
                if (currentY > 180f) currentY -= 360f; // Convert to -180 to 180 range
                
                float straightenForce = -currentY * 0.8f; // Proportional force towards 0
                targetRotationVelocity += straightenForce * Time.deltaTime;
            }
        }
        else
        {
            // Original rotation code
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
            else
            {
                if (enableAutoStraighten && Time.time - lastTurnTime > autoStraightenDelay)
                {
                    float straightAngle = 0f;
                    if (Mathf.Abs(Mathf.DeltaAngle(targetRotationY, straightAngle)) > 5f)
                    {
                        targetRotationY = Mathf.LerpAngle(targetRotationY, straightAngle, Time.deltaTime * 0.1f);
                        ApplyRotationAroundPivot();
                    }
                }
            }
        }
    }

    
    // Apply rotation around custom pivot point
    private void ApplyRotationAroundPivot()
    {
        Vector3 pivotWorldPos = transform.position + transform.TransformDirection(rotationPivotOffset);
        transform.RotateAround(pivotWorldPos, Vector3.up, currentRotationY - transform.eulerAngles.y);
    }
    
    // Turn left effect (smooth rotation)
    private void TurnLeft()
    {
        DebugLog("Executing smooth turn left");
        
        // Store starting rotation for smooth curve
        startRotationY = currentRotationY;
        targetRotationY -= turnAngle;
        lastTurnTime = Time.time;
        isRotating = true;
        turnProgress = 0f;
        
        // Optional: Keep rigidbody physics for water interaction
        if (boatRb != null)
        {
            boatRb.AddTorque(Vector3.up * -turnForce * 0.3f, ForceMode.Impulse);
        }
    }
    
    // Turn right effect (smooth rotation)
    private void TurnRight()
    {
        DebugLog("Executing smooth turn right");
        
        // Store starting rotation for smooth curve
        startRotationY = currentRotationY;
        targetRotationY += turnAngle;
        lastTurnTime = Time.time;
        isRotating = true;
        turnProgress = 0f;
        
        // Optional: Keep rigidbody physics for water interaction
        if (boatRb != null)
        {
            boatRb.AddTorque(Vector3.up * turnForce * 0.3f, ForceMode.Impulse);
        }
    }

    private void PlayPaddleEffects(bool isLeft)
    {
        DebugLog($"Playing paddle effects for {(isLeft ? "LEFT" : "RIGHT")} paddle");
        
        // Position water splash effect at correct side
        Vector3 splashPos = transform.position + (isLeft ? -transform.right : transform.right) * 1.5f;
        splashPos.y = transform.position.y - 0.2f;
        
        // Instantiate splash effect
        if (waterSplashEffect != null)
        {
            ParticleSystem splash = Instantiate(waterSplashEffect, splashPos, Quaternion.identity);
            Destroy(splash.gameObject, 2f);
            DebugLog("Water splash effect created");
        }
        else
        {
            DebugLog("No water splash effect assigned");
        }

        // Play sound
        if (audioSource != null && paddleSound != null && currentSpeed >= minSpeedToMakeSound)
        {
            // Adjust volume and pitch based on speed and randomness
            float intensityFactor = Mathf.Clamp01(currentSpeed / maxSpeed);
            float volume = 0.5f + (intensityFactor * 0.5f);
            float pitch = Random.Range(0.9f, 1.1f);
            
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(paddleSound, volume);
            DebugLog($"Paddle sound played - volume: {volume:F2}, pitch: {pitch:F2}");
        }
        else
        {
            DebugLog($"Paddle sound not played - AudioSource: {audioSource != null}, PaddleSound: {paddleSound != null}, Speed: {currentSpeed:F2}, MinSpeed: {minSpeedToMakeSound}");
        }
    }

    private IEnumerator LeftPaddleCooldown()
    {
        DebugLog("Left paddle cooldown started");
        canPaddleLeft = false;
        yield return new WaitForSeconds(paddleCooldown);
        canPaddleLeft = true;
        
        // Reset paddle state after cooldown
        isLeftPaddling = false;
        DebugLog("Left paddle cooldown ended");
    }

    private IEnumerator RightPaddleCooldown()
    {
        DebugLog("Right paddle cooldown started");
        canPaddleRight = false;
        yield return new WaitForSeconds(paddleCooldown);
        canPaddleRight = true;
        
        // Reset paddle state after cooldown
        isRightPaddling = false;
        DebugLog("Right paddle cooldown ended");
    }

    private void OnCollisionEnter(Collision collision)
    {
        DebugLog($"Collision detected with: {collision.gameObject.name}");
        
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            DebugLog("Collision with obstacle!");
            
            // Get obstacle component for damage info
            ObstacleBase obstacle = collision.gameObject.GetComponent<ObstacleBase>();
            int damageAmount = (obstacle != null && obstacle.GetType().GetMethod("GetDamageAmount") != null) ? 
                               obstacle.GetDamageAmount() : 10;
            
            // Damage player
            if (gameManager != null)
            {
                gameManager.TakeDamage(damageAmount);
                DebugLog($"Damage dealt: {damageAmount}");
            }

            // Calculate collision intensity based on speed
            float collisionIntensity = Mathf.Clamp01(currentSpeed / maxSpeed);
            
            // Bounce effect
            Vector3 bounceDir = transform.position - collision.transform.position;
            bounceDir.y = 0; // Keep bounce horizontal
            boatRb.AddForce(bounceDir.normalized * bounceFactor * collisionIntensity, ForceMode.Impulse);

            // Slow down boat based on collision intensity
            currentSpeed *= (1.0f - (collisionIntensity * 0.5f));

            // Play sound
            if (audioSource != null && collisionSound != null)
            {
                audioSource.pitch = Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(collisionSound, collisionIntensity);
                DebugLog("Collision sound played");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        DebugLog($"Trigger entered: {other.gameObject.name}");
        
        if (other.CompareTag("Award"))
        {
            DebugLog("Award collected!");
            
            // Get treasure box component
            TreasureBox treasureBox = other.GetComponent<TreasureBox>();
            
            if (treasureBox != null && gameManager != null)
            {
                // Add score
                gameManager.AddScore(treasureBox.pointValue);
                DebugLog($"Score added: {treasureBox.pointValue}");
            }

            // Play collect effect
            if (audioSource != null && collectSound != null)
            {
                audioSource.PlayOneShot(collectSound);
                DebugLog("Collect sound played");
            }
        }
        else if (other.CompareTag("FinishLine"))
        {
            DebugLog("Finish line reached!");
            
            // Level complete
            if (gameManager != null)
            {
                gameManager.LevelComplete();
            }
        }
    }

    // Set input mode
    public void SetInputMode(InputMode mode)
    {
        inputMode = mode;
        DebugLog($"Input mode changed to: {mode}");
    }
    
    // Public accessors for external components
    public float GetCurrentSpeed() { return currentSpeed; }
    public float GetMaxSpeed() { return maxSpeed; }
    public bool IsLeftPaddling() { return isLeftPaddling; }
    public bool IsRightPaddling() { return isRightPaddling; }
    public InputMode GetInputMode() { return inputMode; }
    
    // FIXED: For debugging and visualization - now uses real-time tracking
    public int GetConsecutiveLeftCount()
    {
        // Return current consecutive count instead of calculating from history
        return currentConsecutiveLeft;
    }
    
    public int GetConsecutiveRightCount()
    {
        // Return current consecutive count instead of calculating from history
        return currentConsecutiveRight;
    }
    
    public List<bool> GetPaddleHistory()
    {
        List<bool> history = new List<bool>();
        foreach (PaddleStroke stroke in paddleHistory)
        {
            history.Add(stroke.isLeftPaddle);
        }
        return history;
    }
    
    // Debug logging method
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[BoatController] {message}");
        }
    }
}