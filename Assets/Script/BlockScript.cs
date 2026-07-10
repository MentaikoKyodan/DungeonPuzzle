using UnityEngine;
using System.Collections;

public class BlockScript : MonoBehaviour
{
    [Header("設定関係")]
    [SerializeField] private Grid targetGrid;          // シーン上のGrid（Tilemapの親）
    [SerializeField] private float moveSpeed = 5f;     // ブロックが動くスピード（お好みで調整）
    [SerializeField] private LayerMask obstacleLayer;  // 壁のレイヤー（これに触れると止まる）

    private bool isMoving = false;

    // ★プレイヤーのスクリプトから「いまブロック動いてる？」を確認するための窓口
    public bool IsMoving => isMoving;

    // プレイヤーがコライダー（Is Trigger = ON）に触れている間、毎フレーム呼ばれる
    private void OnTriggerStay2D(Collider2D other)
    {
        // 触れたのがプレイヤーじゃなければ何もしない
        if (!other.CompareTag("Player")) return;

        // すでにブロックが移動中なら、新しい入力は受け付けない
        if (isMoving) return;

        // 押された方向キーを判別する
        Vector3Int direction = Vector3Int.zero;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) direction = Vector3Int.up;
        else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) direction = Vector3Int.down;
        else if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) direction = Vector3Int.left;
        else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) direction = Vector3Int.right;

        // 方向キーが押されたら処理をスタート
        if (direction != Vector3Int.zero)
        {
            // 1. 現在のマスから、次のマスの座標を計算
            Vector3Int currentCell = targetGrid.WorldToCell(transform.position);
            Vector3Int targetCell = currentCell + direction;
            Vector3 targetPosition = targetGrid.GetCellCenterWorld(targetCell);

            // 2. 移動先が「壁」じゃないかチェック
            if (!IsObstacleAt(targetPosition))
            {
                // 壁がなければ、滑らかに移動するコルーチンを開始
                StartCoroutine(SmoothMove(targetPosition));
            }
        }
    }

    // 移動先に壁（指定したレイヤー）があるか調べるセンサー
    private bool IsObstacleAt(Vector3 targetPos)
    {
        // 目標地点を中心に、半径0.4mの円の中に壁があるかチェック
        return Physics2D.OverlapCircle(targetPos, 0.4f, obstacleLayer);
    }

    // 1マス分をスーッと滑らかに動かす処理
    private IEnumerator SmoothMove(Vector3 targetPosition)
    {
        isMoving = true;

        // 目的地にほぼ到着するまで、少しずつ位置を近づける
        while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );
            yield return null; // 1フレーム待つ
        }

        // 最後にカチッと目的地の座標に合わせる
        transform.position = targetPosition;

        isMoving = false;
    }
}