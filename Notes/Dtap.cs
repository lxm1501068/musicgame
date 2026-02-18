using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 双Tap（Dtap）音符组件：
/// 1. 由两个独立Tap Note组成，各自独立控制Sprite
/// 2. 第一次判定同普通Tap（Perfect/Good/Bad/Miss）
/// 3. 第二次判定仅两种结果：第一次后0.3秒内按下=Perfect，超时=Miss
/// </summary>
[RequireComponent(typeof(Transform))]
public class Dtap : MonoBehaviour
{
    [Header("核心关联")]
    public NoteData noteData;
    public NoteTools noteTools;
    public Command dropToCmd; // 绑定DropTo指令（提供判定时间/按键/坐标）

    [Header("第一个Tap Note配置")]
    public Sprite tap1DefaultSprite;    // Tap1默认精灵
    public Sprite tap1PerfectSprite;   // Tap1 Perfect精灵
    public Sprite tap1FailSprite;      // Tap1 非Perfect（Good/Bad/Miss）精灵
    public Vector2 tap1Offset = new Vector2(-0.5f, 0); // Tap1位置偏移

    [Header("第二个Tap Note配置")]
    public Sprite tap2DefaultSprite;    // Tap2默认精灵
    public Sprite tap2PerfectSprite;   // Tap2 Perfect（0.3秒内按下）精灵
    public Sprite tap2MissSprite;      // Tap2 Miss（超时）精灵
    public Vector2 tap2Offset = new Vector2(0.5f, 0); // Tap2位置偏移

    [Header("双判定规则")]
    public float secondJudgeWindow = 0.3f; // 第二次判定窗口（固定0.3秒）
    public float destroyDelay = 0.2f;     // 判定完成后延迟销毁时间

    // 双Tap实例（独立物体+SpriteRenderer）
    private GameObject tap1Obj;
    private GameObject tap2Obj;
    private SpriteRenderer tap1Renderer;
    private SpriteRenderer tap2Renderer;

    // 判定状态
    private DropToCommand tap1DropToCmd; // 复用DropTo判定逻辑
    private bool isTap1Judged = false;
    private bool isTap2Judged = false;
    private float tap1JudgeTime = 0f;    // 第一次判定完成时间
    private JudgeResult tap1Result = JudgeResult.None;
    private JudgeResult tap2Result = JudgeResult.None;

    // 辅助变量
    private Coroutine destroyCoroutine;
    private bool hasUpdatedSprite = false;

    void Awake()
    {
        // 核心依赖校验
        if (noteTools == null)
        {
            Debug.LogError($"[{gameObject.name}] Dtap：未引用NoteTools！");
            enabled = false;
            return;
        }
        if (dropToCmd == null)
        {
            Debug.LogError($"[{gameObject.name}] Dtap：未配置DropToCommand参数！");
            enabled = false;
            return;
        }

        // 初始化新版NoteData（仅保留4个字段）
        if (noteData == null)
        {
            noteData = new NoteData
            {
                keyIndex = dropToCmd.num,
                x = dropToCmd.x1,
                y = dropToCmd.y1,
                isVisible = true
            };
        }

        // 创建两个独立的Tap Note物体（带独立SpriteRenderer）
        CreateTapNoteObjects();

        // 初始化Tap1的DropTo判定逻辑（复用NoteTools的判定规则）
        tap1DropToCmd = new DropToCommand(noteData, dropToCmd, noteTools.input);
        noteTools.CreateDropToCommand(noteData, dropToCmd); // 注册到NoteTools驱动移动

        // 初始显示默认精灵
        ResetTapSprites();
    }

