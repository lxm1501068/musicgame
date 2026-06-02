using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 所有音符组件的基类，处理通用的生命周期、显示控制、位置同步和 UI 更新逻辑。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public abstract class BaseNote : MonoBehaviour
{
    [Header("核心关联")]
    public NoteData noteData;

    [Header("销毁设置")]
    public float destroyDelay = 0.2f;

    protected SpriteRenderer spriteRenderer;
    protected bool hasSwitchedSprite = false;
    protected bool isCommandsInitialized = false;
    protected JudgeResult judgeResult = JudgeResult.None;
    protected float firstCommandStartTime;

    protected virtual void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError($"[{gameObject.name}] {GetType().Name}：未找到SpriteRenderer组件！");
            enabled = false;
            return;
        }

        spriteRenderer.enabled = false; // 初始隐藏

        if (noteData == null)
        {
            noteData = GetComponent<NoteData>();
        }

        if (noteData == null)
        {
            Debug.LogError($"[{gameObject.name}] {GetType().Name}：NoteData未赋值且无法自动获取！");
            enabled = false;
            return;
        }
    }

    protected virtual void Update()
    {
        if (GameManager.Instance == null) return;

        float currentTime = GameManager.Instance.CurrentPlayTime;
        if (currentTime == -1) return;

        // 首次初始化
        if (!isCommandsInitialized)
        {
            InitCommands();
            SyncPosition();
            isCommandsInitialized = true;
        }

        // 显示控制
        UpdateVisibility(currentTime);

        if (!spriteRenderer.enabled) return;

        // 已判定完成，只同步位置
        if (hasSwitchedSprite)
        {
            SyncPosition();
            return;
        }

        // 执行音符逻辑（移动、判定等）
        OnNoteUpdate(currentTime);

        // 如果判定完成，执行通用处理
        if (judgeResult != JudgeResult.None)
        {
            HandleJudgeResult(judgeResult);
        }

        SyncPosition();
    }

    /// <summary>
    /// 初始化指令，由子类实现。需设置 firstCommandStartTime。
    /// </summary>
    protected abstract void InitCommands();

    /// <summary>
    /// 每帧执行的音符逻辑（如移动和判定），由子类实现。
    /// </summary>
    /// <param name="currentTime">当前游戏时间</param>
    protected abstract void OnNoteUpdate(float currentTime);

    protected virtual void UpdateVisibility(float currentTime)
    {
        if (!spriteRenderer.enabled && currentTime >= firstCommandStartTime)
        {
            spriteRenderer.enabled = true;
        }
    }

    protected virtual void SyncPosition()
    {
        if (noteData != null)
        {
            transform.position = new Vector2(noteData.x, noteData.y);
            transform.rotation = Quaternion.Euler(0, 0, noteData.rotation);
        }
    }

    protected virtual void HandleJudgeResult(JudgeResult result)
    {
        hasSwitchedSprite = true;
        UpdateGlobalUI(result);
        StartCoroutine(DelayDestroyNote());
    }

    protected virtual void UpdateGlobalUI(JudgeResult result)
    {
        if (JudgeResultDisplay.Instance != null) 
            JudgeResultDisplay.Instance.ShowJudgeResult(result);
        
        if (ScoreDisplay.Instance != null) 
            ScoreDisplay.Instance.AddScoreByJudge(result);
        
        if (ComboDisplay.Instance != null)
        {
            if (result == JudgeResult.Perfect || result == JudgeResult.Good)
                ComboDisplay.Instance.AddCombo();
            else if (result == JudgeResult.Bad || result == JudgeResult.Miss)
                ComboDisplay.Instance.ResetCombo();
        }

        // 显示判定结果圆形（子类可以重写此行为）
        ShowJudgeCircle(result);
    }

    /// <summary>
    /// 显示判定结果圆形，子类可以重写以实现特殊逻辑
    /// </summary>
    protected virtual void ShowJudgeCircle(JudgeResult result)
    {
        if (JudgeCircleManager.Instance != null && noteData != null)
        {
            Vector2 keyPosition = new Vector2(noteData.x, noteData.y);
            JudgeCircleManager.Instance.ShowJudgeCircle(keyPosition, result, IsHoldNote());
        }
    }

    /// <summary>
    /// 判断是否为Hold音符，子类可以重写
    /// </summary>
    protected virtual bool IsHoldNote()
    {
        return false;
    }

    protected virtual IEnumerator DelayDestroyNote()
    {
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }

    protected virtual void OnDestroy()
    {
        StopAllCoroutines();
    }
}
