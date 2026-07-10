// TitleManager.cs
using UnityEngine;

public class TitleManager : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "StageSelectScene";

    private bool isEntered = false;

    private void Update()
    {
        if (isEntered) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            isEntered = true; // 二重入力防止
            ScreenTransitionManager.Instance.FadeTransitionToScene(nextSceneName);
        }
    }
}