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

    [Header("Placement Settings")]
    public TMP_Dropdown noteTypeDropdown;          // 用于选择要放置的音符类型
    public TMP_InputField timeInputField;          // 用于编辑放置时间/选中音符的判定时间

    [Header("Note Editor")]
    public TMP_InputField noteTypeInputField;      // 用于编辑选中音符的类型（文本输入）
    public TMP_InputField keyIndexInputField;      // 用于编辑音符的 key_name (仅当指令为 drop_to 时有效)
    
    [Header("Command Management")]
    public Button cmdModeToggleBtn;                // 切换 Add/Delete 模式的按钮
    public TMP_Dropdown cmdDropdown;               // 指令列表（Add 时为可选类型，Delete 时为已有指令）
    public TMP_InputField cmdInputField;           // Add 模式下的输入框
    public Button cmdConfirmBtn;                   // 执行 Add/Delete 的确认按钮

    [Header("Key Editor")]
    public TMP_InputField keyNameInputField;       // 用于编辑按键的 keyName (仅选中按键时显示)

    [Header("Chart Settings")]
    public Button settingsButton;                  // 打开/关闭设置面板的按钮
    public GameObject settingsPanel;               // 设置面板（默认隐藏）
    public TMP_InputField totalDurationInput;      // 总时长输入框
    public TMP_InputField keyIdsInput;             // KeyIds 输入框（逗号分隔整数）
    public Button confirmSettingsButton;           // 确认设置按钮

    [Header("General UI & Info")]
    public TextMeshProUGUI infoText;               // 显示选中对象信息
    public Camera mainCamera;

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

    // 指令管理状态
    private bool isAddMode = true; // 默认 Add 模式

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
        
        // 初始隐藏指令管理相关
        if (cmdModeToggleBtn != null) cmdModeToggleBtn.gameObject.SetActive(false);
        if (cmdDropdown != null) cmdDropdown.gameObject.SetActive(false);
        if (cmdInputField != null) cmdInputField.gameObject.SetActive(false);
        if (cmdConfirmBtn != null) cmdConfirmBtn.gameObject.SetActive(false);

        if (mainCamera == null) mainCamera = Camera.main;

        // 初始化下拉选项
        noteTypeDropdown.ClearOptions();
        noteTypeDropdown.AddOptions(new List<string> { "(empty)", "Tap", "Hold", "DTap", "Flick", "Key", "Drag" });

        // ---------- 初始化指令管理 UI ----------
        if (cmdModeToggleBtn != null)
            cmdModeToggleBtn.onClick.AddListener(ToggleCmdMode);

        if (cmdConfirmBtn != null)
            cmdConfirmBtn.onClick.AddListener(OnConfirmCmdAction);

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

        // 新增：加载已有的音符和按键对象
        SpawnExistingObjects();
    }

    void Update()
    {
        // 新增：快捷键删除 (仅当没有输入框获得焦点时)
        if (Input.GetKeyDown(KeyCode.Delete) && 
            (EventSystem.current.currentSelectedGameObject == null || 
             EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() == null))
        {
            DeleteSelected();
        }

        // 优先处理放置模式逻辑
        if (isPlacing)
        {
            if (followObject != null)
            {
                Vector3 mousePos = Input.mousePosition;
                mousePos.z = mainCamera.WorldToScreenPoint(new Vector3(0, 0, planeZ)).z;
                Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);
                worldPos.z = planeZ;
                followObject.transform.position = worldPos;

                // 动态更新预览精灵（处理 Ctrl 键切换 KeyObject/NoteObject）
                UpdateFollowObjectSprite();

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
            }

            // 右键退出放置模式（无论跟随物体是否存在）
            if (Input.GetMouseButtonDown(1) && !EventSystem.current.IsPointerOverGameObject())
            {
                ExitPlaceMode();
            }
        }
        // 点击选择逻辑（仅在非放置模式或未点击UI时）
        else if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
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
        if (!float.TryParse(timeInputField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float chartTime))
        {
            infoText.text = "请输入有效的时间";
            return;
        }

        Command newCmd = new Command
        {
            type = currentNoteType,
            num = GenerateNoteNumber(),
            timeB = chartTime,             // 使用输入框中的时间作为判定时间 (timeB)
            timeA = chartTime - 1f,        // 默认开始时间 (timeA) 为判定时间前 1s
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

        SpawnNoteObject(newCmd);

        infoText.text = $"已放置 {currentNoteType} 音符，编号 {newCmd.num}，时间 {chartTime:F2}s";
    }

    void SpawnNoteObject(Command cmd)
    {
        Vector3 worldPos = new Vector3(cmd.x1, cmd.y1, planeZ);
        GameObject noteObj = Instantiate(notePrefab, worldPos, Quaternion.identity);
        NoteObject noteComp = noteObj.AddComponent<NoteObject>();
        noteComp.command = cmd;

        SpriteRenderer sr = noteObj.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            int idx = GetNoteTypeIndex(cmd.type);
            if (idx >= 0 && idx < noteSprites.Length) sr.sprite = noteSprites[idx];
        }
    }

    void PlaceKeyObject(Vector3 worldPos)
    {
        int keyId = 1; // 默认起始 ID
        if (ChartData.Instance.keyDatas.Count > 0)
        {
            keyId = ChartData.Instance.keyDatas.Max(k => k.keyName) + 1;
        }

        if (ChartData.Instance.keyDatas.Any(k => k.keyName == keyId))
        {
            infoText.text = $"Key ID {keyId} 已存在，请勿重复放置";
            return;
        }

        KeyData newKey = new KeyData(keyId, worldPos.x, worldPos.y, 1);
        ChartData.Instance.keyDatas.Add(newKey);

        SpawnKeyObject(newKey);

        infoText.text = $"已放置按键 Key ID {keyId}，位置 ({worldPos.x:F2}, {worldPos.y:F2})";
    }

    void SpawnKeyObject(KeyData keyData)
    {
        Vector3 worldPos = new Vector3(keyData.x, keyData.y, planeZ);
        GameObject keyObj = Instantiate(keyPrefab, worldPos, Quaternion.identity);
        KeyObject keyComp = keyObj.AddComponent<KeyObject>();
        keyComp.keyData = keyData;

        SpriteRenderer sr = keyObj.GetComponent<SpriteRenderer>();
        if (sr != null && keyData.keyName >= 1 && keyData.keyName <= keySprites.Length)
            sr.sprite = keySprites[keyData.keyName - 1];
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

        // 隐藏指令管理相关
        if (cmdModeToggleBtn != null) cmdModeToggleBtn.gameObject.SetActive(false);
        if (cmdDropdown != null) cmdDropdown.gameObject.SetActive(false);
        if (cmdInputField != null) cmdInputField.gameObject.SetActive(false);
        if (cmdConfirmBtn != null) cmdConfirmBtn.gameObject.SetActive(false);

        // 显示下拉列表，用于放置模式选择
        noteTypeDropdown.gameObject.SetActive(true);
        noteTypeDropdown.value = 0; // 重置为 (empty)
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
                       $"判定时间 (timeB): {selectedCommand.timeB}\n" +
                       $"开始时间 (timeA): {selectedCommand.timeA}\n" +
                       $"类型: {selectedCommand.type}\n" +
                       $"位置: ({selectedCommand.x1}, {selectedCommand.y1})";

        // 填充时间输入框（显示判定时间）
        timeInputField.text = selectedCommand.timeB.ToString(CultureInfo.InvariantCulture);

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

        // 更新指令管理 UI
        UpdateCmdManagementUI();
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

        // 隐藏指令管理相关
        if (cmdModeToggleBtn != null) cmdModeToggleBtn.gameObject.SetActive(false);
        if (cmdDropdown != null) cmdDropdown.gameObject.SetActive(false);
        if (cmdInputField != null) cmdInputField.gameObject.SetActive(false);
        if (cmdConfirmBtn != null) cmdConfirmBtn.gameObject.SetActive(false);
    }

    // ---------- 指令管理方法 ----------
    private void ToggleCmdMode()
    {
        isAddMode = !isAddMode;
        UpdateCmdManagementUI();
    }

    private void UpdateCmdManagementUI()
    {
        if (selectedType != SelectedType.Note || selectedCommand == null)
        {
            cmdModeToggleBtn.gameObject.SetActive(false);
            cmdDropdown.gameObject.SetActive(false);
            cmdInputField.gameObject.SetActive(false);
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
        cmdDropdown.gameObject.SetActive(true);

        cmdDropdown.ClearOptions();
        if (isAddMode)
        {
            // Add 模式：显示可选指令类型
            cmdDropdown.AddOptions(new List<string> { "shift", "move", "destroy", "drop_to" });
            cmdInputField.gameObject.SetActive(true);
            cmdInputField.placeholder.GetComponent<TextMeshProUGUI>().text = "Params (e.g., move_1 or duration)";
        }
        else
        {
            // Delete 模式：显示该音符已有的指令
            cmdInputField.gameObject.SetActive(false);
            var noteCmds = ChartData.Instance.commands.Where(c => c.num == selectedCommand.num).ToList();
            List<string> options = noteCmds.Select(c => $"{c.commandName} (tA:{c.timeA})").ToList();
            cmdDropdown.AddOptions(options);
        }
    }

    private void OnConfirmCmdAction()
    {
        if (selectedType != SelectedType.Note || selectedCommand == null) return;

        if (isAddMode)
        {
            // 执行 Add 逻辑
            string cmdName = cmdDropdown.options[cmdDropdown.value].text;
            string param = cmdInputField.text;
            
            Command newCmd = new Command
            {
                num = selectedCommand.num,
                type = selectedCommand.type,
                commandName = cmdName,
                is_show = true,
                timeA = selectedCommand.timeA, // 默认使用选中指令的时间
                timeB = selectedCommand.timeB,
                x1 = selectedCommand.x1,
                y1 = selectedCommand.y1,
                x2 = selectedCommand.x2,
                y2 = selectedCommand.y2,
                key_name = selectedCommand.key_name,
                isNoteFirstTimeOccured = false // 新增指令非首次出现
            };

            // 根据指令类型处理参数
            if (cmdName == "move") newCmd.json_filename = param;
            else if (cmdName == "drop_to" && selectedCommand.type == NoteType.Hold)
            {
                if (float.TryParse(param, out float dur)) newCmd.hold_duration = dur;
            }

            ChartData.Instance.AddNoteData(newCmd);
            ChartData.Instance.SortCommandsByTime();
            infoText.text = $"已为音符 {selectedCommand.num} 添加指令 {cmdName}";
        }
        else
        {
            // 执行 Delete 逻辑
            var noteCmds = ChartData.Instance.commands.Where(c => c.num == selectedCommand.num).ToList();
            if (cmdDropdown.value >= 0 && cmdDropdown.value < noteCmds.Count)
            {
                Command toDelete = noteCmds[cmdDropdown.value];
                if (toDelete == selectedCommand && noteCmds.Count == 1)
                {
                    infoText.text = "不能删除音符的唯一指令，请使用 Delete 键删除整个音符";
                    return;
                }
                
                ChartData.Instance.commands.Remove(toDelete);
                if (toDelete == selectedCommand)
                {
                    // 如果删除了当前选中的指令，重新选择一个
                    selectedCommand = ChartData.Instance.commands.FirstOrDefault(c => c.num == toDelete.num);
                }
                infoText.text = $"已删除音符 {toDelete.num} 的指令 {toDelete.commandName}";
            }
        }

        UpdateCmdManagementUI();
        UpdateInfoPanelForNote();
    }

    // ---------- 编辑事件回调 ----------
    // 下拉列表选择回调：用于进入放置模式并设置当前类型
    private void OnDropdownTypeSelected(int index)
    {
        if (index == 0) // 选择 "(empty)"，退出放置模式
        {
            ExitPlaceMode();
            return;
        }

        // 更新当前要放置的类型 (index 0 为 empty，index 1 对应 NoteType.Tap(0))
        currentNoteType = (NoteType)(index - 1);

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

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float newTimeB))
        {            float duration = selectedCommand.timeB - selectedCommand.timeA;
            selectedCommand.timeB = newTimeB;
            selectedCommand.timeA = newTimeB - (duration > 0 ? duration : 1f); // 保持持续时间或设为 1s
            
            // 新增：修改时间后重新排序
            ChartData.Instance.SortCommandsByTime();
            
            UpdateInfoPanelForNote();
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

    // ---------- 新增辅助方法 ----------
    private void SpawnExistingObjects()
    {
        // 加载音符
        foreach (var cmd in ChartData.Instance.commands)
        {
            SpawnNoteObject(cmd);
        }
        // 加载按键
        foreach (var keyData in ChartData.Instance.keyDatas)
        {
            SpawnKeyObject(keyData);
        }
    }

    private void DeleteSelected()
    {
        if (selectedObject == null) return;

        if (selectedType == SelectedType.Note && selectedCommand != null)
        {
            ChartData.Instance.commands.Remove(selectedCommand);
            infoText.text = $"已删除音符，编号 {selectedCommand.num}";
        }
        else if (selectedType == SelectedType.Key && selectedKeyData != null)
        {
            // 新增：删除按键时，将所有关联该按键的音符 key_name 重置为 0
            int oldKeyId = selectedKeyData.keyName;
            foreach (var cmd in ChartData.Instance.commands)
            {
                if (cmd.key_name == oldKeyId)
                {
                    cmd.key_name = 0;
                }
            }

            ChartData.Instance.keyDatas.Remove(selectedKeyData);
            infoText.text = $"已删除按键，ID {selectedKeyData.keyName}";
        }

        Destroy(selectedObject);
        Deselect();
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
            case NoteType.Key: return 4;
            case NoteType.Drag: return 5;
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

        // 新增：同步下拉框状态，重置为 "(empty)"
        if (noteTypeDropdown != null && noteTypeDropdown.value != 0)
        {
            noteTypeDropdown.value = 0;
        }
    }

    void UpdateFollowObjectSprite()
    {
        if (followObject == null) return;
        SpriteRenderer sr = followObject.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        if (currentMode == PlaceMode.Note)
        {
            // 如果按住 Ctrl 且当前类型是 Key，预览显示 KeyObject 精灵，否则预览显示音符精灵
            if (currentNoteType == NoteType.Key && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
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
        // 直接设置下拉框的值，由 OnDropdownTypeSelected 触发后续逻辑
        if (noteTypeDropdown != null)
        {
            noteTypeDropdown.value = typeIndex + 1;
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