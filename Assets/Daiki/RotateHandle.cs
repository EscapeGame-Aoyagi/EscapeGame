using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class InteractRotateAndFill : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Text promptText;
    public Slider progressSlider;

    [Header("Interaction Settings")]
    public KeyCode interactKey = KeyCode.E;
    public float interactRange = 2.0f;
    public float rotationSpeed = 180f;
    public float fillSpeedPerSec = 0.25f;

    [Header("Result")]
    public string nextSceneName = "NextScene";
    public bool keepRotatingAfterComplete = false;

    private float progress = 0f;
    private bool completed = false;

    private ScoreManager scoreManager;

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

        scoreManager = FindObjectOfType<ScoreManager>();
        if (scoreManager == null)
        {
            Debug.LogWarning("ScoreManager がシーン内に見つかりません。");
        }
    }

    void Update()
    {
        if (player == null) return;

        float distance = Vector3.Distance(player.position, transform.position);
        bool isInRange = distance <= interactRange;

        bool hasEnoughScore = (scoreManager != null && ScoreManager.score >= 4);

        // 範囲外ならUI全て非表示
        if (!isInRange)
        {
            if (promptText != null) promptText.gameObject.SetActive(false);
            if (progressSlider != null) progressSlider.gameObject.SetActive(false);
            return;
        }

        // 範囲内ならテキストを表示
        if (promptText != null && !completed)
        {
            promptText.gameObject.SetActive(true);
            promptText.text = hasEnoughScore
                ? "Eを押してハンドルを回す"
                : "ハンドルの欠片を探す";
        }

        // ゲージの表示（範囲内のみ）
        if (progressSlider != null && !completed)
            progressSlider.gameObject.SetActive(hasEnoughScore && (progress > 0f));

        // スコアが足りないときは操作できない
        if (!hasEnoughScore || completed) return;

        // Eキーを押している間の処理
        if (Input.GetKey(interactKey))
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);

            progress += fillSpeedPerSec * Time.deltaTime;
            progress = Mathf.Clamp01(progress);
            if (progressSlider != null) progressSlider.value = progress;

            // 100%達成でシーン遷移
            if (progress >= 1f)
            {
                completed = true;
                if (promptText != null) promptText.gameObject.SetActive(false);
                if (progressSlider != null) progressSlider.gameObject.SetActive(false);

                if (!string.IsNullOrEmpty(nextSceneName))
                    SceneManager.LoadScene(nextSceneName);
                else
                    Debug.LogWarning("nextSceneName が未設定です。");
            }
        }
    }
}
