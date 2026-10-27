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

    [Header("正面向き(下移動用) 未設定なら通常アニメで代用")]
    [SerializeField] private AnimClip2D idleFrontAnim;
    [SerializeField] private AnimClip2D pushFrontAnim;
    [SerializeField] private AnimClip2D charge1FrontAnim;
    [SerializeField] private AnimClip2D charge2FrontAnim;
    [SerializeField] private AnimClip2D punchFrontAnim;

    public enum FacingDir { Side, Back, Front }
    private FacingDir facingDir = FacingDir.Front;

    // 既存の呼び出しを壊さないために残す
    public void SetFacingBack(bool back)
    {
        SetDirection(back ? FacingDir.Back : FacingDir.Side);
    }

    public void SetDirection(FacingDir dir)
    {
        if (facingDir == dir) return;
        facingDir = dir;
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
        AnimClip2D side, back, front;
        switch (state)
        {
            case AnimState.Push: side = pushAnim; back = pushBackAnim; front = pushFrontAnim; break;
            case AnimState.Charge1: side = charge1Anim; back = charge1BackAnim; front = charge1FrontAnim; break;
            case AnimState.Charge2: side = charge2Anim; back = charge2BackAnim; front = charge2FrontAnim; break;
            case AnimState.Punch: side = punchAnim; back = punchBackAnim; front = punchFrontAnim; break;
            default: side = idleAnim; back = idleBackAnim; front = idleFrontAnim; break;
        }

        AnimClip2D dirClip = null;
        if (facingDir == FacingDir.Back) dirClip = back;
        else if (facingDir == FacingDir.Front) dirClip = front;

        if (dirClip != null && dirClip.frames != null && dirClip.frames.Length > 0)
            return dirClip;
        return side;
    }
}