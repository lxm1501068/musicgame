using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;
using System.Linq;

public partial class CreateSceneManager
{
    // 段落信息结构
    private class SectionInfo
    {
        public int startMeasure;
        public int endMeasure;
        public float bpm;
        public int beatsPerMeasure;
        public int beatUnit;
    }
    
    // ---------- 谱面导出与运动 JSON 生成 ----------

    /// <summary>
    /// 将连续相同设置的小节合并为段落
    /// </summary>
    private List<SectionInfo> MergeMeasuresToSections(List<MeasureData> measures)
    {
        List<SectionInfo> sections = new List<SectionInfo>();
        
        if (measures == null || measures.Count == 0)
            return sections;
        
        // 按小节索引排序
        var sortedMeasures = new List<MeasureData>(measures);
        sortedMeasures.Sort((a, b) => a.measureIndex.CompareTo(b.measureIndex));
        
        SectionInfo currentSection = new SectionInfo
        {
            startMeasure = sortedMeasures[0].measureIndex,
            endMeasure = sortedMeasures[0].measureIndex,
            bpm = sortedMeasures[0].bpm,
            beatsPerMeasure = sortedMeasures[0].beatsPerMeasure,
            beatUnit = sortedMeasures[0].beatUnit
        };
        
        for (int i = 1; i < sortedMeasures.Count; i++)
        {
            var measure = sortedMeasures[i];
            
            // 检查是否与当前段落设置相同且连续
            bool isSameSettings = (
                Mathf.Approximately(measure.bpm, currentSection.bpm) &&
                measure.beatsPerMeasure == currentSection.beatsPerMeasure &&
                measure.beatUnit == currentSection.beatUnit &&
                measure.measureIndex == currentSection.endMeasure + 1
            );
            
            if (isSameSettings)
            {
                // 扩展到当前段落
                currentSection.endMeasure = measure.measureIndex;
            }
            else
            {
                // 保存当前段落，开始新段落
                sections.Add(currentSection);
                currentSection = new SectionInfo
                {
                    startMeasure = measure.measureIndex,
                    endMeasure = measure.measureIndex,
                    bpm = measure.bpm,
                    beatsPerMeasure = measure.beatsPerMeasure,
                    beatUnit = measure.beatUnit
                };
            }
        }
        
        // 添加最后一个段落
        sections.Add(currentSection);
        
        return sections;
    }

    /// <summary>
    /// 生成并保存特殊运动轨迹的 JSON 文件
    /// </summary>
    private void GenerateAndSaveMovementJson(string type, float timeA, float timeB, string fileName)
    {
        MoveFrameList frameList = new MoveFrameList();
        frameList.frames = new List<MoveFrame>();

        float duration = timeB - timeA;
        if (duration <= 0) duration = 0.1f;

        // 设置采样率（例如每秒 60 帧）
        float sampleRate = 60f;
        int frameCount = Mathf.CeilToInt(duration * sampleRate);

        for (int i = 0; i <= frameCount; i++)
        {
            float t = (float)i / frameCount;
            float currentTime = timeA + t * duration;
            Vector2 pos = Vector2.zero;

            // 获取音符的当前起始位置（如果有的话）
            Vector2 startPos = capturedStartPos;
            Vector2 endPos = capturedEndPos;

            switch (type.ToLower())
            {
                case "harmonic":
                    // 简谐运动：在 startPos 和 endPos 之间做正弦往复
                    float sinVal = Mathf.Sin(t * Mathf.PI * 2f); // 一个周期
                    pos = Vector2.Lerp(startPos, endPos, (sinVal + 1f) / 2f);
                    break;
                case "parabolic":
                    // 抛物线：线性插值 X，二次曲线插值 Y（简单抛物线）
                    float h = 5f; // 抛物线高度
                    float parabolicY = 4 * h * t * (1 - t);
                    pos = Vector2.Lerp(startPos, endPos, t);
                    pos.y += parabolicY;
                    break;
                case "circular":
                    // 圆周运动：以 startPos 为中心，endPos 到 startPos 的距离为半径
                    float radius = Vector2.Distance(startPos, endPos);
                    float angle = t * Mathf.PI * 2f;
                    pos.x = startPos.x + Mathf.Cos(angle) * radius;
                    pos.y = startPos.y + Mathf.Sin(angle) * radius;
                    break;
            }

            frameList.frames.Add(new MoveFrame
            {
                time = currentTime,
                x = pos.x,
                y = pos.y
            });
        }

        string json = JsonUtility.ToJson(frameList, true);
        // 1. 修改JSON文件输出路径到 StreamingAssets/Create/
        string createDirPath = Path.Combine(Application.streamingAssetsPath, "Create");
        // 确保目录存在，不存在则创建
        if (!Directory.Exists(createDirPath))
        {
            Directory.CreateDirectory(createDirPath);
        }
        string path = Path.Combine(createDirPath, fileName);
        
        try
        {
            File.WriteAllText(path, json);
            // Debug.Log($"已生成运动 JSON: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存运动 JSON 失败: {e.Message}");
        }
    }

