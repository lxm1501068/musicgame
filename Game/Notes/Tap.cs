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
    private bool isCommandsInitialized = false;

    // 存储所有指令对象
    private List<ShiftCommand> shiftCommands = new List<ShiftCommand>();
    private List<MoveCommand> moveCommands = new List<MoveCommand>();
    private DropToCommand dropToCommand;  // 假设只有一个 DropTo 指令

    // 第一个指令的开始时间（用于显示控制）
    private float firstCommandStartTime;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"[{noteData.NoteIndex}] Tap组件：未找到SpriteRenderer组件！");
            enabled = false;
            return;
        }

        spriteRenderer.enabled = false; // 初始隐藏

        if (defaultTapSprite != null)
            spriteRenderer.sprite = defaultTapSprite;
        else
            Debug.LogWarning($"[{noteData.NoteIndex}] Tap组件：未设置默认Tap精灵！");

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

        // 首次初始化所有指令
        if (!isCommandsInitialized)
        {
            InitAllCommands();
            transform.position = new Vector2(noteData.x, noteData.y);
            isCommandsInitialized = true;

            if (currentTime >= firstCommandStartTime)
                spriteRenderer.enabled = true;
        }

        // 显示控制：未到时隐藏并跳过后续逻辑
        if (!spriteRenderer.enabled)
        {
            if (currentTime >= firstCommandStartTime)
                spriteRenderer.enabled = true;
            else
                return;
        }

        // 已切换判定精灵，只同步位置
        if (hasSwitchedSprite)
        {
            SyncPosition();
            return;
        }

        // 执行所有激活的指令（根据各自 startTime 过滤）
        ExecuteCommands(currentTime);

        // 如果 DropTo 已判定完成，切换精灵并准备销毁
        if (dropToCommand != null && dropToCommand.judgeResult != JudgeResult.None)
        {
            Debug.Log($"[{gameObject.name}] DropTo判定完成，结果={dropToCommand.judgeResult}，切换精灵并准备销毁");
            SwitchJudgeSprite(dropToCommand.judgeResult);
            hasSwitchedSprite = true;
            StartCoroutine(DelayDestroyNote());
        }

        SyncPosition();
    }

    private void InitAllCommands()
    {
        if (noteData.commands == null || noteData.commands.Count == 0)
        {
            Debug.Log($"[{noteData.NoteIndex}] Tap组件：NoteData无关联的Command！");
            firstCommandStartTime = 0f;
            spriteRenderer.enabled = true;
            return;
        }

        // 记录第一个指令的开始时间（用于显示）
        firstCommandStartTime = noteData.commands[0].timeA;

        // 遍历所有 Command
        foreach (var cmd in noteData.commands)
        {
            // 根据命令特征创建对应指令对象
            // 注意：这里假设 DropTo 是第一个且唯一，但也可通过其他方式识别（如 cmd.type）
            if (dropToCommand == null && cmd.timeB > cmd.timeA) // 简单用 timeB 存在作为 DropTo 标志
            {
                dropToCommand = new DropToCommand(noteData, cmd, cmd.key_name);
            }

            // 如果有终点坐标，视为 Shift 指令
            if (cmd.x2 != 0 || cmd.y2 != 0)
            {
                shiftCommands.Add(new ShiftCommand(noteData, cmd));
                Debug.Log($"[{gameObject.name}] 添加Shift指令，timeA={cmd.timeA}, timeB={cmd.timeB}");
            }

            // 如果有文件名，视为 Move 指令
            if (!string.IsNullOrEmpty(cmd.json_filename))
            {
                moveCommands.Add(new MoveCommand(noteData, cmd)); // 使用修改后的构造函数
                Debug.Log($"[{gameObject.name}] 添加Move指令，timeA={cmd.timeA}");
            }
        }

        // 如果没有找到 DropTo，可能需要报错
        if (dropToCommand == null)
            Debug.LogWarning($"[{gameObject.name}] 未找到 DropTo 指令！");
    }

    private void ExecuteCommands(float currentTime)
    {
        // 执行 Shift 指令（内部已检查 startTime）
        foreach (var shiftCmd in shiftCommands)
            shiftCmd.UpdateNotePosition(currentTime, Time.deltaTime);

        // 执行 Move 指令（内部已检查 startTime）
        foreach (var moveCmd in moveCommands)
            moveCmd.UpdateNotePosition(currentTime);

        // 执行 DropTo 判定（内部已检查 startTime）
        if (dropToCommand != null)
        {
            // KeyIndex 来自 cmd.key_name，已在构造函数中传入
            dropToCommand.Judge(currentTime, dropToCommand.KeyIndex);
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