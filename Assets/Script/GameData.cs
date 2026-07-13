using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// シーンをまたいでゲームデータを保持するシングルトン
/// クリア済みステージ・解放済みステージ・最後にプレイしたステージ番号を管理する
/// </summary>
public class GameData : MonoBehaviour
{
    public static GameData Instance { get; private set; }

    [Header("--- ステージ解放設定 ---")]
    [Tooltip("ONにすると最初のステージをクリアした瞬間に全ステージが解放される")]
    [SerializeField] private bool unlockAllOnFirstClear = false;

    private HashSet<int> clearedStages = new HashSet<int>();
    private HashSet<int> unlockedStages = new HashSet<int>();
    private bool allUnlocked = false;

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

        // ステージ0は最初から解放済み
        unlockedStages.Add(0);
    }

    /// <summary>
    /// ステージクリア時に呼ぶ。GoalScript2Dから呼ばれる。
    /// </summary>
    public void SetCleared(int stageIndex)
    {
        bool isFirstClear = clearedStages.Count == 0;
        clearedStages.Add(stageIndex);
        LastPlayedStageIndex = stageIndex;

        if (unlockAllOnFirstClear && isFirstClear)
        {
            // 初クリアで全ステージ一気に解放
            allUnlocked = true;
        }
        else
        {
            // 次のステージだけ解放
            unlockedStages.Add(stageIndex + 1);
        }
    }

    /// <summary>指定ステージがクリア済みか</summary>
    public bool IsCleared(int stageIndex) => clearedStages.Contains(stageIndex);

    /// <summary>指定ステージが解放済み(入れる状態)か</summary>
    public bool IsUnlocked(int stageIndex)
    {
        if (stageIndex == 0) return true;
        if (allUnlocked) return true;
        return unlockedStages.Contains(stageIndex);
    }
}