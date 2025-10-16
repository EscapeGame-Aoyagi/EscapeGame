using System.Collections;
using UnityEngine;

/// <summary>
/// HeartbeatProximity.onBeat(float intensity 0..1) に接続して、
/// FOVを一瞬だけ広げて戻す“鼓動”演出を付ける。
/// </summary>
public class HeartbeatFOVPulse : MonoBehaviour
{
    [Header("Target")]
    public Camera targetCamera;                   // 未指定なら mainCamera
    public bool autoGrabFromCamera = true;       // 有効ならStart時に現在FOVを基準値として取得

    [Header("Pulse")]
    [Tooltip("近距離(強い鼓動)でFOVに加える最大値（度）")]
    public float maxAddFOV = 2.0f;
    [Tooltip("膨らむ時間（秒）")]
    public float attack = 0.06f;
    [Tooltip("戻る時間（秒）")]
    public float release = 0.14f;
    [Tooltip("TimeScaleの影響を受けない（ポーズ中も鼓動演出したい）")]
    public bool useUnscaledTime = false;

    [Header("Curves")]
    public AnimationCurve attackCurve = AnimationCurve.EaseInOut(0,0, 1,1);
    public AnimationCurve releaseCurve = AnimationCurve.EaseInOut(0,1, 1,0);

    [Header("Clamp / Safety")]
    [Tooltip("FOVの最小最大（0で無効）")]
    public Vector2 clampFOV = new Vector2(40f, 100f);

    float baseFOV;
    Coroutine anim;

    void Awake()
    {
        if (!targetCamera && Camera.main) targetCamera = Camera.main;
        if (autoGrabFromCamera && targetCamera) baseFOV = targetCamera.fieldOfView;
        else if (baseFOV <= 0f) baseFOV = 60f;
    }

    /// <summary>
    /// HeartbeatProximity.onBeat から接続して呼ぶ
    /// intensity: 0..1（遠0〜近1）
    /// </summary>
    public void OnBeat(float intensity)
    {
        if (!targetCamera) return;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(PulseOnce(intensity));
    }

    IEnumerator PulseOnce(float intensity)
    {
        float add = Mathf.Lerp(0f, maxAddFOV, Mathf.Clamp01(intensity));

        // Attack: base → base+add
        float t = 0f;
        while (t < attack)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(attack, 0.0001f));
            float fov = baseFOV + add * attackCurve.Evaluate(k);
            targetCamera.fieldOfView = ApplyClamp(fov);
            yield return null;
        }

        // Release: base+add → base
        t = 0f;
        while (t < release)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(release, 0.0001f));
            float fov = Mathf.Lerp(baseFOV + add, baseFOV, 1f - releaseCurve.Evaluate(k));
            targetCamera.fieldOfView = ApplyClamp(fov);
            yield return null;
        }

        targetCamera.fieldOfView = ApplyClamp(baseFOV);
        anim = null;
    }

    float ApplyClamp(float fov)
    {
        if (clampFOV.x > 0f || clampFOV.y > 0f)
            return Mathf.Clamp(fov, clampFOV.x, clampFOV.y);
        return fov;
    }

    // ベースFOVを動的に更新したい場合に呼ぶ
    public void SetBaseFOV(float fov)
    {
        baseFOV = fov;
        if (targetCamera) targetCamera.fieldOfView = ApplyClamp(baseFOV);
    }
}
