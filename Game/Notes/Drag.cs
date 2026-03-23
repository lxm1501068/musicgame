using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteRenderer))]
public class Drag : MonoBehaviour
{
    [Header("核心关联")]
    public NoteData noteData;

    [Header("判定配置")]
    public float perfectThreshold = 0.1f;   // Perfect判定窗口（秒）

    [Header("判定对应的精灵图片")]
    public Sprite defaultDragSprite;
    public Sprite perfectDragSprite;
    public Sprite missDragSprite;

    [Header("销毁设置")]
    public float destroyDelay = 0.2f;

    private SpriteRenderer spriteRenderer;
    private bool hasSwitchedSprite = false;
    private bool isCommandsInitialized = false;

    // 存储所有指令对象
    private List<ShiftCommand> shiftCommands = new List<ShiftCommand>();
    private List<MoveCommand> moveCommands = new List<MoveCommand>();

    // DropTo 指令相关信息（Drag 只使用 Perfect/Miss 判定）
    private float dropEndTime;          // 判定基准时间（即 timeB）
    private int dropKeyIndex;           // 绑定的按键序号
    private float dropStartTime;         // 指令开始时间（用于判定窗口起始）
    private JudgeResult judgeResult = JudgeResult.None;  // 当前判定结果

    // 第一个指令的开始时间（用于显示控制）
    private float firstCommandStartTime;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"[{gameObject.name}] Drag组件：未找到SpriteRenderer组件！");
            enabled = false;
            return;
        }

        spriteRenderer.enabled = false; // 初始隐藏

        if (defaultDragSprite != null)
            spriteRenderer.sprite = defaultDragSprite;
        else
            Debug.LogWarning($"[{gameObject.name}] Drag组件：未设置默认Drag精灵！");

        if (noteData == null)
        {
            Debug.LogError($"[{gameObject.name}] Drag组件：NoteData未赋值！");
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

            // 如果当前时间已到达第一个指令的开始时间，立即显示
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

        // 进行 Drag 判定（仅 Perfect / Miss）
        CheckDragJudge(currentTime);

        // 如果已判定，切换精灵并准备销毁
        if (judgeResult != JudgeResult.None)
        {
            SwitchDragSprite(judgeResult);

            // 更新 UI 显示
            if (JudgeResultDisplay.Instance != null) JudgeResultDisplay.Instance.ShowJudgeResult(judgeResult);
            if (ScoreDisplay.Instance != null) ScoreDisplay.Instance.AddScoreByJudge(judgeResult);
            
            if (ComboDisplay.Instance != null)
            {
                if (judgeResult == JudgeResult.Perfect || judgeResult == JudgeResult.Good)
                    ComboDisplay.Instance.AddCombo();
                else if (judgeResult == JudgeResult.Bad || judgeResult == JudgeResult.Miss)
                    ComboDisplay.Instance.ResetCombo();
            }

            hasSwitchedSprite = true;
            StartCoroutine(DelayDestroyNote());
        }

        SyncPosition();
    }

    /// <summary>
    /// 从 NoteData.commands 初始化所有指令对象，并提取 DropTo 相关信息
    /// </summary>
    private void InitAllCommands()
    {
        if (noteData.commands == null || noteData.commands.Count == 0)
        {
            Debug.Log($"[{noteData.NoteIndex}] Drag组件：NoteData无关联的Command！");
            firstCommandStartTime = 0f;
            spriteRenderer.enabled = true;
            return;
        }

        // 记录第一个指令的开始时间（用于显示）
        firstCommandStartTime = noteData.commands[0].timeA;

        // 标记是否已找到 DropTo 指令
        bool dropToFound = false;

        foreach (var cmd in noteData.commands)
        {
            // 识别 DropTo 指令：我们假定第一个具有 timeB > timeA 且可能无其他特征的指令为 DropTo
            // 实际项目中可通过 cmd.type 或约定来精确判断，这里沿用 Tap.cs 的简单方式
            if (!dropToFound && cmd.timeB > cmd.timeA)
            {
                dropEndTime = cmd.timeB;
                dropKeyIndex = cmd.key_name;          // 假设 Command 中有 key_name 字段
                dropStartTime = cmd.timeA;
                dropToFound = true;

                // 可选：将 DropTo 也视为一个 Shift 指令（如果需要移动的话）
                // 但 Drag 的 DropTo 本身可能不产生移动，只是判定点，所以不添加 ShiftCommand
                // 如果 DropTo 也包含终点坐标，可以按需添加 Shift
                if (cmd.x2 != 0 || cmd.y2 != 0)
                {
                    shiftCommands.Add(new ShiftCommand(noteData, cmd));
                    Debug.Log($"[{gameObject.name}] 添加Shift指令（源自DropTo），timeA={cmd.timeA}, timeB={cmd.timeB}");
                }
            }

            // 如果有终点坐标且不是上面已处理的 DropTo（避免重复），视为 Shift 指令
            if ((cmd.x2 != 0 || cmd.y2 != 0) && !(dropToFound && cmd.timeB == dropEndTime && cmd.timeA == dropStartTime))
            {
                shiftCommands.Add(new ShiftCommand(noteData, cmd));
                Debug.Log($"[{gameObject.name}] 添加Shift指令，timeA={cmd.timeA}, timeB={cmd.timeB}");
            }

            // 如果有文件名，视为 Move 指令
            if (!string.IsNullOrEmpty(cmd.json_filename))
            {
                moveCommands.Add(new MoveCommand(noteData, cmd));
                Debug.Log($"[{gameObject.name}] 添加Move指令，timeA={cmd.timeA}");
            }
        }

        if (!dropToFound)
        {
            Debug.LogWarning($"[{gameObject.name}] 未找到 DropTo 指令，将使用默认判定时间0");
            dropEndTime = 0f;
            dropKeyIndex = noteData.KeyIndex;  // 后备使用 NoteData 的 KeyIndex
            dropStartTime = 0f;
        }
    }

    /// <summary>
    /// 执行所有激活的指令（更新 noteData 中的位置）
    /// </summary>
    private void ExecuteCommands(float currentTime)
    {
        // 执行 Shift 指令（内部已检查 startTime）
        foreach (var shiftCmd in shiftCommands)
            shiftCmd.UpdateNotePosition(currentTime, Time.deltaTime);

        // 执行 Move 指令（内部已检查 startTime）
        foreach (var moveCmd in moveCommands)
            moveCmd.UpdateNotePosition(currentTime);
    }

    /// <summary>
    /// Drag 的判定逻辑：在 perfectThreshold 窗口内，如果按键被按下或按住，则 Perfect，否则 Miss
    /// </summary>
    private void CheckDragJudge(float currentTime)
    {
        if (judgeResult != JudgeResult.None) return;

        // 计算与判定时间的差值
        float timeDiff = currentTime - dropEndTime;
        float absDiff = Mathf.Abs(timeDiff);

        // 检查按键状态：按下或按住都视为有效输入
        bool isKeyActive = InputManager.Instance.IsGroupPressed(dropKeyIndex) ||
                           InputManager.Instance.IsGroupHeld(dropKeyIndex);

        // 在 Perfect 窗口内且按键有效 => Perfect
        if (absDiff <= perfectThreshold && isKeyActive)
        {
            judgeResult = JudgeResult.Perfect;
            Debug.Log($"[{gameObject.name}] Drag判定：Perfect | 时间差：{timeDiff:F2}s");
        }
        // 超过窗口右边界（时间晚于判定点+阈值）且尚未判定 => Miss
        else if (timeDiff > perfectThreshold)
        {
            judgeResult = JudgeResult.Miss;
            Debug.Log($"[{gameObject.name}] Drag判定：Miss | 超出 +{perfectThreshold}秒窗口，时间差：{timeDiff:F2}s");
        }
        // 注意：窗口左边界（提前太多）不判定，等待进入窗口
    }

    /// <summary>
    /// 根据判定结果切换精灵
    /// </summary>
    private void SwitchDragSprite(JudgeResult result)
    {
        Sprite targetSprite = result switch
        {
            JudgeResult.Perfect => perfectDragSprite,
            JudgeResult.Miss => missDragSprite,
            _ => defaultDragSprite
        };

        if (targetSprite != null)
            spriteRenderer.sprite = targetSprite;
        else
        {
            Debug.LogError($"[{gameObject.name}] Drag组件：{result}对应的精灵未赋值！");
            spriteRenderer.sprite = defaultDragSprite;
        }
    }

    /// <summary>
    /// 将 GameObject 位置同步为 noteData 中的坐标
    /// </summary>
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

    void OnDestroy()
    {
        StopAllCoroutines();
    }

    // 对外提供判定结果（可选）
    public JudgeResult GetDragJudgeResult() => judgeResult;
    public bool IsJudged() => judgeResult != JudgeResult.None;
}