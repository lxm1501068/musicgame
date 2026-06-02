using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 判定结果圆形管理器（单例模式）
/// 功能：管理JudgeCircle对象池，提供显示判定结果圆形的接口
/// </summary>
public class JudgeCircleManager : MonoBehaviour
{
    // 单例实例
    public static JudgeCircleManager Instance { get; private set; }

    [Header("预制体配置")]
    [Tooltip("JudgeCircle预制体")]
    public GameObject judgeCirclePrefab;

    [Header("对象池配置")]
    [Tooltip("初始对象池大小")]
    public int poolSize = 10;

    private Queue<JudgeCircle> circlePool = new Queue<JudgeCircle>();
    private List<JudgeCircle> activeHoldCircles = new List<JudgeCircle>();
    private Transform poolParent;

    void Awake()
    {
        // 单例逻辑
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 创建对象池父对象
        poolParent = new GameObject("JudgeCirclePool").transform;
        poolParent.SetParent(transform);

        // 初始化对象池
        InitializePool();
    }

    /// <summary>
    /// 初始化对象池
    /// </summary>
    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            CreateNewCircle();
        }
    }

    /// <summary>
    /// 创建新的JudgeCircle对象
    /// </summary>
    private JudgeCircle CreateNewCircle()
    {
        GameObject circleObj;
        
        if (judgeCirclePrefab != null)
        {
            circleObj = Instantiate(judgeCirclePrefab, poolParent);
        }
        else
        {
            // 如果没有预制体，动态创建
            circleObj = new GameObject("JudgeCircle");
            circleObj.transform.SetParent(poolParent);
        }

        JudgeCircle circle = circleObj.GetComponent<JudgeCircle>();
        if (circle == null)
        {
            circle = circleObj.AddComponent<JudgeCircle>();
        }

        circleObj.SetActive(false);
        circlePool.Enqueue(circle);
        return circle;
    }

    /// <summary>
    /// 从对象池获取一个JudgeCircle对象
    /// </summary>
    private JudgeCircle GetCircleFromPool()
    {
        if (circlePool.Count > 0)
        {
            return circlePool.Dequeue();
        }
        
        // 如果池为空，创建新的
        return CreateNewCircle();
    }

    /// <summary>
    /// 将JudgeCircle对象返回对象池
    /// </summary>
    private void ReturnCircleToPool(JudgeCircle circle)
    {
        circle.gameObject.SetActive(false);
        circlePool.Enqueue(circle);
    }

    /// <summary>
    /// 在指定位置显示判定结果圆形
    /// </summary>
    /// <param name="position">显示位置</param>
    /// <param name="result">判定结果</param>
    public void ShowJudgeCircle(Vector2 position, JudgeResult result)
    {
        ShowJudgeCircle(position, result, false);
    }

    /// <summary>
    /// 在指定位置显示判定结果圆形
    /// </summary>
    /// <param name="position">显示位置</param>
    /// <param name="result">判定结果</param>
    /// <param name="isHoldNote">是否为Hold音符</param>
    public void ShowJudgeCircle(Vector2 position, JudgeResult result, bool isHoldNote)
    {
        // Miss不显示圆形
        if (result == JudgeResult.Miss || result == JudgeResult.None)
            return;

        JudgeCircle circle = GetCircleFromPool();
        circle.gameObject.SetActive(true);
        circle.ShowJudgeCircle(position, result, isHoldNote);

        if (isHoldNote)
        {
            // Hold音符：将circle引用保存，等待手动隐藏
            activeHoldCircles.Add(circle);
        }
        else
        {
            // 普通音符：在淡出动画结束后返回对象池
            StartCoroutine(ReturnToPoolAfterFade(circle));
        }
    }

    /// <summary>
    /// 隐藏Hold音符的判定圆形（Hold结束时调用）
    /// </summary>
    public void HideHoldJudgeCircle()
    {
        foreach (var circle in activeHoldCircles)
        {
            circle.HideImmediately();
            ReturnCircleToPool(circle);
        }
        activeHoldCircles.Clear();
    }

    /// <summary>
    /// 等待淡出动画结束后返回对象池
    /// </summary>
    private System.Collections.IEnumerator ReturnToPoolAfterFade(JudgeCircle circle)
    {
        // 等待0.2秒（与fadeDuration一致）
        yield return new WaitForSeconds(0.2f);
        ReturnCircleToPool(circle);
    }

    /// <summary>
    /// 重置管理器（场景切换或重玩游戏时调用）
    /// </summary>
    public void ResetManager()
    {
        StopAllCoroutines();
        
        foreach (var circle in circlePool)
        {
            circle.HideImmediately();
            circle.gameObject.SetActive(false);
        }

        // 清理所有活跃的Hold圆形
        HideHoldJudgeCircle();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
