using UnityEngine;
using System.Collections.Generic;
using System;

public class InputManager : MonoBehaviour
{
    /// <summary>
    /// 单例实例，确保全局唯一访问点，方便其他脚本直接调用
    /// </summary>
    public static InputManager Instance;
    
    /// <summary>
    /// 存储按键名称到KeyCode的映射字典
    /// 例如: "a" → KeyCode.A, "space" → KeyCode.Space
    /// </summary>
    private Dictionary<string, KeyCode> keyNameToKeyCode = new Dictionary<string, KeyCode>();
    
    /// <summary>
    /// 存储11个按键组，每组包含多个按键名称
    /// 每个组代表一个功能键集合，按下组内任一按键即触发该组
    /// </summary>
    private List<List<string>> keyGroups = new List<List<string>>();
    
    /// <summary>
    /// 记录每组是否已在本帧被触发，避免同一组在同一帧内重复触发
    /// 数组索引对应组索引(0-10)，值为true表示该组已在本帧触发
    /// </summary>
    private bool[] groupTriggeredThisFrame;
    
    /// <summary>
    /// 事件：当任何按键组的按键被按下时触发
    /// 参数int: 被触发的组编号(1-11)
    /// </summary>
    public event Action<int> OnGroupKeyPressed;

    void Awake()
    {
        // 单例模式初始化
        if (Instance == null)
        {
            Instance = this;
            // 可选：场景切换时不销毁此对象，根据需求决定是否启用
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // 确保全局只有一个Input实例，销毁重复对象
            Destroy(gameObject);
            return;
        }

        // 初始化按键映射和按键组
        InitializeKeyMappings();
        InitializeKeyGroups();
        
        // 初始化触发状态数组，长度等于按键组数量
        groupTriggeredThisFrame = new bool[keyGroups.Count];
    }

    void Update()
    {
        // 每帧开始时重置所有组的触发状态
        ResetFrameTriggerStates();
        
        // 检查所有按键组的输入状态
        CheckKeyGroups();
    }

    /// <summary>
    /// 初始化按键名称到KeyCode的映射表
    /// 支持数字键0-9、字母键a-z以及特殊键
    /// </summary>
    void InitializeKeyMappings()
    {
        // 映射数字键 0-9 (KeyCode.Alpha0 到 KeyCode.Alpha9)
        for (int i = 0; i <= 9; i++)
        {
            string keyName = i.ToString();
            keyNameToKeyCode[keyName] = (KeyCode)Enum.Parse(typeof(KeyCode), "Alpha" + i);
        }
        
        // 映射字母键 a-z (KeyCode.A 到 KeyCode.Z)
        for (char c = 'a'; c <= 'z'; c++)
        {
            string keyName = c.ToString();
            KeyCode keyCode = (KeyCode)Enum.Parse(typeof(KeyCode), keyName.ToUpper());
            keyNameToKeyCode[keyName] = keyCode;
        }
        
        // 映射特殊功能键
        keyNameToKeyCode["space"] = KeyCode.Space;
        keyNameToKeyCode[","] = KeyCode.Comma;
        keyNameToKeyCode["."] = KeyCode.Period;
        keyNameToKeyCode[";"] = KeyCode.Semicolon;
        keyNameToKeyCode["/"] = KeyCode.Slash;
    }

    /// <summary>
    /// 初始化11个按键组，每组包含一组相关的按键
    /// 组内任一按键被按下都会触发该组事件
    /// </summary>
    void InitializeKeyGroups()
    {
        keyGroups.Clear();
        
        // 组1: 左上区域按键
        keyGroups.Add(new List<string> { "1", "2", "q", "w" });
        
        // 组2: 上中区域按键
        keyGroups.Add(new List<string> { "3", "4", "e", "r" });
        
        // 组3: 右上区域按键
        keyGroups.Add(new List<string> { "5", "6", "t", "y" });
        
        // 组4: 中左区域按键
        keyGroups.Add(new List<string> { "7", "8", "u", "i" });
        
        // 组5: 中右区域按键
        keyGroups.Add(new List<string> { "9", "0", "o", "p" });
        
        // 组6: 左下手势区域
        keyGroups.Add(new List<string> { "a", "s", "z", "x" });
        
        // 组7: 中下手势区域
        keyGroups.Add(new List<string> { "d", "f", "c", "v" });
        
        // 组8: 右下手势区域
        keyGroups.Add(new List<string> { "g", "h", "b", "n" });
        
        // 组9: 左下符号区域
        keyGroups.Add(new List<string> { "j", "k", "m", "," });
        
        // 组10: 右下符号区域
        keyGroups.Add(new List<string> { "l", ";", ".", "/" });
        
        // 组11: 空格键单独一组（常用功能键）
        keyGroups.Add(new List<string> { "space" });
    }

