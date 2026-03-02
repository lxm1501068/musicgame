using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(SpriteRenderer))] // 确保Key有视觉渲染组件
public class Key_move : MonoBehaviour
{
    [Header("核心关联")]
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

    #region 私有字段（替换NoteData为自定义Key状态）
    private SpriteRenderer spriteRenderer;
    private KeyData _chartKeyData;           // 从ChartData读取的Key初始数据
    private List<KeyCommand> _keyCommands;   // 该Key的所有指令列表
    private List<KeyShiftCommand> _shiftCommands = new List<KeyShiftCommand>(); // Shift指令缓存
    private List<KeyMoveCommand> _moveCommands = new List<KeyMoveCommand>();   // Move指令缓存
    private bool _isInitialized = false;     // 初始化完成标记

    // 替换NoteData的核心状态字段
    private float _currentX;                 // 当前X坐标
    private float _currentY;                 // 当前Y坐标
    private bool _currentVisible;            // 当前显示状态
    private int _noteIndex;                  // 音符索引（原NoteData.NoteIndex）
    #endregion

    #region 生命周期
    void Awake()
    {
        // 初始化组件引用
        spriteRenderer = GetComponent<SpriteRenderer>();
        ValidateComponentReferences();

        // 初始化视觉状态
        InitVisualState();

        // 初始化核心状态（替代NoteData）
        InitKeyState();

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

        // 同步自定义状态到Transform
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
        InitAllCommands();

        Debug.Log($"[{gameObject.name}] Key{keyIndex} 谱面解析后初始化完成");
    }
    #endregion

    #region 核心状态初始化/重置（替换NoteData）
    /// <summary>
    /// 初始化Key核心状态（替代NoteData初始化）
    /// </summary>
    private void InitKeyState()
    {
        _noteIndex = keyIndex;
        _currentX = initialPos.x;
        _currentY = initialPos.y;
        _currentVisible = isVisible;
    }

    /// <summary>
    /// 游戏未播放时，将Key重置到初始状态
    /// </summary>
    private void ResetKeyToInitialState()
    {
        // 重置坐标到初始值
        float initX = _chartKeyData != null ? _chartKeyData.x : initialPos.x;
        float initY = _chartKeyData != null ? _chartKeyData.y : initialPos.y;
        _currentX = initX;
        _currentY = initY;
        SyncPosition();

        // 重置显示状态
        bool initVisible = _chartKeyData != null ? (_chartKeyData.show == 1) : isVisible;
        _currentVisible = initVisible;
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

        // 更新核心状态为ChartData中的初始值
        _currentX = _chartKeyData.x;
        _currentY = _chartKeyData.y;
        _currentVisible = _chartKeyData.show == 1;

        Debug.Log($"LoadKeyDataFromChart: 加载Key{keyIndex}的初始状态（x:{_chartKeyData.x}, y:{_chartKeyData.y}, show:{_chartKeyData.show}），指令数：{_keyCommands.Count}");
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
        // 构建Shift指令所需的参数（直接使用KeyCommand字段）
        // 直接使用KeyCommand创建指令
        KeyShiftCommand shiftCmd = new KeyShiftCommand(this, keyCmd);
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

        // 替换MoveCommand的构造参数，直接传入当前Key的状态引用
        KeyMoveCommand moveCmd = new KeyMoveCommand(this, keyCmd.filename);
        _moveCommands.Add(moveCmd);
        useMoveFirst = true;
        Debug.Log($"InitAllCommands: Key{keyIndex} 创建Move指令（JSON：{keyCmd.filename}）");
    }

