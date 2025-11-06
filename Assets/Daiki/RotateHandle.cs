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
    public Image fadePanel;           // 画面全体の黒Image（最前面、Raycast TargetはOFFでOK）
    public Text scoreText;            // スコア表示用（開始時は非表示）

    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.E;
    public float interactRange = 2.0f;
    public float rotationSpeed = 180f;
    public float fillSpeedPerSec = 0.25f;

    [Header("Completion Flow")]
    public float fadeDuration = 3.0f;          // 透明→黒 にかける時間
    public float delayBeforeScore = 4.0f;      // フェード完了（真っ黒）からスコア表示までの遅延
    public KeyCode continueKey = KeyCode.Return;
    public string scoreFormat = "Time: {0}\n\nPress Enter";

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

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        // UI初期化
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
            // 透明スタート（ここから黒へフェードして最終的に真っ黒）
            var c = fadePanel.color;
            c.a = 0f;
            fadePanel.color = c;
            fadePanel.gameObject.SetActive(false);
        }

        if (scoreText != null) scoreText.gameObject.SetActive(false);

        scoreManager = FindObjectOfType<ScoreManagerScript>();
        if (scoreManager == null)
            Debug.LogWarning("ScoreManager がシーン内に見つかりません。");
    }

    void Update()
    {
        if (completed || player == null) return;

        float distance = Vector3.Distance(player.position, transform.position);
        bool isInRange = distance <= interactRange;
        bool hasEnoughScore = (scoreManager != null && ScoreManagerScript.score >= 4);

        // 範囲外 → テキストもスライダーも完全非表示
        if (!isInRange)
        {
            if (promptText != null) promptText.gameObject.SetActive(false);
            if (progressSlider != null) progressSlider.gameObject.SetActive(false);
            return;
        }

        // 範囲内 → テキスト表示（条件別文言）
        if (promptText != null)
        {
            promptText.gameObject.SetActive(true);
            promptText.text = hasEnoughScore ? "Eを押してハンドルを回す"
                                             : "ハンドルがありません";
        }

        // ゲージ表示（範囲内・操作可能時。進み始めたら表示維持）
        if (progressSlider != null)
            progressSlider.gameObject.SetActive(hasEnoughScore && (progress > 0f));

        if (!hasEnoughScore) return;

        // E長押しで回転＆ゲージ増加
        if (Input.GetKey(interactKey))
        {
            transform.parent.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime, Space.Self);

            progress += fillSpeedPerSec * Time.deltaTime / 3;
            progress = Mathf.Clamp01(progress);
            if (progressSlider != null) progressSlider.value = progress;

            // 100%到達
            if (progress >= 1f)
            {
                completed = true;

                // 指定オブジェクトを即時に消す
                if (targetToHide != null)
                {
                    if (destroyTarget) Destroy(targetToHide);
                    else targetToHide.SetActive(false);
                }

                // 不要UIを消す
                if (promptText != null) promptText.gameObject.SetActive(false);
                if (progressSlider != null) progressSlider.gameObject.SetActive(false);

                // 完了フロー開始（透明→黒フェード → 待機 → スコア表示 → Enterで遷移）
                StartCoroutine(HandleCompletionFlow());
            }
        }
    }

    IEnumerator HandleCompletionFlow()
    {
        // 透明→黒フェード（最終的に真っ黒）
        if (fadePanel != null)
        {
            fadePanel.gameObject.SetActive(true);

            Color c = fadePanel.color;
            c.a = 0f;                 // 透明から
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
            // 念のため完全黒で固定
            c.a = 1f;
            fadePanel.color = c;
        }

        // 真っ黒になってから一定時間待ってスコア表示
        if (delayBeforeScore > 0f)
            yield return new WaitForSeconds(delayBeforeScore);

        if (scoreText != null)
        {
            float shownScore = scoreManager.GetTimeScore();
            scoreText.text = string.Format(scoreFormat, (int)shownScore);
            scoreText.gameObject.SetActive(true);
        }

        // Enter待ちでシーン遷移
        while (!Input.GetKeyDown(continueKey))
            yield return null;

        if (!string.IsNullOrEmpty(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
        else
            Debug.LogWarning("nextSceneName が未設定です。");
    }
}
