using UnityEngine;
using TMPro; // 若用UGUI Text则替换为using UnityEngine.UI;

/// <summary>
/// 得分显示组件
/// 仅处理显示，得分计算逻辑由外部（如Tap.cs）调用SetScore传入
/// </summary>
public class ScoreDisplay : MonoBehaviour
{
    [Header("UI配置")]
    [Tooltip("显示得分的文本组件（TMP_Text/Text）")]
    public TMP_Text scoreText; // 若用UGUI Text则改为public Text scoreText;
    [Tooltip("得分文本的前缀（如“得分：”）")]
    public string scorePrefix = "得分：";
    [Tooltip("得分显示的位数补零（如6位：000000），0则不补零")]
    public int scoreDigit = 6;

    private int currentScore = 0; // 当前得分

    void Start()
    {
        // 初始化显示0分
        UpdateScoreDisplay();
    }

    /// <summary>
    /// 外部调用：设置当前得分
    /// </summary>
    /// <param name="newScore">新得分</param>
    public void SetScore(int newScore)
    {
        currentScore = Mathf.Max(0, newScore); // 确保得分非负
        UpdateScoreDisplay();
    }

    /// <summary>
    /// 外部调用：增加得分（便捷方法）
    /// </summary>
    /// <param name="addScore">增加的分数</param>
    public void AddScore(int addScore)
    {
        currentScore = Mathf.Max(0, currentScore + addScore);
        UpdateScoreDisplay();
    }

    /// <summary>
    /// 更新得分显示文本
    /// </summary>
    private void UpdateScoreDisplay()
    {
        if (scoreDigit > 0)
        {
            // 补零显示（如6位：123 → 000123）
            string scoreStr = currentScore.ToString($"D{scoreDigit}");
            scoreText.text = $"{scorePrefix}{scoreStr}";
        }
        else
        {
            // 不补零显示
            scoreText.text = $"{scorePrefix}{currentScore}";
        }
    }

    // 供外部调用的重置方法（游戏重启/结算时）
    public void ResetScore()
    {
        currentScore = 0;
        UpdateScoreDisplay();
    }
}