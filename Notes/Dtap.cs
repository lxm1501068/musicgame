using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteRenderer))]
public class Dtap : MonoBehaviour
{
    [Header("核心关联")]
    public NoteData noteData;

    [Header("Tap精灵配置（Tap1/Tap2共用）")]
    public Sprite tapDefaultSprite;
    public Sprite tapPerfectSprite;
    public Sprite tapGoodSprite;
    public Sprite tapBadSprite;
    public Sprite tapMissSprite;

    [Header("位置偏移")]
    public Vector2 tap1Offset = new Vector2(-0.5f, 0);
    public Vector2 tap2Offset = new Vector2(0.5f, 0);

    [Header("判定规则")]
    public float secondJudgeWindow = 0.3f;
    public float destroyDelay = 0.2f;

    // 指令缓存
    private DropToCommand dropToCommand;
    private List<ShiftCommand> shiftCommands = new List<ShiftCommand>();
    private List<MoveCommand> moveCommands = new List<MoveCommand>();

    // 双Tap视觉组件
    private GameObject tap1Obj;
    private GameObject tap2Obj;
    private SpriteRenderer tap1Renderer;
    private SpriteRenderer tap2Renderer;

    // 判定状态
    private bool hasCompletedJudge = false;
    private bool isTap1Judged = false;
    private bool isTap2Judged = false;
    private float tap1JudgeTime = 0f;
    private JudgeResult tap1Result = JudgeResult.None;
    private JudgeResult tap2Result = JudgeResult.None;

    // 新增：标记是否已完成指令初始化（依赖谱面加载解析完成）
    private bool isCommandsInitialized = false;

    private Coroutine destroyCoroutine;

    void Awake()
    {
        // 核心依赖校验
        if (noteData == null)
        {
            Debug.LogError($"[{gameObject.name}] Dtap组件：NoteData未赋值！");
            enabled = false;
            return;
        }

        // 创建双Tap视觉物体（仅初始化视觉组件，指令和位置初始化延迟）
        CreateTapNoteObjects();

        // 初始化精灵（仅视觉层，判定逻辑延迟）
        ResetTapSprites();
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
            Debug.Log($"[{gameObject.name}] Dtap组件：谱面加载完成，指令初始化完成");
        }

        // 4. 判定已完成：仅同步位置
        if (hasCompletedJudge)
        {
            SyncPosition();
            return;
        }

        // 5. 执行所有指令（仅在谱面加载完成后执行）
        ExecuteShiftCommands(currentTime);
        ExecuteMoveCommands(currentTime);

        // 6. 双Tap判定逻辑
        if (!isTap1Judged)
        {
            ExecuteDropToJudge(currentTime);
            if (dropToCommand != null && dropToCommand.judgeResult != JudgeResult.None)
            {
                tap1Result = dropToCommand.judgeResult;
                isTap1Judged = true;
                tap1JudgeTime = currentTime;
                Debug.Log($"[{gameObject.name}] Tap1判定结果：{tap1Result} | 时间：{currentTime:F2}s");
            }
        }
        else if (isTap1Judged && !isTap2Judged)
        {
            CheckTap2Judge(currentTime);
        }

        // 7. 两次判定完成后处理
        if (isTap1Judged && isTap2Judged && !hasCompletedJudge)
        {
            SwitchJudgeSprites();
            hasCompletedJudge = true;
            destroyCoroutine = StartCoroutine(DelayDestroyNote());
        }

