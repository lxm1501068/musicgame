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
            // 1. 获取/创建NoteData组件（确保音符对象挂载NoteData）
            NoteData noteData = noteObj.GetComponent<NoteData>();
            if (noteData == null)
            {
                noteData = noteObj.AddComponent<NoteData>();
            }

            // 2. 初始化NoteData数据（从Command映射到NoteData字段）
            InitNoteData(noteData, cmd);

            // 3. （可选）如果音符需要绑定指令，初始化指令列表
            if (noteData.commands == null)
            {
                noteData.commands = new List<Command>();
            }
            noteData.commands.Add(cmd);

            allNotes.Add(noteObj);
            noteObj.SetActive(false); // 初始隐藏，待游戏运行时显示

            Debug.Log($"ChartRunner: 预创建音符 ID:{cmd.num} 类型:{cmd.type} 触发时间:{cmd.timeA}");
        }

        IsNotesPreCreated = true;
        Debug.Log($"ChartRunner: 所有音符预创建完成，共{allNotes.Count}个");
    }

    /// <summary>
    /// 初始化NoteData数据（从Command映射字段）
    /// </summary>
    /// <param name="noteData">要初始化的NoteData</param>
    /// <param name="cmd">谱面指令数据</param>
    private void InitNoteData(NoteData noteData, Command cmd)
    {
        // 映射NoteTools.cs中NoteData的核心字段
        noteData.NoteIndex = cmd.num;          // 音符序号（对应cmd.num）
        noteData.KeyIndex = cmd.keyIndex;      // 键序号（需确保Command有keyIndex字段）
        noteData.x = cmd.x1;                   // 初始X坐标（从cmd的x1读取）
        noteData.y = cmd.y1;                   // 初始Y坐标（从cmd的y1读取）
        noteData.isVisible = false;            // 初始隐藏，待时机显示
    }
    #endregion
}