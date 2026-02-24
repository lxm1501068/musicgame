using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(SpriteRenderer))] // 确保Key有视觉渲染组件
public class Key_move : MonoBehaviour
{
    [Header("核心关联")]
    public NoteData noteData;               // 绑定Key的核心数据
    public int keyIndex = 1;                // 按键序号（对应InputManager的按键组）

    [Header("备用配置（ChartData无数据时生效）")]
    public Vector2 initialPos = Vector2.zero; // 初始坐标
    public float shiftStartTime = 0f;         // Shift起始时间
    public float shiftEndTime = 2f;           // Shift结束时间
    public Vector2 shiftTargetPos = Vector2.zero; // Shift目标坐标
    public string moveJsonPath = "key_move_frames.json"; // Move指令JSON路径
    public bool useMoveFirst = true;         // 是否优先使用Move指令

    [Header("视觉配置")]
    public Sprite defaultKeySprite;          // 默认Key精灵
    public bool isVisible = true;            // 是否显示

    #region 私有字段
    private SpriteRenderer spriteRenderer;
    private KeyData _chartKeyData;           // 从ChartData读取的Key初始数据
    private List<KeyCommand> _keyCommands;   // 该Key的所有指令列表
    private List<ShiftCommand> _shiftCommands = new List<ShiftCommand>(); // Shift指令缓存
    private List<MoveCommand> _moveCommands = new List<MoveCommand>();   // Move指令缓存
    private bool _isInitialized = false;     // 初始化完成标记
    #endregion

    #region 生命周期
    void Awake()
    {
        // 初始化组件引用
        spriteRenderer = GetComponent<SpriteRenderer>();
        ValidateComponentReferences();

        // 初始化视觉状态
        InitVisualState();

        // 【核心修改】移除依赖ChartData的初始化逻辑，移到InitAfterChartLoaded
        // LoadKeyDataFromChart();
        // ValidateKeyIndex(); 
        // InitNoteData();
        // InitAllCommands();

        _isInitialized = true;
    }

    void Update()
    {
        // ========== 核心新增：游戏未播放时直接返回 ==========
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying)
        {
            // 可选：未播放时重置Key到初始状态
            ResetKeyToInitialState();
            return;
        }
        // 防护：未初始化/keyIndex不在合法列表中时跳过
        if (!_isInitialized || !IsKeyIndexValid())
        {
            return;
        }

        float currentTime = GameManager.Instance.CurrentPlayTime;
        float deltaTime = Time.deltaTime;

        // 执行所有指令逻辑
        ExecuteShiftCommands(currentTime, deltaTime);
        ExecuteMoveCommands(currentTime);
        ExecuteShowHideCommands(currentTime);

