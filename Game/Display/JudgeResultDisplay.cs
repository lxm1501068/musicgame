using UnityEngine;
using System.Collections;

/// <summary>
/// 全局判定结果显示组件（单例模式）
/// 功能：1. 切换不同判定结果的精灵 2. 2秒无判定自动隐藏 3. 供外部类主动调用展示判定结果
/// </summary>
public class JudgeResultDisplay : MonoBehaviour
{
    // 单例实例（全局唯一）
    public static JudgeResultDisplay Instance { get; private set; }

    [Header("精灵配置")]
    [Tooltip("判定结果对应的精灵（顺序：Perfect/Good/Bad/Miss）")]
    public Sprite perfectSprite;
    public Sprite goodSprite;
    public Sprite badSprite;
    public Sprite missSprite;

    [Header("时间配置")]
    [Tooltip("无判定时自动隐藏的延迟（秒）")]
    public float hideDelay = 2f;

    [Header("动画配置")]
    [Tooltip("判定结果出现时的缩放动画曲线")]
    public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0.5f, 0.2f, 1.2f);
    [Tooltip("缩放动画持续时间")]
    public float animationDuration = 0.2f;

    private SpriteRenderer spriteRenderer; // 精灵渲染器
    private float lastJudgeTime; // 最后一次判定的时间
    private bool isHidden = true; // 是否处于隐藏状态
    private Coroutine activeAnimation;

    /// <summary>
    /// 单例初始化（确保全局唯一）
    /// </summary>
    private void Awake()
    {
        // 单例逻辑：如果已有实例，销毁当前对象；否则保留实例
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // 可选：场景切换时不销毁（根据项目需求决定）
        // DontDestroyOnLoad(gameObject);

        // 初始化SpriteRenderer
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        // 初始隐藏
        spriteRenderer.enabled = false;
        lastJudgeTime = Time.time;
    }

    void Update()
    {
        // 检查是否超过隐藏延迟，自动隐藏
        if (!isHidden && Time.time - lastJudgeTime > hideDelay)
        {
            HideJudgeResult();
        }
    }

    private IEnumerator PlayAppearAnimation()
    {
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            float t = elapsed / animationDuration;
            float scale = scaleCurve.Evaluate(t);
            transform.localScale = Vector3.one * scale;
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localScale = Vector3.one;
        activeAnimation = null;
    }

    /// <summary>
    /// 供外部类调用：展示指定的判定结果
    /// （如Tap.cs/Flick.cs等音符类判定完成后调用）
    /// </summary>
    /// <param name="result">判定结果（Perfect/Good/Bad/Miss）</param>
    public void ShowJudgeResult(JudgeResult result)
    {
        // 过滤无效判定
        if (result == JudgeResult.None)
        {
            HideJudgeResult();
            return;
        }

        // 刷新最后判定时间
        lastJudgeTime = Time.time;
        // 显示渲染器
        spriteRenderer.enabled = true;
        isHidden = false;

        // 根据判定结果切换精灵
        switch (result)
        {
            case JudgeResult.Perfect:
                spriteRenderer.sprite = perfectSprite;
                break;
            case JudgeResult.Good:
                spriteRenderer.sprite = goodSprite;
                break;
            case JudgeResult.Bad:
                spriteRenderer.sprite = badSprite;
                break;
            case JudgeResult.Miss:
                spriteRenderer.sprite = missSprite;
                break;
            default:
                HideJudgeResult();
                return;
        }

        // 播放出现动画
        if (activeAnimation != null) StopCoroutine(activeAnimation);
        activeAnimation = StartCoroutine(PlayAppearAnimation());

        // 调试日志
        Debug.Log($"[JudgeResultDisplay] 展示判定结果：{result}");
    }

    /// <summary>
    /// 隐藏判定结果
    /// </summary>
    private void HideJudgeResult()
    {
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;
        isHidden = true;
        transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 供外部调用的重置方法（如重玩游戏时）
    /// </summary>
    public void ResetDisplay()
    {
        HideJudgeResult();
        lastJudgeTime = Time.time;
    }

    /// <summary>
    /// 单例销毁时清空实例
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}