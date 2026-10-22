using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ButtonTilemapManager : MonoBehaviour
{
    [Header("タイルマップ")]
    [SerializeField] private Tilemap buttonTilemap;
    [SerializeField] private TileBase idleTile;     // 押されてない時のタイル
    [SerializeField] private TileBase pressedTile;  // 押されてる時のタイル

    [Header("検知設定")]
    [SerializeField] private LayerMask blockLayer;

    [Header("SE設定")]
    [SerializeField] private AudioClip pressSE;

    [SerializeField] private bool relockOnRelease = true;

    private class ButtonState
    {
        public Vector3Int cell;
        public bool isPressed;
        public bool hasNotifiedGoal;
    }

    private readonly List<ButtonState> buttons = new List<ButtonState>();

    private void Start()
    {
        // 起動時にボタンタイルのあるマスを全部集める
        foreach (var cell in buttonTilemap.cellBounds.allPositionsWithin)
        {
            TileBase tile = buttonTilemap.GetTile(cell);
            if (tile == idleTile || tile == pressedTile)
                buttons.Add(new ButtonState { cell = cell });
        }
    }

    private void Update()
    {
        Refresh(true);
    }

    // Undoから呼ぶ用(SEは鳴らさない)
    public void ForceRefresh()
    {
        Refresh(false);
    }

    private void Refresh(bool playSE)
    {
        foreach (var b in buttons)
        {
            Vector3 center = buttonTilemap.GetCellCenterWorld(b.cell);
            bool pressedNow = Physics2D.OverlapPoint(center, blockLayer);

            // 見た目・SE
            if (pressedNow != b.isPressed)
            {
                if (pressedNow && playSE && pressSE != null && SEManager.Instance != null)
                    SEManager.Instance.PlaySE(pressSE);

                b.isPressed = pressedNow;
                buttonTilemap.SetTile(b.cell, pressedNow ? pressedTile : idleTile);
            }

            // ゴールへの通知
            bool shouldNotify = relockOnRelease ? pressedNow : (pressedNow || b.hasNotifiedGoal);
            if (shouldNotify == b.hasNotifiedGoal) continue;
            if (GoalScript2D.Instance == null) continue;

            if (shouldNotify)
                GoalScript2D.Instance.NotifyButtonPressed();
            else
                GoalScript2D.Instance.NotifyButtonReleased();

            b.hasNotifiedGoal = shouldNotify;
        }
    }
}