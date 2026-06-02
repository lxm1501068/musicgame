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
    public TMP_InputField measureInputField;       // 用于输入小节序数（从0开始）
    public Slider measureSlider;                   // 用于滑动选择小节序数
    public TMP_InputField beatInputField;          // 用于输入小节内的节拍位置
    public Slider beatSlider;                      // 用于滑动选择小节内的节拍位置
    public Button timeConfirmBtn;                  // 时间预览确认按钮

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
    public TMP_Dropdown moveTypeDropdown;          // 运动类型下拉菜单（Harmonic, Parabolic, Circular）

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

    // 时间预览相关
    private bool isTimeChanged = false;      // 标记时间是否已更改但未确认
    private float pendingTime = 0f;          // 待确认的时间值
    private float currentDisplayTime = 0f;   // 当前实际显示的时间

    async void Start()
    {
        // 监听下拉列表变化：用于放置模式类型选择
        noteTypeDropdown.onValueChanged.AddListener(OnDropdownTypeSelected);

        // 监听输入框变化
        if (measureInputField != null) measureInputField.onEndEdit.AddListener(OnMeasureChanged);
        if (measureSlider != null) measureSlider.onValueChanged.AddListener(OnMeasureSliderChanged);
        if (beatInputField != null) beatInputField.onEndEdit.AddListener(OnBeatInputChanged);
        if (beatSlider != null) beatSlider.onValueChanged.AddListener(OnBeatSliderChanged);
        keyIndexInputField.onValueChanged.AddListener(OnKeyIndexChanged);
        keyNameInputField.onValueChanged.AddListener(OnKeyNameChanged);
        noteTypeInputField.onValueChanged.AddListener(OnNoteTypeInputChanged);
        if (existingCmdsDropdown != null) existingCmdsDropdown.onValueChanged.AddListener(OnExistingCmdSelected);
        
        // 初始化时间确认按钮
        if (timeConfirmBtn != null)
        {
            timeConfirmBtn.onClick.AddListener(OnTimeConfirmClicked);
            timeConfirmBtn.gameObject.SetActive(false); // 初始隐藏
        }
        
        // 初始化吸附相关 UI
        InitializeSnapSettings();

        // 初始隐藏专用编辑框
        keyIndexInputField.gameObject.SetActive(false);
        keyNameInputField.gameObject.SetActive(false);
        noteTypeInputField.gameObject.SetActive(false);
        if (changeNoteTypeBtn != null) changeNoteTypeBtn.gameObject.SetActive(false);
        if (finishBtn != null) finishBtn.gameObject.SetActive(false);
        if (moveTypeDropdown != null) moveTypeDropdown.gameObject.SetActive(false);
        
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
        
        // 初始化小节输入框和滑块
        if (measureInputField != null)
        {
            measureInputField.text = "0"; // 默认从第0小节开始
        }
        
        // 初始化小节滑块
        if (measureSlider != null)
        {
            measureSlider.minValue = 0;
            measureSlider.maxValue = Mathf.Max(0, ChartData.Instance.measureCount - 1);
            measureSlider.value = 0;
            
            // 添加监听器，确保在谱面数据改变时更新范围
            measureSlider.onValueChanged.AddListener((value) => {
                int maxMeasure = ChartData.Instance.measureCount - 1;
                if (measureSlider.maxValue != maxMeasure)
                {
                    measureSlider.maxValue = maxMeasure;
                }
            });
        }
        
        // 初始化节拍滑块
        InitializeBeatSlider();

        // ---------- 初始化指令管理 UI ----------
        if (cmdModeToggleBtn != null)
            cmdModeToggleBtn.onClick.AddListener(ToggleCmdMode);

        if (cmdConfirmBtn != null)
            cmdConfirmBtn.onClick.AddListener(OnConfirmCmdAction);

        if (finishBtn != null)
            finishBtn.onClick.AddListener(Deselect);

        if (changeNoteTypeBtn != null)
            changeNoteTypeBtn.onClick.AddListener(OnChangeNoteTypeBtnClicked);

        // 初始化运动类型下拉菜单
        InitializeMoveTypeDropdown();

        if (startPosBtn != null) startPosBtn.onClick.AddListener(OnStartPosBtnClicked);
        if (endPosBtn != null) endPosBtn.onClick.AddListener(OnEndPosBtnClicked);
        if (startPosInputField != null) startPosInputField.onEndEdit.AddListener(OnStartPosInputEndEdit);
        if (endPosInputField != null) endPosInputField.onEndEdit.AddListener(OnEndPosInputEndEdit);

        // 获取要编辑的文件名（如果有）
        string editingFileName = PlayerPrefs.GetString("EditingChartFileName", "");
        
        if (saveChartBtn != null)
        {
            string saveName = string.IsNullOrEmpty(editingFileName) ? "chart.txt" : editingFileName;
            saveChartBtn.onClick.AddListener(() => ExportChartAndReturn(saveName));
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
        
        // 新增：初始化时间显示，将所有对象位置更新到时间 0
        InitializeTimeDisplay();
    }
    
    /// <summary>
    /// 导出谱面并返回上一场景
    /// </summary>
    private void ExportChartAndReturn(string fileName = "chart.txt")
    {
        ExportChart(fileName);
        
        // 保存完成后返回上一场景（通常是CreateTableScene）
        UnityEngine.SceneManagement.SceneManager.LoadScene("CreateTableScene");
    }
    
    /// <summary>
    /// 初始化时间显示，将所有对象位置更新到默认时间
    /// </summary>
    private void InitializeTimeDisplay()
    {
        // 设置默认小节为 0
        if (measureInputField != null)
        {
            measureInputField.text = "0";
        }
        
        // 设置小节滑块
        if (measureSlider != null)
        {
            int maxMeasure = Mathf.Max(0, ChartData.Instance.measureCount - 1);
            measureSlider.minValue = 0;
            measureSlider.maxValue = maxMeasure;
            measureSlider.value = 0;
        }
        
        // 初始化节拍滑块
        InitializeBeatSlider();
        
        // 更新所有对象到时间 0 的位置
        float currentTime = CalculateCurrentTime();
        UpdateObjectsPositionAtTime(currentTime);
        
        // Debug.Log($"InitializeTimeDisplay: 已初始化时间显示，谱面总时长: {ChartData.Instance.totalDuration}s, 小节数: {ChartData.Instance.measureCount}");
    }
    
    /// <summary>
    /// 初始化吸附设置 UI
    /// </summary>
    private void InitializeSnapSettings()
    {
        // 现在吸附逻辑固定为支持 1/4 拍和 1/3 拍（三连音）
    }
    
    /// <summary>
    /// 获取吸附间隔（始终支持 1/4 拍和 1/3 拍的精度）
    /// </summary>
    private float GetSnapInterval()
    {
        // 返回最小的吸附单位，以支持所有可能的节拍位置
        // 1/4 拍 = 0.25
        // 1/3 拍 ≈ 0.333...
        // 为了同时支持两者，我们使用它们的最大公约数概念
        // 实际上，我们会分别尝试两种吸附，选择最接近的
        return 0f; // 返回值不使用，实际逻辑在 SnapBeatPosition 中
    }
    
    /// <summary>
    /// 将节拍位置吸附到最近的 1/4 拍或 1/3 拍
    /// </summary>
    private float SnapBeatPosition(float beatPosition)
    {
        // 计算吸附到 1/4 拍的位置
        float snapToQuarter = Mathf.Round(beatPosition / 0.25f) * 0.25f;
        
        // 计算吸附到 1/3 拍的位置（三连音）
        float snapToThird = Mathf.Round(beatPosition / (1f/3f)) * (1f/3f);
        
        // 选择距离原始位置更近的吸附点
        float distToQuarter = Mathf.Abs(beatPosition - snapToQuarter);
        float distToThird = Mathf.Abs(beatPosition - snapToThird);
        
        return distToQuarter <= distToThird ? snapToQuarter : snapToThird;
    }
    
    /// <summary>
    /// 初始化节拍滑块
    /// </summary>
    private void InitializeBeatSlider()
    {
        if (beatSlider == null) return;
        
        // 获取当前小节的拍数
        int measureIndex = 0;
        if (measureInputField != null)
        {
            int.TryParse(measureInputField.text, out measureIndex);
        }
        
        UpdateBeatSliderRange(measureIndex);
        beatSlider.value = 0;
        
        // 同步更新节拍输入框
        if (beatInputField != null)
        {
            beatInputField.text = "0";
        }
    }
    
    /// <summary>
    /// 更新节拍滑块的范围为当前小节的总拍数
    /// </summary>
    private void UpdateBeatSliderRange(int measureIndex)
    {
        if (beatSlider == null) return;
        
        MeasureData measure;
        if (ChartData.Instance.measures != null && measureIndex >= 0 && measureIndex < ChartData.Instance.measures.Count)
        {
            measure = ChartData.Instance.measures[measureIndex];
        }
        else
        {
            measure = new MeasureData(0, ChartData.Instance.defaultBpm, 
                                    ChartData.Instance.defaultBeatsPerMeasure, 
                                    ChartData.Instance.defaultBeatUnit);
        }
        
        beatSlider.minValue = 0;
        beatSlider.maxValue = measure.beatsPerMeasure;
    }

    /// <summary>
    /// 初始化运动类型下拉菜单
    /// </summary>
    private void InitializeMoveTypeDropdown()
    {
        if (moveTypeDropdown == null) return;
        
        // 清除现有选项并添加运动类型
        moveTypeDropdown.ClearOptions();
        moveTypeDropdown.AddOptions(new List<string> { "Harmonic (简谐运动)", "Parabolic (抛物线)", "Circular (圆周运动)" });
        moveTypeDropdown.value = 0; // 默认选择第一个
        
        // 监听下拉菜单变化
        moveTypeDropdown.onValueChanged.AddListener(OnMoveTypeDropdownChanged);
    }

    /// <summary>
    /// 运动类型下拉菜单变化回调
    /// </summary>
    private void OnMoveTypeDropdownChanged(int index)
    {
        if (currentAddStep != AddStep.InputMoveParams) return;
        
        string[] options = { "harmonic", "parabolic", "circular" };
        if (index >= 0 && index < options.Length)
        {
            OnMoveOptionSelected(options[index]);
        }
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
        
        // Debug.Log($"CreateSceneManager: 跳转到谱面设置场景 | 当前编辑文件：{editingFileName}");
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
        
        // Debug.Log($"CreateSceneManager: 已保存谱面设置 | 总时长：{ChartData.Instance.totalDuration} | KeyIds: [{keyIdsStr}]");
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
                    // 检查音符是否应该可以被选中（未销毁且已到出现时间）
                    if (CanSelectNote(noteComp.command))
                    {
                        SelectNote(noteComp);
                    }
                    else
                    {
                        // 音符已被销毁或还未出现，不选中
                        Deselect();
                    }
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

    /// <summary>
    /// 公开方法：刷新所有对象在指定时间的位置显示
    /// </summary>
    public void RefreshDisplayAtTime(float time)
    {
        // 根据时间计算对应的小节
        int measureIndex = ChartData.Instance.GetMeasureIndexAtTime(time);
        
        // 更新小节输入框
        if (measureInputField != null)
        {
            measureInputField.text = measureIndex.ToString();
        }
        
        // 更新小节滑块
        if (measureSlider != null)
        {
            measureSlider.onValueChanged.RemoveListener(OnMeasureSliderChanged);
            measureSlider.value = measureIndex;
            measureSlider.onValueChanged.AddListener(OnMeasureSliderChanged);
        }
        
        // 计算该小节内的节拍位置
        float measureStartTime = CalculateMeasureStartTime(measureIndex);
        float timeInMeasure = time - measureStartTime;
        
        MeasureData measure;
        if (ChartData.Instance.measures != null && measureIndex >= 0 && measureIndex < ChartData.Instance.measures.Count)
        {
            measure = ChartData.Instance.measures[measureIndex];
        }
        else
        {
            measure = new MeasureData(0, ChartData.Instance.defaultBpm, 
                                    ChartData.Instance.defaultBeatsPerMeasure, 
                                    ChartData.Instance.defaultBeatUnit);
        }
        
        float beatPosition = timeInMeasure * (measure.bpm / 60f);
        
        // 更新节拍输入框
        if (beatInputField != null)
        {
            beatInputField.text = beatPosition.ToString("F2", CultureInfo.InvariantCulture);
        }
        
        // 更新节拍滑块
        if (beatSlider != null)
        {
            beatSlider.onValueChanged.RemoveListener(OnBeatSliderChanged);
            beatSlider.value = beatPosition;
            beatSlider.onValueChanged.AddListener(OnBeatSliderChanged);
        }
        
        // 更新所有对象位置
        UpdateObjectsPositionAtTime(time);
    }

    void OnDestroy()
    {
        if (followObject != null) Destroy(followObject);
    }
}