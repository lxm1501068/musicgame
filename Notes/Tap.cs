using UnityEngine;
using System.Collections; 

/// <summary>
/// Tap音符组件：绑定到Tap音符GameObject，适配新版NoteTools（判定结果从DropToCommand读取）
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Tap : MonoBehaviour
{
    [Header("核心关联")]
    public NoteData noteData;
    public NoteTools noteTools;
    public DropToCommand bindDropToCommand;

    [Header("判定对应的精灵图片")]
    public Sprite defaultTapSprite;
    public Sprite perfectTapSprite;
    public Sprite goodTapSprite;
    public Sprite badTapSprite;
    public Sprite missTapSprite;

    [Header("销毁设置")]
    [Tooltip("判定完成后延迟销毁的时间（秒）")]
    public float destroyDelay = 0.2f; 

    private SpriteRenderer spriteRenderer;
    private bool hasSwitchedSprite = false;
    private Coroutine destroyCoroutine; 

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // 安全校验
        if (spriteRenderer == null)
        {
            Debug.LogError($"[{gameObject.name}] Tap组件：未找到SpriteRenderer组件！");
            enabled = false;
            return;
        }

        if (noteTools == null)
        {
            Debug.LogError($"[{gameObject.name}] Tap组件：未引用NoteTools实例！");
            enabled = false;
            return;
        }

        // 初始显示默认精灵
        if (defaultTapSprite != null)
        {
            spriteRenderer.sprite = defaultTapSprite;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] Tap组件：未设置默认Tap精灵！");
        }

        // 初始化NoteData（适配新版字段：仅keyIndex/x/y/isVisible）
        if (noteData == null)
        {
            noteData = new NoteData();
            noteData.isVisible = true;
            Debug.LogWarning($"[{gameObject.name}] Tap组件：未赋值NoteData，已自动创建默认实例！");
        }
        // 同步NoteData坐标到物体初始位置
        transform.position = new Vector2(noteData.x, noteData.y);
    }

    void Update()
    {
        // 1. 已切换过精灵 → 跳过
        // 2. 未绑定DropTo指令 → 跳过
        // 3. 指令未完成判定 → 跳过
        if (hasSwitchedSprite || bindDropToCommand == null || bindDropToCommand.judgeResult == JudgeResult.None)
        {
            // 同步NoteData的坐标到物体
            if (noteData != null)
            {
                transform.position = new Vector2(noteData.x, noteData.y);
            }
            return;
        }

        // 判定完成，切换对应精灵
        SwitchJudgeSprite(bindDropToCommand.judgeResult);
        hasSwitchedSprite = true;

        // 启动延迟销毁协程
        if (destroyCoroutine == null)
        {
            destroyCoroutine = StartCoroutine(DelayDestroyNote());
        }
    }

    /// <summary>
    /// 延迟销毁音符的协程
    /// </summary>
    private IEnumerator DelayDestroyNote()
    {
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
        Debug.Log($"[{gameObject.name}] Tap音符已延迟{destroyDelay}秒销毁");
    }

    /// <summary>
    /// 根据判定结果切换Sprite
    /// </summary>
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
            Debug.Log($"[{gameObject.name}] Tap判定：{judgeResult} → 切换精灵完成");
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] Tap组件：{judgeResult}对应的精灵未赋值！");
            if (defaultTapSprite != null)
            {
                spriteRenderer.sprite = defaultTapSprite;
            }
        }
    }

    /// <summary>
    /// 手动重置Tap音符状态（适配新版：重置DropTo指令判定结果）
    /// </summary>
    public void ResetTapState()
    {
        // 停止正在运行的销毁协程
        if (destroyCoroutine != null)
        {
            StopCoroutine(destroyCoroutine);
            destroyCoroutine = null;
        }

        hasSwitchedSprite = false;
        // 重置绑定指令的判定结果（而非NoteData）
        if (bindDropToCommand != null)
        {
            bindDropToCommand.judgeResult = JudgeResult.None;
        }
        spriteRenderer.sprite = defaultTapSprite;
        gameObject.SetActive(true);
        
        // 重置坐标到NoteData初始值
        if (noteData != null)
        {
            transform.position = new Vector2(noteData.x, noteData.y);
        }
    }

    /// <summary>
    /// 快捷创建Tap音符（适配新版NoteTools：创建并绑定DropToCommand）
    /// </summary>
    /// <param name="parent">父节点</param>
    /// <param name="position">初始位置</param>
    /// <param name="noteTools">NoteTools实例</param>
    /// <param name="keyIndex">按键序号</param>
    /// <param name="judgeTime">判定时间（对应DropToCommand的endTime）</param>
    /// <param name="cmd">DropTo指令的原始Command数据</param>
    public static Tap CreateTapNote(Transform parent, Vector2 position, NoteTools noteTools, int keyIndex, float judgeTime, Command cmd)
    {
        GameObject tapObj = new GameObject($"Tap_Note_Key{keyIndex}");
        tapObj.transform.SetParent(parent);
        tapObj.transform.position = position;

        Tap tap = tapObj.AddComponent<Tap>();
        tap.noteTools = noteTools;
        
        // 初始化新版NoteData
        tap.noteData = new NoteData()
        {
            KeyIndex = keyIndex,
            x = position.x,
            y = position.y,
            isVisible = true
        };

        // 创建并绑定DropToCommand（核心适配点）
        noteTools.CreateDropToCommand(tap.noteData, cmd);
        // 找到刚创建的DropToCommand并绑定到Tap组件
        var dropToCmd = noteTools.GetComponent<NoteTools>()._dropToCommands.FindLast(c => c.note == tap.noteData);
        if (dropToCmd != null)
        {
            tap.bindDropToCommand = dropToCmd;
        }
        else
        {
            Debug.LogError($"[{tapObj.name}] 创建Tap音符时未找到绑定的DropToCommand！");
        }

        return tap;
    }

    // 物体被销毁时清理协程
    void OnDestroy()
    {
        if (destroyCoroutine != null)
        {
            StopCoroutine(destroyCoroutine);
        }
    }
}