using UnityEngine;
using UnityEngine.UI;

public class ScoreManager : MonoBehaviour
{
    // ====== スコア関連 ======
    public static int score = 0;        // ハンドルの欠片スコア（共有用）
    public Text scoreText;              // スコアUI表示用
    public GameObject Handle;           // ハンドルオブジェクト
    private MeshRenderer mr;            // ハンドルの表示制御用

    // ====== タイマー関連 ======
    private float elapsedTime = 0f;     // 経過時間（カウントアップ）
    private bool timerRunning = true;   // タイマーの動作フラグ

    void Start()
    {
        if (Handle != null)
        {
            mr = Handle.GetComponent<MeshRenderer>();
        }

        score = 0;
        elapsedTime = 0f;
        UpdateScoreText();
    }

    void Update()
    {
        // ---- タイマーを進める（カウントアップ） ----
        if (timerRunning)
        {
            elapsedTime += Time.deltaTime;
        }
    }

    // ====== スコア加算 ======
    public void AddScore(int amount)
    {
        score += amount;
        UpdateScoreText();
    }

    // ====== スコアUI更新 ======
    void UpdateScoreText()
    {
        if (scoreText != null)
        {
            if (score >= 4)
            {
                scoreText.text = "出口から脱出する";
            }
            else
            {
                scoreText.text = "ハンドルの欠片を集める： " + score.ToString() + " / 4";
            }
        }

        // ハンドルを表示（スコアが4以上のとき）
        if (mr != null && score >= 4)
        {
            mr.enabled = true;
        }
    }

    // ====== タイマー制御 ======
    public void PauseTimer()
    {
        timerRunning = false;
    }

    public void ResumeTimer()
    {
        timerRunning = true;
    }

    // ====== 結果取得（クリアタイムなど） ======
    public float GetTimeScore()
    {
        // 経過時間をスコアとして返す
        return elapsedTime;
    }
}
