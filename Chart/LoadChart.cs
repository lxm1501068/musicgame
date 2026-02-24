using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

// 轨道按键示例注释保留（仅作参考）
// 0,1,2,3
// 6
// 11
// 28.000
// (key_name x y show)
// 0 5 -5 1
// 1 4 -4 1
// 2 3 -3 1
// 3 2 -2 1

// 谱面示例注释保留（仅作参考）
// 谱面
// # tap 1 drop_to 0 14.000 16.000 4 -4 5 -5
// ! tap 2 drift 14.000 16.000 4 -4 5 -5
// % tap 2 destroy 16.000
// # tap 3 move 14.000 16.000 move_1.json
// # hold 4 drop_to 1 18.000 20.000 4 -4 5 -5 3
// # dtap 5 drop_to 0 20.000 22.000 4 -4 5 -5
// # flick 6 drop_to 2 22.000 24.000 -1 1 0 0
// $ key 0 hide 24.000
// $ key 1 drift 24.000 26.000 4 -4 3 -3
// $ key 0 show 26.000
// $ key 2 move 26.000 28.000 move_2.json

public class LoadChart : MonoBehaviour
{
    private string chartContent; // 加载的谱面文本内容
    private List<int> keyIds;    // 轨道按键ID列表（轨道按键部分解析）
    private int noteCount;       // 解析到的音符总数
    private int cmdCount;        // 解析到的指令总数
    private float totalDuration; // 解析到的谱面总时长
    private List<string> _spectrumLines; // 本地缓存谱面行（替代ChartData的SpectrumLines）
    
    // 兼容原有访问逻辑：轨道按键数=列表长度
    public int KeyCount => keyIds?.Count ?? 0;
    // 对外提供轨道按键ID列表
    public List<int> KeyIds => keyIds ?? new List<int>();
    public string ChartContent => chartContent;

