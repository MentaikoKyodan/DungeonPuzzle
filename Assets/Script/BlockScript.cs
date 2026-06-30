using UnityEngine;

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

    [Tooltip("1マスのサイズ (PlayerScriptと同じ値にすること)")]
    [SerializeField] private float gridSize = 1f;

    private Vector3 targetPosition;
    private bool isMoving = false;

    private void Start()
    {
        targetPosition = SnapToGrid(transform.position);
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

        Vector3 destination = transform.position + direction * gridSize;

        // 押し先に別のブロックがあるかチェック(壁なども増やすならここにタグを追加)
        Collider2D hit = Physics2D.OverlapPoint(destination);
        if (hit != null && hit.gameObject != gameObject && hit.CompareTag("Block"))
        {
            return false; // 押し先がふさがっているので押せない
        }

        targetPosition = destination;
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