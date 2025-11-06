using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.Space;

    [Header("Scene")]
    public string nextSceneName = "NextScene";

    void Start()
    {
    }

    void Update()
    {
        //Enterでシーン移動
        if (Input.GetKey(interactKey))
        {
            if (!string.IsNullOrEmpty(nextSceneName))
                SceneManager.LoadScene(nextSceneName);
        }
    }
}
    