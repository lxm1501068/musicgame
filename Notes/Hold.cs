using UnityEngine;
using System.Collections.Generic;

// Hold音符核心逻辑类（挂载到Hold音符预制体）
[RequireComponent(typeof(NoteData))]
public class Hold : MonoBehaviour
{
    #region 组件引用
    private NoteData noteData;                          // 关联的音符数据
    private SpriteRenderer leftSemiCircle;              // 左半圆
    private SpriteRenderer rectanglePart;               // 中间矩形（可变长度）
    private SpriteRenderer rightSemiCircle;             // 右半圆
    private Transform rectangleTransform;               // 矩形部分的Transform（用于缩放）
    private ShiftCommand shiftCommand;                  // 移动指令
    private DropToCommand dropToCommand;                // 长按判定指令
    private MoveCommand moveCommand;                    // 帧动画移动指令
    #endregion

    #region Hold特有配置
    [Header("Hold判定配置")]
    [Tooltip("长按判定间隔（秒），用于检测持续按住")]
    public float holdCheckInterval = 0.05f;
    [Tooltip("长按判定容错（秒），允许短暂松开的时间")]
    public float holdTolerance = 0.1f;
    [Tooltip("Hold完成判定阈值（秒）")]
    public float holdCompleteThreshold = 0.1f;

    [Header("Hold视觉配置 - 半圆（左右共用）")]
    [Tooltip("半圆默认精灵（左右共用）")]
    public Sprite semiCircleDefault;
    [Tooltip("半圆Perfect判定精灵（左右共用）")]
    public Sprite semiCirclePerfect;
    [Tooltip("半圆Good判定精灵（左右共用）")]
    public Sprite semiCircleGood;
    [Tooltip("半圆Bad判定精灵（左右共用）")]
    public Sprite semiCircleBad;
    [Tooltip("半圆Miss判定精灵（左右共用）")]
    public Sprite semiCircleMiss;

    [Header("Hold视觉配置 - 矩形部分")]
    [Tooltip("矩形默认精灵")]
    public Sprite rectangleDefault;
    [Tooltip("矩形Perfect判定精灵")]
    public Sprite rectanglePerfect;
    [Tooltip("矩形Good判定精灵")]
    public Sprite rectangleGood;
    [Tooltip("矩形Bad判定精灵")]
    public Sprite rectangleBad;
    [Tooltip("矩形Miss判定精灵")]
    public Sprite rectangleMiss;
    [Tooltip("胶囊形矩形部分的初始长度")]
    public float initialRectangleLength = 2.0f;

    // Hold状态跟踪
    private bool isHoldStarted = false;      // 是否开始长按
    private bool isHoldCompleted = false;    // 是否完成长按
    private float lastPressTime = 0;         // 最后一次检测到按键按下的时间
    private float holdStartTime = 0;         // 长按开始时间
    private float currentRectangleLength;    // 当前矩形长度
    private float holdDuration = 0f;         // 长按持续时间
    private bool isJudgmentSwitched = false; // 是否已切换判定精灵
    
    // 新增：指令初始化标记（参考Tap.cs）
    private bool isCommandsInitialized = false;
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

        // 获取或创建三个胶囊形子部件（左半圆、中间矩形、右半圆）
        InitializeCapsuleComponents();

        // 初始化矩形长度
        currentRectangleLength = initialRectangleLength;