    /// <summary>
    /// 将当前的 ChartData 导出为 chart.txt 文件
    /// </summary>
    public void ExportChart(string fileName = "chart.txt")
    {
        StringBuilder sb = new StringBuilder();

        // 1. 写入头部信息
        // 第一行：轨道按键列表
        sb.AppendLine(string.Join(",", ChartData.Instance.keyIds));
        
        // 计算总指令数（音符指令 + 按键指令）
        int cmdCount = ChartData.Instance.commands.Count;
        foreach (var keyData in ChartData.Instance.keyDatas)
            cmdCount += keyData.keyCommands.Count;

        // 第二行：指令总数
        sb.AppendLine(cmdCount.ToString());
        
        // 第三行：音符总数（这里假设 num 唯一的数量）
        int noteCount = ChartData.Instance.commands.Select(c => c.num).Distinct().Count();
        sb.AppendLine(noteCount.ToString());
        
        // 第四行：总时长
        sb.AppendLine(ChartData.Instance.totalDuration.ToString("F3", CultureInfo.InvariantCulture));
        
        // 第五行：默认 BPM
        sb.AppendLine($"BPM:{ChartData.Instance.defaultBpm:F2}");
        
        // 第六行：默认拍号
        sb.AppendLine($"TimeSignature:{ChartData.Instance.defaultBeatsPerMeasure}/{ChartData.Instance.defaultBeatUnit}");
        
        // 第七行：小节数量
        sb.AppendLine($"Measures:{ChartData.Instance.measureCount}");
        
        // 第八行及以后：段落信息（Section）
        if (ChartData.Instance.measures != null && ChartData.Instance.measures.Count > 0)
        {
            // 将连续相同设置的小节合并为段落
            List<SectionInfo> sections = MergeMeasuresToSections(ChartData.Instance.measures);
            
            // 写入段落数量
            sb.AppendLine($"Sections:{sections.Count}");
            
            // 写入每个段落
            foreach (var section in sections)
            {
                sb.AppendLine($"Section:{section.startMeasure}-{section.endMeasure} BPM:{section.bpm:F2} TimeSig:{section.beatsPerMeasure}/{section.beatUnit}");
            }
        }

        // 2. 写入按键初始状态
        sb.AppendLine("(key_name x y show)");
        foreach (var key in ChartData.Instance.keyDatas)
        {
            sb.AppendLine($"{key.keyName} {key.x:F2} {key.y:F2} {key.show}");
        }

        // 3. 写入谱面分割符
        sb.AppendLine("谱面");
        sb.AppendLine("(note num command time_a time_b x1 y1 x2 y2 (.json file or hold_duration))");

        // 4. 写入音符指令
        // 先按时间排序
        ChartData.Instance.SortCommandsByTime();
        foreach (var cmd in ChartData.Instance.commands)
        {
            string prefix = cmd.isNoteFirstTimeOccured ? (ChartData.Instance.isScorable.GetValueOrDefault(cmd.num, true) ? "#" : "!") : "%";
            string line = $"{prefix} {cmd.type.ToString().ToLower()} {cmd.num} {cmd.commandName}";

            switch (cmd.commandName)
            {
                case "destroy":
                    line += $" {cmd.timeA:F3}";
                    break;
                case "move":
                    line += $" {cmd.timeA:F3} {cmd.timeB:F3} {cmd.json_filename}";
                    break;
                case "drop_to":
                    line += $" {cmd.key_name} {cmd.timeA:F3} {cmd.timeB:F3} {cmd.x1:F2} {cmd.y1:F2} {cmd.x2:F2} {cmd.y2:F2}";
                    if (cmd.type == NoteType.Hold || cmd.type == NoteType.MTap)
                        line += $" {cmd.hold_duration:F3}";
                    break;
                case "shift":
                    line += $" {cmd.timeA:F3} {cmd.timeB:F3} {cmd.x1:F2} {cmd.y1:F2} {cmd.x2:F2} {cmd.y2:F2}";
                    break;
                case "spin":
                    line += $" {cmd.timeA:F3} {cmd.timeB:F3} {cmd.x1:F2} {cmd.y1:F2}";
                    break;
                case "flick_simple":
                    line += $" {cmd.timeA:F3} {cmd.timeB:F3}";
                    break;
                case "":
                    // Tap 音符（无指令）：保存判定时间和位置
                    line += $" {cmd.timeB:F3} {cmd.x1:F2} {cmd.y1:F2}";
                    break;
            }
            sb.AppendLine(line);
        }

        // 5. 写入按键指令
        foreach (var keyData in ChartData.Instance.keyDatas)
        {
            foreach (var keyCmd in keyData.keyCommands)
            {
                string line = $"$ key {keyData.keyName} {keyCmd.cmdType}";
                switch (keyCmd.cmdType.ToLower())
                {
                    case "hide":
                    case "show":
                        line += $" {keyCmd.startTime:F3}";
                        break;
                    case "shift":
                        line += $" {keyCmd.startTime:F3} {keyCmd.endTime:F3} {keyCmd.x1:F2} {keyCmd.y1:F2} {keyCmd.x2:F2} {keyCmd.y2:F2}";
                        break;
                    case "move":
                        line += $" {keyCmd.startTime:F3} {keyCmd.endTime:F3} {keyCmd.json_filename}";
                        break;
                }
                sb.AppendLine(line);
            }
        }

        // 2. 修改chart.txt文件输出路径到 StreamingAssets/Create/
        string createDirPath = Path.Combine(Application.streamingAssetsPath, "Create");
        // 确保目录存在，不存在则创建
        if (!Directory.Exists(createDirPath))
        {
            Directory.CreateDirectory(createDirPath);
        }
        string path = Path.Combine(createDirPath, fileName);
        
        try
        {
            File.WriteAllText(path, sb.ToString());
            infoText.text = $"谱面已导出至: {path}";
            // Debug.Log(infoText.text);
        }
        catch (System.Exception e)
        {
            infoText.text = $"导出失败: {e.Message}";
            Debug.LogError(infoText.text);
        }
    }
}