    /// <summary>
    /// 重置所有按键组的本帧触发状态
    /// 每帧开始时调用，为新的输入检测做准备
    /// </summary>
    void ResetFrameTriggerStates()
    {
        for (int i = 0; i < groupTriggeredThisFrame.Length; i++)
        {
            groupTriggeredThisFrame[i] = false;
        }
    }

    /// <summary>
    /// 检查所有按键组的输入状态
    /// 支持多组同时触发，但同一组在同一帧只会触发一次
    /// </summary>
    void CheckKeyGroups()
    {
        // 遍历所有按键组
        for (int groupIndex = 0; groupIndex < keyGroups.Count; groupIndex++)
        {
            // 如果该组在本帧已经触发过，跳过检测
            if (groupTriggeredThisFrame[groupIndex])
                continue;
                
            var group = keyGroups[groupIndex];
            
            // 检查组内的每个按键
            foreach (var keyName in group)
            {
                if (keyNameToKeyCode.TryGetValue(keyName, out KeyCode keyCode))
                {
                    // 检测按键是否在本帧被按下
                    if (Input.GetKeyDown(keyCode))
                    {
                        // 标记该组已触发，避免重复
                        groupTriggeredThisFrame[groupIndex] = true;
                        Debug.Log($"按键组 {groupIndex} 被触发");
                        
                        // 将组索引转换为组编号(1-11)并发送命令
                        int groupNumber = groupIndex + 1;
                        SendGroupCommand(groupNumber);
                        
                        // 跳出当前组的按键循环，继续检测其他组
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 发送组按键命令
    /// 触发OnGroupKeyPressed事件，传递被触发的组编号
    /// </summary>
    /// <param name="groupNumber">组编号(1-11)</param>
    void SendGroupCommand(int groupNumber)
    {
        // 调用事件，通知所有订阅者
        OnGroupKeyPressed?.Invoke(groupNumber);
        
        // 调试输出（可根据需要取消注释）
        // Debug.Log($"输入指令: 组{groupNumber}");
    }

    /// <summary>
    /// 检查指定组是否有按键在当前帧被按下
    /// </summary>
    /// <param name="groupNumber">组编号(1-11)</param>
    /// <returns>如果有按键被按下返回true，否则false</returns>
    public bool IsGroupPressed(int groupNumber)
    {
        if (groupNumber >= 1 && groupNumber <= keyGroups.Count)
        {
            int groupIndex = groupNumber - 1;
            var group = keyGroups[groupIndex];
            
            foreach (var keyName in group)
            {
                if (keyNameToKeyCode.TryGetValue(keyName, out KeyCode keyCode))
                {
                    if (Input.GetKeyDown(keyCode))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }
    
    /// <summary>
    /// 检查指定组是否有按键正处于被按住状态
    /// </summary>
    /// <param name="groupNumber">组编号(1-11)</param>
    /// <returns>如果有按键被按住返回true，否则false</returns>
    public bool IsGroupHeld(int groupNumber)
    {
        if (groupNumber >= 1 && groupNumber <= keyGroups.Count)
        {
            int groupIndex = groupNumber - 1;
            var group = keyGroups[groupIndex];
            
            foreach (var keyName in group)
            {
                if (keyNameToKeyCode.TryGetValue(keyName, out KeyCode keyCode))
                {
                    if (Input.GetKey(keyCode))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }
    
    /// <summary>
    /// 获取所有在当前帧被按下的组编号列表
    /// 支持多组同时按下的情况
    /// </summary>
    /// <returns>包含所有被按下组编号的列表</returns>
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
                        break; // 找到该组的一个按键即可，跳出循环
                    }
                }
            }
        }
        
        return pressedGroups;
    }

    /// <summary>
    /// 获取按键组的总数量
    /// </summary>
    /// <returns>按键组数量，固定返回11</returns>
    public int GetGroupCount()
    {
        return keyGroups.Count;
    }

    /// <summary>
    /// 获取指定组包含的所有按键名称列表
    /// </summary>
    /// <param name="groupNumber">组编号(1-11)</param>
    /// <returns>按键名称列表，如果组编号无效返回空列表</returns>
    public List<string> GetGroupKeys(int groupNumber)
    {
        if (groupNumber >= 1 && groupNumber <= keyGroups.Count)
        {
            return new List<string>(keyGroups[groupNumber - 1]);
        }
        return new List<string>();
    }
}