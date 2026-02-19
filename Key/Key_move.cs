using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Key移动控制组件：处理Key的shift（匀速移动）和move（JSON帧移动）指令
/// 复用NoteTools的核心移动逻辑，减少冗余
/// </summary>
public class Key_move : MonoBehaviour
{
    #region 序列化配置（Inspector面板可编辑）
    [Header("核心关联")]
    [Tooltip("关联NoteTools实例（获取时间阈值/输入管理）")]
    public NoteTools noteTools;
    [Tooltip("当前Key的序号（对应InputManager的按键组）")]
    public int keyIndex = 1;

    [Header("Shift指令配置")]
    [Tooltip("Shift移动起始时间（秒，基于音乐时间）")]
    public float shiftStartTime = 0f;
    [Tooltip("Shift移动结束时间（秒，基于音乐时间）")]
    public float shiftEndTime = 2f;
    [Tooltip("Shift移动的目标坐标")]
    public Vector2 shiftTargetPos;

    [Header("Move指令配置")]
    [Tooltip("Move指令的JSON帧数据路径（StreamingAssets目录下）")]
    public string moveJsonPath = "key_move_frames.json";
    [Tooltip("是否优先使用Move指令（true=Move，false=Shift）")]
    public bool useMoveFirst = true;

    [Header("初始配置")]
    [Tooltip("Key的初始坐标")]
    public Vector2 initialPos;
    #endregion

    #region 私有状态
    private Transform _keyTransform; // 缓存Key的Transform（控制位置）
    private NoteData _keyNoteData;   // 复用NoteData存储Key的位置/状态
    private ShiftCommand _shiftCmd;  // 复用ShiftCommand处理匀速移动
    private MoveCommand _moveCmd;    // 复用MoveCommand处理JSON帧移动
    #endregion

    #region 生命周期
    private void Awake()
    {
        // 初始化缓存
        _keyTransform = transform;
        
        // 安全校验
        ValidateReferences();

        // 初始化Key的NoteData（模拟音符数据，仅用于存储位置）
        _keyNoteData = new NoteData
        {
            NoteIndex = keyIndex,
            KeyIndex = keyIndex,
            x = initialPos.x,
            y = initialPos.y,
            isVisible = true
        };
        _keyTransform.position = new Vector3(_keyNoteData.x, _keyNoteData.y, _keyTransform.position.z);

        // 初始化指令（复用NoteTools的指令类）
        InitCommands();
    }

    private void Update()
    {
        if (noteTools == null) return;

        // 替换为实际音乐播放时间（建议替换为AudioSource.time）
        float currentMusicTime = Time.time;
        float deltaTime = Time.deltaTime;

        // 更新Key位置（优先Move指令，其次Shift）
        UpdateKeyPosition(currentMusicTime, deltaTime);
    }
    #endregion

    #region 核心指令逻辑
    /// <summary>
    /// 校验核心引用，缺失则提示并禁用脚本
    /// </summary>
    private void ValidateReferences()
    {
        if (_keyTransform == null)
        {
            Debug.LogError($"[{gameObject.name}] Key_move组件：未找到Transform组件！", this);
            enabled = false;
        }

        if (noteTools == null)
        {
            Debug.LogError($"[{gameObject.name}] Key_move组件：未关联NoteTools实例！", this);
        }

        // Shift指令时间校验
        if (shiftEndTime <= shiftStartTime)
        {
            Debug.LogWarning($"[{gameObject.name}] Key_move组件：Shift结束时间({shiftEndTime})需大于起始时间({shiftStartTime})，已自动修正为起始时间+1秒", this);
            shiftEndTime = shiftStartTime + 1f;
        }
    }

    /// <summary>
    /// 初始化Shift/Move指令（复用NoteTools的指令类）
    /// </summary>
    private void InitCommands()
    {
        // 1. 初始化Shift指令（构造模拟的Command数据）
        Command shiftCmdData = new Command
        {
            timeA = shiftStartTime,
            timeB = shiftEndTime,
            x1 = initialPos.x,
            y1 = initialPos.y,
            x2 = shiftTargetPos.x,
            y2 = shiftTargetPos.y
        };
        _shiftCmd = new ShiftCommand(_keyNoteData, shiftCmdData);

        // 2. 初始化Move指令（复用NoteTools的MoveCommand）
        _moveCmd = new MoveCommand(_keyNoteData, moveJsonPath);
    }

