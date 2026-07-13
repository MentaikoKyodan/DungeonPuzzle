using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 画面遷移演出を一元管理するシングルトン
///
/// アイリス演出はSpriteMaskで実装。
/// BlackOverlayのMask InteractionをVisible Outside Maskに設定することで
/// 「円の外側だけ黒が見える」状態を作る。
///
/// [使い方]
/// ゲームオーバー時: ScreenTransitionManager.Instance.TriggerGameOver(playerWorldPos);
/// シーン遷移時:     ScreenTransitionManager.Instance.TransitionToScene("SceneName", centerWorldPos);
/// </summary>
public class ScreenTransitionManager : MonoBehaviour
{
    public static ScreenTransitionManager Instance { get; private set; }

    // =========================================================
    // Inspector設定
    // =========================================================

    [Header("--- アイリス設定 ---")]
    [Tooltip("全画面を覆う黒いSpriteRenderer。Mask Interaction を Visible Outside Mask に設定すること")]
    [SerializeField] private SpriteRenderer blackOverlay;

    [Tooltip("円形スプライトをアタッチしたSpriteMask")]
    [SerializeField] private SpriteMask irisMask;

    [Tooltip("アイリスが閉じるまでの時間(秒)")]
    [SerializeField] private float irisOutDuration = 1.5f;

    [Tooltip("アイリスが開くまでの時間(秒)")]
    [SerializeField] private float irisInDuration = 1.0f;

    [Header("--- サイレン設定 ---")]
    [Tooltip("発見時に再生するサイレン音")]
    [SerializeField] private AudioClip sirenClip;

    [Header("--- 赤フラッシュ設定 ---")]
    [SerializeField] private Canvas transitionCanvas;
    [SerializeField] private Image redFlashImage;

    [Tooltip("赤フラッシュ全体の時間(秒)")]
    [SerializeField] private float flashDuration = 1.0f;

    [Tooltip("ピーク時の透明度")]
    [Range(0f, 1f)]
    [SerializeField] private float flashPeakAlpha = 0.7f;

    [Tooltip("全体時間のうち何割でピークに達するか")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float flashPeakTiming = 0.3f;

    [Header("--- 暗転設定 ---")]
    [SerializeField] private Image fadeImage;

    [Tooltip("暗転にかかる時間(秒)")]
    [SerializeField] private float fadeDuration = 1.0f;
    // =========================================================
    // 内部状態
    // =========================================================
    public bool IsTransitioning { get; private set; } = false;

    // =========================================================
    // 初期化
    // =========================================================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        blackOverlay.gameObject.SetActive(false);
        irisMask.gameObject.SetActive(false);
        redFlashImage.gameObject.SetActive(false);
    }

    // =========================================================
    // 公開API
    // =========================================================

    public void TriggerGameOver(Vector3 playerWorldPos)
    {
        Debug.Log("TriggerGameOver呼ばれたよ");
        if (IsTransitioning) return;
        StartCoroutine(GameOverSequence(playerWorldPos));
    }

    public void TransitionToScene(string sceneName, Vector3 centerWorldPos)
    {
        if (IsTransitioning) return;
        StartCoroutine(SceneTransitionSequence(sceneName, centerWorldPos));
    }

    // =========================================================
    // シーケンス
    // =========================================================

    private IEnumerator GameOverSequence(Vector3 playerWorldPos)
    {
        IsTransitioning = true;
        SetPlayerInputEnabled(false);

        if (sirenClip != null)
            AudioSource.PlayClipAtPoint(sirenClip, Camera.main.transform.position);

        StartCoroutine(RedFlash());
        yield return StartCoroutine(IrisOut(playerWorldPos));

        string currentScene = SceneManager.GetActiveScene().name;
        yield return SceneManager.LoadSceneAsync(currentScene);
        yield return null; // 1フレーム待ってオブジェクトを確定させる

        Vector3 spawnPos = GetPlayerWorldPos();
        yield return StartCoroutine(IrisIn(spawnPos));

        SetPlayerInputEnabled(true);
        IsTransitioning = false;
    }

    private IEnumerator SceneTransitionSequence(string sceneName, Vector3 centerWorldPos)
    {
        IsTransitioning = true;
        SetPlayerInputEnabled(false);

        yield return StartCoroutine(IrisOut(centerWorldPos));

        yield return SceneManager.LoadSceneAsync(sceneName);
        yield return null;

        // ステージセレクトならアイコン位置、ゲームシーンならカメラ中心を使う
        var stageSelect = FindFirstObjectByType<StageSelectManager>();
        Vector3 irisCenter = stageSelect != null
            ? stageSelect.StageIconWorldPos
            : Camera.main.transform.position;

        yield return StartCoroutine(IrisIn(irisCenter));

        SetPlayerInputEnabled(true);
        IsTransitioning = false;
    }

    // =========================================================
    // 演出コルーチン
    // =========================================================

