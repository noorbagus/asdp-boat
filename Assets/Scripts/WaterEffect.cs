using UnityEngine;

public class WaterEffect : MonoBehaviour
{
    [Header("Water Properties")]
    [SerializeField] private float scrollSpeed = 0.5f;
    [SerializeField] private float waveHeight = 0.1f;
    [SerializeField] private float waveFrequency = 1.0f;
    
    private Renderer waterRenderer;
    private Material waterMaterial;
    private float offset = 0f;
    
    private void Start()
    {
        waterRenderer = GetComponent<Renderer>();
        if (waterRenderer != null)
        {
            waterMaterial = waterRenderer.material;
        }
    }
    
    private void Update()
    {
        if (waterMaterial != null)
        {
            offset += Time.deltaTime * scrollSpeed;
            Vector2 textureOffset = new Vector2(offset, 0);
            waterMaterial.SetTextureOffset("_MainTex", textureOffset);
            
            float height = Mathf.Sin(Time.time * waveFrequency) * waveHeight;
            transform.position = new Vector3(transform.position.x, height, transform.position.z);
        }
    }
}