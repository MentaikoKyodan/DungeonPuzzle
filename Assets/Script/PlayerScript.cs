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
    public Vector3 startPosition; // スタート地点(EnemyScriptなどから戻す際に使
    [SerializeField] private LayerMask blockLayer;

    [SerializeField] private Tilemap targetTilemap; // 対象のタイルマップ
    [SerializeField] private Tilemap wallTilemap; //壁のタイルマップ
    [SerializeField] private float moveSpeed = 5f;    // 移動速度

    private Vector3Int currentCell; // 現在のグリッド座標
    private Vector3Int startCell; // スタート地点のグリッド座標
    public Vector3 targetWorldPos; // 移動先のワールド座標
    private bool isMoving = false;  // 移動中かどうかのフラグ
    // 移動前のワールド座標（到着後に重なりが発生した場合はここへ戻す）
    private Vector3 prevWorldPos;
    // ブロックを押している間、プレイヤーがその方向に動かないようにロックする
    private Vector3Int blockedDirection = Vector3Int.zero;

    // インスペクター等でブロックの
    [SerializeField] private BlockScript blockMover;



    private void Start()
    {
        // 起動時の位置をグリッドにスナップしておく(エディタ上で半端な位置に置いても安全)
        startPosition = transform.position; // スタート地点として記録

        // ゲーム開始時の現在位置をグリッド座標に変換
        startCell = currentCell = targetTilemap.WorldToCell(transform.position);

        // マスの中心のワールド座標を取得して、プレイヤーをピタッと合わせる
        transform.position = targetTilemap.GetCellCenterWorld(currentCell);
    }

    private System.Collections.IEnumerator UnlockWhenBlockStopped(BlockScript bs)
    {
        // ブロックが動き終わるまで待つ
        while (bs != null && bs.IsMoving)
        {
            yield return null;
        }
        blockedDirection = Vector3Int.zero;
    }

    private void Update()
    {// 移動中は新しい入力を受け付けない
        if (!isMoving)
        {
            HandleInput();
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            if (ScreenTransitionManager.Instance != null)
                ScreenTransitionManager.Instance.TriggerGameOver(transform.position);
        }
        else
        {
            MoveToTarget();
        }
    }

   

    private void HandleInput()
    {
        // もしブロックが動いている最中なら、プレイヤーはキー入力を無視して動けない
        if (isMoving) return;

        Vector3Int direction = Vector3Int.zero;
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) direction = Vector3Int.up;
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) direction = Vector3Int.down;
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) direction = Vector3Int.left;
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) direction = Vector3Int.right;
        // ブロックを押している間、その方向の入力は無視する
        if (direction != Vector3Int.zero && direction == blockedDirection)
        {
            return;
        }
        if (blockMover != null && blockMover.IsMoving)
        {
            return;
        }
        // 斜め移動はさせず、上下か左右どちらかの入力があった場合のみ処理
        if (direction != Vector3Int.zero)
        {
            // 移動前の位置を記録しておく
            prevWorldPos = transform.position;

            Vector3Int nextCell = currentCell + direction;
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



    private void MoveToTarget()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, moveSpeed * Time.deltaTime);

        if ((transform.position - targetWorldPos).sqrMagnitude < 0.0001f)
        {
            transform.position = targetWorldPos;
            isMoving = false;
        }
    }


    /// 移動を停止し、移動先を指定したワールド座標に設定する。
    /// 外部から移動を強制的に止めたい場合に使用する。
    //public void StopMovement(Vector3Int cell)
    //{
    //    currentCell = cell;
    //    targetWorldPos = targetTilemap.GetCellCenterWorld(currentCell);
    //    isMoving = true;
    //}

    //private Vector3 SnapToGrid(Vector3 pos)
    //{
    //    float x = Mathf.Round(pos.x / gridSize) * gridSize;
    //    float y = Mathf.Round(pos.y / gridSize) * gridSize;
    //    return new Vector3(x, y, pos.z);
    //}


    //Vector3Int型のNextCellをstartPositionに初期化
    public void ResetToStart(Vector3Int nextCell)
    {
        Debug.Log("Resetting player to start position.");
        transform.position = startPosition;
        targetWorldPos = startPosition;
        // startPosition (Vector3) を Vector3Int に変換して nextCell に代入
        //nextCell = Vector3Int.RoundToInt(startPosition);
        currentCell = startCell;
    }
}