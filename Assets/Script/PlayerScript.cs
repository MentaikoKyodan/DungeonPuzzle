using UnityEngine;

/// <summary>
/// トップビュー(2D)用のグリッドベース移動コントローラー
/// WASDで1マスずつ移動する
/// 移動中は次の入力を受け付けない(マス移動が完了してから次に動ける)
/// </summary>
public class PlayerScript : MonoBehaviour
{
    [Header("移動設定")]
    [Tooltip("1マス移動する速さ (units / sec)")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("1マスのサイズ (Tilemapのセルサイズと合わせる)")]
    [SerializeField] private float gridSize = 1f;

    private Vector3 targetPosition;
    private Vector3 startPosition;//スタート地点（EnemyScriptなどから戻す際に使用）

    private bool isMoving = false;

    private void Start()
    {
        // 起動時の位置をグリッドにスナップしておく(エディタ上で半端な位置に置いても安全)
        targetPosition = SnapToGrid(transform.position);
        transform.position = targetPosition;
        startPosition = targetPosition;//スタート地点として記録
    }

    private void Update()
    {
        if (!isMoving)
        {
            HandleInput();
        }
        else
        {
            MoveToTarget();
        }
    }

    private void HandleInput()
    {
        Vector3 dir = Vector3.zero;

        if (Input.GetKeyDown(KeyCode.W)) dir = Vector3.up;
        else if (Input.GetKeyDown(KeyCode.S)) dir = Vector3.down;
        else if (Input.GetKeyDown(KeyCode.A)) dir = Vector3.left;
        else if (Input.GetKeyDown(KeyCode.D)) dir = Vector3.right;

        if (dir != Vector3.zero)
        {
            // TODO: ここに後で「移動先に殴れるブロックがあるか」「監視範囲かどうか」の判定を入れる
            targetPosition = transform.position + dir * gridSize;
            isMoving = true;
        }
    }

    private void MoveToTarget()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        if ((transform.position - targetPosition).sqrMagnitude < 0.0001f)
        {
            transform.position = targetPosition;
            isMoving = false;
        }
    }

    private Vector3 SnapToGrid(Vector3 pos)
    {
        float x = Mathf.Round(pos.x / gridSize) * gridSize;
        float y = Mathf.Round(pos.y / gridSize) * gridSize;
        return new Vector3(x, y, pos.z);
    }
    /// <summary>
    /// プレイヤーをスタート地点へ戻す。（やり直し）
    /// 移動中の目標地点（targetPosition)と移動フラグ(isMoving)もリセットすることで、
    /// 戻した直後に古い目標地点へ動き出してしまうのを防ぐ。
    /// EnemyScriptのセンサーなど、外部から呼び出す想定。
    /// <summary>
    public void ResetToStart()
    {
        transform.position = startPosition;
        targetPosition = startPosition;
        isMoving = false;

    }
}