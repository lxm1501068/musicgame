using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(NoteData))]
public class Hold : MonoBehaviour
{
    #region 组件引用
    private NoteData noteData;
    private SpriteRenderer leftSemiCircle;
    private SpriteRenderer rectanglePart;
    private SpriteRenderer rightSemiCircle;
    private Transform rectangleTransform;
    private List<ShiftCommand> shiftCommands = new List<ShiftCommand>();
    private DropToCommand dropToCommand;
    private List<MoveCommand> moveCommands = new List<MoveCommand>();
    private float holdDuration;
    #endregion

    #region Hold特有配置
    [Header("Hold判定配置")]
    public float holdCheckInterval = 0.05f;
    public float holdTolerance = 0.1f;
    public float holdCompleteThreshold = 0.1f;

    [Header("Hold视觉配置 - 半圆（左右共用）")]
    public Sprite semiCircleDefault;
    public Sprite semiCirclePerfect;
    public Sprite semiCircleGood;
    public Sprite semiCircleBad;
    public Sprite semiCircleMiss;

    [Header("Hold视觉配置 - 矩形部分")]
    public Sprite rectangleDefault;
    public Sprite rectanglePerfect;
    public Sprite rectangleGood;
    public Sprite rectangleBad;
    public Sprite rectangleMiss;

    // 新增：胶囊方向与长度（由drop_to指令决定）
    private Vector2 holdDirection = Vector2.right;
    private float totalHoldLength = 2.0f;          // 初始总长度（起始点到终点距离）
    private float currentRectangleLength;           // 当前矩形长度（随时间缩短）

    private bool isHoldStarted = false;
    private bool isHoldCompleted = false;
    private float lastPressTime = 0;
    private float holdStartTime = 0;
    private bool isJudgmentSwitched = false;        // 若希望每次判定都更新精灵，可移除该标志
    
    private bool isCommandsInitialized = false;
    private float firstCommandStartTime;
    #endregion

    #region 生命周期
    private void Awake()
    {
        noteData = GetComponent<NoteData>();
        if (noteData == null)
        {
            Debug.LogError($"Hold音符{noteData.NoteIndex}缺少NoteData组件！");
            enabled = false;
            return;
        }

        InitializeCapsuleComponents();               // 创建子物体，但初始隐藏
        HideAllParts();                              // 隐藏所有子部件
        currentRectangleLength = totalHoldLength;    // 初始长度等于总长
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        float currentPlayTime = GameManager.Instance.CurrentPlayTime;
        if (currentPlayTime == -1) return;

        if (!isCommandsInitialized)
        {
            InitCommands();
            Debug.Log($"Hold音符{noteData.NoteIndex}指令初始化完成，firstCommandStartTime={firstCommandStartTime}");
            transform.position = new Vector2(noteData.x, noteData.y); // 左半圆圆心
            isCommandsInitialized = true;
            return;
        }

        // 根据第一个指令的开始时间控制显示
        if (currentPlayTime < firstCommandStartTime)
        {
            HideAllParts();
            return;
        }
        else
        {
            ShowAllParts();
        }
        // 更新左半圆圆心位置（由指令驱动）
        UpdateNotePosition(currentPlayTime, Time.deltaTime);
        // 更新胶囊视觉（根据当前剩余长度和方向）
        UpdateCapsuleVisual(currentRectangleLength);
        // 判定逻辑
        UpdateHoldJudge(currentPlayTime);
    }

    private void OnDestroy()
    {
        shiftCommands.Clear();
        moveCommands.Clear();
        shiftCommands = null;
        moveCommands = null;
        dropToCommand = null;
    }
    #endregion

    #region 指令初始化
    private void InitCommands()
    {
        if (noteData.commands == null || noteData.commands.Count == 0) return;

        firstCommandStartTime = noteData.commands[0].timeA;

        foreach (var cmd in noteData.commands)
        {
            string cmdName = cmd.commandName?.ToLower() ?? "";

            if (cmdName.StartsWith("drop_to"))
            {
                dropToCommand = new DropToCommand(noteData, cmd, cmd.key_name);
                dropToCommand.perfectThreshold = 0.15f;
                dropToCommand.goodThreshold = 0.25f;
                dropToCommand.badThreshold = 0.35f;
                holdDuration = cmd.hold_duration;

                // 从drop_to指令计算方向与总长度
                Vector2 start = new Vector2(cmd.x1, cmd.y1);
                Vector2 end = new Vector2(cmd.x2, cmd.y2);
                holdDirection = (end - start).normalized;
                totalHoldLength = Vector2.Distance(start, end);
                currentRectangleLength = totalHoldLength;   // 初始化为总长

                // 将drop_to也加入移动列表，确保其位移被执行
                shiftCommands.Add(dropToCommand);
            }
            else if (cmdName == "shift")
            {
                var shiftCmd = new ShiftCommand(noteData, cmd);
                shiftCommands.Add(shiftCmd);
            }
            else if (cmdName == "move" && !string.IsNullOrEmpty(cmd.json_filename))
            {
                var moveCmd = new MoveCommand(noteData, cmd);
                moveCommands.Add(moveCmd);
            }
        }

        if (dropToCommand == null)
            Debug.Log($"Hold音符{noteData.NoteIndex}缺少DropTo指令（必须）！");
    }
    #endregion

