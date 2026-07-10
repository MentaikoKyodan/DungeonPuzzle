using UnityEngine;
using System.Collections;

public class BlockScript : MonoBehaviour
{
    [Header("設定関係")]
    [SerializeField] private Grid targetGrid;          // シーン上のGrid（Tilemapの親）
    [SerializeField] private float moveSpeed = 5f;     // ブロックが動くスピード（お好みで調整）
    [SerializeField] private LayerMask obstacleLayer;  // 壁のレイヤー（これに触れると止まる）

    private bool isMoving = false;
    public bool IsMoving => isMoving;

    private void Awake()
    {
        // targetGrid が Inspector で未設定の場合は親オブジェクトから探す
        if (targetGrid == null)
        {
            targetGrid = GetComponent<Grid>();
            if (targetGrid == null)
            {
                targetGrid = GetComponentInParent<Grid>();
            }
        }
    }

    
    

    // Blocks no longer read input themselves. Movement is triggered via TryPush/TryPushToWorld called by PlayerScript.

    // 移動先に壁（指定したレイヤー）があるか調べるセンサー
    public bool IsObstacleAt(Vector3 targetPos)
    {
        // 目標地点を中心に、半径0.4mの円の中に壁があるかチェック
        return Physics2D.OverlapCircle(targetPos, 0.4f, obstacleLayer);
    }

    // 1マス分をスーッと滑らかに動かす処理
    public IEnumerator SmoothMove(Vector3 targetPosition)
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

    /// <summary>
    /// プレイヤーなどから押されたときに呼ぶ。direction はセル単位の方向ベクトル。
    /// 押せるなら移動を開始して true を返す。押せないなら false を返す。
    /// </summary>
    public bool TryPush(Vector3Int direction)
    {
        if (isMoving) return false;
        Vector3 destWorld;
        if (targetGrid != null)
        {
            Vector3Int currentCell = targetGrid.WorldToCell(transform.position);
            Vector3Int destCell = currentCell + direction;
            destWorld = targetGrid.GetCellCenterWorld(destCell);
        }
        else
        {
            destWorld = transform.position + new Vector3(direction.x, direction.y, 0);
        }

        // 壁タイルや障害物レイヤーがあれば押せない
        if (IsObstacleAt(destWorld)) return false;

        // 押し先に別のブロックがあるかチェック
        Collider2D hit = Physics2D.OverlapCircle(destWorld, 0.2f);
        if (hit != null && hit.gameObject != gameObject && hit.CompareTag("Block"))
        {
            return false;
        }

        // 押せるので移動開始
        StartCoroutine(SmoothMove(destWorld));
        Debug.Log("移動しているよ");
        return true;
    }

    /// <summary>
    /// world座標で目的地を指定して押す処理を行う（Player側のTilemap座標に合わせたいときに使用）
    /// </summary>
    public bool TryPushToWorld(Vector3 destWorld)
    {
        if (isMoving) return false;

        if (IsObstacleAt(destWorld)) return false;

        Collider2D hit = Physics2D.OverlapCircle(destWorld, 0.2f);
        if (hit != null && hit.gameObject != gameObject && hit.CompareTag("Block"))
        {
            return false;
        }

        StartCoroutine(SmoothMove(destWorld));
        return true;
    }
}