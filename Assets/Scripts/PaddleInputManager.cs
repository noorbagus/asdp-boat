using UnityEngine;
using UnityEngine.UI;

public class PaddleInputManager : MonoBehaviour
{
    [Header("Game References")]
    [SerializeField] private PaddleController paddleController;
    [SerializeField] private BoatController boatController;
    [SerializeField] private PaddleIKController paddleIKController;
    [SerializeField] private Transform paddleTransform;
    [SerializeField] private Transform boat;
    
    [Header("UI References")]
    [SerializeField] private Text statusText;
    [SerializeField] private Text angleText;
    [SerializeField] private Text directionText;
    [SerializeField] private Slider angleSlider;
    [SerializeField] private Button calibrateButton;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject leftTiltIndicator;
    [SerializeField] private GameObject rightTiltIndicator;
    [SerializeField] private Color neutralColor = Color.white;
    [SerializeField] private Color leftColor = Color.red;
    [SerializeField] private Color rightColor = Color.green;
    
    [Header("Game Settings")]
    [SerializeField] private float boatRotationSpeed = 50f;
    [SerializeField] private float boatMoveSpeed = 5f;
    [SerializeField] private float paddleVisualizationScale = 2f;
    [SerializeField] private bool enableBoatMovement = true;
    [SerializeField] private bool enablePaddleVisualization = true;
    
    // Game state
    private float currentBoatRotation = 0f;
    private Vector3 boatVelocity = Vector3.zero;
    private Renderer paddleRenderer;
    
    void Start()
    {
        // Find PaddleController if not assigned
        if (paddleController == null)
            paddleController = FindObjectOfType<PaddleController>();
        
        // Find BoatController if not assigned
        if (boatController == null)
            boatController = FindObjectOfType<BoatController>();
        
        // Find PaddleIKController if not assigned
        if (paddleIKController == null)
            paddleIKController = FindObjectOfType<PaddleIKController>();
        
        if (paddleController == null)
        {
            Debug.LogError("[PaddleInputManager] PaddleController not found!");
            return;
        }
        
        if (boatController == null)
        {
            Debug.LogError("[PaddleInputManager] BoatController not found!");
            return;
        }
        
        // Get paddle renderer for color changes
        if (paddleTransform != null)
            paddleRenderer = paddleTransform.GetComponent<Renderer>();
        
        // Subscribe to paddle events
        paddleController.OnZAngleChanged += OnPaddleAngleChanged;
        paddleController.OnPaddleStateChanged += OnPaddleStateChanged;
        paddleController.OnCalibrationStatusChanged += OnCalibrationStatusChanged;
        paddleController.OnTiltDetected += OnTiltDetected;
        
        // Setup UI
        SetupUI();
        
        Debug.Log("[PaddleInputManager] Game initialized!");
    }
    
    void Update()
    {
        UpdateUI();
        
        if (enableBoatMovement && paddleController.IsReady())
        {
            UpdateBoatMovement();
        }
        
        if (enablePaddleVisualization && paddleController.IsReady())
        {
            UpdatePaddleVisualization();
        }
    }
    
    #region UI Management
    
    void SetupUI()
    {
        // Setup calibrate button
        if (calibrateButton != null)
        {
            calibrateButton.onClick.AddListener(() => paddleController.StartCalibration());
        }
        
        // Setup angle slider range
        if (angleSlider != null)
        {
            angleSlider.minValue = -45f;
            angleSlider.maxValue = 45f;
            angleSlider.value = 0f;
            angleSlider.interactable = false; // Read-only display
        }
    }
    
