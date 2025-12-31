using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance;
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 1.0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (fadeCanvasGroup)
        {
            fadeCanvasGroup.alpha = 0f; // Start clear
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    [Header("Visual Effects")]
    public GameObject blurObject; // Drag your Blur Panel/Volume here

    public void FadeOut(float duration = -1)
    {
        if (blurObject != null) blurObject.SetActive(true); // Enable blur when starting transition
        StartCoroutine(FadeRoutine(1f, duration > 0 ? duration : fadeDuration));
    }

    public void FadeIn(float duration = -1)
    {
        StartCoroutine(FadeRoutine(0f, duration > 0 ? duration : fadeDuration));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (fadeCanvasGroup == null) yield break;

        fadeCanvasGroup.blocksRaycasts = (targetAlpha > 0); // Block input while black

        float startAlpha = fadeCanvasGroup.alpha;
        float time = 0;

        while (time < duration)
        {
            time += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            yield return null;
        }

        fadeCanvasGroup.alpha = targetAlpha;
        fadeCanvasGroup.blocksRaycasts = (targetAlpha > 0);

        // Disable blur ONLY if we have fully faded in (alpha is 0)
        if (targetAlpha == 0 && blurObject != null)
        {
            blurObject.SetActive(false);
        }
    }
}
