using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;   // ← これを足す

public class PlayerCaughtHandler : MonoBehaviour
{
    [Header("Camera / View")]
    public Camera playerCamera;
    public float lookDuration = 0.4f;

    [Header("Fade / UI")]
    public ScreenFader fader;
    public GameObject gameOverPanel;
    public float fadeTime = 3f;
    public float afterFadeWait = 1f;

    [Header("Scene")]
    [Tooltip("ゲームオーバー表示後にEnterで移動するシーン名。空ならロードしない")]
    public string nextSceneName;

    [Header("Player disable settings")]
    public MonoBehaviour[] dontDisable;

    void Awake()
    {
        if (!playerCamera) playerCamera = Camera.main;
    }

    public void StartCaughtSequence(EnemyAI enemy)
    {
        StartCoroutine(CaughtRoutine(enemy));
    }

    private IEnumerator CaughtRoutine(EnemyAI enemy)
    {
        // 1) プレイヤーと敵を同時に向ける
        if (playerCamera && enemy)
        {
            Vector3 toEnemy = enemy.transform.position - playerCamera.transform.position;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude < 0.0001f)
                toEnemy = playerCamera.transform.forward;

            Quaternion camStart = playerCamera.transform.rotation;
            Quaternion camTarget = Quaternion.LookRotation(toEnemy.normalized, Vector3.up);

            // 敵もプレイヤーの方向を向かせる
            Vector3 enemyToPlayer = (transform.position - enemy.transform.position);
            enemyToPlayer.y = 0f;
            if (enemyToPlayer.sqrMagnitude < 0.0001f)
                enemyToPlayer = -enemy.transform.forward;

            Quaternion enemyStart = enemy.transform.rotation;
            Quaternion enemyTarget = Quaternion.LookRotation(enemyToPlayer.normalized, Vector3.up);

            float t = 0f;
            while (t < lookDuration)
            {
                t += Time.deltaTime;
                float k = t / lookDuration;

                playerCamera.transform.rotation = Quaternion.Slerp(camStart, camTarget, k);
                enemy.SetCapturedRotation(Quaternion.Slerp(enemyStart, enemyTarget, k));
                yield return null;
            }

            playerCamera.transform.rotation = camTarget;
            enemy.SetCapturedRotation(enemyTarget);
            enemy.LockCapturedRotation();
        }

        // 2) プレイヤーのコンポーネントを無効化
        DisablePlayerComponents();

        // 3) 演出で敵を歩きにする（実際には動かない）
        if (enemy != null)
        {
            enemy.PlayWalkAfterCapture();
        }

        // 4) フェードアウト
        if (fader != null)
            yield return fader.FadeOut(fadeTime);
        else
            yield return new WaitForSeconds(fadeTime);

        // 5) フェード後に敵を消す
        if (enemy != null)
            Destroy(enemy.gameObject);

        // 6) 少し待ってGameOver表示
        yield return new WaitForSeconds(afterFadeWait);
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        // 7) ここでEnterを待つ
        yield return StartCoroutine(WaitForEnterAndLoad());
    }

    private IEnumerator WaitForEnterAndLoad()
    {
        // GameOver出ている間だけ待つ
        while (true)
        {
            // Enter / Return / KeypadEnter
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (!string.IsNullOrEmpty(nextSceneName))
                {
                    SceneManager.LoadScene(nextSceneName);
                }
                // 名前が空なら何もしないで抜ける
                yield break;
            }
            yield return null;
        }
    }

    private void DisablePlayerComponents()
    {
        var all = GetComponents<MonoBehaviour>();
        foreach (var mb in all)
        {
            if (mb == null) continue;
            if (mb == this) continue;

            bool skip = false;
            if (dontDisable != null)
            {
                foreach (var d in dontDisable)
                {
                    if (d == mb) { skip = true; break; }
                }
            }
            if (skip) continue;

            mb.enabled = false;
        }

        var rb = GetComponent<Rigidbody>();
        if (rb) rb.velocity = Vector3.zero;
    }
}
