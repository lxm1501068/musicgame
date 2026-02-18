using UnityEngine;

public class GameManager : MonoBehaviour
{
    // 单例实例（全局唯一）
    public static GameManager Instance;

    [Header("谱面运行器引用（Unity编辑器赋值）")]
    public ChartRunner chartRunner; // 关联ChartRunner组件

    // 播放状态与时间相关字段
    public bool IsPlaying { get; private set; } // 播放状态（只读，外部仅能通过方法修改）
    public float chartStartTime { get; private set; } // 播放起始时间锚点
    private float pauseAccumulator = 0; // 暂停时长累计（抵消暂停的时间差）
    private float lastPauseTime = 0; // 上次暂停的时间

    // 供音符访问的「精准播放时间」（排除暂停）
    public float CurrentPlayTime
    {
        get
        {
            if (!IsPlaying)
            {
                // 暂停时返回“暂停瞬间的播放时间”，避免音符继续移动
                return lastPauseTime - chartStartTime - pauseAccumulator;
            }
            // 播放中：当前时间 - 起始时间 - 累计暂停时长
            return Time.time - chartStartTime - pauseAccumulator;
        }
    }

    private void Awake()
    {
        // 单例初始化
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 校验ChartRunner引用（避免空引用）
        if (chartRunner == null)
        {
            Debug.LogError("GameManager: 未赋值ChartRunner引用！请在Unity编辑器中绑定。");
        }
    }

    #region 新增：谱面播放控制核心方法（从ChartRunner迁移）
    /// <summary>
    /// 开始播放谱面（需先调用PreCreateAllNotes预创建音符）
    /// </summary>
    public void PlayChart()
    {
        // 前置校验
        if (chartRunner == null)
        {
            Debug.LogError("GameManager.PlayChart: ChartRunner引用为空！");
            return;
        }
        if (!chartRunner.IsNotesPreCreated)
        {
            Debug.LogError("GameManager.PlayChart: 未预创建音符，请先调用PreCreateAllNotes！");
            return;
        }

        // 重置播放状态
        IsPlaying = true;
        chartStartTime = Time.time;
        pauseAccumulator = 0; // 清空暂停累计时长
        Debug.Log("GameManager: 谱面开始播放！");
    }

    /// <summary>
    /// 暂停/恢复谱面播放
    /// </summary>
    public void TogglePlay()
    {
        if (chartRunner == null)
        {
            Debug.LogError("GameManager.TogglePlay: ChartRunner引用为空！");
            return;
        }

        IsPlaying = !IsPlaying;
        if (IsPlaying)
        {
            // 恢复播放：累计暂停时长
            pauseAccumulator += Time.time - lastPauseTime;
            Debug.Log("GameManager: 谱面恢复播放");
        }
        else
        {
            // 暂停播放：记录暂停瞬间的时间
            lastPauseTime = Time.time;
            Debug.Log("GameManager: 谱面已暂停");
        }
    }

    /// <summary>
    /// 停止播放并清理所有音符
    /// </summary>
    public void StopChart()
    {
        if (chartRunner == null)
        {
            Debug.LogError("GameManager.StopChart: ChartRunner引用为空！");
            return;
        }

        // 重置全局播放状态
        IsPlaying = false;
        pauseAccumulator = 0;

        // 调用ChartRunner清理音符（核心：ChartRunner仅做具体的音符管理）
        chartRunner.CleanAllNotes();

        Debug.Log("GameManager: 谱面已停止，所有音符已清理");
    }

    /// <summary>
    /// 预创建所有音符（封装ChartRunner的方法，对外统一入口）
    /// </summary>
    public void PreCreateAllNotes()
    {
        if (chartRunner == null)
        {
            Debug.LogError("GameManager.PreCreateAllNotes: ChartRunner引用为空！");
            return;
        }
        chartRunner.PreCreateAllNotes();
    }
    #endregion
}