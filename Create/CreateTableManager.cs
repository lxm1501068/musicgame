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

    [Header("Rename Dialog")]
    public GameObject renameDialogPanel; // 重命名对话框面板
    public TMP_InputField renameInputField; // 重命名输入框
    public Button renameConfirmBtn;      // 确认重命名按钮
    public Button renameCancelBtn;       // 取消重命名按钮

    // 新增：临时解析谱面的辅助对象（需在场景中挂载LoadChart脚本，或动态创建）
    public LoadChart loadChartHelper;

    // 重命名相关状态
    private string renamingFileName = ""; // 当前正在重命名的文件名

    private void Start()
    {
        // 绑定按钮事件
        if (addNewButton != null)
            addNewButton.onClick.AddListener(OnAddNewChart);
            
        if (backButton != null)
            backButton.onClick.AddListener(OnBackToStart);

        // 初始化重命名对话框
        InitializeRenameDialog();

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

            try
            {
                // PC端直接读取文件
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

        // 5. 添加右键菜单功能
        AddRightClickMenu(item, fileName);
    }

    /// <summary>
    /// 为谱面条目添加右键菜单
    /// </summary>
    private void AddRightClickMenu(GameObject item, string fileName)
    {
        // 添加 EventTrigger 组件来监听右键点击
        var eventTrigger = item.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (eventTrigger == null)
        {
            eventTrigger = item.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        }

        // 创建右键点击触发器
        var rightClickTrigger = new UnityEngine.EventSystems.EventTrigger.TriggerEvent();
        rightClickTrigger.AddListener((eventData) => OnChartItemRightClick(fileName, eventData));

        // 绑定右键点击事件
        var entry = new UnityEngine.EventSystems.EventTrigger.Entry();
        entry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerClick;
        entry.callback = rightClickTrigger;
        eventTrigger.triggers.Add(entry);

        Debug.Log($"[CreateTableManager] 已为谱面 {fileName} 添加右键菜单");
    }

    /// <summary>
    /// 处理谱面条目右键点击
    /// </summary>
    private void OnChartItemRightClick(string fileName, UnityEngine.EventSystems.BaseEventData eventData)
    {
        var pointerData = eventData as UnityEngine.EventSystems.PointerEventData;
        
        // 检查是否是右键点击（鼠标右键 = 1）
        if (pointerData != null && pointerData.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
        {
            Debug.Log($"[CreateTableManager] 右键点击谱面: {fileName}");
            ShowRenameDialog(fileName);
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
        // 先进入ChartSettingScene进行设置
        SceneManager.LoadScene("ChartSettingScene");
    }

    /// <summary>
    /// 点击添加新谱面
    /// </summary>
    private void OnAddNewChart()
    {
        // 清除编辑标记，表示新建
        PlayerPrefs.DeleteKey("EditingChartFileName");
        PlayerPrefs.DeleteKey("EditingChartPath");
        // 先进入ChartSettingScene进行设置
        SceneManager.LoadScene("ChartSettingScene");
    }

    /// <summary>
    /// 返回主界面
    /// </summary>
    private void OnBackToStart()
    {
        SceneManager.LoadScene(startSceneName);
    }

    #region 重命名功能

    /// <summary>
    /// 初始化重命名对话框
    /// </summary>
    private void InitializeRenameDialog()
    {
        if (renameDialogPanel != null)
        {
            renameDialogPanel.SetActive(false); // 初始隐藏
        }

        if (renameConfirmBtn != null)
        {
            renameConfirmBtn.onClick.AddListener(OnRenameConfirm);
        }

        if (renameCancelBtn != null)
        {
            renameCancelBtn.onClick.AddListener(OnRenameCancel);
        }
    }

    /// <summary>
    /// 显示重命名对话框
    /// </summary>
    private void ShowRenameDialog(string fileName)
    {
        renamingFileName = fileName;

        if (renameDialogPanel != null)
        {
            renameDialogPanel.SetActive(true);
        }

        if (renameInputField != null)
        {
            // 去掉 .txt 扩展名，只显示文件名
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            renameInputField.text = nameWithoutExt;
            renameInputField.ActivateInputField(); // 自动聚焦
            renameInputField.Select();
        }

        Debug.Log($"[CreateTableManager] 打开重命名对话框: {fileName}");
    }

    /// <summary>
    /// 确认重命名
    /// </summary>
    private void OnRenameConfirm()
    {
        if (string.IsNullOrEmpty(renamingFileName))
        {
            Debug.LogWarning("[CreateTableManager] 重命名失败：未选择文件");
            OnRenameCancel();
            return;
        }

        if (renameInputField == null)
        {
            Debug.LogError("[CreateTableManager] 重命名失败：输入框为空");
            OnRenameCancel();
            return;
        }

        string newName = renameInputField.text.Trim();

        // 验证新名称
        if (string.IsNullOrEmpty(newName))
        {
            Debug.LogWarning("[CreateTableManager] 重命名失败：新名称不能为空");
            return;
        }

        // 检查是否包含非法字符
        if (IsFileNameInvalid(newName))
        {
            Debug.LogWarning($"[CreateTableManager] 重命名失败：名称 '{newName}' 包含非法字符");
            return;
        }

        // 添加 .txt 扩展名
        if (!newName.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase))
        {
            newName += ".txt";
        }

        // 执行重命名
        bool success = RenameChartFile(renamingFileName, newName);

        if (success)
        {
            Debug.Log($"[CreateTableManager] 重命名成功: {renamingFileName} → {newName}");
            RefreshChartList(); // 刷新列表
        }
        else
        {
            Debug.LogError($"[CreateTableManager] 重命名失败: {renamingFileName} → {newName}");
        }

        OnRenameCancel(); // 关闭对话框
    }

    /// <summary>
    /// 取消重命名
    /// </summary>
    private void OnRenameCancel()
    {
        renamingFileName = "";

        if (renameDialogPanel != null)
        {
            renameDialogPanel.SetActive(false);
        }

        if (renameInputField != null)
        {
            renameInputField.text = "";
        }
    }

    /// <summary>
    /// 重命名谱面文件
    /// </summary>
    private bool RenameChartFile(string oldFileName, string newFileName)
    {
        try
        {
            string createPath = Path.Combine(Application.streamingAssetsPath, "Create");
            string oldFilePath = Path.Combine(createPath, oldFileName);
            string newFilePath = Path.Combine(createPath, newFileName);

            // 检查原文件是否存在
            if (!File.Exists(oldFilePath))
            {
                Debug.LogError($"[CreateTableManager] 原文件不存在: {oldFilePath}");
                return false;
            }

            // 检查新文件名是否已存在
            if (File.Exists(newFilePath) && oldFileName != newFileName)
            {
                Debug.LogError($"[CreateTableManager] 目标文件已存在: {newFilePath}");
                return false;
            }

            // 执行重命名
            File.Move(oldFilePath, newFilePath);
            Debug.Log($"[CreateTableManager] 文件重命名成功: {oldFileName} → {newFileName}");

            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CreateTableManager] 重命名文件时发生异常: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// 检查文件名是否包含非法字符
    /// </summary>
    private bool IsFileNameInvalid(string fileName)
    {
        // Windows 非法字符: < > : " / \ | ? *
        char[] invalidChars = Path.GetInvalidFileNameChars();
        
        foreach (char c in fileName)
        {
            if (invalidChars.Contains(c))
            {
                return true;
            }
        }

        return false;
    }

    #endregion
}