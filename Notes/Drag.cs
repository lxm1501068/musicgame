using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Drag音符组件：判定时间±0.1秒内检测到按下/按住即有效
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
    [Tooltip("Drag判定基准时间（音乐时间）")]
    public float judgeTime;            // 替代原noteData.endTime（新NoteData无此字段）

    [Header("判定对应的精灵图片")]
    public Sprite defaultDragSprite;   // 未判定默认精灵
    public Sprite perfectDragSprite;   // Perfect判定精灵
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

        // 初始化NoteData（轻量化结构）
        if (noteData == null)
        {
            noteData = new NoteData()
            {
                KeyIndex = 0,
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
        float currentMusicTime = GameManager.Instance.CurrentPlayTime;
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

    #region 核心：Drag判定逻辑（二级判定体系：Perfect/Miss）
    private void CheckDragJudge(float currentTime)
    {
        // 时间差：当前音乐时间 - 判定基准时间
        float timeDiff = currentTime - judgeTime;
        float absTimeDiff = Mathf.Abs(timeDiff);

        // 检测输入（按下/按住任一有效）
        bool isKeyPressed = InputManager.Instance.IsGroupPressed(noteData.KeyIndex);
        bool isKeyHeld = InputManager.Instance.IsGroupHeld(noteData.KeyIndex);

        // 判定逻辑：
        if (absTimeDiff <= perfectThreshold && (isKeyPressed || isKeyHeld))
        {
            // ±0.1秒内有按下/按住 → Perfect
            judgeResult = JudgeResult.Perfect;
            isJudged = true;
            Debug.Log($"[{gameObject.name}] Drag判定：Perfect | 时间差：{timeDiff:F2}s");
        }
        else if (absTimeDiff > perfectThreshold)
        {
            // 超出±0.1秒 → Miss
            judgeResult = JudgeResult.Miss;
            isJudged = true;
            Debug.Log($"[{gameObject.name}] Drag判定：Miss | 超出±{perfectThreshold}秒窗口，时间差：{timeDiff:F2}s");
        }
    }
    #endregion

    #region 精灵切换 + 延迟销毁
    private void SwitchDragSprite()
    {
        Sprite targetSprite = judgeResult switch
        {
            JudgeResult.Perfect => perfectDragSprite,
            JudgeResult.Miss => missDragSprite,
            _ => defaultDragSprite
        };

        if (targetSprite != null)
        {
            spriteRenderer.sprite = targetSprite;
            Debug.Log($"[{gameObject.name}] Drag精灵已切换为：{judgeResult}");
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] Drag组件：{judgeResult}对应的精灵未赋值！");
            spriteRenderer.sprite = defaultDragSprite;
        }
    }

    private IEnumerator DelayDestroyNote()
    {
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }
    #endregion

    #region 外部接口
    /// <summary>
    /// 获取Drag判定结果
    /// </summary>
    public JudgeResult GetDragJudgeResult()
    {
        return judgeResult;
    }

    /// <summary>
    /// 是否已判定
    /// </summary>
    public bool IsJudged()
    {
        return isJudged;
    }
    #endregion

    void OnDestroy()
    {
        StopAllCoroutines();
    }
}