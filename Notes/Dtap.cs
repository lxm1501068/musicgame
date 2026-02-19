using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteRenderer))]
public class Dtap : MonoBehaviour
{
    [Header("核心关联")]
    public NoteData noteData;

    [Header("第一个Tap配置")]
    public Sprite tap1DefaultSprite;
    public Sprite tap1PerfectSprite;
    public Sprite tap1GoodSprite;
    public Sprite tap1BadSprite;
    public Sprite tap1MissSprite;
    public Vector2 tap1Offset = new Vector2(-0.5f, 0);

    [Header("第二个Tap配置")]
    public Sprite tap2DefaultSprite;
    public Sprite tap2PerfectSprite;
    public Sprite tap2GoodSprite;
    public Sprite tap2BadSprite;
    public Sprite tap2MissSprite;
    public Vector2 tap2Offset = new Vector2(0.5f, 0);

    [Header("判定规则")]
    public float secondJudgeWindow = 0.3f;
    public float destroyDelay = 0.2f;

    // 指令缓存（完全对齐Tap.cs）
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

    private Coroutine destroyCoroutine;

    void Awake()
    {
        // 核心依赖校验（对齐Tap.cs）
        if (noteData == null)
        {
            Debug.LogError($"[{gameObject.name}] Dtap组件：NoteData未赋值！");
            enabled = false;
            return;
        }

        // 初始化位置（对齐Tap.cs）
        transform.position = new Vector2(noteData.x, noteData.y);

        // 初始化指令（完全复用Tap.cs的InitCommands逻辑）
        InitCommands();

        // 创建双Tap视觉物体（DTap独有逻辑）
        CreateTapNoteObjects();

        // 初始化精灵（DTap独有）
        ResetTapSprites();
    }

    void Update()
    {
        // 判定完成后仅同步位置（对齐Tap.cs的hasSwitchedSprite逻辑）
        if (hasCompletedJudge)
        {
            SyncPosition();
            return;
        }

        if (GameManager.Instance == null) return;
        float currentTime = GameManager.Instance.CurrentPlayTime;

        // 执行指令（完全对齐Tap.cs的执行顺序）
        ExecuteShiftCommands(currentTime);
        ExecuteMoveCommands(currentTime);

        // 双Tap判定逻辑（DTap核心扩展）
        if (!isTap1Judged)
        {
            ExecuteDropToJudge(currentTime); // 复用Tap的DropTo判定逻辑
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
            CheckTap2Judge(currentTime); // DTap独有：第二个Tap判定
        }

        // 两次判定完成后处理（对齐Tap.cs的精灵切换+延迟销毁）
        if (isTap1Judged && isTap2Judged && !hasCompletedJudge)
        {
            SwitchJudgeSprites();
            hasCompletedJudge = true;
            destroyCoroutine = StartCoroutine(DelayDestroyNote());
        }

        // 同步坐标（完全对齐Tap.cs）
        SyncPosition();
    }

    #region 指令体系（完全模仿Tap.cs）
    /// <summary>
    /// 初始化指令：移除NoteType筛选，直接解析Command（和Tap.cs完全一致）
    /// </summary>
    private void InitCommands()
    {
        if (noteData.commands == null || noteData.commands.Count == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] Dtap组件：NoteData无关联的Command！");
            return;
        }

        // 取第一个Command（ChartRunner保证仅绑定首次出现的指令）
        Command cmd = noteData.commands[0];

        // 1. 初始化DropTo指令（核心判定）
        dropToCommand = new DropToCommand(noteData, cmd, cmd.key_name);

        // 2. 初始化Shift指令（x2/y2非0时）
        if (cmd.x2 != 0 || cmd.y2 != 0)
        {
            shiftCommands.Add(new ShiftCommand(noteData, cmd));
            Debug.Log($"[{gameObject.name}] Dtap音符ID:{cmd.num} 初始化Shift指令（目标坐标：{cmd.x2},{cmd.y2}）");
        }

        // 3. 初始化Move指令（filename非空时）
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
    /// 执行Shift指令（完全对齐Tap.cs）
    /// </summary>
    private void ExecuteShiftCommands(float currentTime)
    {
        foreach (var shiftCmd in shiftCommands)
        {
            shiftCmd.UpdatePosition(currentTime, Time.deltaTime);
        }
    }

