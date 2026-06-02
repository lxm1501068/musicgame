using UnityEngine;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 音符皮肤管理器（单例）
/// </summary>
public class SkinManager : MonoBehaviour
{
    public static SkinManager Instance { get; private set; }
    
    [Header("当前使用的皮肤")]
    public NoteSkin currentSkin;
    
    [Header("可用皮肤列表")]
    public List<NoteSkin> availableSkins = new List<NoteSkin>();
    
    [Header("皮肤配置")]
    public string skinsFolderName = "Skins";
    public string defaultSkinName = "Default";
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        LoadAllSkins();
    }
    
    /// <summary>
    /// 加载所有皮肤
    /// </summary>
    public void LoadAllSkins()
    {
        availableSkins.Clear();
        
        // 从Resources文件夹加载皮肤
        LoadSkinsFromResources();
        
        // 设置默认皮肤
        SetDefaultSkin();
        
        Debug.Log($"[SkinManager] 已加载 {availableSkins.Count} 个皮肤");
    }
    
    /// <summary>
    /// 从Resources文件夹加载皮肤
    /// </summary>
    private void LoadSkinsFromResources()
    {
        // 尝试从Resources/Skins文件夹加载所有NoteSkin资源
        NoteSkin[] skins = Resources.LoadAll<NoteSkin>(skinsFolderName);
        
        if (skins != null && skins.Length > 0)
        {
            foreach (var skin in skins)
            {
                if (skin != null)
                {
                    availableSkins.Add(skin);
                    Debug.Log($"[SkinManager] 加载皮肤: {skin.skinName}");
                }
            }
        }
        else
        {
            Debug.LogWarning($"[SkinManager] 未在 Resources/{skinsFolderName} 中找到皮肤资源，创建默认皮肤");
            CreateDefaultSkin();
        }
    }
    
    /// <summary>
    /// 创建默认皮肤
    /// </summary>
    private void CreateDefaultSkin()
    {
        NoteSkin defaultSkin = ScriptableObject.CreateInstance<NoteSkin>();
        defaultSkin.skinName = defaultSkinName;
        availableSkins.Add(defaultSkin);
    }
    
    /// <summary>
    /// 设置默认皮肤
    /// </summary>
    private void SetDefaultSkin()
    {
        if (availableSkins.Count > 0)
        {
            // 优先使用配置的默认皮肤名称
            foreach (var skin in availableSkins)
            {
                if (skin.skinName == defaultSkinName)
                {
                    currentSkin = skin;
                    Debug.Log($"[SkinManager] 设置默认皮肤: {currentSkin.skinName}");
                    return;
                }
            }
            
            // 如果没有找到指定名称的默认皮肤，使用第一个
            currentSkin = availableSkins[0];
            Debug.Log($"[SkinManager] 使用第一个皮肤作为默认: {currentSkin.skinName}");
        }
    }
    
    /// <summary>
    /// 切换皮肤（通过索引）
    /// </summary>
    public void SwitchSkin(int index)
    {
        if (index >= 0 && index < availableSkins.Count)
        {
            currentSkin = availableSkins[index];
            Debug.Log($"[SkinManager] 切换到皮肤: {currentSkin.skinName}");
            
            // 保存当前皮肤选择
            SaveCurrentSkinPreference();
        }
        else
        {
            Debug.LogWarning($"[SkinManager] 皮肤索引 {index} 超出范围");
        }
    }
    
    /// <summary>
    /// 切换皮肤（通过名称）
    /// </summary>
    public void SwitchSkin(string skinName)
    {
        foreach (var skin in availableSkins)
        {
            if (skin.skinName == skinName)
            {
                currentSkin = skin;
                Debug.Log($"[SkinManager] 切换到皮肤: {currentSkin.skinName}");
                
                // 保存当前皮肤选择
                SaveCurrentSkinPreference();
                return;
            }
        }
        
        Debug.LogWarning($"[SkinManager] 未找到名为 '{skinName}' 的皮肤");
    }
    


    /// <summary>
    /// 保存当前皮肤偏好
    /// </summary>
    private void SaveCurrentSkinPreference()
    {
        if (currentSkin != null)
        {
            PlayerPrefs.SetString("CurrentSkin", currentSkin.skinName);
            PlayerPrefs.Save();
            Debug.Log($"[SkinManager] 已保存皮肤偏好: {currentSkin.skinName}");
        }
    }
    
    /// <summary>
    /// 加载保存的皮肤偏好
    /// </summary>
    public void LoadSavedSkinPreference()
    {
        string savedSkinName = PlayerPrefs.GetString("CurrentSkin", "");
        if (!string.IsNullOrEmpty(savedSkinName))
        {
            SwitchSkin(savedSkinName);
        }
    }
    
    /// <summary>
    /// 获取皮肤数量
    /// </summary>
    public int GetSkinCount()
    {
        return availableSkins.Count;
    }
    
    /// <summary>
    /// 获取皮肤名称列表
    /// </summary>
    public List<string> GetSkinNames()
    {
        List<string> names = new List<string>();
        foreach (var skin in availableSkins)
        {
            names.Add(skin.skinName);
        }
        return names;
    }
}
