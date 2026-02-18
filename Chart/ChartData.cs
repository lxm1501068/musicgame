using System;
using System.Collections.Generic;
using UnityEngine;

// 按键初始状态数据
[Serializable]
public class KeyData
{
    public int keyName;       // 按键编号
    public float x;           // 初始x坐标
    public float y;           // 初始y坐标
    public int show;          // 是否显示（1显示/0隐藏）

    public KeyData(int keyName, float x, float y, int show)
    {
        this.keyName = keyName;
        this.x = x;
        this.y = y;
        this.show = show;
    }
}

//Key_move指令数据
[Serializable]
public class KeyMoveData
{
    public int keyIndex;      // 按键序号（对应InputManager的按键组）
    public float startTime;   // 移动开始时间
    public float endTime;     // 移动结束时间
    public Vector2 targetPos; // 移动目标坐标

    public KeyMoveData(int keyIndex, float startTime, float endTime, Vector2 targetPos)
    {
        this.keyIndex = keyIndex;
        this.startTime = startTime;
        this.endTime = endTime;
        this.targetPos = targetPos;
    }
}

// 音符类型枚举
public enum NoteType
{
    Tap, Hold, DTap, Flick, Key, Drag
}

// 音符数据（仅新增 is_show 字段，其余完全保留）
[Serializable]
public class Command
{
    public bool is_show = true; // 新增：控制该音符是否显示（默认显示）
    public NoteType type;       // 音符类型
    public int num;             // 音符编号（唯一）
    public float timeA;         // 起始时间（排序依据）
    public float timeB;         // 结束时间（destroy指令为0）
    public float x1;           // x1坐标（支持表达式）
    public float y1;           // y1坐标（支持表达式）
    public float x2;           // x2坐标（支持表达式）
    public float y2;           // y2坐标（支持表达式）
    public string command; // 指令列表
    public bool isNoteFirstTimeOccured; // 是否第一次创建音符

    public Command(int num, NoteType type, float timeA, float timeB, float x1, float y1, float x2, float y2, string command, bool isNoteFirstTimeOccured = true)
    {
        this.num = num;
        this.type = type;
        this.timeA = timeA;
        this.timeB = timeB;
        this.x1 = x1;
        this.y1 = y1;
        this.x2 = x2;
        this.y2 = y2;
        this.command = command;
        this.isNoteFirstTimeOccured = isNoteFirstTimeOccured;
    }
}

// 谱面总数据（改造为ScriptableObject单例，保留所有原有方法/字段）
[CreateAssetMenu(fileName = "ChartDataSingleton", menuName = "节奏游戏/ChartData单例")] // 手动创建实例的菜单
public class ChartData : ScriptableObject
{
    // 单例核心：全局唯一实例
    private static ChartData _instance;
    // 公共访问入口
    public static ChartData Instance
    {
        get
        {
            // 1. 如果实例已存在，直接返回
            if (_instance != null) return _instance;

            // 2. 尝试从Resources文件夹加载已创建的ChartData实例
            _instance = Resources.Load<ChartData>("ChartDataSingleton");

            // 3. 如果加载不到，自动创建一个运行时实例（仅内存中，不会保存到本地）
            if (_instance == null)
            {
                _instance = CreateInstance<ChartData>();
                // 标记为隐藏（避免在Hierarchy显示）
                _instance.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.HideInHierarchy;
                Debug.LogWarning("未找到ChartData单例资源，已自动创建运行时实例（仅内存有效）");
            }

            return _instance;
        }
    }

    // ========== 以下是原有字段，完全保留 ==========
    public List<KeyData> keyDatas = new List<KeyData>();  // 按键初始状态
    public List<Command> commands = new List<Command>();// 所有音符（保持原有List结构）
    public float totalDuration;                           // 谱面总时长
    public int noteCount;                                // 音符总数量
    public Dictionary<int, bool> isScorable = new Dictionary<int, bool>(); // 音符是否可记分
    public List<KeyMoveData> keyMoveDatas = new List<KeyMoveData>(); // Key_move指令数据列表

    // ========== 以下是原有方法，完全保留 ==========
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

    // ========== 可选：重置单例数据（切换谱面时用） ==========
    public void ResetChartData()
    {
        keyDatas.Clear();
        commands.Clear();
        totalDuration = 0;
        noteCount = 0;
        isScorable.Clear();
        keyMoveDatas.Clear();
    }
}