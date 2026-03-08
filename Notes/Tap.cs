using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteRenderer))]
public class Tap : MonoBehaviour
{
    [Header("核心关联")]
    public NoteData noteData;

    [Header("判定对应的精灵图片")]
    public Sprite defaultTapSprite;
    public Sprite perfectTapSprite;
    public Sprite goodTapSprite;
    public Sprite badTapSprite;
    public Sprite missTapSprite;

    [Header("销毁设置")]
    public float destroyDelay = 0.2f; 

    private SpriteRenderer spriteRenderer;
    private bool hasSwitchedSprite = false;
    private DropToCommand dropToCommand;
    private List<ShiftCommand> shiftCommands = new List<ShiftCommand>();
    private List<MoveCommand> moveCommands = new List<MoveCommand>();
    private bool isCommandsInitialized = false;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"[{noteData.NoteIndex}] Tap组件：未找到SpriteRenderer组件！");
            enabled = false;
            return;
        }

        // 【新增】初始隐藏：禁用 SpriteRenderer
        spriteRenderer.enabled = false;

        if (defaultTapSprite != null)
        {
            spriteRenderer.sprite = defaultTapSprite;
        }
        else
        {
            Debug.LogWarning($"[{noteData.NoteIndex}] Tap组件：未设置默认Tap精灵！");
        }

        if (noteData == null)
        {
            Debug.LogError($"[{noteData.NoteIndex}] Tap组件：NoteData未赋值！");
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
            Debug.Log($"[{noteData.NoteIndex}] Tap组件：谱面加载完成，指令初始化完成，显示音符");
        }

        if (hasSwitchedSprite)
        {
            SyncPosition();
            return;
        }

        ExecuteShiftCommands(currentTime);
        ExecuteMoveCommands(currentTime);
        ExecuteDropToJudge(currentTime);

        if (dropToCommand != null && dropToCommand.judgeResult != JudgeResult.None)
        {
            SwitchJudgeSprite(dropToCommand.judgeResult);
            hasSwitchedSprite = true;
            StartCoroutine(DelayDestroyNote());
        }

        SyncPosition();
    }

    private void InitCommands()
    {
        if (noteData.commands == null || noteData.commands.Count == 0)
        {
            Debug.Log($"[{noteData.NoteIndex}] Tap组件：NoteData无关联的Command！");
            return;
        }

        Command cmd = noteData.commands[0];
        dropToCommand = new DropToCommand(noteData, cmd, cmd.key_name);

        if (cmd.x2 != 0 || cmd.y2 != 0)
        {
            shiftCommands.Add(new ShiftCommand(noteData, cmd));
            Debug.Log($"[{gameObject.name}] Tap音符ID:{cmd.num} 初始化Shift指令（目标坐标：{cmd.x2},{cmd.y2}）");
        }

        if (!string.IsNullOrEmpty(cmd.filename))
        {
            moveCommands.Add(new MoveCommand(noteData, cmd.filename));
            Debug.Log($"[{gameObject.name}] Tap音符ID:{cmd.num} 初始化Move指令（JSON路径：{cmd.filename}）");
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
        }
    }

    private void SyncPosition()
    {
        if (noteData == null) return;
        transform.position = new Vector2(noteData.x, noteData.y);
    }

    private IEnumerator DelayDestroyNote()
    {
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }

    private void SwitchJudgeSprite(JudgeResult judgeResult)
    {
        Sprite targetSprite = judgeResult switch
        {
            JudgeResult.Perfect => perfectTapSprite,
            JudgeResult.Good => goodTapSprite,
            JudgeResult.Bad => badTapSprite,
            JudgeResult.Miss => missTapSprite,
            _ => defaultTapSprite
        };

        if (targetSprite != null)
            spriteRenderer.sprite = targetSprite;
        else
        {
            Debug.LogError($"[{gameObject.name}] Tap组件：{judgeResult}对应的精灵未赋值！");
            spriteRenderer.sprite = defaultTapSprite;
        }
    }

    void OnDestroy()
    {
        StopAllCoroutines();
    }
}