// TitleManager.cs
using UnityEngine;
using UnityEngine.InputSystem;

public class TitleManager : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "StageSelectScene";

    private bool isEntered = false;

    private void Update()
    {
        if (isEntered) return;

        bool confirmPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

        //コントローラーのAボタン/×ボタンでも決定できるようにする
        if (!confirmPressed && Gamepad.current != null)
        {
            confirmPressed = Gamepad.current.buttonSouth.wasPressedThisFrame;
        }

        if (confirmPressed)
        {
            isEntered = true; // 二重入力防止
            ScreenTransitionManager.Instance.FadeTransitionToScene(nextSceneName);
        }
    }
}