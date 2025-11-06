using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class InteractRotateAndFill : MonoBehaviour
{
    [Header("References")]
    public Transform player;          // Player Transform（Tag=Player でも可）
    public Text promptText;           // 「Eを押してハンドルを回す」「スコアが足りません」
    public Slider progressSlider;     // 0～1で使用
    public Image fadePanel;           // 画面全体の黒Image（最前面）
    public Text scoreText;            // スコア表示用（開始時は非表示）

    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.E;
    public float interactRange = 2.0f;
    public float rotationSpeed = 180f;
    public float fillSpeedPerSec = 0.25f;

    [Header("SFX While Rotating")]
    [Tooltip("回している間に一定間隔で鳴らす効果音")]
    public AudioClip rotateTickClip;
    [Tooltip("効果音を鳴らす最小間隔（秒）")]
    public float sfxInterval = 0.4f;
    [Tooltip("効果音の音量（PlayOneShotの第二引数）")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("ピッチのランダム幅（1=等倍）。(min,max)")]
    public Vector2 sfxPitchRange = new Vector2(0.98f, 1.02f);
    [Tooltip("効果音の再生に使う AudioSource（未設定なら自動追加）")]
    public AudioSource sfxSource;

    [Header("Completion Flow")]
    public float fadeDuration = 3.0f;          // 透明→黒 にかける時間（最終的に真っ黒）
    public float delayBeforeScore = 4.0f;      // フェード完了（真っ黒）からスコア表示まで
    public KeyCode continueKey = KeyCode.Return;
    public string scoreFormat = "Score: {0}\n\nPress Enter";

    [Header("Scene")]
    public string nextSceneName = "NextScene";

    [Header("On Complete: Hide Target")]
    [Tooltip("ゲージ100%到達の瞬間に消したい対象（任意）")]
    public GameObject targetToHide;
    [Tooltip("true で Destroy、false で SetActive(false)")]
    public bool destroyTarget = false;

    private float progress = 0f;
    private bool completed = false;
    private ScoreManagerScript scoreManager;

    // 効果音用タイマー
    private float sfxTimer = 0f;

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        if (promptText != null) promptText.gameObject.SetActive(false);

        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = 0f;
            progressSlider.gameObject.SetActive(false);
        }

        if (fadePanel != null)
        {
            var c = fadePanel.color;
            c.a = 0f; // 透明スタート（ここから黒へフェード）
            fadePanel.color = c;
            fadePanel.gameObject.SetActive(false);
        }

        if (scoreText != null) scoreText.gameObject.SetActive(false);

        scoreManager = FindObjectOfType<ScoreManagerScript>();
        if (scoreManager == null)
            Debug.LogWarning("ScoreManager がシーン内に見つかりません。");

        // SFX用AudioSourceが未割り当てなら自動追加
        if (sfxSource == null)
        {
            sfxSource = gameObject.GetComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f; // 2D再生（3Dにしたい場合は1に）
        }
    }

    void Update()
    {
        if (completed || player == null) return;

        float distance = Vector3.Distance(player.position, transform.position);
        bool isInRange = distance <= interactRange;
        bool hasEnoughScore = (scoreManager != null && ScoreManagerScript.score >= 4);

        // 範囲外 → UI完全非表示 & SFXタイマーリセット
        if (!isInRange)
        {
            if (promptText != null) promptText.gameObject.SetActive(false);
            if (progressSlider != null) progressSlider.gameObject.SetActive(false);
            sfxTimer = 0f;
            return;
        }

        // 範囲内 → テキスト
        if (promptText != null)
        {
            promptText.gameObject.SetActive(true);
            promptText.text = hasEnoughScore ? "Eを押してハンドルを回す"
                                             : "スコアが足りません (4以上で操作可能)";
        }

        // ゲージ表示（範囲内・操作可時。進み始めたら表示維持）
        if (progressSlider != null)
            progressSlider.gameObject.SetActive(hasEnoughScore && (progress > 0f));

        if (!hasEnoughScore)
        {
            sfxTimer = 0f; // 条件を満たさないときはSFX間隔をリセット
            return;
        }

        bool isHolding = Input.GetKey(interactKey);
        if (isHolding)
        {
            // 回転
            transform.parent.Rotate(Vector3.back, rotationSpeed * Time.deltaTime, Space.Self);

            // ゲージ
            progress += fillSpeedPerSec * Time.deltaTime;
            progress = Mathf.Clamp01(progress);
            if (progressSlider != null) progressSlider.value = progress;

            // 効果音：一定間隔で鳴らす
            if (rotateTickClip != null && sfxSource != null)
            {
                sfxTimer += Time.deltaTime;
                while (sfxTimer >= sfxInterval) // 低FPSでも等間隔で鳴らす
                {
                    PlayRotateTick();
                    sfxTimer -= sfxInterval;
                }
            }

            // 100%達成
            if (progress >= 1f)
            {
                completed = true;

                // 指定オブジェクトを消す
                if (targetToHide != null)
                {
                    if (destroyTarget) Destroy(targetToHide);
                    else targetToHide.SetActive(false);
                }

                // UIを消す
                if (promptText != null) promptText.gameObject.SetActive(false);
                if (progressSlider != null) progressSlider.gameObject.SetActive(false);

                // 完了フロー（透明→黒→待機→スコア表示→Enterで遷移）
                StartCoroutine(HandleCompletionFlow());
            }
        }
        else
        {
            // 押していない間はSFX間隔リセット（次に押した瞬間から間隔カウント）
            sfxTimer = 0f;
        }
    }

    void PlayRotateTick()
    {
        float pitch = Mathf.Clamp(Random.Range(sfxPitchRange.x, sfxPitchRange.y), 0.5f, 2f);
        float prevPitch = sfxSource.pitch;
        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(rotateTickClip, sfxVolume);
        sfxSource.pitch = prevPitch; // 元に戻す
    }

    IEnumerator HandleCompletionFlow()
    {
        // 透明→黒フェード（最終的に真っ黒）
        if (fadePanel != null)
        {
            fadePanel.gameObject.SetActive(true);

            Color c = fadePanel.color;
            c.a = 0f; // 透明から
            fadePanel.color = c;

            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(0f, 1f, Mathf.Clamp01(t / fadeDuration)); // 透明→黒
                c.a = a;
                fadePanel.color = c;
                yield return null;
            }
            c.a = 1f; // 真っ黒で固定
            fadePanel.color = c;
        }

        // 真っ黒で待機 → スコア表示
        if (delayBeforeScore > 0f)
            yield return new WaitForSeconds(delayBeforeScore);

        if (scoreText != null)
        {
            float shownScore = scoreManager.GetTimeScore();
            scoreText.text = string.Format(scoreFormat, (int)shownScore);
            scoreText.gameObject.SetActive(true);
        }

        // Enterでシーン遷移
        while (!Input.GetKeyDown(continueKey))
            yield return null;

        if (!string.IsNullOrEmpty(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
        else
            Debug.LogWarning("nextSceneName が未設定です。");
    }
}
