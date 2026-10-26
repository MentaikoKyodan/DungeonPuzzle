using UnityEngine;
using UnityEngine.Tilemaps;

public class CameraTilemapSpawner : MonoBehaviour
{
    [Header("Tilemap設定")]
    [SerializeField] private Tilemap cameraTilemap;     // 最初からオン
    [SerializeField] private Tilemap offCameraTilemap;  // 最初はオフ

    [Header("生成設定")]
    [SerializeField] private GameObject cameraSensorPrefab;
    [SerializeField] private bool clearTilesAfterSpawn = false;

    [Header("向きごとのタイル参照")]
    [Tooltip("上向きの監視カメライラストのタイルアセット")]
    [SerializeField] private TileBase upTile;
    [Tooltip("右向きの監視カメライラストのタイルアセット")]
    [SerializeField] private TileBase rightTile;
    [Tooltip("左向きの監視カメライラストのタイルアセット")]
    [SerializeField] private TileBase leftTile;
    [Tooltip("下向きの監視カメライラストのタイルアセット（使わないなら空でOK）")]
    [SerializeField] private TileBase downTile;

    void Start()
    {
        SpawnCameras(cameraTilemap, true);
        SpawnCameras(offCameraTilemap, false);
    }

    private void SpawnCameras(Tilemap tilemap, bool startActive)
    {
        if (tilemap == null) return;

        foreach (var cell in tilemap.cellBounds.allPositionsWithin)
        {
            TileBase tile = tilemap.GetTile(cell);
            if (tile == null) continue;

            EnemyScript.SensorAxis? axis = TileToAxis(tile);
            if (axis == null)
            {
                Debug.LogWarning($"{cell} のタイルがどの向きにも登録されてない！");
                continue;
            }

            Vector3 worldPos = tilemap.GetCellCenterWorld(cell);
            GameObject obj = Instantiate(cameraSensorPrefab, worldPos, Quaternion.identity);

            EnemyScript enemy = obj.GetComponent<EnemyScript>();
            if (enemy != null)
                enemy.Initialize(axis.Value, false, startActive);

            if (clearTilesAfterSpawn)
                tilemap.SetTile(cell, null);
        }
    }

    private EnemyScript.SensorAxis? TileToAxis(TileBase tile)
    {
        if (tile == upTile) return EnemyScript.SensorAxis.Up;
        if (tile == rightTile) return EnemyScript.SensorAxis.Right;
        if (tile == leftTile) return EnemyScript.SensorAxis.Left;
        if (tile == downTile) return EnemyScript.SensorAxis.Down;
        return null;
    }
}