using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteRenderer))]
public class Drag : MonoBehaviour
{
    [Header("核心关联")]
    public NoteData noteData;
    public NoteTools noteTools;

    [Header("判定配置")]
    public float perfectThreshold = 0.1f;
    public float judgeTime;

    [Header("判定对应的精灵图片")]
    public Sprite defaultDragSprite;
    public Sprite perfectDragSprite;
    public Sprite missDragSprite;

    [Header("销毁设置")]
    public float destroyDelay = 0.2f;

    private bool isJudged = false;
    private JudgeResult judgeResult = JudgeResult.None;
    private SpriteRenderer spriteRenderer;
    private Coroutine destroyCoroutine;
    private bool hasSwitchedSprite = false;
    private bool isJudgeLogicInitialized = false;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"[{gameObject.name}] Drag组件：未找到SpriteRenderer组件！");
            enabled = false;
            return;
        }

        // 【新增】初始隐藏
        spriteRenderer.enabled = false;

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
        if (GameManager.Instance == null) return;
        
        float currentMusicTime = GameManager.Instance.CurrentPlayTime;
        if (currentMusicTime == -1) return;

        if (!isJudgeLogicInitialized)
        {
            InitJudgeLogic();
            isJudgeLogicInitialized = true;

            // 【新增】判定逻辑初始化完成，显示音符
            spriteRenderer.enabled = true;
            Debug.Log($"[{gameObject.name}] Drag组件：谱面加载完成，判定逻辑初始化完成，显示音符");
        }

        if (isJudged || hasSwitchedSprite) return;

        CheckDragJudge(currentMusicTime);

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

    private void InitJudgeLogic()
    {
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
            transform.position = new Vector2(noteData.x, noteData.y);
        }
    }

    private void CheckDragJudge(float currentTime)
    {
        float timeDiff = currentTime - judgeTime;
        float absTimeDiff = Mathf.Abs(timeDiff);

        bool isKeyPressed = InputManager.Instance.IsGroupPressed(noteData.KeyIndex);
        bool isKeyHeld = InputManager.Instance.IsGroupHeld(noteData.KeyIndex);

        if (absTimeDiff <= perfectThreshold && (isKeyPressed || isKeyHeld))
        {
            judgeResult = JudgeResult.Perfect;
            isJudged = true;
            Debug.Log($"[{gameObject.name}] Drag判定：Perfect | 时间差：{timeDiff:F2}s");
        }
        else if (absTimeDiff > perfectThreshold)
        {
            judgeResult = JudgeResult.Miss;
            isJudged = true;
            Debug.Log($"[{gameObject.name}] Drag判定：Miss | 超出±{perfectThreshold}秒窗口，时间差：{timeDiff:F2}s");
        }
    }

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

    public JudgeResult GetDragJudgeResult() => judgeResult;
    public bool IsJudged() => isJudged;

    void OnDestroy()
    {
        StopAllCoroutines();
    }
}