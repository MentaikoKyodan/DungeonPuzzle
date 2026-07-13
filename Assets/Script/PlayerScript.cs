using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Tilemaps; // タイルマップを操作するために必要

public class PlayerScript : MonoBehaviour
{
    [Header("設定関係")]
    [SerializeField] private Grid targetGrid;          // シーン上のGrid（Tilemapの親）
    [SerializeField] private float moveSpeed = 5f;     // 移動スピード（プレイヤー・ブロック共通）
    [SerializeField] private LayerMask obstacleLayer; // 壁（Obstacle）のレイヤー
    [SerializeField] private LayerMask blockLayer;    // ブロック（Block）のレイヤー
    [SerializeField] private Tilemap blockTilemap;    // ブロックが描かれているTilemap
    [SerializeField] private GameObject blockRenderPrefab; // ブロックの見た目用プレハブ
    [SerializeField] private PlayerAnimationController animController;
    public Vector3 startPosition;

    // --- 溜め機能用の変数 ---
    private float spacePressedTime = 0f;
    private int chargeLevel = 0; // 0: 通常(1個), 1: 1段階(2個), 2: 2段階(3個)

    private bool isPlayerMoving = false; // プレイヤーが移動中か
    private bool isBlockMoving = false;  // ブロックが移動中か
    private Vector3 currentPosition;

