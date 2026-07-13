using UnityEngine;
using System;

public class PlayerAnimationController : MonoBehaviour
{
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
    }

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

    public AnimState CurrentState { get; private set; } = AnimState.Idle;

    // ループしないアニメ（殴りなど）が終わった時に呼ばれる
    public event Action OnNonLoopAnimFinished;

    private AnimClip2D currentClip;
    private int frameIndex;
    private float timer;
    private bool finished;

    private void Awake()
    {
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
        }
    }

    private AnimClip2D GetClip(AnimState state)
    {
        switch (state)
        {
            case AnimState.Idle: return idleAnim;
            case AnimState.Push: return pushAnim;
            case AnimState.Charge1: return charge1Anim;
            case AnimState.Charge2: return charge2Anim;
            case AnimState.Punch: return punchAnim;
            default: return idleAnim;
        }
    }
}