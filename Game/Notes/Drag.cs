using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Drag : BaseNote
{
    [Header("判定配置")]
    public float perfectThreshold = 0.1f;

    [Header("判定对应的精灵图片")]
    public Sprite defaultDragSprite;
    public Sprite perfectDragSprite;
    public Sprite missDragSprite;

    private List<ShiftCommand> shiftCommands = new List<ShiftCommand>();
    private List<MoveCommand> moveCommands = new List<MoveCommand>();

    private float dropEndTime;
    private int dropKeyIndex;
    private float dropStartTime;

    protected override void Awake()
    {
        base.Awake();
        if (defaultDragSprite != null)
            spriteRenderer.sprite = defaultDragSprite;
    }

    protected override void InitCommands()
    {
        if (noteData.commands == null || noteData.commands.Count == 0)
        {
            Debug.Log($"[{noteData.NoteIndex}] Drag组件：NoteData无关联的Command！");
            firstCommandStartTime = 0f;
            return;
        }

        firstCommandStartTime = noteData.commands[0].timeA;
        bool dropToFound = false;

        foreach (var cmd in noteData.commands)
        {
            if (!dropToFound && cmd.timeB > cmd.timeA)
            {
                dropEndTime = cmd.timeB;
                dropKeyIndex = cmd.key_name;
                dropStartTime = cmd.timeA;
                dropToFound = true;

                if (cmd.x2 != 0 || cmd.y2 != 0)
                {
                    shiftCommands.Add(new ShiftCommand(noteData, cmd));
                }
            }
            else if (cmd.x2 != 0 || cmd.y2 != 0)
            {
                shiftCommands.Add(new ShiftCommand(noteData, cmd));
            }

            if (!string.IsNullOrEmpty(cmd.json_filename))
            {
                moveCommands.Add(new MoveCommand(noteData, cmd));
            }
        }

        if (!dropToFound)
        {
            dropEndTime = 0f;
            dropKeyIndex = noteData.KeyIndex;
            dropStartTime = 0f;
        }
    }

    protected override void OnNoteUpdate(float currentTime)
    {
        foreach (var shiftCmd in shiftCommands)
            shiftCmd.UpdateNotePosition(currentTime, Time.deltaTime);

        foreach (var moveCmd in moveCommands)
            moveCmd.UpdateNotePosition(currentTime);

        CheckDragJudge(currentTime);
    }

    private void CheckDragJudge(float currentTime)
    {
        float timeDiff = currentTime - dropEndTime;
        float absDiff = Mathf.Abs(timeDiff);

        bool isKeyActive = InputManager.Instance != null && 
                           (InputManager.Instance.IsGroupPressed(dropKeyIndex) || InputManager.Instance.IsGroupHeld(dropKeyIndex));

        if (absDiff <= perfectThreshold && isKeyActive)
        {
            judgeResult = JudgeResult.Perfect;
        }
        else if (timeDiff > perfectThreshold)
        {
            judgeResult = JudgeResult.Miss;
        }
    }

    protected override void SwitchJudgeSprite(JudgeResult result)
    {
        Sprite targetSprite = result switch
        {
            JudgeResult.Perfect => perfectDragSprite,
            JudgeResult.Miss => missDragSprite,
            _ => defaultDragSprite
        };

        if (targetSprite != null)
            spriteRenderer.sprite = targetSprite;
    }
}
