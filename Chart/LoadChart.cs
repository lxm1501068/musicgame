using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

// 轨道按键示例注释行（可用按键名，音符数，指令数，总时长）
// 0,1,2,3
// 6
// 11
// 28.000
// (key_name x y show)
// 0 5 -5 1
// 1 4 -4 1
// 2 3 -3 1
// 3 2 -2 1

// 谱面示例注释行（#：记分音符，!：不计分音符，%：非音符首次出现，$：按键指令）(note num command time_a time_b x1 y1 x2 y2 (.json file or hold_duration))
// 谱面
// # tap 1 drop_to 0 14.000 16.000 4 -4 5 -5
// ! tap 2 shift 14.000 16.000 4 -4 5 -5
// % tap 2 destroy 16.000
// # tap 3 move 14.000 16.000 move_1
// # hold 4 drop_to 1 18.000 20.000 4 -4 5 -5 3
// # dtap 5 drop_to 0 20.000 22.000 4 -4 5 -5
// # flick 6 22.000 24.000
// $ key 0 hide 24.000
// $ key 1 shift 24.000 26.000 4 -4 3 -3
// $ key 0 show 26.000
// $ key 2 move 26.000 28.000 move_2

public class LoadChart : MonoBehaviour
{
    private string chartContent; // 加载的谱面文本内容
    private List<int> keyIds;    // 轨道按键 ID 列表（轨道按键部分解析）
    private int noteCount;       // 解析到的音符总数
    private int cmdCount;        // 解析到的指令总数
    private float totalDuration; // 解析到的谱面总时长
    private List<string> _spectrumLines; // 本地缓存谱面行（替代 ChartData 的 SpectrumLines）
    private List<Line> parsedLines = new List<Line>(); // 解析到的 Line 列表
    
    // 兼容原有访问逻辑：轨道按键数=列表长度
    public int KeyCount => keyIds?.Count ?? 0;
    // 对外提供轨道按键 ID 列表
    public List<int> KeyIds => keyIds ?? new List<int>();
    public string ChartContent => chartContent;

