using UnityEngine;
using UnityEngine.UI; // UI Textを使う場合

public class ScoreManager : MonoBehaviour
{
    public static int score = 0; // スコアの共有用
    public Text scoreText;       // UI表示用（任意）
    public GameObject Handle;
    private MeshRenderer mr;     // ← フィールド宣言だけにする

    void Start()
    {
        mr = Handle.GetComponent<MeshRenderer>(); // ← Start内で取得
        score = 0;
        UpdateScoreText();
    }

    public void AddScore(int amount)
    {
        score += amount;
        UpdateScoreText();
    }

    void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = "ハンドルの欠片を集める： " + score.ToString() + " / 4";
        }

        if (score >= 4)
        {
            mr.enabled = true;
        }
    }
}
