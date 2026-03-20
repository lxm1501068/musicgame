using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;

public class InputManager : MonoBehaviour
{
    /// <summary>
    /// 单例实例
    /// </summary>
    public static InputManager Instance;
    
    private Dictionary<string, KeyCode> keyNameToKeyCode = new Dictionary<string, KeyCode>();
    private List<List<string>> keyGroups = new List<List<string>>();
    private bool[] groupTriggeredThisFrame;
    
    public event Action<int> OnGroupKeyPressed;

    private const string ConfigFileName = "input.json";                // 持久化文件名
    private const string DefaultConfigResourceName = "input_default";  // Resources 中的默认配置（无扩展名）

    void Awake()
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

        InitializeKeyMappings();

        if (!LoadKeyGroupsFromPersistentPath())
        {
            Debug.LogWarning("无法加载配置文件，将使用默认按键组配置。");
            InitializeDefaultKeyGroups();
        }

        groupTriggeredThisFrame = new bool[keyGroups.Count];
    }

    void Update()
    {
        ResetFrameTriggerStates();
        CheckKeyGroups();
    }

    void InitializeKeyMappings()
    {
        for (int i = 0; i <= 9; i++)
        {
            string keyName = i.ToString();
            keyNameToKeyCode[keyName] = (KeyCode)Enum.Parse(typeof(KeyCode), "Alpha" + i);
        }
        
        for (char c = 'a'; c <= 'z'; c++)
        {
            string keyName = c.ToString();
            KeyCode keyCode = (KeyCode)Enum.Parse(typeof(KeyCode), keyName.ToUpper());
            keyNameToKeyCode[keyName] = keyCode;
        }
        
        keyNameToKeyCode["space"] = KeyCode.Space;
        keyNameToKeyCode[","] = KeyCode.Comma;
        keyNameToKeyCode["."] = KeyCode.Period;
        keyNameToKeyCode[";"] = KeyCode.Semicolon;
        keyNameToKeyCode["/"] = KeyCode.Slash;
    }

    /// <summary>
    /// 从 persistentDataPath 加载配置，若文件不存在则从 Resources 复制默认配置
    /// </summary>
    bool LoadKeyGroupsFromPersistentPath()
    {
        string persistentPath = Path.Combine(Application.persistentDataPath, ConfigFileName);

        // 如果持久化文件不存在，尝试从 Resources 复制默认配置
        if (!File.Exists(persistentPath))
        {
            Debug.Log($"配置文件不存在于 {persistentPath}，尝试从 Resources 复制默认配置...");
            TextAsset defaultConfig = Resources.Load<TextAsset>(DefaultConfigResourceName);
            if (defaultConfig != null)
            {
                try
                {
                    File.WriteAllText(persistentPath, defaultConfig.text);
                    Debug.Log($"默认配置已复制到 {persistentPath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"无法写入默认配置文件: {e.Message}");
                    return false;
                }
            }
            else
            {
                Debug.LogError($"Resources 中未找到默认配置文件 {DefaultConfigResourceName}，将使用硬编码默认组。");
                return false;
            }
        }

        // 从持久化路径读取配置
        try
        {
            string jsonText = File.ReadAllText(persistentPath);
            InputConfig config = JsonUtility.FromJson<InputConfig>(jsonText);

            if (config == null || config.groups == null || config.groups.Length == 0)
            {
                Debug.LogError("JSON 配置文件格式错误或没有定义任何按键组。");
                return false;
            }

            keyGroups.Clear();
            foreach (var group in config.groups)
            {
                if (group.keys != null && group.keys.Length > 0)
                    keyGroups.Add(new List<string>(group.keys));
                else
                    keyGroups.Add(new List<string>());
            }

            Debug.Log($"成功从 {persistentPath} 加载 {keyGroups.Count} 个按键组。");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"读取配置文件时发生异常: {e.Message}");
            return false;
        }
    }

    void InitializeDefaultKeyGroups()
    {
        keyGroups.Clear();
        keyGroups.Add(new List<string> { "1", "2", "q", "w" });
        keyGroups.Add(new List<string> { "3", "4", "e", "r" });
        keyGroups.Add(new List<string> { "5", "6", "t", "y" });
        keyGroups.Add(new List<string> { "7", "8", "u", "i" });
        keyGroups.Add(new List<string> { "9", "0", "o", "p" });
        keyGroups.Add(new List<string> { "a", "s", "z", "x" });
        keyGroups.Add(new List<string> { "d", "f", "c", "v" });
        keyGroups.Add(new List<string> { "g", "h", "b", "n" });
        keyGroups.Add(new List<string> { "j", "k", "m", "," });
        keyGroups.Add(new List<string> { "l", ";", ".", "/" });
        keyGroups.Add(new List<string> { "space" });
    }

    void ResetFrameTriggerStates()
    {
        for (int i = 0; i < groupTriggeredThisFrame.Length; i++)
            groupTriggeredThisFrame[i] = false;
    }

    void CheckKeyGroups()
    {
        for (int groupIndex = 0; groupIndex < keyGroups.Count; groupIndex++)
        {
            if (groupTriggeredThisFrame[groupIndex])
                continue;
                
            var group = keyGroups[groupIndex];
            
            foreach (var keyName in group)
            {
                if (keyNameToKeyCode.TryGetValue(keyName, out KeyCode keyCode))
                {
                    if (Input.GetKeyDown(keyCode))
                    {
                        groupTriggeredThisFrame[groupIndex] = true;
                        Debug.Log($"按键组 {groupIndex} 被触发");
                        int groupNumber = groupIndex + 1;
                        SendGroupCommand(groupNumber);
                        break;
                    }
                }
            }
        }
    }

    void SendGroupCommand(int groupNumber)
    {
        OnGroupKeyPressed?.Invoke(groupNumber);
    }

    public bool IsGroupPressed(int groupNumber)
    {
        if (groupNumber >= 0 && groupNumber < keyGroups.Count)
        {
            int groupIndex = groupNumber;
            var group = keyGroups[groupIndex];
            
            foreach (var keyName in group)
            {
                if (keyNameToKeyCode.TryGetValue(keyName, out KeyCode keyCode))
                {
                    if (Input.GetKeyDown(keyCode))
                        return true;
                }
            }
        }
        return false;
    }
    
    public bool IsGroupHeld(int groupNumber)
    {
        if (groupNumber >= 0 && groupNumber < keyGroups.Count)
        {
            int groupIndex = groupNumber;
            var group = keyGroups[groupIndex];
            
            foreach (var keyName in group)
            {
                if (keyNameToKeyCode.TryGetValue(keyName, out KeyCode keyCode))
                {
                    if (Input.GetKey(keyCode))
                        return true;
                }
            }
        }
        return false;
    }
    
    public List<int> GetAllPressedGroups()
    {
        List<int> pressedGroups = new List<int>();
        
        for (int groupIndex = 0; groupIndex < keyGroups.Count; groupIndex++)
        {
            var group = keyGroups[groupIndex];
            
            foreach (var keyName in group)
            {
                if (keyNameToKeyCode.TryGetValue(keyName, out KeyCode keyCode))
                {
                    if (Input.GetKeyDown(keyCode))
                    {
                        pressedGroups.Add(groupIndex + 1);
                        break;
                    }
                }
            }
        }
        
        return pressedGroups;
    }

    public int GetGroupCount()
    {
        return keyGroups.Count;
    }

    public List<string> GetGroupKeys(int groupNumber)
    {
        if (groupNumber >= 1 && groupNumber <= keyGroups.Count)
        {
            return new List<string>(keyGroups[groupNumber - 1]);
        }
        return new List<string>();
    }
}

[Serializable]
public class KeyGroupConfig
{
    public string[] keys;
}

[Serializable]
public class InputConfig
{
    public KeyGroupConfig[] groups;
}