using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using System.Globalization;
using TMPro;
using System;

public class CreateSceneManager : MonoBehaviour
{
    public enum PlaceMode
    {
        Note
    }

    [Header("UI References")]
    public TMP_InputField timeInputField;          // 用于编辑选中音符的时间
    public TMP_Dropdown noteTypeDropdown;          // 用于选择要放置的音符类型（不再用于编辑）
    public TMP_InputField noteTypeInputField;      // 新增：用于编辑选中音符的类型（文本输入）
    public TMP_InputField keyIndexInputField;      // 用于编辑音符的 key_name (仅当指令为 drop_to 时有效)
    public TMP_InputField keyNameInputField;       // 用于编辑按键的 keyName (仅选中按键时显示)
    public TextMeshProUGUI infoText;               // 显示选中对象信息

    [Header("Prefabs")]
    public GameObject notePrefab;
    public GameObject keyPrefab;
    public GameObject followPrefab;                // 放置时的跟随物体

    [Header("Sprites")]
    public Sprite[] noteSprites;
    public Sprite[] keySprites;

    [Header("Preview")]
    public Sprite previewDotSprite;                // 预览点（可选，保留但未使用）

    [Header("Settings")]
    public float planeZ = 0f;
    public Camera mainCamera;

    [Header("Chart Settings UI")]                  // 新增：谱面设置 UI
    public Button settingsButton;                  // 打开/关闭设置面板的按钮
    public GameObject settingsPanel;               // 设置面板（默认隐藏）
    public TMP_InputField totalDurationInput;      // 总时长输入框
    public TMP_InputField keyIdsInput;             // KeyIds 输入框（逗号分隔整数）
    public Button confirmSettingsButton;           // 确认设置按钮

    // 放置模式相关
    private bool isPlacing = false;
    private PlaceMode currentMode = PlaceMode.Note;
    private NoteType currentNoteType;
    private GameObject followObject;

    // 选中对象相关
    private GameObject selectedObject;
    private enum SelectedType { None, Note, Key }
    private SelectedType selectedType = SelectedType.None;
    private Command selectedCommand;    // 当选中音符时
    private KeyData selectedKeyData;    // 当选中按键时

    void Start()
    {
        // 监听下拉列表变化：用于放置模式类型选择
        noteTypeDropdown.onValueChanged.AddListener(OnDropdownTypeSelected);

        // 监听输入框变化
        timeInputField.onValueChanged.AddListener(OnTimeChanged);
        keyIndexInputField.onValueChanged.AddListener(OnKeyIndexChanged);
        keyNameInputField.onValueChanged.AddListener(OnKeyNameChanged);
        noteTypeInputField.onValueChanged.AddListener(OnNoteTypeInputChanged);

        // 初始隐藏专用编辑框
        keyIndexInputField.gameObject.SetActive(false);
        keyNameInputField.gameObject.SetActive(false);
        noteTypeInputField.gameObject.SetActive(false);

        if (mainCamera == null) mainCamera = Camera.main;

        // 初始化下拉选项
        noteTypeDropdown.ClearOptions();
        noteTypeDropdown.AddOptions(new List<string> { "Tap", "Hold", "DTap", "Flick", "Drag", "Key" });

        // ---------- 初始化谱面设置 UI ----------
        if (settingsButton != null)
            settingsButton.onClick.AddListener(ToggleSettingsPanel);

        if (confirmSettingsButton != null)
            confirmSettingsButton.onClick.AddListener(OnConfirmSettings);

        // 设置面板默认隐藏
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // 从 ChartData 加载当前设置值到输入框
        LoadChartSettingsToUI();
    }

