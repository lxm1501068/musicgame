using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 装饰性音符线管理器
/// 用于管理一组具有装饰效果（spin、shift 等）的音符，形成视觉图案
/// </summary>
[System.Serializable]
public class Line
{
    [Header("基础信息")]
    public int lineIndex;              // Line 序号
    public string lineName;            // Line 名称（可选，用于标识）
    
    [Header("音符配置")]
    public List<int> noteIndices;      // 包含的音符序号列表
    public float startTime;            // Line 开始时间
    public float endTime;              // Line 结束时间
    
    [Header("装饰效果")]
    public bool useSpin = true;        // 是否启用旋转效果
    public bool useShift = true;       // 是否启用位移效果
    public float spinSpeed = 36f;      // 旋转速度（度/秒）
    public Vector2 shiftAmplitude = new Vector2(1f, 0f);  // 位移幅度
    
    [Header("图案类型")]
    public LinePattern patternType = LinePattern.Spiral;  // 图案类型
    
    /// <summary>
    /// Line 图案类型
    /// </summary>
    public enum LinePattern
    {
        Spiral,           // 螺旋形
        Wave,             // 波浪形
        Sine,             // 正弦曲线
        Circle,           // 圆形
        Custom            // 自定义
    }
    
    /// <summary>
    /// 初始化 Line
    /// </summary>
    public Line(int index, List<int> indices, float start, float end)
    {
        this.lineIndex = index;
        this.noteIndices = indices;
        this.startTime = start;
        this.endTime = end;
    }
    
    /// <summary>
    /// 为音符应用装饰效果
    /// </summary>
    public void ApplyDecorations(List<Command> commands, NoteType noteType = NoteType.Hold)
    {
        if (commands == null || noteIndices == null) return;
        
        float duration = endTime - startTime;
        
        foreach (int noteIdx in noteIndices)
        {
            // 查找该音符的所有命令
            var noteCommands = commands.FindAll(c => c.num == noteIdx);
            if (noteCommands.Count == 0) continue;
            
            Command mainCmd = noteCommands[0];
            
            // 添加 spin 指令
            if (useSpin)
            {
                Command spinCmd = new Command
                {
                    is_show = true,
                    type = noteType,
                    num = noteIdx,
                    timeA = startTime,
                    timeB = endTime,
                    x1 = GetInitialDirection(noteIdx),  // 初始方向
                    y1 = spinSpeed,                      // 旋转速度
                    commandName = "spin",
                    isNoteFirstTimeOccured = false
                };
                commands.Add(spinCmd);
            }
            
            // 添加 shift 指令（根据图案类型）
            if (useShift)
            {
                AddShiftCommands(commands, noteIdx, noteType, mainCmd, duration);
            }
        }
    }
    
    /// <summary>
    /// 根据图案类型添加 shift 指令
    /// </summary>
    private void AddShiftCommands(List<Command> commands, int noteIdx, NoteType noteType, 
                                  Command mainCmd, float duration)
    {
        switch (patternType)
        {
            case LinePattern.Spiral:
                AddSpiralShift(commands, noteIdx, noteType, mainCmd, duration);
                break;
            case LinePattern.Wave:
                AddWaveShift(commands, noteIdx, noteType, mainCmd, duration);
                break;
            case LinePattern.Sine:
                AddSineShift(commands, noteIdx, noteType, mainCmd, duration);
                break;
        }
    }
    
    /// <summary>
    /// 添加螺旋形 shift
    /// </summary>
    private void AddSpiralShift(List<Command> commands, int noteIdx, NoteType noteType, 
                                Command baseCmd, float duration)
    {
        // 分段创建曲线路径
        float segmentDuration = duration / 2f;
        
        // 第一段：向左偏移
        Command shift1 = CreateShiftCommand(noteIdx, noteType, 
            baseCmd.timeA, baseCmd.timeA + segmentDuration,
            baseCmd.x1, baseCmd.y1,
            baseCmd.x1 - shiftAmplitude.x, baseCmd.y1 + shiftAmplitude.y * 0.5f);
        commands.Add(shift1);
        
        // 第二段：回到原位并继续
        Command shift2 = CreateShiftCommand(noteIdx, noteType,
            baseCmd.timeA + segmentDuration, baseCmd.timeB,
            baseCmd.x1 - shiftAmplitude.x, baseCmd.y1 + shiftAmplitude.y * 0.5f,
            baseCmd.x2, baseCmd.y2);
        commands.Add(shift2);
    }
    
