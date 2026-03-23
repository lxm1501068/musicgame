using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using System.Globalization;
using TMPro;
using System;

public partial class CreateSceneManager
{
    // ========== 新增：谱面设置相关方法 ==========
    /// <summary>
    /// 从 ChartData 加载当前值到 UI 输入框
    /// </summary>
    private void LoadChartSettingsToUI()
    {
        if (totalDurationInput != null)
        {
            float duration = ChartData.Instance.totalDuration;
            totalDurationInput.text = duration.ToString(CultureInfo.InvariantCulture);
            
            // 更新时间滑杆范围
            if (timeSlider != null)
            {
                timeSlider.minValue = 0;
                timeSlider.maxValue = duration;
            }
        }

        if (keyIdsInput != null)
        {
            // 将 List<int> 转为逗号分隔的字符串
            string keyIdsStr = string.Join(",", ChartData.Instance.keyIds);
            keyIdsInput.text = keyIdsStr;
        }
    }

    /// <summary>
    /// 切换设置面板的显示/隐藏
    /// </summary>
    private void ToggleSettingsPanel()
    {
        if (settingsPanel != null)
        {
            bool isActive = !settingsPanel.activeSelf;
            settingsPanel.SetActive(isActive);

            // 打开面板时重新加载当前值，确保显示最新数据
            if (isActive)
                LoadChartSettingsToUI();
        }
    }

    /// <summary>
    /// 确认设置：将 UI 中的值写入 ChartData
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
                        // 可选：避免重复添加，这里允许重复，但通常 keyIds 应该唯一
                        newKeyIds.Add(id);
                    }
                    else
                    {
                        hasError = true;
                        errorMsg += $"无效的按键 ID: {trimmed}；";
                    }
                }
            }

            if (!hasError)
            {
                ChartData.Instance.keyIds = newKeyIds;
            }
        }

        // 显示结果
        if (hasError)
        {
            infoText.text = $"设置更新失败: {errorMsg}";
        }
        else
        {
            infoText.text = "谱面设置已更新 (totalDuration, keyIds)";
            // 更新 UI 和 Slider 范围
            LoadChartSettingsToUI();
            // 可选：自动关闭设置面板
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
        }
    }
}
