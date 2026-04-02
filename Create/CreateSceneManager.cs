using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;
using System.Globalization;
using TMPro;
using System;

public partial class CreateSceneManager : MonoBehaviour
{
    public enum PlaceMode
    {
        Note
    }

    [Header("Placement Settings")]
    public TMP_Dropdown noteTypeDropdown;          // 用于选择要放置的音符类型
    public TMP_InputField timeInputField;          // 用于编辑放置时间/选中音符的判定时间
    public Slider timeSlider;                      // 用于滑动选择判定时间 (与 timeInputField 同步)

    [Header("Note Editor")]
    public Button changeNoteTypeBtn;               // 更改音符类型按钮
    public Button finishBtn;                       // 完成按钮
    public TMP_InputField noteTypeInputField;      // 用于编辑选中音符的类型（文本输入）
    public TMP_InputField keyIndexInputField;      // 用于编辑音符的 key_name (仅当指令为 drop_to 时有效)
    
    [Header("Command Management")]
    public Button cmdModeToggleBtn;                // 切换 Add/Delete 模式的按钮
    public TMP_Dropdown cmdDropdown;               // 指令列表（Add 时为可选类型，Delete 时为已有指令）
    public TMP_Dropdown existingCmdsDropdown;      // 用于显示/选择该 note/key 现有的所有指令
    public Button cmdConfirmBtn;                   // 执行 Add/Delete 的确认按钮

    [Header("Command Detail Fields")]
    public Button startPosBtn;                     // 起始坐标捕捉按钮
    public Button endPosBtn;                       // 终止坐标捕捉按钮
    public TMP_InputField startPosInputField;       // 起始坐标输入框
    public TMP_InputField endPosInputField;         // 终止坐标输入框
    public TMP_InputField startTimeInputField;      // 起始时间输入框
    public TMP_InputField endTimeInputField;        // 终止时间输入框
    public TMP_InputField extraParamInputField;     // 额外参数输入框 (key_name, hold_dur, json_file 等)

    [Header("Move Command Special Options")]
    public GameObject moveOptionPanel;             // 移动选项面板 (Harmonic, Parabolic, Circular)
    public Button harmonicBtn;
    public Button parabolicBtn;
    public Button circularBtn;

    [Header("Key Editor")]
    public TMP_InputField keyNameInputField;       // 用于编辑按键的 keyName (仅选中按键时显示)

    [Header("General UI & Info")]
    public Button saveChartBtn;                    // 导出谱面按钮
    public Button settingsButton;                  // 打开谱面设置场景的按钮
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
    private KeyCommand selectedKeyCommand; // 当选中按键指令时

    // 指令管理状态
    private bool isAddMode = true; // 默认 Add 模式
    public enum AddStep { None, Start, SelectType, InputTime, InputPos, InputMoveParams, InputSpinParams }
    private AddStep currentAddStep = AddStep.None;

    private bool isCapturingStartPos = false;
    private bool isCapturingEndPos = false;
    private Vector2 capturedStartPos;
    private Vector2 capturedEndPos;

    async void Start()
    {
        // 监听下拉列表变化：用于放置模式类型选择
        noteTypeDropdown.onValueChanged.AddListener(OnDropdownTypeSelected);

        // 监听输入框变化
        timeInputField.onValueChanged.AddListener(OnTimeChanged);
        if (timeSlider != null) timeSlider.onValueChanged.AddListener(OnTimeSliderChanged);
        keyIndexInputField.onValueChanged.AddListener(OnKeyIndexChanged);
        keyNameInputField.onValueChanged.AddListener(OnKeyNameChanged);
        noteTypeInputField.onValueChanged.AddListener(OnNoteTypeInputChanged);
        if (existingCmdsDropdown != null) existingCmdsDropdown.onValueChanged.AddListener(OnExistingCmdSelected);

        // 初始隐藏专用编辑框
        keyIndexInputField.gameObject.SetActive(false);
        keyNameInputField.gameObject.SetActive(false);
        noteTypeInputField.gameObject.SetActive(false);
        if (changeNoteTypeBtn != null) changeNoteTypeBtn.gameObject.SetActive(false);
        if (finishBtn != null) finishBtn.gameObject.SetActive(false);
        if (moveOptionPanel != null) moveOptionPanel.SetActive(false);
        
        // 初始隐藏指令管理相关
        if (cmdModeToggleBtn != null) cmdModeToggleBtn.gameObject.SetActive(false);
        if (cmdDropdown != null) cmdDropdown.gameObject.SetActive(false);
        if (existingCmdsDropdown != null) existingCmdsDropdown.gameObject.SetActive(false);
        if (cmdConfirmBtn != null) cmdConfirmBtn.gameObject.SetActive(false);

        // 初始隐藏指令详细输入框
        HideCommandDetailFields();

        if (mainCamera == null) mainCamera = Camera.main;

        // 初始化下拉选项
        noteTypeDropdown.ClearOptions();
        noteTypeDropdown.AddOptions(new List<string> { "(empty)", "Tap", "Hold", "DTap", "Flick", "Key", "Drag" });

        // ---------- 初始化指令管理 UI ----------
        if (cmdModeToggleBtn != null)
            cmdModeToggleBtn.onClick.AddListener(ToggleCmdMode);

        if (cmdConfirmBtn != null)
            cmdConfirmBtn.onClick.AddListener(OnConfirmCmdAction);

        if (finishBtn != null)
            finishBtn.onClick.AddListener(Deselect);

        if (changeNoteTypeBtn != null)
            changeNoteTypeBtn.onClick.AddListener(OnChangeNoteTypeBtnClicked);

        if (harmonicBtn != null) harmonicBtn.onClick.AddListener(() => OnMoveOptionSelected("harmonic"));
        if (parabolicBtn != null) parabolicBtn.onClick.AddListener(() => OnMoveOptionSelected("parabolic"));
        if (circularBtn != null) circularBtn.onClick.AddListener(() => OnMoveOptionSelected("circular"));

        if (startPosBtn != null) startPosBtn.onClick.AddListener(OnStartPosBtnClicked);
        if (endPosBtn != null) endPosBtn.onClick.AddListener(OnEndPosBtnClicked);
        if (startPosInputField != null) startPosInputField.onEndEdit.AddListener(OnStartPosInputEndEdit);
        if (endPosInputField != null) endPosInputField.onEndEdit.AddListener(OnEndPosInputEndEdit);

        // 获取要编辑的文件名（如果有）
        string editingFileName = PlayerPrefs.GetString("EditingChartFileName", "");
        
        if (saveChartBtn != null)
        {
            string saveName = string.IsNullOrEmpty(editingFileName) ? "chart.txt" : editingFileName;
            saveChartBtn.onClick.AddListener(() => ExportChart(saveName));
        }

        // 监听设置按钮点击，跳转到 ChartSettingScene
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        }