    void UpdateUI()
    {
        // Update status text
        if (statusText != null)
        {
            string status = "";
            
            if (paddleController.IsCalibrating())
            {
                status = $"🎯 Calibrating... {paddleController.GetCalibrationProgress()}%";
            }
            else if (!paddleController.IsReady())
            {
                status = "❌ Not Ready - Need Bluetooth + Calibration";
            }
            else
            {
                status = "✅ Ready";
            }
            
            statusText.text = status;
        }
        
        // Update angle text and slider
        if (paddleController.IsReady())
        {
            float angle = paddleController.GetZAngle();
            
            if (angleText != null)
                angleText.text = $"Angle: {angle:F1}°";
            
            if (angleSlider != null)
                angleSlider.value = angle;
        }
        
        // Update direction text
        if (directionText != null && paddleController.IsReady())
        {
            var state = paddleController.GetPaddleState();
            var intensity = paddleController.GetTiltIntensity();
            
            string directionInfo = $"Direction: {state}";
            if (intensity > 0)
                directionInfo += $" ({intensity:P0})";
            
            directionText.text = directionInfo;
        }
    }
    
    #endregion
    
    #region Paddle Events
    
    void OnPaddleAngleChanged(float angle)
    {
        // This gets called every frame when paddle angle changes
        // You can add real-time responsive feedback here
    }
    
    void OnPaddleStateChanged(PaddleController.PaddleState newState)
    {
        Debug.Log($"[PaddleInputManager] Paddle state changed to: {newState}");
        
        // Update visual indicators
        UpdateTiltIndicators(newState);
        UpdatePaddleColor(newState);
        
        // Game logic based on paddle state
        switch (newState)
        {
            case PaddleController.PaddleState.TiltLeft:
                Debug.Log("[PaddleInputManager] 🚣 Paddling LEFT!");
                break;
                
            case PaddleController.PaddleState.TiltRight:
                Debug.Log("[PaddleInputManager] 🚣 Paddling RIGHT!");
                break;
                
            case PaddleController.PaddleState.Neutral:
                Debug.Log("[PaddleInputManager] 🚣 Paddle NEUTRAL");
                break;
        }
    }
    
    void OnCalibrationStatusChanged(bool isCalibrating)
    {
        Debug.Log($"[PaddleInputManager] Calibration {(isCalibrating ? "started" : "finished")}");
        
        // You can show/hide calibration UI here
        if (calibrateButton != null)
            calibrateButton.interactable = !isCalibrating;
    }
    
    void OnTiltDetected(int direction, float intensity)
    {
        Debug.Log($"[PaddleInputManager] Tilt detected: Direction={direction}, Intensity={intensity:F2}");
        
        // Direct boat control integration
        if (boatController != null && direction != 0)
        {
            // Apply rowing input to boat
            if (direction == -1)
            {
                boatController.PaddleLeft();
            }
            else if (direction == 1)
            {
                boatController.PaddleRight();
            }
            
            // Add forward thrust based on intensity
            boatController.AddForwardThrust(intensity * 0.5f);
        }
        
        // Update paddle visual animation
        if (paddleIKController != null)
        {
            paddleIKController.SetRawAngle(paddleController.GetZAngle());
        }
        
        // Additional game effects
        ApplyRowingForce(direction, intensity);
    }
    
    #endregion
    
    #region Visual Feedback
    
    void UpdateTiltIndicators(PaddleController.PaddleState state)
    {
        if (leftTiltIndicator != null)
            leftTiltIndicator.SetActive(state == PaddleController.PaddleState.TiltLeft);
        
        if (rightTiltIndicator != null)
            rightTiltIndicator.SetActive(state == PaddleController.PaddleState.TiltRight);
    }
    
    void UpdatePaddleColor(PaddleController.PaddleState state)
    {
        if (paddleRenderer == null) return;
        
        Color targetColor = neutralColor;
        
        switch (state)
        {
            case PaddleController.PaddleState.TiltLeft:
                targetColor = leftColor;
                break;
            case PaddleController.PaddleState.TiltRight:
                targetColor = rightColor;
                break;
        }
        
        paddleRenderer.material.color = targetColor;
    }
    
