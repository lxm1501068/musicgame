using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

// 起始状态：(key_name x y show)
// 0 5 -5 1
// 1 4 -4 1
// 2 3 -3 1
// 3 2 -2 1

// 谱面：(note num time_a time_b x1 y1 x2 y2 command)
// 音符的编号不可重复，指令按照起始时间排列，以下为音符数，指令数和总时长
// 6
// 11
// 28.000
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
    private int noteCount;       // 解析到的音符总数
    private int cmdCount;        // 解析到的指令总数
    private float totalDuration; // 解析到的谱面总时长

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

        if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
        {
            using (UnityWebRequest www = UnityWebRequest.Get(path))
            {
                yield return www.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                if (www.result == UnityWebRequest.Result.Success)
#else
                if (www.isDone && string.IsNullOrEmpty(www.error))
#endif
                {
                    chartContent = www.downloadHandler.text;
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
            }
            else
            {
                Debug.LogError($"谱面文件不存在：{path}");
                yield break;
            }
        }

        ParseChartHeader();
        Debug.Log($"谱面加载完成！音符数：{noteCount} | 指令数：{cmdCount} | 总时长：{totalDuration}s");
    }

    private void ParseChartHeader()
    {
        if (string.IsNullOrEmpty(chartContent))
        {
            Debug.LogWarning("ParseChartHeader: 谱面内容为空，跳过头部解析");
            return;
        }

        var lines = chartContent.Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line))
            .ToList();

        if (lines.Count >= 3)
        {
            if (int.TryParse(lines[0], out int noteCnt)) noteCount = noteCnt;
            else Debug.LogError("ParseChartHeader: 音符数解析失败");

            if (int.TryParse(lines[1], out int cmdCnt)) cmdCount = cmdCnt;
            else Debug.LogError("ParseChartHeader: 指令数解析失败");

            if (float.TryParse(lines[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float totalTime))
                totalDuration = totalTime;
            else Debug.LogError("ParseChartHeader: 总时长解析失败");
        }
        else
        {
            Debug.LogError("ParseChartHeader: 谱面头部行数不足，无法解析");
        }
    }

    public ChartData ParseChart()
    {
        if (string.IsNullOrEmpty(chartContent))
        {
            Debug.LogError("ParseChart: 谱面内容为空，无法解析");
            return null;
        }
        
        // 解析前先重置数据，避免切换谱面时数据残留
        ChartData.Instance.ResetChartData();
        
        ChartData.Instance.noteCount = noteCount;
        ChartData.Instance.totalDuration = totalDuration;

        var lines = chartContent.Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line))
            .ToList();

        ParseKeyInitialState(lines);
        ParseCommands(lines, ChartData.Instance);

        ChartData.Instance.SortCommandsByTime();

        Debug.Log($"谱面解析完成：KeyData数={ChartData.Instance.keyDatas.Count} | Command数={ChartData.Instance.commands.Count} | KeyMoveData数={ChartData.Instance.keyMoveDatas.Count}");
        return ChartData.Instance;
    }

    private void ParseKeyInitialState(List<string> lines)
    {
        int keyStateStartIndex = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("(key_name x y show)"))
            {
                keyStateStartIndex = i + 1;
                break;
            }
        }

        if (keyStateStartIndex == -1)
        {
            Debug.LogWarning("ParseKeyInitialState: 未找到按键初始状态标记行");
            return;
        }

        for (int i = keyStateStartIndex; i < lines.Count; i++)
        {
            string line = lines[i];
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
                }
                else
                {
                    Debug.LogWarning($"ParseKeyInitialState: 解析失败，行内容：{line}");
                }
            }
        }
    }

    private void ParseCommands(List<string> lines, ChartData chartData)
    {
        int commandStartIndex = -1;
        for (int i = 3; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("(note num time_a time_b x1 y1 x2 y2 command)"))
            {
                commandStartIndex = i + 1;
                break;
            }
        }

        if (commandStartIndex == -1)
        {
            Debug.LogError("ParseCommands: 未找到谱面指令标记行");
            return;
        }

        for (int i = commandStartIndex; i < lines.Count; i++)
        {
            string line = lines[i];
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
                    // # 开头：首次出现 + 可记分
                    ParseNoteCommand(parts, chartData, isScorable: true, isNoteFirstTimeOccured: true);
                    break;
                case "!":
                    // ! 开头：首次出现 + 不可记分
                    ParseNoteCommand(parts, chartData, isScorable: false, isNoteFirstTimeOccured: true);
                    break;
                case "%":
                    // % 开头：非首次出现 + 沿用已有记分状态
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

    // 核心修改：适配Command无自定义构造函数的结构，修正字段赋值逻辑
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

            // 处理记分状态
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

            // 初始化指令字段
            float timeA = 0, timeB = 0, x1 = 0, y1 = 0, x2 = 0, y2 = 0;
            int keyName = 0;
            string cmd = parts[3];
            string noteMoveFileName = "";

            // 按指令类型分情况解析参数
            switch (cmd)
            {
                case "destroy":
                    // % tap 2 destroy 16.000 → 仅时间A
                    if (parts.Length >= 5)
                    {
                        float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out timeA);
                    }
                    break;

                case "move":
                    // # tap 3 move 14.000 16.000 move_1.json → 时间A + 时间B + JSON文件名
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
                    // # tap 1 drop_to 0 14.000 16.000 4 -4 5 -5 → 目标key + 时间 + 坐标
                    if (parts.Length >= 5)
                    {
                        int.TryParse(parts[4], out keyName); // 解析drop_to指向的key
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
                    // hold指令额外拼接持续时间参数
                    if (noteType == NoteType.Hold && parts.Length >= 12)
                    {
                        cmd += $" {parts[11]}";
                    }
                    break;

                case "drift":
                    // ! tap 2 drift 14.000 16.000 4 -4 5 -5 → 时间 + 坐标
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

            // 核心修改：Command无自定义构造函数，直接赋值字段
            Command noteCmd = new Command();
            noteCmd.is_show = true; // 默认显示
            noteCmd.type = noteType;
            noteCmd.num = num;
            noteCmd.timeA = timeA;
            noteCmd.timeB = timeB;
            noteCmd.x1 = x1;
            noteCmd.y1 = y1;
            noteCmd.x2 = x2;
            noteCmd.y2 = y2;
            noteCmd.key_name = keyName; // 赋值drop_to指向的key
            noteCmd.filename = noteMoveFileName; // 单独赋值move指令的JSON文件名
            noteCmd.commandName = cmd; // 指令类型（不再拼接文件名）
            noteCmd.isNoteFirstTimeOccured = isNoteFirstTimeOccured;

            chartData.commands.Add(noteCmd);

            // 调试日志：验证move指令解析
            if (cmd.StartsWith("move"))
            {
                Debug.Log($"解析Note{num}的move指令成功 | 时间[{timeA},{timeB}] | JSON文件：{noteMoveFileName} | 首次出现：{isNoteFirstTimeOccured}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"ParseNoteCommand: 解析失败，错误={e.Message}，parts={string.Join(",", parts)}");
        }
    }

    private void ParseKeyMoveCommand(string[] parts, ChartData chartData)
    {
        try
        {
            if (parts.Length < 4)
            {
                Debug.LogWarning($"ParseKeyMoveCommand: 指令参数不足，parts={string.Join(",", parts)}");
                return;
            }

            if (!int.TryParse(parts[2], out int keyNum))
            {
                Debug.LogWarning($"ParseKeyMoveCommand: Key编号解析失败，parts={string.Join(",", parts)}");
                return;
            }

            string cmdType = parts[3];
            float startTime = 0, endTime = 0;
            Vector2 targetPos = Vector2.zero;
            string extraParam = "";

            // 解析时间参数
            if (parts.Length >= 5)
            {
                float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out startTime);
            }
            if (parts.Length >= 6)
            {
                float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out endTime);
            }

            // 解析不同指令类型的参数
            switch (cmdType.ToLower())
            {
                case "hide":
                case "show":
                    // hide/show仅需时间，无坐标
                    break;
                case "drift":
                    // drift需要目标坐标
                    if (parts.Length >= 10)
                    {
                        float x2 = 0, y2 = 0;
                        float.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out x2);
                        float.TryParse(parts[9], NumberStyles.Float, CultureInfo.InvariantCulture, out y2);
                        targetPos = new Vector2(x2, y2);
                    }
                    break;
                case "move":
                    // move指令暂存JSON文件名（如需解析JSON可在此扩展）
                    if (parts.Length >= 7)
                    {
                        extraParam = parts[6];
                    }
                    break;
                default:
                    Debug.LogWarning($"ParseKeyMoveCommand: 未知Key指令类型 {cmdType}");
                    break;
            }

            // 创建KeyMoveData并添加到列表
            KeyMoveData keyMoveData = new KeyMoveData(
                keyIndex: keyNum,
                startTime: startTime,
                endTime: endTime,
                targetPos: targetPos
            );
            chartData.keyMoveDatas.Add(keyMoveData);

            Debug.Log($"解析KeyMove指令成功：Key{keyNum} | {cmdType} | 时间[{startTime},{endTime}] | 目标坐标：{targetPos}");
        }
        catch (Exception e)
        {
            Debug.LogError($"ParseKeyMoveCommand: 解析失败，错误={e.Message}，parts={string.Join(",", parts)}");
        }
    }

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
            _ => NoteType.Tap
        };
    }

    public void ClearChartContent()
    {
        chartContent = string.Empty;
        noteCount = 0;
        cmdCount = 0;
        totalDuration = 0;
        Debug.Log("谱面内容已清空");
    }
}