        // 同步NoteData坐标到Transform
        SyncPosition();
    }

    void OnDestroy()
    {
        StopAllCoroutines();
        _shiftCommands.Clear();
        _moveCommands.Clear();
        _keyCommands?.Clear();
    }
    #endregion

    #region 新增：谱面解析后初始化方法
    /// <summary>
    /// 谱面解析完成后执行的初始化（依赖ChartData）
    /// 供GameManager调用，支持重复初始化（切换谱面）
    /// </summary>
    public void InitAfterChartLoaded()
    {
        if (!_isInitialized)
        {
            Debug.LogError($"[{gameObject.name}] Key_move基础初始化未完成，无法执行Chart相关初始化！");
            return;
        }

        // 清空旧指令缓存
        _shiftCommands.Clear();
        _moveCommands.Clear();
        _keyCommands?.Clear();

        // 执行依赖ChartData的初始化逻辑
        LoadKeyDataFromChart();
        ValidateKeyIndex();
        InitNoteData();
        InitAllCommands();

        Debug.Log($"[{gameObject.name}] Key{keyIndex} 谱面解析后初始化完成");
    }
    #endregion

    #region 新增：未播放时重置Key状态
    /// <summary>
    /// 游戏未播放时，将Key重置到初始状态
    /// </summary>
    private void ResetKeyToInitialState()
    {
        if (noteData == null) return;

        // 重置坐标到初始值
        float initX = _chartKeyData != null ? _chartKeyData.x : initialPos.x;
        float initY = _chartKeyData != null ? _chartKeyData.y : initialPos.y;
        noteData.x = initX;
        noteData.y = initY;
        SyncPosition();

        // 重置显示状态
        bool initVisible = _chartKeyData != null ? (_chartKeyData.show == 1) : isVisible;
        noteData.isVisible = initVisible;
        gameObject.SetActive(initVisible);
    }
    #endregion

    #region 初始化逻辑
    /// <summary>
    /// 校验组件引用有效性
    /// </summary>
    private void ValidateComponentReferences()
    {
        if (spriteRenderer == null)
        {
            Debug.LogError($"[{gameObject.name}] Key_move组件：未找到SpriteRenderer组件！");
            enabled = false;
            return;
        }

        if (noteData == null)
        {
            Debug.LogWarning($"[{gameObject.name}] Key_move组件：NoteData未赋值，自动创建新实例！");
            noteData = new NoteData();
        }
    }

    /// <summary>
    /// 初始化视觉状态
    /// </summary>
    private void InitVisualState()
    {
        if (defaultKeySprite != null)
        {
            spriteRenderer.sprite = defaultKeySprite;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] Key_move组件：未设置默认Key精灵！");
        }

        gameObject.SetActive(isVisible);
    }

    /// <summary>
    /// 从ChartData加载Key初始数据和指令
    /// </summary>
    private void LoadKeyDataFromChart()
    {
        if (ChartData.Instance == null)
        {
            Debug.LogWarning("LoadKeyDataFromChart: ChartData.Instance为空，使用备用配置");
            _keyCommands = new List<KeyCommand>();
            return;
        }

        // 先检查keyIndex是否在合法列表中
        if (!IsKeyIndexValid())
        {
            Debug.LogWarning($"LoadKeyDataFromChart: Key{keyIndex} 不在合法keyIds列表中，使用备用配置");
            _keyCommands = new List<KeyCommand>();
            return;
        }

        // 查找当前keyIndex对应的KeyData
        _chartKeyData = ChartData.Instance.keyDatas.FirstOrDefault(k => k.keyName == keyIndex);
        if (_chartKeyData == null)
        {
            Debug.LogWarning($"LoadKeyDataFromChart: ChartData中无Key{keyIndex}的初始数据，使用备用配置");
            _keyCommands = new List<KeyCommand>();
            return;
        }

        // 读取并排序指令（按开始时间）
        _keyCommands = _chartKeyData.keyCommands?
            .OrderBy(cmd => cmd.startTime)
            .ToList() ?? new List<KeyCommand>();

        Debug.Log($"LoadKeyDataFromChart: 加载Key{keyIndex}的初始状态（x:{_chartKeyData.x}, y:{_chartKeyData.y}, show:{_chartKeyData.show}），指令数：{_keyCommands.Count}");
    }

    /// <summary>
    /// 初始化NoteData（优先ChartData配置）
    /// </summary>
    private void InitNoteData()
    {
        // 优先使用ChartData的初始值，否则用备用配置
        float initX = _chartKeyData != null ? _chartKeyData.x : initialPos.x;
        float initY = _chartKeyData != null ? _chartKeyData.y : initialPos.y;
        bool initVisible = _chartKeyData != null ? (_chartKeyData.show == 1) : isVisible;

        // 赋值核心字段
        noteData.NoteIndex = keyIndex;
        noteData.KeyIndex = keyIndex;
        noteData.x = initX;
        noteData.y = initY;
        noteData.isVisible = initVisible;
        noteData.commands ??= new List<Command>(); // 初始化指令列表

        // 同步初始位置和显示状态
        transform.position = new Vector3(initX, initY, transform.position.z);
        gameObject.SetActive(initVisible);
    }

    /// <summary>
    /// 初始化所有指令（Shift/Move/Show/Hide）
    /// </summary>
    private void InitAllCommands()
    {
        // 优先使用ChartData中的KeyCommand（仅当keyIndex合法时）
        if (IsKeyIndexValid() && _keyCommands != null && _keyCommands.Count > 0)
        {
            foreach (var keyCmd in _keyCommands)
            {
                switch (keyCmd.cmdType?.ToLower())
                {
                    case "drift": // Drift对应Shift指令
                        CreateShiftCommandFromKeyCmd(keyCmd);
                        break;
                    case "move": // Move指令（JSON帧动画）
                        CreateMoveCommandFromKeyCmd(keyCmd);
                        break;
                    case "hide": // 隐藏指令（无需提前初始化，Update中实时判断）
                    case "show": // 显示指令（无需提前初始化，Update中实时判断）
                        break;
                    default:
                        Debug.LogWarning($"InitAllCommands: 未知指令类型 {keyCmd.cmdType}，跳过Key{keyIndex}的该指令");
                        break;
                }
            }
        }
        else
        {
            // 备用配置：创建手动设置的指令
            CreateBackupShiftCommand();
            CreateBackupMoveCommand();
        }
    }

    /// <summary>
    /// 从KeyCommand创建Shift指令
    /// </summary>
    private void CreateShiftCommandFromKeyCmd(KeyCommand keyCmd)
    {
        Command shiftCmdData = new Command
        {
            timeA = keyCmd.startTime,
            timeB = keyCmd.endTime,
            x1 = keyCmd.x1,
            y1 = keyCmd.y1,
            x2 = keyCmd.x2,
            y2 = keyCmd.y2
        };

        ShiftCommand shiftCmd = new ShiftCommand(noteData, shiftCmdData);
        _shiftCommands.Add(shiftCmd);
        Debug.Log($"InitAllCommands: Key{keyIndex} 创建Drift(Shift)指令（{keyCmd.startTime}~{keyCmd.endTime}）");
    }

    /// <summary>
    /// 从KeyCommand创建Move指令
    /// </summary>
    private void CreateMoveCommandFromKeyCmd(KeyCommand keyCmd)
    {
        if (string.IsNullOrEmpty(keyCmd.filename))
        {
            Debug.LogWarning($"InitAllCommands: Key{keyIndex} 的Move指令JSON路径为空，跳过");
            return;
        }

        MoveCommand moveCmd = new MoveCommand(noteData, keyCmd.filename);
        _moveCommands.Add(moveCmd);
        useMoveFirst = true;
        Debug.Log($"InitAllCommands: Key{keyIndex} 创建Move指令（JSON：{keyCmd.filename}）");
    }

    /// <summary>
    /// 创建备用Shift指令（ChartData无数据时）
    /// </summary>
    private void CreateBackupShiftCommand()
    {
        Command shiftCmdData = new Command
        {
            timeA = shiftStartTime,
            timeB = shiftEndTime,
            x1 = noteData.x,
            y1 = noteData.y,
            x2 = shiftTargetPos.x,
            y2 = shiftTargetPos.y
        };

        ShiftCommand shiftCmd = new ShiftCommand(noteData, shiftCmdData);
        _shiftCommands.Add(shiftCmd);
        Debug.Log($"InitAllCommands: Key{keyIndex} 创建备用Shift指令");
    }

    /// <summary>
    /// 创建备用Move指令（ChartData无数据时）
    /// </summary>
    private void CreateBackupMoveCommand()
    {
        if (string.IsNullOrEmpty(moveJsonPath))
        {
            Debug.LogWarning($"InitAllCommands: Key{keyIndex} 备用Move指令JSON路径为空，跳过");
            return;
        }

        MoveCommand moveCmd = new MoveCommand(noteData, moveJsonPath);
        _moveCommands.Add(moveCmd);
        Debug.Log($"InitAllCommands: Key{keyIndex} 创建备用Move指令（JSON：{moveJsonPath}）");
    }
    #endregion

    #region 指令执行逻辑
    /// <summary>
    /// 执行所有Shift指令
    /// </summary>
    private void ExecuteShiftCommands(float currentTime, float deltaTime)
    {
        // 优先Move时跳过Shift
        if (useMoveFirst && _moveCommands.Count > 0) return;

        foreach (var shiftCmd in _shiftCommands)
        {
            shiftCmd?.UpdatePosition(currentTime, deltaTime);
        }
    }

    /// <summary>
    /// 执行所有Move指令
    /// </summary>
    private void ExecuteMoveCommands(float currentTime)
    {
        if (!useMoveFirst && _shiftCommands.Count > 0) return;

        foreach (var moveCmd in _moveCommands)
        {
            moveCmd?.UpdatePosition(currentTime);
        }
    }

    /// <summary>
    /// 执行显示/隐藏指令
    /// </summary>
    private void ExecuteShowHideCommands(float currentTime)
    {
        if (!IsKeyIndexValid() || _keyCommands == null || _keyCommands.Count == 0) return;

        foreach (var keyCmd in _keyCommands)
        {
            if (currentTime < keyCmd.startTime || currentTime > keyCmd.endTime) continue;

            switch (keyCmd.cmdType?.ToLower())
            {
                case "hide":
                    noteData.isVisible = false;
                    gameObject.SetActive(false);
                    break;
                case "show":
                    noteData.isVisible = true;
                    gameObject.SetActive(true);
                    break;
            }
        }
    }

    /// <summary>
    /// 同步NoteData坐标到Transform
    /// </summary>
    private void SyncPosition()
    {
        if (noteData == null) return;
        transform.position = new Vector3(noteData.x, noteData.y, transform.position.z);
    }
    #endregion

    #region 扩展方法（可选）
    /// <summary>
    /// 手动重置Key位置和指令状态
    /// </summary>
    public void ResetKeyState()
    {
        if (!IsKeyIndexValid() || noteData == null) return;

        // 重置坐标
        noteData.x = _chartKeyData != null ? _chartKeyData.x : initialPos.x;
        noteData.y = _chartKeyData != null ? _chartKeyData.y : initialPos.y;
        SyncPosition();

        // 重置显示状态
        noteData.isVisible = _chartKeyData != null ? (_chartKeyData.show == 1) : isVisible;
        gameObject.SetActive(noteData.isVisible);

        // 清空并重新初始化指令
        _shiftCommands.Clear();
        _moveCommands.Clear();
        InitAllCommands();

        Debug.Log($"ResetKeyState: Key{keyIndex} 已重置到初始状态");
    }
    #endregion

    #region 校验方法
    /// <summary>
    /// 校验keyIndex是否在合法的keyIds列表中
    /// </summary>
    private void ValidateKeyIndex()
    {
        if (!IsKeyIndexValid())
        {
            Debug.LogError($"[{gameObject.name}] Key_move组件：keyIndex={keyIndex} 不在合法的keyIds列表中！已禁用组件");
            enabled = false;
        }
        else
        {
            Debug.Log($"[{gameObject.name}] Key_move组件：keyIndex={keyIndex} 校验通过（在合法keyIds列表中）");
        }
    }

    /// <summary>
    /// 检查keyIndex是否在ChartData的keyIds列表中
    /// </summary>
    /// <returns>是否合法</returns>
    private bool IsKeyIndexValid()
    {
        if (ChartData.Instance == null || ChartData.Instance.keyIds == null || ChartData.Instance.keyIds.Count == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] Key_move组件：ChartData或keyIds未加载，暂时跳过keyIndex校验");
            return true; // 未加载时暂时放行，避免影响初始化
        }

        return ChartData.Instance.keyIds.Contains(keyIndex);
    }
    #endregion
}