using UnityEngine;

public class GameManager : MonoBehaviour
{
    public string filename;          // 谱面文件名（如chart1.txt）
    // 单例实例（全局唯一访问点）
    public static GameManager Instance;

    // 谱面开始时间（单位：秒，可在Inspector面板调整）
    [Tooltip("谱面开始的基准时间（单位：秒）")]
    public float chartStartTime;

    // 游戏播放状态标记（true=播放中，false=暂停/停止）
    [Tooltip("标记当前游戏/谱面是否处于播放状态")]
    public bool IsPlaying;

    // 可选：引用场景中的ChartRunner，方便一键调用
    public ChartRunner chartRunner;

    public LoadChart loadChart; // 引用LoadChart组件，自动加载谱面数据

    // 新增：防止ESC键短时间重复触发的状态锁
    private bool isProcessingInput = false;

    // 新增：暂停时的时间缩放值（0=完全暂停，1=正常，可在Inspector调整）
    [Tooltip("暂停时的时间缩放值（0=完全暂停，1=正常）")]
    public float pauseTimeScale = 0f;

    // 单例初始化
    private void Awake()
    {
        // 确保全局唯一实例
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 跨场景保留GameManager
        }
        else
        {
            Destroy(gameObject); // 销毁重复的实例
        }

        // 变量默认值初始化
        chartStartTime = 0f;
        IsPlaying = false;
        
        // 初始化时间缩放为正常状态
        Time.timeScale = 1f;
        StartCoroutine(loadChart.LoadChartFile(filename));// 自动加载谱面
    }

    // 新增：每帧检测ESC键输入
    private void Update()
    {
        // 检测ESC键按下 + 避免重复触发
        if (Input.GetKeyDown(KeyCode.Escape) && !isProcessingInput)
        {
            ToggleGamePlay(); // 调用原有暂停/继续逻辑
            LockInputTemporarily(); // 锁定输入防止重复触发
        }
    }

    // ========== 原有核心方法（保留并优化） ==========
    /// <summary>
    /// 启动游戏/谱面播放
    /// </summary>
    public void StartGame()
    {
        // 1. 初始化游戏状态
        IsPlaying = true;
        Time.timeScale = 1f; // 确保启动时时间缩放正常
        chartStartTime = Time.time; // 记录当前时间作为谱面开始时间
        Debug.Log($"游戏已启动 | 谱面开始时间：{chartStartTime} | 播放状态：{IsPlaying}");

        // 2. 自动调用ChartRunner的播放逻辑（如果绑定）
        if (chartRunner != null)
        {
            chartRunner.PlayChart();
        }
        else
        {
            Debug.LogWarning("GameManager中未绑定ChartRunner，需手动调用PlayChart()");
        }
    }

    /// <summary>
    /// 停止游戏/谱面播放
    /// </summary>
    public void StopGame()
    {
        IsPlaying = false;
        Time.timeScale = 1f; // 停止后恢复时间缩放（可选，根据需求调整）
        if (chartRunner != null)
        {
            chartRunner.StopChart();
        }
        Debug.Log("游戏已停止");
    }

    /// <summary>
    /// 暂停/继续游戏（核心切换逻辑）
    /// </summary>
    public void ToggleGamePlay()
    {
        IsPlaying = !IsPlaying;
        
        // 同步时间缩放：暂停时设为0，继续时恢复1
        Time.timeScale = IsPlaying ? 1f : pauseTimeScale;
        
        // 同步ChartRunner的播放状态
        if (chartRunner != null)
        {
            chartRunner.TogglePlay();
        }
        
        Debug.Log($"游戏状态切换：{(IsPlaying ? "播放中" : "已暂停")} | 时间缩放：{Time.timeScale}");
    }

    // ========== 新增：输入防重复触发逻辑 ==========
    /// <summary>
    /// 临时锁定输入（防止ESC键短时间重复触发）
    /// </summary>
    private void LockInputTemporarily()
    {
        isProcessingInput = true;
        // 0.1秒后解锁（可根据需求调整间隔）
        Invoke(nameof(UnlockInput), 0.1f);
    }

    /// <summary>
    /// 解锁输入
    /// </summary>
    private void UnlockInput()
    {
        isProcessingInput = false;
    }

    // ========== 新增：场景销毁时恢复时间缩放 ==========
    /// <summary>
    /// 防止切换场景后时间缩放残留为0
    /// </summary>
    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}