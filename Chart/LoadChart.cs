using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

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
        ChartData.Instance.noteCount = noteCount;
        ChartData.Instance.totalDuration = totalDuration;

        var lines = chartContent.Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line))
            .ToList();

        ParseKeyInitialState(lines); // 修正原代码参数错误（原代码调用时传了ChartData.Instance，但方法定义未接收）
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

    // 核心修改：新增 isNoteFirstTimeOccured 参数，用于给Command赋值
    private void ParseNoteCommand(string[] parts, ChartData chartData, bool? isScorable, bool isNoteFirstTimeOccured)
    {
        try
        {
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
            string cmd = parts[3];
            string noteMoveFileName = ""; // 存储move指令的JSON文件名

            // 按指令类型分情况解析参数
            switch (cmd)
            {
                case "destroy":
                    // % tap 2 destroy 16.000 → 仅时间A
                    float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out timeA);
                    break;

                case "move":
                    // # tap 3 move 14.000 16.000 move_1.json → 时间A + 时间B + JSON文件名
                    int moveParamIndex = 4;
                    if (moveParamIndex + 1 < parts.Length)
                    {
                        float.TryParse(parts[moveParamIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out timeA);
                        float.TryParse(parts[moveParamIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out timeB);
                    }
                    if (moveParamIndex + 2 < parts.Length)
                    {
                        noteMoveFileName = parts[moveParamIndex + 2];
                        cmd += $" {noteMoveFileName}"; // 拼接文件名到command字段
                    }
                    break;

                case "drop_to":
                    // # tap 1 drop_to 0 14.000 16.000 4 -4 5 -5 → 目标key + 时间 + 坐标
                    int dropToParamIndex = 5; // 跳过drop_to后的目标key（parts[4]）
                    if (dropToParamIndex + 1 < parts.Length)
                    {
                        float.TryParse(parts[dropToParamIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out timeA);
                        float.TryParse(parts[dropToParamIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out timeB);
                    }
                    if (dropToParamIndex + 5 < parts.Length)
                    {
                        float.TryParse(parts[dropToParamIndex + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out x1);
                        float.TryParse(parts[dropToParamIndex + 3], NumberStyles.Float, CultureInfo.InvariantCulture, out y1);
                        float.TryParse(parts[dropToParamIndex + 4], NumberStyles.Float, CultureInfo.InvariantCulture, out x2);
                        float.TryParse(parts[dropToParamIndex + 5], NumberStyles.Float, CultureInfo.InvariantCulture, out y2);
                    }
                    // hold指令额外处理持续时间
                    if (noteType == NoteType.Hold && dropToParamIndex + 2 < parts.Length)
                    {
                        cmd += $" {parts[dropToParamIndex + 2]}";
                    }
                    break;

                case "drift":
                    // ! tap 2 drift 14.000 16.000 4 -4 5 -5 → 时间 + 坐标
                    int driftParamIndex = 4;
                    if (driftParamIndex + 1 < parts.Length)
                    {
                        float.TryParse(parts[driftParamIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out timeA);
                        float.TryParse(parts[driftParamIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out timeB);
                    }
                    if (driftParamIndex + 5 < parts.Length)
                    {
                        float.TryParse(parts[driftParamIndex + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out x1);
                        float.TryParse(parts[driftParamIndex + 3], NumberStyles.Float, CultureInfo.InvariantCulture, out y1);
                        float.TryParse(parts[driftParamIndex + 4], NumberStyles.Float, CultureInfo.InvariantCulture, out x2);
                        float.TryParse(parts[driftParamIndex + 5], NumberStyles.Float, CultureInfo.InvariantCulture, out y2);
                    }
                    break;

                default:
                    Debug.LogWarning($"ParseNoteCommand: 未处理的note指令类型 {cmd}，按默认逻辑解析");
                    int defaultParamIndex = 4;
                    if (cmd == "drop_to") defaultParamIndex++;
                    if (defaultParamIndex + 1 < parts.Length)
                    {
                        float.TryParse(parts[defaultParamIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out timeA);
                        float.TryParse(parts[defaultParamIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out timeB);
                    }
                    if (defaultParamIndex + 5 < parts.Length)
                    {
                        float.TryParse(parts[defaultParamIndex + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out x1);
                        float.TryParse(parts[defaultParamIndex + 3], NumberStyles.Float, CultureInfo.InvariantCulture, out y1);
                        float.TryParse(parts[defaultParamIndex + 4], NumberStyles.Float, CultureInfo.InvariantCulture, out x2);
                        float.TryParse(parts[defaultParamIndex + 5], NumberStyles.Float, CultureInfo.InvariantCulture, out y2);
                    }
                    break;
            }

            // 核心修改：创建Command时传入 isNoteFirstTimeOccured 参数
            Command noteCmd = new Command(
                num: num,
                type: noteType,
                timeA: timeA,
                timeB: timeB,
                x1: x1,
                y1: y1,
                x2: x2,
                y2: y2,
                command: cmd,
                isNoteFirstTimeOccured: isNoteFirstTimeOccured // 赋值首次出现标记
            );
            noteCmd.is_show = true;

            chartData.commands.Add(noteCmd);
            // 修正原代码：原代码用了ChartData.Instance，改为传入的chartData参数（更规范）
            chartData.commandScorable.Add(currentScorable);

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
            if (!int.TryParse(parts[2], out int keyNum))
            {
                Debug.LogWarning($"ParseKeyMoveCommand: Key编号解析失败，parts={string.Join(",", parts)}");
                return;
            }

            string cmdType = parts[3];
            float timeA = 0, timeB = 0, x1 = 0, y1 = 0, x2 = 0, y2 = 0;
            string extraParam = "";

            if (parts.Length >= 5)
            {
                float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out timeA);
            }

            if (parts.Length >= 6)
            {
                float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out timeB);
            }

            switch (cmdType.ToLower())
            {
                case "hide":
                case "show":
                    break;
                case "drift":
                    if (parts.Length >= 9)
                    {
                        float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out x1);
                        float.TryParse(parts[7], NumberStyles.Float, CultureInfo.InvariantCulture, out y1);
                        float.TryParse(parts[8], NumberStyles.Float, CultureInfo.InvariantCulture, out x2);
                        float.TryParse(parts[9], NumberStyles.Float, CultureInfo.InvariantCulture, out y2);
                    }
                    break;
                case "move":
                    if (parts.Length >= 7)
                    {
                        extraParam = parts[6];
                    }
                    break;
                default:
                    Debug.LogWarning($"ParseKeyMoveCommand: 未知Key指令类型 {cmdType}");
                    break;
            }

            // 修正：适配ChartData中KeyMoveData的构造函数参数（原代码构造函数参数与解析字段不匹配）
            KeyMoveData keyMoveData = new KeyMoveData(
                keyIndex: keyNum,
                startTime: timeA,
                endTime: timeB,
                targetPos: new Vector2(x2, y2) // 取drift/move的最终目标坐标
            );
            chartData.keyMoveDatas.Add(keyMoveData);

            Debug.Log($"解析KeyMove指令成功：Key{keyNum} | {cmdType} | 时间[{timeA},{timeB}]");
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