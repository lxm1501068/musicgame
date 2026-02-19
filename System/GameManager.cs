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
    private float pausedPlayTime = 0; // 新增：记录暂停瞬间的播放时间

    // 供音符访问的「精准播放时间」（排除暂停）
    public float CurrentPlayTime
    {
        get
        {
            if (!IsPlaying)
            {
                // 修复：暂停时返回「暂停瞬间的播放时间」，而非动态计算
                return pausedPlayTime;
            }
            // 播放中：当前时间 - 起始时间 - 累计暂停时长
            return Time.time - chartStartTime - pauseAccumulator;
        }
    }

    private void Awake()
    {
        // 修复：严谨的单例模式（跨场景保留+防止重复创建）
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 跨场景保留单例
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 校验ChartRunner引用（避免空引用）
        if (chartRunner == null)
        {
            Debug.LogError("GameManager: 未赋值ChartRunner引用！请在Unity编辑器中绑定。");
        }

        // 修复：初始化暂停相关变量，避免首次暂停时值为0导致计算错误
        lastPauseTime = Time.time;
        pausedPlayTime = 0;
    }

    #region 谱面播放控制核心方法
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
        // 修复：防止重复调用PlayChart导致时间轴错乱
        if (IsPlaying)
        {
            Debug.LogWarning("GameManager.PlayChart: 谱面已在播放中，无需重复调用！");
            return;
        }

        // 重置播放状态
        IsPlaying = true;
        chartStartTime = Time.time;
        pauseAccumulator = 0; // 清空暂停累计时长
        pausedPlayTime = 0;   // 重置暂停时的播放时间
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
        if (!chartRunner.IsNotesPreCreated)
        {
            Debug.LogError("GameManager.TogglePlay: 未预创建音符，无法暂停/恢复！");
            return;
        }

        IsPlaying = !IsPlaying;
        if (IsPlaying)
        {
            // 恢复播放：累计暂停时长，修正起始时间锚点
            pauseAccumulator += Time.time - lastPauseTime;
            chartStartTime = Time.time - (pausedPlayTime + pauseAccumulator);
            Debug.Log($"GameManager: 谱面恢复播放 | 累计暂停时长：{pauseAccumulator:F2}秒");
        }
        else
        {
            // 暂停播放：记录暂停瞬间的绝对时间 + 播放时间
            lastPauseTime = Time.time;
            pausedPlayTime = CurrentPlayTime; // 关键：保存暂停时的播放时间
            Debug.Log($"GameManager: 谱面已暂停 | 暂停时播放时间：{pausedPlayTime:F2}秒");
        }
    }

    /// <summary>
    /// 停止播放（仅重置状态，不清理音符）
    /// </summary>
    public void StopChart()
    {
        if (chartRunner == null)
        {
            Debug.LogError("GameManager.StopChart: ChartRunner引用为空！");
            return;
        }

        // 仅重置全局播放状态（不清理音符）
        IsPlaying = false;
        pauseAccumulator = 0;
        lastPauseTime = Time.time;
        pausedPlayTime = 0;

        Debug.Log("GameManager: 谱面已停止，播放状态已重置（未清理音符）");
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
        Debug.Log("GameManager: 所有音符已预创建完成");
    }
    #endregion

    // 可选：防止意外销毁单例导致空引用
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}