    #region 位置更新
    private void UpdateNotePosition(float currentTime, float deltaTime)
    {
        foreach (var moveCmd in moveCommands)
            moveCmd.UpdateNotePosition(currentTime);
        foreach (var shiftCmd in shiftCommands)
            shiftCmd.UpdateNotePosition(currentTime, deltaTime);

        // 左半圆圆心 = 更新后的noteData坐标
        transform.position = new Vector3(noteData.x, noteData.y, transform.position.z);
    }
    #endregion

    #region Hold判定核心逻辑
    private void UpdateHoldJudge(float currentTime)
    {
        if (dropToCommand == null) return;

        if (!isHoldStarted)
        {
            dropToCommand.Judge(currentTime, dropToCommand.KeyIndex); // 使用自身KeyIndex
            var judgeResult = dropToCommand.judgeResult;

            if (judgeResult != JudgeResult.None && judgeResult != JudgeResult.Miss)
            {
                isHoldStarted = true;
                holdStartTime = currentTime;
                lastPressTime = currentTime;
                SwitchJudgeSprite(judgeResult);
                Debug.Log($"Hold音符{noteData.NoteIndex}开始长按，判定结果：{judgeResult}");
            }
            else if (judgeResult == JudgeResult.Miss)
            {
                SwitchJudgeSprite(JudgeResult.Miss);
                noteData.isVisible = false;
                gameObject.SetActive(false);
                Debug.Log($"Hold音符{noteData.NoteIndex}初始按下超时 → Miss");
            }
            return;
        }

        if (isHoldStarted && !isHoldCompleted)
        {
            bool isKeyPressed = InputManager.Instance.IsGroupPressed(dropToCommand.KeyIndex);
            holdDuration = currentTime - holdStartTime;
            float expectedHoldEndTime = dropToCommand.endTime; // 修正：直接使用dropTo的结束时间
            float totalHoldTime = expectedHoldEndTime - holdStartTime;

            if (isKeyPressed)
                lastPressTime = currentTime;

            if (dropToCommand.judgeResult != JudgeResult.Miss && totalHoldTime > 0)
            {
                float holdProgress = Mathf.Clamp01(holdDuration / totalHoldTime);
                currentRectangleLength = Mathf.Lerp(totalHoldLength, 0, holdProgress);
                // 视觉更新已在Update中调用，此处不再重复
            }

            // 中断判定
            if (currentTime - lastPressTime > holdTolerance)
            {
                dropToCommand.judgeResult = holdDuration < (expectedHoldEndTime - dropToCommand.startTime) * 0.5f 
                    ? JudgeResult.Miss 
                    : JudgeResult.Bad;
                
                SwitchJudgeSprite(dropToCommand.judgeResult);
                isHoldCompleted = true;
                noteData.isVisible = false;
                gameObject.SetActive(false);
                Debug.Log($"Hold音符{noteData.NoteIndex}长按中断 → {dropToCommand.judgeResult}");
                return;
            }

            // 完成判定
            if (currentTime >= expectedHoldEndTime - holdCompleteThreshold)
            {
                currentRectangleLength = 0;
                isHoldCompleted = true;
                noteData.isVisible = false;
                gameObject.SetActive(false);
                Debug.Log($"Hold音符{noteData.NoteIndex}长按完成 → 最终判定：{dropToCommand.judgeResult}");
            }
        }
    }
    #endregion

    #region 视觉效果处理
    private void SwitchJudgeSprite(JudgeResult judgeResult)
    {
        // 若希望每次判定都更新精灵，可移除 isJudgmentSwitched 判断
        Sprite semiSprite = judgeResult switch
        {
            JudgeResult.Perfect => semiCirclePerfect,
            JudgeResult.Good => semiCircleGood,
            JudgeResult.Bad => semiCircleBad,
            JudgeResult.Miss => semiCircleMiss,
            _ => semiCircleDefault
        };

        leftSemiCircle.sprite = semiSprite;
        rightSemiCircle.sprite = semiSprite;

        rectanglePart.sprite = judgeResult switch
        {
            JudgeResult.Perfect => rectanglePerfect,
            JudgeResult.Good => rectangleGood,
            JudgeResult.Bad => rectangleBad,
            JudgeResult.Miss => rectangleMiss,
            _ => rectangleDefault
        };

        // 可选：添加空值检查
        if (leftSemiCircle.sprite == null)
            Debug.LogWarning($"Hold音符{noteData.NoteIndex} 半圆 {judgeResult} 精灵未赋值！");
        if (rectanglePart.sprite == null)
            Debug.LogWarning($"Hold音符{noteData.NoteIndex} 矩形 {judgeResult} 精灵未赋值！");

        Debug.Log($"Hold音符{noteData.NoteIndex}切换精灵为：{judgeResult}");
    }

