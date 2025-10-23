using UnityEngine;

public class RandomAssetPlacer : MonoBehaviour
{
    [Header("配置するアセット（Prefab）")]
    public GameObject assetPrefab;

    [Header("候補となる地点（Transform）")]
    public Transform[] spawnPoints;

    [Header("設置する数")]
    public int numberToSpawn = 4;

    void Start()
    {
        if (assetPrefab == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("PrefabまたはSpawnPointsが設定されていません。");
            return;
        }

        // 設置数が候補数を超えないように制限
        int spawnCount = Mathf.Min(numberToSpawn, spawnPoints.Length);

        // ランダムに地点を選ぶ
        Transform[] selectedPoints = GetRandomPoints(spawnPoints, spawnCount);

        // 各地点にアセットを設置
        foreach (Transform point in selectedPoints)
        {
            Instantiate(assetPrefab, point.position, point.rotation);
        }
    }

    // 重複しないランダム地点選択関数
    Transform[] GetRandomPoints(Transform[] points, int count)
    {
        Transform[] selected = new Transform[count];
        int[] indices = new int[points.Length];

        // インデックス初期化
        for (int i = 0; i < points.Length; i++)
            indices[i] = i;

        // Fisher–Yates シャッフル
        for (int i = points.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = indices[i];
            indices[i] = indices[j];
            indices[j] = temp;
        }

        // 最初の count 個を選択
        for (int i = 0; i < count; i++)
            selected[i] = points[indices[i]];

        return selected;
    }
}