    void UpdatePaddleVisualization()
    {
        if (paddleTransform == null) return;
        
        float angle = paddleController.GetZAngle();
        
        // Rotate paddle to match sensor angle
        paddleTransform.rotation = Quaternion.Euler(0, 0, angle * paddleVisualizationScale);
    }
    
    #endregion
    
    #region Game Mechanics
    
    void UpdateBoatMovement()
    {
        if (boat == null) return;
        
        int direction = paddleController.GetPaddleDirection();
        float intensity = paddleController.GetTiltIntensity();
        
        // Apply rotation based on paddle direction
        if (direction != 0)
        {
            float rotationAmount = direction * boatRotationSpeed * intensity * Time.deltaTime;
            currentBoatRotation += rotationAmount;
            
            boat.rotation = Quaternion.Euler(0, currentBoatRotation, 0);
        }
        
        // Move boat forward
        boatVelocity = boat.forward * boatMoveSpeed * Time.deltaTime;
        boat.position += boatVelocity;
    }
    
    void ApplyRowingForce(int direction, float intensity)
    {
        // Example: Add rowing effects like splash particles, sound, etc.
        Debug.Log($"[PaddleInputManager] 🌊 Rowing force applied: {direction} with intensity {intensity:P0}");
        
        // You can add:
        // - Particle effects
        // - Audio feedback
        // - Screen shake
        // - Boat acceleration
        // - Water ripples
        // etc.
    }
    
    #endregion
    
    #region Public API for Game Integration
    
    /// <summary>
    /// Get the current paddle angle for custom game logic
    /// </summary>
    public float GetPaddleAngle()
    {
        return paddleController.IsReady() ? paddleController.GetZAngle() : 0f;
    }
    
    /// <summary>
    /// Get normalized paddle input (-1 to 1)
    /// </summary>
    public float GetNormalizedPaddleInput()
    {
        if (!paddleController.IsReady()) return 0f;
        
        float angle = paddleController.GetZAngle();
        return Mathf.Clamp(angle / 45f, -1f, 1f); // Normalize to -1..1 range
    }
    
    /// <summary>
    /// Check if paddle is actively being used
    /// </summary>
    public bool IsPaddleActive()
    {
        return paddleController.IsReady() && 
               paddleController.GetPaddleState() != PaddleController.PaddleState.Neutral;
    }
    
    /// <summary>
    /// Get boat heading based on paddle input
    /// </summary>
    public Vector3 GetBoatDirection()
    {
        return boat != null ? boat.forward : Vector3.forward;
    }
    
    #endregion
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (paddleController != null)
        {
            paddleController.OnZAngleChanged -= OnPaddleAngleChanged;
            paddleController.OnPaddleStateChanged -= OnPaddleStateChanged;
            paddleController.OnCalibrationStatusChanged -= OnCalibrationStatusChanged;
            paddleController.OnTiltDetected -= OnTiltDetected;
        }
    }
    
    #region Debug Gizmos
    
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !paddleController.IsReady()) return;
        
        // Draw paddle direction
        if (paddleTransform != null)
        {
            float angle = paddleController.GetZAngle();
            Vector3 direction = Quaternion.Euler(0, 0, angle) * Vector3.right;
            
            Gizmos.color = angle > 0 ? Color.green : Color.red;
            Gizmos.DrawRay(paddleTransform.position, direction * 2f);
            
            // Draw intensity as sphere size
            float intensity = paddleController.GetTiltIntensity();
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(paddleTransform.position + direction * 2f, intensity * 0.5f);
        }
        
        // Draw boat movement direction
        if (boat != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(boat.position, boat.forward * 3f);
            
            // Draw turn indicator
            int paddleDirection = paddleController.GetPaddleDirection();
            if (paddleDirection != 0)
            {
                Gizmos.color = paddleDirection > 0 ? Color.green : Color.red;
                Vector3 turnDirection = Quaternion.Euler(0, paddleDirection * 45f, 0) * boat.forward;
                Gizmos.DrawRay(boat.position, turnDirection * 2f);
            }
        }
    }
    
    #endregion
}