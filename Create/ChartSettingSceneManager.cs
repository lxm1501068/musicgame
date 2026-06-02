using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;

/// <summary>
/// 谱面设置场景管理器
/// 负责管理谱面配置（总时长、轨道按键ID 等）的编辑
/// </summary>
public class ChartSettingSceneManager : MonoBehaviour
{
    [Header("谱面设置 UI")]
    public TMP_InputField totalDurationInput;      // 总时长输入框（只读，自动计算）
    public TMP_InputField keyIdsInput;             // KeyIds 输入框（逗号分隔整数）
    public Button backButton;                      // 返回创建场景按钮（自动保存）
        
    [Header("小节与节拍设置")]
    public TMP_InputField measureCountInput;       // 小节数量输入框
    public TMP_Dropdown sectionDropdown;           // 节奏段落选择下拉框
    public TMP_InputField sectionRangeInput;       // 变奏区间输入框（格式：51-100）
    public Button addSectionBtn;                   // 添加变奏段落按钮
    public Button removeSectionBtn;                // 删除当前段落按钮
    public TMP_InputField sectionBpmInput;         // 当前段落的 BPM 输入框
    public TMP_InputField sectionTimeSignatureInput; // 当前段落的拍号输入框

    private float originalDuration;                // 保存原始时长（用于取消时恢复）
    private List<int> originalKeyIds;              // 保存原始 KeyIds（用于取消时恢复）
    private string editingChartFileName;           // 当前编辑的谱面文件名
    private int originalMeasureCount;              // 保存原始小节数量
    private float originalDefaultBpm;              // 保存原始默认 BPM
    private int originalDefaultBeatsPerMeasure;    // 保存原始默认拍号分子
    private int originalDefaultBeatUnit;           // 保存原始默认拍号分母
    
    // 变奏段落管理
    private List<SectionData> sections = new List<SectionData>(); // 节奏段落列表
    
    [System.Serializable]
    public class SectionData
    {
        public int startMeasure;   // 起始小节
        public int endMeasure;     // 结束小节
        public float bpm;          // BPM
        public int beatsPerMeasure; // 拍号分子
        public int beatUnit;       // 拍号分母
        
        public SectionData(int start, int end, float bpm, int beatsPerMeasure, int beatUnit)
        {
            this.startMeasure = start;
            this.endMeasure = end;
            this.bpm = bpm;
            this.beatsPerMeasure = beatsPerMeasure;
            this.beatUnit = beatUnit;
        }
        
        public string GetDisplayName()
        {
            return $"{startMeasure}-{endMeasure}";
        }
    }

    void Start()
    {
        // 获取当前编辑的谱面文件名
        editingChartFileName = PlayerPrefs.GetString("EditingChartFileName", "");
        
        // 初始化按钮监听
        InitializeButtons();
        
        // 初始化输入框事件
        InitializeInputFields();
        
        // 从 PlayerPrefs 或 ChartData 加载当前谱面设置到 UI
        LoadChartSettingsToUI();
    }

    /// <summary>
    /// 初始化所有按钮的点击事件
    /// </summary>
    private void InitializeButtons()
    {
        SetupButton(backButton, OnBackToCreate);
        
        // 变奏段落管理按钮
        if (addSectionBtn != null)
        {
            addSectionBtn.onClick.AddListener(OnAddSectionClicked);
        }
        
        if (removeSectionBtn != null)
        {
            removeSectionBtn.onClick.AddListener(OnRemoveSectionClicked);
        }
        
        // 段落选择下拉框
        if (sectionDropdown != null)
        {
            sectionDropdown.onValueChanged.AddListener(OnSectionDropdownChanged);
        }
    }
    
