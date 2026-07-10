using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// シーンをまたいでゲームデータを保持するシングルトン
/// クリア済みステージ番号と最後にプレイしたステージ番号を管理する
/// </summary>
public class GameData : MonoBehaviour
{
    public static GameData Instance { get; private set; }

    // クリア済みステージのインデックス一覧
    private HashSet<int> clearedStages = new HashSet<int>();

    // ステージセレクトに戻った時にアイコンを置く位置
    public int LastPlayedStageIndex { get; private set; } = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// ステージクリア時に呼ぶ。GoalScript2Dから呼ばれる。
    /// </summary>
    public void SetCleared(int stageIndex)
    {
        clearedStages.Add(stageIndex);
        LastPlayedStageIndex = stageIndex;
    }

    /// <summary>
    /// 指定ステージがクリア済みかどうか
    /// </summary>
    public bool IsCleared(int stageIndex)
    {
        return clearedStages.Contains(stageIndex);
    }
}