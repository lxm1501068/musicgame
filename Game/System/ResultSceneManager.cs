using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// 结算场景管理器
/// 功能：显示最终得分、评级（AP/S/A/B/C/F）、判定统计（Perfect/Good/Bad/Miss）
/// </summary>
public class ResultSceneManager : MonoBehaviour
{
    [Header("UI 引用 - 分数")]
    public TMP_Text scoreText;           // 显示最终分数
    
    [Header("UI 引用 - 评级")]
    public TMP_Text rankText;            // 显示评级（AP/S/A/B/C/F）
    public Image rankImage;              // 评级背景图片（可选）
    
    [Header("UI 引用 - 判定统计")]
    public TMP_Text perfectCountText;    // Perfect 数量
    public TMP_Text goodCountText;       // Good 数量
    public TMP_Text badCountText;        // Bad 数量
    public TMP_Text missCountText;       // Miss 数量
    
    [Header("UI 引用 - 按钮")]
    public Button retryButton;           // 重试按钮
    public Button backToLevelButton;     // 返回关卡选择按钮
    
    [Header("场景配置")]
    public string levelSceneName = "LevelScene";  // 关卡场景名
    public string gameSceneName = "GameScene";    // 游戏场景名（需要重新加载谱面）
    
    [Header("动画配置")]
    public float scoreAnimationDuration = 1.5f;   // 分数滚动动画时长
    public AnimationCurve scoreAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    // 运行时数据
    private int finalScore = 0;
    private string rank = "F";
    private float animationTimer = 0;
    private bool isAnimating = false;
    
    void Awake()
    {
        // 初始化按钮事件
        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(OnRetryClicked);
        }
        
        if (backToLevelButton != null)
        {
            backToLevelButton.onClick.RemoveAllListeners();
            backToLevelButton.onClick.AddListener(OnBackToLevelClicked);
        }
    }
    
    void Start()
    {
        // 从 ScoreDisplay 获取结算数据
        LoadResultData();
        
        // 显示结算信息
        DisplayResult();
        
        // 开始分数动画
        StartScoreAnimation();
    }
    
    void Update()
    {
        if (isAnimating)
        {
            UpdateScoreAnimation();
        }
    }
    
    /// <summary>
    /// 从 ScoreDisplay 加载结算数据
    /// </summary>
    private void LoadResultData()
    {
        if (ScoreDisplay.Instance != null)
        {
            finalScore = ScoreDisplay.Instance.GetFinalScore();
            rank = ScoreDisplay.Instance.CalculateRank();
            
            Debug.Log($"ResultSceneManager: 加载结算数据 - 分数: {finalScore}, 评级: {rank}");
            Debug.Log($"ResultSceneManager: Perfect: {ScoreDisplay.Instance.PerfectCount}, Good: {ScoreDisplay.Instance.GoodCount}, Bad: {ScoreDisplay.Instance.BadCount}, Miss: {ScoreDisplay.Instance.MissCount}");
        }
        else
        {
            Debug.LogWarning("ResultSceneManager: ScoreDisplay 实例不存在，使用默认数据");
            finalScore = 0;
            rank = "F";
        }
    }
    
    /// <summary>
    /// 显示结算信息
    /// </summary>
    private void DisplayResult()
    {
        // 显示分数（初始为0，通过动画递增）
        if (scoreText != null)
        {
            scoreText.text = "0";
        }
        
        // 显示评级
        if (rankText != null)
        {
            rankText.text = rank;
            
            // 根据评级设置颜色
            Color rankColor = GetRankColor(rank);
            rankText.color = rankColor;
            
            if (rankImage != null)
            {
                rankImage.color = new Color(rankColor.r, rankColor.g, rankColor.b, 0.3f);
            }
        }
        
        // 显示判定统计
        if (ScoreDisplay.Instance != null)
        {
            if (perfectCountText != null)
                perfectCountText.text = ScoreDisplay.Instance.PerfectCount.ToString();
            
            if (goodCountText != null)
                goodCountText.text = ScoreDisplay.Instance.GoodCount.ToString();
            
            if (badCountText != null)
                badCountText.text = ScoreDisplay.Instance.BadCount.ToString();
            
            if (missCountText != null)
                missCountText.text = ScoreDisplay.Instance.MissCount.ToString();
        }
    }
    
    /// <summary>
    /// 开始分数滚动动画
    /// </summary>
    private void StartScoreAnimation()
    {
        isAnimating = true;
        animationTimer = 0;
    }
    
    /// <summary>
    /// 更新分数动画
    /// </summary>
    private void UpdateScoreAnimation()
    {
        animationTimer += Time.deltaTime;
        
        if (animationTimer >= scoreAnimationDuration)
        {
            // 动画结束，显示最终分数
            if (scoreText != null)
            {
                scoreText.text = finalScore.ToString("D6");
            }
            isAnimating = false;
        }
        else
        {
            // 计算当前显示的分数
            float progress = animationTimer / scoreAnimationDuration;
            float easedProgress = scoreAnimationCurve.Evaluate(progress);
            int currentDisplayScore = Mathf.FloorToInt(finalScore * easedProgress);
            
            if (scoreText != null)
            {
                scoreText.text = currentDisplayScore.ToString("D6");
            }
        }
    }
    
    /// <summary>
    /// 根据评级获取颜色
    /// </summary>
    private Color GetRankColor(string rank)
    {
        switch (rank)
        {
            case "AP": return new Color(1.0f, 0.84f, 0.0f);   // 金色
            case "S":  return new Color(1.0f, 0.65f, 0.0f);   // 橙色
            case "A":  return new Color(0.0f, 0.8f, 1.0f);    // 蓝色
            case "B":  return new Color(0.2f, 1.0f, 0.2f);    // 绿色
            case "C":  return new Color(1.0f, 1.0f, 0.0f);    // 黄色
            case "F":  return new Color(0.8f, 0.2f, 0.2f);    // 红色
            default:   return Color.white;
        }
    }
    
    /// <summary>
    /// 重试按钮点击事件
    /// </summary>
    private void OnRetryClicked()
    {
        Debug.Log("ResultSceneManager: 重试按钮被点击");
        SceneManager.LoadScene(gameSceneName);
    }
    
    /// <summary>
    /// 返回关卡选择按钮点击事件
    /// </summary>
    private void OnBackToLevelClicked()
    {
        Debug.Log("ResultSceneManager: 返回关卡选择按钮被点击");
        SceneManager.LoadScene(levelSceneName);
    }
}