    /// <summary>
    /// 初始化输入框的事件监听
    /// </summary>
    private void InitializeInputFields()
    {
        // 小节数量输入框
        if (measureCountInput != null)
        {
            measureCountInput.onEndEdit.AddListener(OnMeasureCountChanged);
        }
        
        // 变奏区间输入框
        if (sectionRangeInput != null)
        {
            sectionRangeInput.onEndEdit.AddListener(OnSectionRangeChanged);
        }
        
        // 段落 BPM 输入框
        if (sectionBpmInput != null)
        {
            sectionBpmInput.onEndEdit.AddListener(OnSectionBpmChanged);
        }
        
        // 段落拍号输入框
        if (sectionTimeSignatureInput != null)
        {
            sectionTimeSignatureInput.onEndEdit.AddListener(OnSectionTimeSignatureChanged);
        }
    }

    /// <summary>
    /// 设置按钮点击事件
    /// </summary>
    private void SetupButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }

    /// <summary>
    /// 从 PlayerPrefs 或 ChartData 加载当前值到 UI 输入框
    /// </summary>
    private void LoadChartSettingsToUI()
    {
        // 加载数据
        LoadChartData();
        
        // 更新 UI 显示
        UpdateUIDisplay();
        
        Debug.Log($"ChartSettingSceneManager: 加载设置 | 总时长：{originalDuration} | KeyIds: [{string.Join(",", originalKeyIds)}]");
    }

    /// <summary>
    /// 从 PlayerPrefs 或 ChartData 加载数据
    /// </summary>
    private void LoadChartData()
    {
        bool hasPlayerPrefs = PlayerPrefs.HasKey("Chart_TotalDuration");
        
        if (hasPlayerPrefs)
        {
            originalDuration = PlayerPrefs.GetFloat("Chart_TotalDuration");
            originalKeyIds = ParseKeyIdsString(PlayerPrefs.GetString("Chart_KeyIds", ""));
            
            // 加载默认 BPM 和拍号
            if (PlayerPrefs.HasKey("Chart_DefaultBpm"))
            {
                ChartData.Instance.defaultBpm = PlayerPrefs.GetFloat("Chart_DefaultBpm");
            }
            if (PlayerPrefs.HasKey("Chart_DefaultBeatsPerMeasure"))
            {
                ChartData.Instance.defaultBeatsPerMeasure = PlayerPrefs.GetInt("Chart_DefaultBeatsPerMeasure");
            }
            if (PlayerPrefs.HasKey("Chart_DefaultBeatUnit"))
            {
                ChartData.Instance.defaultBeatUnit = PlayerPrefs.GetInt("Chart_DefaultBeatUnit");
            }
            
            // 同步到 ChartData
            ChartData.Instance.totalDuration = originalDuration;
            ChartData.Instance.keyIds = new List<int>(originalKeyIds);
        }
        else
        {
            originalDuration = ChartData.Instance.totalDuration;
            originalKeyIds = new List<int>(ChartData.Instance.keyIds);
        }
        
        // 保存原始值用于比较
        originalDefaultBpm = ChartData.Instance.defaultBpm;
        originalDefaultBeatsPerMeasure = ChartData.Instance.defaultBeatsPerMeasure;
        originalDefaultBeatUnit = ChartData.Instance.defaultBeatUnit;
    }

    /// <summary>
    /// 更新 UI 显示
    /// </summary>
    private void UpdateUIDisplay()
    {
        // 总时长（只读，由小节数据自动计算）
        if (totalDurationInput != null)
        {
            totalDurationInput.text = ChartData.Instance.totalDuration.ToString("F3", CultureInfo.InvariantCulture);
            totalDurationInput.interactable = false; // 设为只读
        }

        // 轨道按键 ID
        if (keyIdsInput != null)
        {
            keyIdsInput.text = string.Join(",", originalKeyIds);
        }
        
        // 小节数量
        if (measureCountInput != null)
        {
            originalMeasureCount = ChartData.Instance.measureCount > 0 ? ChartData.Instance.measureCount : 1;
            measureCountInput.text = originalMeasureCount.ToString();
        }
        
        // 初始化或更新段落列表
        InitializeSections();
        
        // 更新段落下拉框
        UpdateSectionDropdown();
    }

    /// <summary>
    /// 解析 KeyIds 字符串为整数列表
    /// </summary>
    private List<int> ParseKeyIdsString(string keyIdsStr)
    {
        List<int> result = new List<int>();
        
        if (string.IsNullOrEmpty(keyIdsStr))
            return result;
        
        string[] parts = keyIdsStr.Split(',');
        foreach (string part in parts)
        {
            if (int.TryParse(part.Trim(), out int id))
            {
                result.Add(id);
            }
        }
        
        return result;
    }

    /// <summary>
    /// 保存设置到 ChartData 和 PlayerPrefs
    /// </summary>
    private bool SaveSettings()
    {
        bool durationValid = ValidateAndSaveDuration(out _);
        bool keyIdsValid = ValidateAndSaveKeyIds();
        
        if (durationValid && keyIdsValid)
        {
            // 保存所有段落到小节数据
            ApplyAllSectionsToMeasures();
            RecalculateTotalDuration();
            
            PlayerPrefs.Save();
            Debug.Log($"ChartSettingSceneManager: 谱面设置已保存 | 总时长：{ChartData.Instance.totalDuration} | KeyIds: [{string.Join(",", ChartData.Instance.keyIds)}] | 段落数: {sections.Count}");
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// 获取错误信息
    /// </summary>
    private string GetErrorMessage()
    {
        string errorMsg = "";
        
        // 检查 KeyIds
        if (keyIdsInput != null)
        {
            string input = keyIdsInput.text.Trim();
            if (!string.IsNullOrEmpty(input))
            {
                string[] parts = input.Split(',');
                foreach (string part in parts)
                {
                    string trimmed = part.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    
                    if (!int.TryParse(trimmed, out _))
                    {
                        errorMsg += $"无效的按键ID: {trimmed}；";
                    }
                }
            }
        }
        
        return errorMsg;
    }

    /// <summary>
    /// 验证并保存总时长（只读，由小节数据自动计算）
    /// </summary>
    private bool ValidateAndSaveDuration(out float duration)
    {
        duration = ChartData.Instance.totalDuration;
        return true; // 总时长由小节数据自动计算，无需验证
    }

    /// <summary>
    /// 验证并保存 KeyIds
    /// </summary>
    private bool ValidateAndSaveKeyIds()
    {
        if (keyIdsInput == null) return true;
        
        List<int> newKeyIds = ParseKeyIdsString(keyIdsInput.text.Trim());
        
        // 验证是否有无效的 ID
        string input = keyIdsInput.text.Trim();
        if (!string.IsNullOrEmpty(input))
        {
            string[] parts = input.Split(',');
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                
                if (!int.TryParse(trimmed, out _))
                {
                    return false;
                }
            }
        }
        
        // 去重 + 排序
        newKeyIds = newKeyIds.Distinct().OrderBy(x => x).ToList();
        ChartData.Instance.keyIds = newKeyIds;
        PlayerPrefs.SetString("Chart_KeyIds", string.Join(",", newKeyIds));
        
        return true;
    }
    
    /// <summary>
    /// 返回创建场景（自动保存设置）
    /// </summary>
    private void OnBackToCreate()
    {
        // 验证并保存当前设置
        if (SaveSettings())
        {
            Debug.Log("ChartSettingSceneManager: 设置已保存，返回创建场景");
        }
        else
        {
            // 如果有错误，显示提示但仍返回
            string errorMsg = GetErrorMessage();
            Debug.LogWarning($"ChartSettingSceneManager: 设置有误 - {errorMsg}");
        }
        
        // 加载创建场景
        UnityEngine.SceneManagement.SceneManager.LoadScene("CreateScene");
    }

    /// <summary>
    /// 外部调用：刷新 UI 显示（当 ChartData 被外部修改时）
    /// </summary>
    public void RefreshUI()
    {
        LoadChartSettingsToUI();
    }
    
    // ========== 小节与时间相关方法 ==========
    
    /// <summary>
    /// 小节数量变化回调
    /// </summary>
    private void OnMeasureCountChanged(string value)
    {
        if (int.TryParse(value, out int count) && count > 0)
        {
            // 如果小节列表为空或数量不匹配，重新生成
            if (ChartData.Instance.measures.Count != count)
            {
                GenerateMeasures(count);
            }
            
            // 重新计算总时长
            RecalculateTotalDuration();
            
            // 更新 UI 显示
            if (totalDurationInput != null)
            {
                totalDurationInput.text = ChartData.Instance.totalDuration.ToString("F3", CultureInfo.InvariantCulture);
            }
            
            Debug.Log($"已设置 {count} 个小节，总时长：{ChartData.Instance.totalDuration:F3}秒");
        }
        else
        {
            Debug.LogWarning("无效的小节数量");
        }
    }
    
    /// <summary>
    /// 生成小节数据列表
    /// </summary>
    private void GenerateMeasures(int count)
    {
        ChartData.Instance.measures.Clear();
        
        // 使用第一个段落的设置，如果没有段落则使用默认值
        float bpm = sections.Count > 0 ? sections[0].bpm : ChartData.Instance.defaultBpm;
        int beatsPerMeasure = sections.Count > 0 ? sections[0].beatsPerMeasure : ChartData.Instance.defaultBeatsPerMeasure;
        int beatUnit = sections.Count > 0 ? sections[0].beatUnit : ChartData.Instance.defaultBeatUnit;
        
        for (int i = 0; i < count; i++)
        {
            var measure = new MeasureData(i, bpm, beatsPerMeasure, beatUnit);
            ChartData.Instance.measures.Add(measure);
        }
        
        // 应用所有段落到小节
        ApplyAllSectionsToMeasures();
        
        Debug.Log($"已生成 {count} 个小节数据");
    }
    
    /// <summary>
    /// 根据小节数据重新计算总时长
    /// </summary>
    private void RecalculateTotalDuration()
    {
        float totalDuration = 0f;
        foreach (var measure in ChartData.Instance.measures)
        {
            totalDuration += ChartData.Instance.CalculateMeasureDuration(measure);
        }
        
        ChartData.Instance.totalDuration = totalDuration;
        PlayerPrefs.SetFloat("Chart_TotalDuration", totalDuration);
    }
    
    // ========== 变奏段落管理方法 ==========
    
    /// <summary>
    /// 初始化段落列表
    /// </summary>
    private void InitializeSections()
    {
        sections.Clear();
        
        int totalMeasures = ChartData.Instance.measureCount > 0 ? ChartData.Instance.measureCount : 1;
        
        // 从 ChartData 的小节数据中提取段落信息
        if (ChartData.Instance.measures != null && ChartData.Instance.measures.Count > 0)
        {
            int currentStartMeasure = 0;
            float currentBpm = ChartData.Instance.measures[0].bpm;
            int currentBeatsPerMeasure = ChartData.Instance.measures[0].beatsPerMeasure;
            int currentBeatUnit = ChartData.Instance.measures[0].beatUnit;
            
            for (int i = 1; i <= ChartData.Instance.measures.Count; i++)
            {
                bool isLastMeasure = (i == ChartData.Instance.measures.Count);
                bool hasChanged = false;
                
                if (!isLastMeasure)
                {
                    var measure = ChartData.Instance.measures[i];
                    hasChanged = (measure.bpm != currentBpm || 
                                 measure.beatsPerMeasure != currentBeatsPerMeasure || 
                                 measure.beatUnit != currentBeatUnit);
                }
                
                if (hasChanged || isLastMeasure)
                {
                    // 创建一个新段落
                    int endMeasure = isLastMeasure ? i - 1 : i - 1;
                    sections.Add(new SectionData(currentStartMeasure, endMeasure, currentBpm, currentBeatsPerMeasure, currentBeatUnit));
                    
                    if (!isLastMeasure)
                    {
                        // 开始新的段落
                        currentStartMeasure = i;
                        currentBpm = ChartData.Instance.measures[i].bpm;
                        currentBeatsPerMeasure = ChartData.Instance.measures[i].beatsPerMeasure;
                        currentBeatUnit = ChartData.Instance.measures[i].beatUnit;
                    }
                }
            }
        }
        else
        {
            // 如果没有小节数据，创建一个默认段落
            sections.Add(new SectionData(0, totalMeasures - 1, 
                                        ChartData.Instance.defaultBpm,
                                        ChartData.Instance.defaultBeatsPerMeasure,
                                        ChartData.Instance.defaultBeatUnit));
        }
    }
    
    /// <summary>
    /// 更新段落下拉框显示
    /// </summary>
    private void UpdateSectionDropdown()
    {
        if (sectionDropdown == null) return;
        
        sectionDropdown.options.Clear();
        
        foreach (var section in sections)
        {
            sectionDropdown.options.Add(new TMP_Dropdown.OptionData(section.GetDisplayName()));
        }
        
        sectionDropdown.value = 0;
        sectionDropdown.RefreshShownValue();
        
        // 更新当前选中段落的输入框
        UpdateCurrentSectionInputs();
    }
    
    /// <summary>
    /// 更新当前选中段落的输入框
    /// </summary>
    private void UpdateCurrentSectionInputs()
    {
        if (sections.Count == 0) return;
        
        int selectedIndex = sectionDropdown != null ? sectionDropdown.value : 0;
        if (selectedIndex >= 0 && selectedIndex < sections.Count)
        {
            var section = sections[selectedIndex];
            
            // 更新区间输入框
            if (sectionRangeInput != null)
            {
                sectionRangeInput.text = $"{section.startMeasure}-{section.endMeasure}";
            }
            
            // 更新 BPM 输入框
            if (sectionBpmInput != null)
            {
                sectionBpmInput.text = section.bpm.ToString("F2", CultureInfo.InvariantCulture);
            }
            
            // 更新拍号输入框
            if (sectionTimeSignatureInput != null)
            {
                sectionTimeSignatureInput.text = $"{section.beatsPerMeasure}/{section.beatUnit}";
            }
        }
    }
    
    /// <summary>
    /// 段落下拉框变化回调
    /// </summary>
    private void OnSectionDropdownChanged(int index)
    {
        UpdateCurrentSectionInputs();
    }
    
    /// <summary>
    /// 变奏区间输入框变化回调
    /// </summary>
    private void OnSectionRangeChanged(string value)
    {
        // 解析格式：51-100
        string[] parts = value.Split('-');
        if (parts.Length == 2 && 
            int.TryParse(parts[0].Trim(), out int startMeasure) && 
            int.TryParse(parts[1].Trim(), out int endMeasure) &&
            startMeasure >= 0 && endMeasure >= startMeasure)
        {
            // 检查是否超出范围
            int totalMeasures = ChartData.Instance.measureCount;
            if (endMeasure >= totalMeasures)
            {
                Debug.LogWarning($"结束小节 {endMeasure} 超出总小节数 {totalMeasures}");
                return;
            }
            
            // 查找这个区间属于哪个段落
            int sectionIndex = -1;
            for (int i = 0; i < sections.Count; i++)
            {
                if (startMeasure >= sections[i].startMeasure && startMeasure <= sections[i].endMeasure)
                {
                    sectionIndex = i;
                    break;
                }
            }
            
            if (sectionIndex >= 0)
            {
                // 分割该段落
                SplitSection(sectionIndex, startMeasure);
                
                // 更新 UI
                UpdateSectionDropdown();
                
                // 选中新创建的段落
                for (int i = 0; i < sections.Count; i++)
                {
                    if (sections[i].startMeasure == startMeasure)
                    {
                        if (sectionDropdown != null)
                        {
                            sectionDropdown.value = i;
                        }
                        break;
                    }
                }
                
                Debug.Log($"已创建新段落: {startMeasure}-{endMeasure}");
            }
        }
        else
        {
            Debug.LogWarning("无效的区间格式（例如：51-100）");
        }
    }
    
    /// <summary>
    /// 段落 BPM 输入框变化回调
    /// </summary>
    private void OnSectionBpmChanged(string value)
    {
        if (sections.Count == 0) return;
        
        int selectedIndex = sectionDropdown != null ? sectionDropdown.value : 0;
        if (selectedIndex >= 0 && selectedIndex < sections.Count)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float bpm) && bpm > 0)
            {
                sections[selectedIndex].bpm = bpm;
                ApplySectionToMeasures(selectedIndex);
                RecalculateTotalDuration();
                
                // 更新总时长显示
                if (totalDurationInput != null)
                {
                    totalDurationInput.text = ChartData.Instance.totalDuration.ToString("F3", CultureInfo.InvariantCulture);
                }
                
                Debug.Log($"已更新段落 {sections[selectedIndex].GetDisplayName()} 的 BPM: {bpm:F2}");
            }
        }
    }
    
    /// <summary>
    /// 段落拍号输入框变化回调
    /// </summary>
    private void OnSectionTimeSignatureChanged(string value)
    {
        if (sections.Count == 0) return;
        
        int selectedIndex = sectionDropdown != null ? sectionDropdown.value : 0;
        if (selectedIndex >= 0 && selectedIndex < sections.Count)
        {
            string[] parts = value.Split('/');
            if (parts.Length == 2 && 
                int.TryParse(parts[0].Trim(), out int beatsPerMeasure) && 
                int.TryParse(parts[1].Trim(), out int beatUnit) &&
                beatsPerMeasure > 0 && beatUnit > 0)
            {
                sections[selectedIndex].beatsPerMeasure = beatsPerMeasure;
                sections[selectedIndex].beatUnit = beatUnit;
                ApplySectionToMeasures(selectedIndex);
                RecalculateTotalDuration();
                
                // 更新总时长显示
                if (totalDurationInput != null)
                {
                    totalDurationInput.text = ChartData.Instance.totalDuration.ToString("F3", CultureInfo.InvariantCulture);
                }
                
                Debug.Log($"已更新段落 {sections[selectedIndex].GetDisplayName()} 的拍号: {beatsPerMeasure}/{beatUnit}");
            }
        }
    }
    
    /// <summary>
    /// 添加变奏段落按钮点击回调
    /// </summary>
    private void OnAddSectionClicked()
    {
        if (sections.Count == 0) return;
        
        // 从区间输入框获取起始小节
        int newStartMeasure = -1;
        if (sectionRangeInput != null)
        {
            string[] parts = sectionRangeInput.text.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int startMeasure))
            {
                newStartMeasure = startMeasure;
            }
        }
        
        // 如果没有输入，使用最后一个段落的下一个小节
        if (newStartMeasure == -1)
        {
            int lastEndMeasure = sections[sections.Count - 1].endMeasure;
            int totalMeasures = ChartData.Instance.measureCount;
            
            if (lastEndMeasure >= totalMeasures - 1)
            {
                Debug.LogWarning("无法添加更多段落，已达到最大小节数");
                return;
            }
            
            newStartMeasure = lastEndMeasure + 1;
        }
        
        // 检查是否超出范围
        int totalMeasures2 = ChartData.Instance.measureCount;
        if (newStartMeasure >= totalMeasures2 || newStartMeasure < 0)
        {
            Debug.LogWarning($"起始小节 {newStartMeasure} 无效");
            return;
        }
        
        // 查找这个位置属于哪个段落
        int sectionIndex = -1;
        for (int i = 0; i < sections.Count; i++)
        {
            if (newStartMeasure >= sections[i].startMeasure && newStartMeasure <= sections[i].endMeasure)
            {
                sectionIndex = i;
                break;
            }
        }
        
        if (sectionIndex >= 0)
        {
            // 分割该段落
            SplitSection(sectionIndex, newStartMeasure);
            
            Debug.Log($"已添加新段落，起始小节: {newStartMeasure}");
        }
        else
        {
            Debug.LogWarning($"无法在位置 {newStartMeasure} 创建段落");
        }
    }
    
    /// <summary>
    /// 删除当前段落按钮点击回调
    /// </summary>
    private void OnRemoveSectionClicked()
    {
        if (sections.Count <= 1)
        {
            Debug.LogWarning("至少需要保留一个段落");
            return;
        }
        
        int selectedIndex = sectionDropdown != null ? sectionDropdown.value : sections.Count - 1;
        
        // 合并当前段落与前一个或后一个段落
        MergeSection(selectedIndex);
        
        Debug.Log($"已删除段落 {selectedIndex}");
    }
    
    /// <summary>
    /// 分割段落
    /// </summary>
    private void SplitSection(int sectionIndex, int splitMeasure)
    {
        if (sectionIndex < 0 || sectionIndex >= sections.Count) return;
        
        var originalSection = sections[sectionIndex];
        
        // 创建两个新段落
        var firstSection = new SectionData(
            originalSection.startMeasure,
            splitMeasure - 1,
            originalSection.bpm,
            originalSection.beatsPerMeasure,
            originalSection.beatUnit
        );
        
        var secondSection = new SectionData(
            splitMeasure,
            originalSection.endMeasure,
            originalSection.bpm,
            originalSection.beatsPerMeasure,
            originalSection.beatUnit
        );
        
        // 替换原段落
        sections.RemoveAt(sectionIndex);
        sections.Insert(sectionIndex, secondSection);
        sections.Insert(sectionIndex, firstSection);
        
        // 应用更改到小节数据
        ApplyAllSectionsToMeasures();
        RecalculateTotalDuration();
        
        // 更新 UI
        UpdateSectionDropdown();
        
        // 选中第二个段落
        if (sectionDropdown != null)
        {
            sectionDropdown.value = sectionIndex + 1;
        }
    }
    
    /// <summary>
    /// 合并段落
    /// </summary>
    private void MergeSection(int sectionIndex)
    {
        if (sectionIndex < 0 || sectionIndex >= sections.Count) return;
        if (sections.Count <= 1) return;
        
        // 与前一个段落合并，如果是第一个则与后一个合并
        int mergeWithIndex = sectionIndex > 0 ? sectionIndex - 1 : sectionIndex + 1;
        
        var section1 = sections[Mathf.Min(sectionIndex, mergeWithIndex)];
        var section2 = sections[Mathf.Max(sectionIndex, mergeWithIndex)];
        
        // 创建合并后的段落（使用前一个段落的设置）
        var mergedSection = new SectionData(
            section1.startMeasure,
            section2.endMeasure,
            section1.bpm,
            section1.beatsPerMeasure,
            section1.beatUnit
        );
        
        // 移除两个原段落，插入合并后的段落
        int removeIndex1 = Mathf.Max(sectionIndex, mergeWithIndex);
        int removeIndex2 = Mathf.Min(sectionIndex, mergeWithIndex);
        
        sections.RemoveAt(removeIndex1);
        sections.RemoveAt(removeIndex2);
        sections.Insert(removeIndex2, mergedSection);
        
        // 应用更改到小节数据
        ApplyAllSectionsToMeasures();
        RecalculateTotalDuration();
        
        // 更新 UI
        UpdateSectionDropdown();
    }
    
    /// <summary>
    /// 将段落设置应用到小节数据
    /// </summary>
    private void ApplySectionToMeasures(int sectionIndex)
    {
        if (sectionIndex < 0 || sectionIndex >= sections.Count) return;
        
        var section = sections[sectionIndex];
        
        for (int i = section.startMeasure; i <= section.endMeasure && i < ChartData.Instance.measures.Count; i++)
        {
            ChartData.Instance.measures[i].bpm = section.bpm;
            ChartData.Instance.measures[i].beatsPerMeasure = section.beatsPerMeasure;
            ChartData.Instance.measures[i].beatUnit = section.beatUnit;
        }
    }
    
    /// <summary>
    /// 将所有段落设置应用到小节数据
    /// </summary>
    private void ApplyAllSectionsToMeasures()
    {
        for (int i = 0; i < sections.Count; i++)
        {
            ApplySectionToMeasures(i);
        }
    }
}