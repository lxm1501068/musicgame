using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;
using TMPro;
using System.Globalization;
using System.Linq;
using System;

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

    // 新增：临时解析谱面的辅助对象（需在场景中挂载LoadChart脚本，或动态创建）
    public LoadChart loadChartHelper;

    private void Start()
    {
        // 绑定按钮事件
        if (addNewButton != null)
            addNewButton.onClick.AddListener(OnAddNewChart);
            
        if (backButton != null)
            backButton.onClick.AddListener(OnBackToStart);

        // 加载并显示谱面列表（含时长、按键数）
        RefreshChartList();
    }

    /// <summary>
    /// 刷新谱面列表显示（新增：解析时长和按键数）
    /// </summary>
    public void RefreshChartList()
    {
        // 清空旧列表
        foreach (Transform child in listParent)
        {
            Destroy(child.gameObject);
        }

        // 修正路径：读取 StreamingAssets/Create/ 下的 .txt 文件
        string createPath = Path.Combine(Application.streamingAssetsPath, "Create");
        if (!Directory.Exists(createPath))
        {
            Directory.CreateDirectory(createPath);
            Debug.LogWarning($"Create目录不存在，已创建：{createPath}");
            return;
        }

        string[] files = Directory.GetFiles(createPath, "*.txt");

        foreach (string filePath in files)
        {
            string fileName = Path.GetFileName(filePath);
            
            // 过滤掉 meta 文件
            if (fileName.EndsWith(".meta")) continue;

            // 解析谱面的时长和按键数
            if (ParseChartInfo(filePath, out int keyCount, out float totalDuration))
            {
                CreateChartItem(fileName, keyCount, totalDuration);
            }
            else
            {
                // 解析失败时仍显示条目，但标注异常
                CreateChartItem(fileName, -1, -1);
            }
        }
    }

    /// <summary>
    /// 解析单个谱面文件的按键数和时长（复用LoadChart的核心解析逻辑）
    /// </summary>
    /// <param name="filePath">谱面文件完整路径</param>
    /// <param name="keyCount">输出：按键数</param>
    /// <param name="totalDuration">输出：总时长</param>
    /// <returns>是否解析成功</returns>
    private bool ParseChartInfo(string filePath, out int keyCount, out float totalDuration)
    {
        keyCount = -1;
        totalDuration = -1;

        try
        {
            string chartContent = string.Empty;

            // 分平台读取文件（兼容移动端）
            if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
            {
                // 移动端需用UnityWebRequest读取StreamingAssets
                string url = $"file://{filePath}";
                using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequest.Get(url))
                {
                    // 发送请求
                    var operation = www.SendWebRequest();
                    // 同步等待（仅在编辑器/初始化时使用，非主线程阻塞场景可忽略）
                    while (!operation.isDone) { }

#if UNITY_2020_1_OR_NEWER
                    if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
#else
                    if (!string.IsNullOrEmpty(www.error))
#endif
                    {
                        Debug.LogError($"读取移动端谱面失败：{www.error} | 路径：{filePath}");
                        return false;
                    }
                    chartContent = www.downloadHandler.text;
                }
            }
            else
            {
                try
                {
                    // 电脑端直接读取
                    if (!File.Exists(filePath))
                    {
                        Debug.LogError($"谱面文件不存在：{filePath}");
                        return false;
                    }
                    chartContent = File.ReadAllText(filePath);
                }
                catch (IOException ex)
                {
                    Debug.LogError($"读取谱面文件时发生IO异常：{ex.Message} | 路径：{filePath}");
                    return false;
                }
            }

            // 解析谱面头部（轨道按键、时长）—— 复用LoadChart的核心逻辑
            var allLines = chartContent.Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .ToList();

            // 步骤1：读取轨道头部4行（轨道ID、指令数、音符数、总时长）
            int lineIndex = 0;
            lineIndex++; // 跳过第一行注释
            List<string> trackKeyLines = new List<string>();
            int headerLineCount = 0;

            for (; lineIndex < allLines.Count && headerLineCount < 4; lineIndex++)
            {
                string line = allLines[lineIndex].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                trackKeyLines.Add(line);
                headerLineCount++;
            }

            if (trackKeyLines.Count != 4)
            {
                // 新增：详细错误信息，包含行数信息
                Debug.LogError($"谱面头部行数异常：{filePath} | 期望4行，实际{trackKeyLines.Count}行 | 内容：{string.Join("\n", trackKeyLines)}");
                return false;
            }

            // 解析按键数（第一行：轨道按键列表）
            var keyIdStrs = trackKeyLines[0].Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            List<int> keyIds = new List<int>();
            foreach (var str in keyIdStrs)
            {
                if (int.TryParse(str.Trim(), out int keyId))
                {
                    keyIds.Add(keyId);
                }
            }
            keyIds = keyIds.Distinct().ToList();
            keyCount = keyIds.Count;

            // 解析总时长（第四行）
            if (!float.TryParse(trackKeyLines[3], NumberStyles.Float, CultureInfo.InvariantCulture, out totalDuration))
            {
                Debug.LogError($"时长解析失败：{filePath} | 内容：{trackKeyLines[3]}");
                return false;
            }

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析谱面信息异常：{filePath} | {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// 创建单个谱面条目 UI（新增：显示按键数、时长）
    /// </summary>
    private void CreateChartItem(string fileName, int keyCount, float totalDuration)
    {
        if (chartItemPrefab == null) return;

        GameObject item = Instantiate(chartItemPrefab, listParent);
        
        // 1. 设置文件名文本（默认第一个TextMeshProUGUI）
        TextMeshProUGUI nameText = item.GetComponentInChildren<TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text = fileName;
        }

        // 2. 设置按键数文本（查找命名为KeyCountText的组件）
        TextMeshProUGUI keyCountText = item.transform.Find("KeyCountText")?.GetComponent<TextMeshProUGUI>();
        if (keyCountText != null)
        {
            keyCountText.text = keyCount < 0 ? "按键数：解析失败" : $"按键数：{keyCount}";
        }

        // 3. 设置时长文本（查找命名为DurationText的组件）
        TextMeshProUGUI durationText = item.transform.Find("DurationText")?.GetComponent<TextMeshProUGUI>();
        if (durationText != null)
        {
            durationText.text = totalDuration < 0 ? "时长：解析失败" : $"时长：{totalDuration:F2}s";
        }

        // 4. 设置编辑按钮点击事件
        Button editBtn = item.GetComponentInChildren<Button>();
        if (editBtn != null)
        {
            // 传递完整路径（Create目录下）
            string fullPath = Path.Combine(Application.streamingAssetsPath, "Create", fileName);
            editBtn.onClick.AddListener(() => OnEditChart(fileName));
        }
    }

    /// <summary>
    /// 点击编辑已有谱面
    /// </summary>
    private void OnEditChart(string fileName)
    {
        // 记录要编辑的文件的完整相对路径（Create目录下）
        string relativePath = Path.Combine("Create", fileName);
        PlayerPrefs.SetString("EditingChartFileName", relativePath);
        SceneManager.LoadScene(createSceneName);
    }

    /// <summary>
    /// 点击添加新谱面
    /// </summary>
    private void OnAddNewChart()
    {
        // 清除编辑标记，表示新建
        PlayerPrefs.DeleteKey("EditingChartFileName");
        PlayerPrefs.DeleteKey("EditingChartPath");
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