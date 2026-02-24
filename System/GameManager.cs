using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour
{
    // 单例实例（全局唯一）
    public static GameManager Instance;

    [Header("核心模块引用（Unity编辑器赋值）")]
    public ChartRunner chartRunner; // 音符创建/管理
    public LoadChart chartLoader;   // 谱面加载/解析
    public string initialChartFileName = "chart.txt"; // 初始加载的谱面文件名

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

        // 自动初始化LoadChart组件（若未赋值）
        InitLoadChart();
        
        // 校验核心引用
        ValidateCoreReferences();
    }

    #region 初始化与校验
    /// <summary>
    /// 初始化LoadChart组件（防止空引用）
    /// </summary>
    private void InitLoadChart()
    {
        if (chartLoader == null)
        {
            chartLoader = gameObject.AddComponent<LoadChart>();
            Debug.LogWarning("GameManager: 未赋值LoadChart，已自动挂载到自身");
        }
    }

    /// <summary>
    /// 校验核心模块引用（提前暴露错误）
    /// </summary>
    private void ValidateCoreReferences()
    {
        if (chartRunner == null)
        {
            Debug.LogError("GameManager: 未赋值ChartRunner引用！请在Unity编辑器中绑定。");
        }
        if (chartLoader == null)
        {
            Debug.LogError("GameManager: LoadChart初始化失败！请检查loadChartPrefab或手动挂载LoadChart组件。");
        }
    }
    #endregion

    #region 谱面加载&解析（核心新增逻辑）
    /// <summary>
    /// 加载并解析谱面（封装LoadChart的流程）
    /// </summary>
    /// <param name="fileName">谱面文件名（如chart.txt）</param>
    public void LoadAndParseChart(string fileName)
    {
        // 前置校验
        if (chartLoader == null)
        {
            Debug.LogError("GameManager.LoadAndParseChart: LoadChart引用为空！");
            return;
        }
        
        // 清空旧谱面数据
        ClearChartData();
        
        // 加载并解析谱面（协程）
        StartCoroutine(chartLoader.LoadChartFile(fileName));
        // 加载完成后解析（注：若需等待加载完成，需调整为回调/异步）
        Invoke(nameof(ParseLoadedChart), 0.1f); // 简易等待：实际项目建议用回调/AsyncAwait
    }

    /// <summary>
    /// 解析已加载的谱面内容
    /// </summary>
    private void ParseLoadedChart()
    {
        if (string.IsNullOrEmpty(chartLoader.ChartContent))
        {
            Debug.LogError("GameManager.ParseLoadedChart: 谱面内容为空，解析失败！");
            return;
        }
        
        // 解析谱面数据到ChartData
        ChartData parsedData = chartLoader.ParseChart();
        if (parsedData == null)
        {
            Debug.LogError("GameManager.ParseLoadedChart: 谱面解析失败！");
            return;
        }
        
        // 预创建所有音符
        PreCreateAllNotes();
        
        Debug.Log("GameManager: 谱面加载→解析→音符创建 全流程完成！");
    }

    /// <summary>
    /// 清空谱面数据（切换谱面时调用）
    /// </summary>
    public void ClearChartData()
    {
        // 清空ChartData的核心数据
        ChartData.Instance.ResetChartData();
        
        // 清空LoadChart的缓存
        ChartData.Instance.ClearChartContent();
        
        // 重置音符创建状态
        if (chartRunner != null)
        {
            // 若ChartRunner有清空音符的方法，此处调用（需补充ChartRunner的ClearNotes）
            // chartRunner.ClearAllNotes();
        }
        
        Debug.Log("GameManager: 旧谱面数据已清空");
    }
    #endregion

    #region 谱面播放控制核心方法（保留并优化）
    /// <summary>
    /// 开始播放谱面（需先完成加载→解析→创建音符）
    /// </summary>
    public void PlayChart()
    {
        // 前置校验：补充谱面数据和音符创建的校验
        if (chartRunner == null)
        {
            Debug.LogError("GameManager.PlayChart: ChartRunner引用为空！");
            return;
        }
        if (!chartRunner.IsNotesPreCreated)
        {
            Debug.LogError("GameManager.PlayChart: 未预创建音符，请先调用LoadAndParseChart！");
            return;
        }
        if (ChartData.Instance.commands.Count == 0)
        {
            Debug.LogError("GameManager.PlayChart: 谱面无指令数据，请先解析谱面！");
            return;
        }
        // 防止重复调用
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
        // 前置校验
        if (chartRunner == null || !chartRunner.IsNotesPreCreated)
        {
            Debug.LogError("GameManager.TogglePlay: 未加载谱面或未创建音符，无法暂停/恢复！");
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
    /// 停止播放（重置状态+可选清理音符）
    /// </summary>
    public void StopChart()
    {
        // 仅重置全局播放状态
        IsPlaying = false;
        pauseAccumulator = 0;
        lastPauseTime = Time.time;
        pausedPlayTime = 0;

        Debug.Log("GameManager: 谱面已停止，播放状态已重置");
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