        // 8. 同步坐标
        SyncPosition();
    }

    #region 指令体系
    /// <summary>
    /// 初始化指令：移除NoteType筛选，直接解析Command
    /// </summary>
    private void InitCommands()
    {
        if (noteData.commands == null || noteData.commands.Count == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] Dtap组件：NoteData无关联的Command！");
            return;
        }

        // 取第一个Command
        Command cmd = noteData.commands[0];

        // 1. 初始化DropTo指令
        dropToCommand = new DropToCommand(noteData, cmd, cmd.key_name);

        // 2. 初始化Shift指令
        if (cmd.x2 != 0 || cmd.y2 != 0)
        {
            shiftCommands.Add(new ShiftCommand(noteData, cmd));
            Debug.Log($"[{gameObject.name}] Dtap音符ID:{cmd.num} 初始化Shift指令（目标坐标：{cmd.x2},{cmd.y2}）");
        }

        // 3. 初始化Move指令
        if (!string.IsNullOrEmpty(cmd.filename))
        {
            moveCommands.Add(new MoveCommand(noteData, cmd.filename));
            Debug.Log($"[{gameObject.name}] Dtap音符ID:{cmd.num} 初始化Move指令（JSON路径：{cmd.filename}）");
        }

        // 指令校验
        if (dropToCommand == null)
        {
            Debug.LogError($"[{gameObject.name}] Dtap组件：未解析到DropTo指令！");
            enabled = false;
        }
    }

    /// <summary>
    /// 执行Shift指令
    /// </summary>
    private void ExecuteShiftCommands(float currentTime)
    {
        foreach (var shiftCmd in shiftCommands)
        {
            shiftCmd.UpdateNotePosition(currentTime, Time.deltaTime);
        }
    }

    /// <summary>
    /// 执行Move指令
    /// </summary>
    private void ExecuteMoveCommands(float currentTime)
    {
        foreach (var moveCmd in moveCommands)
        {
            moveCmd.UpdateNotePosition(currentTime);
        }
    }

    /// <summary>
    /// 执行DropTo判定
    /// </summary>
    private void ExecuteDropToJudge(float currentTime)
    {
        if (dropToCommand != null && noteData.commands.Count > 0)
        {
            Command cmd = noteData.commands[0];
            dropToCommand.Judge(currentTime, cmd.key_name);
        }
    }

    /// <summary>
    /// 同步坐标
    /// </summary>
    private void SyncPosition()
    {
        if (noteData == null) return;
        transform.position = new Vector2(noteData.x, noteData.y);
        
        // 同步子Tap偏移
        if (tap1Obj != null) tap1Obj.transform.localPosition = tap1Offset;
        if (tap2Obj != null) tap2Obj.transform.localPosition = tap2Offset;
    }
    #endregion

    #region 双Tap视觉与判定
    /// <summary>
    /// 创建双Tap视觉物体
    /// </summary>
    private void CreateTapNoteObjects()
    {
        // 创建Tap1
        tap1Obj = new GameObject($"Dtap_Tap1_Key{noteData.KeyIndex}");
        tap1Obj.transform.SetParent(transform);
        tap1Obj.transform.localPosition = tap1Offset;
        tap1Renderer = tap1Obj.AddComponent<SpriteRenderer>();
        tap1Renderer.sortingOrder = 1;

        // 创建Tap2
        tap2Obj = new GameObject($"Dtap_Tap2_Key{noteData.KeyIndex}");
        tap2Obj.transform.SetParent(transform);
        tap2Obj.transform.localPosition = tap2Offset;
        tap2Renderer = tap2Obj.AddComponent<SpriteRenderer>();
        tap2Renderer.sortingOrder = 1;

        // 精灵配置校验
        ValidateSpriteConfig();
    }

    /// <summary>
    /// 校验精灵配置
    /// </summary>
    private void ValidateSpriteConfig()
    {
        if (tapDefaultSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap默认精灵未配置！");
        if (tapPerfectSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap Perfect精灵未配置！");
        if (tapGoodSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap Good精灵未配置！");
        if (tapBadSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap Bad精灵未配置！");
        if (tapMissSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap Miss精灵未配置！");
    }

    /// <summary>
    /// 重置精灵为默认状态
    /// </summary>
    private void ResetTapSprites()
    {
        if(tap1Renderer != null) tap1Renderer.sprite = tapDefaultSprite;
        if(tap2Renderer != null) tap2Renderer.sprite = tapDefaultSprite;
    }

    /// <summary>
    /// 检测第二个Tap的判定
    /// </summary>
    private void CheckTap2Judge(float currentTime)
    {
        float timeSinceTap1 = currentTime - tap1JudgeTime;

        // 超出判定窗口 → Miss
        if (timeSinceTap1 > secondJudgeWindow)
        {
            tap2Result = JudgeResult.Miss;
            isTap2Judged = true;
            Debug.Log($"[{gameObject.name}] Tap2判定：Miss（超出{secondJudgeWindow}秒窗口）");
            return;
        }

        // 检测按键按下
        if (noteData.commands.Count > 0)
        {
            Command cmd = noteData.commands[0];
            bool isKeyPressed = InputManager.Instance.IsGroupPressed(cmd.key_name);
            if (isKeyPressed)
            {
                tap2Result = JudgeResult.Perfect;
                isTap2Judged = true;
                Debug.Log($"[{gameObject.name}] Tap2判定：Perfect | 距Tap1：{timeSinceTap1:F2}s");
            }
        }
    }

    /// <summary>
    /// 切换判定精灵（Tap1/Tap2共用同一套Sprite）
    /// </summary>
    private void SwitchJudgeSprites()
    {
        // 根据判定结果获取对应的精灵
        Sprite targetSprite = tap1Result switch
        {
            JudgeResult.Perfect => tapPerfectSprite,
            JudgeResult.Good => tapGoodSprite,
            JudgeResult.Bad => tapBadSprite,
            JudgeResult.Miss => tapMissSprite,
            _ => tapDefaultSprite
        };

        // 给两个Tap设置相同的精灵
        if(tap1Renderer != null) tap1Renderer.sprite = targetSprite;
        if(tap2Renderer != null) tap2Renderer.sprite = targetSprite;

        // 精灵空值容错
        if (tap1Renderer?.sprite == null)
        {
            Debug.LogError($"[{gameObject.name}] Dtap组件：{tap1Result}对应的精灵未赋值！");
            ResetTapSprites();
        }
    }
    #endregion

    #region 销毁逻辑
    private IEnumerator DelayDestroyNote()
    {
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (destroyCoroutine != null)
        {
            StopCoroutine(destroyCoroutine);
        }
        StopAllCoroutines();
    }
    #endregion

    /// <summary>
    /// 重置DTap状态
    /// </summary>
    public void ResetDtapState()
    {
        if (destroyCoroutine != null)
        {
            StopCoroutine(destroyCoroutine);
            destroyCoroutine = null;
        }

        hasCompletedJudge = false;
        isTap1Judged = false;
        isTap2Judged = false;
        tap1JudgeTime = 0f;
        tap1Result = JudgeResult.None;
        tap2Result = JudgeResult.None;
        isCommandsInitialized = false; // 重置指令初始化标记

        if (dropToCommand != null)
        {
            dropToCommand.judgeResult = JudgeResult.None;
        }

        ResetTapSprites();
        transform.position = new Vector2(noteData.x, noteData.y);
        gameObject.SetActive(true);
    }
}