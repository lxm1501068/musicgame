using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 皮肤选择UI控制器
/// </summary>
public class SkinSelectorUI : MonoBehaviour
{
    [Header("UI引用")]
    public Dropdown skinDropdown;         // 皮肤选择下拉菜单
    public Button applyButton;            // 应用按钮
    public Text skinInfoText;             // 皮肤信息显示文本
    
    private List<string> skinNames = new List<string>();
    
    void Start()
    {
        InitializeUI();
        SetupEventListeners();
        
        // 加载保存的皮肤偏好
        if (SkinManager.Instance != null)
        {
            SkinManager.Instance.LoadSavedSkinPreference();
        }
    }
    
    /// <summary>
    /// 初始化UI
    /// </summary>
    private void InitializeUI()
    {
        if (SkinManager.Instance == null)
        {
            Debug.LogError("[SkinSelectorUI] SkinManager实例不存在！");
            enabled = false;
            return;
        }
        
        // 填充皮肤名称列表
        skinNames = SkinManager.Instance.GetSkinNames();
        
        // 设置下拉菜单选项
        if (skinDropdown != null)
        {
            skinDropdown.ClearOptions();
            skinDropdown.AddOptions(skinNames);
            
            // 设置当前选中的皮肤
            string currentSkinName = SkinManager.Instance.currentSkin?.skinName;
            int currentIndex = skinNames.IndexOf(currentSkinName);
            if (currentIndex >= 0)
            {
                skinDropdown.value = currentIndex;
            }
        }
        
        UpdateSkinInfo();
    }
    
    /// <summary>
    /// 设置事件监听器
    /// </summary>
    private void SetupEventListeners()
    {
        if (applyButton != null)
        {
            applyButton.onClick.AddListener(ApplySelectedSkin);
        }
        
        if (skinDropdown != null)
        {
            skinDropdown.onValueChanged.AddListener(OnSkinSelectionChanged);
        }
    }
    
    /// <summary>
    /// 当皮肤选择改变时
    /// </summary>
    private void OnSkinSelectionChanged(int index)
    {
        UpdateSkinInfo();
    }
    
    /// <summary>
    /// 应用选中的皮肤
    /// </summary>
    private void ApplySelectedSkin()
    {
        if (skinDropdown != null && skinDropdown.options.Count > 0)
        {
            int selectedIndex = skinDropdown.value;
            if (selectedIndex >= 0 && selectedIndex < skinNames.Count)
            {
                string selectedSkinName = skinNames[selectedIndex];
                SkinManager.Instance.SwitchSkin(selectedSkinName);
                
                Debug.Log($"[SkinSelectorUI] 已应用皮肤: {selectedSkinName}");
                
                // 更新显示信息
                UpdateSkinInfo();
            }
        }
    }
    
    /// <summary>
    /// 更新皮肤信息显示
    /// </summary>
    private void UpdateSkinInfo()
    {
        if (skinInfoText != null && skinDropdown != null && skinDropdown.options.Count > 0)
        {
            int selectedIndex = skinDropdown.value;
            if (selectedIndex >= 0 && selectedIndex < skinNames.Count)
            {
                string selectedSkinName = skinNames[selectedIndex];
                skinInfoText.text = $"当前皮肤: {selectedSkinName}";
            }
        }
    }
}
