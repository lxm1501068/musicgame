using UnityEngine;

public class SpriteSwitcher : MonoBehaviour
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

    void Start()
    {
        // 获取当前物体上的SpriteRenderer组件
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 初始化检查
        if (spriteRenderer == null)
        {
            Debug.LogError($"【{gameObject.name}】物体上未挂载SpriteRenderer组件！请添加后再运行。");
            return;
        }
        if (sprite1 == null || sprite2 == null)
        {
            Debug.LogError($"【{gameObject.name}】请在编辑器中为sprite1和sprite2赋值对应的Sprite资源！");
            return;
        }

        // 初始状态：默认显示sprite2（未收到信号）
        spriteRenderer.sprite = sprite2;

        //Debug.Log($"【{gameObject.name}】初始化完成 | 监听组编号：{targetGroupNumber} | 按住保持显示：{keepVisibleWhileHeld}");
    }

    void Update()
    {
        // 空引用保护：组件或Sprite未赋值时直接返回
        if (spriteRenderer == null || sprite1 == null || sprite2 == null) return;

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
}