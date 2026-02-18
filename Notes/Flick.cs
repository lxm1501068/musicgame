using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Flick音符组件：基于NoteTools体系实现，判定窗口±0.3秒内需同时触发按键11 + 其他任意有效按键
/// 核心适配：对齐NoteTools的NoteData结构、InputManager调用、判定阈值体系
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Flick : MonoBehaviour
{
    [Header("核心关联（必须配置）")]
    public NoteData noteData;          // 关联NoteTools的核心音符数据
    public NoteTools noteTools;        // 关联NoteTools管理器（获取InputManager和判定阈值）

    [Header("视觉表现")]
    [Tooltip("未判定时的默认精灵")]
    public Sprite defaultFlickSprite;
    [Tooltip("Perfect判定（±0.1秒）精灵")]
    public Sprite perfectFlickSprite;
    [Tooltip("Good判定（±0.1~±0.2秒）精灵")]
    public Sprite goodFlickSprite;
    [Tooltip("Bad判定（±0.2~±0.3秒）精灵")]
    public Sprite badFlickSprite;
    [Tooltip("Miss判定（超时/按键不满足）精灵")]
    public Sprite missFlickSprite;

    [Header("Flick专属配置")]
    [Tooltip("判定完成后延迟销毁时间（秒）")]
    public float destroyDelay = 0.2f;
    [Tooltip("除按键11外的有效触发按键组")]
    public List<int> validOtherKeyIndices = new List<int>() { 1, 2, 3 };
    [Tooltip("Flick判定基准时间（音乐时间，对应NoteTools的endTime逻辑）")]
    public float judgeTime;            // 替代原NoteData的endTime（新NoteData无该字段）

    // 内部状态
    private bool isJudged = false;                  // 是否完成判定
    private JudgeResult judgeResult = JudgeResult.None; // 判定结果（复用NoteTools的枚举）
    private SpriteRenderer spriteRenderer;          // 精灵渲染器缓存
    private Coroutine destroyCoroutine;             // 延迟销毁协程
    private bool hasSwitchedSprite = false;         // 是否已切换判定结果精灵

    // 判定阈值（复用NoteTools的DropToCommand阈值，保证全局一致）
    private float perfectThreshold => 0.1f; // 与DropToCommand.perfectThreshold对齐
    private float goodThreshold => 0.2f;    // 与DropToCommand.goodThreshold对齐
    private float badThreshold => 0.3f;     // 与DropToCommand.badThreshold对齐

    void Awake()
    {
        // 1. 缓存核心组件 + 安全校验
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"[{gameObject.name}] Flick组件缺失SpriteRenderer！");
            enabled = false;
            return;
        }

        if (noteTools == null)
        {
            Debug.LogError($"[{gameObject.name}] Flick组件未关联NoteTools实例！");
            enabled = false;
            return;
        }

        if (noteTools.input == null)
        {
            Debug.LogError($"[{gameObject.name}] NoteTools未配置InputManager！");
            enabled = false;
            return;
        }

        // 2. 初始化NoteData（保证与NoteTools数据结构一致）
        if (noteData == null)
        {
            noteData = new NoteData()
            {
                NoteIndex = 11, // Flick默认绑定按键11
                x = transform.position.x,
                y = transform.position.y,
                isVisible = true
            };
            Debug.LogWarning($"[{gameObject.name}] 自动创建NoteData，默认绑定按键11");
        }
        else
        {
            // 同步NoteData坐标到物体位置
            transform.position = new Vector2(noteData.x, noteData.y);
        }

        // 3. 校验有效按键组
        if (validOtherKeyIndices.Count == 0)
        {
            validOtherKeyIndices = new List<int>() { 1, 2, 3 };
            Debug.LogWarning($"[{gameObject.name}] 有效按键组为空，默认添加1/2/3");
        }
        validOtherKeyIndices.RemoveAll(x => x == 11); // 强制排除按键11

        // 4. 初始化默认精灵
        if (defaultFlickSprite != null)
        {
            spriteRenderer.sprite = defaultFlickSprite;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 未设置默认Flick精灵");
        }
    }

    void Update()
    {
        // 已判定/已切换精灵 → 跳过逻辑
        if (isJudged || hasSwitchedSprite) return;

        // 获取当前音乐时间（复用NoteTools的时间逻辑，保证全局时间统一）
        float currentMusicTime = noteTools.GetComponent<NoteTools>().GetCurrentMusicTime();
        
        // 执行Flick核心判定
        CheckFlickJudge(currentMusicTime);

        // 判定完成后切换精灵 + 启动延迟销毁
        if (isJudged && !hasSwitchedSprite)
        {
            SwitchFlickSprite();
            hasSwitchedSprite = true;
            StartDelayDestroy();
        }
    }

    #region 核心逻辑：Flick判定（11+其他按键 + 时间窗口）
    /// <summary>
    /// 检测Flick判定条件
    /// </summary>
    /// <param name="currentTime">当前音乐时间（来自NoteTools）</param>
    private void CheckFlickJudge(float currentTime)
    {
        // 计算时间差（当前时间 - 判定基准时间）
        float timeDiff = currentTime - judgeTime;
        float absTimeDiff = Mathf.Abs(timeDiff);

        // 1. 超出±0.3秒判定窗口 → 直接Miss
        if (absTimeDiff > badThreshold)
        {
            SetJudgeResult(JudgeResult.Miss, timeDiff);
            return;
        }

        // 2. 检测按键条件：①按键11按下/按住 ②其他任意有效按键按下/按住
        bool isKey11Triggered = IsKeyTriggered(11);
        bool isOtherKeyTriggered = IsAnyOtherKeyTriggered();

        // 3. 双条件满足 → 判定有效，细分等级
        if (isKey11Triggered && isOtherKeyTriggered)
        {
            JudgeResult result = GetJudgeResultByTimeDiff(absTimeDiff);
            SetJudgeResult(result, timeDiff);
        }
    }

    /// <summary>
    /// 检测单个按键组是否触发（按下/按住）
    /// </summary>
    private bool IsKeyTriggered(int keyIndex)
    {
        return noteTools.input.IsGroupPressed(keyIndex) || noteTools.input.IsGroupHeld(keyIndex);
    }

    /// <summary>
    /// 检测其他任意有效按键是否触发
    /// </summary>
    private bool IsAnyOtherKeyTriggered()
    {
        foreach (int keyIndex in validOtherKeyIndices)
        {
            if (IsKeyTriggered(keyIndex)) return true;
        }
        return false;
    }

    /// <summary>
    /// 根据时间差获取判定等级（与NoteTools的DropToCommand逻辑完全对齐）
    /// </summary>
    private JudgeResult GetJudgeResultByTimeDiff(float absTimeDiff)
    {
        if (absTimeDiff <= perfectThreshold) return JudgeResult.Perfect;
        if (absTimeDiff <= goodThreshold) return JudgeResult.Good;
        if (absTimeDiff <= badThreshold) return JudgeResult.Bad;
        return JudgeResult.Miss; // 兜底
    }

    /// <summary>
    /// 设置判定结果并标记完成
    /// </summary>
    private void SetJudgeResult(JudgeResult result, float timeDiff)
    {
        judgeResult = result;
        isJudged = true;
        Debug.Log($"[{gameObject.name}] Flick判定结果：{result} | 时间差：{timeDiff:F2}s");
    }
    #endregion

    #region 视觉表现：精灵切换 + 延迟销毁
    /// <summary>
    /// 根据判定结果切换精灵
    /// </summary>
    private void SwitchFlickSprite()
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
            Debug.LogError($"[{gameObject.name}] {judgeResult}判定对应的精灵未配置！");
            spriteRenderer.sprite = defaultFlickSprite;
        }
    }

    /// <summary>
    /// 启动延迟销毁协程
    /// </summary>
    private void StartDelayDestroy()
    {
        if (destroyCoroutine != null) StopCoroutine(destroyCoroutine);
        destroyCoroutine = StartCoroutine(DelayDestroyCoroutine());
    }

    /// <summary>
    /// 延迟销毁音符物体
    /// </summary>
    private IEnumerator DelayDestroyCoroutine()
    {
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
        Debug.Log($"[{gameObject.name}] Flick音符已延迟{destroyDelay}秒销毁");
    }
    #endregion

    #region 工具方法：重置状态 + 快捷创建
    /// <summary>
    /// 重置Flick音符状态（重玩时调用）
    /// </summary>
    public void ResetFlickState()
    {
        // 停止销毁协程
        if (destroyCoroutine != null)
        {
            StopCoroutine(destroyCoroutine);
            destroyCoroutine = null;
        }

        // 重置判定状态
        isJudged = false;
        judgeResult = JudgeResult.None;
        hasSwitchedSprite = false;

        // 重置视觉和激活状态
        spriteRenderer.sprite = defaultFlickSprite;
        gameObject.SetActive(true);

        // 同步NoteData坐标到物体
        transform.position = new Vector2(noteData.x, noteData.y);
    }

    /// <summary>
    /// 快捷创建Flick音符（适配NoteTools的NoteData结构）
    /// </summary>
    public static Flick CreateFlickNote(Transform parent, Vector2 position, NoteTools noteTools, float judgeTime)
    {
        GameObject flickObj = new GameObject($"Flick_Note_Key11");
        flickObj.transform.SetParent(parent);
        flickObj.transform.position = position;

        // 添加核心组件
        Flick flick = flickObj.AddComponent<Flick>();
        flick.noteTools = noteTools;
        flick.judgeTime = judgeTime;

        // 初始化NoteData（严格对齐NoteTools的结构）
        flick.noteData = new NoteData()
        {
            KeyIndex = 11,          // Flick默认绑定按键11
            x = position.x,
            y = position.y,
            isVisible = true
        };

        // 默认有效按键组
        flick.validOtherKeyIndices = new List<int>() { 1, 2, 3 };

        return flick;
    }
    #endregion

    #region 生命周期：清理协程
    void OnDestroy()
    {
        // 防止协程内存泄漏
        if (destroyCoroutine != null)
        {
            StopCoroutine(destroyCoroutine);
            destroyCoroutine = null;
        }
    }
    #endregion
}