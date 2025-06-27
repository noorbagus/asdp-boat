using UnityEngine;
using System;

public class InputManager : MonoBehaviour
{
    [Header("Input Configuration")]
    [SerializeField] private KeyCode leftPaddleKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode rightPaddleKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
    
    // Events for input actions
    public event Action OnLeftPaddle;
    public event Action OnRightPaddle;
    public event Action OnPause;
    
    // Input state tracking
    private bool leftPaddlePressed = false;
    private bool rightPaddlePressed = false;
    
    // Singleton pattern
    public static InputManager Instance { get; private set; }
    
    private void Awake()
    {
        // Singleton implementation
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
    }
    
    private void Update()
    {
        // Check for paddle inputs
        CheckPaddleInput();
        
        // Check for pause input
        if (Input.GetKeyDown(pauseKey))
        {
            OnPause?.Invoke();
        }
    }
    
    private void CheckPaddleInput()
    {
        // Left paddle
        if (Input.GetKeyDown(leftPaddleKey) && !leftPaddlePressed)
        {
            leftPaddlePressed = true;
            OnLeftPaddle?.Invoke();
        }
        else if (Input.GetKeyUp(leftPaddleKey))
        {
            leftPaddlePressed = false;
        }
        
        // Right paddle
        if (Input.GetKeyDown(rightPaddleKey) && !rightPaddlePressed)
        {
            rightPaddlePressed = true;
            OnRightPaddle?.Invoke();
        }
        else if (Input.GetKeyUp(rightPaddleKey))
        {
            rightPaddlePressed = false;
        }
    }
    
    // Public methods to check input state
    public bool IsLeftPaddlePressed()
    {
        return leftPaddlePressed;
    }
    
    public bool IsRightPaddlePressed()
    {
        return rightPaddlePressed;
    }
    
    // Methods to set key bindings
    public void SetLeftPaddleKey(KeyCode key)
    {
        leftPaddleKey = key;
    }
    
    public void SetRightPaddleKey(KeyCode key)
    {
        rightPaddleKey = key;
    }
    
    public void SetPauseKey(KeyCode key)
    {
        pauseKey = key;
    }
    
    // Get current key bindings
    public KeyCode GetLeftPaddleKey()
    {
        return leftPaddleKey;
    }
    
    public KeyCode GetRightPaddleKey()
    {
        return rightPaddleKey;
    }
    
    public KeyCode GetPauseKey()
    {
        return pauseKey;
    }
}