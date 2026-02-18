using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂载到开始游戏按钮上，点击即触发游戏开始
/// </summary>
public class StartGameButton : MonoBehaviour
{
    // 游戏管理器（若你的GameManager不是单例，可手动拖拽赋值）
    public GameManager gameManager;

    private Button _button;

    void Awake()
    {
        // 自动获取按钮组件
        _button = GetComponent<Button>();
        
        // 绑定点击事件：点击按钮 → 执行StartGameLogic方法
        if (_button != null)
        {
            _button.onClick.AddListener(StartGameLogic);
        }
        else
        {
            Debug.LogError("按钮物体上没有挂载Button组件！");
        }

        // 自动查找GameManager（避免手动拖拽，简化操作）
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
            if (gameManager == null)
            {
                Debug.LogError("场景中未找到GameManager，请确保场景中有该组件！");
            }
        }
    }

    /// <summary>
    /// 按钮点击后执行的核心逻辑
    /// </summary>
    private void StartGameLogic()
    {
        // 容错：检查GameManager是否有效
        if (gameManager == null)
        {
            Debug.LogError("GameManager为空，无法开始游戏！");
            return;
        }

        // 核心：调用GameManager的开始游戏方法
        gameManager.StartGame();

        // 关键修改：点击后直接隐藏按钮（整 GameObject 不可见）
        gameObject.SetActive(false);

        // 可选：如果需要仅隐藏按钮但保留物体，可替换为下面的方式（隐藏按钮的Image和Text）
        // Image buttonImage = _button.GetComponent<Image>();
        // Text buttonText = _button.GetComponentInChildren<Text>();
        // if (buttonImage != null) buttonImage.enabled = false;
        // if (buttonText != null) buttonText.enabled = false;

        //Debug.Log("按钮点击成功，游戏已启动，按钮已隐藏！");
    }
}