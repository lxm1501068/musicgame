using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;
    
    private Dictionary<string, KeyCode> keyNameToKeyCode = new Dictionary<string, KeyCode>();
    private List<List<string>> keyGroups = new List<List<string>>();
    private bool[] groupTriggeredThisFrame;
    
    public event Action<int> OnGroupKeyPressed;

    private const string ConfigFileName = "input.json";
    private const string DefaultConfigResourceName = "input_default";

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

    private void InitializeKeyMappings()
    {
        // 0-9
        for (int i = 0; i <= 9; i++)
        {
            keyNameToKeyCode[i.ToString()] = KeyCode.Alpha0 + i;
        }
        
        // a-z
        for (char c = 'a'; c <= 'z'; c++)
        {
            string keyName = c.ToString();
            keyNameToKeyCode[keyName] = (KeyCode)Enum.Parse(typeof(KeyCode), keyName.ToUpper());
        }
        
        // 常用特殊键
        keyNameToKeyCode["space"] = KeyCode.Space;
        keyNameToKeyCode[","] = KeyCode.Comma;
        keyNameToKeyCode["."] = KeyCode.Period;
        keyNameToKeyCode[";"] = KeyCode.Semicolon;
        keyNameToKeyCode["/"] = KeyCode.Slash;
    }

    private bool LoadKeyGroupsFromPersistentPath()
    {
        string persistentPath = Path.Combine(Application.persistentDataPath, ConfigFileName);

        if (!File.Exists(persistentPath))
        {
            TextAsset defaultConfig = Resources.Load<TextAsset>(DefaultConfigResourceName);
            if (defaultConfig != null)
            {
                try
                {
                    File.WriteAllText(persistentPath, defaultConfig.text);
                }
                catch (Exception e)
                {
                    Debug.LogError($"无法写入默认配置文件: {e.Message}");
                    return false;
                }
            }
            else return false;
        }

        try
        {
            string jsonText = File.ReadAllText(persistentPath);
            InputConfig config = JsonUtility.FromJson<InputConfig>(jsonText);

            if (config == null || config.groups == null || config.groups.Length == 0) return false;

            keyGroups.Clear();
            foreach (var group in config.groups)
            {
                keyGroups.Add(group.keys != null ? new List<string>(group.keys) : new List<string>());
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"读取配置文件时发生异常: {e.Message}");
            return false;
        }
    }

    private void InitializeDefaultKeyGroups()
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

    private void ResetFrameTriggerStates()
    {
        if (groupTriggeredThisFrame.Length != keyGroups.Count)
            groupTriggeredThisFrame = new bool[keyGroups.Count];

        for (int i = 0; i < groupTriggeredThisFrame.Length; i++)
            groupTriggeredThisFrame[i] = false;
    }

    private void CheckKeyGroups()
    {
        for (int i = 0; i < keyGroups.Count; i++)
        {
            if (IsGroupPressed(i))
            {
                groupTriggeredThisFrame[i] = true;
                OnGroupKeyPressed?.Invoke(i); // 统一使用 0-based 索引
            }
        }
    }

    /// <summary>
    /// 检查按键组是否在当前帧按下（0-based 索引）
    /// </summary>
    public bool IsGroupPressed(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= keyGroups.Count) return false;
        
        foreach (var keyName in keyGroups[groupIndex])
        {
            if (keyNameToKeyCode.TryGetValue(keyName, out KeyCode keyCode))
            {
                if (Input.GetKeyDown(keyCode)) return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// 检查按键组是否正在按住（0-based 索引）
    /// </summary>
    public bool IsGroupHeld(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= keyGroups.Count) return false;
        
        foreach (var keyName in keyGroups[groupIndex])
        {
            if (keyNameToKeyCode.TryGetValue(keyName, out KeyCode keyCode))
            {
                if (Input.GetKey(keyCode)) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 检查特定名称的按键是否按下
    /// </summary>
    public bool IsKeyPressed(string keyName)
    {
        if (keyNameToKeyCode.TryGetValue(keyName.ToLower(), out KeyCode keyCode))
        {
            return Input.GetKeyDown(keyCode);
        }
        return false;
    }

    public int GetGroupCount() => keyGroups.Count;
}

[Serializable]
public class KeyGroupConfig { public string[] keys; }

[Serializable]
public class InputConfig { public KeyGroupConfig[] groups; }
