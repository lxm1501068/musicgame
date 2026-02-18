using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Drag音符组件：判定时间±0.2秒内检测到按下/按住即有效
/// 适配NoteTools的NoteData结构和判定体系
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Drag : MonoBehaviour
{
    [Header("核心关联")]
    public NoteData noteData;          // 仅保留keyIndex/x/y/isVisible的轻量化数据
    public NoteTools noteTools;        // 关联NoteTools管理类

    [Header("判定配置")]
    [Tooltip("Perfect判定阈值（秒）±0.1")]
    public float perfectThreshold = 0.1f;
    [Tooltip("Good判定阈值（秒）±0.2")]
    public float goodThreshold = 0.2f;
    [Tooltip("Drag判定基准时间（音乐时间）")]
    public float judgeTime;            // 替代原noteData.endTime（新NoteData无此字段）

    [Header("判定对应的精灵图片")]
    public Sprite defaultDragSprite;   // 未判定默认精灵
    public Sprite perfectDragSprite;   // Perfect判定精灵
    public Sprite goodDragSprite;      // Good判定精灵
    public Sprite missDragSprite;      // Miss判定精灵

    [Header("销毁设置")]
    public float destroyDelay = 0.2f;  // 判定完成后延迟销毁时间

    // 私有状态
    private bool isJudged = false;                  // 是否完成判定
    private JudgeResult judgeResult = JudgeResult.None; // 判定结果
    private SpriteRenderer spriteRenderer;          // 精灵渲染器缓存
    private Coroutine destroyCoroutine;             // 销毁协程
    private bool hasSwitchedSprite = false;         // 是否已切换判定精灵

    void Awake()
    {
        // 组件缓存与安全校验
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"[{gameObject.name}] Drag组件：未找到SpriteRenderer组件！");
            enabled = false;
            return;
        }

        if (noteTools == null)
        {
            Debug.LogError($"[{gameObject.name}] Drag组件：未引用NoteTools实例！");
            enabled = false;
            return;
        }

        if (noteTools.input == null)
        {
            Debug.LogError($"[{gameObject.name}] Drag组件：NoteTools未绑定InputManager！");
            enabled = false;
            return;
        }

        // 初始化NoteData（轻量化结构）
        if (noteData == null)
        {
            noteData = new NoteData()
            {
                keyIndex = 0,
                x = transform.position.x,
                y = transform.position.y,
                isVisible = true
            };
            Debug.LogWarning($"[{gameObject.name}] Drag组件：自动创建默认NoteData实例！");
        }
        else
        {
            // 同步NoteData坐标到物体
            transform.position = new Vector2(noteData.x, noteData.y);
        }

        // 初始化默认精灵
        if (defaultDragSprite != null)
        {
            spriteRenderer.sprite = defaultDragSprite;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] Drag组件：未设置默认Drag精灵！");
        }
    }

    void Update()
    {
        // 已判定/已切换精灵 → 跳过更新
        if (isJudged || hasSwitchedSprite) return;

        // 获取NoteTools统一管理的音乐时间（替代直接使用Time.time）
        float currentMusicTime = noteTools.GetCurrentMusicTime();
        CheckDragJudge(currentMusicTime);

        // 判定完成后执行精灵切换和延迟销毁
        if (isJudged && !hasSwitchedSprite)
        {
            SwitchDragSprite();
            hasSwitchedSprite = true;
            if (destroyCoroutine == null)
            {
                destroyCoroutine = StartCoroutine(DelayDestroyNote());
            }
        }
    }

    #region 核心：Drag判定逻辑（对齐NoteTools的判定体系）
    private void CheckDragJudge(float currentTime)
    {
        // 时间差：当前音乐时间 - 判定基准时间
        float timeDiff = currentTime - judgeTime;

        // 1. 超出±0.2秒判定窗口 → Miss
        if (Mathf.Abs(timeDiff) > goodThreshold)
        {
            judgeResult = JudgeResult.Miss;
            isJudged = true;
            Debug.Log($"[{gameObject.name}] Drag判定：Miss | 超出±{goodThreshold}秒窗口，时间差：{timeDiff:F2}s");
            return;
        }

        // 2. 检测输入（按下/按住任一有效，需InputManager实现IsGroupHeld）
        bool isKeyPressed = noteTools.input.IsGroupPressed(noteData.keyIndex);
        bool isKeyHeld = noteTools.input.IsGroupHeld(noteData.keyIndex);

        if (isKeyPressed || isKeyHeld)
        {
            // 2.1 ±0.1秒内 → Perfect
            if (Mathf.Abs(timeDiff) <= perfectThreshold)
            {
                judgeResult = JudgeResult.Perfect;
            }
            // 2.2 ±0.1~±0.2秒内 → Good
            else if (Mathf.Abs(timeDiff) <= goodThreshold)
            {
                judgeResult = JudgeResult.Good;
            }

            isJudged = true;
            Debug.Log($"[{gameObject.name}] Drag判定：{judgeResult} | 时间差：{timeDiff:F2}s | 按下={isKeyPressed}，按住={isKeyHeld}");
        }
    }
    #endregion

    #region 精灵切换 + 延迟销毁
    private void SwitchDragSprite()
    {
        Sprite targetSprite = judgeResult switch
        {
            JudgeResult.Perfect => perfectDragSprite,
            JudgeResult.Good => goodDragSprite,
            JudgeResult.Miss => missDragSprite,
            _ => defaultDragSprite
        };

        // 安全赋值精灵（容错处理）
        if (targetSprite != null)
        {
            spriteRenderer.sprite = targetSprite;
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] Drag组件：{judgeResult}判定对应的精灵未赋值！");
            if (defaultDragSprite != null) spriteRenderer.sprite = defaultDragSprite;
        }
    }

    private IEnumerator DelayDestroyNote()
    {
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
        Debug.Log($"[{gameObject.name}] Drag音符延迟{destroyDelay}秒销毁");
    }
    #endregion

    #region 状态重置 + 快捷创建（适配新NoteData结构）
    /// <summary>
    /// 重置Drag状态（重玩时调用）
    /// </summary>
    public void ResetDragState()
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

        // 重置精灵和显示状态
        hasSwitchedSprite = false;
        spriteRenderer.sprite = defaultDragSprite;
        gameObject.SetActive(true);

        // 同步NoteData坐标到物体
        if (noteData != null)
        {
            transform.position = new Vector2(noteData.x, noteData.y);
        }
    }

    /// <summary>
    /// 快捷创建Drag音符（适配新NoteData和NoteTools体系）
    /// </summary>
    /// <param name="parent">父节点</param>
    /// <param name="position">初始位置</param>
    /// <param name="noteTools">NoteTools实例</param>
    /// <param name="keyIndex">按键序号</param>
    /// <param name="judgeTime">判定基准时间（音乐时间）</param>
    /// <returns>创建的Drag组件</returns>
    public static Drag CreateDragNote(Transform parent, Vector2 position, NoteTools noteTools, int keyIndex, float judgeTime)
    {
        GameObject dragObj = new GameObject($"Drag_Note_Key{keyIndex}");
        dragObj.transform.SetParent(parent);
        dragObj.transform.position = position;

        // 添加组件并初始化核心参数
        Drag drag = dragObj.AddComponent<Drag>();
        drag.noteTools = noteTools;
        drag.judgeTime = judgeTime;
        drag.noteData = new NoteData()
        {
            keyIndex = keyIndex,
            x = position.x,
            y = position.y,
            isVisible = true
        };

        // 默认阈值对齐NoteTools的DropToCommand
        drag.perfectThreshold = 0.2f;

        return drag;
    }
    #endregion

    #region 生命周期清理
    // 销毁时清理协程，避免内存泄漏
    void OnDestroy()
    {
        if (destroyCoroutine != null)
        {
            StopCoroutine(destroyCoroutine);
        }
    }

    // 禁用时重置协程状态
    void OnDisable()
    {
        if (destroyCoroutine != null)
        {
            StopCoroutine(destroyCoroutine);
            destroyCoroutine = null;
        }
    }
    #endregion

    #region 辅助方法（可选扩展）
    /// <summary>
    /// 手动触发判定（外部调用）
    /// </summary>
    /// <param name="result">指定判定结果</param>
    public void ForceJudge(JudgeResult result)
    {
        if (isJudged) return;
        
        judgeResult = result;
        isJudged = true;
        SwitchDragSprite();
        
        if (destroyCoroutine == null)
        {
            destroyCoroutine = StartCoroutine(DelayDestroyNote());
        }
        
        Debug.Log($"[{gameObject.name}] Drag强制判定：{result}");
    }
    #endregion
}