    /// <summary>
    /// 执行Move指令（完全对齐Tap.cs）
    /// </summary>
    private void ExecuteMoveCommands(float currentTime)
    {
        foreach (var moveCmd in moveCommands)
        {
            moveCmd.UpdatePosition(currentTime);
        }
    }

    /// <summary>
    /// 执行DropTo判定（完全模仿Tap.cs的ExecuteDropToJudge）
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
    /// 同步坐标（完全对齐Tap.cs）
    /// </summary>
    private void SyncPosition()
    {
        if (noteData == null) return;
        transform.position = new Vector2(noteData.x, noteData.y);
        
        // 同步子Tap偏移（DTap独有扩展）
        if (tap1Obj != null) tap1Obj.transform.localPosition = tap1Offset;
        if (tap2Obj != null) tap2Obj.transform.localPosition = tap2Offset;
    }
    #endregion

    #region 双Tap视觉与判定（DTap独有逻辑）
    /// <summary>
    /// 创建双Tap视觉物体（DTap独有）
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
    /// 校验精灵配置（DTap独有）
    /// </summary>
    private void ValidateSpriteConfig()
    {
        if (tap1DefaultSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap1默认精灵未配置！");
        if (tap1PerfectSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap1 Perfect精灵未配置！");
        if (tap1GoodSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap1 Good精灵未配置！");
        if (tap1BadSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap1 Bad精灵未配置！");
        if (tap1MissSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap1 Miss精灵未配置！");
        if (tap2DefaultSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap2默认精灵未配置！");
        if (tap2PerfectSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap2 Perfect精灵未配置！");
        if (tap2GoodSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap2 Good精灵未配置！");
        if (tap2BadSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap2 Bad精灵未配置！");
        if (tap2MissSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap2 Miss精灵未配置！");
    }

    /// <summary>
    /// 重置精灵为默认状态（DTap独有）
    /// </summary>
    private void ResetTapSprites()
    {
        tap1Renderer.sprite = tap1DefaultSprite;
        tap2Renderer.sprite = tap2DefaultSprite;
    }

    /// <summary>
    /// 检测第二个Tap的判定（DTap独有）
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

        // 检测按键按下（复用Command的key_name）
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
    /// 切换判定精灵（模仿Tap.cs的SwitchJudgeSprite，扩展双Tap逻辑）
    /// </summary>
    private void SwitchJudgeSprites()
    {
        // Tap1精灵切换（完全模仿Tap.cs的SwitchJudgeSprite）
        tap1Renderer.sprite = tap1Result switch
        {
            JudgeResult.Perfect => tap1PerfectSprite,
            JudgeResult.Good => tap1GoodSprite,
            JudgeResult.Bad => tap1BadSprite,
            JudgeResult.Miss => tap1MissSprite,
            _ => tap1DefaultSprite
        };

        // Tap2复用Tap1的精灵样式（DTap视觉逻辑）
        tap2Renderer.sprite = tap1Result switch
        {
            JudgeResult.Perfect => tap2PerfectSprite,
            JudgeResult.Good => tap2GoodSprite,
            JudgeResult.Bad => tap2BadSprite,
            JudgeResult.Miss => tap2MissSprite,
            _ => tap2DefaultSprite
        };

        // 精灵空值容错（模仿Tap.cs）
        if (tap1Renderer.sprite == null)
        {
            Debug.LogError($"[{gameObject.name}] Dtap组件：Tap1 {tap1Result}对应的精灵未赋值！");
            tap1Renderer.sprite = tap1DefaultSprite;
        }
        if (tap2Renderer.sprite == null)
        {
            Debug.LogError($"[{gameObject.name}] Dtap组件：Tap2 {tap1Result}对应的精灵未赋值！");
            tap2Renderer.sprite = tap2DefaultSprite;
        }
    }
    #endregion

    #region 销毁逻辑（完全模仿Tap.cs）
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
    /// 重置DTap状态（重玩时调用，扩展方法）
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

        if (dropToCommand != null)
        {
            dropToCommand.judgeResult = JudgeResult.None;
        }

        ResetTapSprites();
        transform.position = new Vector2(noteData.x, noteData.y);
        gameObject.SetActive(true);
    }
}