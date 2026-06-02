using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using System.Globalization;
using TMPro;
using System;
using System.IO;

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
        if (moveTypeDropdown != null) moveTypeDropdown.gameObject.SetActive(false);

        // 隐藏指令管理相关
        if (cmdModeToggleBtn != null) cmdModeToggleBtn.gameObject.SetActive(false);
        if (cmdDropdown != null) cmdDropdown.gameObject.SetActive(false);
        if (existingCmdsDropdown != null) existingCmdsDropdown.gameObject.SetActive(false);
        if (cmdConfirmBtn != null) cmdConfirmBtn.gameObject.SetActive(false);
        HideCommandDetailFields();

        // 显示下拉列表，用于放置模式选择
        noteTypeDropdown.gameObject.SetActive(true);
        noteTypeDropdown.value = 0; // 重置为 (empty)
        
        // 显示小节控制组件（小节输入和滑块）
        if (measureInputField != null) measureInputField.gameObject.SetActive(true);
        if (measureSlider != null) measureSlider.gameObject.SetActive(true);
        if (beatInputField != null) beatInputField.gameObject.SetActive(true);
        if (beatSlider != null) beatSlider.gameObject.SetActive(true);
        
        // 隐藏时间确认按钮并重置状态
        if (timeConfirmBtn != null) timeConfirmBtn.gameObject.SetActive(false);
        isTimeChanged = false;
        pendingTime = 0f;

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
        if (measureInputField != null) measureInputField.gameObject.SetActive(false);
        if (measureSlider != null) measureSlider.gameObject.SetActive(false);
        if (beatInputField != null) beatInputField.gameObject.SetActive(false);
        if (beatSlider != null) beatSlider.gameObject.SetActive(false);
        keyIndexInputField.gameObject.SetActive(false);
        keyNameInputField.gameObject.SetActive(false);
        noteTypeInputField.gameObject.SetActive(false);
        noteTypeDropdown.gameObject.SetActive(false);

        // 显示核心按钮
        if (changeNoteTypeBtn != null)
        {
            changeNoteTypeBtn.gameObject.SetActive(true);
            // 更新按钮文本为当前音符类型
            TextMeshProUGUI btnText = changeNoteTypeBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = selectedCommand.type.ToString();
            }
        }
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

        // 更新按钮文本为新类型
        if (changeNoteTypeBtn != null)
        {
            TextMeshProUGUI btnText = changeNoteTypeBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = selectedCommand.type.ToString();
            }
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
                            $"时间: {selectedKeyCommand.startTime:F3} -> {selectedKeyCommand.endTime:F3}\n" +
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
        if (measureInputField != null) measureInputField.gameObject.SetActive(false);
        if (measureSlider != null) measureSlider.gameObject.SetActive(false);
        if (beatInputField != null) beatInputField.gameObject.SetActive(false);
        if (beatSlider != null) beatSlider.gameObject.SetActive(false);     // 按键不需要小节编辑

        // 更新指令管理 UI
        UpdateCmdManagementUI();
    }

    // ---------- 编辑事件回调 ----------
    
    /// <summary>
    /// 小节序数变化回调
    /// </summary>
    private void OnMeasureChanged(string value)
    {
        if (int.TryParse(value, out int measureIndex))
        {
            // 限制小节范围
            int maxMeasure = ChartData.Instance.measureCount - 1;
            if (measureIndex < 0) measureIndex = 0;
            if (measureIndex > maxMeasure) measureIndex = maxMeasure;
            
            // 更新输入框显示（防止超出范围）
            if (measureInputField != null && measureIndex.ToString() != value)
            {
                measureInputField.text = measureIndex.ToString();
            }
            
            // 同步更新滑块
            if (measureSlider != null)
            {
                measureSlider.onValueChanged.RemoveListener(OnMeasureSliderChanged);
                measureSlider.value = measureIndex;
                measureSlider.onValueChanged.AddListener(OnMeasureSliderChanged);
            }
            
            // 更新节拍滑块的范围
            UpdateBeatSliderRange(measureIndex);
            
            // 重置节拍位置为 0
            if (beatInputField != null)
            {
                beatInputField.text = "0";
            }
            if (beatSlider != null)
            {
                beatSlider.onValueChanged.RemoveListener(OnBeatSliderChanged);
                beatSlider.value = 0;
                beatSlider.onValueChanged.AddListener(OnBeatSliderChanged);
            }
            
            // 计算待确认的时间
            pendingTime = CalculateCurrentTime();
            isTimeChanged = true;
            
            // 显示确认按钮
            if (timeConfirmBtn != null)
            {
                timeConfirmBtn.gameObject.SetActive(true);
            }
            
            // 显示当前预览信息
            MeasureData measure = GetMeasureData(measureIndex);
            infoText.text = $"小节 {measureIndex} | 节拍: 0/{measure.beatsPerMeasure} | 时间: {pendingTime:F2}s\n请点击确认按钮更新预览";
        }
        else
        {
            infoText.text = "请输入有效的小节序数";
        }
    }
    
    /// <summary>
    /// 小节滑块变化回调
    /// </summary>
    private void OnMeasureSliderChanged(float value)
    {
        int measureIndex = Mathf.RoundToInt(value);
        
        // 限制范围
        int maxMeasure = ChartData.Instance.measureCount - 1;
        if (measureIndex < 0) measureIndex = 0;
        if (measureIndex > maxMeasure) measureIndex = maxMeasure;
        
        // 同步更新输入框
        if (measureInputField != null)
        {
            measureInputField.text = measureIndex.ToString();
        }
        
        // 更新节拍滑块的范围
        UpdateBeatSliderRange(measureIndex);
        
        // 重置节拍位置为 0
        if (beatInputField != null)
        {
            beatInputField.text = "0";
        }
        if (beatSlider != null)
        {
            beatSlider.onValueChanged.RemoveListener(OnBeatSliderChanged);
            beatSlider.value = 0;
            beatSlider.onValueChanged.AddListener(OnBeatSliderChanged);
        }
        
        // 计算待确认的时间
        pendingTime = CalculateCurrentTime();
        isTimeChanged = true;
        
        // 显示确认按钮
        if (timeConfirmBtn != null)
        {
            timeConfirmBtn.gameObject.SetActive(true);
        }
        
        // 显示当前预览信息
        MeasureData measure = GetMeasureData(measureIndex);
        infoText.text = $"小节 {measureIndex} | 节拍: 0/{measure.beatsPerMeasure} | 时间: {pendingTime:F2}s\n请点击确认按钮更新预览";
    }
    
    /// <summary>
    /// 节拍输入框变化回调
    /// </summary>
    private void OnBeatInputChanged(string value)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float beatPosition))
        {
            // 获取当前小节的拍数
            int measureIndex = 0;
            if (measureInputField != null)
            {
                int.TryParse(measureInputField.text, out measureIndex);
            }
            
            MeasureData measure = GetMeasureData(measureIndex);
            
            // 限制范围
            if (beatPosition < 0) beatPosition = 0;
            if (beatPosition > measure.beatsPerMeasure) beatPosition = measure.beatsPerMeasure;
            
            // 自动吸附到最近的 1/4 拍或 1/3 拍
            beatPosition = SnapBeatPosition(beatPosition);
            
            // 更新输入框显示（防止超出范围或吸附后改变）
            if (beatInputField != null)
            {
                string formattedValue = beatPosition.ToString("F2", CultureInfo.InvariantCulture);
                if (value != formattedValue)
                {
                    beatInputField.text = formattedValue;
                }
            }
            
            // 同步更新滑块
            if (beatSlider != null)
            {
                beatSlider.onValueChanged.RemoveListener(OnBeatSliderChanged);
                beatSlider.value = beatPosition;
                beatSlider.onValueChanged.AddListener(OnBeatSliderChanged);
            }
            
            // 计算待确认的时间
            pendingTime = CalculateTimeFromMeasureAndBeat(measureIndex, beatPosition);
            isTimeChanged = true;
            
            // 显示确认按钮
            if (timeConfirmBtn != null)
            {
                timeConfirmBtn.gameObject.SetActive(true);
            }
            
            // 显示当前预览信息
            infoText.text = $"小节 {measureIndex} | 节拍 {beatPosition:F2}/{measure.beatsPerMeasure} | 时间: {pendingTime:F2}s\n请点击确认按钮更新预览";
        }
        else
        {
            infoText.text = "请输入有效的节拍位置";
        }
    }
    
    /// <summary>
    /// 节拍滑块变化回调
    /// </summary>
    private void OnBeatSliderChanged(float value)
    {
        // 获取当前小节
        int measureIndex = 0;
        if (measureInputField != null)
        {
            int.TryParse(measureInputField.text, out measureIndex);
        }
        
        float beatPosition = value;
        
        // 自动吸附到最近的 1/4 拍或 1/3 拍
        beatPosition = SnapBeatPosition(beatPosition);
        
        // 更新滑块值（可能因吸附而改变）
        if (beatSlider != null)
        {
            beatSlider.onValueChanged.RemoveListener(OnBeatSliderChanged);
            beatSlider.value = beatPosition;
            beatSlider.onValueChanged.AddListener(OnBeatSliderChanged);
        }
        
        // 同步更新输入框
        if (beatInputField != null)
        {
            beatInputField.text = beatPosition.ToString("F2", CultureInfo.InvariantCulture);
        }
        
        // 计算待确认的时间
        pendingTime = CalculateTimeFromMeasureAndBeat(measureIndex, beatPosition);
        isTimeChanged = true;
        
        // 显示确认按钮
        if (timeConfirmBtn != null)
        {
            timeConfirmBtn.gameObject.SetActive(true);
        }
        
        // 显示当前预览信息
        MeasureData measure = GetMeasureData(measureIndex);
        infoText.text = $"小节 {measureIndex} | 节拍 {beatPosition:F2}/{measure.beatsPerMeasure} | 时间: {pendingTime:F2}s\n请点击确认按钮更新预览";
    }
    
    /// <summary>
    /// 计算指定小节的起始时间
    /// </summary>
    private float CalculateMeasureStartTime(int measureIndex)
    {
        float totalTime = 0f;
        
        for (int i = 0; i < measureIndex && i < ChartData.Instance.measures.Count; i++)
        {
            totalTime += ChartData.Instance.CalculateMeasureDuration(ChartData.Instance.measures[i]);
        }
        
        return totalTime;
    }
    
    /// <summary>
    /// 根据小节序数和节拍位置计算实际时间
    /// </summary>
    private float CalculateTimeFromMeasureAndBeat(int measureIndex, float beatPosition)
    {
        // 计算前面所有小节的总时间
        float totalTime = CalculateMeasureStartTime(measureIndex);
        
        // 加上当前小节内的时间
        MeasureData currentMeasure = GetMeasureData(measureIndex);
        float timeInMeasure = beatPosition * (60f / currentMeasure.bpm);
        
        return totalTime + timeInMeasure;
    }
    
    /// <summary>
    /// 计算当前时间（基于小节和节拍位置）
    /// </summary>
    private float CalculateCurrentTime()
    {
        int measureIndex = 0;
        float beatPosition = 0f;
        
        if (measureInputField != null)
        {
            int.TryParse(measureInputField.text, out measureIndex);
        }
        
        if (beatSlider != null)
        {
            beatPosition = beatSlider.value;
        }
        
        return CalculateTimeFromMeasureAndBeat(measureIndex, beatPosition);
    }
    
    /// <summary>
    /// 获取指定小节的 MeasureData
    /// </summary>
    private MeasureData GetMeasureData(int measureIndex)
    {
        if (ChartData.Instance.measures != null && measureIndex >= 0 && measureIndex < ChartData.Instance.measures.Count)
        {
            return ChartData.Instance.measures[measureIndex];
        }
        else
        {
            return new MeasureData(0, ChartData.Instance.defaultBpm, 
                                  ChartData.Instance.defaultBeatsPerMeasure, 
                                  ChartData.Instance.defaultBeatUnit);
        }
    }
    

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
            // 检查 ID 是否为非负数
            if (newKeyId < 0)
            {
                infoText.text = "Key ID 必须为非负整数！";
                return;
            }

            // 检查 ID 是否冲突（排除自身）
            if (ChartData.Instance.keyDatas.Any(k => k != selectedKeyData && k.keyName == newKeyId))
            {
                infoText.text = $"Key ID {newKeyId} 已被其他按键使用！";
                return;
            }

            // 同步更新关联该按键的音符
            int oldKeyId = selectedKeyData.keyName;
            int updatedCount = 0;
            foreach (var cmd in ChartData.Instance.commands)
            {
                if (cmd.key_name == oldKeyId)
                {
                    cmd.key_name = newKeyId;
                    updatedCount++;
                }
            }

            selectedKeyData.keyName = newKeyId;

            // 更新精灵
            SpriteRenderer sr = selectedObject.GetComponent<SpriteRenderer>();
            if (sr != null && newKeyId >= 0 && newKeyId < keySprites.Length)
                sr.sprite = keySprites[newKeyId];

            UpdateInfoPanelForKey();
            
            if (updatedCount > 0)
            {
                infoText.text = $"已更新 Key ID 为 {newKeyId}，并更新了 {updatedCount} 个关联音符";
            }
            else
            {
                infoText.text = $"已更新 Key ID 为 {newKeyId}";
            }
        }
        else
        {
            infoText.text = "请输入有效的整数 ID";
        }
    }

    /// <summary>
    /// 时间预览确认按钮点击回调
    /// </summary>
    private void OnTimeConfirmClicked()
    {
        if (!isTimeChanged)
        {
            infoText.text = "时间未更改";
            return;
        }
        
        // 更新对象位置
        UpdateObjectsPositionAtTime(pendingTime);
        
        // 重置状态
        isTimeChanged = false;
        pendingTime = 0f;
        
        // 隐藏确认按钮
        if (timeConfirmBtn != null)
        {
            timeConfirmBtn.gameObject.SetActive(false);
        }
        
        // 显示成功信息
        int measureIndex = 0;
        if (measureInputField != null)
        {
            int.TryParse(measureInputField.text, out measureIndex);
        }
        float beatPosition = 0f;
        if (beatSlider != null)
        {
            beatPosition = beatSlider.value;
        }
        MeasureData measure = GetMeasureData(measureIndex);
        infoText.text = $"已更新预览 | 小节 {measureIndex} | 节拍 {beatPosition:F2}/{measure.beatsPerMeasure}";
        
        // Debug.Log($"OnTimeConfirmClicked: 已更新时间到实际值 {currentDisplayTime:F3}s");
    }
    

    
    /// <summary>
    /// 根据指定时间更新所有NoteObject和KeyObject的显示位置
    /// </summary>
    private void UpdateObjectsPositionAtTime(float time)
    {
        // 更新所有音符对象的位置
        NoteObject[] noteObjects = FindObjectsOfType<NoteObject>();
        int updatedNoteCount = 0;
        foreach (NoteObject noteObj in noteObjects)
        {
            if (noteObj.command != null)
            {
                // 检查是否应该显示
                bool shouldShow = ShouldShowNoteAtTime(noteObj.command, time);
                
                Vector2 position = CalculateNotePositionAtTime(noteObj.command, time);
                noteObj.transform.position = new Vector3(position.x, position.y, planeZ);
                
                // 根据时间控制显示/隐藏
                SpriteRenderer sr = noteObj.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.enabled = shouldShow;
                }
                
                updatedNoteCount++;
                

            }
        }

        // 更新所有按键对象的位置
        KeyObject[] keyObjects = FindObjectsOfType<KeyObject>();
        int updatedKeyCount = 0;
        foreach (KeyObject keyObj in keyObjects)
        {
            if (keyObj.keyData != null)
            {
                Vector2 position = CalculateKeyPositionAtTime(keyObj.keyData, time);
                
                // 记录更新前的位置
                Vector3 oldPos = keyObj.transform.position;
                Vector3 oldLocalPos = keyObj.transform.localPosition;
                Transform parent = keyObj.transform.parent;
                
                // 设置新位置
                keyObj.transform.position = new Vector3(position.x, position.y, planeZ);
                
                // 记录更新后的位置
                Vector3 newPos = keyObj.transform.position;
                Vector3 newLocalPos = keyObj.transform.localPosition;
                
                // 检查 SpriteRenderer 状态
                SpriteRenderer sr = keyObj.GetComponent<SpriteRenderer>();
                bool srEnabled = (sr != null) ? sr.enabled : false;
                Vector3 scale = keyObj.transform.localScale;
                
                updatedKeyCount++;
                

            }
        }
        
        // 记录当前显示的时间
        currentDisplayTime = time;
        
        // Debug.Log($"UpdateObjectsPositionAtTime({time:F2}s): 更新了 {updatedNoteCount} 个音符, {updatedKeyCount} 个按键");
    }

    /// <summary>
    /// 判断音符是否可以被选中（未销毁且已到出现时间）
    /// </summary>
    private bool CanSelectNote(Command command)
    {
        if (command == null) return false;
        
        // 1. 检查是否已被销毁
        if (IsNoteDestroyed(command))
        {
            return false;
        }
        
        // 2. 检查是否已到出现时间（timeA是首次出现时间）
        if (currentDisplayTime < command.timeA)
        {
            return false; // 还未到出现时间
        }
        
        return true;
    }

    /// <summary>
    /// 判断音符是否已被销毁（有destroy指令且当前时间已超过destroy时间）
    /// </summary>
    private bool IsNoteDestroyed(Command command)
    {
        if (command == null) return false;
        
        // 查找该音符的 destroy 指令
        var destroyCmd = ChartData.Instance.commands
            .FirstOrDefault(c => c.num == command.num && c.commandName == "destroy");
        
        // 如果有 destroy 指令且当前显示时间已超过 destroy 时间，则认为已销毁
        if (destroyCmd != null && currentDisplayTime >= destroyCmd.timeA)
        {
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// 判断音符在指定时间是否应该显示
    /// </summary>
    private bool ShouldShowNoteAtTime(Command command, float time)
    {
        if (command == null) return false;
        
        // 1. 如果时间早于首次出现时间，不显示
        if (time < command.timeA)
            return false;
        
        // 2. 检查是否有 destroy 指令
        var destroyCmd = ChartData.Instance.commands
            .FirstOrDefault(c => c.num == command.num && c.commandName == "destroy");
        
        if (destroyCmd != null && time >= destroyCmd.timeA)
            return false; // 已被销毁
        
        // 3. 检查是否有 drop_to 指令且已结束
        var dropToCmds = ChartData.Instance.commands
            .Where(c => c.num == command.num && c.commandName == "drop_to")
            .OrderByDescending(c => c.timeB); // 按结束时间降序，取最后一个
        
        foreach (var dropToCmd in dropToCmds)
        {
            // 如果时间超过 drop_to 的结束时间，且没有后续指令，则不显示
            if (time > dropToCmd.timeB)
            {
                // 检查是否有后续的 move 或 shift 指令
                var hasSubsequentMove = ChartData.Instance.commands
                    .Any(c => c.num == command.num && 
                             (c.commandName == "move" || c.commandName == "shift") && 
                             c.timeA >= dropToCmd.timeB);
                
                if (!hasSubsequentMove)
                    return false; // drop_to 结束后没有后续移动，音符消失
            }
        }
        
        return true;
    }

    /// <summary>
    /// 计算音符在指定时间的理论位置
    /// </summary>
    private Vector2 CalculateNotePositionAtTime(Command command, float time)
    {
        if (command == null)
        {
            Debug.LogWarning("CalculateNotePositionAtTime: command 为 null");
            return Vector2.zero;
        }

        // 默认位置：使用初始位置 (x1, y1)
        Vector2 currentPos = new Vector2(command.x1, command.y1);

        // 查找所有与此音符相关的移动指令（shift, move, drop_to）
        var moveCommands = ChartData.Instance.commands
            .Where(c => c.num == command.num && 
                       (c.commandName == "shift" || c.commandName == "move" || c.commandName == "drop_to") &&
                       c.timeA <= time)
            .OrderBy(c => c.timeA);

        foreach (var cmd in moveCommands)
        {
            switch (cmd.commandName)
            {
                case "shift":
                    // Shift指令：线性移动到目标位置
                    if (time >= cmd.timeA && time <= cmd.timeB && cmd.timeB > cmd.timeA)
                    {
                        float progress = Mathf.Clamp01((time - cmd.timeA) / (cmd.timeB - cmd.timeA));
                        currentPos = Vector2.Lerp(new Vector2(cmd.x1, cmd.y1), new Vector2(cmd.x2, cmd.y2), progress);
                    }
                    else if (time > cmd.timeB)
                    {
                        // 移动完成后保持在目标位置
                        currentPos = new Vector2(cmd.x2, cmd.y2);
                    }
                    break;

                case "move":
                    // Move指令：基于JSON文件的复杂移动路径
                    if (!string.IsNullOrEmpty(cmd.json_filename) && time >= cmd.timeA)
                    {
                        Vector2 movePos = CalculateMovePositionFromJson(cmd.json_filename, time - cmd.timeA, currentPos);
                        // 更新位置（如果解析失败会返回原位置）
                        currentPos = movePos;
                    }
                    break;

                case "drop_to":
                    // Drop_to指令：移动到指定按键位置
                    if (time >= cmd.timeA && time <= cmd.timeB && cmd.timeB > cmd.timeA)
                    {
                        float progress = Mathf.Clamp01((time - cmd.timeA) / (cmd.timeB - cmd.timeA));
                        currentPos = Vector2.Lerp(new Vector2(cmd.x1, cmd.y1), new Vector2(cmd.x2, cmd.y2), progress);
                    }
                    else if (time > cmd.timeB)
                    {
                        // 移动完成后保持在目标位置
                        currentPos = new Vector2(cmd.x2, cmd.y2);
                    }
                    break;
            }
        }

        return currentPos;
    }

    /// <summary>
    /// 计算按键在指定时间的理论位置
    /// </summary>
    private Vector2 CalculateKeyPositionAtTime(KeyData keyData, float time)
    {
        // 默认位置：使用初始位置 (x, y)
        Vector2 currentPos = new Vector2(keyData.x, keyData.y);

        // 检查所有按键指令，只处理移动相关的
        if (keyData.keyCommands != null)
        {
            var moveCommands = keyData.keyCommands
                .Where(c => (c.cmdType == "shift" || c.cmdType == "move") && c.startTime <= time)
                .OrderBy(c => c.startTime);

            foreach (var keyCmd in moveCommands)
            {
                switch (keyCmd.cmdType)
                {
                    case "shift":
                        // Shift指令：线性移动
                        if (time >= keyCmd.startTime && time <= keyCmd.endTime && keyCmd.endTime > keyCmd.startTime)
                        {
                            float progress = Mathf.Clamp01((time - keyCmd.startTime) / (keyCmd.endTime - keyCmd.startTime));
                            currentPos = Vector2.Lerp(new Vector2(keyCmd.x1, keyCmd.y1), new Vector2(keyCmd.x2, keyCmd.y2), progress);
                        }
                        else if (time > keyCmd.endTime)
                        {
                            // 移动完成后保持在目标位置
                            currentPos = new Vector2(keyCmd.x2, keyCmd.y2);
                        }
                        break;

                    case "move":
                        // Move指令：基于JSON文件的复杂移动路径
                        if (!string.IsNullOrEmpty(keyCmd.json_filename) && time >= keyCmd.startTime)
                        {
                            Vector2 movePos = CalculateMovePositionFromJson(keyCmd.json_filename, time - keyCmd.startTime, currentPos);
                            // 更新位置（如果解析失败会返回原位置）
                            currentPos = movePos;
                        }
                        break;
                }
            }
        }

        return currentPos;
    }

    /// <summary>
    /// 从JSON文件计算移动位置
    /// 如果JSON文件不存在或解析失败，返回原位置
    /// </summary>
    private Vector2 CalculateMovePositionFromJson(string jsonFilename, float relativeTime, Vector2 currentPos)
    {
        try
        {
            string jsonPath = Path.Combine(Application.streamingAssetsPath, jsonFilename);
            if (!File.Exists(jsonPath))
            {
                // JSON文件不存在，保持当前位置
                Debug.LogWarning($"Move指令JSON文件不存在: {jsonPath}");
                return currentPos;
            }

            string jsonContent = File.ReadAllText(jsonPath);
            
            // 使用 MoveFrameList 的模式解析
            var frameList = JsonUtility.FromJson<MoveFrameList>(jsonContent);

            if (frameList != null && frameList.frames != null && frameList.frames.Count > 0)
            {
                // 找到当前时间对应的帧
                MoveFrame prevFrame = null;
                MoveFrame nextFrame = null;

                foreach (var frame in frameList.frames)
                {
                    if (frame.time <= relativeTime)
                    {
                        prevFrame = frame;
                    }
                    else
                    {
                        nextFrame = frame;
                        break;
                    }
                }

                if (prevFrame == null)
                {
                    return new Vector2(frameList.frames[0].x, frameList.frames[0].y);
                }
                else if (nextFrame == null)
                {
                    return new Vector2(prevFrame.x, prevFrame.y);
                }
                else
                {
                    float progress = (relativeTime - prevFrame.time) / (nextFrame.time - prevFrame.time);
                    return Vector2.Lerp(new Vector2(prevFrame.x, prevFrame.y), new Vector2(nextFrame.x, nextFrame.y), progress);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"解析移动JSON文件失败 {jsonFilename}: {e.Message}");
        }

        // 解析失败，返回原位置
        return currentPos;
    }

}
