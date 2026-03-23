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
    public TMP_InputField startTimeInputField;      // 起始时间输入框
    public TMP_InputField endTimeInputField;        // 终止时间输入框
    public TMP_InputField extraParamInputField;     // 额外参数输入框 (key_name, hold_dur, json_file 等)

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
    private KeyCommand selectedKeyCommand; // 当选中按键指令时

    // 指令管理状态
    private bool isAddMode = true; // 默认 Add 模式
    private bool isCapturingStartPos = false;
    private bool isCapturingEndPos = false;
    private Vector2 capturedStartPos;
    private Vector2 capturedEndPos;

    void Start()
    {
        // 监听下拉列表变化：用于放置模式类型选择
        noteTypeDropdown.onValueChanged.AddListener(OnDropdownTypeSelected);

        // 监听输入框变化
        timeInputField.onValueChanged.AddListener(OnTimeChanged);
        if (timeSlider != null) timeSlider.onValueChanged.AddListener(OnTimeSliderChanged);
        keyIndexInputField.onValueChanged.AddListener(OnKeyIndexChanged);
        keyNameInputField.onValueChanged.AddListener(OnKeyNameChanged);
        noteTypeInputField.onValueChanged.AddListener(OnNoteTypeInputChanged);
        if (cmdDropdown != null) cmdDropdown.onValueChanged.AddListener(OnAddCmdTypeChanged);
        if (existingCmdsDropdown != null) existingCmdsDropdown.onValueChanged.AddListener(OnExistingCmdSelected);

        // 初始隐藏专用编辑框
        keyIndexInputField.gameObject.SetActive(false);
        keyNameInputField.gameObject.SetActive(false);
        noteTypeInputField.gameObject.SetActive(false);
        
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

        if (startPosBtn != null) startPosBtn.onClick.AddListener(OnStartPosBtnClicked);
        if (endPosBtn != null) endPosBtn.onClick.AddListener(OnEndPosBtnClicked);

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
        // 处理坐标捕捉
        if (isCapturingStartPos || isCapturingEndPos)
        {
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = mainCamera.WorldToScreenPoint(new Vector3(0, 0, planeZ)).z;
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(mousePos);
            
            if (isCapturingStartPos)
            {
                startPosBtn.GetComponentInChildren<TextMeshProUGUI>().text = $"起始: ({worldPos.x:F2}, {worldPos.y:F2})";
                if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
                {
                    capturedStartPos = new Vector2(worldPos.x, worldPos.y);
                    isCapturingStartPos = false;
                    infoText.text = "已捕捉起始坐标";
                }
            }
            else if (isCapturingEndPos)
            {
                endPosBtn.GetComponentInChildren<TextMeshProUGUI>().text = $"终止: ({worldPos.x:F2}, {worldPos.y:F2})";
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
