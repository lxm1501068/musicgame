using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Tap : BaseNote
{
    // 存储所有指令对象
    private List<ShiftCommand> shiftCommands = new List<ShiftCommand>();
    private List<MoveCommand> moveCommands = new List<MoveCommand>();
    private DropToCommand dropToCommand;

    protected override void Awake()
    {
        base.Awake();
    }


    protected override void InitCommands()
    {
        if (noteData.commands == null || noteData.commands.Count == 0)
        {
            Debug.Log($"[{noteData.NoteIndex}] Tap组件：NoteData无关联的Command！");
            firstCommandStartTime = 0f;
            return;
        }

        firstCommandStartTime = noteData.commands[0].timeA;

        foreach (var cmd in noteData.commands)
        {
            if (dropToCommand == null && cmd.timeB > cmd.timeA)
            {
                dropToCommand = new DropToCommand(noteData, cmd, cmd.key_name);
            }

            if (cmd.x2 != 0 || cmd.y2 != 0)
            {
                shiftCommands.Add(new ShiftCommand(noteData, cmd));
            }

            if (!string.IsNullOrEmpty(cmd.json_filename))
            {
                moveCommands.Add(new MoveCommand(noteData, cmd));
            }
        }

        if (dropToCommand == null)
            Debug.LogWarning($"[{gameObject.name}] 未找到 Drop To 指令！");
    }

    protected override void OnNoteUpdate(float currentTime)
    {
        // 执行所有激活的指令
        foreach (var shiftCmd in shiftCommands)
            shiftCmd.UpdateNotePosition(currentTime, Time.deltaTime);

        foreach (var moveCmd in moveCommands)
            moveCmd.UpdateNotePosition(currentTime);

        if (dropToCommand != null)
        {
            dropToCommand.Judge(currentTime, dropToCommand.KeyIndex);
            judgeResult = dropToCommand.judgeResult;
        }
    }

}
