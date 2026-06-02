using UnityEngine;
using TMPro;

/// <summary>
/// 得分显示组件
/// 处理得分计算、Combo 计数以及 UI 显示。总分为 100,000。
/// </summary>
public class ScoreDisplay : MonoBehaviour
{
    [Header("UI 配置 - 分数")]
    public TMP_Text scoreText;
    public string scorePrefix = "";
    public int scoreDigit = 6; // 总分 10w，建议 6 位

    [Header("UI 配置 - Combo")]
    public TMP_Text comboText;
    public string comboSuffix = " COMBO";

    [Header("平滑滚动")]
    public float scrollSpeed = 5000f;

    // 分数计算常量
    private const float MAX_TOTAL_SCORE = 100000f;
    private const float JUDGE_SCORE_RATIO = 0.9f; // 判定分占 90% (90,000)
    private const float COMBO_SCORE_RATIO = 0.1f; // Combo 分占 10% (10,000)

    // 运行时数据
    private float currentScore = 0;
    private float displayedScore = 0;
    private int currentCombo = 0;
    private int maxCombo = 0;
    private int totalNotes = 0;
    private float judgeScorePerNote = 0;
    private float comboScorePerNote = 0;
    
    // 判定统计数据
    public int PerfectCount { get; private set; } = 0;
    public int GoodCount { get; private set; } = 0;
    public int BadCount { get; private set; } = 0;
    public int MissCount { get; private set; } = 0;

    public static ScoreDisplay Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        UpdateScoreDisplay();
        UpdateComboDisplay();
    }

    void Update()
    {
        if (displayedScore < currentScore)
        {
            displayedScore = Mathf.MoveTowards(displayedScore, currentScore, scrollSpeed * Time.deltaTime);
            UpdateScoreDisplay();
        }
    }

    /// <summary>
    /// 初始化谱面相关参数
    /// </summary>
    /// <param name="notesCount">谱面中的总音符数</param>
    public void Initialize(int notesCount)
    {
        totalNotes = notesCount;
        if (totalNotes > 0)
        {
            judgeScorePerNote = (MAX_TOTAL_SCORE * JUDGE_SCORE_RATIO) / totalNotes;
            comboScorePerNote = (MAX_TOTAL_SCORE * COMBO_SCORE_RATIO) / totalNotes;
        }
        ResetScore();
        Debug.Log($"ScoreDisplay: 初始化完成。总音符: {totalNotes}, 每音符判定分: {judgeScorePerNote:F2}, Combo分: {comboScorePerNote:F2}");
    }

    /// <summary>
    /// 根据判定结果更新分数和 Combo
    /// </summary>
    public void AddScoreByJudge(JudgeResult result)
    {
        if (totalNotes <= 0) return;

        float scoreToAdd = 0;

        // 1. 计算判定分
        float weight = result switch
        {
            JudgeResult.Perfect => 1.0f,
            JudgeResult.Good => 0.6f,
            JudgeResult.Bad => 0.2f,
            _ => 0f
        };
        scoreToAdd += judgeScorePerNote * weight;

        // 2. 更新 Combo 并计算 Combo 分
        if (result == JudgeResult.Perfect || result == JudgeResult.Good)
        {
            currentCombo++;
            maxCombo = Mathf.Max(maxCombo, currentCombo);
            scoreToAdd += comboScorePerNote; // 只要不断 Combo 就给 Combo 分
            
            // 统计判定数
            if (result == JudgeResult.Perfect) PerfectCount++;
            else if (result == JudgeResult.Good) GoodCount++;
        }
        else if (result == JudgeResult.Bad || result == JudgeResult.Miss)
        {
            currentCombo = 0; // 断 Combo
            
            // 统计判定数
            if (result == JudgeResult.Bad) BadCount++;
            else if (result == JudgeResult.Miss) MissCount++;
        }

        AddScore(scoreToAdd);
        UpdateComboDisplay();
    }

    /// <summary>
    /// 手动增加分数（用于特殊加分，如 Hold 持续加分）
    /// </summary>
    /// <param name="addScore">增加的分数</param>
    public void AddScore(float addScore)
    {
        currentScore += addScore;
        // 确保不会因为浮点误差超过 1,000,000
        currentScore = Mathf.Min(currentScore, MAX_TOTAL_SCORE);
    }

    private void UpdateScoreDisplay()
    {
        if (scoreText == null) return;
        int scoreInt = Mathf.FloorToInt(displayedScore);
        scoreText.text = scorePrefix + scoreInt.ToString("D" + scoreDigit);
    }

    private void UpdateComboDisplay()
    {
        if (comboText == null) return;
        
        if (currentCombo > 0)
        {
            comboText.gameObject.SetActive(true);
            comboText.text = currentCombo.ToString() + comboSuffix;
        }
        else
        {
            comboText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 重置分数和 Combo（用于重新开始）
    /// </summary>
    public void ResetScore()
    {
        currentScore = 0;
        displayedScore = 0;
        currentCombo = 0;
        PerfectCount = 0;
        GoodCount = 0;
        BadCount = 0;
        MissCount = 0;
        UpdateScoreDisplay();
        UpdateComboDisplay();
        Debug.Log("ScoreDisplay: 分数与 Combo 已重置");
    }
    
    /// <summary>
    /// 获取最终得分（整数）
    /// </summary>
    public int GetFinalScore()
    {
        return Mathf.FloorToInt(currentScore);
    }
    
    /// <summary>
    /// 根据判定结果计算评级（AP/S/A/B/C/F）
    /// </summary>
    public string CalculateRank()
    {
        if (totalNotes <= 0) return "F";
        
        // AP (All Perfect): 全部是 Perfect
        if (PerfectCount == totalNotes && GoodCount == 0 && BadCount == 0 && MissCount == 0)
            return "AP";
        
        // 计算准确率
        float accuracy = (PerfectCount * 1.0f + GoodCount * 0.6f + BadCount * 0.2f) / totalNotes;
        
        if (accuracy >= 0.95f && MissCount == 0)
            return "S";
        else if (accuracy >= 0.90f)
            return "A";
        else if (accuracy >= 0.80f)
            return "B";
        else if (accuracy >= 0.70f)
            return "C";
        else
            return "F";
    }
}
