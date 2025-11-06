using UnityEngine;

public class CollectibleObject : MonoBehaviour
{
    public int scoreValue = 1;            // 加算するスコア
    public AudioClip collectSound;        // 効果音
    private AudioSource audioSource;      // 再生用AudioSource

    void Start()
    {
        // AudioSourceがついていなければ自動で追加
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // スコア加算
            FindObjectOfType<ScoreManagerScript>().AddScore(scoreValue);

            // 効果音再生（オブジェクト削除前に再生）
            if (collectSound != null)
            {
                AudioSource.PlayClipAtPoint(collectSound, transform.position);
            }

            // このオブジェクトを削除
            Destroy(gameObject);
        }
    }
}
