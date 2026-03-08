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
    public Sprite missFlickSprite;   // 注意：已移除 good/bad 精灵

    [Header("销毁设置")]
    public float destroyDelay = 0.2f;

    [Header("Flick 专属配置")]
    public int spaceKeyIndex = 11;                     // 空格键的索引
    public List<int> validOtherKeyIndices = new List<int>() { 0, 6, 7, 8, 9 }; // 需要同时按下的其他键
    public float perfectThreshold = 0.1f;              // Perfect 判定窗口（秒）

    [Header("固定运动参数")]
    public Vector2 startPos = new Vector2(0, 5);   // 画面正上方起始点（根据实际屏幕调整）
    public Vector2 endPos = new Vector2(0, 0);     // 画面正中间终点

    private SpriteRenderer spriteRenderer;
    private bool hasSwitchedSprite = false;
    private JudgeResult judgeResult = JudgeResult.None;

    private float startTime;   // 从 timeA 获取
    private float endTime;     // 从 timeB 获取
    private float judgeTime;   // 判定时间 = endTime
    private bool isCommandsInitialized = false;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"[{noteData?.NoteIndex}] Flick组件：未找到SpriteRenderer组件！");
            enabled = false;
            return;
        }

        // 初始隐藏
        spriteRenderer.enabled = false;

        if (defaultFlickSprite != null)
            spriteRenderer.sprite = defaultFlickSprite;
        else
            Debug.LogWarning($"[{noteData?.NoteIndex}] Flick组件：未设置默认Flick精灵！");

        if (noteData == null)
        {
            Debug.LogError($"[Flick] NoteData未赋值！");
            enabled = false;
            return;
        }

        // 确保有效键列表中不包含空格键（避免误判）
        validOtherKeyIndices.RemoveAll(x => x == spaceKeyIndex);
    }

    void Update()
    {
        if (GameManager.Instance == null) return;

        float currentTime = GameManager.Instance.CurrentPlayTime;
        if (currentTime == -1) return;

        if (!isCommandsInitialized)
        {
            InitCommands();
            isCommandsInitialized = true;
        }

        // 已判定完成，只同步位置（不再移动或判定）
        if (hasSwitchedSprite)
        {
            SyncPosition();
            return;
        }

        // 未到达开始时间：隐藏音符并返回
        if (currentTime < startTime)
        {
            spriteRenderer.enabled = false;
            return;
        }

        // 到达开始时间：确保显示
        if (!spriteRenderer.enabled)
            spriteRenderer.enabled = true;

        // 计算匀加速位置
        UpdatePosition(currentTime);

        // 执行 Flick 判定（仅 Perfect/Miss）
        CheckFlickJudge(currentTime);

        if (judgeResult != JudgeResult.None)
        {
            SwitchJudgeSprite(judgeResult);
            hasSwitchedSprite = true;
            StartCoroutine(DelayDestroyNote());
        }

        SyncPosition();
    }

    private void InitCommands()
    {
        if (noteData.commands == null || noteData.commands.Count == 0)
        {
            Debug.Log($"[{noteData.NoteIndex}] Flick组件：NoteData无关联的Command！");
            return;
        }

        Command cmd = noteData.commands[0];
        // 只取时间参数，忽略原有坐标
        startTime = cmd.timeA;
        endTime = cmd.timeB;
        judgeTime = endTime; // 判定时刻为到达终点时

        // 覆盖 noteData 的初始位置为固定起始点（忽略谱面中的坐标）
        noteData.x = startPos.x;
        noteData.y = startPos.y;

        Debug.Log($"[{gameObject.name}] Flick音符ID:{cmd.num} 初始化：startTime={startTime}, endTime={endTime}");
    }

    private void UpdatePosition(float currentTime)
    {
        // 如果当前时间已超过结束时间，位置固定在终点
        if (currentTime >= endTime)
        {
            noteData.x = endPos.x;
            noteData.y = endPos.y;
            return;
        }

        // 在时间区间内按 t² 插值（匀加速，初速为0）
        float t = (currentTime - startTime) / (endTime - startTime);
        float easedT = t * t; // 匀加速曲线
        noteData.x = Mathf.Lerp(startPos.x, endPos.x, easedT);
        noteData.y = Mathf.Lerp(startPos.y, endPos.y, easedT);
    }

    private void SyncPosition()
    {
        if (noteData == null) return;
        transform.position = new Vector2(noteData.x, noteData.y);
    }

    /// <summary>
    /// Flick 判定逻辑（仅 Perfect / Miss）
    /// </summary>
    private void CheckFlickJudge(float currentTime)
    {
        if (judgeResult != JudgeResult.None) return;

        float timeDiff = currentTime - judgeTime;
        float absTimeDiff = Mathf.Abs(timeDiff);

        // 超出 Perfect 窗口直接判 Miss
        if (absTimeDiff > perfectThreshold)
        {
            SetJudgeResult(JudgeResult.Miss, timeDiff);
            return;
        }

        // 检查空格键 + 任意有效键是否同时按下
        bool spacePressed = InputManager.Instance.IsGroupPressed(spaceKeyIndex);
        if (!spacePressed) return;

        bool otherPressed = false;
        foreach (int keyIndex in validOtherKeyIndices)
        {
            if (InputManager.Instance.IsGroupPressed(keyIndex))
            {
                otherPressed = true;
                break;
            }
        }
        if (!otherPressed) return;

        // 在 Perfect 窗口内且按键正确 → Perfect
        SetJudgeResult(JudgeResult.Perfect, timeDiff);
    }

    private void SetJudgeResult(JudgeResult result, float timeDiff)
    {
        judgeResult = result;
        Debug.Log($"[{noteData.NoteIndex}] Flick判定结果：{result} | 时间差：{timeDiff:F2}s");
    }

    private void SwitchJudgeSprite(JudgeResult judgeResult)
    {
        Sprite targetSprite = judgeResult switch
        {
            JudgeResult.Perfect => perfectFlickSprite,
            JudgeResult.Miss => missFlickSprite,
            _ => defaultFlickSprite   // 其他情况（理论上不会发生）
        };

        if (targetSprite != null)
            spriteRenderer.sprite = targetSprite;
        else
        {
            Debug.LogError($"[{noteData.NoteIndex}] Flick组件：{judgeResult}对应的精灵未赋值！");
            spriteRenderer.sprite = defaultFlickSprite;
        }
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
}