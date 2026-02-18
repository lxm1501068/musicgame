using System.Collections.Generic;
using UnityEngine;

public class ChartRunner : MonoBehaviour
{
    #region 公共配置字段（Unity编辑器赋值）
    [Header("音符预制体")]
    public GameObject tapPrefab;       // Tap音符预制体
    public GameObject dtapPrefab;      // Dtap音符预制体
    public GameObject holdPrefab;      // Hold音符预制体
    public GameObject flickPrefab;     // Flick音符预制体
    public GameObject dragPrefab;      // Drag音符预制体
    public GameObject keyPrefab;       // Key按键预制体

    [Header("层级管理")]
    public Transform noteParent;       // 所有音符的父物体（整理层级）
    #endregion

    #region 私有运行时字段
    private List<GameObject> allNotes = new List<GameObject>(); // 预创建的所有音符
    // 对外暴露“是否已预创建音符”（供GameManager校验）
    public bool IsNotesPreCreated { get; private set; } = false;
    #endregion

    #region 仅保留：音符管理相关方法（播放控制已迁移到GameManager）
    /// <summary>
    /// 预创建所有音符（供GameManager调用）
    /// </summary>
    public void PreCreateAllNotes()
    {
        if (ChartData.Instance == null)
        {
            Debug.LogError("ChartRunner.PreCreateAllNotes: 谱面数据未加载！");
            return;
        }
        if (IsNotesPreCreated)
        {
            Debug.LogWarning("ChartRunner.PreCreateAllNotes: 音符已预创建，跳过！");
            return;
        }

        // 确保指令按时间排序
        ChartData.Instance.SortCommandsByTime();

        // 遍历所有指令，创建音符并初始化
        foreach (Command cmd in ChartData.Instance.commands)
        {
            if (cmd == null || !cmd.isNoteFirstTimeOccured) continue; // 仅处理首次出现的音符

            // 选择预制体
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

            if (notePrefab == null)
            {
                Debug.LogError($"音符ID:{cmd.num} 类型{cmd.type} 预制体未赋值！");
                continue;
            }

            // 创建音符实例（初始隐藏）
            GameObject noteObj = Instantiate(notePrefab, noteParent);
            NoteBehaviour noteBehaviour = noteObj.GetComponent<NoteBehaviour>();

            // 初始化音符：传递指令数据
            noteBehaviour.Init(cmd);
            allNotes.Add(noteObj);

            Debug.Log($"ChartRunner: 预创建音符 ID:{cmd.num} 类型:{cmd.type} 触发时间:{cmd.timeA}");
        }

        IsNotesPreCreated = true;
        Debug.Log($"ChartRunner: 所有音符预创建完成，共{allNotes.Count}个");
    }

    /// <summary>
    /// 清理所有预创建的音符（供GameManager调用）
    /// </summary>
    public void CleanAllNotes()
    {
        // 销毁所有音符对象
        foreach (GameObject note in allNotes)
        {
            if (note != null) Destroy(note);
        }
        // 重置状态
        allNotes.Clear();
        IsNotesPreCreated = false;
        Debug.Log("ChartRunner: 所有音符已清理");
    }
    #endregion
}