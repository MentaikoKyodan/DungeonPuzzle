using UnityEngine;

/// <summary>
/// 2Dトップビュー用、移動しない(固定)エネミー。
///
/// 自分の向き(transform.right または transform.up)に沿って
/// 直線状のセンサーを伸ばす。エネミー自身は移動しない。
/// プレイヤーがそのセンサー線に触れたら、プレイヤーをスタート地点へ戻す(やり直し)。
///
/// エネミーをInspector上で回転させると、センサーの向きもそれに追従する。
/// </summary>
public class EnemyScript : MonoBehaviour
{
    public enum SensorAxis { Right, Up,Left,Down }
    
    [Header("センサー設定")]
    [Tooltip("センサーの「前方」をどちらの軸とするか")]
    [SerializeField] private SensorAxis sensorAxis = SensorAxis.Right;

    [Tooltip("逆方向に伸ばしたい場合はON")]
    [SerializeField] private bool invertDirection = false;

    [Tooltip("センサーの長さ(検知範囲)")]
    [SerializeField] private float sensorLength = 6f;

    [Tooltip("検知対象のタグ")]
    [SerializeField] private string targetTag = "Player";

    [Header("デバッグ表示(Sceneビュー)")]
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private Color gizmoColor = Color.red;

    [Header("ゲーム画面表示")]
    [Tooltip("Gameビュー(プレイ中の実際の画面)にもセンサー線を表示する")]
    [SerializeField] private bool showInGameView = true;
    [Tooltip("通常時のセンサー線の色")]
    [SerializeField] private Color idleColor = Color.red;
    [Tooltip("プレイヤーを検知した瞬間の色")]
    [SerializeField] private Color detectColor = Color.yellow;
    [Tooltip("センサーの太さ")]
    [SerializeField] private float lineWidth = 0.05f;

    [Tooltip("センサー線の描画順(小さいほど奥に表示される。ブロックなどのSprite Rendererより小さい値にすること)")]
    [SerializeField] private int sortingOrder = -1;

    private LineRenderer lineRenderer;

    // 前のフレームでプレイヤーを検知していたかどうか(検知の切り替わり判定用)
    private bool wasDetected = false;

    private void Awake()
    {
        // レイの発射地点が自分自身のコライダーの中にあるため、
        // これを無効にしないと毎回「自分自身」にヒットしてしまい、
        // プレイヤーまでレイが届かなくなる。
        Physics2D.queriesStartInColliders = false;

        // プロジェクト設定によってはIs TriggerのColliderをRaycastが無視してしまうため、
        // ここで明示的に「Triggerにも当たる」ようにしておく。
        // (ブロックなど、Is TriggerがオンのColliderでもセンサーが遮られるようにするため)
        Physics2D.queriesHitTriggers = true;

        if (showInGameView)
        {
            SetupLineRenderer();
        }
    }

    private void SetupLineRenderer()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = idleColor;
        lineRenderer.endColor = idleColor;
        lineRenderer.sortingOrder = sortingOrder; // ブロックなどのSpriteより奥に描画されるようにする
    }

    //範囲内に入ったらやり直し
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        if (!other.CompareTag(targetTag)) return;

        PlayerScript player = other.GetComponent<PlayerScript>();
        if (player != null)
        {
            // PlayerScript.ResetToStart now expects a Vector3Int parameter.
            // Convert the player's recorded startPosition (Vector3) to cell coordinates.
            player.ResetToStart(Vector3Int.RoundToInt(player.startPosition));
        }
    }
    
    //直線のセンサー
    void Start()
    {

    }

    void Update()
    {
        Vector2 origin = transform.position;
        Vector2 direction = GetSensorDirection();

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, sensorLength);
        bool isDetectedNow = hit.collider != null && hit.collider.CompareTag(targetTag);

        if (hit.collider != null)
            Debug.Log($"Raycastが何かに当たった: {hit.collider.name} / タグ: {hit.collider.tag}");
        // 「検知していない→検知した」に切り替わった瞬間だけ反応する。
        // 時間で制御しないので、プレイヤーが範囲に入るたびに何度でも反応する。
        if (isDetectedNow && !wasDetected)
        {
            Debug.Log("プレイヤー検知！TriggerDetectionを呼ぶよ");
            PlayerScript player = hit.collider.GetComponent<PlayerScript>();
            if (player != null)
            {
                // Use the player's startPosition converted to cell coordinates
                TriggerDetection(player);
            }
        }

        wasDetected = isDetectedNow;

        // Gameビューに表示するセンサー線の位置と色を更新
        // 何か(ブロックなど)に当たっていれば、その地点で線を止める
        if (showInGameView && lineRenderer != null)
        {
            Vector3 start = transform.position;
            Vector3 end = hit.collider != null
                ? (Vector3)hit.point
                : start + (Vector3)(direction * sensorLength);

            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);

            Color c = isDetectedNow ? detectColor : idleColor;
            lineRenderer.startColor = c;
            lineRenderer.endColor = c;
        }
    }

    /// <summary>
    /// 検知時の処理をまとめたメソッド
    /// ScreenTransitionManagerにプレイヤー位置を渡してゲームオーバー演出を起動する
    /// </summary>
    private void TriggerDetection(PlayerScript player)
    {
        if (ScreenTransitionManager.Instance != null)
        {
            ScreenTransitionManager.Instance.TriggerGameOver(player.transform.position);
        }
        else
        {
            // ScreenTransitionManagerがシーンにない場合の保険(直接リセット)
            Debug.LogWarning("ScreenTransitionManagerが見つからなかった。直接リセットするよ");
            player.ResetToStart(Vector3Int.RoundToInt(player.startPosition));
        }
    }

    private Vector2 GetSensorDirection()
    {
        Vector2 dir = sensorAxis switch
        {
            SensorAxis.Right => Vector2.right,
            SensorAxis.Left => Vector2.left,
            SensorAxis.Up => Vector2.up,
            SensorAxis.Down => Vector2.down,
            _ => Vector2.right
        };
        return invertDirection ? -dir : dir;
    }

    // シーンビューでセンサー線を可視化(エディタ用)
    private void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;
        Gizmos.color = gizmoColor;
        Vector2 dir = GetSensorDirection();
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(dir * sensorLength));
    }
}