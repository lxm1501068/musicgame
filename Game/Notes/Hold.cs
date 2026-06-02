using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Hold : BaseNote
{
    #region 组件引用
    private SpriteRenderer leftSemiCircle;
    private SpriteRenderer rectanglePart;
    private SpriteRenderer rightSemiCircle;
    private Transform rectangleTransform;
    private List<ShiftCommand> shiftCommands = new List<ShiftCommand>();
    private List<MoveCommand> moveCommands = new List<MoveCommand>();
    private List<SpinCommand> spinCommands = new List<SpinCommand>();
    private DropToCommand dropToCommand;
    private float holdDuration;
    private int keyIndex = -1; // 保存按键索引
    #endregion

    private Vector2 holdDirection = Vector2.right;
    private float totalHoldLength = 2.0f;
    private float currentRectangleLength;
    private bool isHoldStarted = false;
    private bool isHoldCompleted = false;
    private float holdStartTime = 0;
    private Vector2 holdFixedEndPos;
    private float holdCompleteThreshold = 0.1f;

    protected override void Awake()
    {
        base.Awake();
        InitializeCapsuleComponents();
        currentRectangleLength = totalHoldLength;
    }


    protected override void InitCommands()
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
                keyIndex = cmd.key_name; // 保存按键索引

                Vector2 start = new Vector2(cmd.x1, cmd.y1);
                Vector2 end = new Vector2(cmd.x2, cmd.y2);
                holdDirection = -1 * (end - start).normalized;

                totalHoldLength = dropToCommand.speed * holdDuration;
                currentRectangleLength = totalHoldLength;
                holdFixedEndPos = end;

                shiftCommands.Add(dropToCommand);
            }
            else if (cmdName == "shift")
            {
                shiftCommands.Add(new ShiftCommand(noteData, cmd));
            }
            else if (cmdName == "move" && !string.IsNullOrEmpty(cmd.json_filename))
            {
                moveCommands.Add(new MoveCommand(noteData, cmd));
            }
            else if (cmdName == "spin")
            {
                spinCommands.Add(new SpinCommand(noteData, cmd));
            }
        }
    }

    protected override void UpdateVisibility(float currentTime)
    {
        bool shouldBeVisible = currentTime >= firstCommandStartTime && !isHoldCompleted;
        if (leftSemiCircle.enabled != shouldBeVisible) leftSemiCircle.enabled = shouldBeVisible;
        if (rectanglePart.enabled != shouldBeVisible) rectanglePart.enabled = shouldBeVisible;
        if (rightSemiCircle.enabled != shouldBeVisible) rightSemiCircle.enabled = shouldBeVisible;
        
        // BaseNote uses spriteRenderer.enabled, we sync it with our parts
        spriteRenderer.enabled = shouldBeVisible;
    }

    protected override void OnNoteUpdate(float currentTime)
    {
        if (!isHoldStarted)
        {
            foreach (var moveCmd in moveCommands) moveCmd.UpdateNotePosition(currentTime);
            foreach (var shiftCmd in shiftCommands) shiftCmd.UpdateNotePosition(currentTime, Time.deltaTime);
            foreach (var spinCmd in spinCommands) spinCmd.UpdateNoteRotation(currentTime);
        }
        else
        {
            noteData.x = holdFixedEndPos.x;
            noteData.y = holdFixedEndPos.y;
        }

        UpdateCapsuleVisual(currentRectangleLength);
        UpdateHoldJudge(currentTime);
    }

    private void UpdateHoldJudge(float currentTime)
    {
        if (dropToCommand == null) return;

        if (!isHoldStarted)
        {
            dropToCommand.Judge(currentTime, dropToCommand.KeyIndex);
            var result = dropToCommand.judgeResult;

            if (result != JudgeResult.None)
            {
                if (result != JudgeResult.Miss)
                {
                    isHoldStarted = true;
                    holdStartTime = currentTime;
                    UpdateGlobalUI(result);
                    
                    noteData.x = holdFixedEndPos.x;
                    noteData.y = holdFixedEndPos.y;
                }
                else
                {
                    HandleHoldMiss();
                }
            }
            return;
        }

        if (isHoldStarted && !isHoldCompleted)
        {
            bool isKeyHeld = InputManager.Instance != null && InputManager.Instance.IsGroupHeld(dropToCommand.KeyIndex);
            float elapsedHoldTime = currentTime - holdStartTime;

            if (!isKeyHeld)
            {
                HandleHoldMiss();
                return;
            }

            if (elapsedHoldTime < holdDuration)
            {
                float progress = elapsedHoldTime / holdDuration;
                currentRectangleLength = Mathf.Lerp(totalHoldLength, 0, progress);
            }

            if (elapsedHoldTime >= holdDuration - holdCompleteThreshold)
            {
                currentRectangleLength = 0;
                isHoldCompleted = true;
                if (JudgeResultDisplay.Instance != null) JudgeResultDisplay.Instance.ShowJudgeResult(JudgeResult.Perfect);
                
                // Set judgeResult to finish the note in BaseNote
                judgeResult = JudgeResult.Perfect; 
            }
        }
    }

    private void HandleHoldMiss()
    {
        judgeResult = JudgeResult.Miss;
        isHoldCompleted = true;
        UpdateGlobalUI(JudgeResult.Miss);
    }

    protected override void HandleJudgeResult(JudgeResult result)
    {
        // Hold manages its own completion/destruction flow
        if (result == JudgeResult.Miss || (result == JudgeResult.Perfect && isHoldCompleted))
        {
            hasSwitchedSprite = true;
            
            // Hold结束时隐藏判定圆形
            if (JudgeCircleManager.Instance != null)
            {
                JudgeCircleManager.Instance.HideHoldJudgeCircle();
            }
            
            StartCoroutine(DelayDestroyNote());
        }
    }

    /// <summary>
    /// 显示判定结果圆形，重写以使用按键位置
    /// </summary>
    protected override void ShowJudgeCircle(JudgeResult result)
    {
        if (JudgeCircleManager.Instance != null && noteData != null)
        {
            // Hold音符的判定圈显示在按键位置上，而不是音符位置
            Vector2 keyPosition = GetKeyPosition();
            JudgeCircleManager.Instance.ShowJudgeCircle(keyPosition, result, true);
        }
    }

    /// <summary>
    /// 获取按键当前位置
    /// </summary>
    private Vector2 GetKeyPosition()
    {
        if (keyIndex < 0)
        {
            // 如果没有有效的按键索引，返回音符位置作为后备
            return new Vector2(noteData.x, noteData.y);
        }

        // 查找场景中所有 Key_move 对象
        Key_move[] allKeys = FindObjectsOfType<Key_move>();
        foreach (var key in allKeys)
        {
            if (key.keyIndex == keyIndex)
            {
                return new Vector2(key.CurrentX, key.CurrentY);
            }
        }

        // 如果找不到对应的按键，返回音符位置作为后备
        Debug.LogWarning($"Hold: 未找到 keyIndex={keyIndex} 的按键，使用音符位置");
        return new Vector2(noteData.x, noteData.y);
    }

    protected override bool IsHoldNote()
    {
        return true;
    }


    private void InitializeCapsuleComponents()
    {
        leftSemiCircle = CreatePart("LeftSemiCircle", null);
        rectanglePart = CreatePart("Rectangle", null);
        rectangleTransform = rectanglePart.transform;
        rightSemiCircle = CreatePart("RightSemiCircle", null);
    }

    private SpriteRenderer CreatePart(string name, Sprite defaultSprite)
    {
        Transform t = transform.Find(name);
        if (t == null)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            t = obj.transform;
        }
        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        if (sr == null) sr = t.gameObject.AddComponent<SpriteRenderer>();
        if (defaultSprite != null)
            sr.sprite = defaultSprite;
        sr.sortingOrder = 1;
        sr.enabled = false;
        return sr;
    }

    private void UpdateCapsuleVisual(float currentLength)
    {
        if (rectangleTransform == null) return;
        currentLength = Mathf.Max(currentLength, 0.01f);
        rectangleTransform.position = (Vector2)transform.position + holdDirection * (currentLength * 0.5f);
        rectangleTransform.rotation = Quaternion.FromToRotation(Vector3.right, holdDirection);
        rectangleTransform.localScale = new Vector3(currentLength, 1f, 1f);
        rightSemiCircle.transform.position = (Vector2)transform.position + holdDirection * currentLength;
        rightSemiCircle.transform.rotation = Quaternion.FromToRotation(Vector3.right, -holdDirection);
        leftSemiCircle.transform.rotation = Quaternion.FromToRotation(Vector3.right, holdDirection);
    }
}
