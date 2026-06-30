using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// トップビュー(2D)用のグリッドベース移動コントローラー
/// WASDで1マスずつ移動する
/// 移動中は次の入力を受け付けない(マス移動が完了してから次に動ける)
/// </summary>
public class PlayerScript : MonoBehaviour
{
    //[Header("移動設定")]
    //[Tooltip("1マス移動する速さ (units / sec)")]
    //[SerializeField] private float moveSpeed = 5f;

    //[Tooltip("1マスのサイズ (Tilemapのセルサイズと合わせる)")]
    //[SerializeField] private float gridSize = 1f;

    //[Tooltip("ブロックのオブジェクトについているタグ")]
    //[SerializeField] private string blockTag = "Block";

    private Vector3 targetPosition;
    private Vector3 startPosition; // スタート地点(EnemyScriptなどから戻す際に使用)

    [SerializeField] private Tilemap targetTilemap; // 対象のタイルマップ
    [SerializeField] private Tilemap wallTilemap; //壁のタイルマップ
    [SerializeField] private float moveSpeed = 5f;    // 移動速度

    private Vector3Int currentCell; // 現在のグリッド座標
    private Vector3 targetWorldPos; // 移動先のワールド座標
    private bool isMoving = false;  // 移動中かどうかのフラグ


    private void Start()
    {
        // 起動時の位置をグリッドにスナップしておく(エディタ上で半端な位置に置いても安全)
        //targetPosition = SnapToGrid(transform.position);
        //transform.position = targetPosition;
        //startPosition = targetPosition; // スタート地点として記録

        // ゲーム開始時の現在位置をグリッド座標に変換
        currentCell = targetTilemap.WorldToCell(transform.position);
        // マスの中心のワールド座標を取得して、プレイヤーをピタッと合わせる
        transform.position = targetTilemap.GetCellCenterWorld(currentCell);
    }

    private void Update()
    {// 移動中は新しい入力を受け付けない
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
        //Vector3 dir = Vector3.zero;

        //if (Input.GetKeyDown(KeyCode.W)) dir = Vector3.up;
        //else if (Input.GetKeyDown(KeyCode.S)) dir = Vector3.down;
        //else if (Input.GetKeyDown(KeyCode.A)) dir = Vector3.left;
        //else if (Input.GetKeyDown(KeyCode.D)) dir = Vector3.right;

        //if (dir == Vector3.zero) return;

        //Vector3 destination = transform.position + dir * gridSize;
        // WASD（または矢印キー）の入力を取得

        int moveX = 0;
        int moveY = 0;

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) moveX = -1;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) moveX = 1;
        else if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) moveY = 1;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) moveY = -1;
        // 移動先にブロックがあるかチェック
        //Collider2D hit = Physics2D.OverlapPoint(destination);
        //if (hit != null && hit.CompareTag(blockTag))
        //{
        //    BlockScript block = hit.GetComponent<BlockScript>();
        //    if (block != null)
        //    {
        //        // ブロックを殴る(押す)。プレイヤー自身はその場から動かない。
        //        block.TryPush(dir);
        //    }
        //    return;
        //}

        //targetPosition = destination;
        //isMoving = true;
        // 斜め移動はさせず、上下か左右どちらかの入力があった場合のみ処理
        if (moveX != 0 || moveY != 0)
        {
            Vector3Int nextCell = currentCell + new Vector3Int(moveX, moveY, 0);
            //移動先のマスに「壁」があるかチェック
            if (HasWall(nextCell))
            {
                // 壁があったら何もしない（ここに効果音などを入れても良い）
                Debug.Log("壁があるので進めません！");
                return;
            }
            // 今はそのまま移動を開始する
            StartMove(nextCell);
        }
    }
    // 移動の開始処理
    private void StartMove(Vector3Int nextCell)
    {
        currentCell = nextCell;
        targetWorldPos = targetTilemap.GetCellCenterWorld(currentCell);
        isMoving = true;
    }
    private bool HasWall(Vector3Int cellPosition)
    {
        // 壁用タイルマップから、指定座標のタイルを取得
        TileBase tile = wallTilemap.GetTile(cellPosition);

        // タイルが「空っぽではない（＝何か描かれている）」なら壁とみなす
        return tile != null;
    }

    // マスの中心へ向かってスムーズに移動させる
    private void MoveSmoothly()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, moveSpeed * Time.deltaTime);

        // 目的のマスにぴったり到着したら移動終了
        if (Vector3.Distance(transform.position, targetWorldPos) < 0.001f)
        {
            transform.position = targetWorldPos;
            isMoving = false;
        }
    }

    private void MoveToTarget()
    {
        transform.position = Vector3.MoveTowards(transform.position,targetWorldPos,moveSpeed * Time.deltaTime);

        if ((transform.position - targetWorldPos).sqrMagnitude < 0.0001f)
        {
            transform.position = targetWorldPos;
            isMoving = false;
        }
    }

    //private Vector3 SnapToGrid(Vector3 pos)
    //{
    //    float x = Mathf.Round(pos.x / gridSize) * gridSize;
    //    float y = Mathf.Round(pos.y / gridSize) * gridSize;
    //    return new Vector3(x, y, pos.z);
    //}

    /// <summary>
    /// プレイヤーをスタート地点へ戻す(やり直し)。
    /// 移動中の目標地点(targetPosition)と移動フラグ(isMoving)もリセットすることで、
    /// 戻した直後に古い目標地点へ動き出してしまうのを防ぐ。
    /// EnemyScriptのセンサーなど、外部から呼び出す想定。
    /// </summary>
    public void ResetToStart()
    {
        transform.position = startPosition;
        targetPosition = startPosition;
        isMoving = false;
    }
}