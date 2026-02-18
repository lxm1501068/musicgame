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

// ===== 核心：仅保留4个字段的NoteData =====
[Serializable]
public class NoteData
{
    public int keyIndex;          // 按键序号
    public float x;               // X坐标
    public float y;               // Y坐标
    public bool isVisible = true; // 是否显示
}

#region 1. Shift指令类（速度+方向+终止时间+移动逻辑）
public class ShiftCommand
{
    public float speed;          // 移动速度（坐标/秒）
    public Vector2 direction;    // 移动方向（归一化）
    public float endTime;        // 移动终止时间（音乐时间）
    public NoteData note;        // 关联音符（仅操作x/y）

    // 构造函数：从Command解析参数
    public ShiftCommand(NoteData note, Command cmd)
    {
        this.note = note;
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
    public void UpdatePosition(float currentTime, float deltaTime)
    {
        if (note == null || currentTime > endTime) return;

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

    // 判定结果移到这里（不再依赖NoteData）
    public JudgeResult judgeResult = JudgeResult.None;
    public InputManager input;    // 输入检测

    // 构造函数
    public DropToCommand(NoteData note, Command cmd, InputManager input) : base(note, cmd)
    {
        this.input = input;
        note.keyIndex = cmd.num; // 绑定按键序号
    }

    // 判定方法（结果存在当前类的judgeResult）
    public void Judge(float currentTime)
    {
        if (note == null || judgeResult != JudgeResult.None) return;

        float timeDiff = currentTime - endTime;
        bool isKeyPressed = input.IsGroupPressed(note.keyIndex);

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
    public string jsonPath;      // JSON路径
    public NoteData note;        // 关联音符
    private List<MoveFrame> _frames; // 帧数据移到这里（不再依赖NoteData）

    // 构造函数
    public MoveCommand(NoteData note, string jsonPath)
    {
        this.note = note;
        this.jsonPath = jsonPath;
        this._frames = LoadMoveFrames(); // 加载帧数据
        if (_frames != null) _frames.Sort((a, b) => a.time.CompareTo(b.time));
    }

    // 加载JSON帧数据（不再调用NoteData的SetMoveFrames）
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
    public void UpdatePosition(float currentTime)
    {
        if (note == null || _frames == null || _frames.Count == 0) return;

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

#region 4. 简化的NoteTools（仅管理指令实例）
public class NoteTools : MonoBehaviour
{
    public InputManager input; // 输入检测类
    // 指令实例列表
    private List<ShiftCommand> _shiftCommands = new List<ShiftCommand>();
    private List<DropToCommand> _dropToCommands = new List<DropToCommand>();
    private List<MoveCommand> _moveCommands = new List<MoveCommand>();

    // 创建Shift指令
    public void CreateShiftCommand(NoteData note, Command cmd)
    {
        _shiftCommands.Add(new ShiftCommand(note, cmd));
    }

    // 创建DropTo指令
    public void CreateDropToCommand(NoteData note, Command cmd)
    {
        _dropToCommands.Add(new DropToCommand(note, cmd, input));
    }

    // 创建Move指令
    public void CreateMoveCommand(NoteData note, string jsonPath)
    {
        _moveCommands.Add(new MoveCommand(note, jsonPath));
    }

    // 每帧驱动所有指令
    private void Update()
    {
        float currentTime = GetCurrentMusicTime(); // 替换为你的音乐时间逻辑
        float deltaTime = Time.deltaTime;

        // 更新Shift指令
        foreach (var cmd in _shiftCommands) cmd.UpdatePosition(currentTime, deltaTime);

        // 更新DropTo指令（移动+判定）
        foreach (var cmd in _dropToCommands)
        {
            cmd.UpdatePosition(currentTime, deltaTime);
            cmd.Judge(currentTime);
            // 如需获取判定结果，可从cmd.judgeResult读取
            // 示例：if (cmd.judgeResult == JudgeResult.Perfect) { ... }
        }

        // 更新Move指令
        foreach (var cmd in _moveCommands) cmd.UpdatePosition(currentTime);
    }

    // 示例：获取音乐当前时间（需替换为AudioSource.time）
    private float GetCurrentMusicTime()
    {
        return Time.time; // 临时用系统时间，实际需改
    }
}
#endregion