    private void InitializeCapsuleComponents()
    {
        // 左半圆
        Transform leftChild = transform.Find("LeftSemiCircle");
        if (leftChild == null)
        {
            GameObject leftObj = new GameObject("LeftSemiCircle");
            leftObj.transform.SetParent(transform, false);
            leftChild = leftObj.transform;
        }
        leftSemiCircle = leftChild.GetComponent<SpriteRenderer>();
        if (leftSemiCircle == null)
            leftSemiCircle = leftChild.gameObject.AddComponent<SpriteRenderer>();
        leftSemiCircle.sprite = semiCircleDefault;

        // 矩形
        Transform rectChild = transform.Find("Rectangle");
        if (rectChild == null)
        {
            GameObject rectObj = new GameObject("Rectangle");
            rectObj.transform.SetParent(transform, false);
            rectChild = rectObj.transform;
        }
        rectanglePart = rectChild.GetComponent<SpriteRenderer>();
        if (rectanglePart == null)
            rectanglePart = rectChild.gameObject.AddComponent<SpriteRenderer>();
        rectangleTransform = rectChild;
        rectanglePart.sprite = rectangleDefault;

        // 右半圆
        Transform rightChild = transform.Find("RightSemiCircle");
        if (rightChild == null)
        {
            GameObject rightObj = new GameObject("RightSemiCircle");
            rightObj.transform.SetParent(transform, false);
            rightChild = rightObj.transform;
        }
        rightSemiCircle = rightChild.GetComponent<SpriteRenderer>();
        if (rightSemiCircle == null)
            rightSemiCircle = rightChild.gameObject.AddComponent<SpriteRenderer>();
        rightSemiCircle.sprite = semiCircleDefault;

        // 初始隐藏
        HideAllParts();
    }

    private void UpdateCapsuleVisual(float currentLength)
    {
        if (rectangleTransform == null) return;

        // 限制最小长度（避免缩放为0时出现异常）
        currentLength = Mathf.Max(currentLength, 0.01f);

        // 1. 矩形：中心位置 = 左半圆圆心 + direction * (currentLength/2)
        rectangleTransform.position = (Vector2)transform.position + holdDirection * (currentLength * 0.5f);
        // 矩形旋转：使X轴指向direction
        rectangleTransform.rotation = Quaternion.FromToRotation(Vector3.right, holdDirection);
        // 矩形缩放：假设原始宽度为1，则X缩放 = currentLength
        rectangleTransform.localScale = new Vector3(currentLength, 1f, 1f);

        // 2. 右半圆：位置 = 左半圆圆心 + direction * currentLength
        rightSemiCircle.transform.position = (Vector2)transform.position + holdDirection * currentLength;
        // 右半圆旋转：使其开口指向 -direction（朝向矩形）
        rightSemiCircle.transform.rotation = Quaternion.FromToRotation(Vector3.right, -holdDirection);

        // 3. 左半圆：位置就是transform.position，旋转使其开口指向 direction
        leftSemiCircle.transform.rotation = Quaternion.FromToRotation(Vector3.right, holdDirection);
    }

    private void HideAllParts()
    {
        if (leftSemiCircle != null) leftSemiCircle.enabled = false;
        if (rectanglePart != null) rectanglePart.enabled = false;
        if (rightSemiCircle != null) rightSemiCircle.enabled = false;
    }

    private void ShowAllParts()
    {
        if (leftSemiCircle != null && !leftSemiCircle.enabled) leftSemiCircle.enabled = true;
        if (rectanglePart != null && !rectanglePart.enabled) rectanglePart.enabled = true;
        if (rightSemiCircle != null && !rightSemiCircle.enabled) rightSemiCircle.enabled = true;
    }
    #endregion

    #region 外部接口
    public JudgeResult GetHoldJudgeResult()
    {
        return dropToCommand?.judgeResult ?? JudgeResult.None;
    }

    public void ForceEndHold()
    {
        isHoldCompleted = true;
        noteData.isVisible = false;
        gameObject.SetActive(false);
    }
    #endregion
}