using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine.SceneManagement; // 添加场景管理命名空间

public class SettingManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject groupItemPrefab;          // 组条目预制体（包含Button和Text）
    public Transform groupsParent;               // 放置组条目的父对象
    public Button finishButton;                  // 完成按钮

    // 按键名称到KeyCode的映射（用于验证按键有效性）
    private Dictionary<string, KeyCode> keyNameToKeyCode = new Dictionary<string, KeyCode>();

    private int selectedGroupIndex = -1;          // 当前选中的组索引（-1表示未选中）
    private List<GroupUI> groupUIs = new List<GroupUI>();

    // JSON 配置文件名（位于 persistentDataPath 目录下）
    private const string ConfigFileName = "input.json";

    // 硬编码的组位置数组（在Awake中初始化）
    private Vector3[] groupPositions;

    void Awake()
    {
        InitializeKeyMappings();          // 初始化按键映射表
        InitializeGroupPositions();       // 初始化组位置数组（硬编码）
        LoadInitialConfig();               // 从文件加载现有配置，若无则使用默认
    }

    void Start()
    {
        finishButton.onClick.AddListener(OnFinish);
    }

    /// <summary>
    /// 初始化按键名称到KeyCode的映射表（与InputManager保持一致）
    /// </summary>
    void InitializeKeyMappings()
    {
        // 数字键 0-9
        for (int i = 0; i <= 9; i++)
        {
            string keyName = i.ToString();
            keyNameToKeyCode[keyName] = (KeyCode)Enum.Parse(typeof(KeyCode), "Alpha" + i);
        }
        // 字母键 a-z
        for (char c = 'a'; c <= 'z'; c++)
        {
            string keyName = c.ToString();
            KeyCode keyCode = (KeyCode)Enum.Parse(typeof(KeyCode), keyName.ToUpper());
            keyNameToKeyCode[keyName] = keyCode;
        }
        // 特殊键
        keyNameToKeyCode["space"] = KeyCode.Space;
        keyNameToKeyCode[","] = KeyCode.Comma;
        keyNameToKeyCode["."] = KeyCode.Period;
        keyNameToKeyCode[";"] = KeyCode.Semicolon;
        keyNameToKeyCode["/"] = KeyCode.Slash;
    }

    /// <summary>
    /// 硬编码组位置数组（可根据实际场景需求调整坐标）
    /// </summary>
    void InitializeGroupPositions()
    {
        groupPositions = new Vector3[]
        {
            new Vector3(-200, 100, 0),   // 组0
            new Vector3(0, 100, 0),       // 组1
            new Vector3(200, 100, 0),     // 组2
            new Vector3(-200, 0, 0),      // 组3
            new Vector3(0, 0, 0),         // 组4
            new Vector3(200, 0, 0),       // 组5
            new Vector3(-200, -100, 0),   // 组6
            new Vector3(0, -100, 0),      // 组7
            new Vector3(200, -100, 0),    // 组8
            new Vector3(-200, -200, 0),   // 组9
            new Vector3(0, -200, 0)       // 组10
        };
    }

    /// <summary>
    /// 从 persistentDataPath/input.json 加载配置，若失败则使用默认配置
    /// </summary>
    void LoadInitialConfig()
    {
        List<List<string>> loadedGroups = LoadKeyGroupsFromJson();
        if (loadedGroups == null || loadedGroups.Count == 0)
        {
            Debug.LogWarning("无法加载配置文件，将使用默认按键组配置。");
            loadedGroups = GetDefaultKeyGroups();
        }

        // 创建UI
        for (int i = 0; i < loadedGroups.Count; i++)
        {
            int groupNumber = i + 1;
            CreateGroupUI(i, loadedGroups[i]);
        }
    }

    /// <summary>
    /// 从 persistentDataPath 读取 JSON 配置文件
    /// </summary>
    List<List<string>> LoadKeyGroupsFromJson()
    {
        string filePath = Path.Combine(Application.persistentDataPath, ConfigFileName);
        if (!File.Exists(filePath))
        {
            Debug.Log($"配置文件不存在: {filePath}，将使用默认配置。");
            return null;
        }

        try
        {
            string jsonText = File.ReadAllText(filePath);
            InputConfig config = JsonUtility.FromJson<InputConfig>(jsonText);
            if (config == null || config.groups == null || config.groups.Length == 0)
            {
                Debug.LogError("JSON 配置文件格式错误或没有定义任何按键组。");
                return null;
            }

            List<List<string>> groups = new List<List<string>>();
            foreach (var group in config.groups)
            {
                if (group.keys != null && group.keys.Length > 0)
                {
                    groups.Add(new List<string>(group.keys));
                }
                else
                {
                    groups.Add(new List<string>());
                }
            }
            return groups;
        }
        catch (Exception e)
        {
            Debug.LogError($"解析配置文件时发生异常: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取默认的11个按键组（与InputManager保持一致）
    /// </summary>
    List<List<string>> GetDefaultKeyGroups()
    {
        return new List<List<string>>
        {
            new List<string> { "1", "2", "q", "w" },
            new List<string> { "3", "4", "e", "r" },
            new List<string> { "5", "6", "t", "y" },
            new List<string> { "7", "8", "u", "i" },
            new List<string> { "9", "0", "o", "p" },
            new List<string> { "a", "s", "z", "x" },
            new List<string> { "d", "f", "c", "v" },
            new List<string> { "g", "h", "b", "n" },
            new List<string> { "j", "k", "m", "," },
            new List<string> { "l", ";", ".", "/" },
            new List<string> { "space" }
        };
    }

    /// <summary>
    /// 创建单个组UI，并根据硬编码数组设置位置
    /// </summary>
    void CreateGroupUI(int index, List<string> keys)
    {
        GameObject go = Instantiate(groupItemPrefab, groupsParent);
        // 设置位置（使用硬编码数组中的坐标）
        if (groupPositions != null && index < groupPositions.Length)
        {
            go.transform.localPosition = groupPositions[index];
        }
        else
        {
            Debug.LogWarning($"组索引 {index} 超出位置数组范围，将保持预制体默认位置。");
        }

        GroupUI ui = go.GetComponent<GroupUI>();
        ui.SetGroupIndex(index);
        ui.SetKeys(keys);
        ui.button.onClick.AddListener(() => OnGroupSelected(index));
        groupUIs.Add(ui);
    }

    void OnGroupSelected(int index)
    {
        // 点击同一组则取消选中，否则选中新组
        selectedGroupIndex = (selectedGroupIndex == index) ? -1 : index;
        UpdateGroupHighlights();
    }

    void UpdateGroupHighlights()
    {
        for (int i = 0; i < groupUIs.Count; i++)
        {
            groupUIs[i].SetHighlight(i == selectedGroupIndex);
        }
    }

    void Update()
    {
        if (selectedGroupIndex >= 0)
        {
            // 监听按键输入
            string input = Input.inputString;
            if (!string.IsNullOrEmpty(input))
            {
                foreach (char c in input)
                {
                    string keyName = c.ToString();
                    if (c == ' ') keyName = "space";

                    // 验证按键是否有效（在映射表中）
                    if (keyNameToKeyCode.ContainsKey(keyName))
                    {
                        ProcessKeyPress(keyName);
                    }
                }
            }
        }
    }

    void ProcessKeyPress(string keyName)
    {
        int currentGroup = FindGroupContainingKey(keyName);
        if (currentGroup == selectedGroupIndex)
        {
            // 按键已在当前组 → 移除
            RemoveKeyFromGroup(selectedGroupIndex, keyName);
        }
        else
        {
            // 按键不在当前组 → 从原组移除（如果有），再加入当前组
            if (currentGroup != -1)
                RemoveKeyFromGroup(currentGroup, keyName);
            AddKeyToGroup(selectedGroupIndex, keyName);
        }
    }

    int FindGroupContainingKey(string keyName)
    {
        for (int i = 0; i < groupUIs.Count; i++)
        {
            if (groupUIs[i].keys.Contains(keyName))
                return i;
        }
        return -1;
    }

    void RemoveKeyFromGroup(int groupIndex, string keyName)
    {
        if (groupUIs[groupIndex].keys.Remove(keyName))
        {
            groupUIs[groupIndex].UpdateKeysText();
        }
    }

    void AddKeyToGroup(int groupIndex, string keyName)
    {
        if (!groupUIs[groupIndex].keys.Contains(keyName))
        {
            groupUIs[groupIndex].keys.Add(keyName);
            groupUIs[groupIndex].UpdateKeysText();
        }
    }

    void OnFinish()
    {
        // 检查各组按键是否有重复
        HashSet<string> allKeys = new HashSet<string>();
        bool hasDuplicate = false;
        foreach (var ui in groupUIs)
        {
            foreach (var key in ui.keys)
            {
                if (allKeys.Contains(key))
                {
                    hasDuplicate = true;
                    break;
                }
                allKeys.Add(key);
            }
            if (hasDuplicate) break;
        }

        if (hasDuplicate)
        {
            Debug.LogError("按键组中存在重复按键，请修改！");
            return;
        }

        // 构建配置对象
        InputConfig config = new InputConfig();
        config.groups = new KeyGroupConfig[groupUIs.Count];
        for (int i = 0; i < groupUIs.Count; i++)
        {
            config.groups[i] = new KeyGroupConfig();
            config.groups[i].keys = groupUIs[i].keys.ToArray();
        }

        // 序列化并保存到 persistentDataPath/input.json
        string json = JsonUtility.ToJson(config, true);
        string filePath = Path.Combine(Application.persistentDataPath, ConfigFileName);

        try
        {
            // 确保目录存在
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(filePath, json);
            Debug.Log("配置已保存到 " + filePath);
            Debug.Log("保存成功！");

            // 切换到开始场景
            SceneManager.LoadScene("Start");
        }
        catch (Exception e)
        {
            Debug.LogError("保存失败：" + e.Message);
        }
    }
}