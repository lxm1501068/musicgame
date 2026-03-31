using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;
using TMPro;

public class CreateTableManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform listParent;          // 谱面条目父对象
    public GameObject chartItemPrefab;   // 谱面条目预制体（包含文本和编辑按钮）
    public Button addNewButton;          // 添加新谱面按钮
    public Button backButton;            // 返回主界面按钮

    [Header("Scene Names")]
    public string startSceneName = "StartScene";
    public string createSceneName = "CreateScene";

    private void Start()
    {
        // 绑定按钮事件
        if (addNewButton != null)
            addNewButton.onClick.AddListener(OnAddNewChart);
            
        if (backButton != null)
            backButton.onClick.AddListener(OnBackToStart);

        // 加载并显示谱面列表
        RefreshChartList();
    }

    /// <summary>
    /// 刷新谱面列表显示
    /// </summary>
    public void RefreshChartList()
    {
        // 清空旧列表
        foreach (Transform child in listParent)
        {
            Destroy(child.gameObject);
        }

        // 获取 StreamingAssets 下的所有 .txt 文件
        string path = Application.streamingAssetsPath;
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        string[] files = Directory.GetFiles(path, "*.txt");

        foreach (string filePath in files)
        {
            string fileName = Path.GetFileName(filePath);
            
            // 过滤掉 meta 文件（虽然 GetFiles 已经过滤了后缀，但有些平台可能需要）
            if (fileName.EndsWith(".meta")) continue;

            CreateChartItem(fileName);
        }
    }

    /// <summary>
    /// 创建单个谱面条目 UI
    /// </summary>
    private void CreateChartItem(string fileName)
    {
        if (chartItemPrefab == null) return;

        GameObject item = Instantiate(chartItemPrefab, listParent);
        
        // 设置名称文本
        TextMeshProUGUI nameText = item.GetComponentInChildren<TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text = fileName;
        }

        // 设置编辑按钮点击事件
        Button editBtn = item.GetComponentInChildren<Button>();
        if (editBtn != null)
        {
            editBtn.onClick.AddListener(() => OnEditChart(fileName));
        }
    }

    /// <summary>
    /// 点击编辑已有谱面
    /// </summary>
    private void OnEditChart(string fileName)
    {
        // 记录要编辑的文件名，以便在 CreateScene 中加载
        PlayerPrefs.SetString("EditingChartFileName", fileName);
        SceneManager.LoadScene(createSceneName);
    }

    /// <summary>
    /// 点击添加新谱面
    /// </summary>
    private void OnAddNewChart()
    {
        // 清除编辑标记，表示新建
        PlayerPrefs.DeleteKey("EditingChartFileName");
        SceneManager.LoadScene(createSceneName);
    }

    /// <summary>
    /// 返回主界面
    /// </summary>
    private void OnBackToStart()
    {
        SceneManager.LoadScene(startSceneName);
    }
}
