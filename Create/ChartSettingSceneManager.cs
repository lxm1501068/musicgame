using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;

/// <summary>
/// 谱面设置场景管理器
/// 负责管理谱面配置（总时长、轨道按键ID 等）的编辑
/// </summary>
public class ChartSettingSceneManager : MonoBehaviour
{
    [Header("谱面设置 UI")]
    public TMP_InputField totalDurationInput;      // 总时长输入框
    public TMP_InputField keyIdsInput;             // KeyIds 输入框（逗号分隔整数）
    public Button confirmSettingsButton;           // 确认设置按钮
    public Button cancelButton;                    // 取消按钮
    public Button backToCreateButton;              // 返回创建场景按钮
    
    [Header("信息提示")]
    public TextMeshProUGUI infoText;               // 显示操作提示信息
    
    [Header("可选：时间滑杆")]
    public Slider timeSlider;                      // 用于滑动选择时间范围

    private float originalDuration;                // 保存原始时长（用于取消时恢复）
    private List<int> originalKeyIds;              // 保存原始 KeyIds（用于取消时恢复）
    private string editingChartFileName;           // 当前编辑的谱面文件名

    void Start()
    {
        // 获取当前编辑的谱面文件名
        editingChartFileName = PlayerPrefs.GetString("EditingChartFileName", "");
        
        // 初始化按钮监听
        InitializeButtons();
        
        // 从 PlayerPrefs 或 ChartData 加载当前谱面设置到 UI
        LoadChartSettingsToUI();
    }

