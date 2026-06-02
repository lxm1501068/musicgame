using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class Mtap : BaseNote
{
    [Header("判定规则")]
    public float maxTapInterval = 0.2f;

    [Header("UI引用")]
    public TMP_Text countText;

    private DropToCommand dropToCommand;
    private List<ShiftCommand> shiftCommands = new List<ShiftCommand>();
    private List<MoveCommand> moveCommands = new List<MoveCommand>();

    private int remainingTaps;
    private bool isFirstTapJudged = false;
    private float lastTapTime = 0f;
    private bool isMissed = false;

    protected override void Awake()
    {
        base.Awake();
        InitCountText();
    }


    private void InitCountText()
    {
        if (countText != null) return;
        countText = GetComponentInChildren<TMP_Text>();
        if (countText == null)
        {
            GameObject textObj = new GameObject("Mtap_CountText");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = new Vector3(0, 0, -0.1f);
            
            TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
            tmp.fontSize = 6;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            var meshRenderer = textObj.GetComponent<MeshRenderer>();
            if (meshRenderer != null && spriteRenderer != null)
            {
                meshRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                meshRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            }
            countText = tmp;
        }
    }

    protected override void InitCommands()
    {
        if (noteData.commands == null || noteData.commands.Count == 0)
        {
            Debug.Log($"[{noteData.NoteIndex}] Mtap组件：NoteData无关联的Command！");
            firstCommandStartTime = 0f;
            return;
        }

        firstCommandStartTime = noteData.commands[0].timeA;
        Command cmd = noteData.commands[0];
        dropToCommand = new DropToCommand(noteData, cmd, cmd.key_name);

        if (cmd.x2 != 0 || cmd.y2 != 0)
            shiftCommands.Add(new ShiftCommand(noteData, cmd));

        if (!string.IsNullOrEmpty(cmd.json_filename))
            moveCommands.Add(new MoveCommand(noteData, cmd));

        int totalTaps = cmd.hold_duration > 0 ? (int)cmd.hold_duration : 2;
        remainingTaps = totalTaps;
        UpdateCountText();
    }

    protected override void UpdateVisibility(float currentTime)
    {
        base.UpdateVisibility(currentTime);
        if (countText != null) countText.enabled = spriteRenderer.enabled;
    }

    protected override void OnNoteUpdate(float currentTime)
    {
        foreach (var shiftCmd in shiftCommands)
            shiftCmd.UpdateNotePosition(currentTime, Time.deltaTime);

        foreach (var moveCmd in moveCommands)
            moveCmd.UpdateNotePosition(currentTime);

        if (!isFirstTapJudged)
        {
            if (dropToCommand != null)
            {
                dropToCommand.Judge(currentTime, dropToCommand.KeyIndex);
                if (dropToCommand.judgeResult != JudgeResult.None)
                {
                    isFirstTapJudged = true;
                    lastTapTime = currentTime;
                    judgeResult = dropToCommand.judgeResult;

                    if (judgeResult == JudgeResult.Miss)
                    {
                        isMissed = true;
                    }
                    else
                    {
                        remainingTaps--;
                        UpdateCountText();
                        if (remainingTaps <= 0)
                        {
                            // 保持当前判定结果
                        }
                        else
                        {
                            // 还没完，重置 judgeResult 让 BaseNote 别销毁我们
                            judgeResult = JudgeResult.None;
                        }
                    }
                }
            }
        }
        else
        {
            CheckSubsequentTaps(currentTime);
        }
    }

    private void CheckSubsequentTaps(float currentTime)
    {
        if (isMissed) return;

        float interval = currentTime - lastTapTime;

        if (interval > maxTapInterval)
        {
            isMissed = true;
            judgeResult = JudgeResult.Miss;
            return;
        }

        if (dropToCommand != null && InputManager.Instance != null && InputManager.Instance.IsGroupPressed(dropToCommand.KeyIndex))
        {
            lastTapTime = currentTime;
            remainingTaps--;
            UpdateCountText();
            
            if (remainingTaps <= 0)
            {
                // 后续点击完成，使用第一次判定的结果（通常是 Perfect 或 Good）
                judgeResult = dropToCommand.judgeResult;
            }
        }
    }

    private void UpdateCountText()
    {
        if (countText != null)
        {
            countText.text = remainingTaps > 0 ? remainingTaps.ToString() : "";
        }
    }

}
