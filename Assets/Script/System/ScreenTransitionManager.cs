using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [Header("--- 暗転インターバル設定 ---")]
    [Tooltip("シーン遷移時、真っ黒のまま止まる最低秒数")]
    [SerializeField] private float sceneHoldSeconds = 0.5f;

    [Tooltip("ゲームオーバー(リトライ)時の最低秒数。テンポ重視なら短め")]
    [SerializeField] private float gameOverHoldSeconds = 0.3f;

    [Tooltip("タイトル文字やロード演出をまとめた親。空なら演出なしで待つだけ")]
    [SerializeField] private CanvasGroup intervalContent;

    [Tooltip("演出が出るときのフェード時間(秒)")]
    [SerializeField] private float intervalFadeSeconds = 0.15f;

    [Tooltip("タイトル文字(1文字ずつ表示)")]
    [SerializeField] private TMP_Text intervalTitleText;

    [Tooltip("1文字ごとの表示間隔(秒)")]
    [SerializeField] private float titleCharInterval = 0.08f;

    [Tooltip("ロード風の回転アイコン")]
    [SerializeField] private RectTransform intervalSpinner;

    [Tooltip("回転速度(度/秒)。マイナスで時計回り")]
    [SerializeField] private float spinnerSpeed = -360f;

    [Tooltip("暗転にかかる時間(秒)")]
    [SerializeField] private float fadeDuration = 1.0f;
    // =========================================================
    // 内部状態
    // =========================================================
    public bool IsTransitioning { get; private set; } = false;
    public bool IsIrisIn { get; private set; } = false;               // アイリスインの最中だけtrue
    public bool IsInputLocked => IsTransitioning && !IsIrisIn;        // 操作・検知を止めたい期間

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
        redFlashImage.gameObject.SetActive(false);
        if (intervalContent != null) intervalContent.gameObject.SetActive(false);

        blackOverlay.gameObject.SetActive(false);
        irisMask.gameObject.SetActive(false);
        redFlashImage.gameObject.SetActive(false);
    }

    // =========================================================
    // 公開API
    // =========================================================

    public void TriggerGameOver(Vector3 playerWorldPos)
    {
        // アイリスアウト中やシーン読み込み中は無視する
        if (IsInputLocked) return;

        // アイリスイン中なら、それを止めてゲームオーバー演出に割り込む
        if (IsIrisIn)
        {
            StopAllCoroutines();
            IsIrisIn = false;
        }

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
        var op = SceneManager.LoadSceneAsync(currentScene);
        op.allowSceneActivation = false;
        yield return StartCoroutine(BlackInterval(gameOverHoldSeconds, false, () => op.progress >= 0.9f));
        op.allowSceneActivation = true;
        yield return op;
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

        var op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;
        yield return StartCoroutine(BlackInterval(sceneHoldSeconds, true, () => op.progress >= 0.9f));
        op.allowSceneActivation = true;
        yield return op;
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
        // アイリスインの途中から割り込んだ場合は、今の円の大きさから閉じ始める
        bool wasOpening = irisMask.gameObject.activeSelf;
        float startRadius = wasOpening
            // カメラ四隅のうちworldPosから最も遠い点までの距離 = 画面全体を確実に覆える半径

            ? irisMask.transform.localScale.x * 0.5f
            : CalcScreenCoverRadius(worldPos);

        blackOverlay.gameObject.SetActive(true);
        irisMask.gameObject.SetActive(true);

        // カメラ四隅のうちworldPosから最も遠い点までの距離 = 画面全体を確実に覆える半径

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
        IsIrisIn = true;
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
        IsIrisIn = false;
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
    /// <summary>
    /// アイリスアウト後の真っ黒な間。holdSeconds経過 かつ 文字表示完了 かつ extraWait完了 まで待つ
    /// showContent=falseなら黒のまま待つだけ
    /// </summary>
    private IEnumerator BlackInterval(float holdSeconds, bool showContent, System.Func<bool> extraWait = null)
    {
        bool useContent = showContent && intervalContent != null;
        int total = 0;

        if (useContent)
        {
            intervalContent.alpha = 0f;
            intervalContent.gameObject.SetActive(true);

            if (intervalTitleText != null)
            {
                intervalTitleText.ForceMeshUpdate();
                total = intervalTitleText.textInfo.characterCount;
                intervalTitleText.maxVisibleCharacters = 0;
            }

            yield return StartCoroutine(FadeIntervalContent(0f, 1f));
        }

        float elapsed = 0f;
        float charTimer = 0f;
        int shown = 0;

        while (true)
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            if (useContent)
            {
                if (intervalSpinner != null)
                    intervalSpinner.Rotate(0f, 0f, spinnerSpeed * dt);

                if (shown < total)
                {
                    charTimer += dt;
                    while (charTimer >= titleCharInterval && shown < total)
                    {
                        charTimer -= titleCharInterval;
                        shown++;
                        intervalTitleText.maxVisibleCharacters = shown;
                    }
                }
            }

            bool timeDone = elapsed >= holdSeconds;
            bool textDone = shown >= total;
            bool extraDone = extraWait == null || extraWait();
            if (timeDone && textDone && extraDone) break;

            yield return null;
        }

        if (useContent)
        {
            // アイリスインの穴から文字が見えないように、先に消す
            yield return StartCoroutine(FadeIntervalContent(1f, 0f));
            intervalContent.gameObject.SetActive(false);
        }
    }

    private IEnumerator FadeIntervalContent(float from, float to)
    {
        float t = 0f;
        while (t < intervalFadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            intervalContent.alpha = Mathf.Lerp(from, to, t / intervalFadeSeconds);
            yield return null;
        }
        intervalContent.alpha = to;
    }

    // 任意: ステージ名などを表示したいときに外から呼ぶ
    public void SetIntervalTitle(string text)
    {
        if (intervalTitleText != null) intervalTitleText.text = text;
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
        var player = FindFirstObjectByType<PlayerScript2>();
        if (player != null)
            player.enabled = enabled;
    }

    private Vector3 GetPlayerWorldPos()
    {
        var player = FindFirstObjectByType<PlayerScript2>();
        if (player == null)
        {
            Debug.LogWarning("PlayerScript2が見つからなかった。アイリスの中心をVector3.zeroにするよ");
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