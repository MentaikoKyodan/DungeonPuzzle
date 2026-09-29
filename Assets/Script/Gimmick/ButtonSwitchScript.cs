using UnityEngine;

public class ButtonSwitchScript : MonoBehaviour
{
    [Header("検知設定")]
    [SerializeField] private LayerMask blockLayer;
    [SerializeField] private float detectRadius = 0.4f;

    [Header("見た目")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color idleColor = Color.green;
    [SerializeField] private Color pressedColor = new Color(0.2f, 0.6f, 0.2f);

    [Header("SE設定")]
    [SerializeField] private AudioClip pressSE;
    [SerializeField] private AudioSource audioSource;

    [Tooltip("ブロックが離れたら鍵を再度かけるか。falseなら一度押したら解錠されっぱなし")]
    [SerializeField] private bool relockOnRelease = true;

    private bool isPressed = false;        // 今ブロックが乗っているか(見た目用)
    private bool hasNotifiedGoal = false;  // ゴールに「押された」と通知中か

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
        Refresh(true);
    }

    // Undoなどから呼ぶ用(SEは鳴らさない)
    public void ForceRefresh()
    {
        Refresh(false);
    }

    private void Refresh(bool playSE)
    {
        bool pressedNow = Physics2D.OverlapCircle(transform.position, detectRadius, blockLayer);

        // 見た目・SE
        if (pressedNow != isPressed)
        {
            if (pressedNow && playSE && pressSE != null && audioSource != null)
                audioSource.PlayOneShot(pressSE);

            isPressed = pressedNow;
            spriteRenderer.color = isPressed ? pressedColor : idleColor;
        }

        // ゴールへの通知(通知中かどうかだけを基準にする)
        bool shouldNotify = relockOnRelease ? pressedNow : (pressedNow || hasNotifiedGoal);
        if (shouldNotify == hasNotifiedGoal) return;
        if (GoalScript2D.Instance == null) return;

        if (shouldNotify)
            GoalScript2D.Instance.NotifyButtonPressed();
        else
            GoalScript2D.Instance.NotifyButtonReleased();

        hasNotifiedGoal = shouldNotify;
    }
}