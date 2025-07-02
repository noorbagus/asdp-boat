using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class CollisionDebugger : MonoBehaviour
{
    [Header("Configuration")]
    public bool enableLogging = true;
    public bool logToFile = true;
    public bool logToScreen = true;
    public int maxCollisionsTracked = 100;
    public float warningThreshold = 0.1f; // Log warning if physics step takes longer than 100ms
    
    [Header("Runtime Info")]
    public int collisionsThisFrame = 0;
    public int totalCollisionsTracked = 0;
    public float lastFramePhysicsTime = 0f;
    
    private List<CollisionEvent> recentCollisions = new List<CollisionEvent>();
    private StringBuilder logBuilder = new StringBuilder();
    private string logFilePath;
    private float physicsStartTime;
    
    private class CollisionEvent
    {
        public string objectA;
        public string objectB;
        public Vector3 position;
        public float time;
        public float physicsDuration;
        
        public override string ToString()
        {
            return $"[{time:F2}s] Collision: {objectA} ↔ {objectB} at {position}, Physics took: {physicsDuration*1000:F2}ms";
        }
    }
    
    void Awake()
    {
        if (logToFile)
        {
            logFilePath = Path.Combine(Application.persistentDataPath, "collision_log.txt");
            File.WriteAllText(logFilePath, $"=== COLLISION LOG START: {System.DateTime.Now} ===\n");
            Debug.Log($"Collision log file: {logFilePath}");
        }
    }
    
    void OnEnable()
    {
        Physics.autoSimulation = false; // Take control of physics simulation
    }
    
    void OnDisable()
    {
        Physics.autoSimulation = true; // Return control to Unity
    }
    
    void FixedUpdate()
    {
        collisionsThisFrame = 0;
        physicsStartTime = Time.realtimeSinceStartup;
        
        // Run physics step manually
        Physics.Simulate(Time.fixedDeltaTime);
        
        // Calculate how long physics took
        lastFramePhysicsTime = Time.realtimeSinceStartup - physicsStartTime;
        
        // Warning for slow physics
        if (lastFramePhysicsTime > warningThreshold)
        {
            string warning = $"WARNING: Physics step took {lastFramePhysicsTime*1000:F2}ms - possible hang";
            Debug.LogWarning(warning);
            LogToFile(warning);
        }
    }
    
    void OnGUI()
    {
        if (!logToScreen) return;
        
        GUI.Box(new Rect(10, 10, 300, 120), "Collision Debugger");
        GUI.Label(new Rect(20, 30, 290, 25), $"Collisions this frame: {collisionsThisFrame}");
        GUI.Label(new Rect(20, 50, 290, 25), $"Physics time: {lastFramePhysicsTime*1000:F2}ms");
        GUI.Label(new Rect(20, 70, 290, 25), $"Total collisions: {totalCollisionsTracked}");
        
        if (lastFramePhysicsTime > warningThreshold)
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(20, 90, 290, 25), $"WARNING: Physics slowdown detected!");
            GUI.color = Color.white;
        }
    }
    
    // Add this to any GameObject you want to monitor
    public void RegisterCollision(Collision collision)
    {
        if (!enableLogging) return;
        
        collisionsThisFrame++;
        totalCollisionsTracked++;
        
        CollisionEvent collEvent = new CollisionEvent
        {
            objectA = collision.gameObject.name,
            objectB = collision.collider.gameObject.name,
            position = collision.contacts[0].point,
            time = Time.time,
            physicsDuration = lastFramePhysicsTime
        };
        
        recentCollisions.Add(collEvent);
        if (recentCollisions.Count > maxCollisionsTracked)
        {
            recentCollisions.RemoveAt(0);
        }
        
        string logMessage = collEvent.ToString();
        Debug.Log(logMessage);
        LogToFile(logMessage);
    }
    
    public void RegisterTrigger(Collider trigger, Collider other)
    {
        if (!enableLogging) return;
        
        collisionsThisFrame++;
        totalCollisionsTracked++;
        
        CollisionEvent collEvent = new CollisionEvent
        {
            objectA = trigger.gameObject.name,
            objectB = other.gameObject.name,
            position = other.ClosestPoint(trigger.transform.position),
            time = Time.time,
            physicsDuration = lastFramePhysicsTime
        };
        
        recentCollisions.Add(collEvent);
        if (recentCollisions.Count > maxCollisionsTracked)
        {
            recentCollisions.RemoveAt(0);
        }
        
        string logMessage = $"[{collEvent.time:F2}s] Trigger: {collEvent.objectA} → {collEvent.objectB} at {collEvent.position}, Physics took: {collEvent.physicsDuration*1000:F2}ms";
        Debug.Log(logMessage);
        LogToFile(logMessage);
    }
    
    private void LogToFile(string message)
    {
        if (!logToFile) return;
        
        logBuilder.AppendLine(message);
        
        // Periodically write to file to avoid losing data if crash occurs
        if (logBuilder.Length > 4096)
        {
            File.AppendAllText(logFilePath, logBuilder.ToString());
            logBuilder.Clear();
        }
    }
    
    void OnApplicationQuit()
    {
        if (logToFile && logBuilder.Length > 0)
        {
            File.AppendAllText(logFilePath, logBuilder.ToString());
        }
    }
}