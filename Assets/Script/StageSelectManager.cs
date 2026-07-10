using System.Collections;
using UnityEngine;

/// <summary>
/// ステージセレクト画面の管理スクリプト
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

        [Tooltip("ノードのSpriteRenderer。クリア済みの色変更に使う")]
        public SpriteRenderer nodeRenderer;
    }

    [Header("--- ステージ設定 ---")]
    [SerializeField] private StageNode[] stages;

    [Header("--- キャラクター設定 ---")]
    [SerializeField] private Transform stageIcon;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Vector2 iconOffset = new Vector2(0f, 0.5f);

    [Header("--- パスライン設定 ---")]
    [SerializeField] private LineRenderer pathLine;
    [SerializeField] private float lineWidth = 0.1f;
    [SerializeField] private Color lineColor = Color.white;

    [Header("--- クリア演出設定 ---")]
    [Tooltip("クリア済みノードに変わる色")]
    [SerializeField] private Color clearedColor = Color.yellow;

    // stageIconの位置をScreenTransitionManagerから取得するためのプロパティ
    public Vector3 StageIconWorldPos => stageIcon.position;

    private int currentIndex = 0;
    private bool isMoving = false;

    private void Start()
    {
        if (stages == null || stages.Length == 0)
        {
            Debug.LogWarning("StageSelectManager: ステージが登録されていない");
            return;
        }

        // GameDataが存在する場合、最後にプレイしたステージ番号から再開する
        if (GameData.Instance != null)
        {
            currentIndex = Mathf.Clamp(
                GameData.Instance.LastPlayedStageIndex,
                0,
                stages.Length - 1
            );

            // クリア済みステージのノード色を更新する
            for (int i = 0; i < stages.Length; i++)
            {
                if (GameData.Instance.IsCleared(i) && stages[i].nodeRenderer != null)
                    stages[i].nodeRenderer.color = clearedColor;
            }
        }

        // アイコンを該当ノード位置に配置
        stageIcon.position = (Vector2)stages[currentIndex].nodeTransform.position + iconOffset;

        DrawPath();
    }

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

    private void TryMove(int direction)
    {
        int next = currentIndex + direction;
        if (next < 0 || next >= stages.Length) return;

        currentIndex = next;
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

    private void EnterStage()
    {
        if (ScreenTransitionManager.Instance == null)
        {
            Debug.LogWarning("ScreenTransitionManagerが見つからない");
            return;
        }

        isMoving = true; // 演出中の入力を止める

        ScreenTransitionManager.Instance.TransitionToScene(
            stages[currentIndex].sceneName,
            stageIcon.position
        );
    }

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