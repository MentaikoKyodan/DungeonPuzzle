using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// ポーズ画面の管理スクリプト
/// Escapeキー or コントローラーのStartボタンで開閉
/// </summary>
public class PauseManager : MonoBehaviour
{
    [Header("--- UI設定 ---")]
    [SerializeField] private GameObject pausePanel;

    [SerializeField] private Image respawnImage;
    [SerializeField] private Sprite respawnNormalSprite;
    [SerializeField] private Sprite respawnSelectedSprite;

    [SerializeField] private Image stageSelectImage;
    [SerializeField] private Sprite stageSelectNormalSprite;
    [SerializeField] private Sprite stageSelectSelectedSprite;

    [Header("--- シーン設定 ---")]
    [SerializeField] private string stageSelectSceneName = "StageSelect";

    [Header("--- 参照 ---")]
    [SerializeField] private PlayerScript2 playerScript;

    private enum Selection { Respawn, StageSelect }
    private Selection currentSelection = Selection.Respawn;

    public bool IsPaused { get; private set; } = false;
    private bool stickWasNeutral = true;

    private void Start()
    {
        pausePanel.SetActive(false);
    }

    private void Update()
    {
        // シーン遷移演出中はポーズ操作自体を受け付けない
        if (ScreenTransitionManager.Instance != null && ScreenTransitionManager.Instance.IsTransitioning)
            return;

        HandlePauseToggle();

        if (!IsPaused) return;

        HandleSelectionInput();
        HandleConfirmInput();
    }

    private void HandlePauseToggle()
    {
        bool togglePressed = Input.GetKeyDown(KeyCode.Escape);

        if (!togglePressed && Gamepad.current != null)
            togglePressed = Gamepad.current.startButton.wasPressedThisFrame;

        if (togglePressed)
        {
            if (IsPaused) Resume();
            else Pause();
        }
    }

    private void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;

        if (playerScript != null)
            playerScript.enabled = false;

        currentSelection = Selection.Respawn;
        UpdateSelectionVisual();

        pausePanel.SetActive(true);
    }

    private void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        if (playerScript != null)
            playerScript.enabled = true;

        pausePanel.SetActive(false);
    }

    private void HandleSelectionInput()
    {
        bool up = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
        bool down = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();

            bool dpadUp = Gamepad.current.dpad.up.wasPressedThisFrame;
            bool dpadDown = Gamepad.current.dpad.down.wasPressedThisFrame;

            bool stickTriggered = false;
            bool stickUp = false;

            if (Mathf.Abs(stick.y) >= 0.5f)
            {
                if (stickWasNeutral)
                {
                    stickTriggered = true;
                    stickUp = stick.y > 0;
                }
                stickWasNeutral = false;
            }
            else
            {
                stickWasNeutral = true;
            }

            if (!up) up = dpadUp || (stickTriggered && stickUp);
            if (!down) down = dpadDown || (stickTriggered && !stickUp);
        }

        if (up || down)
        {
            currentSelection = currentSelection == Selection.Respawn
                ? Selection.StageSelect
                : Selection.Respawn;
            UpdateSelectionVisual();
        }
    }

    private void HandleConfirmInput()
    {
        bool confirmPressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

        if (!confirmPressed && Gamepad.current != null)
            confirmPressed = Gamepad.current.buttonSouth.wasPressedThisFrame;

        if (confirmPressed)
            Confirm();
    }

    private void UpdateSelectionVisual()
    {
        if (respawnImage != null)
            respawnImage.sprite = currentSelection == Selection.Respawn
                ? respawnSelectedSprite : respawnNormalSprite;

        if (stageSelectImage != null)
            stageSelectImage.sprite = currentSelection == Selection.StageSelect
                ? stageSelectSelectedSprite : stageSelectNormalSprite;
    }

    private void Confirm()
    {
        // 演出コルーチンはリアルタイムで動く前提だから、先にタイムスケールを戻す
        Time.timeScale = 1f;
        IsPaused = false;
        pausePanel.SetActive(false);

        // ScreenTransitionManager側が入力オフ→オンをやってくれるので先に有効化しておく
        if (playerScript != null)
            playerScript.enabled = true;

        Vector3 pos = playerScript != null ? playerScript.transform.position : transform.position;

        if (currentSelection == Selection.Respawn)
        {
            ScreenTransitionManager.Instance.TriggerGameOver(pos);
        }
        else
        {
            ScreenTransitionManager.Instance.TransitionToScene(stageSelectSceneName, pos);
        }
    }
}