﻿using UnityEngine;

public class KeySpriteSwitcher : MonoBehaviour
{
    // 【可在编辑器中设置】要监听的目标组编号（比如填11监听空格键，填1监听1/2/q/w组）
    [Header("要监听的按键组编号（1-11）")]
    public int targetGroupNumber = 11;

    // 【可选】是否在按键按住期间保持显示（true=按住显示，false=仅按下瞬间显示一帧）
    [Header("是否按住期间保持显示")]
    public bool keepVisibleWhileHeld = true;

    // 新增：两个要切换的Sprite（需在编辑器中拖入对应的Sprite资源）
    [Header("切换的Sprite资源")]
    public Sprite sprite1; // 收到信号时显示的Sprite（形象1）
    public Sprite sprite2; // 未收到信号时显示的Sprite（形象2）

    // 存储物体上的SpriteRenderer组件
    private SpriteRenderer spriteRenderer;
    // 新增：标记是否已完成组编号的合法性校验（避免重复校验）
    private bool hasValidatedGroupNumber = false;

    void Start()
    {
        // 获取当前物体上的SpriteRenderer组件
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 初始化检查（仅检查组件和Sprite资源，不校验组编号）
        if (spriteRenderer == null)
        {
            Debug.LogError($"【{gameObject.name}】物体上未挂载SpriteRenderer组件！请添加后再运行。");
            enabled = false;
            return;
        }
        if (sprite1 == null || sprite2 == null)
        {
            Debug.LogError($"【{gameObject.name}】请在编辑器中为sprite1和sprite2赋值对应的Sprite资源！");
            enabled = false;
            return;
        }

        // 初始状态：默认显示sprite2（未收到信号）
        spriteRenderer.sprite = sprite2;

        // 移除原有的立即校验，改为延迟到Update中执行
        // Debug.Log($"【{gameObject.name}】初始化完成 | 监听组编号：{targetGroupNumber} | 按住保持显示：{keepVisibleWhileHeld}");
    }

    void Update()
    {
        // ========== 核心新增：游戏未播放时直接返回 ==========
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying)
        {
            // 未播放时强制恢复默认Sprite
            if (spriteRenderer != null && spriteRenderer.sprite != sprite2)
            {
                spriteRenderer.sprite = sprite2;
            }
            // 重置校验标志（游戏停止后，下次启动需重新校验）
            hasValidatedGroupNumber = false;
            return;
        }

        // 空引用保护：组件或Sprite未赋值时直接返回
        if (spriteRenderer == null || sprite1 == null || sprite2 == null) return;

        // 延迟校验组编号：仅当ChartData加载完成且未校验过时执行
        if (!hasValidatedGroupNumber)
        {
            ValidateTargetGroupNumber();
            hasValidatedGroupNumber = true; // 标记为已校验，避免重复执行
        }

        // 校验：监听的组编号不在合法列表中，直接返回
        if (!IsTargetGroupValid())
        {
            spriteRenderer.sprite = sprite2; // 恢复默认Sprite
            return;
        }

        // 确保输入管理器实例存在
        if (InputManager.Instance != null)
        {
            bool hasSignal = false;
            // 根据配置检测按键组状态
            if (keepVisibleWhileHeld)
            {
                hasSignal = InputManager.Instance.IsGroupHeld(targetGroupNumber);
            }
            else
            {
                hasSignal = InputManager.Instance.IsGroupPressed(targetGroupNumber);
            }

            // 切换对应的Sprite
            UpdateSprite(hasSignal);
        }
        else
        {
            // 输入管理器不存在时，恢复显示sprite2
            spriteRenderer.sprite = sprite2;
        }
    }

    /// <summary>
    /// 根据信号状态更新显示的Sprite
    /// </summary>
    /// <param name="hasSignal">是否收到信号</param>
    private void UpdateSprite(bool hasSignal)
    {
        // 只有Sprite需要变化时才修改，避免频繁赋值
        if (hasSignal && spriteRenderer.sprite != sprite1)
        {
            spriteRenderer.sprite = sprite1;
            // Debug.Log($"{gameObject.name} 切换为Sprite1（触发组：{targetGroupNumber}）");
        }
        else if (!hasSignal && spriteRenderer.sprite != sprite2)
        {
            spriteRenderer.sprite = sprite2;
            // Debug.Log($"{gameObject.name} 切换为Sprite2（触发组：{targetGroupNumber}）");
        }
    }

    #region 校验方法
    /// <summary>
    /// 校验监听的组编号是否在合法的keyIds列表中
    /// </summary>
    private void ValidateTargetGroupNumber()
    {
        if (!IsTargetGroupValid())
        {
            Debug.LogError($"【{gameObject.name}】监听的组编号{targetGroupNumber}不在合法的keyIds列表中！已禁用组件");
            enabled = false;
        }
        else
        {
            Debug.Log($"【{gameObject.name}】监听的组编号{targetGroupNumber}校验通过（在合法keyIds列表中）");
        }
    }

    /// <summary>
    /// 检查目标组编号是否在ChartData的keyIds列表中
    /// </summary>
    /// <returns>是否合法</returns>
    private bool IsTargetGroupValid()
    {
        if (ChartData.Instance == null || ChartData.Instance.keyIds == null || ChartData.Instance.keyIds.Count == 0)
        {
            Debug.LogWarning($"【{gameObject.name}】ChartData或keyIds未加载，暂时放行组编号{targetGroupNumber}的监听");
            return true; // 未加载时暂时放行，等加载完成后再校验
        }

        bool isValid = ChartData.Instance.keyIds.Contains(targetGroupNumber);
        if (!isValid)
        {
            Debug.LogWarning($"【{gameObject.name}】组编号{targetGroupNumber}不在ChartData的keyIds列表中！");
        }
        return isValid;
    }
    #endregion
}