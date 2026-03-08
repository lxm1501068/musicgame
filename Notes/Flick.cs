using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteRenderer))]
public class Flick : MonoBehaviour
{
    [Header("核心关联")]
    public NoteData noteData;

    [Header("判定对应的精灵图片")]
    public Sprite defaultFlickSprite;
    public Sprite perfectFlickSprite;
    public Sprite goodFlickSprite;
    public Sprite badFlickSprite;
    public Sprite missFlickSprite;

    [Header("销毁设置")]
    public float destroyDelay = 0.2f;

    [Header("Flick专属配置")]
    public List<int> validOtherKeyIndices = new List<int>() { 1, 2, 3 };
    public float judgeTime;

    private SpriteRenderer spriteRenderer;
    private bool hasSwitchedSprite = false;
    private bool isJudged = false;
    private JudgeResult judgeResult = JudgeResult.None;

    private DropToCommand dropToCommand;
    private List<ShiftCommand> shiftCommands = new List<ShiftCommand>();
    private List<MoveCommand> moveCommands = new List<MoveCommand>();

    private float perfectThreshold => 0.1f;
    private float goodThreshold => 0.2f;
    private float badThreshold => 0.3f;

    private bool isCommandsInitialized = false;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"[{noteData.NoteIndex}] Flick组件：未找到SpriteRenderer组件！");
            enabled = false;
            return;
        }

        // 【新增】初始隐藏
        spriteRenderer.enabled = false;

        if (defaultFlickSprite != null)
        {
            spriteRenderer.sprite = defaultFlickSprite;
        }
        else
        {
            Debug.LogWarning($"[{noteData.NoteIndex}] Flick组件：未设置默认Flick精灵！");
        }

        if (validOtherKeyIndices.Count == 0)
        {
            validOtherKeyIndices = new List<int>() { 1, 2, 3 };
            Debug.LogWarning($"[{noteData.NoteIndex}] Flick组件：有效按键组为空，默认添加1/2/3");
        }
        validOtherKeyIndices.RemoveAll(x => x == 11);

        if (noteData == null)
        {
            Debug.LogError($"[{noteData.NoteIndex}] Flick组件：NoteData未赋值！");
            enabled = false;
            return;
        }
    }

    void Update()
    {
        if (GameManager.Instance == null) return;
        
        float currentTime = GameManager.Instance.CurrentPlayTime;
        if (currentTime == -1) return;

        if (!isCommandsInitialized)
        {
            InitCommands();
            transform.position = new Vector2(noteData.x, noteData.y);
            isCommandsInitialized = true;

            // 【新增】指令初始化完成，显示音符
            spriteRenderer.enabled = true;
            Debug.Log($"[{noteData.NoteIndex}] Flick组件：谱面加载完成，指令初始化完成，显示音符");
        }

        if (hasSwitchedSprite)
        {
            SyncPosition();
            return;
        }

        ExecuteShiftCommands(currentTime);
        ExecuteMoveCommands(currentTime);
        ExecuteDropToJudge(currentTime);
        CheckFlickJudge(currentTime);

        if (isJudged && !hasSwitchedSprite)
        {
            SwitchJudgeSprite(judgeResult);
            hasSwitchedSprite = true;
            StartCoroutine(DelayDestroyNote());
        }

        SyncPosition();
    }

    private void InitCommands()
    {
        if (noteData.commands == null || noteData.commands.Count == 0)
        {
            Debug.Log($"[{noteData.NoteIndex}] Flick组件：NoteData无关联的Command！");
            return;
        }

        Command cmd = noteData.commands[0];
        dropToCommand = new DropToCommand(noteData, cmd, noteData.KeyIndex);
        judgeTime = cmd.timeB;

        if (cmd.x2 != 0 || cmd.y2 != 0)
        {
            shiftCommands.Add(new ShiftCommand(noteData, cmd));
        }

        if (!string.IsNullOrEmpty(cmd.filename))
        {
            moveCommands.Add(new MoveCommand(noteData, cmd.filename));
        }
    }

    private void ExecuteShiftCommands(float currentTime)
    {
        foreach (var shiftCmd in shiftCommands)
            shiftCmd.UpdateNotePosition(currentTime, Time.deltaTime);
    }

    private void ExecuteMoveCommands(float currentTime)
    {
        foreach (var moveCmd in moveCommands)
            moveCmd.UpdateNotePosition(currentTime);
    }

    private void ExecuteDropToJudge(float currentTime)
    {
        if (dropToCommand != null && noteData.commands.Count > 0)
        {
            Command cmd = noteData.commands[0];
            dropToCommand.Judge(currentTime, cmd.key_name);
            if (dropToCommand.judgeResult != JudgeResult.None && !isJudged)
            {
                judgeResult = dropToCommand.judgeResult;
                isJudged = true;
            }
        }
    }

    private void SyncPosition()
    {
        if (noteData == null) return;
        transform.position = new Vector2(noteData.x, noteData.y);
    }

    private void CheckFlickJudge(float currentTime)
    {
        if (dropToCommand != null && dropToCommand.judgeResult != JudgeResult.None) return;

        float timeDiff = currentTime - judgeTime;
        float absTimeDiff = Mathf.Abs(timeDiff);

        if (absTimeDiff > badThreshold)
        {
            SetJudgeResult(JudgeResult.Miss, timeDiff);
            return;
        }

        bool isKey11Triggered = IsKeyTriggered(11);
        bool isOtherKeyTriggered = IsAnyOtherKeyTriggered();

        if (isKey11Triggered && isOtherKeyTriggered)
        {
            JudgeResult result = GetJudgeResultByTimeDiff(absTimeDiff);
            SetJudgeResult(result, timeDiff);
        }
    }

    private bool IsKeyTriggered(int keyIndex)
    {
        return InputManager.Instance.IsGroupPressed(keyIndex) || InputManager.Instance.IsGroupHeld(keyIndex);
    }

    private bool IsAnyOtherKeyTriggered()
    {
        foreach (int keyIndex in validOtherKeyIndices)
        {
            if (IsKeyTriggered(keyIndex)) return true;
        }
        return false;
    }

    private JudgeResult GetJudgeResultByTimeDiff(float absTimeDiff)
    {
        if (absTimeDiff <= perfectThreshold) return JudgeResult.Perfect;
        if (absTimeDiff <= goodThreshold) return JudgeResult.Good;
        if (absTimeDiff <= badThreshold) return JudgeResult.Bad;
        return JudgeResult.Miss;
    }

    private void SetJudgeResult(JudgeResult result, float timeDiff)
    {
        judgeResult = result;
        isJudged = true;
        Debug.Log($"[{noteData.NoteIndex}] Flick判定结果：{result} | 时间差：{timeDiff:F2}s");
    }

    private void SwitchJudgeSprite(JudgeResult judgeResult)
    {
        Sprite targetSprite = judgeResult switch
        {
            JudgeResult.Perfect => perfectFlickSprite,
            JudgeResult.Good => goodFlickSprite,
            JudgeResult.Bad => badFlickSprite,
            JudgeResult.Miss => missFlickSprite,
            _ => defaultFlickSprite
        };

        if (targetSprite != null)
            spriteRenderer.sprite = targetSprite;
        else
        {
            Debug.LogError($"[{noteData.NoteIndex}] Flick组件：{judgeResult}对应的精灵未赋值！");
            spriteRenderer.sprite = defaultFlickSprite;
        }
    }

    private IEnumerator DelayDestroyNote()
    {
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        StopAllCoroutines();
    }
}