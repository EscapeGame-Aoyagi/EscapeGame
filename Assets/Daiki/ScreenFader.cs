using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFader : MonoBehaviour
{
    public Image fadeImage;      // 黒全画面Image
    public Color fadeColor = Color.black;

    void Awake()
    {
        if (!fadeImage) fadeImage = GetComponent<Image>();
        if (fadeImage)
        {
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        }
    }

    public IEnumerator FadeOut(float duration)
    {
        if (!fadeImage)
        {
            yield return null;
            yield break;
        }

        float t = 0f;
        Color c = fadeColor;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / duration);
            fadeImage.color = new Color(c.r, c.g, c.b, a);
            yield return null;
        }
        fadeImage.color = new Color(c.r, c.g, c.b, 1f);
    }
}