    void Update()
    {
        // 放置模式逻辑
        if (isPlacing && followObject != null)
        {
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = mainCamera.WorldToScreenPoint(new Vector3(0, 0, planeZ)).z;
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);
            worldPos.z = planeZ;
            followObject.transform.position = worldPos;

            // 左键放置（非UI区域）
            if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
            {
                if (currentMode == PlaceMode.Note)
                {
                    // Ctrl+左键放置按键，否则放置音符
                    if (currentNoteType == NoteType.Key && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                    {
                        PlaceKeyObject(worldPos);
                    }
                    else
                    {
                        PlaceNote(worldPos);
                    }
                }
            }

            // 右键退出放置模式（销毁跟随物体）
            if (Input.GetMouseButtonDown(1) && !EventSystem.current.IsPointerOverGameObject())
            {
                ExitPlaceMode();
            }
        }

        // 点击选择逻辑（使用2D射线检测）
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            Vector2 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

            if (hit.collider != null)
            {
                GameObject hitObj = hit.collider.gameObject;

                NoteObject noteComp = hitObj.GetComponent<NoteObject>();
                KeyObject keyComp = hitObj.GetComponent<KeyObject>();

                if (noteComp != null)
                {
                    SelectNote(noteComp);
                }
                else if (keyComp != null)
                {
                    SelectKey(keyComp);
                }
                else
                {
                    Deselect();
                }
            }
            else
            {
                Deselect();
            }
        }
    }

    // ---------- 放置方法 ----------
    void PlaceNote(Vector3 worldPos)
    {
        if (!float.TryParse(timeInputField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float timeA))
        {
            infoText.text = "请输入有效的时间";
            return;
        }

        Command newCmd = new Command
        {
            type = currentNoteType,
            num = GenerateNoteNumber(),
            timeA = timeA,
            x1 = worldPos.x,
            y1 = worldPos.y,
            x2 = worldPos.x,
            y2 = worldPos.y,
            is_show = true,
            isNoteFirstTimeOccured = true,
            commandName = "drop_to",
            hold_duration = (currentNoteType == NoteType.Hold) ? 1f : 0f,
            key_name = 0 // 默认无关联按键
        };

        ChartData.Instance.AddNoteData(newCmd);
        ChartData.Instance.SortCommandsByTime();

        GameObject noteObj = Instantiate(notePrefab, worldPos, Quaternion.identity);
        NoteObject noteComp = noteObj.AddComponent<NoteObject>();
        noteComp.command = newCmd;

        SpriteRenderer sr = noteObj.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            int idx = GetNoteTypeIndex(currentNoteType);
            if (idx >= 0) sr.sprite = noteSprites[idx];
        }

        infoText.text = $"已放置 {currentNoteType} 音符，编号 {newCmd.num}，时间 {timeA}s";
    }

    void PlaceKeyObject(Vector3 worldPos)
    {
        int keyId = 1; // 固定 Key ID，可根据需要修改

        if (ChartData.Instance.keyDatas.Any(k => k.keyName == keyId))
        {
            infoText.text = $"Key ID {keyId} 已存在，请勿重复放置";
            return;
        }

        KeyData newKey = new KeyData(keyId, worldPos.x, worldPos.y, 1);
        ChartData.Instance.keyDatas.Add(newKey);

        GameObject keyObj = Instantiate(keyPrefab, worldPos, Quaternion.identity);
        KeyObject keyComp = keyObj.AddComponent<KeyObject>();
        keyComp.keyData = newKey;

        SpriteRenderer sr = keyObj.GetComponent<SpriteRenderer>();
        if (sr != null && keyId >= 1 && keyId <= keySprites.Length)
            sr.sprite = keySprites[keyId - 1];

        infoText.text = $"已放置按键 Key ID {keyId}，位置 ({worldPos.x:F2}, {worldPos.y:F2})";
    }

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

        UpdateInfoPanelForKey();
    }

    private void Deselect()
    {
        selectedObject = null;
        selectedType = SelectedType.None;
        selectedCommand = null;
        selectedKeyData = null;

        // 隐藏所有编辑控件
        keyIndexInputField.gameObject.SetActive(false);
        keyNameInputField.gameObject.SetActive(false);
        noteTypeInputField.gameObject.SetActive(false); // 隐藏类型输入框

        // 显示下拉列表，用于放置模式选择
        noteTypeDropdown.gameObject.SetActive(true);
        timeInputField.gameObject.SetActive(true); // 时间输入框保持显示（可用于放置时输入时间）

        // 清空信息文本，不显示任何消息
        infoText.text = "";
    }

    private void UpdateInfoPanelForNote()
    {
        if (selectedCommand == null) return;

        // 显示基本信息
        string keyIndexText = selectedCommand.commandName == "drop_to" ? selectedCommand.key_name.ToString() : "null";
        infoText.text = $"选中音符 [编号 {selectedCommand.num}] (不可编辑)\n" +
                       $"keyindex: {keyIndexText} (可编辑)\n" +
                       $"时间: {selectedCommand.timeA}\n" +
                       $"类型: {selectedCommand.type}\n" +
                       $"位置: ({selectedCommand.x1}, {selectedCommand.y1})";

        // 填充时间输入框
        timeInputField.text = selectedCommand.timeA.ToString(CultureInfo.InvariantCulture);

        // 显示并设置 keyIndex 输入框（仅当是 drop_to 指令时允许编辑）
        bool canEditKeyIndex = (selectedCommand.commandName == "drop_to");
        keyIndexInputField.gameObject.SetActive(canEditKeyIndex);
        if (canEditKeyIndex)
        {
            keyIndexInputField.text = selectedCommand.key_name.ToString();
        }

        // 隐藏按键专用的输入框
        keyNameInputField.gameObject.SetActive(false);

        // 切换 UI：隐藏下拉列表，显示类型输入框
        noteTypeDropdown.gameObject.SetActive(false);
        noteTypeInputField.gameObject.SetActive(true);
        noteTypeInputField.text = selectedCommand.type.ToString(); // 显示当前类型字符串
    }

    private void UpdateInfoPanelForKey()
    {
        if (selectedKeyData == null) return;

        infoText.text = $"选中按键 [ID {selectedKeyData.keyName}] (可编辑)\n" +
                       $"位置: ({selectedKeyData.x}, {selectedKeyData.y})\n" +
                       $"显示: {(selectedKeyData.show == 1 ? "是" : "否")}\n" +
                       $"移动指令数: {selectedKeyData.keyCommands.Count} (可编辑)";

        // 显示并设置 keyName 输入框
        keyNameInputField.gameObject.SetActive(true);
        keyNameInputField.text = selectedKeyData.keyName.ToString();

        // 隐藏音符专用的输入框
        keyIndexInputField.gameObject.SetActive(false);
        noteTypeInputField.gameObject.SetActive(false); // 隐藏类型输入框
        noteTypeDropdown.gameObject.SetActive(false);    // 隐藏下拉列表（按键无需类型）
        timeInputField.gameObject.SetActive(false);     // 按键不需要时间编辑
    }

    // ---------- 编辑事件回调 ----------
    // 下拉列表选择回调：用于进入放置模式并设置当前类型
    private void OnDropdownTypeSelected(int index)
    {
        // 更新当前要放置的类型
        currentNoteType = (NoteType)index;

        // 如果当前没有选中任何对象，则进入放置模式
        if (selectedType == SelectedType.None)
        {
            EnterPlaceMode();
        }
        // 如果有选中对象，我们仍然允许切换类型（但不影响选中对象），可保持放置模式或仅更新跟随物体
        // 这里选择：如果已经在放置模式，更新跟随物体；否则不自动进入放置模式
        else if (isPlacing)
        {
            UpdateFollowObjectSprite();
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

    private void OnTimeChanged(string value)
    {
        if (selectedType != SelectedType.Note || selectedCommand == null) return;

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float newTime))
        {
            selectedCommand.timeA = newTime;
            UpdateInfoPanelForNote();
        }
    }

    private void OnKeyIndexChanged(string value)
    {
        if (selectedType != SelectedType.Note || selectedCommand == null || selectedCommand.commandName != "drop_to") return;

        if (int.TryParse(value, out int newKeyIndex))
        {
            selectedCommand.key_name = newKeyIndex;
            UpdateInfoPanelForNote();
        }
    }

    private void OnKeyNameChanged(string value)
    {
        if (selectedType != SelectedType.Key || selectedKeyData == null) return;

        if (int.TryParse(value, out int newKeyId))
        {
            selectedKeyData.keyName = newKeyId;

            SpriteRenderer sr = selectedObject.GetComponent<SpriteRenderer>();
            if (sr != null && newKeyId >= 1 && newKeyId <= keySprites.Length)
                sr.sprite = keySprites[newKeyId - 1];

            UpdateInfoPanelForKey();
        }
    }

    // ---------- 辅助方法 ----------
    int GenerateNoteNumber()
    {
        if (ChartData.Instance.commands.Count == 0) return 0;
        return ChartData.Instance.commands.Max(c => c.num) + 1;
    }

    int GetNoteTypeIndex(NoteType type)
    {
        switch (type)
        {
            case NoteType.Tap: return 0;
            case NoteType.Hold: return 1;
            case NoteType.DTap: return 2;
            case NoteType.Flick: return 3;
            case NoteType.Drag: return 4;
            case NoteType.Key: return 5;
            default: return 0;
        }
    }

    // ---------- 放置模式控制 ----------
    public void EnterPlaceMode()
    {
        if (isPlacing) return;
        isPlacing = true;

        if (followPrefab != null)
        {
            followObject = Instantiate(followPrefab);
            UpdateFollowObjectSprite();
        }
    }

    public void ExitPlaceMode()
    {
        if (!isPlacing) return;
        isPlacing = false;
        if (followObject != null) Destroy(followObject);
    }

    void UpdateFollowObjectSprite()
    {
        if (followObject == null) return;
        SpriteRenderer sr = followObject.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        if (currentMode == PlaceMode.Note)
        {
            if (currentNoteType == NoteType.Key)
            {
                sr.sprite = keySprites.Length > 0 ? keySprites[0] : null;
            }
            else
            {
                int idx = GetNoteTypeIndex(currentNoteType);
                sr.sprite = (idx >= 0 && idx < noteSprites.Length) ? noteSprites[idx] : null;
            }
        }
    }

    // 可以由外部 UI 按钮调用
    public void SetNoteType(int typeIndex)
    {
        currentNoteType = (NoteType)typeIndex;
        if (isPlacing)
        {
            ExitPlaceMode();
            EnterPlaceMode();
        }
    }

    // ========== 新增：谱面设置相关方法 ==========
    /// <summary>
    /// 从 ChartData 加载当前值到 UI 输入框
    /// </summary>
    private void LoadChartSettingsToUI()
    {
        if (totalDurationInput != null)
            totalDurationInput.text = ChartData.Instance.totalDuration.ToString(CultureInfo.InvariantCulture);

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
            // 可选：自动关闭设置面板
            if (settingsPanel != null)
                settingsPanel.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (followObject != null) Destroy(followObject);
    }
}