        // 如果是编辑已有谱面，先加载
        if (!string.IsNullOrEmpty(editingFileName))
        {
            infoText.text = $"正在加载谱面：{editingFileName}...";
            
            // 确保有 LoadChart 组件
            LoadChart loader = GetComponent<LoadChart>();
            if (loader == null) loader = gameObject.AddComponent<LoadChart>();
            
            // 清空旧数据
            ChartData.Instance.ResetChartData();
            
            // editingFileName 已经包含完整相对路径（如"Create/chart.txt"）
            bool success = await loader.LoadChartFileAsync(editingFileName);
            if (success)
            {
                infoText.text = $"谱面 {editingFileName} 加载成功";
            }
            else
            {
                infoText.text = $"谱面 {editingFileName} 加载失败！";
            }
        }
        else
        {
            // 新建谱面，清空数据
            ChartData.Instance.ResetChartData();
            infoText.text = "新建谱面模式";
        }

        // 新增：加载已有的音符和按键对象
        SpawnExistingObjects();
    }

    /// <summary>
    /// 设置按钮点击回调：跳转到 ChartSettingScene
    /// </summary>
    private void OnSettingsButtonClicked()
    {
        // 保存当前编辑的谱面文件名，方便返回时使用
        string editingFileName = PlayerPrefs.GetString("EditingChartFileName", "");
        
        // 在跳转前，先保存当前的谱面设置（可选）
        SaveCurrentChartSettings();
        
        // 加载 ChartSettingScene
        UnityEngine.SceneManagement.SceneManager.LoadScene("ChartSettingScene");
        
        Debug.Log($"CreateSceneManager: 跳转到谱面设置场景 | 当前编辑文件：{editingFileName}");
    }

    /// <summary>
    /// 保存当前谱面设置到 PlayerPrefs（用于返回时恢复）
    /// </summary>
    private void SaveCurrentChartSettings()
    {
        // 保存总时长
        PlayerPrefs.SetFloat("Chart_TotalDuration", ChartData.Instance.totalDuration);
        
        // 保存 KeyIds（转为逗号分隔的字符串）
        string keyIdsStr = string.Join(",", ChartData.Instance.keyIds);
        PlayerPrefs.SetString("Chart_KeyIds", keyIdsStr);
        
        PlayerPrefs.Save();
        
        Debug.Log($"CreateSceneManager: 已保存谱面设置 | 总时长：{ChartData.Instance.totalDuration} | KeyIds: [{keyIdsStr}]");
    }

    void Update()
    {
        // 处理坐标捕捉
        if (isCapturingStartPos || isCapturingEndPos)
        {
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = mainCamera.WorldToScreenPoint(new Vector3(0, 0, planeZ)).z;
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);
            
            if (isCapturingStartPos)
            {
                startPosBtn.GetComponentInChildren<TextMeshProUGUI>().text = $"起始：({worldPos.x:F2}, {worldPos.y:F2})";
                if (startPosInputField != null) startPosInputField.text = $"{worldPos.x:F2}, {worldPos.y:F2}";
                if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
                {
                    capturedStartPos = new Vector2(worldPos.x, worldPos.y);
                    isCapturingStartPos = false;
                    infoText.text = "已捕捉起始坐标";
                }
            }
            else if (isCapturingEndPos)
            {
                endPosBtn.GetComponentInChildren<TextMeshProUGUI>().text = $"终止：({worldPos.x:F2}, {worldPos.y:F2})";
                if (endPosInputField != null) endPosInputField.text = $"{worldPos.x:F2}, {worldPos.y:F2}";
                if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
                {
                    capturedEndPos = new Vector2(worldPos.x, worldPos.y);
                    isCapturingEndPos = false;
                    infoText.text = "已捕捉终止坐标";
                }
            }
            
            // 右键取消捕捉
            if (Input.GetMouseButtonDown(1))
            {
                isCapturingStartPos = false;
                isCapturingEndPos = false;
                UpdatePosBtnTexts();
                infoText.text = "取消坐标捕捉";
            }

            return; // 捕捉模式下不执行其他 Update 逻辑
        }

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

                // 左键放置（非 UI 区域）
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
        // 点击选择逻辑（仅在非放置模式或未点击 UI 时）
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

    void OnDestroy()
    {
        if (followObject != null) Destroy(followObject);
    }
}