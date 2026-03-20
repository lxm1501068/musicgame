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
    private float holdDuration;                // 从谱面读取的 hold_duration（总按住时长）
    #endregion

    #region Hold特有配置
    [Header("Hold判定配置")]
    public float holdCheckInterval = 0.05f;
    public float holdCompleteThreshold = 0.1f; // 提前多少时间视为完成

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

    // 胶囊方向与长度（由drop_to指令决定）
    private Vector2 holdDirection = Vector2.right;
    private float totalHoldLength = 2.0f;          // 总长度 = speed * hold_duration
    private float currentRectangleLength;           // 当前矩形长度（随时间缩短）

    private bool isHoldStarted = false;
    private bool isHoldCompleted = false;
    private float holdStartTime = 0;

    private bool isCommandsInitialized = false;
    private float firstCommandStartTime;

    // 固定终点坐标（头部判定后左半圆固定于此）
    private Vector2 holdFixedEndPos;
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

        // 更新位置：头部判定前由指令驱动，头部判定后固定在终点
        if (!isHoldStarted)
        {
            UpdateNotePosition(currentPlayTime, Time.deltaTime);
        }
        else
        {
            // 强制固定左半圆在终点
            noteData.x = holdFixedEndPos.x;
            noteData.y = holdFixedEndPos.y;
        }
        transform.position = new Vector3(noteData.x, noteData.y, transform.position.z);

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
                holdDuration = cmd.hold_duration;                     // 记录按住总时长

                // 从drop_to指令计算方向
                Vector2 start = new Vector2(cmd.x1, cmd.y1);
                Vector2 end = new Vector2(cmd.x2, cmd.y2);
                holdDirection = -1*(end - start).normalized;

                // 总长度 = 速度 * 按住时长
                totalHoldLength = dropToCommand.speed * holdDuration;
                currentRectangleLength = totalHoldLength;

                // 记录终点坐标（用于固定左半圆）
                holdFixedEndPos = end;

                // 将drop_to也加入移动列表，确保其位移在头部判定前被执行
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
        // 头部判定前才执行指令位移
        foreach (var moveCmd in moveCommands)
            moveCmd.UpdateNotePosition(currentTime);
        foreach (var shiftCmd in shiftCommands)
            shiftCmd.UpdateNotePosition(currentTime, deltaTime);
    }
    #endregion

    #region Hold判定核心逻辑
    private void UpdateHoldJudge(float currentTime)
    {
        if (dropToCommand == null) return;

        if (!isHoldStarted)
        {
            // 头部判定
            dropToCommand.Judge(currentTime, dropToCommand.KeyIndex);
            var judgeResult = dropToCommand.judgeResult;

            if (judgeResult != JudgeResult.None && judgeResult != JudgeResult.Miss)
            {
                isHoldStarted = true;
                holdStartTime = currentTime;
                SwitchJudgeSprite(judgeResult);
                // 立即将左半圆固定在终点（防止后续指令影响）
                noteData.x = holdFixedEndPos.x;
                noteData.y = holdFixedEndPos.y;
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
            bool isKeyHeld = InputManager.Instance.IsGroupHeld(dropToCommand.KeyIndex);
            float elapsedHoldTime = currentTime - holdStartTime;

            // 中断判定：只要松开按键立即判 Miss
            if (!isKeyHeld)
            {
                dropToCommand.judgeResult = JudgeResult.Miss;
                SwitchJudgeSprite(JudgeResult.Miss);
                isHoldCompleted = true;
                noteData.isVisible = false;
                gameObject.SetActive(false);
                Debug.Log($"Hold音符{noteData.NoteIndex}长按中断 → Miss");
                return;
            }

            // 正常进度更新
            if (elapsedHoldTime < holdDuration)
            {
                float progress = elapsedHoldTime / holdDuration;
                currentRectangleLength = Mathf.Lerp(totalHoldLength, 0, progress);
            }

            // 完成判定
            if (elapsedHoldTime >= holdDuration - holdCompleteThreshold)
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
        leftSemiCircle.sortingOrder = 1;  // 设置图层顺序为1

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
        rectanglePart.sortingOrder = 1;   // 设置图层顺序为1

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
        rightSemiCircle.sortingOrder = 1; // 设置图层顺序为1

        HideAllParts();
    }

    private void UpdateCapsuleVisual(float currentLength)
    {
        if (rectangleTransform == null) return;

        currentLength = Mathf.Max(currentLength, 0.01f);

        // 矩形：中心位置 = 左半圆圆心 + direction * (currentLength/2)
        rectangleTransform.position = (Vector2)transform.position + holdDirection * (currentLength * 0.5f);
        rectangleTransform.rotation = Quaternion.FromToRotation(Vector3.right, holdDirection);
        rectangleTransform.localScale = new Vector3(currentLength, 1f, 1f);

        // 右半圆：位置 = 左半圆圆心 + direction * currentLength
        rightSemiCircle.transform.position = (Vector2)transform.position + holdDirection * currentLength;
        rightSemiCircle.transform.rotation = Quaternion.FromToRotation(Vector3.right, -holdDirection);

        // 左半圆：旋转使其开口指向 direction
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