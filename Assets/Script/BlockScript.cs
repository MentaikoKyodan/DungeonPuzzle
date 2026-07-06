using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// プレイヤーに殴られると1マス押される(進む)ブロック。
/// PlayerScriptと同じグリッド移動方式を使う。
///
/// 必須: このオブジェクトにCollider2Dを付けておくこと
/// (EnemyScriptのセンサーが遮られる判定や、移動先の空き判定に使われる)。
/// Tagは"Block"にしておくこと(PlayerScript側がこのタグで判定する)。
/// </summary>
public class BlockScript : MonoBehaviour
{
    [Header("移動設定")]
    [Tooltip("1マス移動する速さ (units / sec)")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("1マスのサイズ (Tilemap を使わない場合にのみ使用)")]
    [SerializeField] private float gridSize = 1f;

    [Header("Tilemap (任意)")]
    [Tooltip("Tilemap を設定すると、そのグリッドに合わせて移動します。未設定の場合は gridSize を使用します。")]
    [SerializeField] private Tilemap tilemap;

    [Tooltip("壁タイルがある Tilemap。移動先にタイルがあると押せません。未設定なら Collider で判定します。")]
    [SerializeField] private Tilemap wallTilemap;

    private Vector3 targetPosition;
    private bool isMoving = false;

    private void Start()
    {
        // 初期位置をグリッドにスナップ
        if (tilemap != null)
        {
            Vector3Int cell = tilemap.WorldToCell(transform.position);
            targetPosition = tilemap.GetCellCenterWorld(cell);
        }
        else
        {
            targetPosition = SnapToGrid(transform.position);
        }
        transform.position = targetPosition;
    }

    private void Update()
    {
        if (isMoving)
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
    }

    /// <summary>
    /// 指定方向に1マス押せるかどうかを判定し、押せるなら移動を開始する。
    /// 押せたかどうかを返す(PlayerScript側はこれを見て、自分も進むかどうかを決める)。
    /// </summary>
    public bool TryPush(Vector3 direction)
    {
        if (isMoving) return false; // 自分が移動中は押せない
        Vector3 destinationWorld;

        if (tilemap != null)
        {
            // 現在セルを取得して、direction の整数オフセットを適用する
            Vector3Int currentCell = tilemap.WorldToCell(transform.position);
            Vector3Int offset = new Vector3Int(Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.y), 0);
            Vector3Int destCell = currentCell + offset;

            // 壁タイルがあるかチェック
            if (wallTilemap != null)
            {
                TileBase tile = wallTilemap.GetTile(destCell);
                if (tile != null) return false; // 壁がある
            }

            destinationWorld = tilemap.GetCellCenterWorld(destCell);

            // 押し先に別のブロックがあるかチェック
            Collider2D hit = Physics2D.OverlapPoint(destinationWorld);
            if (hit != null && hit.gameObject != gameObject && hit.CompareTag("Block"))
            {
                return false; // 押し先がふさがっている
            }
        }
        else
        {
            // Tilemap 未使用時は従来どおり gridSize を用いる
            Vector3 destination = transform.position + direction * gridSize;
            Collider2D hit = Physics2D.OverlapPoint(destination);
            if (hit != null && hit.gameObject != gameObject && hit.CompareTag("Block"))
            {
                return false;
            }
            destinationWorld = destination;
        }

        targetPosition = destinationWorld;
        isMoving = true;
        return true;
    }

    private Vector3 SnapToGrid(Vector3 pos)
    {
        float x = Mathf.Round(pos.x / gridSize) * gridSize;
        float y = Mathf.Round(pos.y / gridSize) * gridSize;
        return new Vector3(x, y, pos.z);
    }
}