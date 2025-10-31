using UnityEngine;
using System.Collections;

public class CellAnimator : MonoBehaviour
{
    private Material material;
    private Coroutine animationCoroutine;
    
    void Start()
    {
        material = GetComponent<MeshRenderer>().material;
    }
    
    public void AnimateStateChange(Color targetColor)
    {
        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);
        
        animationCoroutine = StartCoroutine(AnimateColorChange(targetColor));
    }
    
    private IEnumerator AnimateColorChange(Color targetColor)
    {
        Color startColor = material.color;
        float duration = 0.3f;
        float elapsed = 0f;
        
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = targetColor != Color.black ? 
            Vector3.one * 1.2f : Vector3.one * 0.8f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            material.color = Color.Lerp(startColor, targetColor, t);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            
            yield return null;
        }
        
        elapsed = 0f;
        startScale = transform.localScale;
        Vector3 finalScale = Vector3.one * 0.9f;
        
        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.5f);
            transform.localScale = Vector3.Lerp(startScale, finalScale, t);
            yield return null;
        }
        
        transform.localScale = finalScale;
    }
}
