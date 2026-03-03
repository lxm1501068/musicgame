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

    // 保留指令缓存（Flick需要支持Shift/Move/DropTo指令）
    private DropToCommand dropToCommand;
    private List<ShiftCommand> shiftCommands = new List<ShiftCommand>();
    private List<MoveCommand> moveCommands = new List<MoveCommand>();

    // 判定阈值（复用Tap风格的写法）
    private float perfectThreshold => 0.1f;
    private float goodThreshold => 0.2f;
    private float badThreshold => 0.3f;

    // 新增：标记是否已完成指令初始化（依赖谱面加载解析完成）
    private bool isCommandsInitialized = false;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (spriteRenderer == null)
        {
            Debug.LogError($"[{gameObject.name}] Flick组件：未找到SpriteRenderer组件！");
            enabled = false;
            return;
        }

        if (defaultFlickSprite != null)
        {
            spriteRenderer.sprite = defaultFlickSprite;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] Flick组件：未设置默认Flick精灵！");
        }

        // 校验有效按键组
        if (validOtherKeyIndices.Count == 0)
        {
            validOtherKeyIndices = new List<int>() { 1, 2, 3 };
            Debug.LogWarning($"[{gameObject.name}] Flick组件：有效按键组为空，默认添加1/2/3");
        }
        validOtherKeyIndices.RemoveAll(x => x == 11); // 移除11号键（Flick核心键）

        // NoteData校验
        if (noteData == null)
        {
            Debug.LogError($"[{gameObject.name}] Flick组件：NoteData未赋值！");
            enabled = false;
            return;
        }

        // 移除Awake中的InitCommands调用，延迟到Update中谱面加载完成后执行
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
            Debug.Log($"[{gameObject.name}] Flick组件：谱面加载完成，指令初始化完成");
        }

        if (hasSwitchedSprite)
        {
            SyncPosition();
            return;
        }

        // 4. 执行所有指令（仅在谱面加载完成后执行）
        ExecuteShiftCommands(currentTime);
        ExecuteMoveCommands(currentTime);
        ExecuteDropToJudge(currentTime);

        // 5. 检测Flick核心判定（仅在谱面加载完成后执行）
        CheckFlickJudge(currentTime);

        // 6. 判定完成后切换精灵 + 启动延迟销毁（模仿Tap的逻辑）
        if (isJudged && !hasSwitchedSprite)
        {
            SwitchJudgeSprite(judgeResult);
            hasSwitchedSprite = true;
            StartCoroutine(DelayDestroyNote());
        }

        SyncPosition();
    }

    // 初始化指令（完全模仿Tap的InitCommands风格）
    private void InitCommands()
    {
        if (noteData.commands == null || noteData.commands.Count == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] Flick组件：NoteData无关联的Command！");
            return;
        }

        // 模仿Tap：取第一个Command（ChartRunner保证仅绑定一个首次出现的Command）
        Command cmd = noteData.commands[0];

        // 1. 初始化DropTo指令（Flick核心判定逻辑）
        dropToCommand = new DropToCommand(noteData, cmd, noteData.KeyIndex);
        judgeTime = cmd.timeB; // 优先使用指令的endTime作为判定时间
        Debug.Log($"[{gameObject.name}] Flick音符ID:{cmd.num} 初始化DropTo指令（判定时间：{judgeTime}）");

        // 2. 初始化Shift指令（Flick支持x1/x2/y1/y2的移动逻辑）
        if (cmd.x2 != 0 || cmd.y2 != 0)
        {
            shiftCommands.Add(new ShiftCommand(noteData, cmd));
            Debug.Log($"[{gameObject.name}] Flick音符ID:{cmd.num} 初始化Shift指令（目标坐标：{cmd.x2},{cmd.y2}）");
        }

        // 3. 初始化Move指令（Flick支持JSON帧移动）
        if (!string.IsNullOrEmpty(cmd.filename))
        {
            moveCommands.Add(new MoveCommand(noteData, cmd.filename));
            Debug.Log($"[{gameObject.name}] Flick音符ID:{cmd.num} 初始化Move指令（JSON路径：{cmd.filename}）");
        }
    }

    // 执行Shift指令（完全模仿Tap）
    private void ExecuteShiftCommands(float currentTime)
    {
        foreach (var shiftCmd in shiftCommands)
        {
            shiftCmd.UpdateNotePosition(currentTime, Time.deltaTime);
        }
    }

    // 执行Move指令（完全模仿Tap）
    private void ExecuteMoveCommands(float currentTime)
    {
        foreach (var moveCmd in moveCommands)
        {
            moveCmd.UpdateNotePosition(currentTime);
        }
    }

    // 执行DropTo判定（模仿Tap的写法）
    private void ExecuteDropToJudge(float currentTime)
    {
        if (dropToCommand != null && noteData.commands.Count > 0)
        {
            Command cmd = noteData.commands[0];
            dropToCommand.Judge(currentTime, cmd.key_name);

            // 同步DropTo指令的判定结果到Flick
            if (dropToCommand.judgeResult != JudgeResult.None && !isJudged)
            {
                judgeResult = dropToCommand.judgeResult;
                isJudged = true;
            }
        }
    }

    // 同步坐标（完全复用Tap的SyncPosition逻辑）
    private void SyncPosition()
    {
        if (noteData == null) return;
        transform.position = new Vector2(noteData.x, noteData.y);
    }

    // Flick核心判定逻辑（封装为独立方法）
    private void CheckFlickJudge(float currentTime)
    {
        // 若DropTo指令已判定，跳过自定义判定
        if (dropToCommand != null && dropToCommand.judgeResult != JudgeResult.None) return;

        float timeDiff = currentTime - judgeTime;
        float absTimeDiff = Mathf.Abs(timeDiff);

        // 超出判定窗口 → Miss
        if (absTimeDiff > badThreshold)
        {
            SetJudgeResult(JudgeResult.Miss, timeDiff);
            return;
        }

        // 检测Flick按键条件（11+其他按键）
        bool isKey11Triggered = IsKeyTriggered(11);
        bool isOtherKeyTriggered = IsAnyOtherKeyTriggered();

        // 按键条件满足 → 判定等级
        if (isKey11Triggered && isOtherKeyTriggered)
        {
            JudgeResult result = GetJudgeResultByTimeDiff(absTimeDiff);
            SetJudgeResult(result, timeDiff);
        }
    }

    // 检测单个按键是否触发
    private bool IsKeyTriggered(int keyIndex)
    {
        return InputManager.Instance.IsGroupPressed(keyIndex) || InputManager.Instance.IsGroupHeld(keyIndex);
    }

    // 检测其他有效按键是否触发
    private bool IsAnyOtherKeyTriggered()
    {
        foreach (int keyIndex in validOtherKeyIndices)
        {
            if (IsKeyTriggered(keyIndex)) return true;
        }
        return false;
    }

    // 根据时间差获取判定结果
    private JudgeResult GetJudgeResultByTimeDiff(float absTimeDiff)
    {
        if (absTimeDiff <= perfectThreshold) return JudgeResult.Perfect;
        if (absTimeDiff <= goodThreshold) return JudgeResult.Good;
        if (absTimeDiff <= badThreshold) return JudgeResult.Bad;
        return JudgeResult.Miss;
    }

    // 设置判定结果
    private void SetJudgeResult(JudgeResult result, float timeDiff)
    {
        judgeResult = result;
        isJudged = true;
        Debug.Log($"[{gameObject.name}] Flick判定结果：{result} | 时间差：{timeDiff:F2}s");
    }

    // 切换判定精灵（完全模仿Tap的SwitchJudgeSprite逻辑）
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
        {
            spriteRenderer.sprite = targetSprite;
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] Flick组件：{judgeResult}对应的精灵未赋值！");
            spriteRenderer.sprite = defaultFlickSprite;
        }
    }

    // 延迟销毁（完全复用Tap的DelayDestroyNote逻辑）
    private IEnumerator DelayDestroyNote()
    {
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }

    // 生命周期（模仿Tap的OnDestroy）
    void OnDestroy()
    {
        StopAllCoroutines();
    }
}