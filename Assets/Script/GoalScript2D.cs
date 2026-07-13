using UnityEngine;

/// <summary>
/// 2Dトップビュー用のゴール判定スクリプト。
/// このオブジェクトに Collider2D (Is Trigger = true) を付けて使用してください。
/// </summary>
public class GoalScript2D : MonoBehaviour
{
    public static GoalScript2D Instance { get; private set; }

    [Header("判定対象")]
    [Tooltip("ゴールと判定するオブジェクトのタグ")]
    [SerializeField] private string targetTag = "Player";

    [Header("鍵設定")]
    [Tooltip("ボタンギミックがあるステージではON。最初は鍵がかかった状態になる")]
    [SerializeField] private bool startLocked = false;
    [Tooltip("解錠に必要なボタンの数")]
    [SerializeField] private int requiredButtonCount = 1;

    [Header("鍵の見た目")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("鍵がかかっている時の色")]
    [SerializeField] private Color lockedColor = Color.gray;
    [Tooltip("鍵が開いている時の色")]
    [SerializeField] private Color unlockedColor = Color.white;

    [Header("ゴール後の動作")]
    [Tooltip("チェックすると指定したシーンに遷移します")]
    [SerializeField] private bool loadNextScene = false;
    [Tooltip("読み込むシーン名（Build Settingsに追加しておくこと）")]
    [SerializeField] private string nextSceneName = "";
    [Tooltip("アイリスアウト開始までの待機時間（秒）")]
    [SerializeField] private float loadDelay = 1.0f;

    [Header("ステージ情報")]
    [Tooltip("StageSelectManagerのStages配列の何番目のステージか(0始まり)")]
    [SerializeField] private int stageIndex = 0;

    [Header("演出")]
    [SerializeField] private AudioClip goalSound;
    [SerializeField] private GameObject goalEffectPrefab;
    [SerializeField] private GameObject goalUI;

    private bool isGoaled = false;
    private bool isLocked;
    private int pressedButtonCount = 0;

    public bool IsLocked => isLocked;

    private void Awake()
    {
        Instance = this;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        isLocked = startLocked;
        UpdateVisual();
    }

    // --- ボタンギミックから呼ばれる ---
    public void NotifyButtonPressed()
    {
        pressedButtonCount++;
        CheckUnlock();
    }

    public void NotifyButtonReleased()
    {
        pressedButtonCount = Mathf.Max(0, pressedButtonCount - 1);
        CheckUnlock();
    }

    private void CheckUnlock()
    {
        bool shouldBeLocked = pressedButtonCount < requiredButtonCount;

        if (shouldBeLocked != isLocked)
        {
            isLocked = shouldBeLocked;
            UpdateVisual();
        }
    }

    private void UpdateVisual()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.color = isLocked ? lockedColor : unlockedColor;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isGoaled) return;
        if (isLocked) return; // ★鍵がかかっている間はゴール処理を無視する
        if (!other.CompareTag(targetTag)) return;

        isGoaled = true;
        HandleGoal(other);
    }

    private void HandleGoal(Collider2D player)
    {
        Debug.Log("Goal!");

        // クリア済みとして記録
        if (GameData.Instance != null)
            GameData.Instance.SetCleared(stageIndex);

        if (goalSound != null)
            AudioSource.PlayClipAtPoint(goalSound, transform.position);

        if (goalEffectPrefab != null)
            Instantiate(goalEffectPrefab, transform.position, Quaternion.identity);

        if (goalUI != null)
            goalUI.SetActive(true);

        if (loadNextScene && !string.IsNullOrEmpty(nextSceneName))
            Invoke(nameof(StartTransition), loadDelay);
    }

    private void StartTransition()
    {
        if (ScreenTransitionManager.Instance == null)
        {
            Debug.LogWarning("ScreenTransitionManagerが見つからない");
            return;
        }

        var player = FindFirstObjectByType<PlayerScript>();
        Vector3 center = player != null ? player.transform.position : transform.position;

        ScreenTransitionManager.Instance.TransitionToScene(nextSceneName, center);
    }
}