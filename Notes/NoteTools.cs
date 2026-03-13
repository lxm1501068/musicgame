using System;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

// 判定结果枚举（保留）
[Serializable]
public enum JudgeResult { None, Perfect, Good, Bad, Miss }

// Move指令的单帧数据（JSON解析用）
[Serializable]
public class MoveFrame
{
    public float time;
    public float x;
    public float y;
}

// JSON列表包装类（Unity解析List需要）
[Serializable]
public class MoveFrameList
{
    public List<MoveFrame> frames;
}

#region 1. Shift指令类（速度+方向+终止时间+移动逻辑）
public class ShiftCommand
{
    public float startTime;       // 新增：指令开始时间
    public float speed;           // 移动速度（坐标/秒）
    public Vector2 direction;     // 移动方向（归一化）
    public float endTime;         // 移动终止时间（音乐时间）
    public NoteData note;         // 关联音符（仅操作x/y）

    // 构造函数：从Command解析参数
    public ShiftCommand(NoteData note, Command cmd)
    {
        this.note = note;
        this.startTime = cmd.timeA;   // 记录开始时间
        this.endTime = cmd.timeB;

        // 计算方向和速度
        Vector2 startPos = new Vector2(cmd.x1, cmd.y1);
        Vector2 targetPos = new Vector2(cmd.x2, cmd.y2);
        Vector2 moveDistance = targetPos - startPos;
        this.direction = moveDistance.normalized;

        float moveDuration = cmd.timeB - cmd.timeA;
        if (moveDuration <= 0) moveDuration = 0.01f; // 防除0
        this.speed = moveDistance.magnitude / moveDuration;
    }

    // 每帧更新位置（仅操作note.x/note.y）
    public void UpdateNotePosition(float currentTime, float deltaTime)
    {
        if (note == null || currentTime < startTime || currentTime > endTime) return; // 增加开始时间检查
        // Debug.Log($"[ShiftCommand] 更新位置，currentTime={currentTime}, startTime={startTime}, endTime={endTime}, speed={speed}, direction={direction}");
        // 计算单帧移动距离
        Vector2 frameMove = direction * speed * deltaTime;
        note.x += frameMove.x;
        note.y += frameMove.y;

        // 边界处理：到终止时间直接跳到目标位置
        if (currentTime + deltaTime >= endTime)
        {
            float remainTime = endTime - currentTime;
            note.x += direction.x * speed * remainTime;
            note.y += direction.y * speed * remainTime;
        }
    }
}
#endregion

#region 2. DropTo指令类（继承Shift+判定逻辑）
public class DropToCommand : ShiftCommand
{
    // 判定阈值（可外部配置）
    public float perfectThreshold = 0.1f;
    public float goodThreshold = 0.2f;
    public float badThreshold = 0.3f;
    public int KeyIndex;

    // 判定结果移到这里
    public JudgeResult judgeResult = JudgeResult.None;

    // 构造函数
    public DropToCommand(NoteData note, Command cmd, int KeyIndex) : base(note, cmd)
    {
        this.note = note; // 关联NoteData
        this.endTime = cmd.timeB; // 判定基准时间
        this.KeyIndex = KeyIndex; // 绑定按键序号
        // startTime 已在基类中设置
    }

    // 判定方法（结果存在当前类的judgeResult）
    public void Judge(float currentTime, int KeyIndex)
    {
        if (note == null || judgeResult != JudgeResult.None) return;
        // 可选的开始时间检查，判定通常在 endTime 附近进行，但如果当前时间远小于 startTime，不应该判定
        if (currentTime < startTime) return;

        float timeDiff = currentTime - endTime;
        bool isKeyPressed = InputManager.Instance.IsGroupPressed(KeyIndex);

        // 判定逻辑
        if (isKeyPressed)
        {
            if (Mathf.Abs(timeDiff) <= perfectThreshold)
                judgeResult = JudgeResult.Perfect;
            else if (Mathf.Abs(timeDiff) <= goodThreshold)
                judgeResult = JudgeResult.Good;
            else if (timeDiff >= -badThreshold && timeDiff < -goodThreshold)
                judgeResult = JudgeResult.Bad;
        }
        else if (timeDiff > goodThreshold)
        {
            judgeResult = JudgeResult.Miss;
        }
    }
}
#endregion

#region 3. Move指令类（JSON帧数据+插值移动）
public class MoveCommand
{
    public float startTime;       // 新增：指令开始时间
    public string jsonPath;       // JSON路径
    public NoteData note;         // 关联音符
    private List<MoveFrame> _frames; // 帧数据

    // 构造函数：改为接受 Command，从中提取开始时间和文件名
    public MoveCommand(NoteData note, Command cmd)
    {
        this.note = note;
        this.startTime = cmd.timeA;          // 记录开始时间
        this.jsonPath = cmd.json_filename;
        this._frames = LoadMoveFrames();     // 加载帧数据
        if (_frames != null) _frames.Sort((a, b) => a.time.CompareTo(b.time));
    }

    // 加载JSON帧数据
    private List<MoveFrame> LoadMoveFrames()
    {
        try
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, jsonPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"JSON不存在：{fullPath}");
                return null;
            }

            string jsonContent = File.ReadAllText(fullPath);
            MoveFrameList frameList = JsonUtility.FromJson<MoveFrameList>(jsonContent);
            return frameList.frames;
        }
        catch (Exception e)
        {
            Debug.LogError($"解析JSON失败：{e.Message}");
            return null;
        }
    }

    // 每帧更新位置（仅操作note.x/note.y）
    public void UpdateNotePosition(float currentTime)
    {
        if (note == null || _frames == null || _frames.Count == 0 || currentTime < startTime) return; // 增加开始时间检查

        MoveFrame prevFrame = null;
        MoveFrame nextFrame = null;

        // 找当前时间的帧区间
        foreach (var frame in _frames)
        {
            if (frame.time <= currentTime) prevFrame = frame;
            else { nextFrame = frame; break; }
        }

        // 边界处理+插值更新x/y
        if (prevFrame == null)
        {
            note.x = _frames[0].x;
            note.y = _frames[0].y;
        }
        else if (nextFrame == null)
        {
            note.x = prevFrame.x;
            note.y = prevFrame.y;
        }
        else
        {
            float progress = (currentTime - prevFrame.time) / (nextFrame.time - prevFrame.time);
            note.x = Mathf.Lerp(prevFrame.x, nextFrame.x, progress);
            note.y = Mathf.Lerp(prevFrame.y, nextFrame.y, progress);
        }
    }
}
#endregion

#region 4. 单例版NoteTools（仅作为指令工厂）
public class NoteTools : MonoBehaviour
{
    // ===== 核心：单例实现 =====
    public static NoteTools Instance { get; private set; }

    // 单例初始化（保证全局唯一）
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 跨场景保留（可选，根据你的游戏流程）
        }
        else
        {
            Destroy(gameObject); // 重复实例直接销毁
        }
    }
}
#endregion