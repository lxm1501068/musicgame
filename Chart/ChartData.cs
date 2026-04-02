using System;
using System.Collections.Generic;
using UnityEngine;

// Key_move指令数据
[Serializable]
public class KeyCommand
{
    public int keyIndex;      // 按键序号（对应InputManager的按键组）
    public float startTime;   // 移动开始时间
    public float endTime;     // 移动结束时间
    public float x1;          // 起始x坐标
    public float y1;          // 起始y坐标
    public float x2;          // 目标x坐标
    public float y2;          // 目标y坐标
    public string json_filename;   // Move指令的.json文件名
    public string cmdType;    
}

// 音符类型枚举
public enum NoteType
{
    Tap, Hold, MTap, Flick, Key, Drag
}

// 音符指令
[Serializable]
public class Command
{
    public bool is_show = true; 
    public NoteType type;       
    public int num;             
    public float timeA;         
    public float timeB;         
    public float x1;           
    public float y1;           
    public float x2;           
    public float y2;           
    public int key_name;        
    public string json_filename;
    public float hold_duration;    
    public string commandName; 
    public bool isNoteFirstTimeOccured; 
}

// 补全KeyData的构造函数 + 序列化（方便Inspector查看）
[Serializable]
public class KeyData
{
    public int keyName;       // 按键编号
    public float x;           // 初始x坐标
    public float y;           // 初始y坐标
    public int show;          // 是否显示（1显示/0隐藏）
    public List<KeyCommand> keyCommands; // 关联的Key指令列表

    // 新增：对应LoadChart解析时的构造函数（解决编译报错）
    public KeyData(int keyName, float x, float y, int show)
    {
        this.keyName = keyName;
        this.x = x;
        this.y = y;
        this.show = show;
        this.keyCommands = new List<KeyCommand>(); // 初始化指令列表
    }

    // 无参构造（Unity序列化需要）
    public KeyData()
    {
        keyCommands = new List<KeyCommand>();
    }
}

// 改为MonoBehaviour，移除CreateAssetMenu（MonoBehaviour不能作为资源创建）
public class ChartData : MonoBehaviour
{
    private static ChartData _instance;
    public static ChartData Instance
    {
        get
        {
            if (_instance != null) return _instance;
            
            // 先查找场景中已存在的实例
            _instance = FindObjectOfType<ChartData>();
            
            if (_instance == null)
            {
                // 场景中没有则创建GameObject并挂载组件
                GameObject singletonObj = new GameObject("ChartData_Singleton");
                _instance = singletonObj.AddComponent<ChartData>();
                // 设置为DontDestroyOnLoad，保证切换场景不销毁
                DontDestroyOnLoad(singletonObj);
                
                // 隐藏GameObject并设置不保存标记
                singletonObj.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.HideInHierarchy;
                Debug.LogWarning("未找到ChartData实例，已自动创建全局单例对象");
            }
            return _instance;
        }
    }

    // 序列化字段，方便在 Inspector 中查看/编辑
    [Header("谱面核心数据")]
    public List<KeyData> keyDatas = new List<KeyData>();  // 按键初始状态
    public List<Command> commands = new List<Command>();  // 所有音符指令
    public List<Line> lines = new List<Line>();           // 装饰性 Line 列表
    public float totalDuration;                           // 谱面总时长
    public int noteCount => commands?.Count ?? 0;        // 改为属性
    public int keyCount => keyDatas?.Count ?? 0;         // 改为属性
    [Header("运行时数据（无需手动编辑）")]
    public Dictionary<int, bool> isScorable = new Dictionary<int, bool>(); 
    public List<int> keyIds = new List<int>(); // 存储轨道按键 ID 列表（对应 chart.txt 第二行的内容）

    // 防止重复创建单例（MonoBehaviour特有）
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            // 如果已有实例，销毁重复的
            Destroy(gameObject);
        }
    }

    // ========== 恢复的 ClearChartContent 方法 ==========
    /// <summary>
    /// 清空谱面所有内容（兼容原有LoadChart逻辑的命名）
    /// </summary>
    public void ClearChartContent()
    {
        // 先清空所有KeyData内的指令列表（在清空keyDatas之前）
        foreach(var keyData in keyDatas)
        {
            if(keyData?.keyCommands != null) 
                keyData.keyCommands.Clear();
        }

        keyDatas.Clear();
        commands.Clear();
        totalDuration = 0;
        isScorable.Clear();
        keyIds.Clear();
    }

    // 原有方法保留，仅修改 ResetChartData：清空 KeyData 的指令列表
    public void ResetChartData()
    {
        // 复用 ClearChartContent 逻辑，避免代码冗余
        ClearChartContent();
    }
    
    /// <summary>
    /// 添加 Line 对象
    /// </summary>
    public void AddLine(Line line)
    {
        if (line != null)
        {
            lines.Add(line);
        }
    }
    
    /// <summary>
    /// 应用所有 Line 的装饰效果到音符命令
    /// </summary>
    public void ApplyLineDecorations()
    {
        foreach (Line line in lines)
        {
            if (line != null)
            {
                line.ApplyDecorations(commands);
            }
        }
        Debug.Log($"已应用 {lines.Count} 个 Line 的装饰效果");
    }
    
    /// <summary>
    /// 清空所有 Line
    /// </summary>
    public void ClearLines()
    {
        lines.Clear();
    }

    public void AddNoteData(Command newCommand)
    {
        commands.Add(newCommand);
    }

    public void SortCommandsByTime()
    {
        // 空值防护：如果列表为null，先初始化；如果为空，直接返回
        if (commands == null)
        {
            commands = new List<Command>();
            return;
        }
        if (commands.Count == 0) return;

        // 自定义比较器：先比timeA，再比num
        commands.Sort((cmd1, cmd2) =>
        {
            // 处理元素为null的情况：null元素排到最后
            if (cmd1 == null && cmd2 == null) return 0;
            if (cmd1 == null) return 1;
            if (cmd2 == null) return -1;

            // 比较timeA（核心排序键）
            int timeCompare = cmd1.timeA.CompareTo(cmd2.timeA);
            if (timeCompare != 0)
            {
                return timeCompare;
            }
            // timeA相同则比较num（唯一编号，保证排序稳定）
            return cmd1.num.CompareTo(cmd2.num);
        });
    }
}