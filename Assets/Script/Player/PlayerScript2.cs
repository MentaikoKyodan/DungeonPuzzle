using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // ★追加：コントローラー(Gamepad)対応のため
using UnityEngine.Tilemaps; // タイルマップを操作するために必要

public class PlayerScript2 : MonoBehaviour
{
    [Header("設定関係")]
    [SerializeField] private Grid targetGrid;          // シーン上のGrid（Tilemapの親）
    [SerializeField] private float moveSpeed = 5f;     // 移動スピード（プレイヤー・ブロック共通）
    [SerializeField] private LayerMask obstacleLayer; // 壁（Obstacle）のレイヤー
    [SerializeField] private LayerMask blockLayer;    // ブロック（Block）のレイヤー
    [SerializeField] private Tilemap blockTilemap;    // ブロックが描かれているTilemap
    [SerializeField] private GameObject blockRenderPrefab; // ブロックの見た目用プレハブ
    [SerializeField] private PlayerAnimationController animController;
    [SerializeField] private GameObject particleObj; // パーティクルのオブジェクトを指定
    [SerializeField] private GameObject particleObj2;
    public bool isParticleActive = false; // パーティクルのオンオフを制御するフラグ

    public Vector3 startPosition;

    [Header("コントローラー設定")]
    [Tooltip("スティックをこの値以上倒したら方向入力とみなす")]
    [SerializeField, Range(0.1f, 0.9f)] private float stickThreshold = 0.5f;
    // スティックが一度ニュートラルに戻るまで連続入力させないためのフラグ
    private bool stickWasNeutral = true;

    [Header("Undo設定")]
    [SerializeField] private int maxHistoryCount = 50;

    private Stack<MoveRecord> historyStack = new Stack<MoveRecord>();
    private bool isUndoing = false;

    [Header("SE設定")] // ★追加
    [SerializeField] private AudioClip charge1Sound;
    [SerializeField] private AudioClip charge2Sound;
    [SerializeField] private AudioClip punchSound;

    [Header("カメラシェイク設定")] // ★追加
    [SerializeField] private float charge1ShakeMagnitude = 0.05f;
    [SerializeField] private float charge1ShakeDuration = 0.15f;
    [SerializeField] private float charge2ShakeMagnitude = 0.12f;
    [SerializeField] private float charge2ShakeDuration = 0.2f;

    // --- 溜め機能用の変数 ---
    private float spacePressedTime = 0f;
    private int chargeLevel = 0; // 0: 通常(1個), 1: 1段階(2個), 2: 2段階(3個)

    private bool isPlayerMoving = false; // プレイヤーが移動中か
    private bool isBlockMoving = false;  // ブロックが移動中か
    private Vector3 currentPosition;

    void Awake()
    {
        particleObj.SetActive(false);
        particleObj2.SetActive(false);
    }

