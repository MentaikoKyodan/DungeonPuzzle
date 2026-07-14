using UnityEngine;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [SerializeField] private AudioClip bgmClip;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.5f;

    private AudioSource audioSource;

    private void Awake()
    {
        // ★既に他のBGMManagerが存在するなら、自分は消える（重複防止）
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.clip = bgmClip;
        audioSource.loop = true;
        audioSource.volume = volume;
    }

    private void Start()
    {
        // ★まだ再生してなければ再生開始
        if (!audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    // 音量を後から変えたい時用
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        audioSource.volume = volume;
    }
}