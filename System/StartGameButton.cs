using UnityEngine;
using UnityEngine.UI;

public class StartGameButton : MonoBehaviour
{
    private Button _button;

    void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.AddListener(StartGameLogic);
        }
        else
        {
            Debug.LogError("按钮物体上没有挂载Button组件！");
        }
    }

    private void StartGameLogic()
    {
        // 流程：加载解析谱面 → （GameManager内部自动初始化Key → 预创建音符 → 播放）
        GameManager.Instance.LoadAndParseChart(GameManager.Instance.initialChartFileName);

        gameObject.SetActive(false);
        Debug.Log("按钮点击成功，游戏启动流程已触发！");
    }
}