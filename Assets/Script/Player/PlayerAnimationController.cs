using UnityEngine;
using System;

public class PlayerAnimationController : MonoBehaviour
{


    public static PlayerAnimationController Instance { get; private set; }
    public enum AnimState
    {
        Idle,
        Push,
        Charge1,
        Charge2,
        Punch
    }

    [Serializable]
    public class AnimClip2D
    {
        public Sprite[] frames;
        public float frameInterval = 0.2f;
        public bool loop = true;
        public int impactFrame = -1;
    }
    public event Action OnAnimImpact;
    [SerializeField] private SpriteRenderer spriteRenderer;
    public void SetFacing(bool facingRight)
    {
        spriteRenderer.flipX = !facingRight;
    }
    [SerializeField] private AnimClip2D idleAnim;
    [SerializeField] private AnimClip2D pushAnim;
    [SerializeField] private AnimClip2D charge1Anim;
    [SerializeField] private AnimClip2D charge2Anim;
    [SerializeField] private AnimClip2D punchAnim;



    [Header("背中向き(上移動用) 未設定なら通常アニメで代用")]
    [SerializeField] private AnimClip2D idleBackAnim;
    [SerializeField] private AnimClip2D pushBackAnim;
    [SerializeField] private AnimClip2D charge1BackAnim;
    [SerializeField] private AnimClip2D charge2BackAnim;
    [SerializeField] private AnimClip2D punchBackAnim;

    private bool facingBack;

    public void SetFacingBack(bool back)
    {
        if (facingBack == back) return;
        facingBack = back;
        RefreshClip();
    }

    // 状態はそのままで、クリップだけ差し替える
    private void RefreshClip()
    {
        currentClip = GetClip(CurrentState);
        if (currentClip == null || currentClip.frames.Length == 0) return;

        frameIndex = Mathf.Min(frameIndex, currentClip.frames.Length - 1);
        spriteRenderer.sprite = currentClip.frames[frameIndex];
    }
    public AnimState CurrentState { get; private set; } = AnimState.Idle;

    // ループしないアニメ（殴りなど）が終わった時に呼ばれる
    public event Action OnNonLoopAnimFinished;

    private AnimClip2D currentClip;
    private int frameIndex;
    private float timer;
    private bool finished;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        SetState(AnimState.Idle, force: true);
    }

    public void SetState(AnimState newState, bool force = false)
    {
        if (!force && newState == CurrentState) return;

        CurrentState = newState;
        currentClip = GetClip(newState);
        frameIndex = 0;
        timer = 0f;
        finished = false;

        if (currentClip != null && currentClip.frames.Length > 0)
            spriteRenderer.sprite = currentClip.frames[0];
    }

    private void Update()
    {

        if (currentClip == null || currentClip.frames.Length == 0 || finished) return;

        timer += Time.deltaTime;

        if (timer >= currentClip.frameInterval)
        {
            timer -= currentClip.frameInterval;
            frameIndex++;

            if (frameIndex >= currentClip.frames.Length)
            {
                if (currentClip.loop)
                {
                    frameIndex = 0;
                }
                else
                {
                    frameIndex = currentClip.frames.Length - 1;
                    finished = true;
                    OnNonLoopAnimFinished?.Invoke();
                }
            }

            spriteRenderer.sprite = currentClip.frames[frameIndex];

            if (currentClip.impactFrame >= 0 && frameIndex == currentClip.impactFrame)
            {
                OnAnimImpact?.Invoke();
            }
        }
    }

    private AnimClip2D GetClip(AnimState state)
    {
        AnimClip2D front, back;
        switch (state)
        {
            case AnimState.Push: front = pushAnim; back = pushBackAnim; break;
            case AnimState.Charge1: front = charge1Anim; back = charge1BackAnim; break;
            case AnimState.Charge2: front = charge2Anim; back = charge2BackAnim; break;
            case AnimState.Punch: front = punchAnim; back = punchBackAnim; break;
            default: front = idleAnim; back = idleBackAnim; break;
        }

        if (facingBack && back != null && back.frames != null && back.frames.Length > 0)
            return back;
        return front;
    }
}