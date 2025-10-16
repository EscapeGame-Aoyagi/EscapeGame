using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// HeartbeatProximity.onBeat(intensity) に接続して、UI画像の赤い周辺光量落ちを脈動
public class HeartbeatEdgeOverlay : MonoBehaviour
{
    [Header("UI")]
    public Image edgeImage;                // 画面全体を覆うImage（赤・ビネットスプライト）
    public CanvasGroup canvasGroup;        // フェード用（任意）

    [Header("Alpha / Color")]
    [Range(0,1)] public float baseAlpha = 0.08f;    // 常時うっすら
    [Range(0,1)] public float maxAddAlpha = 0.35f;  // 近いほど上乗せ
    public Color edgeColor = new Color(1f, 0f, 0f, 1f); // 赤
    [Header("Timing")]
    public float attack = 0.08f;
    public float release = 0.18f;
    public bool useUnscaledTime = false;

    Coroutine anim;

    void Reset()
    {
        edgeImage = GetComponent<Image>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    void Awake()
    {
        if (edgeImage)
        {
            edgeImage.color = new Color(edgeColor.r, edgeColor.g, edgeColor.b, 1f);
        }
        if (canvasGroup) canvasGroup.alpha = baseAlpha;
    }

    /// HeartbeatProximity.onBeat から接続
    public void OnBeat(float intensity)
    {
        if (!edgeImage && !canvasGroup) return;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(PulseOnce(Mathf.Clamp01(intensity)));
    }

    IEnumerator PulseOnce(float k)
    {
        float add = Mathf.Lerp(0f, maxAddAlpha, k);

        // Attack
        float t=0;
        while (t < attack)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t/attack), 3f);
            SetAlpha(baseAlpha + add * e);
            yield return null;
        }
        // Release
        t=0;
        while (t < release)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float e = (t/release); e = e*e;
            SetAlpha(Mathf.Lerp(baseAlpha + add, baseAlpha, e));
            yield return null;
        }
        SetAlpha(baseAlpha);
        anim = null;
    }

    void SetAlpha(float a)
    {
        if (canvasGroup) canvasGroup.alpha = a;
        else if (edgeImage)
        {
            var c = edgeImage.color; c.a = a; edgeImage.color = c;
        }
    }
}
