using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CameraToggleManager : MonoBehaviour
{
    public static CameraToggleManager Instance { get; private set; }

    [Header("タイルマップ")]
    [SerializeField] private Tilemap buttonTilemap;
    [SerializeField] private TileBase idleTile;     // 押されてない時のタイル
    [SerializeField] private TileBase pressedTile;  // 押されてる時のタイル

    [Header("検知設定")]
    [SerializeField] private LayerMask blockLayer;

    [Header("SE設定")]
    [SerializeField] private AudioClip pressSE;

    private class ButtonState
    {
        public Vector3Int cell;
        public bool isPressed;
    }

    private readonly List<ButtonState> buttons = new List<ButtonState>();

    // どれか1つでも押されていたらtrue(カメラのオン/オフが反転する)
    public bool IsToggled { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        foreach (var cell in buttonTilemap.cellBounds.allPositionsWithin)
        {
            TileBase tile = buttonTilemap.GetTile(cell);
            if (tile == idleTile || tile == pressedTile)
                buttons.Add(new ButtonState { cell = cell });
        }
    }

    private void Update()
    {
        // 押し込み中のダミーブロックが上を通っただけで切り替わるのを防ぐ
        if (PlayerScript2.IsPushingBlocks) return;

        Refresh(true);
    }

    // Undoから呼ぶ用(SEは鳴らさない)
    public void ForceRefresh()
    {
        Refresh(false);
    }

    private void Refresh(bool playSE)
    {
        bool anyPressed = false;

        foreach (var b in buttons)
        {
            Vector3 center = buttonTilemap.GetCellCenterWorld(b.cell);
            bool pressedNow = Physics2D.OverlapPoint(center, blockLayer);

            if (pressedNow != b.isPressed)
            {
                if (pressedNow && playSE && pressSE != null && SEManager.Instance != null)
                    SEManager.Instance.PlaySE(pressSE);

                b.isPressed = pressedNow;
                buttonTilemap.SetTile(b.cell, pressedNow ? pressedTile : idleTile);
            }

            if (pressedNow) anyPressed = true;
        }

        IsToggled = anyPressed;
    }
}