        // 移除Awake中的InitCommands调用 → 延迟到CurrentPlayTime≠-1后执行
    }

    private void Update()
    {
        // 1. 基础校验：GameManager未初始化则直接返回（参考Tap.cs）
        if (GameManager.Instance == null) return;
        
        // 2. 核心检测：谱面未加载解析完成（CurrentPlayTime=-1）则返回，不执行任何逻辑
        float currentPlayTime = GameManager.Instance.CurrentPlayTime;
        if (currentPlayTime == -1)
        {
            return;
        }

        // 3. 指令初始化：仅在首次检测到谱面加载完成后执行一次（参考Tap.cs）
        if (!isCommandsInitialized)
        {
            InitCommands();
            // 初始化完成后设置初始位置（从NoteData读取）
            transform.position = new Vector2(noteData.x, noteData.y);
            isCommandsInitialized = true;
            Debug.Log($"[{gameObject.name}] Hold组件：谱面加载完成，指令初始化完成");
            return; // 初始化完成后先返回，避免当前帧重复执行后续逻辑
        }

        // 4. 基础校验：音符不可见则返回
        if (noteData == null || !noteData.isVisible) return;

        // 5. 执行原有逻辑（仅在谱面加载+指令初始化完成后执行）
        // 更新音符位置（根据指令类型）
        UpdateNotePosition(currentPlayTime, Time.deltaTime);

        // 执行Hold判定逻辑
        UpdateHoldJudge(currentPlayTime);
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
    /// 从NoteData的指令列表初始化对应指令（延迟执行版）
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
                    // 修正：参考Tap.cs使用cmd.key_name而非noteData.KeyIndex
                    dropToCommand = new DropToCommand(noteData, cmd, cmd.key_name);
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
    /// 根据指令类型更新音符位置（仅在谱面加载完成后执行）
    /// </summary>
    /// <param name="currentTime">当前音乐时间（GameManager.CurrentPlayTime）</param>
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
    /// （仅在谱面加载完成后执行）
    /// </summary>
    /// <param name="currentTime">当前音乐时间（GameManager.CurrentPlayTime）</param>
    private void UpdateHoldJudge(float currentTime)
    {
        if (dropToCommand == null) return;

        // 第一步：初始按下判定（Tap逻辑）
        if (!isHoldStarted)
        {
            // 修正：参考Tap.cs使用cmd.key_name而非noteData.KeyIndex
            dropToCommand.Judge(currentTime, noteData.commands[0].key_name);
            var judgeResult = dropToCommand.judgeResult;

            // 仅当按下判定为有效（Perfect/Good/Bad）时，启动长按检测
            if (judgeResult != JudgeResult.None && judgeResult != JudgeResult.Miss)
            {
                isHoldStarted = true;
                holdStartTime = currentTime;
                lastPressTime = currentTime;
                
                // 切换初始判定对应的精灵
                SwitchJudgeSprite(judgeResult);
                
                Debug.Log($"Hold音符{noteData.NoteIndex}开始长按，判定结果：{judgeResult}");
            }
            // 未按下且超时 → Miss
            else if (judgeResult == JudgeResult.Miss)
            {
                SwitchJudgeSprite(JudgeResult.Miss);
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
            bool isKeyPressed = InputManager.Instance.IsGroupPressed(noteData.commands[0].key_name);
            holdDuration = currentTime - holdStartTime;
            float expectedHoldEndTime = dropToCommand.endTime + (noteData.commands[0].timeB - noteData.commands[0].timeA);
            float totalHoldTime = expectedHoldEndTime - holdStartTime;

            // 更新最后按下时间
            if (isKeyPressed)
            {
                lastPressTime = currentTime;
            }

            // 如果非Miss判定，持续缩短矩形长度（基于已长按的进度）
            if (dropToCommand.judgeResult != JudgeResult.Miss && totalHoldTime > 0)
            {
                float holdProgress = Mathf.Clamp01(holdDuration / totalHoldTime);
                currentRectangleLength = Mathf.Lerp(initialRectangleLength, 0, holdProgress);
                UpdateCapsuleScale(currentRectangleLength);
            }

            // 检测是否松开超时（超过容错时间）
            if (currentTime - lastPressTime > holdTolerance)
            {
                // 长按中断 → 判定为Bad/Miss（根据已按住时长）
                dropToCommand.judgeResult = holdDuration < (expectedHoldEndTime - dropToCommand.endTime) * 0.5f 
                    ? JudgeResult.Miss 
                    : JudgeResult.Bad;
                
                // 切换最终判定精灵
                SwitchJudgeSprite(dropToCommand.judgeResult);
                
                isHoldCompleted = true;
                noteData.isVisible = false;
                gameObject.SetActive(false);
                Debug.Log($"Hold音符{noteData.NoteIndex}长按中断 → {dropToCommand.judgeResult}");
                return;
            }

            // 检测是否到达长按结束时间
            if (currentTime >= expectedHoldEndTime - holdCompleteThreshold)
            {
                // 长按完成 → 保留初始判定结果，矩形长度缩为0
                currentRectangleLength = 0;
                UpdateCapsuleScale(currentRectangleLength);
                
                isHoldCompleted = true;
                noteData.isVisible = false;
                gameObject.SetActive(false);
                Debug.Log($"Hold音符{noteData.NoteIndex}长按完成 → 最终判定：{dropToCommand.judgeResult}");
            }
        }
    }
    #endregion

    #region 视觉效果处理
    /// <summary>
    /// 根据判定结果切换三个部件的精灵
    /// </summary>
    /// <param name="judgeResult">判定结果</param>
    private void SwitchJudgeSprite(JudgeResult judgeResult)
    {
        if (isJudgmentSwitched) return;
        
        // 获取共用的半圆精灵
        Sprite semiSprite = judgeResult switch
        {
            JudgeResult.Perfect => semiCirclePerfect,
            JudgeResult.Good => semiCircleGood,
            JudgeResult.Bad => semiCircleBad,
            JudgeResult.Miss => semiCircleMiss,
            _ => semiCircleDefault
        };

        // 左右半圆使用相同精灵
        leftSemiCircle.sprite = semiSprite;
        rightSemiCircle.sprite = semiSprite;

        // 矩形部分精灵切换
        rectanglePart.sprite = judgeResult switch
        {
            JudgeResult.Perfect => rectanglePerfect,
            JudgeResult.Good => rectangleGood,
            JudgeResult.Bad => rectangleBad,
            JudgeResult.Miss => rectangleMiss,
            _ => rectangleDefault
        };

        // 校验精灵是否赋值
        if (leftSemiCircle.sprite == null)
            Debug.LogWarning($"Hold音符{noteData.NoteIndex} 半圆 {judgeResult} 精灵未赋值！");
        if (rectanglePart.sprite == null)
            Debug.LogWarning($"Hold音符{noteData.NoteIndex} 矩形 {judgeResult} 精灵未赋值！");

        isJudgmentSwitched = true;
        Debug.Log($"Hold音符{noteData.NoteIndex}切换精灵为：{judgeResult}");
    }

    /// <summary>
    /// 初始化胶囊形的三个子部件（左半圆、中间矩形、右半圆）
    /// 并赋值默认精灵
    /// </summary>
    private void InitializeCapsuleComponents()
    {
        // 尝试查找已有的子对象
        Transform leftChild = transform.Find("LeftSemiCircle");
        Transform rectChild = transform.Find("Rectangle");
        Transform rightChild = transform.Find("RightSemiCircle");

        // 获取或创建左半圆
        if (leftChild == null)
        {
            GameObject leftObj = new GameObject("LeftSemiCircle");
            leftObj.transform.SetParent(transform, false);
            leftChild = leftObj.transform;
        }
        leftSemiCircle = leftChild.GetComponent<SpriteRenderer>();
        if (leftSemiCircle == null)
        {
            leftSemiCircle = leftChild.gameObject.AddComponent<SpriteRenderer>();
        }
        // 赋值左半圆默认精灵（共用）
        if (semiCircleDefault != null)
            leftSemiCircle.sprite = semiCircleDefault;
        else
            Debug.LogWarning($"Hold音符{noteData.NoteIndex} 半圆默认精灵未赋值！");

        // 获取或创建中间矩形
        if (rectChild == null)
        {
            GameObject rectObj = new GameObject("Rectangle");
            rectObj.transform.SetParent(transform, false);
            rectChild = rectObj.transform;
        }
        rectanglePart = rectChild.GetComponent<SpriteRenderer>();
        if (rectanglePart == null)
        {
            rectanglePart = rectChild.gameObject.AddComponent<SpriteRenderer>();
        }
        rectangleTransform = rectChild;
        // 赋值矩形默认精灵
        if (rectangleDefault != null)
            rectanglePart.sprite = rectangleDefault;
        else
            Debug.LogWarning($"Hold音符{noteData.NoteIndex} 矩形默认精灵未赋值！");

        // 获取或创建右半圆
        if (rightChild == null)
        {
            GameObject rightObj = new GameObject("RightSemiCircle");
            rightObj.transform.SetParent(transform, false);
            rightChild = rightObj.transform;
        }
        rightSemiCircle = rightChild.GetComponent<SpriteRenderer>();
        if (rightSemiCircle == null)
        {
            rightSemiCircle = rightChild.gameObject.AddComponent<SpriteRenderer>();
        }
        // 赋值右半圆默认精灵（共用）
        if (semiCircleDefault != null)
            rightSemiCircle.sprite = semiCircleDefault;
        else
            Debug.LogWarning($"Hold音符{noteData.NoteIndex} 半圆默认精灵未赋值！");

        // 初始化矩形缩放
        UpdateCapsuleScale(initialRectangleLength);

        Debug.Log($"Hold音符{noteData.NoteIndex}胶囊形子部件初始化完成");
    }

    /// <summary>
    /// 根据矩形长度更新胶囊形显示（只缩放矩形部分，保持两端半圆不变）
    /// </summary>
    /// <param name="rectangleLength">矩形部分的当前长度</param>
    private void UpdateCapsuleScale(float rectangleLength)
    {
        if (rectangleTransform == null) return;

        // 只缩放矩形部分的X轴，保持半圆不变
        float scaleRatio = initialRectangleLength > 0 ? rectangleLength / initialRectangleLength : 0;
        
        // 只改变矩形部分的X缩放，Y保持1
        rectangleTransform.localScale = new Vector3(scaleRatio, 1f, 1f);
        
        Debug.Log($"Hold音符{noteData.NoteIndex}矩形长度更新为：{rectangleLength:F3}，缩放比例：{scaleRatio:F3}");
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