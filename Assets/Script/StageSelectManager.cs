using System.Collections;
using UnityEngine;

/// <summary>
/// ステージセレクト画面の管理スクリプト
///
/// [使い方]
/// Inspector上でStageNode配列にノードを登録する。
/// A/D または ←/→ でノード間をキャラが移動する。
/// Enterで選択中ステージにアイリスアウト遷移する。
/// </summary>
public class StageSelectManager : MonoBehaviour
{
    [System.Serializable]
    public class StageNode
    {
        [Tooltip("ロードするシーン名(Build Settingsに登録必須)")]
        public string sceneName;

        [Tooltip("このノードのTransform(位置情報として使う)")]
        public Transform nodeTransform;
    }

    [Header("--- ステージ設定 ---")]
    [SerializeField] private StageNode[] stages;

    [Header("--- キャラクター設定 ---")]
    [Tooltip("ノード間を移動するキャラクターのTransform")]
    [SerializeField] private Transform stageIcon;

    [Tooltip("ノード間の移動速度")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("ノードからのオフセット。Yを上げるとキャラがノードより上に表示される")]
    [SerializeField] private Vector2 iconOffset = new Vector2(0f, 0.5f);

    [Header("--- パスライン設定 ---")]
    [Tooltip("ノード間を繋ぐLineRenderer")]
    [SerializeField] private LineRenderer pathLine;

    [Tooltip("ラインの太さ")]
    [SerializeField] private float lineWidth = 0.1f;

    [Tooltip("ラインの色")]
    [SerializeField] private Color lineColor = Color.white;

    // =========================================================
    // 内部状態
    // =========================================================
    private int currentIndex = 0;
    private bool isMoving = false;

    // =========================================================
    // 初期化
    // =========================================================
    private void Start()
    {
        if (stages == null || stages.Length == 0)
        {
            Debug.LogWarning("StageSelectManager: ステージが登録されていない");
            return;
        }

        // キャラを最初のノード位置 + オフセットに配置
        stageIcon.position = (Vector2)stages[0].nodeTransform.position + iconOffset;

        DrawPath();
    }

    // =========================================================
    // 入力処理
    // =========================================================
    private void Update()
    {
        if (isMoving) return;

        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            TryMove(+1);
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            TryMove(-1);
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            EnterStage();
    }

    // =========================================================
    // ノード間移動
    // =========================================================
    private void TryMove(int direction)
    {
        int next = currentIndex + direction;
        if (next < 0 || next >= stages.Length) return;

        currentIndex = next;

        // 移動先もオフセットを足した位置にする
        Vector3 target = (Vector2)stages[currentIndex].nodeTransform.position + iconOffset;
        StartCoroutine(MoveToNode(target));
    }

    private IEnumerator MoveToNode(Vector3 targetPos)
    {
        isMoving = true;

        while ((stageIcon.position - targetPos).sqrMagnitude > 0.0001f)
        {
            stageIcon.position = Vector3.MoveTowards(
                stageIcon.position,
                targetPos,
                moveSpeed * Time.deltaTime
            );
            yield return null;
        }

        stageIcon.position = targetPos;
        isMoving = false;
    }

    // =========================================================
    // ステージ決定
    // =========================================================
    private void EnterStage()
    {
        if (ScreenTransitionManager.Instance == null)
        {
            Debug.LogWarning("ScreenTransitionManagerが見つからない");
            return;
        }

        isMoving = true;

        ScreenTransitionManager.Instance.TransitionToScene(
            stages[currentIndex].sceneName,
            stageIcon.position
        );
    }

    // =========================================================
    // パスライン描画
    // =========================================================
    private void DrawPath()
    {
        if (pathLine == null) return;

        pathLine.positionCount = stages.Length;
        pathLine.startWidth = lineWidth;
        pathLine.endWidth = lineWidth;
        pathLine.startColor = lineColor;
        pathLine.endColor = lineColor;
        pathLine.material = new Material(Shader.Find("Sprites/Default"));

        for (int i = 0; i < stages.Length; i++)
        {
            pathLine.SetPosition(i, stages[i].nodeTransform.position);
        }
    }
}