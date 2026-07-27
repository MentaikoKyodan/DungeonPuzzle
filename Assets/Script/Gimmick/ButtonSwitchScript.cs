using UnityEngine;

/// <summary>
/// 床に配置するボタンギミック
/// 上にブロックが乗ると押された判定になり、GoalScriptに通知して鍵を開ける
/// 見た目は緑色(監視センサーの赤と混同されないようにするため)
/// </summary>
public class ButtonSwitchScript : MonoBehaviour
{
    [Header("検知設定")]
    [Tooltip("ブロックが乗っているか判定するレイヤー")]
    [SerializeField] private LayerMask blockLayer;
    [Tooltip("判定の半径")]
    [SerializeField] private float detectRadius = 0.4f;

    [Header("見た目")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("何も乗っていない時の色")]
    [SerializeField] private Color idleColor = Color.green;
    [Tooltip("ブロックが乗っている時の色")]
    [SerializeField] private Color pressedColor = new Color(0.2f, 0.6f, 0.2f);

    [Header("SE設定")]
    [SerializeField] private AudioClip pressSE;
    [SerializeField] private AudioSource audioSource;

    [Tooltip("ブロックが離れたら鍵を再度かけるか。falseなら一度押したら解錠されっぱなし")]
    [SerializeField] private bool relockOnRelease = false;

    private bool isPressed = false;
    private bool hasNotifiedGoal = false;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        spriteRenderer.color = idleColor;
    }

    private void Update()
    {
        bool isPressedNow = Physics2D.OverlapCircle(transform.position, detectRadius, blockLayer);

        if (isPressedNow && !isPressed)
        {
            isPressed = true;
            spriteRenderer.color = pressedColor;

            if (pressSE != null && audioSource != null)
                audioSource.PlayOneShot(pressSE);

            if (GoalScript2D.Instance != null)
            {
                GoalScript2D.Instance.NotifyButtonPressed();
                hasNotifiedGoal = true; // 通知したらここも更新
            }
        }
        else if (!isPressedNow && isPressed)
        {
            isPressed = false;
            spriteRenderer.color = idleColor;

            if (relockOnRelease && GoalScript2D.Instance != null)
            {
                GoalScript2D.Instance.NotifyButtonReleased();
                hasNotifiedGoal = false; // ★通知したらここも更新
            }
        }
    }

    public void ForceRefresh()
    {
        bool isPressedNow = Physics2D.OverlapCircle(transform.position, detectRadius, blockLayer);

        // 見た目は常に現状に合わせる
        isPressed = isPressedNow;
        spriteRenderer.color = isPressed ? pressedColor : idleColor;

        // ★比較対象はisPressedじゃなくhasNotifiedGoal
        if (isPressedNow == hasNotifiedGoal) return;
        if (GoalScript2D.Instance == null) return;

        if (isPressedNow)
            GoalScript2D.Instance.NotifyButtonPressed();
        else
            GoalScript2D.Instance.NotifyButtonReleased();

        hasNotifiedGoal = isPressedNow; // 通知した状態を記録
    }
}