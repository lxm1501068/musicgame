using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(SpriteRenderer))]
public class Key_move : MonoBehaviour
{
    [Header("核心配置")]
    public int keyIndex = 1;                // 按键序号（对应InputManager的按键组）
    public Sprite defaultKeySprite;         // 默认Key精灵
    public bool isVisible = true;           // 是否显示

    #region 私有核心字段
    private SpriteRenderer _spriteRenderer;
    private KeyData _chartKeyData;          // 从ChartData读取的Key初始数据
    private List<KeyCommand> _keyCommands;  // 该Key的所有指令列表
    private List<KeyShiftCommand> _shiftCommands = new List<KeyShiftCommand>();
    private List<KeyMoveCommand> _moveCommands = new List<KeyMoveCommand>();
    private bool _isInitialized = false;    // 初始化完成标记
    private float _currentX;                // 当前X坐标
    private float _currentY;                // 当前Y坐标
    private bool _currentVisible;           // 当前显示状态
    #endregion

    #region 生命周期
    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        // 谱面未加载解析完成时跳过
        if (GameManager.Instance == null || !GameManager.Instance.IsChartLoadedAndParsed)
            return;

        // 首次初始化（依赖ChartData）
        if (!_isInitialized)
        {
            InitCoreLogic();
            return;
        }

        // 游戏未播放时重置状态并返回
        if (!GameManager.Instance.IsPlaying)
        {
            ResetKeyToInitialState();
            return;
        }