    /// <summary>
    /// 添加波浪形 shift
    /// </summary>
    private void AddWaveShift(List<Command> commands, int noteIdx, NoteType noteType,
                              Command baseCmd, float duration)
    {
        float segmentDuration = duration / 3f;
        
        // 向右
        Command shift1 = CreateShiftCommand(noteIdx, noteType,
            baseCmd.timeA, baseCmd.timeA + segmentDuration,
            baseCmd.x1, baseCmd.y1,
            baseCmd.x1 + shiftAmplitude.x, baseCmd.y1 + shiftAmplitude.y * 0.33f);
        commands.Add(shift1);
        
        // 向左
        Command shift2 = CreateShiftCommand(noteIdx, noteType,
            baseCmd.timeA + segmentDuration, baseCmd.timeA + segmentDuration * 2,
            baseCmd.x1 + shiftAmplitude.x, baseCmd.y1 + shiftAmplitude.y * 0.33f,
            baseCmd.x1 - shiftAmplitude.x, baseCmd.y1 + shiftAmplitude.y * 0.66f);
        commands.Add(shift2);
        
        // 回到中间
        Command shift3 = CreateShiftCommand(noteIdx, noteType,
            baseCmd.timeA + segmentDuration * 2, baseCmd.timeB,
            baseCmd.x1 - shiftAmplitude.x, baseCmd.y1 + shiftAmplitude.y * 0.66f,
            baseCmd.x2, baseCmd.y2);
        commands.Add(shift3);
    }
    
    /// <summary>
    /// 添加正弦曲线 shift
    /// </summary>
    private void AddSineShift(List<Command> commands, int noteIdx, NoteType noteType,
                              Command baseCmd, float duration)
    {
        int segments = 4;
        float segmentDuration = duration / segments;
        
        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)segments;
            float nextT = (i + 1) / (float)segments;
            
            // 计算正弦偏移
            float offset = Mathf.Sin(t * Mathf.PI * 2) * shiftAmplitude.x;
            float nextOffset = Mathf.Sin(nextT * Mathf.PI * 2) * shiftAmplitude.x;
            
            float startY = Mathf.Lerp(baseCmd.y1, baseCmd.y2, t);
            float endY = Mathf.Lerp(baseCmd.y1, baseCmd.y2, nextT);
            
            Command shift = CreateShiftCommand(noteIdx, noteType,
                baseCmd.timeA + segmentDuration * i,
                baseCmd.timeA + segmentDuration * (i + 1),
                baseCmd.x1 + offset, startY,
                baseCmd.x1 + nextOffset, endY);
            commands.Add(shift);
        }
    }
    
    /// <summary>
    /// 创建 shift 命令
    /// </summary>
    private Command CreateShiftCommand(int noteIdx, NoteType noteType, 
                                       float timeA, float timeB,
                                       float x1, float y1, float x2, float y2)
    {
        return new Command
        {
            is_show = true,
            type = noteType,
            num = noteIdx,
            timeA = timeA,
            timeB = timeB,
            x1 = x1,
            y1 = y1,
            x2 = x2,
            y2 = y2,
            commandName = "shift",
            isNoteFirstTimeOccured = false
        };
    }
    
    /// <summary>
    /// 获取初始方向角度
    /// </summary>
    private float GetInitialDirection(int noteIdx)
    {
        // 根据音符序号返回不同的初始角度
        return (noteIdx % 4) * 90f;
    }
    
    /// <summary>
    /// 从谱面数据创建 Line 对象
    /// </summary>
    public static Line CreateFromChartData(int lineIndex, float startTime, float endTime, 
                                           List<int> noteIndices = null)
    {
        Line line = new Line(lineIndex, noteIndices ?? new List<int>(), startTime, endTime);
        line.lineName = $"Line_{lineIndex}";
        return line;
    }
}