    /// <summary>
    /// 创建备用Shift指令（ChartData无数据时）
    /// </summary>
    private void CreateBackupShiftCommand()
    {
        // 构造一个简化的KeyCommand用于备用配置
        KeyCommand backupCmd = new KeyCommand
        {
            keyIndex = this.keyIndex,
            startTime = shiftStartTime,
            endTime = shiftEndTime,
            x1 = _currentX,
            y1 = _currentY,
            x2 = shiftTargetPos.x,
            y2 = shiftTargetPos.y
        };
        KeyShiftCommand shiftCmd = new KeyShiftCommand(this, backupCmd);
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

        KeyMoveCommand moveCmd = new KeyMoveCommand(this, moveJsonPath);
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
            shiftCmd?.UpdateKeyPosition(currentTime, deltaTime);
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
            moveCmd?.UpdateKeyPosition(currentTime);
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
                    _currentVisible = false;
                    gameObject.SetActive(false);
                    break;
                case "show":
                    _currentVisible = true;
                    gameObject.SetActive(true);
                    break;
            }
        }
    }

    /// <summary>
    /// 同步自定义坐标状态到Transform
    /// </summary>
    private void SyncPosition()
    {
        transform.position = new Vector3(_currentX, _currentY, transform.position.z);
    }
    #endregion

    #region 扩展方法（可选）
    /// <summary>
    /// 手动重置Key位置和指令状态
    /// </summary>
    public void ResetKeyState()
    {
        if (!IsKeyIndexValid()) return;

        // 重置坐标
        _currentX = _chartKeyData != null ? _chartKeyData.x : initialPos.x;
        _currentY = _chartKeyData != null ? _chartKeyData.y : initialPos.y;
        SyncPosition();

        // 重置显示状态
        _currentVisible = _chartKeyData != null ? (_chartKeyData.show == 1) : isVisible;
        gameObject.SetActive(_currentVisible);

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

    #region 对外暴露的状态修改接口（供Shift/Move指令调用）
    /// <summary>
    /// 更新Key的坐标（供Shift/Move指令调用）
    /// </summary>
    /// <param name="newX">新X坐标</param>
    /// <param name="newY">新Y坐标</param>
    public void UpdatePosition(float newX, float newY)
    {
        _currentX = newX;
        _currentY = newY;
    }

    /// <summary>
    /// 获取当前X坐标
    /// </summary>
    public float CurrentX => _currentX;

    /// <summary>
    /// 获取当前Y坐标
    /// </summary>
    public float CurrentY => _currentY;

    /// <summary>
    /// 获取当前显示状态
    /// </summary>
    public bool CurrentVisible => _currentVisible;

    /// <summary>
    /// 获取Key索引
    /// </summary>
    public int KeyIndex => keyIndex;
    #endregion
}

// 配套修改KeyShiftCommand（简化版，适配Key_move的状态）
public class KeyShiftCommand
{
    private Key_move _keyMove;
    private float _startTime;
    private float _endTime;
    private float _startX;
    private float _startY;
    private float _targetX;
    private float _targetY;
    private bool _isExecuting;

    public KeyShiftCommand(Key_move keyMove, KeyCommand cmd)
    {
        _keyMove = keyMove;
        _startTime = cmd.startTime;
        _endTime = cmd.endTime;
        _startX = cmd.x1;
        _startY = cmd.y1;
        _targetX = cmd.x2;
        _targetY = cmd.y2;
        _isExecuting = false;
    }

    public void UpdateKeyPosition(float currentTime, float deltaTime)
    {
        if (currentTime < _startTime || currentTime > _endTime)
        {
            _isExecuting = false;
            return;
        }

        _isExecuting = true;
        float progress = (currentTime - _startTime) / (_endTime - _startTime);
        progress = Mathf.Clamp01(progress);

        float newX = Mathf.Lerp(_startX, _targetX, progress);
        float newY = Mathf.Lerp(_startY, _targetY, progress);

        _keyMove.UpdatePosition(newX, newY);
    }
}

// 配套修改KeyMoveCommand（简化版，适配Key_move的状态）
public class KeyMoveCommand
{
    private Key_move _keyMove;
    private string _jsonPath;
    private List<Vector2> _frames = new List<Vector2>();
    private float _frameDuration;
    private int _totalFrames;

    public KeyMoveCommand(Key_move keyMove, string jsonPath)
    {
        _keyMove = keyMove;
        _jsonPath = jsonPath;
        LoadMoveFrames();
    }

    private void LoadMoveFrames()
    {
        // 此处实现JSON帧数据加载逻辑（示例）
        // 实际需根据你的JSON格式解析帧坐标和时长
        TextAsset jsonFile = Resources.Load<TextAsset>(_jsonPath);
        if (jsonFile == null)
        {
            Debug.LogError($"MoveCommand: 未找到JSON文件 {_jsonPath}");
            return;
        }

        // 解析JSON逻辑（示例）
        // var frameData = JsonUtility.FromJson<MoveFrameData>(jsonFile.text);
        // _frames = frameData.frames;
        // _frameDuration = frameData.frameDuration;
        // _totalFrames = _frames.Count;
    }

    public void UpdateKeyPosition(float currentTime)
    {
        if (_totalFrames == 0) return;

        // 计算当前帧索引
        int currentFrame = Mathf.FloorToInt(currentTime / _frameDuration);
        currentFrame = Mathf.Clamp(currentFrame, 0, _totalFrames - 1);

        Vector2 targetPos = _frames[currentFrame];
        _keyMove.UpdatePosition(targetPos.x, targetPos.y);
    }
}