using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TitleFadeController : MonoBehaviour
{
    [Header("Fade Settings")]
    public Image titleImage;
    public float fadeInDuration = 2f;
    public float displayDuration = 3f;
    public float fadeOutDuration = 2f;
    
    [Header("Scale Animation")]
    public bool useScaleAnimation = false;
    public Vector3 startScale = Vector3.zero;
    public Vector3 endScale = Vector3.one;
    
    [Header("Optional Settings")]
    public bool autoStart = true;
    public bool deactivateAfterFade = true;
    
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
        
        canvasGroup.alpha = 0f;
        
        if (useScaleAnimation)
        {
            rectTransform.localScale = startScale;
        }
        
        if (autoStart)
        {
            StartFadeSequence();
        }
    }
    
    public void StartFadeSequence()
    {
        StartCoroutine(FadeSequence());
    }
    
    private IEnumerator FadeSequence()
    {
        yield return StartCoroutine(FadeIn());
        yield return new WaitForSeconds(displayDuration);
        yield return StartCoroutine(FadeOut());
        
        if (deactivateAfterFade)
        {
            gameObject.SetActive(false);
        }
    }
    
    private IEnumerator FadeIn()
    {
        float elapsedTime = 0f;
        Vector3 initialScale = useScaleAnimation ? startScale : rectTransform.localScale;
        
        while (elapsedTime < fadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / fadeInDuration);
            
            canvasGroup.alpha = progress;
            
            if (useScaleAnimation)
            {
                rectTransform.localScale = Vector3.Lerp(initialScale, endScale, progress);
            }
            
            yield return null;
        }
        
        canvasGroup.alpha = 1f;
        if (useScaleAnimation)
        {
            rectTransform.localScale = endScale;
        }
    }
    
    private IEnumerator FadeOut()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (elapsedTime / fadeOutDuration));
            canvasGroup.alpha = alpha;
            yield return null;
        }
        
        canvasGroup.alpha = 0f;
    }
    
    public void ManualFadeIn()
    {
        StartCoroutine(FadeIn());
    }
    
    public void ManualFadeOut()
    {
        StartCoroutine(FadeOut());
    }
    
    public void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }
}