        // 核心逻辑执行
        float currentTime = GameManager.Instance.CurrentPlayTime;
        ExecuteShiftCommands(currentTime);
        ExecuteMoveCommands(currentTime);
        ExecuteShowHideCommands(currentTime);
        SyncPosition();
    }

    void OnDestroy()
    {
        _shiftCommands.Clear();
        _moveCommands.Clear();
        _keyCommands?.Clear();
    }
    #endregion

    #region 核心初始化逻辑
    private void InitCoreLogic()
    {
        // 校验Key合法性，不合法则销毁
        if (!CheckKeyValidity())
        {
            Destroy(gameObject);
            return;
        }

        // 初始化组件 and 视觉状态
        _spriteRenderer.sprite = defaultKeySprite;
        gameObject.SetActive(isVisible);

        // 加载ChartData中的Key数据
        LoadKeyDataFromChart();

        // 初始化指令
        InitAllCommands();

        // 修复：用三元运算符判断_chartKeyData是否为null，避免??用于非可空bool
        _currentX = _chartKeyData?.x ?? transform.position.x;
        _currentY = _chartKeyData?.y ?? transform.position.y;
        _currentVisible = _chartKeyData != null ? (_chartKeyData.show == 1) : isVisible;

        _isInitialized = true;
    }

    /// <summary>
    /// 校验Key合法性（是否在ChartData的keyIds列表中）
    /// </summary>
    private bool CheckKeyValidity()
    {
        return ChartData.Instance.keyIds != null && ChartData.Instance.keyIds.Contains(keyIndex);
    }

    /// <summary>
    /// 从ChartData加载Key初始数据和指令
    /// </summary>
    private void LoadKeyDataFromChart()
    {
        _chartKeyData = ChartData.Instance.keyDatas.FirstOrDefault(k => k.keyName == keyIndex);
        _keyCommands = _chartKeyData?.keyCommands?.OrderBy(cmd => cmd.startTime).ToList() ?? new List<KeyCommand>();
    }

    /// <summary>
    /// 初始化所有指令（Shift/Move）
    /// </summary>
    private void InitAllCommands()
    {
        foreach (var keyCmd in _keyCommands)
        {
            switch (keyCmd.cmdType?.ToLower())
            {
                case "shift":
                    _shiftCommands.Add(new KeyShiftCommand(this, keyCmd));
                    break;
                case "move":
                    if (!string.IsNullOrEmpty(keyCmd.json_filename))
                        _moveCommands.Add(new KeyMoveCommand(this, keyCmd.json_filename));
                    break;
            }
        }
    }
    #endregion

    #region 指令执行逻辑
    /// <summary>
    /// 执行Shift指令（优先Move时跳过）
    /// </summary>
    private void ExecuteShiftCommands(float currentTime)
    {
        if (_moveCommands.Count > 0) return;

        foreach (var shiftCmd in _shiftCommands)
            shiftCmd?.UpdateKeyPosition(currentTime);
    }

    /// <summary>
    /// 执行Move指令（无Move时执行Shift）
    /// </summary>
    private void ExecuteMoveCommands(float currentTime)
    {
        if (_moveCommands.Count == 0) return;

        foreach (var moveCmd in _moveCommands)
            moveCmd?.UpdateKeyPosition(currentTime);
    }

    /// <summary>
    /// 执行显示/隐藏指令
    /// </summary>
    private void ExecuteShowHideCommands(float currentTime)
    {
        foreach (var keyCmd in _keyCommands)
        {
            if (currentTime < keyCmd.startTime || currentTime > keyCmd.endTime)
                continue;

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
    /// 同步坐标到Transform
    /// </summary>
    private void SyncPosition()
    {
        transform.position = new Vector3(_currentX, _currentY, transform.position.z);
    }

    /// <summary>
    /// 重置Key到初始状态
    /// </summary>
    private void ResetKeyToInitialState()
    {
        _currentX = _chartKeyData?.x ?? transform.position.x;
        _currentY = _chartKeyData?.y ?? transform.position.y;
        // 修复：用三元运算符替代??，避免非可空bool使用null合并运算符
        _currentVisible = _chartKeyData != null ? (_chartKeyData.show == 1) : isVisible;
        
        SyncPosition();
        gameObject.SetActive(_currentVisible);
    }
    #endregion

    #region 对外接口
    /// <summary>
    /// 更新Key坐标（供指令调用）
    /// </summary>
    public void UpdatePosition(float newX, float newY)
    {
        _currentX = newX;
        _currentY = newY;
    }

    // 只读属性暴露核心状态
    public float CurrentX => _currentX;
    public float CurrentY => _currentY;
    public bool CurrentVisible => _currentVisible;
    #endregion
}

#region 指令类（精简版）
public class KeyShiftCommand
{
    private readonly Key_move _keyMove;
    private readonly float _startTime;
    private readonly float _endTime;
    private readonly float _startX;
    private readonly float _startY;
    private readonly float _targetX;
    private readonly float _targetY;

    public KeyShiftCommand(Key_move keyMove, KeyCommand cmd)
    {
        _keyMove = keyMove;
        _startTime = cmd.startTime;
        _endTime = cmd.endTime;
        _startX = cmd.x1;
        _startY = cmd.y1;
        _targetX = cmd.x2;
        _targetY = cmd.y2;
    }

    public void UpdateKeyPosition(float currentTime)
    {
        if (currentTime < _startTime || currentTime > _endTime)
            return;

        float progress = Mathf.Clamp01((currentTime - _startTime) / (_endTime - _startTime));
        float newX = Mathf.Lerp(_startX, _targetX, progress);
        float newY = Mathf.Lerp(_startY, _targetY, progress);
        
        _keyMove.UpdatePosition(newX, newY);
    }
}

public class KeyMoveCommand
{
    private readonly Key_move _keyMove;
    private readonly string _jsonPath;
    private List<Vector2> _frames = new List<Vector2>();
    private float _frameDuration;
    private int _totalFrames;

    public KeyMoveCommand(Key_move keyMove, string jsonPath)
    {
        _keyMove = keyMove;
        _jsonPath = jsonPath;
        LoadMoveFrames();
    }

    /// <summary>
    /// 加载Move指令的JSON帧数据（需根据实际JSON格式实现）
    /// </summary>
    private void LoadMoveFrames()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(_jsonPath);
        if (jsonFile == null)
        {
            Debug.LogError($"未找到Move指令JSON文件: {_jsonPath}");
            return;
        }

        // 此处替换为实际的JSON解析逻辑
        // 示例：_frames = JsonUtility.FromJson<MoveFrameData>(jsonFile.text).frames;
        // _frameDuration = 0.016f; // 60帧/秒示例值
        // _totalFrames = _frames.Count;
    }

    public void UpdateKeyPosition(float currentTime)
    {
        if (_totalFrames == 0)
            return;

        int currentFrame = Mathf.Clamp(Mathf.FloorToInt(currentTime / _frameDuration), 0, _totalFrames - 1);
        Vector2 targetPos = _frames[currentFrame];
        
        _keyMove.UpdatePosition(targetPos.x, targetPos.y);
    }
}
#endregion