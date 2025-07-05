using UnityEngine;
using System.Collections;

public class BoatController : MonoBehaviour
{
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
    
    // Current speed
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
    private float turnProgress = 0f;
    private float startRotationY = 0f;

    // Smooth rotation variables
    private float targetRotationVelocity = 0f;
    private float currentRotationVelocity = 0f;
    private float inputDecay = 4f;

    void Start()
    {
        transform.position = new Vector3(0, 0, 0);
        DebugLog("BoatController Start() - Paddle input driven");
        
        // Initialize rotation
        currentRotationY = transform.eulerAngles.y;
        targetRotationY = currentRotationY;
        
        // Get components
        boatRb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        gameManager = FindObjectOfType<GameManager>();
        suimonoModule = FindObjectOfType<Suimono.Core.SuimonoModule>();
        
        DebugLog($"Components found - Rigidbody: {boatRb != null}, AudioSource: {audioSource != null}, GameManager: {gameManager != null}, Suimono: {suimonoModule != null}");
        
        // Add audio source if missing
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f;
            audioSource.minDistance = 1.0f;
            audioSource.maxDistance = 20.0f;
            DebugLog("AudioSource component added");
        }
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
    
    // PUBLIC METHODS - Called by PaddleInputManager
    public void PaddleLeft()
    {
        DebugLog("PaddleLeft() called by PaddleInputManager");
        
        if (!canPaddleLeft) return;

        if (useSmoothedRotation)
        {
            targetRotationVelocity = -maxRotationSpeed;
            lastTurnTime = Time.time;
        }
        else
        {
            TurnRight(); // Left paddle makes boat turn right
        }

        isLeftPaddling = true;
        isRightPaddling = false;
        
        PlayPaddleEffects(true);
        StartCoroutine(LeftPaddleCooldown());
    }

    public void PaddleRight()
    {
        DebugLog("PaddleRight() called by PaddleInputManager");
        
        if (!canPaddleRight) return;

        if (useSmoothedRotation)
        {
            targetRotationVelocity = maxRotationSpeed;
            lastTurnTime = Time.time;
        }
        else
        {
            TurnLeft(); // Right paddle makes boat turn left
        }

        isRightPaddling = true;
        isLeftPaddling = false;
        
        PlayPaddleEffects(false);
        StartCoroutine(RightPaddleCooldown());
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
    public float GetCurrentSpeed() => currentSpeed;
    public float GetMaxSpeed() => maxSpeed;
    public bool IsLeftPaddling() => isLeftPaddling;
    public bool IsRightPaddling() => isRightPaddling;
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[BoatController] {message}");
        }
    }
}