    private IEnumerator IrisOut(Vector3 worldPos)
    {
        blackOverlay.gameObject.SetActive(true);
        irisMask.gameObject.SetActive(true);

        // カメラ四隅のうちworldPosから最も遠い点までの距離 = 画面全体を確実に覆える半径
        float startRadius = CalcScreenCoverRadius(worldPos);

        float elapsed = 0f;
        while (elapsed < irisOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / irisOutDuration);
            float eased = t * t; // EaseIn(加速しながら閉じる)
            ApplyIris(worldPos, Mathf.Lerp(startRadius, 0f, eased));
            yield return null;
        }

        ApplyIris(worldPos, 0f);
        irisMask.gameObject.SetActive(false);
        // blackOverlayはそのまま(完全に真っ黒の状態をキープ)
    }

    private IEnumerator IrisIn(Vector3 worldPos)
    {
        irisMask.gameObject.SetActive(true);
        float targetRadius = CalcScreenCoverRadius(worldPos);

        float elapsed = 0f;
        while (elapsed < irisInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / irisInDuration);
            float eased = 1f - (1f - t) * (1f - t); // EaseOut(減速しながら開く)
            ApplyIris(worldPos, Mathf.Lerp(0f, targetRadius, eased));
            yield return null;
        }

        blackOverlay.gameObject.SetActive(false);
        irisMask.gameObject.SetActive(false);
    }

    private IEnumerator RedFlash()
    {
        redFlashImage.gameObject.SetActive(true);
        Color c = redFlashImage.color;
        c.a = 0f;
        redFlashImage.color = c;

        float peakTime = flashDuration * flashPeakTiming;
        float fadeOutTime = flashDuration - peakTime;

        float elapsed = 0f;
        while (elapsed < peakTime)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(0f, flashPeakAlpha, elapsed / peakTime);
            redFlashImage.color = c;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < fadeOutTime)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(flashPeakAlpha, 0f, elapsed / fadeOutTime);
            redFlashImage.color = c;
            yield return null;
        }

        c.a = 0f;
        redFlashImage.color = c;
        redFlashImage.gameObject.SetActive(false);
    }

    // =========================================================
    // ユーティリティ
    // =========================================================

    /// <summary>
    /// SpriteMaskの位置とスケールを更新する
    /// 円スプライトが半径0.5(直径1)の場合、scale = 半径 * 2
    /// </summary>
    private void ApplyIris(Vector3 worldPos, float radius)
    {
        irisMask.transform.position = new Vector3(
            worldPos.x, worldPos.y,
            irisMask.transform.position.z
        );
        irisMask.transform.localScale = Vector3.one * (radius * 2f);
    }

    /// <summary>
    /// centerから見てカメラの四隅のうち最も遠い角までの距離を返す
    /// = その位置から画面全体を覆うために必要な最小半径
    /// </summary>
    private float CalcScreenCoverRadius(Vector3 center)
    {
        Camera cam = Camera.main;
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 cp = cam.transform.position;

        Vector2 c2 = new Vector2(center.x, center.y);
        float maxDist = 0f;

        foreach (var corner in new Vector2[]
        {
            new Vector2(cp.x - halfW, cp.y - halfH),
            new Vector2(cp.x + halfW, cp.y - halfH),
            new Vector2(cp.x - halfW, cp.y + halfH),
            new Vector2(cp.x + halfW, cp.y + halfH)
        })
        {
            float d = Vector2.Distance(c2, corner);
            if (d > maxDist) maxDist = d;
        }

        return maxDist;
    }

    private void SetPlayerInputEnabled(bool enabled)
    {
        var player = FindFirstObjectByType<PlayerScript>();
        if (player != null)
            player.enabled = enabled;
    }

    private Vector3 GetPlayerWorldPos()
    {
        var player = FindFirstObjectByType<PlayerScript>();
        if (player == null)
        {
            Debug.LogWarning("PlayerScriptが見つからなかった。アイリスの中心をVector3.zeroにするよ");
            return Vector3.zero;
        }
        return player.transform.position;
    }
    public void FadeTransitionToScene(string sceneName)
    {
        if (IsTransitioning) return;
        StartCoroutine(FadeSequence(sceneName));
    }

    private IEnumerator FadeSequence(string sceneName)
    {
        IsTransitioning = true;

        // 暗転
        yield return StartCoroutine(Fade(0f, 1f, fadeDuration));

        yield return SceneManager.LoadSceneAsync(sceneName);
        yield return null;

        // 明転
        yield return StartCoroutine(Fade(1f, 0f, fadeDuration));

        IsTransitioning = false;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        fadeImage.gameObject.SetActive(true);
        Color c = fadeImage.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, elapsed / duration);
            fadeImage.color = c;
            yield return null;
        }

        c.a = to;
        fadeImage.color = c;

        // 完全に透明になったら非表示
        if (to == 0f)
            fadeImage.gameObject.SetActive(false);
    }
}