using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Flick : BaseNote
{
    [Header("Flick 专属配置")]
    public float perfectThreshold = 0.15f;             // Perfect 判定窗口
    public float secondKeyWindow = 0.3f;               // 第二个按键的响应窗口

    [Header("固定运动参数")]
    public Vector2 startPos = new Vector2(0, 5);
    public Vector2 endPos = new Vector2(0, 0);

    private bool isWaitingForSecondKey = false;
    private float spacePressTime = 0f;
    private float startTime;
    private float endTime;
    private float judgeTime;

    protected override void Awake()
    {
        base.Awake();
    }


    protected override void InitCommands()
    {
        if (noteData.commands == null || noteData.commands.Count == 0)
        {
            Debug.Log($"[{noteData.NoteIndex}] Flick组件：NoteData无关联的Command！");
            firstCommandStartTime = 0f;
            return;
        }

        Command cmd = noteData.commands[0];
        startTime = cmd.timeA;
        endTime = cmd.timeB;
        judgeTime = endTime;
        firstCommandStartTime = startTime;

        noteData.x = startPos.x;
        noteData.y = startPos.y;
    }

    protected override void OnNoteUpdate(float currentTime)
    {
        UpdatePosition(currentTime);
        CheckFlickJudge(currentTime);
    }

    private void UpdatePosition(float currentTime)
    {
        if (currentTime >= endTime)
        {
            noteData.x = endPos.x;
            noteData.y = endPos.y;
            return;
        }

        float t = (currentTime - startTime) / (endTime - startTime);
        float easedT = t * t;
        noteData.x = Mathf.Lerp(startPos.x, endPos.x, easedT);
        noteData.y = Mathf.Lerp(startPos.y, endPos.y, easedT);
    }

    private void CheckFlickJudge(float currentTime)
    {
        float timeDiff = currentTime - judgeTime;

        if (!isWaitingForSecondKey)
        {
            if (timeDiff > perfectThreshold)
            {
                judgeResult = JudgeResult.Miss;
                return;
            }

            if (Mathf.Abs(timeDiff) <= perfectThreshold && Input.GetKeyDown(KeyCode.Space))
            {
                isWaitingForSecondKey = true;
                spacePressTime = currentTime;
                Debug.Log($"[{noteData.NoteIndex}] Flick: 空格已按下，等待第二个键...");
            }
        }
        else
        {
            if (currentTime - spacePressTime > secondKeyWindow)
            {
                judgeResult = JudgeResult.Miss;
                return;
            }

            // 硬编码检查 zxcvbnm 是否被按下
            string[] targetKeys = { "z", "x", "c", "v", "b", "n", "m" };
            foreach (string key in targetKeys)
            {
                if (Input.GetKeyDown(key))
                {
                    judgeResult = JudgeResult.Perfect;
                    return;
                }
            }
        }
    }

}