    void Update()
    {
        // ★修正：何よりも最優先で溜め入力を監視する（移動中であっても溜められる！）
        HandleCharge();
        // プレイヤー自身、またはブロックが移動中なら、新しい入力を受け付けない
        if (isPlayerMoving || isBlockMoving) return;

        // 押された方向キーを判別する
        Vector3Int direction = Vector3Int.zero;
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) direction = Vector3Int.up;
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) direction = Vector3Int.down;
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) direction = Vector3Int.left;
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) direction = Vector3Int.right;

        // キーが押された場合の処理
        if (direction != Vector3Int.zero)
        {
            if (animController != null)
            {
                if (direction == Vector3Int.right)
                    animController.SetFacing(true);
                else if (direction == Vector3Int.left)
                    animController.SetFacing(false);
                // 上下の場合は向きそのまま
            }
            Vector3Int currentCell = targetGrid.WorldToCell(transform.position);
            Vector3Int targetCell = currentCell + direction;
            Vector3 targetPosition = targetGrid.GetCellCenterWorld(targetCell);

            // 【検知1】1マス先がただの壁なら進めない
            if (Physics2D.OverlapCircle(targetPosition, 0.4f, obstacleLayer))
            {
                ResetCharge(); // 壁にぶつかったら溜めリセット
                return;
            }

            // 【検知2】1マス先がブロックだった場合
            if (Physics2D.OverlapCircle(targetPosition, 0.4f, blockLayer))
            {
                // 現在のパワーで押せる最大個数（レベル0=1個, レベル1=2個, レベル2=3個）
                int maxPushable = chargeLevel + 1;

                // 連なっているブロックの座標リストを取得する
                List<Vector3Int> connectedBlocks = GetConnectedBlocks(targetCell, direction);

                // もし連なっている数が、今のパワー（最大個数）を超えていたら重くて押せない！
                if (connectedBlocks.Count > maxPushable)
                {
                    Debug.Log($"ブロックが {connectedBlocks.Count} 個連なっています。パワーが足りません！");
                    ResetCharge();
                    if (animController != null)
                        animController.SetState(PlayerAnimationController.AnimState.Idle);
                    return;
                }
                // 連なっているブロックの「さらに一歩先」の座標を計算
                Vector3Int finalBlockCell = connectedBlocks[connectedBlocks.Count - 1];
                Vector3Int checkNextCell = finalBlockCell + direction;
                Vector3 checkNextPosition = targetGrid.GetCellCenterWorld(checkNextCell);

                // その先が「壁」なら、いくらパワーがあっても押せない！
                if (Physics2D.OverlapCircle(checkNextPosition, 0.4f, obstacleLayer))
                {
                    Debug.Log("ブロックの先が壁なので押せません！");
                    ResetCharge();
                    if (animController != null)
                        animController.SetState(PlayerAnimationController.AnimState.Idle);
                    return;
                }

                // すべての条件をクリア！連なったブロックたちをまとめて動かす
                StartCoroutine(PunchAndPushRoutine(connectedBlocks, direction));

                // ブロックを押したら溜めは消費される
                ResetCharge();
                return;
            }

            // 1マス先が「壁」でも「ブロック」でもない（ただの空き地）なら、自分が1マス進む
            // 1マス先が「壁」でも「ブロック」でもない（ただの空き地）
            if (chargeLevel > 0)
            {
                // 溜めた状態で移動しようとしたら、何もなくても攻撃だけは出す
                StartCoroutine(PunchThenMoveRoutine(targetPosition));
                ResetCharge();
            }
            else
            {
                StartCoroutine(MovePlayerRoutine(targetPosition));
            }
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (ScreenTransitionManager.Instance != null)
                ScreenTransitionManager.Instance.TriggerGameOver(transform.position);
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (ScreenTransitionManager.Instance != null)
                ScreenTransitionManager.Instance.TransitionToScene("StageSelectScene", transform.position);
        }
    }
    // --- 溜め入力の処理 ---

    // 溜めてる時に空振りで前進する用（殴ってから移動）
    private IEnumerator PunchThenMoveRoutine(Vector3 targetPos)
    {
        isPlayerMoving = true; // 殴ってる間も入力ブロックしておく

        if (animController != null)
        {
            bool punchFinished = false;
            System.Action onFinished = () => { punchFinished = true; };

            animController.OnNonLoopAnimFinished += onFinished;
            animController.SetState(PlayerAnimationController.AnimState.Punch);

            while (!punchFinished)
                yield return null;

            animController.OnNonLoopAnimFinished -= onFinished;
        }

        yield return StartCoroutine(MovePlayerRoutine(targetPos)); // これがisPlayerMovingをfalseに戻してくれる

        if (animController != null)
            animController.SetState(PlayerAnimationController.AnimState.Idle);
    }

    // 殴りアニメを再生してから、ブロックを実際に動かす
    private IEnumerator PunchAndPushRoutine(List<Vector3Int> blockList, Vector3Int direction)
    {
        isBlockMoving = true;

        if (animController != null)
        {
            bool impactHappened = false;
            System.Action onImpact = () => { impactHappened = true; };

            animController.OnAnimImpact += onImpact; 
            animController.SetState(PlayerAnimationController.AnimState.Punch);

            while (!impactHappened) 
                yield return null;

            animController.OnAnimImpact -= onImpact; 
        }

        yield return StartCoroutine(MoveMultipleBlocksRoutine(blockList, direction));

        if (animController != null)
            animController.SetState(PlayerAnimationController.AnimState.Idle);

        isBlockMoving = false;
    }
    private void HandleCharge()
    {
        if (isBlockMoving) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            chargeLevel++;

            if (chargeLevel > 2)
            {
                chargeLevel = 2;
            }

            switch (chargeLevel)
            {
                case 0:
                    Debug.Log("【パワー：通常】木箱を 1 個押せます");
                    break;
                case 1:
                    Debug.Log("【パワー：1段階】木箱を 2 個同時に押せます！");
                    if (animController != null)
                        animController.SetState(PlayerAnimationController.AnimState.Charge1);
                    break;
                case 2:
                    Debug.Log("【パワー：2段階】木箱を 3 個同時に押せます！！");
                    if (animController != null)
                        animController.SetState(PlayerAnimationController.AnimState.Charge2);
                    break;
            }
        }
    }
    //溜め状態を完全にゼロ（通常パワー）に戻す
    private void ResetCharge()
    {
        spacePressedTime = 0f;
        chargeLevel = 0; // ← ここで確実に通常状態（1個押し）に戻します！
        Debug.Log("【パワー消費】通常状態に戻りました。再チャージが必要です。");
    }
    // --- プレイヤーを1マス滑らかに動かす処理 ---
    private IEnumerator MovePlayerRoutine(Vector3 targetPos)
    {
        isPlayerMoving = true;

        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPos,
                moveSpeed * Time.deltaTime
            );
            yield return null;
        }

        transform.position = targetPos;
        isPlayerMoving = false;
    }

    // --- ★超絶強化：連なった複数のブロックを同時に滑らかに動かす処理 ---
    private IEnumerator MoveMultipleBlocksRoutine(List<Vector3Int> blockList, Vector3Int direction)
    {
        isBlockMoving = true;

        if (animController != null)
            animController.SetState(PlayerAnimationController.AnimState.Push);

        List<GameObject> dummies = new List<GameObject>();
        List<Vector3> endPositions = new List<Vector3>();
        List<TileBase> originalTiles = new List<TileBase>();
        List<Vector3Int> nextCells = new List<Vector3Int>();

        for (int i = 0; i < blockList.Count; i++)
        {
            Vector3Int fromCell = blockList[i];
            Vector3Int toCell = fromCell + direction;

            originalTiles.Add(blockTilemap.GetTile(fromCell));
            nextCells.Add(toCell);

            Vector3 startPos = targetGrid.GetCellCenterWorld(fromCell);
            Vector3 endPos = targetGrid.GetCellCenterWorld(toCell);
            endPositions.Add(endPos);

            // ダミー生成
            GameObject dummy = Instantiate(blockRenderPrefab, startPos, Quaternion.identity);
            SpriteRenderer sr = dummy.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = blockTilemap.GetSprite(fromCell);

            // 動いてる間だけ当たり判定を持たせる
            BoxCollider2D col = dummy.GetComponent<BoxCollider2D>();
            if (col == null) col = dummy.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            dummy.layer = GetLayerFromMask(blockLayer); // Block Layerに設定

            dummies.Add(dummy);
        }

        // 一斉にタイルマップから消去
        foreach (var cell in blockList)
        {
            blockTilemap.SetTile(cell, null);
        }

        // すべてのダミーブロックを同時にスーッと目標位置まで動かす
        bool allArrived = false;
        while (!allArrived)
        {
            allArrived = true;
            for (int i = 0; i < dummies.Count; i++)
            {
                dummies[i].transform.position = Vector3.MoveTowards(
                    dummies[i].transform.position,
                    endPositions[i],
                    moveSpeed * Time.deltaTime
                );

                if (Vector3.Distance(dummies[i].transform.position, endPositions[i]) > 0.01f)
                {
                    allArrived = false;
                }
            }
            yield return null;
        }

        // 目的地に着いたら、すべてのダミーを消して、本物のタイルマップの新しい位置にデータを書き戻す
        for (int i = 0; i < dummies.Count; i++)
        {
            Destroy(dummies[i]);
            blockTilemap.SetTile(nextCells[i], originalTiles[i]);
        }

        isBlockMoving = false;
    }

    // LayerMaskからレイヤー番号を取り出すヘルパー
    private int GetLayerFromMask(LayerMask mask)
    {
        int layerNumber = 0;
        int layer = mask.value;
        while (layer > 1)
        {
            layer = layer >> 1;
            layerNumber++;
        }
        return layerNumber;
    }
    public void ResetToStart(Vector3Int nextCell)
    {
        Debug.Log("Resetting player to start position.");
        transform.position = startPosition;
        // startPosition (Vector3) を Vector3Int に変換して nextCell に代入
        //nextCell = Vector3Int.RoundToInt(startPosition);
        currentPosition = startPosition;
    }
    // 指定した方向に向かって、ブロックが何個連なっているかを調べる関数
    private List<Vector3Int> GetConnectedBlocks(Vector3Int startCell, Vector3Int direction)
    {
        List<Vector3Int> blockCells = new List<Vector3Int>();
        Vector3Int checkCell = startCell;

        // 前方にブロックがある限り、リストに座標を追加し続ける
        while (Physics2D.OverlapCircle(blockTilemap.GetCellCenterWorld(checkCell), 0.4f, blockLayer))
        {
            blockCells.Add(checkCell);
            checkCell += direction; // さらに1マス先へ進めてループ
        }

        return blockCells;
    }
}