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
    [SerializeField] private bool canCharge = true; //このステージで溜め攻撃を許可するか
    public bool isParticleActive = false; // パーティクルのオンオフを制御するフラグ

    public Vector3 startPosition;

    [Header("コントローラー設定")]
    [Tooltip("スティックをこの値以上倒したら方向入力とみなす")]
    [SerializeField, Range(0.1f, 0.9f)] private float stickThreshold = 0.5f;
    // スティックが一度ニュートラルに戻るまで連続入力させないためのフラグ
    private bool stickWasNeutral = true;

    [Header("Undo設定")]

    private Stack<MoveRecord> historyStack = new Stack<MoveRecord>();
    private bool isUndoing = false;

    [Header("SE設定")]
    [SerializeField] private AudioClip charge1Sound;
    [SerializeField] private AudioClip charge2Sound;
    [SerializeField] private AudioClip punchSound;

    [Header("カメラシェイク設定")]
    [SerializeField] private float charge1ShakeMagnitude = 0.05f;
    [SerializeField] private float charge1ShakeDuration = 0.15f;
    [SerializeField] private float charge2ShakeMagnitude = 0.12f;
    [SerializeField] private float charge2ShakeDuration = 0.2f;

    [Header("エフェクト設定")]
    [SerializeField] private GameObject hitEffectPrefab; //殴った時に流すエフェクト
    [SerializeField] private float hitEffectSpeed = 2f;
    [SerializeField] private float hitEffectDelay = 0f;
    [SerializeField] private Vector2 hitEffectOffset = Vector2.zero;

    [SerializeField] private GameObject dustEffectPrefab; //土煙のプレハブ
    [SerializeField] private float dustEffectSpeed = 1f;  //土煙の再生速度
    [SerializeField] private Vector2 dustEffectOffset = Vector2.zero;

    [Header("溜めエフェクト設定")]
    [SerializeField] private Animator chargeEffect1Animator; // particleObj2(1段階目)についてるAnimator
    [SerializeField] private Animator chargeEffect2Animator; // particleObj(2段階目)についてるAnimator
    [SerializeField] private Vector2 chargeEffectOffset = Vector2.zero;

    [Header("木箱耐久値設定")]
    [Tooltip("耐久値に対応するタイル。index0=耐久1(ボロボロ)〜index4=耐久5(無傷)")]
    [SerializeField] private TileBase[] crateTilesByDurability = new TileBase[5];
    [SerializeField] private GameObject crateBreakEffectPrefab; // 破壊時エフェクト

    private Dictionary<Vector3Int, int> crateDurability = new Dictionary<Vector3Int, int>();
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

        particleObj.transform.localPosition = chargeEffectOffset;
        particleObj2.transform.localPosition = chargeEffectOffset;

        InitializeCrateDurability();
    }

    private void InitializeCrateDurability()
    {
        crateDurability.Clear();
        BoundsInt bounds = blockTilemap.cellBounds;
        foreach (var cell in bounds.allPositionsWithin)
        {
            TileBase tile = blockTilemap.GetTile(cell);
            if (tile == null) continue;

            int durability = GetDurabilityFromTile(tile);

            Debug.Log($"[初期スキャン] セル{cell} タイル名={tile.name} 判定耐久値={durability}");

            if (durability > 0)
                crateDurability[cell] = durability;
        }
        Debug.Log($"[初期スキャン完了] crateDurability登録数={crateDurability.Count} / InstanceID={GetInstanceID()}");
    }

    private int GetDurabilityFromTile(TileBase tile)
    {
        for (int i = 0; i < crateTilesByDurability.Length; i++)
        {
            if (crateTilesByDurability[i] == tile)
                return i + 1; // index0→耐久1 ... index4→耐久5
        }
        return -1; // 対応タイルが無ければ耐久値管理の対象外（今まで通り壊れない木箱扱い）
    }

    void Update()
    {
        HandleUndo();
        //何よりも最優先で溜め入力を監視する（移動中であっても溜められる！）
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
                if (animController != null)
                    animController.SetState(PlayerAnimationController.AnimState.Idle);
                return;
            }

            // 【検知2】1マス先がブロックだった場合
            if (Physics2D.OverlapCircle(targetPosition, 0.4f, blockLayer))
            {
                int maxPushable = chargeLevel + 1;
                List<Vector3Int> connectedBlocks = GetConnectedBlocks(targetCell, direction);
                int chargeLevelAtPunch = chargeLevel; // ResetChargeで消える前に確保しとく

                Debug.Log($"[座標チェック] targetCell={targetCell}, connectedBlocks内容={string.Join(",", connectedBlocks)}");

                if (connectedBlocks.Count > maxPushable)
                {
                    Debug.Log($"ブロックが {connectedBlocks.Count} 個連なっています。パワーが足りません！");
                    ResetCharge();
                    if (animController != null)
                        animController.SetState(PlayerAnimationController.AnimState.Idle);
                    return;
                }

                int pushDistance = Mathf.Max(1, (chargeLevel + 1) / connectedBlocks.Count);
                int actualPushDistance = GetActualPushDistance(connectedBlocks, direction, pushDistance);

                if (actualPushDistance <= 0)
                {
                    Debug.Log("ブロックの先が塞がっているので押せません！");

                    ResetCharge();
                    if (animController != null)
                        animController.SetState(PlayerAnimationController.AnimState.Idle);
                    return;
                }

                List<TileBase> tilesBeforePush = new List<TileBase>();
                foreach (var cell in connectedBlocks)
                    tilesBeforePush.Add(blockTilemap.GetTile(cell)); // ダメージ適用前＝殴る前の見た目を保存

                List<int> durabilityBeforePush = ApplyCrateDamage(connectedBlocks, chargeLevelAtPunch);

                historyStack.Push(new MoveRecord
                {
                    isBlockPush = true,
                    blockCellsBefore = new List<Vector3Int>(connectedBlocks),
                    pushDirection = direction,
                    pushDistance = actualPushDistance,
                    blockTiles = tilesBeforePush,
                    durabilityBefore = durabilityBeforePush
                });

                StartCoroutine(PunchAndPushRoutine(connectedBlocks, direction, actualPushDistance));

                ResetCharge();
                return;
            }
            // Undo用に「移動前」の位置を記録
            historyStack.Push(new MoveRecord
            {
                isBlockPush = false,
                playerPosBefore = transform.position
            });
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

    //キーボードとコントローラーの入力をまとめて判定する関数
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
    //直前の行動を取り消す
    private IEnumerator UndoRoutine()
    {
        isUndoing = true;

        MoveRecord record = historyStack.Pop();

        if (record.isDamageOnly)
        {
            RestoreCrateDurability(record.blockCellsBefore, record.durabilityBefore);

            if (animController != null)
                animController.SetState(PlayerAnimationController.AnimState.Idle);
        }
        else if (record.isBlockPush)
        {
            for (int i = 0; i < record.blockCellsBefore.Count; i++)
            {
                Vector3Int originalCell = record.blockCellsBefore[i];
                Vector3Int movedCell = originalCell + record.pushDirection * record.pushDistance;

                blockTilemap.SetTile(movedCell, null);
                blockTilemap.SetTile(originalCell, record.blockTiles[i]);

                //移動先に残ってる耐久値データを削除
                crateDurability.Remove(movedCell);
            }

            RestoreCrateDurability(record.blockCellsBefore, record.durabilityBefore);

            yield return null;
            foreach (var button in FindObjectsByType<ButtonSwitchScript>(FindObjectsSortMode.None))
                button.ForceRefresh();

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

    private void RestoreCrateDurability(List<Vector3Int> cells, List<int> durabilityBefore)
    {
        if (cells == null || durabilityBefore == null) return;

        for (int i = 0; i < cells.Count; i++)
        {
            int before = durabilityBefore[i];
            if (before <= 0) continue; // 対象外だった箱はスキップ

            crateDurability[cells[i]] = before;
            blockTilemap.SetTile(cells[i], crateTilesByDurability[before - 1]);
        }
    }
    // --- 殴りアニメを再生してから、ブロックを実際に動かす ---
    private IEnumerator PunchAndPushRoutine(List<Vector3Int> blockList, Vector3Int direction, int pushDistance)
    {
        isBlockMoving = true;

        if (animController != null)
        {
            bool impactHappened = false;
            System.Action onImpact = () =>
            {
                impactHappened = true;
                if (SEManager.Instance != null)
                    SEManager.Instance.PlaySE(punchSound);
            };

            animController.OnAnimImpact += onImpact;
            animController.SetState(PlayerAnimationController.AnimState.Punch);

            //impactを待たず、モーション開始と同時にエフェクトのタイマーを起動する
            StartCoroutine(SpawnHitEffectsDelayed(blockList));

            while (!impactHappened)
                yield return null;

            animController.OnAnimImpact -= onImpact;
        }

        yield return StartCoroutine(MoveMultipleBlocksRoutine(blockList, direction, pushDistance));

        if (animController != null)
            animController.SetState(PlayerAnimationController.AnimState.Idle);

        isBlockMoving = false;
    }

    private void HandleCharge()
    {
        if (isBlockMoving || isPlayerMoving) return;

        // キーボードのSpace、またはコントローラーのSouthボタン(Aボタン/×ボタン)で溜め
        bool chargeButtonPressed = Input.GetKeyDown(KeyCode.Space);
        if (!chargeButtonPressed && Gamepad.current != null)
        {
            chargeButtonPressed = Gamepad.current.buttonSouth.wasPressedThisFrame;
        }

        if (chargeButtonPressed)
        {
            if (!canCharge) return; //溜め禁止ステージなら何もせず終了

            if (chargeLevel >= 2) return;

            chargeLevel++;

            switch (chargeLevel)
            {
                case 0:
                    Debug.Log("【パワー：通常】木箱を 1 個押せます");
                    break;
                case 1:
                    Debug.Log("【パワー：1段階】木箱を 2 個同時に押せます！");
                    if (animController != null)
                        particleObj2.SetActive(true);

                    // 毎回最初のコマから再生し直す
                    if (chargeEffect1Animator != null)
                        chargeEffect1Animator.Play(0, 0, 0f);

                    animController.SetState(PlayerAnimationController.AnimState.Charge1);

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

                    //毎回最初のコマから再生し直す
                    if (chargeEffect2Animator != null)
                        chargeEffect2Animator.Play(0, 0, 0f);

                    animController.SetState(PlayerAnimationController.AnimState.Charge2);

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

        SpawnDustEffect(transform.position); //移動前の位置に土煙

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
                //殴った瞬間にSE（空振りでも鳴らす）
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
    private IEnumerator MoveMultipleBlocksRoutine(List<Vector3Int> blockList, Vector3Int direction, int pushDistance)
    {
        isBlockMoving = true;

        if (animController != null)
            animController.SetState(PlayerAnimationController.AnimState.Push);

        foreach (var cell in blockList)
        {
            SpawnDustEffect(targetGrid.GetCellCenterWorld(cell));
        }

        List<GameObject> dummies = new List<GameObject>();
        List<Vector3> endPositions = new List<Vector3>();
        List<TileBase> originalTiles = new List<TileBase>();
        List<Vector3Int> nextCells = new List<Vector3Int>();

        for (int i = 0; i < blockList.Count; i++)
        {
            Vector3Int fromCell = blockList[i];
            Vector3Int toCell = fromCell + direction * pushDistance;

            originalTiles.Add(blockTilemap.GetTile(fromCell));
            nextCells.Add(toCell);

            Vector3 startPos = targetGrid.GetCellCenterWorld(fromCell);
            Vector3 endPos = targetGrid.GetCellCenterWorld(toCell);
            endPositions.Add(endPos);

            GameObject dummy = Instantiate(blockRenderPrefab, startPos, Quaternion.identity);
            SpriteRenderer sr = dummy.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = blockTilemap.GetSprite(fromCell);

            BoxCollider2D col = dummy.GetComponent<BoxCollider2D>();
            if (col == null) col = dummy.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            dummy.layer = GetLayerFromMask(blockLayer);

            dummies.Add(dummy);
        }

        foreach (var cell in blockList)
        {
            blockTilemap.SetTile(cell, null);
        }

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

        //移動前に全部の耐久値を退避してから、元のキーを全部消す
        Dictionary<int, int> durabilityToMove = new Dictionary<int, int>();
        for (int i = 0; i < blockList.Count; i++)
        {
            if (crateDurability.TryGetValue(blockList[i], out int dur))
                durabilityToMove[i] = dur;
        }
        foreach (var cell in blockList)
        {
            crateDurability.Remove(cell);
        }
        //ここまで追加

        for (int i = 0; i < dummies.Count; i++)
        {
            Destroy(dummies[i]);
            blockTilemap.SetTile(nextCells[i], originalTiles[i]);

            //退避しておいた辞書から書き込む
            if (durabilityToMove.TryGetValue(i, out int dur))
            {
                crateDurability[nextCells[i]] = dur;
            }
        }

        isBlockMoving = false;
    }

    // connectedBlocksと同じ順番・同じ数で「殴る前の耐久値」を返す（対象外は-1）
    private List<int> ApplyCrateDamage(List<Vector3Int> connectedBlocks, int chargeLevelAtPunch)
    {
        int maxPushable = chargeLevelAtPunch + 1;
        int hitCount = Mathf.Min(connectedBlocks.Count, maxPushable);
        int baseDamage = chargeLevelAtPunch + 1; // 通常=1, 1段階=2, 2段階=3

        List<int> durabilityBefore = new List<int>();

        for (int i = 0; i < connectedBlocks.Count; i++)
        {
            Vector3Int cell = connectedBlocks[i];

            if (i >= hitCount || !crateDurability.ContainsKey(cell))
            {
                durabilityBefore.Add(-1);
                continue;
            }

            int before = crateDurability[cell];
            int damage = baseDamage; // ★変更：距離による減衰を廃止、全部同じダメージ
            int after = before - damage;

            durabilityBefore.Add(before);

            if (after <= 0)
            {
                crateDurability.Remove(cell);
                blockTilemap.SetTile(cell, null);
                if (crateBreakEffectPrefab != null)
                    Instantiate(crateBreakEffectPrefab, blockTilemap.GetCellCenterWorld(cell), Quaternion.identity);
            }
            else
            {
                crateDurability[cell] = after;
                blockTilemap.SetTile(cell, crateTilesByDurability[after - 1]);
            }
        }

        return durabilityBefore;
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
    private struct MoveRecord
    {
        public bool isBlockPush;
        public bool isDamageOnly; // 押せなかったけど殴ってダメージだけ入ったケース
        public Vector3 playerPosBefore;
        public List<Vector3Int> blockCellsBefore;
        public Vector3Int pushDirection;
        public int pushDistance;
        public List<TileBase> blockTiles;
        public List<int> durabilityBefore; // blockCellsBeforeと同じ順番、対象外は-1
    }
    // 何マス押せるか（壁や別のブロックにぶつかるまでの距離）を調べる
    private int GetActualPushDistance(List<Vector3Int> connectedBlocks, Vector3Int direction, int maxDistance)
    {
        Vector3Int finalBlockCell = connectedBlocks[connectedBlocks.Count - 1];

        for (int step = 1; step <= maxDistance; step++)
        {
            Vector3Int checkCell = finalBlockCell + direction * step;
            Vector3 checkPos = targetGrid.GetCellCenterWorld(checkCell);

            bool isObstacle = Physics2D.OverlapCircle(checkPos, 0.4f, obstacleLayer);
            bool isOtherBlock = false;

            if (!isObstacle)
            {
                Collider2D hit = Physics2D.OverlapCircle(checkPos, 0.4f, blockLayer);
                if (hit != null)
                {
                    // 押されている本人たちではなく、本当に別のブロックかどうか確認
                    if (!connectedBlocks.Contains(checkCell))
                        isOtherBlock = true;
                }
            }

            if (isObstacle || isOtherBlock)
                return step - 1; // ここまでしか進めない
        }

        return maxDistance; // 最後まで何もなかった
    }
    //ブロックが元々あった場所にヒットエフェクトを出す
    private IEnumerator SpawnHitEffectsDelayed(List<Vector3Int> blockList)
    {
        if (hitEffectDelay > 0f)
            yield return new WaitForSeconds(hitEffectDelay);

        SpawnHitEffects(blockList);
    }

    private void SpawnHitEffects(List<Vector3Int> blockList)
    {
        if (hitEffectPrefab == null) return;

        foreach (var cell in blockList)
        {
            Vector3 pos = targetGrid.GetCellCenterWorld(cell) + (Vector3)hitEffectOffset; //オフセットを加算
            GameObject effectInstance = Instantiate(hitEffectPrefab, pos, Quaternion.identity);

            Animator anim = effectInstance.GetComponent<Animator>();
            if (anim != null)
            {
                anim.speed = hitEffectSpeed;
            }
        }
    }
    //指定した位置に土煙エフェクトを出す
    private void SpawnDustEffect(Vector3 pos)
    {
        if (dustEffectPrefab == null) return;

        GameObject dust = Instantiate(dustEffectPrefab, pos + (Vector3)dustEffectOffset, Quaternion.identity);
        Animator anim = dust.GetComponent<Animator>();
        if (anim != null)
            anim.speed = dustEffectSpeed;
    }
}