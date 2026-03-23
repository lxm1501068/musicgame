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
    // ---------- 指令管理方法 ----------
    private void OnStartPosBtnClicked()
    {
        isCapturingStartPos = true;
        isCapturingEndPos = false;
        infoText.text = "点击鼠标左键捕捉起始坐标，右键取消";
    }

    private void OnEndPosBtnClicked()
    {
        isCapturingEndPos = true;
        isCapturingStartPos = false;
        infoText.text = "点击鼠标左键捕捉终止坐标，右键取消";
    }

    private void UpdatePosBtnTexts()
    {
        if (startPosBtn != null) startPosBtn.GetComponentInChildren<TextMeshProUGUI>().text = $"起始: ({capturedStartPos.x:F2}, {capturedStartPos.y:F2})";
        if (endPosBtn != null) endPosBtn.GetComponentInChildren<TextMeshProUGUI>().text = $"终止: ({capturedEndPos.x:F2}, {capturedEndPos.y:F2})";
    }

    private void HideCommandDetailFields()
    {
        if (startPosBtn != null) startPosBtn.gameObject.SetActive(false);
        if (endPosBtn != null) endPosBtn.gameObject.SetActive(false);
        if (startTimeInputField != null) startTimeInputField.gameObject.SetActive(false);
        if (endTimeInputField != null) endTimeInputField.gameObject.SetActive(false);
        if (extraParamInputField != null) extraParamInputField.gameObject.SetActive(false);
    }

    private void ToggleCmdMode()
    {
        isAddMode = !isAddMode;
        UpdateCmdManagementUI();
    }

    private void UpdateCmdManagementUI()
    {
        if (selectedType == SelectedType.None || 
           (selectedType == SelectedType.Note && selectedCommand == null) || 
           (selectedType == SelectedType.Key && selectedKeyData == null))
        {
            cmdModeToggleBtn.gameObject.SetActive(false);
            cmdDropdown.gameObject.SetActive(false);
            if (existingCmdsDropdown != null) existingCmdsDropdown.gameObject.SetActive(false);
            cmdConfirmBtn.gameObject.SetActive(false);
            return;
        }

        // 按钮显示文本
        TextMeshProUGUI btnText = cmdModeToggleBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null) btnText.text = isAddMode ? "切换到删除" : "切换到添加";

        TextMeshProUGUI confirmBtnText = cmdConfirmBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (confirmBtnText != null) confirmBtnText.text = isAddMode ? "添加指令" : "删除指令";

        cmdModeToggleBtn.gameObject.SetActive(true);
        cmdConfirmBtn.gameObject.SetActive(true);

        // 填充现有指令下拉列表
        if (existingCmdsDropdown != null)
        {
            existingCmdsDropdown.gameObject.SetActive(true);
            existingCmdsDropdown.ClearOptions();
            
            if (selectedType == SelectedType.Note)
            {
                var noteCmds = ChartData.Instance.commands.Where(c => c.num == selectedCommand.num).ToList();
                List<string> options = noteCmds.Select(c => {
                    string prefix = "";
                    if (c.isNoteFirstTimeOccured)
                    {
                        bool isScorable = true;
                        if (ChartData.Instance.isScorable.TryGetValue(c.num, out bool scorable))
                            isScorable = scorable;
                        prefix = isScorable ? "# " : "! ";
                    }
                    else
                    {
                        prefix = "% ";
                    }
                    return $"{prefix}{c.type.ToString().ToLower()} {c.num} {c.commandName} {c.timeA:F3} {c.timeB:F3}";
                }).ToList();
                existingCmdsDropdown.AddOptions(options);
                int currentIndex = noteCmds.IndexOf(selectedCommand);
                if (currentIndex >= 0)
                {
                    existingCmdsDropdown.onValueChanged.RemoveListener(OnExistingCmdSelected);
                    existingCmdsDropdown.value = currentIndex;
                    existingCmdsDropdown.onValueChanged.AddListener(OnExistingCmdSelected);
                }
            }
            else if (selectedType == SelectedType.Key)
            {
                List<string> options = selectedKeyData.keyCommands.Select(c => 
                    $"$ key {c.keyIndex} {c.cmdType} {c.startTime:F3} {c.endTime:F3}"
                ).ToList();
                existingCmdsDropdown.AddOptions(options);
                int currentIndex = selectedKeyData.keyCommands.IndexOf(selectedKeyCommand);
                if (currentIndex >= 0)
                {
                    existingCmdsDropdown.onValueChanged.RemoveListener(OnExistingCmdSelected);
                    existingCmdsDropdown.value = currentIndex;
                    existingCmdsDropdown.onValueChanged.AddListener(OnExistingCmdSelected);
                }
            }
        }

        cmdDropdown.ClearOptions();
        if (isAddMode)
        {
            cmdDropdown.gameObject.SetActive(true);
            // Add 模式：显示可选指令类型
            if (selectedType == SelectedType.Note)
                cmdDropdown.AddOptions(new List<string> { "shift", "move", "destroy", "drop_to" });
            else
                cmdDropdown.AddOptions(new List<string> { "shift", "move", "hide", "show" });

            UpdateCommandFieldsVisibility();
        }
        else
        {
            // Delete 模式：不再显示 cmdDropdown，因为 existingCmdsDropdown 已包含选择功能
            cmdDropdown.gameObject.SetActive(false);
            HideCommandDetailFields();
        }
    }

    private void OnAddCmdTypeChanged(int index)
    {
        if (isAddMode) UpdateCommandFieldsVisibility();
    }

    private void UpdateCommandFieldsVisibility()
    {
        if (cmdDropdown == null) return;
        string cmdName = cmdDropdown.options[cmdDropdown.value].text;

        HideCommandDetailFields();
        
        // 初始化默认值
        if (selectedType == SelectedType.Note && selectedCommand != null)
        {
            capturedStartPos = new Vector2(selectedCommand.x1, selectedCommand.y1);
            capturedEndPos = new Vector2(selectedCommand.x2, selectedCommand.y2);
            startTimeInputField.text = selectedCommand.timeA.ToString(CultureInfo.InvariantCulture);
            endTimeInputField.text = selectedCommand.timeB.ToString(CultureInfo.InvariantCulture);
            
            // 预填充额外参数
            if (cmdName == "drop_to") {
                string text = selectedCommand.key_name.ToString();
                if (selectedCommand.type == NoteType.Hold)
                    text += $" {selectedCommand.hold_duration.ToString(CultureInfo.InvariantCulture)}";
                extraParamInputField.text = text;
            } else if (cmdName == "move") {
                extraParamInputField.text = selectedCommand.json_filename ?? "";
            } else {
                extraParamInputField.text = "";
            }
        }
        else if (selectedType == SelectedType.Key && selectedKeyData != null)
        {
            capturedStartPos = new Vector2(selectedKeyData.x, selectedKeyData.y);
            capturedEndPos = new Vector2(selectedKeyData.x, selectedKeyData.y);
            if (selectedKeyCommand != null)
            {
                capturedStartPos = new Vector2(selectedKeyCommand.x1, selectedKeyCommand.y1);
                capturedEndPos = new Vector2(selectedKeyCommand.x2, selectedKeyCommand.y2);
                startTimeInputField.text = selectedKeyCommand.startTime.ToString(CultureInfo.InvariantCulture);
                endTimeInputField.text = selectedKeyCommand.endTime.ToString(CultureInfo.InvariantCulture);
                extraParamInputField.text = selectedKeyCommand.json_filename ?? "";
            }
            else
            {
                startTimeInputField.text = "0";
                endTimeInputField.text = "1";
                extraParamInputField.text = "";
            }
        }
        UpdatePosBtnTexts();

        if (selectedType == SelectedType.Note)
        {
            switch (cmdName)
            {
                case "drop_to":
                    startTimeInputField.gameObject.SetActive(true);
                    endTimeInputField.gameObject.SetActive(true);
                    startPosBtn.gameObject.SetActive(true);
                    endPosBtn.gameObject.SetActive(true);
                    extraParamInputField.gameObject.SetActive(true);
                    extraParamInputField.placeholder.GetComponent<TextMeshProUGUI>().text = 
                        selectedCommand.type == NoteType.Hold ? "key_name hold_dur" : "key_name";
                    break;
                case "shift":
                    startTimeInputField.gameObject.SetActive(true);
                    endTimeInputField.gameObject.SetActive(true);
                    startPosBtn.gameObject.SetActive(true);
                    endPosBtn.gameObject.SetActive(true);
                    break;
                case "move":
                    startTimeInputField.gameObject.SetActive(true);
                    endTimeInputField.gameObject.SetActive(true);
                    extraParamInputField.gameObject.SetActive(true);
                    extraParamInputField.placeholder.GetComponent<TextMeshProUGUI>().text = "json_file";
                    break;
                case "destroy":
                    startTimeInputField.gameObject.SetActive(true);
                    break;
            }
        }
        else if (selectedType == SelectedType.Key)
        {
            switch (cmdName)
            {
                case "shift":
                    startTimeInputField.gameObject.SetActive(true);
                    endTimeInputField.gameObject.SetActive(true);
                    startPosBtn.gameObject.SetActive(true);
                    endPosBtn.gameObject.SetActive(true);
                    break;
                case "move":
                    startTimeInputField.gameObject.SetActive(true);
                    endTimeInputField.gameObject.SetActive(true);
                    extraParamInputField.gameObject.SetActive(true);
                    extraParamInputField.placeholder.GetComponent<TextMeshProUGUI>().text = "json_file";
                    break;
                case "hide":
                case "show":
                    startTimeInputField.gameObject.SetActive(true);
                    break;
            }
        }
    }

    private void OnConfirmCmdAction()
    {
        if (selectedType == SelectedType.None || 
           (selectedType == SelectedType.Note && selectedCommand == null) || 
           (selectedType == SelectedType.Key && selectedKeyData == null)) return;

        if (isAddMode)
        {
            // 执行 Add 逻辑
            string cmdName = cmdDropdown.options[cmdDropdown.value].text;
            
            if (selectedType == SelectedType.Note)
            {
                Command newCmd = new Command
                {
                    num = selectedCommand.num,
                    type = selectedCommand.type,
                    commandName = cmdName,
                    is_show = true,
                    timeA = selectedCommand.timeA,
                    timeB = selectedCommand.timeB,
                    x1 = capturedStartPos.x,
                    y1 = capturedStartPos.y,
                    x2 = capturedEndPos.x,
                    y2 = capturedEndPos.y,
                    key_name = selectedCommand.key_name,
                    isNoteFirstTimeOccured = false
                };

                // 从输入框读取值
                float.TryParse(startTimeInputField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out newCmd.timeA);
                float.TryParse(endTimeInputField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out newCmd.timeB);
                
                string extra = extraParamInputField.text.Trim();
                string[] extraParts = extra.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (cmdName == "drop_to")
                {
                    if (extraParts.Length >= 1) int.TryParse(extraParts[0], out newCmd.key_name);
                    if (extraParts.Length >= 2 && newCmd.type == NoteType.Hold)
                        float.TryParse(extraParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out newCmd.hold_duration);
                }
                else if (cmdName == "move")
                {
                    newCmd.json_filename = extra;
                }

                ChartData.Instance.AddNoteData(newCmd);
                ChartData.Instance.SortCommandsByTime();
                selectedCommand = newCmd; // 选中新添加的指令
                infoText.text = $"已为音符 {selectedCommand.num} 添加指令 {cmdName}";
            }
            else
            {
                // Key 指令添加逻辑
                KeyCommand newKeyCmd = new KeyCommand
                {
                    keyIndex = selectedKeyData.keyName,
                    cmdType = cmdName,
                    startTime = 0,
                    endTime = 1,
                    x1 = capturedStartPos.x,
                    y1 = capturedStartPos.y,
                    x2 = capturedEndPos.x,
                    y2 = capturedEndPos.y,
                    json_filename = extraParamInputField.text.Trim()
                };

                float.TryParse(startTimeInputField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out newKeyCmd.startTime);
                float.TryParse(endTimeInputField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out newKeyCmd.endTime);
                
                if (cmdName == "hide" || cmdName == "show")
                {
                    newKeyCmd.endTime = newKeyCmd.startTime;
                }

                selectedKeyData.keyCommands.Add(newKeyCmd);
                selectedKeyCommand = newKeyCmd;
                infoText.text = $"已为按键 {selectedKeyData.keyName} 添加指令 {cmdName}";
            }
        }
        else
        {
            // 执行 Delete 逻辑：针对当前选中的指令进行删除
            if (selectedType == SelectedType.Note)
            {
                var noteCmds = ChartData.Instance.commands.Where(c => c.num == selectedCommand.num).ToList();
                if (noteCmds.Count == 1)
                {
                    infoText.text = "不能删除音符的唯一指令，请使用 Delete 键删除整个音符";
                    return;
                }
                
                Command toDelete = selectedCommand;
                ChartData.Instance.commands.Remove(toDelete);
                // 自动选择该音符的另一个指令
                selectedCommand = ChartData.Instance.commands.FirstOrDefault(c => c.num == toDelete.num);
                infoText.text = $"已删除音符 {toDelete.num} 的指令 {toDelete.commandName}";
            }
            else
            {
                if (selectedKeyCommand == null) return;

                KeyCommand toDelete = selectedKeyCommand;
                selectedKeyData.keyCommands.Remove(toDelete);
                // 自动选择该按键的另一个指令
                selectedKeyCommand = selectedKeyData.keyCommands.FirstOrDefault();
                infoText.text = $"已删除按键 {selectedKeyData.keyName} 的指令 {toDelete.cmdType}";
            }
        }

        UpdateCmdManagementUI();
        if (selectedType == SelectedType.Note) UpdateInfoPanelForNote();
        else UpdateInfoPanelForKey();
    }

    private void OnExistingCmdSelected(int index)
    {
        if (selectedType == SelectedType.Note && selectedCommand != null)
        {
            var noteCmds = ChartData.Instance.commands.Where(c => c.num == selectedCommand.num).ToList();
            if (index >= 0 && index < noteCmds.Count)
            {
                selectedCommand = noteCmds[index];
                UpdateInfoPanelForNote();
            }
        }
        else if (selectedType == SelectedType.Key && selectedKeyData != null)
        {
            if (index >= 0 && index < selectedKeyData.keyCommands.Count)
            {
                selectedKeyCommand = selectedKeyData.keyCommands[index];
                UpdateInfoPanelForKey();
            }
        }
    }
}