    /// <summary>
    /// 创建两个独立的Tap Note物体（各自带SpriteRenderer）
    /// </summary>
    private void CreateTapNoteObjects()
    {
        // 创建Tap1物体
        tap1Obj = new GameObject($"Dtap_Tap1_Key{noteData.keyIndex}");
        tap1Obj.transform.SetParent(transform);
        tap1Obj.transform.localPosition = tap1Offset;
        tap1Renderer = tap1Obj.AddComponent<SpriteRenderer>();
        tap1Renderer.sprite = tap1DefaultSprite;
        tap1Renderer.sortingOrder = 1;

        // 创建Tap2物体
        tap2Obj = new GameObject($"Dtap_Tap2_Key{noteData.keyIndex}");
        tap2Obj.transform.SetParent(transform);
        tap2Obj.transform.localPosition = tap2Offset;
        tap2Renderer = tap2Obj.AddComponent<SpriteRenderer>();
        tap2Renderer.sprite = tap2DefaultSprite;
        tap2Renderer.sortingOrder = 1;

        // 精灵配置校验
        if (tap1DefaultSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap1默认精灵未配置！");
        if (tap2DefaultSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap2默认精灵未配置！");
        if (tap1PerfectSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap1 Perfect精灵未配置！");
        if (tap1FailSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap1 Fail精灵未配置！");
        if (tap2PerfectSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap2 Perfect精灵未配置！");
        if (tap2MissSprite == null) Debug.LogWarning($"[{gameObject.name}] Tap2 Miss精灵未配置！");
    }

    void Update()
    {
        // 两次判定完成/已更新精灵 → 跳过逻辑
        if ((isTap1Judged && isTap2Judged) || hasUpdatedSprite) return;

        float currentMusicTime = noteTools.GetCurrentMusicTime(); // 统一使用NoteTools的音乐时间

        // 第一步：处理Tap1的判定（复用DropToCommand完整规则）
        if (!isTap1Judged)
        {
            CheckTap1Judge(currentMusicTime);
        }
        // 第二步：Tap1判定完成后，处理Tap2的判定（仅0.3秒内按下=Perfect / 超时=Miss）
        else if (isTap1Judged && !isTap2Judged)
        {
            CheckTap2Judge(currentMusicTime);
        }

        // 两次判定完成 → 更新精灵+启动销毁
        if (isTap1Judged && isTap2Judged && !hasUpdatedSprite)
        {
            UpdateTapSprites();
            hasUpdatedSprite = true;
            if (destroyCoroutine == null)
            {
                destroyCoroutine = StartCoroutine(DelayDestroyDtap());
            }
        }

        // 同步NoteData坐标到Dtap本体（让两个Tap跟随移动）
        transform.position = new Vector2(noteData.x, noteData.y);
    }

    #region Tap1判定逻辑（复用DropToCommand的完整规则：Perfect/Good/Bad/Miss）
    private void CheckTap1Judge(float currentTime)
    {
        if (tap1Result != JudgeResult.None) return;

        // 执行DropTo判定逻辑（结果写入tap1DropToCmd.judgeResult）
        tap1DropToCmd.Judge(currentTime);
        tap1Result = tap1DropToCmd.judgeResult;

        // Tap1判定完成，记录时间
        if (tap1Result != JudgeResult.None)
        {
            isTap1Judged = true;
            tap1JudgeTime = currentTime;
            Debug.Log($"[{gameObject.name}] Tap1判定结果：{tap1Result} | 时间：{currentTime:F2}s");
        }
    }
    #endregion

    #region Tap2判定逻辑（核心：0.3秒内按下=Perfect / 超时=Miss）
    private void CheckTap2Judge(float currentTime)
    {
        float timeSinceTap1 = currentTime - tap1JudgeTime;

        // 超出0.3秒窗口 → 直接判定为Miss
        if (timeSinceTap1 > secondJudgeWindow)
        {
            tap2Result = JudgeResult.Miss;
            isTap2Judged = true;
            Debug.Log($"[{gameObject.name}] Tap2判定：Miss（超出{secondJudgeWindow}秒窗口）");
            return;
        }

        // 检测按键按下（和Tap1共用同一个按键）
        bool isKeyPressed = noteTools.input?.IsGroupPressed(noteData.keyIndex) ?? false;
        if (isKeyPressed)
        {
            // 只要0.3秒内按下，无论时间差多少 → 判定为Perfect
            tap2Result = JudgeResult.Perfect;
            isTap2Judged = true;
            Debug.Log($"[{gameObject.name}] Tap2判定：Perfect | 距Tap1：{timeSinceTap1:F2}s");
        }
    }
    #endregion

    #region 精灵更新逻辑（两个Tap独立控制Sprite）
    /// <summary>
    /// 重置两个Tap的精灵为默认状态
    /// </summary>
    private void ResetTapSprites()
    {
        tap1Renderer.sprite = tap1DefaultSprite;
        tap2Renderer.sprite = tap2DefaultSprite;
    }

    /// <summary>
    /// 根据判定结果更新两个Tap的Sprite
    /// </summary>
    private void UpdateTapSprites()
    {
        // Tap1：Perfect显示PerfectSprite，其他（Good/Bad/Miss）显示FailSprite
        tap1Renderer.sprite = tap1Result == JudgeResult.Perfect 
            ? tap1PerfectSprite 
            : tap1FailSprite;

        // Tap2：仅两种状态（Perfect/Miss）
        tap2Renderer.sprite = tap2Result == JudgeResult.Perfect 
            ? tap2PerfectSprite 
            : tap2MissSprite;

        Debug.Log($"[{gameObject.name}] 双Tap精灵更新完成 → Tap1：{tap1Result} | Tap2：{tap2Result}");
    }
    #endregion

    #region 销毁与重置逻辑
    /// <summary>
    /// 延迟销毁Dtap（包含两个Tap子物体）
    /// </summary>
    private IEnumerator DelayDestroyDtap()
    {
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
        Debug.Log($"[{gameObject.name}] Dtap延迟{destroyDelay}秒销毁");
    }

    /// <summary>
    /// 重置Dtap状态（重玩时调用）
    /// </summary>
    public void ResetDtapState()
    {
        // 停止销毁协程
        if (destroyCoroutine != null)
        {
            StopCoroutine(destroyCoroutine);
            destroyCoroutine = null;
        }

        // 重置判定状态
        isTap1Judged = false;
        isTap2Judged = false;
        tap1JudgeTime = 0f;
        tap1Result = JudgeResult.None;
        tap2Result = JudgeResult.None;
        hasUpdatedSprite = false;

        // 重置Tap1的判定指令
        if (tap1DropToCmd != null)
        {
            tap1DropToCmd.judgeResult = JudgeResult.None;
        }

        // 重置精灵和位置
        ResetTapSprites();
        transform.position = new Vector2(dropToCmd.x1, dropToCmd.y1);
        gameObject.SetActive(true);
    }
    #endregion

    #region 快捷创建方法
    /// <summary>
    /// 快速创建Dtap音符（包含两个独立Tap子物体）
    /// </summary>
    public static Dtap CreateDtapNote(Transform parent, Vector2 position, NoteTools noteTools, Command dropToCmd)
    {
        GameObject dtapObj = new GameObject($"Dtap_Key{dropToCmd.num}");
        dtapObj.transform.SetParent(parent);
        dtapObj.transform.position = position;

        Dtap dtap = dtapObj.AddComponent<Dtap>();
        dtap.noteTools = noteTools;
        dtap.dropToCmd = dropToCmd;
        dtap.noteData = new NoteData
        {
            keyIndex = dropToCmd.num,
            x = dropToCmd.x1,
            y = dropToCmd.y1,
            isVisible = true
        };

        return dtap;
    }
    #endregion

    // 销毁时清理协程
    void OnDestroy()
    {
        if (destroyCoroutine != null) StopCoroutine(destroyCoroutine);
    }

    // 兼容旧版Command类（如果项目中未定义，需补充）
    [System.Serializable]
    public class Command
    {
        public float timeA;
        public float timeB;
        public float x1;
        public float y1;
        public float x2;
        public float y2;
        public int num;
    }
}