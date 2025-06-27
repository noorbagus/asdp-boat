using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BoatController : MonoBehaviour
{
    [Header("Boat Properties")]
    [SerializeField] private float forwardSpeed = 0.0f;      // Initial speed (now 0)
    [SerializeField] private float maxSpeed = 5.0f;          // Maximum speed
    [SerializeField] private float paddleForce = 0.5f;       // Forward thrust per paddle
    [SerializeField] private float turnForce = 10.0f;        // Turn force
    [SerializeField] private float paddleCooldown = 0.5f;    // Cooldown between paddles
    [SerializeField] private float waterDrag = 0.2f;         // Water resistance
    [SerializeField] private float bounceFactor = 500f;      // Collision bounce force

    [Header("Paddle Pattern Settings")]
    [SerializeField] private float patternTimeWindow = 2.0f;  // Time window for paddle pattern
    [SerializeField] private int consecutivePaddlesForTurn = 2; // Consecutive paddles needed for turn
    
    [Header("Input Settings")]
    [SerializeField] private bool useKeyboardInput = true;   // Toggle keyboard input
    [SerializeField] private KeyCode leftPaddleKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode rightPaddleKey = KeyCode.RightArrow;

    [Header("Effects")]
    [SerializeField] private ParticleSystem waterSplashEffect;
    [SerializeField] private AudioClip paddleSound;
    [SerializeField] private AudioClip collisionSound;
    [SerializeField] private AudioClip collectSound;

    // Components
    private Rigidbody boatRb;
    private AudioSource audioSource;
    private GameManager gameManager;

    // Paddle state
    private bool canPaddleLeft = true;
    private bool canPaddleRight = true;
    
    // Paddle pattern tracking
    private List<PaddleStroke> paddleHistory = new List<PaddleStroke>();
    private float currentSpeed = 0.0f;
    
    // Structure to track paddle strokes
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
        boatRb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        
        // Find game manager
        gameManager = FindObjectOfType<GameManager>();
        
        // Add audio source if missing
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Update()
    {
        // Apply current speed for forward movement
        transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime);
        
        // Reduce speed due to water resistance
        currentSpeed = Mathf.Max(0, currentSpeed - (waterDrag * Time.deltaTime));

        // Apply water drag to rigidbody
        if (boatRb != null)
        {
            boatRb.velocity *= (1 - (waterDrag * Time.deltaTime));
            boatRb.angularVelocity *= (1 - (waterDrag * Time.deltaTime));
        }

        // Handle keyboard input if enabled
        if (useKeyboardInput)
        {
            CheckKeyboardInput();
        }
        
        // Clean up old paddle history
        CleanupPaddleHistory();
    }
    
    // Clear paddle history older than time window
    private void CleanupPaddleHistory()
    {
        float currentTime = Time.time;
        
        while (paddleHistory.Count > 0 && currentTime - paddleHistory[0].timestamp > patternTimeWindow)
        {
            paddleHistory.RemoveAt(0);
        }
    }

    // Check for keyboard input
    private void CheckKeyboardInput()
    {
        if (Input.GetKeyDown(leftPaddleKey) && canPaddleLeft)
        {
            PaddleLeft();
        }
        
        if (Input.GetKeyDown(rightPaddleKey) && canPaddleRight)
        {
            PaddleRight();
        }
    }

    // Called from BluetoothReceiver when "L:1" is received or from keyboard input
    public void PaddleLeft()
    {
        if (!canPaddleLeft) return;

        // Record this paddle stroke
        RecordPaddleStroke(true);
        
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
        if (!canPaddleRight) return;

        // Record this paddle stroke
        RecordPaddleStroke(false);
        
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
    }
    
    // Analyze paddle pattern and apply appropriate effect
    private void AnalyzePaddlePattern()
    {
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
        
        // Apply turn effect if enough consecutive paddles
        if (consecutiveLeft >= consecutivePaddlesForTurn)
        {
            // Consecutive left paddles: turn right
            TurnRight();
        }
        else if (consecutiveRight >= consecutivePaddlesForTurn)
        {
            // Consecutive right paddles: turn left
            TurnLeft();
        }
        
        // If no alternating pattern and not enough consecutive paddles, 
        // still add a small forward thrust
        if (!alternatingPattern && consecutiveLeft < consecutivePaddlesForTurn && 
            consecutiveRight < consecutivePaddlesForTurn)
        {
            AddForwardThrust(0.5f); // Half strength
        }
    }
    
    // Add forward thrust
    private void AddForwardThrust(float multiplier = 1.0f)
    {
        // Add forward speed
        currentSpeed = Mathf.Min(maxSpeed, currentSpeed + (paddleForce * multiplier));
    }
    
    // Turn right effect (from consecutive left paddles)
    private void TurnRight()
    {
        if (boatRb != null)
        {
            boatRb.AddTorque(Vector3.up * turnForce, ForceMode.Impulse);
        }
    }
    
    // Turn left effect (from consecutive right paddles)
    private void TurnLeft()
    {
        if (boatRb != null)
        {
            boatRb.AddTorque(Vector3.up * -turnForce, ForceMode.Impulse);
        }
    }

    private void PlayPaddleEffects(bool isLeft)
    {
        // Position water splash effect at correct side
        Vector3 splashPos = transform.position + (isLeft ? -transform.right : transform.right) * 1.5f;
        splashPos.y = transform.position.y - 0.2f;
        
        // Instantiate splash effect
        if (waterSplashEffect != null)
        {
            ParticleSystem splash = Instantiate(waterSplashEffect, splashPos, Quaternion.identity);
            Destroy(splash.gameObject, 2f);
        }

        // Play sound
        if (audioSource != null && paddleSound != null)
        {
            audioSource.PlayOneShot(paddleSound);
        }
    }

    private IEnumerator LeftPaddleCooldown()
    {
        canPaddleLeft = false;
        yield return new WaitForSeconds(paddleCooldown);
        canPaddleLeft = true;
    }

    private IEnumerator RightPaddleCooldown()
    {
        canPaddleRight = false;
        yield return new WaitForSeconds(paddleCooldown);
        canPaddleRight = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            // Damage player
            if (gameManager != null)
            {
                gameManager.TakeDamage(10);
            }

            // Bounce effect
            Vector3 bounceDir = transform.position - collision.transform.position;
            bounceDir.y = 0; // Keep bounce horizontal
            boatRb.AddForce(bounceDir.normalized * bounceFactor, ForceMode.Impulse);

            // Play sound
            if (audioSource != null && collisionSound != null)
            {
                audioSource.PlayOneShot(collisionSound);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Award"))
        {
            // Get treasure box component
            TreasureBox treasureBox = other.GetComponent<TreasureBox>();
            
            if (treasureBox != null && gameManager != null)
            {
                // Add score
                gameManager.AddScore(treasureBox.pointValue);
            }

            // Play collect effect
            if (audioSource != null && collectSound != null)
            {
                audioSource.PlayOneShot(collectSound);
            }
        }
        else if (other.CompareTag("FinishLine"))
        {
            // Level complete
            if (gameManager != null)
            {
                gameManager.LevelComplete();
            }
        }
    }

    // Public method to toggle keyboard input
    public void SetKeyboardInputEnabled(bool enabled)
    {
        useKeyboardInput = enabled;
    }
    
    // For debugging
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
}