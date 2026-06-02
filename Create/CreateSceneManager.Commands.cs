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
        
        if (startPosInputField != null) startPosInputField.text = $"{capturedStartPos.x:F2}, {capturedStartPos.y:F2}";
        if (endPosInputField != null) endPosInputField.text = $"{capturedEndPos.x:F2}, {capturedEndPos.y:F2}";
    }

    private void HideCommandDetailFields()
    {
        if (startPosBtn != null) startPosBtn.gameObject.SetActive(false);
        if (endPosBtn != null) endPosBtn.gameObject.SetActive(false);
        if (startPosInputField != null) startPosInputField.gameObject.SetActive(false);
        if (endPosInputField != null) endPosInputField.gameObject.SetActive(false);
        if (startTimeInputField != null) startTimeInputField.gameObject.SetActive(false);
        if (endTimeInputField != null) endTimeInputField.gameObject.SetActive(false);
        if (extraParamInputField != null) extraParamInputField.gameObject.SetActive(false);
        if (moveTypeDropdown != null) moveTypeDropdown.gameObject.SetActive(false);
    }

    private void OnStartPosInputEndEdit(string value)
    {
        if (TryParseVector2(value, out Vector2 pos))
        {
            capturedStartPos = pos;
            UpdatePosBtnTexts();
        }
    }

    private void OnEndPosInputEndEdit(string value)
    {
        if (TryParseVector2(value, out Vector2 pos))
        {
            capturedEndPos = pos;
            UpdatePosBtnTexts();
        }
    }

    private bool TryParseVector2(string input, out Vector2 result)
    {
        result = Vector2.zero;
        if (string.IsNullOrEmpty(input)) return false;

        // 尝试解析格式如 "x, y" 或 "x y"
        string cleanInput = input.Replace("(", "").Replace(")", "");
        string[] parts = cleanInput.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length >= 2)
        {
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                result = new Vector2(x, y);
                return true;
            }
        }
        return false;
    }

    private void ToggleCmdMode()
    {
        isAddMode = !isAddMode;
        currentAddStep = isAddMode ? AddStep.SelectType : AddStep.None;
        UpdateCmdManagementUI();
    }

    private void UpdateCmdManagementUI()
    {
        // 检查是否有有效的选中对象
        bool hasValidSelection = false;
        if (selectedType == SelectedType.Note && selectedCommand != null)
        {
            hasValidSelection = true;
        }
        else if (selectedType == SelectedType.Key && selectedKeyData != null)
        {
            hasValidSelection = true;
        }

        if (!hasValidSelection)
        {
            // 没有有效选中对象，隐藏所有指令管理 UI
            if (cmdModeToggleBtn != null) cmdModeToggleBtn.gameObject.SetActive(false);
            if (cmdDropdown != null) cmdDropdown.gameObject.SetActive(false);
            if (existingCmdsDropdown != null) existingCmdsDropdown.gameObject.SetActive(false);
            if (cmdConfirmBtn != null) cmdConfirmBtn.gameObject.SetActive(false);
            HideCommandDetailFields();
            if (moveTypeDropdown != null) moveTypeDropdown.gameObject.SetActive(false);
            return;
        }

        // 按钮显示文本
        TextMeshProUGUI btnText = cmdModeToggleBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null) btnText.text = isAddMode ? "切换到删除" : "切换到添加";

        cmdModeToggleBtn.gameObject.SetActive(true);

        // 如果是添加模式，且还没有开始分步流程，则重置为第一步
        if (isAddMode && currentAddStep == AddStep.None)
        {
            currentAddStep = AddStep.SelectType;
        }
        else if (!isAddMode)
        {
            currentAddStep = AddStep.None;
        }

        // 处理分步显示
        if (isAddMode)
        {
            UpdateAddFlowUI();
        }
        else
        {
            UpdateDeleteFlowUI();
        }
    }

    private void UpdateAddFlowUI()
    {
        HideCommandDetailFields();
        if (moveTypeDropdown != null) moveTypeDropdown.gameObject.SetActive(false);
        
        // 确保 cmdDropdown 存在
        if (cmdDropdown == null)
        {
            Debug.LogWarning("cmdDropdown 未赋值！请在 Inspector 中设置。");
            return;
        }
        
        cmdDropdown.gameObject.SetActive(false); // 默认隐藏，根据步骤决定是否显示
        if (existingCmdsDropdown != null)
        {
            existingCmdsDropdown.gameObject.SetActive(true); // 始终显示现有指令供查看
            PopulateExistingCmds(); // 填充现有指令列表
        }
        cmdConfirmBtn.gameObject.SetActive(true);

        TextMeshProUGUI confirmBtnText = cmdConfirmBtn.GetComponentInChildren<TextMeshProUGUI>();

        switch (currentAddStep)
        {
            case AddStep.SelectType:
                cmdDropdown.gameObject.SetActive(true); // 显示指令类型选择
                
                // 清除旧选项并添加新选项
                cmdDropdown.options.Clear();
                
                List<string> options = new List<string>();
                if (selectedType == SelectedType.Note)
                    options = new List<string> { "shift", "move", "destroy", "drop_to", "spin" };
                else
                    options = new List<string> { "shift", "move", "hide", "show" };
                
                // 添加选项
                foreach (string option in options)
                {
                    cmdDropdown.options.Add(new TMP_Dropdown.OptionData(option));
                }
                
                // 重置选中值为第一个选项
                cmdDropdown.value = 0;
                
                // 刷新下拉框显示
                cmdDropdown.RefreshShownValue();
                
                if (confirmBtnText != null) confirmBtnText.text = "确认类型";
                infoText.text = "步骤 1: 选择指令类型";
                break;

            case AddStep.InputTime:
                startTimeInputField.gameObject.SetActive(true);
                // 只有 destroy, shift, move, drop_to, hide, show 有 endTime，部分只有一个时间
                string currentCmd = cmdDropdown.options[cmdDropdown.value].text;
                if (currentCmd == "destroy" || currentCmd == "hide" || currentCmd == "show")
                {
                    // 这些指令只需要一个时间点
                    if (endTimeInputField != null) endTimeInputField.gameObject.SetActive(false);
                }
                else
                {
                    // shift, move, drop_to, spin 需要起始和结束时间
                    if (endTimeInputField != null) endTimeInputField.gameObject.SetActive(true);
                }
                
                if (confirmBtnText != null) confirmBtnText.text = "确认时间";
                infoText.text = $"步骤 2: 输入时间 (当前类型: {currentCmd})";
                break;

            case AddStep.InputPos:
                startPosBtn.gameObject.SetActive(true);
                endPosBtn.gameObject.SetActive(true);
                if (startPosInputField != null) startPosInputField.gameObject.SetActive(true);
                if (endPosInputField != null) endPosInputField.gameObject.SetActive(true);
                UpdatePosBtnTexts();
                
                string cmdName = cmdDropdown.options[cmdDropdown.value].text;
                if (cmdName == "drop_to")
                {
                    extraParamInputField.gameObject.SetActive(true);
                    extraParamInputField.placeholder.GetComponent<TextMeshProUGUI>().text = 
                        selectedCommand.type == NoteType.Hold ? "key_name hold_dur" : "key_name";
                }

                if (confirmBtnText != null) confirmBtnText.text = "完成添加";
                infoText.text = "步骤 3: 输入坐标";
                break;

            case AddStep.InputMoveParams:
                if (moveTypeDropdown != null)
                {
                    moveTypeDropdown.gameObject.SetActive(true);
                    moveTypeDropdown.value = 0; // 重置为默认选项
                }
                extraParamInputField.gameObject.SetActive(true);
                extraParamInputField.placeholder.GetComponent<TextMeshProUGUI>().text = "或输入 .json 文件名";

                if (confirmBtnText != null) confirmBtnText.text = "确认添加 (或选择运动类型)";
                infoText.text = "步骤 3: 选择运动方式";
                break;

            case AddStep.InputSpinParams:
                extraParamInputField.gameObject.SetActive(true);
                extraParamInputField.placeholder.GetComponent<TextMeshProUGUI>().text = "init_direction degree_per_second";
                
                if (confirmBtnText != null) confirmBtnText.text = "完成添加";
                infoText.text = "步骤 3: 输入旋转参数 (初始方向角度，每秒旋转度数)";
                break;
        }
    }

    private void UpdateDeleteFlowUI()
    {
        HideCommandDetailFields();
        if (moveTypeDropdown != null) moveTypeDropdown.gameObject.SetActive(false);
        cmdDropdown.gameObject.SetActive(false);
        if (existingCmdsDropdown != null) existingCmdsDropdown.gameObject.SetActive(true);
        cmdConfirmBtn.gameObject.SetActive(true);

        TextMeshProUGUI confirmBtnText = cmdConfirmBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (confirmBtnText != null) confirmBtnText.text = "删除选中指令";

        // 填充现有指令下拉列表 (代码复用自原 UpdateCmdManagementUI)
        PopulateExistingCmds();
        
        infoText.text = "当前处于删除模式";
    }

    private void PopulateExistingCmds()
    {
        if (existingCmdsDropdown == null) return;
        
        // 清除旧选项
        existingCmdsDropdown.options.Clear();
        
        if (selectedType == SelectedType.Note)
        {
            var noteCmds = ChartData.Instance.commands.Where(c => c.num == selectedCommand.num).ToList();
            
            if (noteCmds.Count == 0)
            {
                // 没有任何指令时显示 (empty)
                existingCmdsDropdown.options.Add(new TMP_Dropdown.OptionData("(empty)"));
                existingCmdsDropdown.value = 0;
            }
            else
            {
                List<string> options = noteCmds.Select(c => {
                    string prefix = c.isNoteFirstTimeOccured ? "# " : "% ";
                    return $"{prefix}{c.type.ToString().ToLower()} {c.num} {c.commandName} {c.timeA:F3} {c.timeB:F3}";
                }).ToList();
                
                // 添加选项
                foreach (string option in options)
                {
                    existingCmdsDropdown.options.Add(new TMP_Dropdown.OptionData(option));
                }
                
                int currentIndex = noteCmds.IndexOf(selectedCommand);
                if (currentIndex >= 0)
                {
                    existingCmdsDropdown.onValueChanged.RemoveListener(OnExistingCmdSelected);
                    existingCmdsDropdown.value = currentIndex;
                    existingCmdsDropdown.onValueChanged.AddListener(OnExistingCmdSelected);
                }
            }
        }
        else if (selectedType == SelectedType.Key)
        {
            if (selectedKeyData.keyCommands.Count == 0)
            {
                // 没有任何指令时显示 (empty)
                existingCmdsDropdown.options.Add(new TMP_Dropdown.OptionData("(empty)"));
                existingCmdsDropdown.value = 0;
            }
            else
            {
                List<string> options = selectedKeyData.keyCommands.Select(c => 
                    $"$ key {c.keyIndex} {c.cmdType} {c.startTime:F3} {c.endTime:F3}"
                ).ToList();
                
                // 添加选项
                foreach (string option in options)
                {
                    existingCmdsDropdown.options.Add(new TMP_Dropdown.OptionData(option));
                }
                
                int currentIndex = selectedKeyData.keyCommands.IndexOf(selectedKeyCommand);
                if (currentIndex >= 0)
                {
                    existingCmdsDropdown.onValueChanged.RemoveListener(OnExistingCmdSelected);
                    existingCmdsDropdown.value = currentIndex;
                    existingCmdsDropdown.onValueChanged.AddListener(OnExistingCmdSelected);
                }
            }
        }
        
        // 刷新下拉框显示
        existingCmdsDropdown.RefreshShownValue();
    }

    private void OnConfirmCmdAction()
    {
        if (selectedType == SelectedType.None || 
           (selectedType == SelectedType.Note && selectedCommand == null) || 
           (selectedType == SelectedType.Key && selectedKeyData == null)) return;

        if (isAddMode)
        {
            GoToNextAddStep();
        }
        else
        {
            ExecuteDelete();
        }
    }

    private void GoToNextAddStep()
    {
        string cmdName = "";
        if (currentAddStep != AddStep.Start)
        {
            cmdName = cmdDropdown.options[cmdDropdown.value].text;
        }

        switch (currentAddStep)
        {
            case AddStep.Start:
                currentAddStep = AddStep.SelectType;
                break;
            case AddStep.SelectType:
                currentAddStep = AddStep.InputTime;
                // 预填默认时间
                if (selectedType == SelectedType.Note)
                {
                    startTimeInputField.text = selectedCommand.timeA.ToString(CultureInfo.InvariantCulture);
                    endTimeInputField.text = selectedCommand.timeB.ToString(CultureInfo.InvariantCulture);
                }
                else if (selectedKeyCommand != null)
                {
                    startTimeInputField.text = selectedKeyCommand.startTime.ToString(CultureInfo.InvariantCulture);
                    endTimeInputField.text = selectedKeyCommand.endTime.ToString(CultureInfo.InvariantCulture);
                }
                break;

            case AddStep.InputTime:
                if (cmdName == "move")
                {
                    currentAddStep = AddStep.InputMoveParams;
                }
                else if (cmdName == "destroy" || cmdName == "hide" || cmdName == "show")
                {
                    ExecuteAdd();
                    currentAddStep = AddStep.SelectType; // 完成后重置
                }
                else if (cmdName == "spin")
                {
                    currentAddStep = AddStep.InputSpinParams;
                }
                else
                {
                    currentAddStep = AddStep.InputPos;
                    // 预填默认坐标
                    if (selectedType == SelectedType.Note)
                    {
                        capturedStartPos = new Vector2(selectedCommand.x1, selectedCommand.y1);
                        capturedEndPos = new Vector2(selectedCommand.x2, selectedCommand.y2);
                    }
                    else if (selectedKeyData != null)
                    {
                        capturedStartPos = new Vector2(selectedKeyData.x, selectedKeyData.y);
                        capturedEndPos = new Vector2(selectedKeyData.x, selectedKeyData.y);
                    }
                }
                break;

            case AddStep.InputPos:
            case AddStep.InputMoveParams:
            case AddStep.InputSpinParams:
                ExecuteAdd();
                
                // 清空输入框，为下一次添加做准备
                if (startTimeInputField != null) startTimeInputField.text = "";
                if (endTimeInputField != null) endTimeInputField.text = "";
                if (extraParamInputField != null) extraParamInputField.text = "";
                capturedStartPos = Vector2.zero;
                capturedEndPos = Vector2.zero;
                UpdatePosBtnTexts();
                
                currentAddStep = AddStep.SelectType; // 完成后重置
                break;
        }

        UpdateCmdManagementUI();
    }

    private void ExecuteAdd()
    {
        string cmdName = cmdDropdown.options[cmdDropdown.value].text;
        
        if (selectedType == SelectedType.Note)
        {
            Command newCmd = new Command
            {
                num = selectedCommand.num,
                type = selectedCommand.type,
                commandName = cmdName,
                is_show = true,
                x1 = capturedStartPos.x,
                y1 = capturedStartPos.y,
                x2 = capturedEndPos.x,
                y2 = capturedEndPos.y,
                key_name = selectedCommand.key_name,
                isNoteFirstTimeOccured = false
            };

            float.TryParse(startTimeInputField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out newCmd.timeA);
            float.TryParse(endTimeInputField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out newCmd.timeB);
            
            if (cmdName == "destroy") newCmd.timeB = newCmd.timeA;

            string extra = extraParamInputField.text.Trim();
            if (cmdName == "drop_to")
            {
                string[] extraParts = extra.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (extraParts.Length >= 1) int.TryParse(extraParts[0], out newCmd.key_name);
                if (extraParts.Length >= 2 && newCmd.type == NoteType.Hold)
                    float.TryParse(extraParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out newCmd.hold_duration);
            }
            else if (cmdName == "move")
            {
                newCmd.json_filename = extra;
            }
            else if (cmdName == "spin")
            {
                // x1 = init_direction, y1 = degree per second
                string[] extraParts = extra.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (extraParts.Length >= 2)
                {
                    float.TryParse(extraParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out newCmd.x1);
                    float.TryParse(extraParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out newCmd.y1);
                }
            }

            ChartData.Instance.AddNoteData(newCmd);
            ChartData.Instance.SortCommandsByTime();
            selectedCommand = newCmd;
            infoText.text = $"已添加指令: {cmdName}";
        }
        else
        {
            KeyCommand newKeyCmd = new KeyCommand
            {
                keyIndex = selectedKeyData.keyName,
                cmdType = cmdName,
                x1 = capturedStartPos.x,
                y1 = capturedStartPos.y,
                x2 = capturedEndPos.x,
                y2 = capturedEndPos.y,
                json_filename = extraParamInputField.text.Trim()
            };

            float.TryParse(startTimeInputField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out newKeyCmd.startTime);
            float.TryParse(endTimeInputField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out newKeyCmd.endTime);
            
            if (cmdName == "hide" || cmdName == "show") newKeyCmd.endTime = newKeyCmd.startTime;

            selectedKeyData.keyCommands.Add(newKeyCmd);
            selectedKeyCommand = newKeyCmd;
            infoText.text = $"已添加按键指令: {cmdName}";
        }
    }

    private void ExecuteDelete()
    {
        if (selectedType == SelectedType.Note)
        {
            var noteCmds = ChartData.Instance.commands.Where(c => c.num == selectedCommand.num).ToList();
            if (noteCmds.Count <= 1)
            {
                infoText.text = "无法删除唯一指令，请使用 Delete 键删除整个音符对象";
                return;
            }
            
            // 记录要删除的指令信息
            string cmdInfo = $"{selectedCommand.commandName} @ {selectedCommand.timeA:F2}s";
            
            ChartData.Instance.commands.Remove(selectedCommand);
            
            // 选中该音符的第一个指令
            selectedCommand = ChartData.Instance.commands.FirstOrDefault(c => c.num == selectedCommand.num);
            
            infoText.text = $"已删除指令: {cmdInfo}";
        }
        else if (selectedType == SelectedType.Key)
        {
            if (selectedKeyCommand == null)
            {
                infoText.text = "没有选中的按键指令";
                return;
            }
            
            // 记录要删除的指令信息
            string cmdInfo = $"{selectedKeyCommand.cmdType} @ {selectedKeyCommand.startTime:F2}s";
            
            selectedKeyData.keyCommands.Remove(selectedKeyCommand);
            
            // 选中该按键的第一个指令
            selectedKeyCommand = selectedKeyData.keyCommands.FirstOrDefault();
            
            infoText.text = $"已删除按键指令: {cmdInfo}";
        }
        
        UpdateCmdManagementUI();
    }

    private void OnMoveOptionSelected(string option)
    {
        if (currentAddStep != AddStep.InputMoveParams) return;
        
        string fileName = $"{option}_{DateTime.Now:yyyyMMddHHmmss}.json";
        float.TryParse(startTimeInputField.text, out float timeA);
        float.TryParse(endTimeInputField.text, out float timeB);

        // 生成并保存 JSON 文件
        GenerateAndSaveMovementJson(option, timeA, timeB, fileName);

        // 设置文件名到输入框
        extraParamInputField.text = fileName;
        
        // 自动完成添加
        ExecuteAdd();
        currentAddStep = AddStep.SelectType;
        UpdateCmdManagementUI();
    }

    private void OnExistingCmdSelected(int index)
    {
        // 如果选择的是 "(empty)"，不执行任何操作
        if (existingCmdsDropdown != null && existingCmdsDropdown.options.Count == 1 && 
            existingCmdsDropdown.options[0].text == "(empty)")
        {
            return;
        }

        if (selectedType == SelectedType.Note && selectedCommand != null)
        {
            var noteCmds = ChartData.Instance.commands.Where(c => c.num == selectedCommand.num).ToList();
            if (index >= 0 && index < noteCmds.Count)
            {
                selectedCommand = noteCmds[index];
                UpdateInfoPanelForNote();
                infoText.text = $"已切换到指令: {selectedCommand.commandName} @ {selectedCommand.timeA:F2}s";
            }
        }
        else if (selectedType == SelectedType.Key && selectedKeyData != null)
        {
            if (index >= 0 && index < selectedKeyData.keyCommands.Count)
            {
                selectedKeyCommand = selectedKeyData.keyCommands[index];
                UpdateInfoPanelForKey();
                infoText.text = $"已切换到按键指令: {selectedKeyCommand.cmdType} @ {selectedKeyCommand.startTime:F2}s";
            }
        }
    }
}
