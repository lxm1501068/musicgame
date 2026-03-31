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
    // ---------- 选择与编辑方法 ----------
    private void SelectNote(NoteObject noteComp)
    {
        selectedObject = noteComp.gameObject;
        selectedType = SelectedType.Note;
        selectedCommand = noteComp.command;

        UpdateInfoPanelForNote();
    }

    private void SelectKey(KeyObject keyComp)
    {
        selectedObject = keyComp.gameObject;
        selectedType = SelectedType.Key;
        selectedKeyData = keyComp.keyData;
        selectedKeyCommand = selectedKeyData.keyCommands.FirstOrDefault(); // 默认选中第一个指令

        UpdateInfoPanelForKey();
    }

    private void Deselect()
    {
        selectedObject = null;
        selectedType = SelectedType.None;
        selectedCommand = null;
        selectedKeyData = null;
        currentAddStep = AddStep.None;

        // 隐藏所有编辑控件
        keyIndexInputField.gameObject.SetActive(false);
        keyNameInputField.gameObject.SetActive(false);
        noteTypeInputField.gameObject.SetActive(false); // 隐藏类型输入框
        if (changeNoteTypeBtn != null) changeNoteTypeBtn.gameObject.SetActive(false);
        if (finishBtn != null) finishBtn.gameObject.SetActive(false);
        if (moveOptionPanel != null) moveOptionPanel.SetActive(false);

        // 隐藏指令管理相关
        if (cmdModeToggleBtn != null) cmdModeToggleBtn.gameObject.SetActive(false);
        if (cmdDropdown != null) cmdDropdown.gameObject.SetActive(false);
        if (existingCmdsDropdown != null) existingCmdsDropdown.gameObject.SetActive(false);
        if (cmdConfirmBtn != null) cmdConfirmBtn.gameObject.SetActive(false);
        HideCommandDetailFields();

        // 显示下拉列表，用于放置模式选择
        noteTypeDropdown.gameObject.SetActive(true);
        noteTypeDropdown.value = 0; // 重置为 (empty)
        timeInputField.gameObject.SetActive(true); // 时间输入框保持显示（可用于放置时输入时间）
        if (timeSlider != null) timeSlider.gameObject.SetActive(true);

        // 清空信息文本，不显示任何消息
        infoText.text = "";
    }

    private void UpdateInfoPanelForNote()
    {
        if (selectedCommand == null) return;

        // 填充基本信息，但不强制显示详细 UI 框
        infoText.text = $"选中音符 [编号 {selectedCommand.num}]\n" +
                       $"判定时间: {selectedCommand.timeB}\n" +
                       $"类型: {selectedCommand.type}";

        // 隐藏不相关的输入框
        timeInputField.gameObject.SetActive(false);
        if (timeSlider != null) timeSlider.gameObject.SetActive(false);
        keyIndexInputField.gameObject.SetActive(false);
        keyNameInputField.gameObject.SetActive(false);
        noteTypeInputField.gameObject.SetActive(false);
        noteTypeDropdown.gameObject.SetActive(false);

        // 显示核心按钮
        if (changeNoteTypeBtn != null) changeNoteTypeBtn.gameObject.SetActive(true);
        if (finishBtn != null) finishBtn.gameObject.SetActive(true);

        // 更新指令管理 UI（显示 Add/Delete 切换）
        UpdateCmdManagementUI();
    }

    private void OnChangeNoteTypeBtnClicked()
    {
        if (selectedType != SelectedType.Note || selectedCommand == null) return;
        
        // 循环切换类型
        int nextType = ((int)selectedCommand.type + 1) % Enum.GetValues(typeof(NoteType)).Length;
        selectedCommand.type = (NoteType)nextType;

        // 更新精灵
        SpriteRenderer sr = selectedObject.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            int idx = GetNoteTypeIndex(selectedCommand.type);
            if (idx >= 0 && idx < noteSprites.Length)
                sr.sprite = noteSprites[idx];
        }

        UpdateInfoPanelForNote();
    }

    private void UpdateInfoPanelForKey()
    {
        if (selectedKeyData == null) return;

        infoText.text = $"选中按键 [ID {selectedKeyData.keyName}] (可编辑)\n" +
                       $"位置: ({selectedKeyData.x}, {selectedKeyData.y})\n" +
                       $"显示: {(selectedKeyData.show == 1 ? "是" : "否")}\n" +
                       $"移动指令数: {selectedKeyData.keyCommands.Count} (可编辑)";

        if (selectedKeyCommand != null)
        {
            infoText.text += $"\n\n当前指令: {selectedKeyCommand.cmdType}\n" +
                            $"时间: {selectedKeyCommand.startTime} -> {selectedKeyCommand.endTime}\n" +
                            $"起始: ({selectedKeyCommand.x1}, {selectedKeyCommand.y1})\n" +
                            $"终点: ({selectedKeyCommand.x2}, {selectedKeyCommand.y2})";
            if (!string.IsNullOrEmpty(selectedKeyCommand.json_filename))
                infoText.text += $"\nJSON: {selectedKeyCommand.json_filename}";
        }

        // 显示并设置 keyName 输入框
        keyNameInputField.gameObject.SetActive(true);
        keyNameInputField.text = selectedKeyData.keyName.ToString();

        // 隐藏音符专用的输入框
        keyIndexInputField.gameObject.SetActive(false);
        noteTypeInputField.gameObject.SetActive(false); // 隐藏类型输入框
        noteTypeDropdown.gameObject.SetActive(false);    // 隐藏下拉列表（按键无需类型）
        timeInputField.gameObject.SetActive(false);     // 按键不需要时间编辑
        if (timeSlider != null) timeSlider.gameObject.SetActive(false);

        // 更新指令管理 UI
        UpdateCmdManagementUI();
    }

    // ---------- 编辑事件回调 ----------
    // 类型输入框文本变化回调：更新选中音符的类型
    private void OnNoteTypeInputChanged(string value)
    {
        if (selectedType != SelectedType.Note || selectedCommand == null) return;

        // 尝试将字符串解析为 NoteType 枚举（忽略大小写）
        if (Enum.TryParse(value, true, out NoteType newType))
        {
            selectedCommand.type = newType;

            // 更新精灵
            SpriteRenderer sr = selectedObject.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                int idx = GetNoteTypeIndex(newType);
                if (idx >= 0 && idx < noteSprites.Length)
                    sr.sprite = noteSprites[idx];
            }

            // 刷新信息显示
            UpdateInfoPanelForNote();
        }
        else
        {
            // 可选：显示错误提示
            infoText.text = $"无效的类型名称: {value}，请使用 Tap/Hold/DTap/Flick/Drag/Key";
        }
    }

    private void OnTimeChanged(string value)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float newTimeB))
        {
            // 同步 Slider (不触发 OnTimeSliderChanged)
            if (timeSlider != null)
            {
                timeSlider.onValueChanged.RemoveListener(OnTimeSliderChanged);
                timeSlider.value = newTimeB;
                timeSlider.onValueChanged.AddListener(OnTimeSliderChanged);
            }

            if (selectedType != SelectedType.Note || selectedCommand == null) return;

            float duration = selectedCommand.timeB - selectedCommand.timeA;
            selectedCommand.timeB = newTimeB;
            selectedCommand.timeA = newTimeB - (duration > 0 ? duration : 1f); // 保持持续时间或设为 1s
            
            // 新增：修改时间后重新排序
            ChartData.Instance.SortCommandsByTime();
            
            UpdateInfoPanelForNote();
        }
    }

    private void OnTimeSliderChanged(float value)
    {
        // 同步 InputField (触发 OnTimeChanged)
        timeInputField.text = value.ToString(CultureInfo.InvariantCulture);
    }

    private void OnKeyIndexChanged(string value)
    {
        if (selectedType != SelectedType.Note || selectedCommand == null || selectedCommand.commandName != "drop_to") return;

        if (int.TryParse(value, out int newKeyIndex))
        {            selectedCommand.key_name = newKeyIndex;
            UpdateInfoPanelForNote();
        }
    }

    private void OnKeyNameChanged(string value)
    {
        if (selectedType != SelectedType.Key || selectedKeyData == null) return;

        if (int.TryParse(value, out int newKeyId))
        {
            // 新增：检查 ID 是否冲突
            if (ChartData.Instance.keyDatas.Any(k => k != selectedKeyData && k.keyName == newKeyId))
            {
                infoText.text = $"Key ID {newKeyId} 已被其他按键使用！";
                return;
            }

            // 新增：同步更新关联该按键的音符
            int oldKeyId = selectedKeyData.keyName;
            foreach (var cmd in ChartData.Instance.commands)
            {
                if (cmd.key_name == oldKeyId)
                {
                    cmd.key_name = newKeyId;
                }
            }

            selectedKeyData.keyName = newKeyId;

            SpriteRenderer sr = selectedObject.GetComponent<SpriteRenderer>();
            if (sr != null && newKeyId >= 1 && newKeyId <= keySprites.Length)
                sr.sprite = keySprites[newKeyId - 1];

            UpdateInfoPanelForKey();
        }
    }
}