    public IEnumerator LoadChartFile(string fileName)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("LoadChart.LoadChartFile: GameManager.Instance 为空！");
            yield break;
        }
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        Debug.Log($"开始加载谱面：{path}");
        
        // 先处理文件加载（包含yield return，移出try-catch块）
        bool loadSuccess = false;
        if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
        {
            using (UnityWebRequest www = UnityWebRequest.Get(path))
            {
                yield return www.SendWebRequest(); // 移出try-catch，解决CS1626
#if UNITY_2020_1_OR_NEWER
                if (www.result == UnityWebRequest.Result.Success)
#else
                if (www.isDone && string.IsNullOrEmpty(www.error))
#endif
                {
                    chartContent = www.downloadHandler.text;
                    loadSuccess = true;
                }
                else
                {
                    Debug.LogError($"加载谱面失败：{www.error}");
                    yield break;
                }
            }
        }
        else
        {
            if (File.Exists(path))
            {
                chartContent = File.ReadAllText(path);
                loadSuccess = true;
            }
            else
            {
                Debug.LogError($"谱面文件不存在：{path}");
                yield break;
            }
        }

        // 加载成功后，处理解析逻辑（放入try-catch）
        if (loadSuccess)
        {
            try
            {
                // 拆分轨道按键和谱面内容，解析头部
                if (!SplitChartContent(out List<string> trackKeyLines, out List<string> spectrumLines))
                {
                    yield break;
                }
                ParseChartHeader(trackKeyLines);
                // 缓存谱面行到本地字段（不再依赖ChartData）
                _spectrumLines = spectrumLines;
                
                // 日志调整：打印轨道按键列表+数量
                Debug.Log($"谱面加载完成！轨道按键列表：[{string.Join(",", KeyIds)}] | 轨道按键数：{KeyCount} | 音符数：{noteCount} | 指令数：{cmdCount} | 总时长：{totalDuration}s");
            }
            catch (Exception e)
            {
                Debug.LogError($"解析谱面异常：{e.Message}\n{e.StackTrace}");
                yield break;
            }
        }
    }

    /// <summary>
    /// 修正：拆分轨道头部4行 + 按键初始状态行到谱面行 + 谱面指令行
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

        // 读取前4行作为轨道头部
        int headerLineCount = 0;
        for (; lineIndex < allLines.Count && headerLineCount < 4; lineIndex++)
        {
            string line = allLines[lineIndex].Trim();
            if (string.IsNullOrEmpty(line))
            {
                Debug.LogError("SplitChartContent: 轨道头部未读满4行就遇到空行");
                return false;
            }
            trackKeyLines.Add(line);
            headerLineCount++;
        }

        if (trackKeyLines.Count != 4)
        {
            Debug.LogError($"SplitChartContent: 轨道头部行数异常，期望4行，实际{trackKeyLines.Count}行");
            return false;
        }

        // ========== 步骤2：读取按键初始状态行（含标记行，直到空行） ==========
        List<string> keyInitialStateLines = new List<string>();
        for (; lineIndex < allLines.Count; lineIndex++)
        {
            string line = allLines[lineIndex].Trim();
            if (string.IsNullOrEmpty(line))
            {
                lineIndex++; // 跳过空行，找「谱面」分割符
                break;
            }
            // 保留原始行（含标记行），加入谱面行的前置部分
            keyInitialStateLines.Add(allLines[lineIndex].Trim());
        }

        // ========== 步骤3：找到「谱面」分割符，读取后续谱面指令行 ==========
        bool spectrumFound = false;
        List<string> noteCommandLines = new List<string>();
        for (; lineIndex < allLines.Count; lineIndex++)
        {
            string line = allLines[lineIndex].Trim();
            if (line == "谱面")
            {
                spectrumFound = true;
                continue;
            }
            if (spectrumFound && !string.IsNullOrEmpty(line))
            {
                noteCommandLines.Add(line);
            }
        }

        if (!spectrumFound)
        {
            Debug.LogError("SplitChartContent: 未找到「谱面」分割符");
            return false;
        }

        // ========== 合并：按键初始状态行 + 谱面指令行 → spectrumLines ==========
        spectrumLines.AddRange(keyInitialStateLines);
        spectrumLines.AddRange(noteCommandLines);

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

    public ChartData ParseChart()
    {
        // 改为校验本地缓存的谱面行，不再依赖ChartData
        if (string.IsNullOrEmpty(chartContent) || _spectrumLines == null || _spectrumLines.Count == 0)
        {
            Debug.LogError("ParseChart: 谱面内容为空，无法解析");
            return null;
        }
        
        // 解析前先重置数据，避免切换谱面时数据残留
        ChartData.Instance.ResetChartData();
        // 赋值轨道按键列表+数量到ChartData
        ChartData.Instance.keyIds = this.KeyIds;
        ChartData.Instance.keyCount = this.KeyCount;
        ChartData.Instance.noteCount = noteCount;
        ChartData.Instance.totalDuration = totalDuration;
        
        // 使用本地缓存的谱面行解析
        ParseKeyInitialState(_spectrumLines);
        ParseCommands(_spectrumLines, ChartData.Instance);
        ChartData.Instance.SortCommandsByTime();
        
        Debug.Log($"谱面解析完成：KeyData数={ChartData.Instance.keyDatas.Count} | Command数={ChartData.Instance.commands.Count}");
        return ChartData.Instance;
    }

    /// <summary>
    /// 优化：兼容无标记行，直接解析前N行（N=轨道按键数）作为按键初始状态
    /// </summary>
    private void ParseKeyInitialState(List<string> spectrumLines)
    {
        // 方案1：优先找标记行（兼容原有逻辑）
        int keyStateStartIndex = -1;
        for (int i = 0; i < spectrumLines.Count; i++)
        {
            if (spectrumLines[i].StartsWith("(key_name x y show)"))
            {
                keyStateStartIndex = i + 1;
                break;
            }
        }

        // 方案2：无标记行时，取前KeyCount行作为按键初始状态
        if (keyStateStartIndex == -1)
        {
            Debug.LogWarning("ParseKeyInitialState: 未找到标记行，尝试解析前KeyCount行作为按键初始状态");
            keyStateStartIndex = 0;
        }

        // 解析按键初始状态（最多解析KeyCount行）
        int parsedCount = 0;
        for (int i = keyStateStartIndex; i < spectrumLines.Count && parsedCount < KeyCount; i++)
        {
            string line = spectrumLines[i];
            // 遇到谱面指令标记行则停止
            if (line.StartsWith("(note num time_a time_b x1 y1 x2 y2 command)")) break;
            
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
                else
                {
                    Debug.LogWarning($"ParseKeyInitialState: 解析失败，行内容：{line}");
                }
            }
        }

        if (parsedCount == 0)
        {
            Debug.LogError("ParseKeyInitialState: 未解析到任何按键初始状态");
        }
    }

    /// <summary>
    /// 解析谱面指令
    /// </summary>
    private void ParseCommands(List<string> spectrumLines, ChartData chartData)
    {
        int commandStartIndex = -1;
        // 查找指令起始标记行
        for (int i = 0; i < spectrumLines.Count; i++)
        {
            if (spectrumLines[i].StartsWith("(note num time_a time_b x1 y1 x2 y2 command)"))
            {
                commandStartIndex = i + 1;
                break;
            }
        }
        
        if (commandStartIndex == -1)
        {
            // 兼容无标记行场景：直接从按键初始状态行之后开始解析
            commandStartIndex = KeyCount;
            Debug.LogWarning("ParseCommands: 未找到谱面指令标记行，从按键初始状态后开始解析");
        }
        
        for (int i = commandStartIndex; i < spectrumLines.Count; i++)
        {
            string line = spectrumLines[i];
            if (string.IsNullOrEmpty(line)) continue;
            
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                Debug.LogWarning($"ParseCommands: 指令行格式错误，内容：{line}");
                continue;
            }
            
            switch (parts[0])
            {
                case "#":
                    ParseNoteCommand(parts, chartData, isScorable: true, isNoteFirstTimeOccured: true);
                    break;
                case "!":
                    ParseNoteCommand(parts, chartData, isScorable: false, isNoteFirstTimeOccured: true);
                    break;
                case "%":
                    ParseNoteCommand(parts, chartData, isScorable: null, isNoteFirstTimeOccured: false);
                    break;
                case "$":
                    ParseKeyMoveCommand(parts, chartData);
                    break;
                default:
                    Debug.LogWarning($"ParseCommands: 未知指令标识，行内容：{line}");
                    break;
            }
        }
    }

    /// <summary>
    /// 解析音符指令
    /// </summary>
    private void ParseNoteCommand(string[] parts, ChartData chartData, bool? isScorable, bool isNoteFirstTimeOccured)
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
                if (!chartData.isScorable.ContainsKey(num))
                {
                    chartData.isScorable.Add(num, currentScorable);
                }
                else
                {
                    Debug.LogWarning($"ParseNoteCommand: Note{num} 重复首次标记，覆盖原有记分状态");
                    chartData.isScorable[num] = currentScorable;
                }
            }
            else
            {
                if (!chartData.isScorable.TryGetValue(num, out currentScorable))
                {
                    Debug.LogWarning($"ParseNoteCommand: 非首次note{num}未找到首次状态，默认记分");
                    currentScorable = true;
                }
            }

            float timeA = 0, timeB = 0, x1 = 0, y1 = 0, x2 = 0, y2 = 0;
            int keyName = 0;
            string cmd = parts[3];
            string noteMoveFileName = "";

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
                    if (noteType == NoteType.Hold && parts.Length >= 12)
                    {
                        cmd += $" {parts[11]}";
                    }
                    break;
                case "drift":
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
                filename = noteMoveFileName,
                commandName = cmd,
                isNoteFirstTimeOccured = isNoteFirstTimeOccured
            };
            chartData.AddNoteData(noteCmd);
        }
        catch (Exception e)
        {
            Debug.LogError($"ParseNoteCommand 解析异常：{e.Message}\n{e.StackTrace} | parts={string.Join(",", parts)}");
        }
    }

    /// <summary>
    /// 解析按键移动指令（KeyCommand）
    /// </summary>
    private void ParseKeyMoveCommand(string[] parts, ChartData chartData)
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
                case "drift":
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
            KeyData targetKeyData = chartData.keyDatas.FirstOrDefault(k => k.keyName == keyIndex);
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
                filename = moveFileName,
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
            "dtap" => NoteType.DTap,
            "flick" => NoteType.Flick,
            "key" => NoteType.Key,
            "drag" => NoteType.Drag,
            _ => NoteType.Tap // 未知类型默认Tap
        };
    }
}