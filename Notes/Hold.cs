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
    private ShiftCommand shiftCommand;
    private DropToCommand dropToCommand;
    private MoveCommand moveCommand;
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
    public float initialRectangleLength = 2.0f;

    private bool isHoldStarted = false;
    private bool isHoldCompleted = false;
    private float lastPressTime = 0;
    private float holdStartTime = 0;
    private float currentRectangleLength;
    private float holdDuration = 0f;
    private bool isJudgmentSwitched = false;
    
    private bool isCommandsInitialized = false;
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

        InitializeCapsuleComponents();

        // 【新增】初始隐藏所有子部件
        if (leftSemiCircle != null) leftSemiCircle.enabled = false;
        if (rectanglePart != null) rectanglePart.enabled = false;
        if (rightSemiCircle != null) rightSemiCircle.enabled = false;

        currentRectangleLength = initialRectangleLength;
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;
        
        float currentPlayTime = GameManager.Instance.CurrentPlayTime;
        if (currentPlayTime == -1) return;

        if (!isCommandsInitialized)
        {
            InitCommands();
            transform.position = new Vector2(noteData.x, noteData.y);
            isCommandsInitialized = true;

            // 【新增】指令初始化完成，显示所有子部件
            if (leftSemiCircle != null) leftSemiCircle.enabled = true;
            if (rectanglePart != null) rectanglePart.enabled = true;
            if (rightSemiCircle != null) rightSemiCircle.enabled = true;
            Debug.Log($"[{noteData.NoteIndex}] Hold组件：谱面加载完成，指令初始化完成，显示音符");
            return;
        }

        if (noteData == null || !noteData.isVisible) return;

        UpdateNotePosition(currentPlayTime, Time.deltaTime);
        UpdateHoldJudge(currentPlayTime);
    }

    private void OnDestroy()
    {
        shiftCommand = null;
        dropToCommand = null;
        moveCommand = null;
    }
    #endregion

    #region 指令初始化
    private void InitCommands()
    {
        if (noteData.commands == null || noteData.commands.Count == 0) return;

        foreach (var cmd in noteData.commands)
        {
            switch (cmd.commandName)
            {
                case "Shift":
                    shiftCommand = new ShiftCommand(noteData, cmd);
                    break;
                case "DropTo":
                    dropToCommand = new DropToCommand(noteData, cmd, cmd.key_name);
                    dropToCommand.perfectThreshold = 0.15f;
                    dropToCommand.goodThreshold = 0.25f;
                    dropToCommand.badThreshold = 0.35f;
                    break;
                case "Move":
                    if (!string.IsNullOrEmpty(cmd.filename))
                        moveCommand = new MoveCommand(noteData, cmd.filename);
                    break;
            }
        }

        if (dropToCommand == null)
            Debug.Log($"Hold音符{noteData.NoteIndex}缺少DropTo指令（必须）！");
    }
    #endregion

    #region 位置更新
    private void UpdateNotePosition(float currentTime, float deltaTime)
    {
        if (moveCommand != null)
            moveCommand.UpdateNotePosition(currentTime);
        else if (shiftCommand != null)
            shiftCommand.UpdateNotePosition(currentTime, deltaTime);

        transform.position = new Vector3(noteData.x, noteData.y, transform.position.z);
    }
    #endregion

    #region Hold判定核心逻辑
    private void UpdateHoldJudge(float currentTime)
    {
        if (dropToCommand == null) return;

        if (!isHoldStarted)
        {
            dropToCommand.Judge(currentTime, noteData.commands[0].key_name);
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
            bool isKeyPressed = InputManager.Instance.IsGroupPressed(noteData.commands[0].key_name);
            holdDuration = currentTime - holdStartTime;
            float expectedHoldEndTime = dropToCommand.endTime + (noteData.commands[0].timeB - noteData.commands[0].timeA);
            float totalHoldTime = expectedHoldEndTime - holdStartTime;

            if (isKeyPressed)
                lastPressTime = currentTime;

            if (dropToCommand.judgeResult != JudgeResult.Miss && totalHoldTime > 0)
            {
                float holdProgress = Mathf.Clamp01(holdDuration / totalHoldTime);
                currentRectangleLength = Mathf.Lerp(initialRectangleLength, 0, holdProgress);
                UpdateCapsuleScale(currentRectangleLength);
            }

            if (currentTime - lastPressTime > holdTolerance)
            {
                dropToCommand.judgeResult = holdDuration < (expectedHoldEndTime - dropToCommand.endTime) * 0.5f 
                    ? JudgeResult.Miss 
                    : JudgeResult.Bad;
                
                SwitchJudgeSprite(dropToCommand.judgeResult);
                isHoldCompleted = true;
                noteData.isVisible = false;
                gameObject.SetActive(false);
                Debug.Log($"Hold音符{noteData.NoteIndex}长按中断 → {dropToCommand.judgeResult}");
                return;
            }

            if (currentTime >= expectedHoldEndTime - holdCompleteThreshold)
            {
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
    private void SwitchJudgeSprite(JudgeResult judgeResult)
    {
        if (isJudgmentSwitched) return;
        
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

        isJudgmentSwitched = true;
        Debug.Log($"Hold音符{noteData.NoteIndex}切换精灵为：{judgeResult}");
    }

    private void InitializeCapsuleComponents()
    {
        Transform leftChild = transform.Find("LeftSemiCircle");
        Transform rectChild = transform.Find("Rectangle");
        Transform rightChild = transform.Find("RightSemiCircle");

        if (leftChild == null)
        {
            GameObject leftObj = new GameObject("LeftSemiCircle");
            leftObj.transform.SetParent(transform, false);
            leftChild = leftObj.transform;
        }
        leftSemiCircle = leftChild.GetComponent<SpriteRenderer>();
        if (leftSemiCircle == null)
            leftSemiCircle = leftChild.gameObject.AddComponent<SpriteRenderer>();
        if (semiCircleDefault != null)
            leftSemiCircle.sprite = semiCircleDefault;

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
        if (rectangleDefault != null)
            rectanglePart.sprite = rectangleDefault;

        if (rightChild == null)
        {
            GameObject rightObj = new GameObject("RightSemiCircle");
            rightObj.transform.SetParent(transform, false);
            rightChild = rightObj.transform;
        }
        rightSemiCircle = rightChild.GetComponent<SpriteRenderer>();
        if (rightSemiCircle == null)
            rightSemiCircle = rightChild.gameObject.AddComponent<SpriteRenderer>();
        if (semiCircleDefault != null)
            rightSemiCircle.sprite = semiCircleDefault;

        UpdateCapsuleScale(initialRectangleLength);
        Debug.Log($"Hold音符{noteData.NoteIndex}胶囊形子部件初始化完成");
    }

    private void UpdateCapsuleScale(float rectangleLength)
    {
        if (rectangleTransform == null) return;

        float scaleRatio = initialRectangleLength > 0 ? rectangleLength / initialRectangleLength : 0;
        rectangleTransform.localScale = new Vector3(scaleRatio, 1f, 1f);
        Debug.Log($"Hold音符{noteData.NoteIndex}矩形长度更新为：{rectangleLength:F3}，缩放比例：{scaleRatio:F3}");
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