    /// <summary>
    /// 更新Key位置（优先Move，其次Shift）
    /// </summary>
    /// <param name="currentTime">当前音乐时间（秒）</param>
    /// <param name="deltaTime">帧间隔时间</param>
    private void UpdateKeyPosition(float currentTime, float deltaTime)
    {
        // 优先执行Move指令
        if (useMoveFirst && _moveCmd != null)
        {
            _moveCmd.UpdatePosition(currentTime);
        }
        // 其次执行Shift指令
        else if (_shiftCmd != null)
        {
            _shiftCmd.UpdatePosition(currentTime, deltaTime);
        }

        // 将NoteData的位置同步到Transform
        _keyTransform.position = new Vector3(
            _keyNoteData.x, 
            _keyNoteData.y, 
            _keyTransform.position.z
        );
    }
    #endregion

    #region 公共方法（外部调用/重置）
    /// <summary>
    /// 手动设置Shift指令参数（外部代码调用）
    /// </summary>
    /// <param name="startTime">起始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="targetPos">目标坐标</param>
    public void SetShiftParams(float startTime, float endTime, Vector2 targetPos)
    {
        shiftStartTime = startTime;
        shiftEndTime = endTime > startTime ? endTime : startTime + 1f;
        shiftTargetPos = targetPos;

        // 重新初始化Shift指令
        Command shiftCmdData = new Command
        {
            timeA = shiftStartTime,
            timeB = shiftEndTime,
            x1 = _keyNoteData.x,
            y1 = _keyNoteData.y,
            x2 = shiftTargetPos.x,
            y2 = shiftTargetPos.y
        };
        _shiftCmd = new ShiftCommand(_keyNoteData, shiftCmdData);

        Debug.Log($"[{gameObject.name}] Key_move：更新Shift参数 → 起始{startTime}s，结束{shiftEndTime}s，目标{targetPos}", this);
    }

    /// <summary>
    /// 手动切换Move指令的JSON路径并重新加载
    /// </summary>
    /// <param name="newJsonPath">新的JSON路径（StreamingAssets下）</param>
    public void ReloadMoveFrames(string newJsonPath)
    {
        moveJsonPath = newJsonPath;
        _moveCmd = new MoveCommand(_keyNoteData, moveJsonPath);
    }

    /// <summary>
    /// 重置Key位置和状态（用于重玩/刷新）
    /// </summary>
    public void ResetKeyState()
    {
        // 重置位置
        _keyNoteData.x = initialPos.x;
        _keyNoteData.y = initialPos.y;
        _keyTransform.position = new Vector3(initialPos.x, initialPos.y, _keyTransform.position.z);

        // 重新初始化指令
        InitCommands();

        Debug.Log($"[{gameObject.name}] Key_move：已重置到初始位置{initialPos}", this);
    }

    /// <summary>
    /// 快捷创建Key物体并挂载Key_move组件
    /// </summary>
    /// <param name="parent">父物体</param>
    /// <param name="keyIndex">Key序号</param>
    /// <param name="initialPos">初始位置</param>
    /// <param name="noteTools">NoteTools实例</param>
    /// <returns>Key_move组件</returns>
    public static Key_move CreateKey(Transform parent, int keyIndex, Vector2 initialPos, NoteTools noteTools)
    {
        GameObject keyObj = new GameObject($"Key_{keyIndex}");
        keyObj.transform.SetParent(parent);
        keyObj.transform.position = new Vector3(initialPos.x, initialPos.y, 0f);

        Key_move keyMove = keyObj.AddComponent<Key_move>();
        keyMove.keyIndex = keyIndex;
        keyMove.initialPos = initialPos;
        keyMove.noteTools = noteTools;

        return keyMove;
    }
    #endregion
}