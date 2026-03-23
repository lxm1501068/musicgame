using UnityEngine;
using TMPro; // 需导入TMP命名空间（若用UGUI Text则替换为using UnityEngine.UI;）
using System.Collections;

/// <summary>
/// Combo数显示组件（单例模式）
/// 规则：1. Perfect/Good增加Combo 2. Bad/Miss重置Combo为0 3. Combo<3时隐藏显示
/// </summary>
public class ComboDisplay : MonoBehaviour
{
    // 单例实例（全局唯一）
    public static ComboDisplay Instance { get; private set; }

    [Header("UI配置")]
    [Tooltip("显示Combo数的文本组件（TMP_Text/Text）")]
    public TMP_Text comboText; // 若用UGUI Text则改为public Text comboText;
    [Tooltip("Combo文本的前缀（如“COMBO x”）")]
    public string comboPrefix = "COMBO x";

    [Header("动画配置")]
    [Tooltip("连击数增加时的缩放动画曲线")]
    public AnimationCurve punchCurve = AnimationCurve.EaseInOut(0, 1.0f, 0.1f, 1.3f);
    [Tooltip("缩放动画持续时间")]
    public float animationDuration = 0.15f;

    private int currentCombo = 0; // 当前连击数
    private bool isVisible = false; // 是否显示Combo
    private Coroutine activeAnimation;

    /// <summary>
    /// 单例初始化（确保全局唯一）
    /// </summary>
    private void Awake()
    {
        // 单例逻辑：重复实例自动销毁
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 初始化隐藏Combo
        if (comboText != null)
        {
            comboText.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("ComboDisplay: 未绑定Combo文本组件！");
        }
    }

    /// <summary>
    /// 供外部调用：增加连击数（Perfect/Good判定时调用）
    /// </summary>
    public void AddCombo()
    {
        currentCombo++;
        UpdateComboDisplay();
        
        // 播放连击增加动画
        if (isVisible && comboText != null)
        {
            if (activeAnimation != null) StopCoroutine(activeAnimation);
            activeAnimation = StartCoroutine(PlayPunchAnimation());
        }

        Debug.Log($"[ComboDisplay] 连击数+1，当前：{currentCombo}");
    }

    private IEnumerator PlayPunchAnimation()
    {
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            float t = elapsed / animationDuration;
            float scale = punchCurve.Evaluate(t);
            comboText.transform.localScale = Vector3.one * scale;
            elapsed += Time.deltaTime;
            yield return null;
        }
        comboText.transform.localScale = Vector3.one;
        activeAnimation = null;
    }

    /// <summary>
    /// 供外部调用：重置连击数为0（Bad/Miss判定时调用）
    /// </summary>
    public void ResetCombo()
    {
        currentCombo = 0;
        UpdateComboDisplay();
        Debug.Log("[ComboDisplay] 连击数重置为0");
    }

    /// <summary>
    /// 更新Combo显示状态和数值
    /// </summary>
    private void UpdateComboDisplay()
    {
        if (comboText == null) return;

        // Combo≥3时显示，否则隐藏
        isVisible = currentCombo >= 3;
        comboText.gameObject.SetActive(isVisible);

        if (isVisible)
        {
            comboText.text = $"{comboPrefix}{currentCombo}";
        }
    }

    /// <summary>
    /// 供外部调用的全局重置方法（如游戏重启/结算时）
    /// </summary>
    public void FullReset()
    {
        currentCombo = 0;
        isVisible = false;
        if (comboText != null)
        {
            comboText.gameObject.SetActive(false);
        }
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