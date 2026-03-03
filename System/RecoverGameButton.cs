using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 恢复游戏按钮（单例模式）
/// 暂停时显示，点击后恢复游戏
/// </summary>
public class RecoverButton : MonoBehaviour
{
    // 全局唯一单例实例
    public static RecoverButton Instance;

    [Header("UI组件引用（Unity编辑器赋值）")]
    public Button recoverButton; // 恢复按钮UI组件

    private void Awake()
    {
        // 严谨的单例实现（跨场景保留 + 防止重复创建）
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 跨场景保留按钮
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 初始化按钮（自动获取+绑定事件）
        InitRecoverButton();
    }

    /// <summary>
    /// 初始化恢复按钮（防呆+事件绑定）
    /// </summary>
    private void InitRecoverButton()
    {
        // 自动获取Button组件（若未手动赋值）
        if (recoverButton == null)
        {
            recoverButton = GetComponent<Button>();
            if (recoverButton == null)
            {
                Debug.LogError("RecoverButton: 未找到Button组件！请将脚本挂载到UI Button对象上");
                return;
            }
        }

        // 初始状态：隐藏按钮
        SetButtonVisible(false);

        // 绑定按钮点击事件
        recoverButton.onClick.RemoveAllListeners();
        recoverButton.onClick.AddListener(OnRecoverClick);
    }

    /// <summary>
    /// 设置按钮显示/隐藏
    /// </summary>
    /// <param name="isVisible">是否显示</param>
    public void SetButtonVisible(bool isVisible)
    {
        if (recoverButton != null)
        {
            recoverButton.gameObject.SetActive(isVisible);
        }
    }

    /// <summary>
    /// 按钮点击事件：恢复游戏
    /// </summary>
    private void OnRecoverClick()
    {
        // 空引用校验
        if (GameManager.Instance == null)
        {
            Debug.LogError("RecoverButton: GameManager实例不存在！");
            return;
        }

        // 调用GameManager的暂停/恢复方法
        GameManager.Instance.TogglePlay();
    }

    // 防止单例销毁后空引用
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}