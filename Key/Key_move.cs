using UnityEngine;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Key移动控制组件：处理Key的shift（匀速移动）和move（JSON帧移动）指令
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
    private Vector2 _currentPos;     // Key当前坐标
    private List<MoveFrame> _moveFrames; // Move指令的帧数据（复用NoteTools的MoveFrame结构）
    private bool _isMoveFramesLoaded = false; // Move帧数据是否加载完成
    #endregion

    #region 生命周期
    private void Awake()
    {
        // 初始化缓存
        _keyTransform = transform;
        
        // 安全校验
        ValidateReferences();

        // 初始化位置
        _currentPos = initialPos;
        _keyTransform.position = new Vector3(initialPos.x, initialPos.y, _keyTransform.position.z);

        // 预加载Move指令的JSON帧数据（如果启用）
        if (useMoveFirst)
        {
            LoadMoveFrames();
        }
    }

    private void Update()
    {
        if (noteTools == null) return;

        // 替换为实际音乐播放时间（如AudioSource.time）
        float currentMusicTime = Time.time;

        // 更新Key位置（优先Move指令，其次Shift）
        UpdateKeyPosition(currentMusicTime);
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
    /// 加载Move指令的JSON帧数据（复用NoteTools的JSON解析逻辑）
    /// </summary>
    private void LoadMoveFrames()
    {
        try
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, moveJsonPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[{gameObject.name}] Move指令：JSON文件不存在 → {fullPath}", this);
                _isMoveFramesLoaded = false;
                return;
            }

            // 读取并解析JSON（复用NoteTools的包装类）
            string jsonContent = File.ReadAllText(fullPath);
            NoteTools.MoveFrameList frameList = JsonUtility.FromJson<NoteTools.MoveFrameList>(jsonContent);
            _moveFrames = frameList?.frames ?? new List<MoveFrame>();

            // 帧数据排序（按时间升序）
            if (_moveFrames.Count > 0)
            {
                _moveFrames.Sort((a, b) => a.time.CompareTo(b.time));
                _isMoveFramesLoaded = true;
                Debug.Log($"[{gameObject.name}] Move指令：成功加载{_moveFrames.Count}帧数据", this);
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] Move指令：JSON文件无帧数据", this);
                _isMoveFramesLoaded = false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[{gameObject.name}] Move指令：解析JSON失败 → {e.Message}", this);
            _isMoveFramesLoaded = false;
        }
    }

    /// <summary>
    /// 处理Shift指令：匀速移动Key（基于起始/结束时间）
    /// </summary>
    /// <param name="currentTime">当前音乐时间（秒）</param>
    private void HandleShift(float currentTime)
    {
        // 未到起始时间 → 保持初始位置
        if (currentTime < shiftStartTime) return;

        // 超过结束时间 → 固定在目标位置
        if (currentTime > shiftEndTime)
        {
            _currentPos = shiftTargetPos;
            _keyTransform.position = new Vector3(shiftTargetPos.x, shiftTargetPos.y, _keyTransform.position.z);
            return;
        }

        // 计算移动进度（0→1），匀速移动
        float progress = (currentTime - shiftStartTime) / (shiftEndTime - shiftStartTime);
        _currentPos = Vector2.Lerp(_currentPos, shiftTargetPos, progress);

        // 应用位置到Transform
        _keyTransform.position = new Vector3(_currentPos.x, _currentPos.y, _keyTransform.position.z);
    }

    /// <summary>
    /// 处理Move指令：按JSON帧数据插值移动Key
    /// </summary>
    /// <param name="currentTime">当前音乐时间（秒）</param>
    private void HandleMove(float currentTime)
    {
        if (!_isMoveFramesLoaded || _moveFrames.Count == 0) return;

        MoveFrame prevFrame = null;
        MoveFrame nextFrame = null;

        // 找到当前时间所在的帧区间
        foreach (var frame in _moveFrames)
        {
            if (frame.time <= currentTime) prevFrame = frame;
            else
            {
                nextFrame = frame;
                break;
            }
        }

        // 边界处理
        if (prevFrame == null)
        {
            // 早于所有帧 → 取第一帧位置
            _currentPos = new Vector2(_moveFrames[0].x, _moveFrames[0].y);
        }
        else if (nextFrame == null)
        {
            // 晚于所有帧 → 取最后一帧位置
            _currentPos = new Vector2(prevFrame.x, prevFrame.y);
        }
        else
        {
            // 帧间插值（匀速）
            float progress = (currentTime - prevFrame.time) / (nextFrame.time - prevFrame.time);
            float x = Mathf.Lerp(prevFrame.x, nextFrame.x, progress);
            float y = Mathf.Lerp(prevFrame.y, nextFrame.y, progress);
            _currentPos = new Vector2(x, y);
        }

        // 应用位置到Transform
        _keyTransform.position = new Vector3(_currentPos.x, _currentPos.y, _keyTransform.position.z);
    }

    /// <summary>
    /// 更新Key位置（优先Move，其次Shift）
    /// </summary>
    /// <param name="currentTime">当前音乐时间（秒）</param>
    private void UpdateKeyPosition(float currentTime)
    {
        if (useMoveFirst && _isMoveFramesLoaded && _moveFrames.Count > 0)
        {
            HandleMove(currentTime);
        }
        else
        {
            HandleShift(currentTime);
        }
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
        Debug.Log($"[{gameObject.name}] Key_move：更新Shift参数 → 起始{startTime}s，结束{shiftEndTime}s，目标{targetPos}", this);
    }

    /// <summary>
    /// 手动切换Move指令的JSON路径并重新加载
    /// </summary>
    /// <param name="newJsonPath">新的JSON路径（StreamingAssets下）</param>
    public void ReloadMoveFrames(string newJsonPath)
    {
        moveJsonPath = newJsonPath;
        LoadMoveFrames();
    }

    /// <summary>
    /// 重置Key位置和状态（用于重玩/刷新）
    /// </summary>
    public void ResetKeyState()
    {
        _currentPos = initialPos;
        _keyTransform.position = new Vector3(initialPos.x, initialPos.y, _keyTransform.position.z);
        _isMoveFramesLoaded = false;

        // 重新加载Move帧数据（如果启用）
        if (useMoveFirst)
        {
            LoadMoveFrames();
        }

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