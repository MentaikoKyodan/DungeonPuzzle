using UnityEngine;

/// <summary>
/// SE(効果音)を一括管理するシングルトン。
/// シーン内に空のGameObjectを1つ作り、このスクリプトとAudioSourceをアタッチして使う。
///
/// 使い方(他スクリプトから):
///     SoundManager.Instance.PlayMove();
///     SoundManager.Instance.PlayBlockPush();
///     SoundManager.Instance.PlayGoal();
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("SE用AudioSource")]
    [Tooltip("空けておくと自動でこのオブジェクトにAudioSourceを追加します")]
    [SerializeField] private AudioSource seSource;

    [Header("SEクリップ")]
    [Tooltip("プレイヤーが1マス移動したときの音")]
    [SerializeField] private AudioClip moveClip;

    [Tooltip("ブロックを押したときの音")]
    [SerializeField] private AudioClip blockPushClip;

    [Tooltip("ゴールしたときの音")]
    [SerializeField] private AudioClip goalClip;

    [Header("音量")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    private void Awake()
    {
        // シングルトン化(シーンを跨いでも1つだけにしたい場合はDontDestroyOnLoadを使う)
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
            {
                seSource = gameObject.AddComponent<AudioSource>();
            }
        }

        seSource.playOnAwake = false;
    }

    /// <summary>
    /// プレイヤーの移動音を再生
    /// </summary>
    public void PlayMove()
    {
        PlaySE(moveClip);
    }

    /// <summary>
    /// ブロックを押した音を再生
    /// </summary>
    public void PlayBlockPush()
    {
        PlaySE(blockPushClip);
    }

    /// <summary>
    /// ゴール音を再生
    /// </summary>
    public void PlayGoal()
    {
        PlaySE(goalClip);
    }

    /// <summary>
    /// 任意のクリップを再生(他のSEを増やしたいときに直接呼んでもよい)
    /// </summary>
    public void PlaySE(AudioClip clip)
    {
        if (clip == null || seSource == null) return;
        seSource.PlayOneShot(clip, volume);
    }
}