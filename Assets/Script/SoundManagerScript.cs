using UnityEngine;

public class SoundManagerScript : MonoBehaviour
{
    public static SoundManagerScript Instance { get; private set; }

    [Header("SE用AudioSource")]
    [SerializeField] private AudioSource seSource;

    [Header("SEクリップ")]
    [SerializeField] private AudioClip moveClip;
    [SerializeField] private AudioClip blockPushClip;
    [SerializeField] private AudioClip goalClip;

    [Header("音量")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    [Header("監視対象")]
    [SerializeField] private PlayerScript player;       // プレイヤーのスクリプト
    [SerializeField] private BlockScript[] blocks;      // シーン上の全ブロック

    // 前フレームの状態を記録するフラグ
    private Vector3 lastPlayerPos;
    private bool wasBlockMovingLastFrame = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (seSource == null)
        {
            seSource = GetComponent<AudioSource>();
            if (seSource == null)
                seSource = gameObject.AddComponent<AudioSource>();
        }
        seSource.playOnAwake = false;
    }

    private void Start()
    {
        // 初期位置を記録
        if (player != null)
            lastPlayerPos = player.transform.position;
    }

    private void Update()
    {
        CheckBlockSound();
        CheckMoveSound();
    }

    // ブロックが動き始めた瞬間にBlockPush SEを鳴らす
    private void CheckBlockSound()
    {
        bool isAnyBlockMovingNow = false;

        foreach (BlockScript block in blocks)
        {
            if (block != null && block.IsMoving)
            {
                isAnyBlockMovingNow = true;
                break;
            }
        }

        // 前フレームは止まっていて、今フレームで動き始めた瞬間だけ鳴らす
        if (isAnyBlockMovingNow && !wasBlockMovingLastFrame)
        {
            PlaySE(blockPushClip);
        }

        wasBlockMovingLastFrame = isAnyBlockMovingNow;
    }

    // プレイヤーが動き始めた瞬間にMove SEを鳴らす（ブロックが動いていなければ）
    private void CheckMoveSound()
    {
        if (player == null) return;

        Vector3 currentPos = player.transform.position;
        bool isPlayerMoving = currentPos != lastPlayerPos;

        // プレイヤーが動いていて、かつブロックが動いていない場合のみMove SE
        if (isPlayerMoving && !wasBlockMovingLastFrame)
        {
            // 移動開始の瞬間だけ鳴らすため targetWorldPos と現在位置を比較
            // PlayerScript の isMoving 相当を targetWorldPos で判定
            if (currentPos != player.targetWorldPos)
            {
                // すでに再生中なら二重再生しない
                if (!seSource.isPlaying)
                {
                    PlaySE(moveClip);
                }
            }
        }

        lastPlayerPos = currentPos;
    }

    // 外部から直接呼ぶ用（Goal SEなど）
    public void Play(string seName)
    {
        switch (seName)
        {
            case "Goal": PlaySE(goalClip); break;
            default:
                Debug.LogWarning($"SoundManager: '{seName}' は登録されていません。");
                break;
        }
    }

    private void PlaySE(AudioClip clip)
    {
        if (clip == null || seSource == null) return;
        seSource.PlayOneShot(clip, volume);
    }
}