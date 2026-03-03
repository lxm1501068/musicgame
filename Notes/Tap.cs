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
    // 保留Shift/Move指令缓存（Tap需要支持这两类指令）
    private DropToCommand dropToCommand;
    private List<ShiftCommand> shiftCommands = new List<ShiftCommand>();
    private List<MoveCommand> moveCommands = new List<MoveCommand>();
    
    // 新增：标记是否已完成指令初始化（依赖谱面加载解析完成）
    private bool isCommandsInitialized = false;

    void Awake()
    {
        // Awake仅初始化SpriteRenderer，不执行依赖谱面的逻辑
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (spriteRenderer == null)
        {
            Debug.LogError($"[{gameObject.name}] Tap组件：未找到SpriteRenderer组件！");
            enabled = false;
            return;
        }

        if (defaultTapSprite != null)
        {
            spriteRenderer.sprite = defaultTapSprite;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] Tap组件：未设置默认Tap精灵！");
        }

        // NoteData校验保留（但指令初始化延迟）
        if (noteData == null)
        {
            Debug.LogError($"[{gameObject.name}] Tap组件：NoteData未赋值！");
            enabled = false;
            return;
        }
    }

    void Update()
    {
        // 1. 基础校验：GameManager未初始化则直接返回
        if (GameManager.Instance == null) return;
        
        // 2. 核心检测：谱面未加载解析完成（CurrentPlayTime=-1）则返回，不执行任何逻辑
        float currentTime = GameManager.Instance.CurrentPlayTime;
        if (currentTime == -1)
        {
            return;
        }

        // 3. 指令初始化：仅在首次检测到谱面加载完成后执行一次
        if (!isCommandsInitialized)
        {
            InitCommands();
            // 初始化完成后设置初始位置（从NoteData读取）
            transform.position = new Vector2(noteData.x, noteData.y);
            isCommandsInitialized = true;
            Debug.Log($"[{gameObject.name}] Tap组件：谱面加载完成，指令初始化完成");
        }

        // 4. 精灵已切换（判定完成）：仅同步位置
        if (hasSwitchedSprite)
        {
            SyncPosition();
            return;
        }

        // 5. 执行所有指令（仅在谱面加载完成后执行）
        ExecuteShiftCommands(currentTime);
        ExecuteMoveCommands(currentTime);
        ExecuteDropToJudge(currentTime);

        // 6. 检测判定结果（仅在谱面加载完成后执行）
        if (dropToCommand != null && dropToCommand.judgeResult != JudgeResult.None)
        {
            SwitchJudgeSprite(dropToCommand.judgeResult);
            hasSwitchedSprite = true;
            StartCoroutine(DelayDestroyNote());
        }

        // 7. 同步坐标
        SyncPosition();
    }

    // 初始化指令：移除cmd.type检测（ChartRunner保证仅传入Tap/DTap的Command）
    // 直接解析Command中的Shift/Move/DropTo逻辑（Tap/DTap的x1/x2/y1/y2对应Shift，filename对应Move）
    private void InitCommands()
    {
        if (noteData.commands == null || noteData.commands.Count == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] Tap组件：NoteData无关联的Command！");
            return;
        }

        // ChartRunner保证每个Tap音符仅绑定一个首次出现的Command，直接取第一个
        Command cmd = noteData.commands[0];

        // 1. 初始化DropTo指令（Tap/DTap核心判定逻辑）
        // 修正：Command中是key_name字段，而非keyIndex
        dropToCommand = new DropToCommand(noteData, cmd, cmd.key_name);

        // 2. 初始化Shift指令（Tap支持x1/x2/y1/y2的移动逻辑）
        // 仅当x2/y2有有效值时初始化（避免空移动）
        if (cmd.x2 != 0 || cmd.y2 != 0)
        {
            shiftCommands.Add(new ShiftCommand(noteData, cmd));
            Debug.Log($"[{gameObject.name}] Tap音符ID:{cmd.num} 初始化Shift指令（目标坐标：{cmd.x2},{cmd.y2}）");
        }

        // 3. 初始化Move指令（Tap支持JSON帧移动）
        // 仅当filename不为空时初始化
        if (!string.IsNullOrEmpty(cmd.filename))
        {
            moveCommands.Add(new MoveCommand(noteData, cmd.filename));
            Debug.Log($"[{gameObject.name}] Tap音符ID:{cmd.num} 初始化Move指令（JSON路径：{cmd.filename}）");
        }
    }

    // 执行Shift指令（保留，Tap需要支持）
    private void ExecuteShiftCommands(float currentTime)
    {
        foreach (var shiftCmd in shiftCommands)
        {
            shiftCmd.UpdateNotePosition(currentTime, Time.deltaTime);
        }
    }

    // 执行Move指令（保留，Tap需要支持）
    private void ExecuteMoveCommands(float currentTime)
    {
        foreach (var moveCmd in moveCommands)
        {
            moveCmd.UpdateNotePosition(currentTime);
        }
    }

    // 执行DropTo判定（修正KeyIndex映射：使用Command的key_name）
    private void ExecuteDropToJudge(float currentTime)
    {
        if (dropToCommand != null && noteData.commands.Count > 0)
        {
            Command cmd = noteData.commands[0];
            dropToCommand.Judge(currentTime, cmd.key_name);
        }
    }

    // 同步坐标（Tap的位置由Shift/Move指令修改NoteData后同步）
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
        {
            spriteRenderer.sprite = targetSprite;
        }
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