using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// いちばん近い敵との距離で心音のテンポ(BPM)と音量を変える（PlayOneShot）。  
/// 毎拍 onBeat(intensity 0..1) を発火し、視覚エフェクトが購読できる。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class HeartbeatProximity : MonoBehaviour
{
    [Header("Targets")]
    public List<Transform> enemies = new List<Transform>();
    public string enemyTag = "Enemy";
    public float rescanInterval = 1.0f;

    [Header("Audio")]
    public AudioClip heartbeatClip; // 一拍
    [Range(0f,1f)] public float maxVolume = 0.8f;
    [Range(0f,1f)] public float spatialBlend = 0f; // 0=2D,1=3D

    [Header("Distance Mapping")]
    public float minDistance = 2f;   // これ以下で最大
    public float maxDistance = 25f;  // これ以上で無音
    public float maxBPM = 150f;
    public float minBPM = 60f;
    public AnimationCurve volumeCurve = AnimationCurve.EaseInOut(0,0, 1,1);
    public AnimationCurve bpmCurve    = AnimationCurve.Linear(0,0, 1,1);

    [Header("Hysteresis / Stability")]
    public float startThreshold = 22f;
    public float stopThreshold  = 24f;

    [Header("Events")]
    [Tooltip("毎拍コールバック（引数: intensity=0..1 遠0〜近1）")]
    public UnityEvent<float> onBeat;

    private AudioSource src;
    private Coroutine loop;
    private float rescanTimer;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = spatialBlend;
        src.volume = maxVolume;
        src.mute = false;

        if (!FindObjectOfType<AudioListener>())
            Debug.LogWarning("[Heartbeat] No AudioListener in scene.");
    }

    void OnValidate()
    {
        maxBPM = Mathf.Max(1f, maxBPM);
        minBPM = Mathf.Clamp(minBPM, 1f, maxBPM);
        if (stopThreshold < startThreshold) stopThreshold = startThreshold + 0.01f;
        if (maxDistance  < stopThreshold)  maxDistance  = stopThreshold;
    }

    void Update()
    {
        if (!heartbeatClip) return;

        // タグ自動スキャン
        if (enemies.Count == 0 && !string.IsNullOrEmpty(enemyTag) && rescanInterval > 0f)
        {
            rescanTimer -= Time.deltaTime;
            if (rescanTimer <= 0f)
            {
                rescanTimer = rescanInterval;
                var found = GameObject.FindGameObjectsWithTag(enemyTag);
                enemies.Clear();
                foreach (var go in found) enemies.Add(go.transform);
            }
        }

        float dist = GetNearestDistance();
        bool shouldRun  = dist <= startThreshold;
        bool shouldStop = dist >= stopThreshold || float.IsInfinity(dist);

        if (loop == null && shouldRun) loop = StartCoroutine(BeatLoop());
        else if (loop != null && shouldStop) { StopCoroutine(loop); loop = null; }
    }

    IEnumerator BeatLoop()
    {
        yield return new WaitForSeconds(0.05f);

        while (true)
        {
            float dist = GetNearestDistance();
            if (float.IsInfinity(dist)) { loop = null; yield break; }

            // 近さ 0..1（遠0〜近1）
            float t = Mathf.InverseLerp(maxDistance, minDistance, dist);
            t = Mathf.Clamp01(t);

            // 音量とBPM
            float vol  = maxVolume * Mathf.Clamp01(volumeCurve.Evaluate(t));
            float bpmT = Mathf.Clamp01(bpmCurve.Evaluate(t));
            float bpm  = Mathf.Lerp(minBPM, maxBPM, bpmT);
            bpm = Mathf.Max(1f, bpm);
            float interval = 60f / bpm;

            // 再生
            src.PlayOneShot(heartbeatClip, vol);

            // ★視覚用イベント（強さ = t）
            onBeat?.Invoke(t);

            // 次拍まで待機
            yield return new WaitForSeconds(interval);

            // 停止側ヒステリシス
            if (dist >= stopThreshold) { loop = null; yield break; }
        }
    }

    float GetNearestDistance()
    {
        if (enemies == null || enemies.Count == 0)
            return float.PositiveInfinity;

        Vector3 p = transform.position;
        float bestSqr = float.PositiveInfinity;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Transform t = enemies[i];
            if (!t) { enemies.RemoveAt(i); continue; }
            float sqr = (t.position - p).sqrMagnitude;
            if (sqr < bestSqr) bestSqr = sqr;
        }

        if (float.IsPositiveInfinity(bestSqr)) return float.PositiveInfinity;
        return Mathf.Sqrt(bestSqr);
    }
}