    /// <summary>
    /// 异步加载谱面文件（核心修改：适配async/await + 加载成功后执行ParseChart）
    /// </summary>
    /// <param name="fileName">谱面文件名（如chart.txt）</param>
    /// <returns>是否加载成功</returns>
    public async Task<bool> LoadChartFileAsync(string fileName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        Debug.Log($"开始加载谱面：{path}");
        
        try
        {
            // PC端异步读取文件
            if (!File.Exists(path))
            {
                Debug.LogError($"谱面文件不存在：{path}");
                return false;
            }

            // 异步读取文件
            chartContent = await File.ReadAllTextAsync(path);
            
            // 加载成功后预处理头部和行数据（保留原有逻辑）
            if (!SplitChartContent(out List<string> trackKeyLines, out List<string> spectrumLines))
            {
                return false;
            }
            ParseChartHeader(trackKeyLines);
            _spectrumLines = spectrumLines;
            
            // 核心修复：异步加载成功后主动执行ParseChart解析谱面
            ParseChart();
            
            // 日志调整：打印轨道按键列表+数量
            Debug.Log($"谱面加载完成！轨道按键列表：[{string.Join(",", KeyIds)}] | 轨道按键数：{KeyCount} | 音符数：{noteCount} | 指令数：{cmdCount} | 总时长：{totalDuration}s");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"加载谱面异常：{e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// 兼容旧协程接口（可选保留，若有其他地方调用）
    /// </summary>
    public IEnumerator LoadChartFile(string fileName)
    {
        bool loadSuccess = false;
        // 异步转协程适配
        var loadTask = LoadChartFileAsync(fileName);
        while (!loadTask.IsCompleted)
        {
            yield return null;
        }
        loadSuccess = loadTask.Result;

        if (loadSuccess)
        {
            try
            {
                // 注：由于异步方法内已调用ParseChart，此处可注释避免重复解析
                // ParseChart(); // 解析谱面内容到ChartData
            }
            catch (Exception e)
            {
                Debug.LogError($"解析谱面异常：{e.Message}\n{e.StackTrace}");
            }
        }
    }

    /// <summary>
    /// 修正：拆分轨道头部4行 + 按键初始状态行 + 谱面指令行
    /// </summary>
    private bool SplitChartContent(out List<string> trackKeyLines, out List<string> spectrumLines)
    {
        trackKeyLines = new List<string>();
        spectrumLines = new List<string>();
        
        if (string.IsNullOrEmpty(chartContent))
        {
            Debug.LogError("SplitChartContent: 谱面内容为空");
            return false;
        }

        // 读取所有行，仅去除换行符，保留原始空白
        var allLines = chartContent.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .ToList();

        // ========== 步骤1：读取轨道头部4行（轨道ID、指令数、音符数、总时长） ==========
        int lineIndex = 0;
        lineIndex++; // 跳过第一行（注释行）

        // 读取前4个非空行作为轨道头部
        int headerLineCount = 0;
        for (; lineIndex < allLines.Count && headerLineCount < 4; lineIndex++)
        {
            string line = allLines[lineIndex].Trim();
            if (string.IsNullOrEmpty(line)) continue; // 跳过空行
            trackKeyLines.Add(line);
            headerLineCount++;
        }

        if (trackKeyLines.Count != 4)
        {
            Debug.LogError($"SplitChartContent: 轨道头部行数异常，期望4行，实际{trackKeyLines.Count}行");
            return false;
        }

        // ========== 步骤2：找到「谱面」分割符，将其之前（头部之后）的作为按键状态，之后的作为指令 ==========
        int spectrumMarkerIndex = -1;
        for (int i = lineIndex; i < allLines.Count; i++)
        {
            if (allLines[i].Trim() == "谱面")
            {
                spectrumMarkerIndex = i;
                break;
            }
        }

        if (spectrumMarkerIndex == -1)
        {
            Debug.LogError("SplitChartContent: 未找到「谱面」分割符");
            return false;
        }

        // 头部之后到「谱面」之前的行：按键初始状态
        for (int i = lineIndex; i < spectrumMarkerIndex; i++)
        {
            string line = allLines[i].Trim();
            if (!string.IsNullOrEmpty(line))
            {
                spectrumLines.Add(line);
            }
        }

        // 「谱面」之后的行：指令
        for (int i = spectrumMarkerIndex; i < allLines.Count; i++)
        {
            string line = allLines[i].Trim();
            if (!string.IsNullOrEmpty(line))
            {
                spectrumLines.Add(line);
            }
        }

        return true;
    }

    /// <summary>
    /// 修正：适配轨道头部顺序（第2行=指令数，第3行=音符数）
    /// </summary>
    private void ParseChartHeader(List<string> trackKeyLines)
    {
        keyIds = new List<int>();
        
        if (trackKeyLines.Count != 4)
        {
            Debug.LogError($"ParseChartHeader: 轨道按键行数不足（需4行），实际行数：{trackKeyLines.Count}");
            return;
        }

        // 第一行：轨道按键列表（空格/逗号分隔兼容）
        ParseKeyIds(trackKeyLines[0]);
        
        // 第二行：指令数（修正！原代码这里解析音符数，顺序错了）
        if (int.TryParse(trackKeyLines[1], out int cmdCnt)) cmdCount = cmdCnt;
        else Debug.LogError($"ParseChartHeader: 指令数解析失败，行内容：{trackKeyLines[1]}");
        
        // 第三行：音符数（修正！原代码这里解析指令数，顺序错了）
        if (int.TryParse(trackKeyLines[2], out int noteCnt)) noteCount = noteCnt;
        else Debug.LogError($"ParseChartHeader: 音符数解析失败，行内容：{trackKeyLines[2]}");
        
        // 第四行：总时长
        if (float.TryParse(trackKeyLines[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float totalTime))
            totalDuration = totalTime;
        else Debug.LogError($"ParseChartHeader: 总时长解析失败，行内容：{trackKeyLines[3]}");
    }
    
    /// <summary>
    /// 解析谱面设置信息（BPM、拍号、小节等）
    /// </summary>
    private void ParseChartSettings(List<string> spectrumLines)
    {
        // 重置默认值
        ChartData.Instance.defaultBpm = 120f;
        ChartData.Instance.defaultBeatsPerMeasure = 4;
        ChartData.Instance.defaultBeatUnit = 4;
        ChartData.Instance.measures.Clear();
        
        // 查找并解析设置行
        for (int i = 0; i < spectrumLines.Count; i++)
        {
            string line = spectrumLines[i].Trim();
            
            // 解析默认 BPM
            if (line.StartsWith("BPM:"))
            {
                string bpmStr = line.Substring(4).Trim();
                if (float.TryParse(bpmStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float bpm))
                {
                    ChartData.Instance.defaultBpm = bpm;
                    Debug.Log($"加载默认 BPM: {bpm}");
                }
            }
            // 解析默认拍号
            else if (line.StartsWith("TimeSignature:"))
            {
                string tsStr = line.Substring(14).Trim();
                string[] parts = tsStr.Split('/');
                if (parts.Length == 2 && 
                    int.TryParse(parts[0], out int beatsPerMeasure) && 
                    int.TryParse(parts[1], out int beatUnit))
                {
                    ChartData.Instance.defaultBeatsPerMeasure = beatsPerMeasure;
                    ChartData.Instance.defaultBeatUnit = beatUnit;
                    Debug.Log($"加载默认拍号: {beatsPerMeasure}/{beatUnit}");
                }
            }
            // 解析小节数量
            else if (line.StartsWith("Measures:"))
            {
                string measureCountStr = line.Substring(9).Trim();
                if (int.TryParse(measureCountStr, out int measureCount))
                {
                    Debug.Log($"加载小节数量: {measureCount}");
                }
            }
            // 解析段落数量
            else if (line.StartsWith("Sections:"))
            {
                string sectionCountStr = line.Substring(9).Trim();
                if (int.TryParse(sectionCountStr, out int sectionCount))
                {
                    Debug.Log($"加载段落数量: {sectionCount}");
                }
            }
            // 解析每个段落的详细信息
            else if (line.StartsWith("Section:"))
            {
                // 格式: Section:0-4 BPM:120.00 TimeSig:4/4
                try
                {
                    var parts = line.Split(' ');
                    if (parts.Length >= 4)
                    {
                        // 解析小节范围 "0-4"
                        string rangeStr = parts[0].Substring(8); // "Section:" 后是范围
                        string[] rangeParts = rangeStr.Split('-');
                        if (rangeParts.Length == 2 && 
                            int.TryParse(rangeParts[0], out int startMeasure) && 
                            int.TryParse(rangeParts[1], out int endMeasure))
                        {
                            float bpm = ChartData.Instance.defaultBpm;
                            if (parts[1].StartsWith("BPM:"))
                            {
                                float.TryParse(parts[1].Substring(4), NumberStyles.Float, CultureInfo.InvariantCulture, out bpm);
                            }
                            
                            int beatsPerMeasure = ChartData.Instance.defaultBeatsPerMeasure;
                            int beatUnit = ChartData.Instance.defaultBeatUnit;
                            if (parts[2].StartsWith("TimeSig:"))
                            {
                                string[] tsParts = parts[2].Substring(8).Split('/');
                                if (tsParts.Length == 2)
                                {
                                    int.TryParse(tsParts[0], out beatsPerMeasure);
                                    int.TryParse(tsParts[1], out beatUnit);
                                }
                            }
                            
                            // 为该段落范围内的所有小节创建 MeasureData
                            for (int m = startMeasure; m <= endMeasure; m++)
                            {
                                var measure = new MeasureData(m, bpm, beatsPerMeasure, beatUnit);
                                ChartData.Instance.measures.Add(measure);
                            }
                            
                            Debug.Log($"加载段落: 小节 {startMeasure}-{endMeasure}, BPM: {bpm}, 拍号: {beatsPerMeasure}/{beatUnit}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"解析段落数据失败: {line}, 错误: {ex.Message}");
                }
            }
        }
        
        // 如果没有找到段落数据，尝试查找旧格式的 Measure 数据（向后兼容）
        if (ChartData.Instance.measures.Count == 0)
        {
            for (int i = 0; i < spectrumLines.Count; i++)
            {
                string line = spectrumLines[i].Trim();
                if (line.StartsWith("Measure:"))
                {
                    try
                    {
                        var parts = line.Split(' ');
                        if (parts.Length >= 4)
                        {
                            int measureIndex = int.Parse(parts[0].Substring(8));
                            
                            float bpm = ChartData.Instance.defaultBpm;
                            if (parts[1].StartsWith("BPM:"))
                            {
                                float.TryParse(parts[1].Substring(4), NumberStyles.Float, CultureInfo.InvariantCulture, out bpm);
                            }
                            
                            int beatsPerMeasure = ChartData.Instance.defaultBeatsPerMeasure;
                            int beatUnit = ChartData.Instance.defaultBeatUnit;
                            if (parts[2].StartsWith("TimeSig:"))
                            {
                                string[] tsParts = parts[2].Substring(8).Split('/');
                                if (tsParts.Length == 2)
                                {
                                    int.TryParse(tsParts[0], out beatsPerMeasure);
                                    int.TryParse(tsParts[1], out beatUnit);
                                }
                            }
                            
                            var measure = new MeasureData(measureIndex, bpm, beatsPerMeasure, beatUnit);
                            ChartData.Instance.measures.Add(measure);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"解析小节数据失败: {line}, 错误: {ex.Message}");
                    }
                }
            }
        }
        
        // 如果仍然没有小节数据，根据默认值生成
        if (ChartData.Instance.measures.Count == 0 && ChartData.Instance.totalDuration > 0)
        {
            float measureDuration = (60f / ChartData.Instance.defaultBpm) * ChartData.Instance.defaultBeatsPerMeasure;
            int measureCount = Mathf.CeilToInt(ChartData.Instance.totalDuration / measureDuration);
            
            for (int i = 0; i < measureCount; i++)
            {
                var measure = new MeasureData(i, ChartData.Instance.defaultBpm, 
                    ChartData.Instance.defaultBeatsPerMeasure, ChartData.Instance.defaultBeatUnit);
                ChartData.Instance.measures.Add(measure);
            }
            
            Debug.Log($"自动生成 {measureCount} 个小节数据");
        }
        
        // 按小节索引排序
        ChartData.Instance.measures.Sort((a, b) => a.measureIndex.CompareTo(b.measureIndex));
        
        Debug.Log($"谱面设置加载完成 | 默认BPM: {ChartData.Instance.defaultBpm} | 默认拍号: {ChartData.Instance.defaultBeatsPerMeasure}/{ChartData.Instance.defaultBeatUnit} | 小节数: {ChartData.Instance.measures.Count}");
    }

    /// <summary>
    /// 解析轨道按键ID列表（兼容逗号/空格分隔）
    /// </summary>
    private void ParseKeyIds(string keyIdLine)
    {
        if (string.IsNullOrEmpty(keyIdLine))
        {
            Debug.LogError("ParseKeyIds: 轨道按键列表行为空");
            return;
        }
        
        // 兼容逗号/空格分隔
        var keyIdStrs = keyIdLine.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var str in keyIdStrs)
        {
            if (int.TryParse(str.Trim(), out int keyId))
            {
                keyIds.Add(keyId);
            }
            else
            {
                Debug.LogWarning($"ParseKeyIds: 轨道按键ID解析失败，内容：{str}，行：{keyIdLine}");
            }
        }
        
        // 去重+排序
        keyIds = keyIds.Distinct().ToList();
        keyIds.Sort();
    }

    public void ParseChart()
    {
        if (string.IsNullOrEmpty(chartContent) || _spectrumLines == null || _spectrumLines.Count == 0)
        {
            Debug.LogError("ParseChart: 谱面内容为空，无法解析");
            return;
        }
            
        // 解析前先重置数据，避免切换谱面时数据残留
        ChartData.Instance.ResetChartData();
        // 赋值轨道按键列表 + 数量到 ChartData
        ChartData.Instance.keyIds = this.KeyIds;
        ChartData.Instance.totalDuration = totalDuration;
        
        // 解析谱面设置（BPM、拍号、小节等）
        ParseChartSettings(_spectrumLines);
            
        // 使用本地缓存的谱面行解析
        ParseKeyInitialState(_spectrumLines);
        ParseCommands(_spectrumLines);
        ParseLines(_spectrumLines);  // 新增：解析 Line 数据
        ChartData.Instance.SortCommandsByTime();
            
        // 应用所有 Line 的装饰效果
        ChartData.Instance.ApplyLineDecorations();
            
        Debug.Log($"LoadChart: 谱面解析完成：KeyData 数={ChartData.Instance.keyDatas.Count} | Command 数={ChartData.Instance.commands.Count} | Line 数={ChartData.Instance.lines.Count}");
        return;
    }

    /// <summary>
    /// 优化：解析按键初始状态
    /// </summary>
    private void ParseKeyInitialState(List<string> spectrumLines)
    {
        int keyStateStartIndex = -1;
        for (int i = 0; i < spectrumLines.Count; i++)
        {
            if (spectrumLines[i].StartsWith("(key_name x y show)"))
            {
                keyStateStartIndex = i + 1;
                break;
            }
        }

        // 如果没找到标记行，尝试从头开始（直到遇到“谱面”）
        if (keyStateStartIndex == -1)
        {
            keyStateStartIndex = 0;
        }

        int parsedCount = 0;
        for (int i = keyStateStartIndex; i < spectrumLines.Count; i++)
        {
            string line = spectrumLines[i];
            if (line == "谱面" || line.StartsWith("(note num command")) break;
            
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4)
            {
                if (int.TryParse(parts[0], out int keyName) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                    int.TryParse(parts[3], out int show))
                {
                    ChartData.Instance.keyDatas.Add(new KeyData(keyName, x, y, show));
                    parsedCount++;
                }
            }
        }
    }

    /// <summary>
    /// 解析谱面指令
    /// </summary>
    private void ParseCommands(List<string> spectrumLines)
    {
        int commandStartIndex = -1;
        // 优先寻找“谱面”标记
        for (int i = 0; i < spectrumLines.Count; i++)
        {
            if (spectrumLines[i] == "谱面")
            {
                commandStartIndex = i + 1;
                break;
            }
        }
        
        // 如果没找到“谱面”，再寻找指令列定义标记
        if (commandStartIndex == -1)
        {
            for (int i = 0; i < spectrumLines.Count; i++)
            {
                if (spectrumLines[i].StartsWith("(note num command"))
                {
                    commandStartIndex = i + 1;
                    break;
                }
            }
        }
        
        if (commandStartIndex == -1)
        {
            Debug.LogError("ParseCommands: 未能找到指令起始位置（缺少“谱面”或指令标记行）");
            return;
        }
        
        for (int i = commandStartIndex; i < spectrumLines.Count; i++)
        {
            string line = spectrumLines[i];
            if (string.IsNullOrEmpty(line) || line.StartsWith("(")) continue;
            
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;
            
            switch (parts[0])
            {
                case "#":
                    ParseNoteCommand(parts, isScorable: true, isNoteFirstTimeOccured: true);
                    break;
                case "!":
                    ParseNoteCommand(parts, isScorable: false, isNoteFirstTimeOccured: true);
                    break;
                case "%":
                    ParseNoteCommand(parts, isScorable: null, isNoteFirstTimeOccured: false);
                    break;
                case "$":
                    ParseKeyMoveCommand(parts);
                    break;
            }
        }
    }

    /// <summary>
    /// 解析 Line 数据（装饰性音符线）
    /// </summary>
    private void ParseLines(List<string> spectrumLines)
    {
        int lineStartIndex = -1;
        
        // 寻找 Line 标记行："// Line" 或 "Line:"
        for (int i = 0; i < spectrumLines.Count; i++)
        {
            string line = spectrumLines[i].Trim();
            if (line.StartsWith("// Line") || line.StartsWith("Line:"))
            {
                lineStartIndex = i + 1;
                break;
            }
        }
        
        if (lineStartIndex == -1)
        {
            Debug.Log("ParseLines: 未找到 Line 定义，跳过解析");
            return;
        }
        
        // 解析 Line 定义
        for (int i = lineStartIndex; i < spectrumLines.Count; i++)
        {
            string line = spectrumLines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("//")) continue;
            if (line.StartsWith("谱面") || line.StartsWith("(note")) break;
            
            // 尝试解析 Line 定义格式：Line index start_time end_time [note_indices]
            var parts = line.Split(new[] { ' ', '=' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4 && parts[0].ToLower() == "line")
            {
                ParseLineDefinition(parts);
            }
        }
    }

    /// <summary>
    /// 解析单个 Line 定义
    /// </summary>
    private void ParseLineDefinition(string[] parts)
    {
        try
        {
            if (!int.TryParse(parts[1], out int lineIndex))
            {
                Debug.LogWarning($"ParseLineDefinition: Line 序号解析失败 - {parts[1]}");
                return;
            }
            
            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float startTime))
            {
                Debug.LogWarning($"ParseLineDefinition: 开始时间解析失败 - {parts[2]}");
                return;
            }
            
            if (!float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float endTime))
            {
                Debug.LogWarning($"ParseLineDefinition: 结束时间解析失败 - {parts[3]}");
                return;
            }
            
            // 解析可选的音符序号列表
            List<int> noteIndices = new List<int>();
            for (int j = 4; j < parts.Length; j++)
            {
                if (int.TryParse(parts[j], out int noteIdx))
                {
                    noteIndices.Add(noteIdx);
                }
            }
            
            // 创建 Line 对象并添加到 ChartData
            Line line = Line.CreateFromChartData(lineIndex, startTime, endTime, noteIndices);
            ChartData.Instance.AddLine(line);
            
            Debug.Log($"ParseLineDefinition: 已解析 Line {lineIndex} | 时间：{startTime}-{endTime} | 音符数：{noteIndices.Count}");
        }
        catch (Exception e)
        {
            Debug.LogError($"ParseLineDefinition 解析异常：{e.Message}\nLine 定义：{string.Join(" ", parts)}");
        }
    }

    /// <summary>
    /// 解析音符指令
    /// </summary>
    private void ParseNoteCommand(string[] parts, bool? isScorable, bool isNoteFirstTimeOccured)
    {
        try
        {
            if (parts.Length < 4)
            {
                Debug.LogWarning($"ParseNoteCommand: 指令参数不足，parts={string.Join(",", parts)}");
                return;
            }
            
            NoteType noteType = ParseNoteType(parts[1]);
            if (!int.TryParse(parts[2], out int num))
            {
                Debug.LogWarning($"ParseNoteCommand: 音符编号解析失败，parts={string.Join(",", parts)}");
                return;
            }

            bool currentScorable = true;
            if (isScorable.HasValue)
            {
                currentScorable = isScorable.Value;
                if (!ChartData.Instance.isScorable.ContainsKey(num))
                {
                    ChartData.Instance.isScorable.Add(num, currentScorable);
                }
                else
                {
                    Debug.LogWarning($"ParseNoteCommand: Note{num} 重复首次标记，覆盖原有记分状态");
                    ChartData.Instance.isScorable[num] = currentScorable;
                }
            }
            else
            {
                if (!ChartData.Instance.isScorable.TryGetValue(num, out currentScorable))
                {
                    Debug.LogWarning($"ParseNoteCommand: 非首次note{num}未找到首次状态，默认记分");
                    currentScorable = true;
                }
            }

            float timeA = 0, timeB = 0, x1 = 0, y1 = 0, x2 = 0, y2 = 0, hold_duration = 0;
            int keyName = 0;
            string cmd = "";
            string noteMoveFileName = "";

            // 针对 Flick 音符的特殊处理：简单格式 "flick num start_time end_time"
            if (noteType == NoteType.Flick)
            {
                if (parts.Length >= 5)
                {
                    float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out timeA);
                    float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out timeB);
                    // 其他参数保持默认值0
                    cmd = "flick_simple"; // 自定义指令名，便于识别
                }
                else
                {
                    Debug.LogWarning($"ParseNoteCommand: Flick指令缺少时间参数，parts={string.Join(",", parts)}");
                }
            }
            else
            {
                // 原有逻辑：根据 cmd (parts[3]) 解析其他音符类型
                cmd = parts[3];  // 指令类型，如 drop_to, shift, move, destroy 等

                switch (cmd)
                {
                    case "destroy":
                        if (parts.Length >= 5)
                        {
                            float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out timeA);
                        }
                        break;
                    case "move":
                        if (parts.Length >= 6)
                        {
                            float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out timeA);
                            float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out timeB);
                        }
                        if (parts.Length >= 7)
                        {
                            noteMoveFileName = parts[6];
                        }
                        break;
                    case "drop_to":
                        if (parts.Length >= 5)
                        {
                            int.TryParse(parts[4], out keyName);
                        }
                        if (parts.Length >= 7)
                        {
                            float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out timeA);
                            float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out timeB);
                        }
                        if (parts.Length >= 11)
                        {
                            float.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out x1);
                            float.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out y1);
                            float.TryParse(parts[9], NumberStyles.Float, CultureInfo.InvariantCulture, out x2);
                            float.TryParse(parts[10], NumberStyles.Float, CultureInfo.InvariantCulture, out y2);
                        }
                        if ((noteType == NoteType.Hold || noteType == NoteType.MTap) && parts.Length >= 12)
                        {
                            float.TryParse(parts[11], NumberStyles.Float, CultureInfo.InvariantCulture, out hold_duration);
                        }
                        break;
                    case "shift":
                        if (parts.Length >= 6)
                        {
                            float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out timeA);
                            float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out timeB);
                        }
                        if (parts.Length >= 10)
                        {
                            float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out x1);
                            float.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out y1);
                            float.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out x2);
                            float.TryParse(parts[9], NumberStyles.Float, CultureInfo.InvariantCulture, out y2);
                        }
                        break;
                    case "spin":
                        if (parts.Length >= 6)
                        {
                            float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out timeA);
                            float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out timeB);
                        }
                        if (parts.Length >= 8)
                        {
                            float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out x1);  // init_direction
                            float.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out y1);  // degree (per second)
                        }
                        break;
                    case "":
                        // Tap 音符（无指令）：解析判定时间和初始位置
                        // 格式：# tap 1 <timeB> <x1> <y1>
                        if (parts.Length >= 5)
                        {
                            float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out timeB);
                        }
                        if (parts.Length >= 7)
                        {
                            float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out x1);
                            float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out y1);
                            // Tap 不需要 x2, y2，保持与 x1, y1 相同
                            x2 = x1;
                            y2 = y1;
                        }
                        // timeA 默认为 timeB - 1
                        timeA = timeB - 1f;
                        break;
                    default:
                        Debug.LogWarning($"ParseNoteCommand: 未处理的note指令类型 {cmd}，按默认逻辑解析");
                        int defaultParamIndex = cmd == "drop_to" ? 5 : 4;
                        if (parts.Length >= defaultParamIndex + 1)
                        {
                            float.TryParse(parts[defaultParamIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out timeA);
                            float.TryParse(parts[defaultParamIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out timeB);
                        }
                        if (parts.Length >= defaultParamIndex + 6)
                        {
                            float.TryParse(parts[defaultParamIndex + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out x1);
                            float.TryParse(parts[defaultParamIndex + 3], NumberStyles.Float, CultureInfo.InvariantCulture, out y1);
                            float.TryParse(parts[defaultParamIndex + 4], NumberStyles.Float, CultureInfo.InvariantCulture, out x2);
                            float.TryParse(parts[defaultParamIndex + 5], NumberStyles.Float, CultureInfo.InvariantCulture, out y2);
                        }
                        break;
                }
            }

            // 构建音符指令并添加到ChartData
            Command noteCmd = new Command
            {
                is_show = true,
                type = noteType,
                num = num,
                timeA = timeA,
                timeB = timeB,
                x1 = x1,
                y1 = y1,
                x2 = x2,
                y2 = y2,
                key_name = keyName,
                json_filename = noteMoveFileName,
                commandName = cmd,
                hold_duration = hold_duration,
                isNoteFirstTimeOccured = isNoteFirstTimeOccured
            };
            ChartData.Instance.AddNoteData(noteCmd);
        }
        catch (Exception e)
        {
            Debug.LogError($"ParseNoteCommand 解析异常：{e.Message}\n{e.StackTrace} | parts={string.Join(",", parts)}");
        }
    }

    /// <summary>
    /// 解析按键移动指令（KeyCommand）
    /// </summary>
    private void ParseKeyMoveCommand(string[] parts)
    {
        try
        {
            if (parts.Length < 4)
            {
                Debug.LogWarning($"ParseKeyMoveCommand: 指令参数不足，parts={string.Join(",", parts)}");
                return;
            }

            // 解析基础参数
            if (!int.TryParse(parts[2], out int keyIndex))
            {
                Debug.LogWarning($"ParseKeyMoveCommand: 按键序号解析失败，parts={string.Join(",", parts)}");
                return;
            }
            string cmdType = parts[3];
            float startTime = 0, endTime = 0;
            float x1 = 0, y1 = 0, x2 = 0, y2 = 0;
            string moveFileName = "";

            // 根据指令类型解析参数
            switch (cmdType)
            {
                case "hide":
                case "show":
                    if (parts.Length >= 5)
                    {
                        float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out startTime);
                        endTime = startTime; // 隐藏/显示无结束时间，默认和开始时间一致
                    }
                    break;
                case "shift":
                    if (parts.Length >= 6)
                    {
                        float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out startTime);
                        float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out endTime);
                    }
                    if (parts.Length >= 10)
                    {
                        float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out x1);
                        float.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out y1);
                        float.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out x2);
                        float.TryParse(parts[9], NumberStyles.Float, CultureInfo.InvariantCulture, out y2);
                    }
                    break;
                case "move":
                    if (parts.Length >= 6)
                    {
                        float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out startTime);
                        float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out endTime);
                    }
                    if (parts.Length >= 7)
                    {
                        moveFileName = parts[6];
                    }
                    break;
                default:
                    Debug.LogWarning($"ParseKeyMoveCommand: 未处理的Key指令类型 {cmdType}，parts={string.Join(",", parts)}");
                    return;
            }

            // 查找对应KeyData，添加KeyCommand
            KeyData targetKeyData = ChartData.Instance.keyDatas.FirstOrDefault(k => k.keyName == keyIndex);
            if (targetKeyData == null)
            {
                Debug.LogWarning($"ParseKeyMoveCommand: 未找到按键{keyIndex}的初始状态，跳过指令 | parts={string.Join(",", parts)}");
                return;
            }

            KeyCommand keyCmd = new KeyCommand
            {
                keyIndex = keyIndex,
                startTime = startTime,
                endTime = endTime,
                x1 = x1,
                y1 = y1,
                x2 = x2,
                y2 = y2,
                json_filename = moveFileName,
                cmdType = cmdType
            };
            targetKeyData.keyCommands.Add(keyCmd);
        }
        catch (Exception e)
        {
            Debug.LogError($"ParseKeyMoveCommand 解析异常：{e.Message}\n{e.StackTrace} | parts={string.Join(",", parts)}");
        }
    }

    /// <summary>
    /// 解析音符类型字符串为枚举
    /// </summary>
    private NoteType ParseNoteType(string typeStr)
    {
        return typeStr.ToLower() switch
        {
            "tap" => NoteType.Tap,
            "hold" => NoteType.Hold,
            "mtap" => NoteType.MTap,
            "dtap" => NoteType.MTap,
            "flick" => NoteType.Flick,
            "key" => NoteType.Key,
            "drag" => NoteType.Drag,
            _ => NoteType.Tap // 未知类型默认Tap
        };
    }
}