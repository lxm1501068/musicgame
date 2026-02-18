using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class ChartRunner : MonoBehaviour
{
    #region 单例核心（新增）
    // 全局唯一实例（公开只读，外部通过 ChartRunner.Instance 访问）
    public static ChartRunner Instance { get; private set; }
    #endregion

    #region 公共配置字段（Unity编辑器赋值）
    [Header("音符预制体")]
    public GameObject tapPrefab;       // Tap音符预制体
    public GameObject dtapPrefab;      // Dtap音符预制体
    public GameObject holdPrefab;      // Hold音符预制体
    public GameObject flickPrefab;     // Flick音符预制体
    public GameObject dragPrefab;      // Drag音符预制体
    public GameObject keyPrefab;       // Key按键预制体（$标记）

    [Header("层级管理")]
    public Transform noteParent;       // 所有音符的父物体（整理层级）
    #endregion

    #region 私有运行时字段
    private Coroutine playCoroutine;           // 播放协程引用
    private List<GameObject> createdNotes = new List<GameObject>(); // 已创建的音符列表
    private HashSet<int> createdNoteIds = new HashSet<int>();       // 已创建的音符ID（避免重复）
    private int currentCommandIndex = 0;       // 当前遍历到的指令索引（避免重复遍历）
    #endregion

    #region 单例初始化（新增）
    private void Awake()
    {
        // 单例核心逻辑：确保全局唯一实例
        if (Instance == null)
        {
            Instance = this;
            // 可选：跨场景保留（如果谱面播放跨场景，取消注释）
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            // 重复创建时销毁多余实例，避免逻辑冲突
            Destroy(gameObject);
        }
    }
    #endregion

    #region 公共核心API（供GameManager调用）

    /// <summary>
    /// 开始播放谱面
    /// </summary>
    public void PlayChart()
    {
        if (GameManager.Instance == null || ChartData.Instance == null)
        {
            Debug.LogError("ChartRunner.PlayChart: GameManager为空或谱面数据未加载！");
            return;
        }

        // 重置遍历索引（防止重复播放时索引残留）
        currentCommandIndex = 0;
        GameManager.Instance.IsPlaying = true;
        GameManager.Instance.chartStartTime = Time.time;
        playCoroutine = StartCoroutine(PlayChartCoroutine());
        Debug.Log("谱面开始播放！");
    }

    /// <summary>
    /// 暂停/恢复播放
    /// </summary>
    public void TogglePlay()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("ChartRunner.TogglePlay: GameManager为空！");
            return;
        }

        GameManager.Instance.IsPlaying = !GameManager.Instance.IsPlaying;
        if (GameManager.Instance.IsPlaying)
        {
            // 校准时间锚点
            GameManager.Instance.chartStartTime = Time.time - (Time.time - GameManager.Instance.chartStartTime);
            playCoroutine = StartCoroutine(PlayChartCoroutine());
            Debug.Log("谱面恢复播放");
        }
        else
        {
            if (playCoroutine != null)
            {
                StopCoroutine(playCoroutine);
                playCoroutine = null;
            }
            Debug.Log("谱面已暂停");
        }
    }

    /// <summary>
    /// 停止播放并清理
    /// </summary>
    public void StopChart()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("ChartRunner.StopChart: GameManager为空！");
            return;
        }

        GameManager.Instance.IsPlaying = false;
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        // 销毁所有音符（仅在主动停止时清理，协程内不自动销毁）
        foreach (GameObject note in createdNotes)
        {
            if (note != null) Destroy(note);
        }

        // 清空缓存
        createdNotes.Clear();
        createdNoteIds.Clear();
        currentCommandIndex = 0;

        Debug.Log("谱面已停止，所有数据已清理");
    }
    #endregion

    #region 私有解析逻辑

    /// <summary>
    /// 谱面播放协程（按时间轴解析，仅处理音符创建）
    /// </summary>
    private IEnumerator PlayChartCoroutine()
    {
        // 空值防护：确保谱面数据有效
        if (GameManager.Instance == null || ChartData.Instance == null)
        {
            Debug.LogError("PlayChartCoroutine: 谱面数据为空！");
            yield break;
        }

        // 确保指令已按时间排序
        ChartData.Instance.SortCommandsByTime();

        // 循环解析直到所有指令处理完成
        while (currentCommandIndex < ChartData.Instance.commands.Count)
        {
            // 暂停逻辑：如果暂停则等待直到恢复
            while (!GameManager.Instance.IsPlaying)
            {
                yield return null;
            }

            // 计算当前谱面播放时间（相对时间，排除暂停时长）
            float currentPlayTime = Time.time - GameManager.Instance.chartStartTime;

            // 处理待创建的音符（按时间轴到点创建）
            while (currentCommandIndex < ChartData.Instance.commands.Count)
            {
                Command cmd = ChartData.Instance.commands[currentCommandIndex];
                // 跳过空指令
                if (cmd == null)
                {
                    currentCommandIndex++;
                    continue;
                }

                // 未到创建时间则退出循环
                if (cmd.timeA > currentPlayTime)
                {
                    break;
                }

                // 核心修改：仅当 isNoteFirstTimeOccured 为 true 时，才尝试创建音符
                if (cmd.isNoteFirstTimeOccured)
                {
                    // 避免重复创建同一ID的音符
                    if (!createdNoteIds.Contains(cmd.num))
                    {
                        CreateNoteFromCommand(cmd);
                    }
                    else
                    {
                        Debug.Log($"跳过重复创建：音符ID:{cmd.num}（已创建过首次出现的实例）");
                    }
                }
                else
                {
                    Debug.Log($"跳过非首次音符：ID:{cmd.num} 类型:{cmd.type}（仅首次出现时创建）");
                }

                currentCommandIndex++;
            }

            // 每帧更新一次
            yield return null;
        }

        // 所有音符创建完成后标记播放结束（仅标记状态，不销毁音符）
        GameManager.Instance.IsPlaying = false;
        Debug.Log("谱面音符创建完成！");
    }

    /// <summary>
    /// 根据指令创建对应音符（无隐藏处理、无NoteBehaviour依赖）
    /// </summary>
    /// <param name="cmd">音符指令数据</param>
    private void CreateNoteFromCommand(Command cmd)
    {
        // 根据音符类型选择预制体
        GameObject notePrefab = cmd.type switch
        {
            NoteType.Tap => tapPrefab,
            NoteType.DTap => dtapPrefab,
            NoteType.Hold => holdPrefab,
            NoteType.Flick => flickPrefab,
            NoteType.Drag => dragPrefab,
            NoteType.Key => keyPrefab,
            _ => null
        };

        // 预制体为空则报错
        if (notePrefab == null)
        {
            Debug.LogError($"音符ID:{cmd.num} 类型{cmd.type} 对应的预制体未赋值！");
            return;
        }

        // 创建音符实例
        GameObject noteObj = Instantiate(notePrefab, noteParent);
        // 设置音符初始位置
        Vector2 notePos = new Vector2(cmd.x1, cmd.y1);
        noteObj.transform.localPosition = new Vector3(notePos.x, notePos.y, 0);

        // 添加到缓存列表
        createdNotes.Add(noteObj);
        createdNoteIds.Add(cmd.num);

        Debug.Log($"创建首次出现的音符 ID:{cmd.num} 类型:{cmd.type} 时间:{cmd.timeA} 位置:{notePos}");
    }
    #endregion
}