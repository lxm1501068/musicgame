using UnityEngine;
using System.Collections;
using System.Collections.Generic;  // 添加 List 命名空间

public class GameAutoStarter : MonoBehaviour
{
    [Header("加载界面UI（需在场景中预先放置）")]
    public GameObject loadingPanel;

    [Header("最小加载显示时间（秒）")]
    public float minimumLoadingTime = 2f;

    // 用于保存被隐藏的按键精灵的 SpriteRenderer
    private List<SpriteRenderer> keyRenderers = new List<SpriteRenderer>();

    private void Start()
    {
        // 显示加载界面
        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        // 隐藏所有按键精灵
        HideAllKeySprites();

        // 开始加载谱面（禁止自动播放，由我们控制播放时机）
        GameManager.Instance.LoadAndParseChart(GameManager.Instance.initialChartFileName, false);

        // 启动协程等待加载完成并确保最少显示时间
        StartCoroutine(WaitForLoadAndStart());
    }

    /// <summary>
    /// 查找并禁用所有按键的 SpriteRenderer
    /// </summary>
    private void HideAllKeySprites()
    {
        // 查找场景中所有 KeySpriteSwitcher 组件（包括未激活的物体，但一般按键都是激活的）
        var switchers = FindObjectsOfType<KeySpriteSwitcher>(true);
        foreach (var switcher in switchers)
        {
            SpriteRenderer sr = switcher.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.enabled = false;          // 禁用渲染，按键不可见
                keyRenderers.Add(sr);        // 保存引用以便后续恢复
            }
        }
    }

    /// <summary>
    /// 恢复所有被禁用的按键 SpriteRenderer
    /// </summary>
    private void ShowAllKeySprites()
    {
        foreach (var sr in keyRenderers)
        {
            if (sr != null)
                sr.enabled = true;
        }
        keyRenderers.Clear();  // 清空列表，避免重复恢复
    }

    private IEnumerator WaitForLoadAndStart()
    {
        float startTime = Time.time;

        // 等待加载解析完成
        while (!GameManager.Instance.IsChartLoadedAndParsed)
        {
            yield return null;
        }

        // 确保最小显示时间
        float elapsed = Time.time - startTime;
        if (elapsed < minimumLoadingTime)
        {
            yield return new WaitForSeconds(minimumLoadingTime - elapsed);
        }

        // 恢复按键显示（此时加载已完成，即将隐藏 loadingPanel）
        ShowAllKeySprites();

        // 隐藏加载界面
        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        // 手动开始播放谱面
        GameManager.Instance.PlayChart();
    }
}