    void Update()
    {
        HandleUndo();
        // ★何よりも最優先で溜め入力を監視する（移動中であっても溜められる！）
        HandleCharge();
        // プレイヤー自身、またはブロックが移動中なら、新しい入力を受け付けない
        if (isPlayerMoving || isBlockMoving) return;

        // 押された方向キーを判別する（キーボード＋コントローラー対応）
        Vector3Int direction = GetDirectionInput();

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
                //Undo用に「押す前」の状態を記録
                List<TileBase> tilesBeforePush = new List<TileBase>();
                foreach (var cell in connectedBlocks)
                    tilesBeforePush.Add(blockTilemap.GetTile(cell));

                historyStack.Push(new MoveRecord
                {
                    isBlockPush = true,
                    blockCellsBefore = new List<Vector3Int>(connectedBlocks),
                    pushDirection = direction,
                    blockTiles = tilesBeforePush
                });
                TrimHistory();

                // すべての条件をクリア！連なったブロックたちをまとめて動かす
                StartCoroutine(PunchAndPushRoutine(connectedBlocks, direction));

                // ブロックを押したら溜めは消費される
                ResetCharge();
                return;
            }
            // ★追加：Undo用に「移動前」の位置を記録
            historyStack.Push(new MoveRecord
            {
                isBlockPush = false,
                playerPosBefore = transform.position
            });
            TrimHistory();
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
    }

    // ★キーボードとコントローラーの入力をまとめて判定する関数
    private Vector3Int GetDirectionInput()
    {
        // --- ① キーボード入力（押した瞬間のみ反応） ---
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) return Vector3Int.up;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) return Vector3Int.down;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) return Vector3Int.left;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) return Vector3Int.right;

        // --- ② コントローラー入力（Gamepad.current が null なら接続なし） ---
        if (Gamepad.current == null) return Vector3Int.zero;

        // 左スティックの入力を取得
        Vector2 stick = Gamepad.current.leftStick.ReadValue();

        // D-padの入力も取得（スティック優先、無ければD-pad）
        Vector2 dpad = Gamepad.current.dpad.ReadValue();
        Vector2 rawInput = stick.magnitude >= stickThreshold ? stick : dpad;

        // 何も倒していない、または閾値未満ならニュートラル扱い
        if (rawInput.magnitude < stickThreshold)
        {
            stickWasNeutral = true; // 次の入力を受け付ける準備完了
            return Vector3Int.zero;
        }

        // スティックが倒れっぱなしで、まだニュートラルに戻っていない間は連射させない
        if (!stickWasNeutral) return Vector3Int.zero;

        // ここまで来たら「倒した瞬間」なので1回だけ方向を返す
        stickWasNeutral = false;

        // 斜め入力は縦横どちらか強い方に丸める
        if (Mathf.Abs(rawInput.x) > Mathf.Abs(rawInput.y))
            return rawInput.x > 0 ? Vector3Int.right : Vector3Int.left;
        else
            return rawInput.y > 0 ? Vector3Int.up : Vector3Int.down;
    }
    //Undo入力の監視
    private void HandleUndo()
    {
        if (isPlayerMoving || isBlockMoving || isUndoing) return;
        if (historyStack.Count == 0) return;

        bool undoPressed = Input.GetKeyDown(KeyCode.Z);

        if (!undoPressed && Gamepad.current != null)
        {
            undoPressed = Gamepad.current.leftShoulder.wasPressedThisFrame;
        }

        if (undoPressed)
        {
            StartCoroutine(UndoRoutine());
        }
    }

    //履歴が溜まりすぎないようにする
    private void TrimHistory()
    {
        if (historyStack.Count <= maxHistoryCount) return;

        var temp = historyStack.ToArray();
        historyStack.Clear();
        for (int i = temp.Length - 2; i >= 0; i--)
            historyStack.Push(temp[i]);
    }

    //直前の行動を取り消す
    private IEnumerator UndoRoutine()
    {
        isUndoing = true;

        MoveRecord record = historyStack.Pop();

        if (record.isBlockPush)
        {
            for (int i = 0; i < record.blockCellsBefore.Count; i++)
            {
                Vector3Int originalCell = record.blockCellsBefore[i];
                Vector3Int movedCell = originalCell + record.pushDirection;

                blockTilemap.SetTile(movedCell, null);
                blockTilemap.SetTile(originalCell, record.blockTiles[i]);
            }

            if (animController != null)
                animController.SetState(PlayerAnimationController.AnimState.Idle);
        }
        else
        {
            transform.position = record.playerPosBefore;

            if (animController != null)
                animController.SetState(PlayerAnimationController.AnimState.Idle);
        }

        ResetCharge();

        yield return null;
        isUndoing = false;
    }
    // --- 殴りアニメを再生してから、ブロックを実際に動かす ---
    private IEnumerator PunchAndPushRoutine(List<Vector3Int> blockList, Vector3Int direction)
    {
        isBlockMoving = true;

        if (animController != null)
        {
            bool impactHappened = false;
            System.Action onImpact = () =>
            {
                impactHappened = true;
                // ★殴った瞬間にSE
                if (SEManager.Instance != null)
                    SEManager.Instance.PlaySE(punchSound);
            };

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

        // キーボードのSpace、またはコントローラーのSouthボタン(Aボタン/×ボタン)で溜め
        bool chargeButtonPressed = Input.GetKeyDown(KeyCode.Space);
        if (!chargeButtonPressed && Gamepad.current != null)
        {
            chargeButtonPressed = Gamepad.current.buttonSouth.wasPressedThisFrame;
        }

        if (chargeButtonPressed)
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
                        particleObj2.SetActive(true);
                    animController.SetState(PlayerAnimationController.AnimState.Charge1);

                    // ★SEとシェイク
                    if (SEManager.Instance != null)
                        SEManager.Instance.PlaySE(charge1Sound);
                    if (CameraShake.Instance != null)
                        CameraShake.Instance.Shake(charge1ShakeMagnitude, charge1ShakeDuration);
                    break;
                case 2:
                    Debug.Log("【パワー：2段階】木箱を 3 個同時に押せます！！");
                    if (animController != null)
                    {
                        particleObj2.SetActive(false);
                        particleObj.SetActive(true);
                    }
                    animController.SetState(PlayerAnimationController.AnimState.Charge2);

                    // ★SEとシェイク
                    if (SEManager.Instance != null)
                        SEManager.Instance.PlaySE(charge2Sound);
                    if (CameraShake.Instance != null)
                        CameraShake.Instance.Shake(charge2ShakeMagnitude, charge2ShakeDuration);
                    break;
            }
        }
    }

    // 溜め状態を完全にゼロ（通常パワー）に戻す
    private void ResetCharge()
    {
        spacePressedTime = 0f;
        chargeLevel = 0; // ← ここで確実に通常状態（1個押し）に戻します！
        Debug.Log("【パワー消費】通常状態に戻りました。再チャージが必要です。");
        particleObj.SetActive(false);
        particleObj2.SetActive(false);
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

    // --- 溜めてる時に空振りで前進する用（殴ってから移動） ---
    private IEnumerator PunchThenMoveRoutine(Vector3 targetPos)
    {
        isPlayerMoving = true; // 殴ってる間も入力ブロックしておく

        if (animController != null)
        {
            bool impactHappened = false;
            System.Action onImpact = () =>
            {
                impactHappened = true;
                // ★殴った瞬間にSE（空振りでも鳴らす）
                if (SEManager.Instance != null)
                    SEManager.Instance.PlaySE(punchSound);
            };

            animController.OnAnimImpact += onImpact;
            animController.SetState(PlayerAnimationController.AnimState.Punch);

            while (!impactHappened)
                yield return null;

            animController.OnAnimImpact -= onImpact;
        }

        yield return StartCoroutine(MovePlayerRoutine(targetPos)); // これがisPlayerMovingをfalseに戻してくれる

        if (animController != null)
            animController.SetState(PlayerAnimationController.AnimState.Idle);
    }

    // --- 連なった複数のブロックを同時に滑らかに動かす処理 ---
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

    // ★LayerMaskからレイヤー番号を取り出すヘルパー
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
        currentPosition = startPosition;
    }

    // ★指定した方向に向かって、ブロックが何個連なっているかを調べる関数
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
    private struct MoveRecord
    {
        public bool isBlockPush;
        public Vector3 playerPosBefore;
        public List<Vector3Int> blockCellsBefore;
        public Vector3Int pushDirection;
        public List<TileBase> blockTiles;
    }
}