using UnityEngine;

public class CollectibleObject : MonoBehaviour
{
    public int scoreValue = 1; // 加算するスコア値（必要に応じて変更可能）

    void OnTriggerEnter(Collider other)
    {
        // プレイヤーに当たったら
        if (other.CompareTag("Player"))
        {
            // スコア加算
            FindObjectOfType<ScoreManager>().AddScore(scoreValue);

            // このオブジェクトを削除
            Destroy(gameObject);
        }
    }
}