    /// <summary>
    /// 初始化所有按钮的点击事件
    /// </summary>
    private void InitializeButtons()
    {
        if (confirmSettingsButton != null)
        {
            confirmSettingsButton.onClick.RemoveAllListeners();
            confirmSettingsButton.onClick.AddListener(OnConfirmSettings);
        }
        
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancel);
        }
        
        if (backToCreateButton != null)
        {
            backToCreateButton.onClick.RemoveAllListeners();
            backToCreateButton.onClick.AddListener(OnBackToCreate);
        }
    }

    /// <summary>
    /// 从 PlayerPrefs 或 ChartData 加载当前值到 UI 输入框
    /// </summary>
    private void LoadChartSettingsToUI()
    {
        // 尝试从 PlayerPrefs 读取（如果从 CreateScene 跳转过来的）
        bool hasPlayerPrefs = PlayerPrefs.HasKey("Chart_TotalDuration");
        
        if (hasPlayerPrefs)
        {
            // 从 PlayerPrefs 读取
            originalDuration = PlayerPrefs.GetFloat("Chart_TotalDuration");
            string keyIdsStr = PlayerPrefs.GetString("Chart_KeyIds", "");
            
            if (!string.IsNullOrEmpty(keyIdsStr))
            {
                string[] parts = keyIdsStr.Split(',');
                originalKeyIds = new List<int>();
                foreach (string part in parts)
                {
                    if (int.TryParse(part.Trim(), out int id))
                    {
                        originalKeyIds.Add(id);
                    }
                }
            }
            else
            {
                originalKeyIds = new List<int>();
            }
            
            // 同步到 ChartData
            ChartData.Instance.totalDuration = originalDuration;
            ChartData.Instance.keyIds = new List<int>(originalKeyIds);
        }
        else
        {
            // 直接从 ChartData 读取
            originalDuration = ChartData.Instance.totalDuration;
            originalKeyIds = new List<int>(ChartData.Instance.keyIds);
        }
        
        // 更新 UI 显示
        if (totalDurationInput != null)
        {
            totalDurationInput.text = originalDuration.ToString(CultureInfo.InvariantCulture);
            
            // 更新时间滑杆范围
            if (timeSlider != null)
            {
                timeSlider.minValue = 0;
                timeSlider.maxValue = originalDuration;
            }
        }

        if (keyIdsInput != null)
        {
            // 将 List<int> 转为逗号分隔的字符串
            string keyIdsStr = string.Join(",", originalKeyIds);
            keyIdsInput.text = keyIdsStr;
        }
        
        string fileNameDisplay = string.IsNullOrEmpty(editingChartFileName) ? "新建谱面" : editingChartFileName;
        infoText.text = $"正在编辑谱面设置：{fileNameDisplay}";
        
        Debug.Log($"ChartSettingSceneManager: 加载设置 | 总时长：{originalDuration} | KeyIds: [{string.Join(",", originalKeyIds)}]");
    }

    /// <summary>
    /// 确认设置：将 UI 中的值写入 ChartData 并保存到 PlayerPrefs
    /// </summary>
    private void OnConfirmSettings()
    {
        bool hasError = false;
        string errorMsg = "";

        // 1. 解析 totalDuration
        if (totalDurationInput != null)
        {
            if (float.TryParse(totalDurationInput.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float duration))
            {
                if (duration >= 0)
                {
                    ChartData.Instance.totalDuration = duration;
                    
                    // 保存到 PlayerPrefs
                    PlayerPrefs.SetFloat("Chart_TotalDuration", duration);
                    
                    // 同步更新 timeSlider 的范围
                    if (timeSlider != null)
                    {
                        timeSlider.maxValue = duration;
                    }
                }
                else
                {
                    hasError = true;
                    errorMsg += "总时长不能为负数；";
                }
            }
            else
            {
                hasError = true;
                errorMsg += "总时长格式无效（应为数字）；";
            }
        }

        // 2. 解析 keyIds (逗号分隔的整数列表)
        if (keyIdsInput != null)
        {
            string input = keyIdsInput.text.Trim();
            List<int> newKeyIds = new List<int>();

            if (!string.IsNullOrEmpty(input))
            {
                string[] parts = input.Split(',');
                foreach (string part in parts)
                {
                    string trimmed = part.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    if (int.TryParse(trimmed, out int id))
                    {
                        newKeyIds.Add(id);
                    }
                    else
                    {
                        hasError = true;
                        errorMsg += $"无效的按键ID: {trimmed}；";
                    }
                }
            }

            if (!hasError)
            {
                // 去重 + 排序
                newKeyIds = newKeyIds.Distinct().OrderBy(x => x).ToList();
                ChartData.Instance.keyIds = newKeyIds;
                
                // 保存到 PlayerPrefs
                string keyIdsStr = string.Join(",", newKeyIds);
                PlayerPrefs.SetString("Chart_KeyIds", keyIdsStr);
            }
        }

        // 显示结果
        if (hasError)
        {
            infoText.text = $"设置更新失败：{errorMsg}";
        }
        else
        {
            PlayerPrefs.Save();
            infoText.text = "谱面设置已更新 (totalDuration, keyIds)";
            Debug.Log($"ChartSettingSceneManager: 谱面设置已保存 | 总时长：{ChartData.Instance.totalDuration} | KeyIds: [{string.Join(",", ChartData.Instance.keyIds)}]");
        }
    }

    /// <summary>
    /// 取消修改：恢复原始值
    /// </summary>
    private void OnCancel()
    {
        // 恢复原始值
        ChartData.Instance.totalDuration = originalDuration;
        ChartData.Instance.keyIds = new List<int>(originalKeyIds);
        
        // 重新加载到 UI
        LoadChartSettingsToUI();
        
        infoText.text = "已取消修改，恢复到原始设置";
        Debug.Log("ChartSettingSceneManager: 已取消修改");
    }

    /// <summary>
    /// 返回创建场景
    /// </summary>
    private void OnBackToCreate()
    {
        // 先保存当前设置
        OnConfirmSettings();
        
        // 加载创建场景
        string createSceneName = "CreateScene";
        
        UnityEngine.SceneManagement.SceneManager.LoadScene(createSceneName);
        Debug.Log($"ChartSettingSceneManager: 返回创建场景：{createSceneName}");
    }

    /// <summary>
    /// 外部调用：刷新 UI 显示（当 ChartData 被外部修改时）
    /// </summary>
    public void RefreshUI()
    {
        LoadChartSettingsToUI();
    }
}