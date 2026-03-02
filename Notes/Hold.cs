using UnityEngine;
using System.Collections.Generic;

// Hold音符核心逻辑类（挂载到Hold音符预制体）
[RequireComponent(typeof(NoteData))]
public class Hold : MonoBehaviour
{
    #region 组件引用
    private NoteData noteData;               // 关联的音符数据
    private ShiftCommand shiftCommand;       // 移动指令（可选）
    private DropToCommand dropToCommand;     // 长按判定指令
    private MoveCommand moveCommand;         // 帧动画移动指令（可选）
    #endregion

    #region Hold特有配置
    [Header("Hold判定配置")]
    [Tooltip("长按判定间隔（秒），用于检测持续按住")]
    public float holdCheckInterval = 0.05f;
    [Tooltip("长按判定容错（秒），允许短暂松开的时间")]
    public float holdTolerance = 0.1f;
    [Tooltip("Hold完成判定阈值（秒）")]
    public float holdCompleteThreshold = 0.1f;

    // Hold状态跟踪
    private bool isHoldStarted = false;      // 是否开始长按
    private bool isHoldCompleted = false;    // 是否完成长按
    private float lastPressTime = 0;         // 最后一次检测到按键按下的时间
    private float holdStartTime = 0;         // 长按开始时间
    #endregion

    #region 生命周期
    private void Awake()
    {
        // 获取NoteData组件（必须挂载）
        noteData = GetComponent<NoteData>();
        if (noteData == null)
        {
            Debug.LogError($"Hold音符{gameObject.name}缺少NoteData组件！");
            enabled = false;
            return;
        }

        // 初始化指令（从NoteData的指令列表解析）
        InitCommands();
    }

    private void Update()
    {
        if (noteData == null || !noteData.isVisible) return;

        // 1. 更新音符位置（根据指令类型）
        UpdateNotePosition(Time.time, Time.deltaTime);

        // 2. 执行Hold判定逻辑
        UpdateHoldJudge(Time.time);
    }

    private void OnDestroy()
    {
        // 清理指令引用
        shiftCommand = null;
        dropToCommand = null;
        moveCommand = null;
    }
    #endregion

    #region 指令初始化
    /// <summary>
    /// 从NoteData的指令列表初始化对应指令
    /// </summary>
    private void InitCommands()
    {
        if (noteData.commands == null || noteData.commands.Count == 0)
        {
            Debug.LogWarning($"Hold音符{noteData.NoteIndex}无指令数据！");
            return;
        }

        // 遍历指令列表（Hold通常包含DropTo+Shift/Move）
        foreach (var cmd in noteData.commands)
        {
            switch (cmd.commandName)
            {
                case "Shift":
                    shiftCommand = new ShiftCommand(noteData, cmd);
                    break;
                case "DropTo":
                    dropToCommand = new DropToCommand(noteData, cmd, noteData.KeyIndex);
                    // 覆盖DropTo的判定阈值（适配Hold）
                    dropToCommand.perfectThreshold = 0.15f;
                    dropToCommand.goodThreshold = 0.25f;
                    dropToCommand.badThreshold = 0.35f;
                    break;
                case "Move":
                    if (!string.IsNullOrEmpty(cmd.filename))
                    {
                        moveCommand = new MoveCommand(noteData, cmd.filename);
                    }
                    break;
            }
        }

        // 校验核心指令
        if (dropToCommand == null)
        {
            Debug.LogError($"Hold音符{noteData.NoteIndex}缺少DropTo指令（必须）！");
        }
    }
    #endregion

    #region 位置更新
    /// <summary>
    /// 根据指令类型更新音符位置
    /// </summary>
    /// <param name="currentTime">当前音乐时间</param>
    /// <param name="deltaTime">帧间隔时间</param>
    private void UpdateNotePosition(float currentTime, float deltaTime)
    {
        // 优先级：Move指令 > Shift指令
        if (moveCommand != null)
        {
            moveCommand.UpdateNotePosition(currentTime);
        }
        else if (shiftCommand != null)
        {
            shiftCommand.UpdateNotePosition(currentTime, deltaTime);
        }

        // 同步位置到Transform
        transform.position = new Vector3(noteData.x, noteData.y, transform.position.z);
    }
    #endregion

    #region Hold判定核心逻辑
    /// <summary>
    /// 处理Hold音符的完整判定流程：按下判定 → 持续按住检测 → 松开/完成判定
    /// </summary>
    /// <param name="currentTime">当前音乐时间</param>
    private void UpdateHoldJudge(float currentTime)
    {
        if (dropToCommand == null) return;

        // 第一步：初始按下判定（Tap逻辑）
        if (!isHoldStarted)
        {
            dropToCommand.Judge(currentTime, noteData.KeyIndex);
            var judgeResult = dropToCommand.judgeResult;

            // 仅当按下判定为有效（Perfect/Good/Bad）时，启动长按检测
            if (judgeResult != JudgeResult.None && judgeResult != JudgeResult.Miss)
            {
                isHoldStarted = true;
                holdStartTime = currentTime;
                lastPressTime = currentTime;
                Debug.Log($"Hold音符{noteData.NoteIndex}开始长按，判定结果：{judgeResult}");
            }
            // 未按下且超时 → Miss
            else if (judgeResult == JudgeResult.Miss)
            {
                noteData.isVisible = false;
                gameObject.SetActive(false);
                Debug.Log($"Hold音符{noteData.NoteIndex}初始按下超时 → Miss");
            }
            return;
        }

        // 第二步：持续按住检测（已开始长按）
        if (isHoldStarted && !isHoldCompleted)
        {
            // 检测按键状态
            bool isKeyPressed = InputManager.Instance.IsGroupPressed(noteData.KeyIndex);
            float holdDuration = currentTime - holdStartTime;
            float expectedHoldEndTime = dropToCommand.endTime + (noteData.commands[0].timeB - noteData.commands[0].timeA);

            // 更新最后按下时间
            if (isKeyPressed)
            {
                lastPressTime = currentTime;
            }

            // 检测是否松开超时（超过容错时间）
            if (currentTime - lastPressTime > holdTolerance)
            {
                // 长按中断 → 判定为Bad/Miss（根据已按住时长）
                dropToCommand.judgeResult = holdDuration < (expectedHoldEndTime - dropToCommand.endTime) * 0.5f 
                    ? JudgeResult.Miss 
                    : JudgeResult.Bad;
                isHoldCompleted = true;
                noteData.isVisible = false;
                gameObject.SetActive(false);
                Debug.Log($"Hold音符{noteData.NoteIndex}长按中断 → {dropToCommand.judgeResult}");
                return;
            }

            // 检测是否到达长按结束时间
            if (currentTime >= expectedHoldEndTime - holdCompleteThreshold)
            {
                // 长按完成 → 保留初始判定结果
                isHoldCompleted = true;
                noteData.isVisible = false;
                gameObject.SetActive(false);
                Debug.Log($"Hold音符{noteData.NoteIndex}长按完成 → 最终判定：{dropToCommand.judgeResult}");
            }
        }
    }
    #endregion

    #region 外部接口
    /// <summary>
    /// 获取Hold判定结果（供计分系统调用）
    /// </summary>
    /// <returns>最终判定结果</returns>
    public JudgeResult GetHoldJudgeResult()
    {
        return dropToCommand?.judgeResult ?? JudgeResult.None;
    }

    /// <summary>
    /// 强制终止Hold判定（如暂停/重玩）
    /// </summary>
    public void ForceEndHold()
    {
        isHoldCompleted = true;
        noteData.isVisible = false;
        gameObject.SetActive(false);
    }
    #endregion
}