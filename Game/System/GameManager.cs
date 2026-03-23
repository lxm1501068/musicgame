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

    // +++ BGM 新增：背景音乐播放器
    [Header("BGM 设置")]
    public AudioSource bgmAudioSource; // 用于播放背景音乐（需在编辑器拖拽赋值）

    // 新增：谱面加载解析完成标志位
    public bool IsChartLoadedAndParsed;

    // 播放状态和时间信息
    public bool IsPlaying;// 播放状态（只读，外部仅能通过方法修改）
    public float chartStartTime;
    // 供音符访问的「精准播放时间」（排除暂停）
    public float CurrentPlayTime
    {
        get
        {
            // 核心修改：加载解析未完成时返回 -1
            if (!IsChartLoadedAndParsed)
            {
                return -1;
            }

            if (!IsPlaying)
            {
                // 修复：暂停时返回「暂停瞬间的播放时间」，而非动态计算
                return pausedPlayTime;
            }
            // 播放中：当前时间 - 起始时间 - 累计暂停时长
            return Time.time - chartStartTime - pauseAccumulator;
        }
    }
    public float debugCurrentPlayTime; // 可选：用于在Unity编辑器中实时显示当前播放时间（仅供调试）
    private float pauseAccumulator = 0; // 暂停时长累计（抵消暂停的时间差）
    private float lastPauseTime = 0; // 上次暂停的时间
    private float pausedPlayTime = 0; // 新增：记录暂停瞬间的播放时间

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

    #region 谱面加载&解析（异步版核心逻辑）
    /// <summary>
    /// 加载并解析谱面（异步版）
    /// </summary>
    /// <param name="fileName">谱面文件名</param>
    /// <param name="autoPlay">解析完成后是否自动开始播放</param>
    public async void LoadAndParseChart(string fileName, bool autoPlay = true)
    {
        // +++ BGM 新增：加载新谱面前，确保停止任何正在播放的谱面和 BGM
        StopChart();

        // 开始加载时标记为未完成
        IsChartLoadedAndParsed = false;

        // 前置校验
        if (chartLoader == null)
        {
            Debug.LogError("GameManager.LoadAndParseChart: LoadChart引用为空！");
            return;
        }
        
        // 清空旧谱面数据
        ClearChartData();
        Debug.Log($"清空旧谱面数据");
        
        // 异步等待谱面加载完成（替代原Invoke的简易延迟）
        await chartLoader.LoadChartFileAsync(fileName);
        
        // 加载完成后执行解析
        ParseLoadedChart();
        
        // 校验解析是否成功（未成功则不播放）
        if (!IsChartLoadedAndParsed)
        {
            Debug.LogError("GameManager.LoadAndParseChart: 谱面解析/音符创建失败，无法播放！");
            return;
        }
        
        // 确保加载→解析→音符创建全完成后，再根据autoPlay决定是否开始播放
        Debug.Log($"GameManager: 谱面加载&解析完成");
        
        if (autoPlay)
        {
            PlayChart();
        }
    }

    /// <summary>
    /// 解析已加载的谱面内容（逻辑不变，保留状态标记）
    /// </summary>
    private void ParseLoadedChart()
    {
        if (string.IsNullOrEmpty(chartLoader.ChartContent))
        {
            Debug.LogError("GameManager.ParseLoadedChart: 谱面内容为空，解析失败！");
            return; // 解析失败，保持IsChartLoadedAndParsed=false
        }
        
        // 预创建所有音符
        PreCreateAllNotes();

        // 加载解析+音符创建完成后标记为已完成
        IsChartLoadedAndParsed = true;
        
        Debug.Log("GameManager: 谱面加载→解析→音符创建 全流程完成！");
    }

    /// <summary>
    /// 清空谱面数据（切换谱面时调用）
    /// </summary>
    public void ClearChartData()
    {
        // 核心修改：清空数据时标记为未完成
        IsChartLoadedAndParsed = false;

        // 清空ChartData的核心数据
        ChartData.Instance.ResetChartData();
        
        // 清空LoadChart的缓存
        ChartData.Instance.ClearChartContent();
        
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

        // 核心修改：确保只有在加载解析完成后才允许开始播放
        if (!IsChartLoadedAndParsed)
        {
            Debug.LogError("GameManager.PlayChart: 谱面加载/解析未完成，无法开始播放！");
            return;
        }

        // 重置播放状态
        IsPlaying = true;
        chartStartTime = Time.time;
        pauseAccumulator = 0; // 清空暂停累计时长
        pausedPlayTime = 0;   // 重置暂停时的播放时间

        // +++ BGM 新增：开始播放 BGM
        if (bgmAudioSource != null && !bgmAudioSource.isPlaying)
        {
            // 确保音频已准备就绪
            if (bgmAudioSource.clip != null)
            {
                bgmAudioSource.Play();
            }
            else
            {
                Debug.LogWarning("GameManager.PlayChart: BGM AudioSource 没有赋值 Clip，将不播放音乐");
            }
        }

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

            // +++ BGM 新增：恢复 BGM 播放
            if (bgmAudioSource != null)
            {
                bgmAudioSource.UnPause();
            }

            // 恢复播放时隐藏恢复按钮
            if (RecoverButton.Instance != null)
            {
                RecoverButton.Instance.SetButtonVisible(false);
            }
        }
        else
        {
            // 暂停播放：记录暂停瞬间的绝对时间 + 播放时间
            lastPauseTime = Time.time;
            pausedPlayTime = CurrentPlayTime; // 关键：保存暂停时的播放时间
            Debug.Log($"GameManager: 谱面已暂停 | 暂停时播放时间：{pausedPlayTime:F2}秒");

            // +++ BGM 新增：暂停 BGM
            if (bgmAudioSource != null)
            {
                bgmAudioSource.Pause();
            }

            // 暂停时显示恢复按钮
            if (RecoverButton.Instance != null)
            {
                RecoverButton.Instance.SetButtonVisible(true);
            }
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

        // +++ BGM 新增：停止 BGM 播放
        if (bgmAudioSource != null)
        {
            bgmAudioSource.Stop();
        }

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
    
    private void Update()
    {
        debugCurrentPlayTime = CurrentPlayTime;
        if(CurrentPlayTime == -1) return; // 加载解析未完成时跳过输入检测
        
        // 检测Esc键按下（GetKeyDown确保仅触发一次）+ 当前处于播放状态
        if (Input.GetKeyDown(KeyCode.Escape) && IsPlaying)
        {
            TogglePlay(); // 执行暂停/恢复逻辑
            Debug.Log("GameManager: 检测到Esc键按下，执行TogglePlay()");
        }
    }
}