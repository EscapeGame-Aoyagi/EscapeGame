using UnityEngine;
using UnityEngine.UI; // UI Textを使う場合

public class ScoreManager : MonoBehaviour
{
    public static int score = 0; // スコアの共有用
    public Text scoreText;       // UI表示用（任意）

    void Start()
    {
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
            scoreText.text = "Item: " + score.ToString() + " / 4";
    }
}
