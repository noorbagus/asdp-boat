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
    
    // Record new paddle stroke
    private void RecordPaddleStroke(bool isLeftPaddle)
    {
        paddleHistory.Add(new PaddleStroke(isLeftPaddle, Time.time));
        DebugLog($"Paddle stroke recorded: {(isLeftPaddle ? "LEFT" : "RIGHT")} at time {Time.time}");
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
        // Smoothly rotate towards target
        currentRotationY = Mathf.LerpAngle(currentRotationY, targetRotationY, rotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(transform.eulerAngles.x, currentRotationY, transform.eulerAngles.z);
        
        // Check if rotation is complete
        if (Mathf.Abs(Mathf.DeltaAngle(currentRotationY, targetRotationY)) < 1f)
        {
            isRotating = false;
        }
        
        // Auto-straighten after delay
        if (enableAutoStraighten && !isRotating && Time.time - lastTurnTime > autoStraightenDelay)
        {
            float straightAngle = 0f; // Assuming 0 is straight forward
            if (Mathf.Abs(Mathf.DeltaAngle(targetRotationY, straightAngle)) > 5f)
            {
                targetRotationY = Mathf.LerpAngle(targetRotationY, straightAngle, Time.deltaTime * 0.5f);
                DebugLog("Auto-straightening boat rotation");
            }
        }
    }
    
    // Turn left effect (smooth rotation)
    private void TurnLeft()
    {
        DebugLog("Executing smooth turn left");
        targetRotationY -= turnAngle;
        lastTurnTime = Time.time;
        isRotating = true;
        
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
        targetRotationY += turnAngle;
        lastTurnTime = Time.time;
        isRotating = true;
        
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
    
    // For debugging and visualization
    public int GetConsecutiveLeftCount()
    {
        int count = 0;
        for (int i = paddleHistory.Count - 1; i >= 0; i--)
        {
            if (paddleHistory[i].isLeftPaddle)
                count++;
            else
                break;
        }
        return count;
    }
    
    public int GetConsecutiveRightCount()
    {
        int count = 0;
        for (int i = paddleHistory.Count - 1; i >= 0; i--)
        {
            if (!paddleHistory[i].isLeftPaddle)
                count++;
            else
                break;
        }
        return count;
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