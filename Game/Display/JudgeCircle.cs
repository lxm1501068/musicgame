using UnityEngine;
using System.Collections;

/// <summary>
/// 判定结果圆形显示组件
/// 功能：在按键位置显示判定结果的圆形，0.2秒内逐渐变透明消失
/// </summary>
public class JudgeCircle : MonoBehaviour
{
    [Header("颜色配置")]
    [Tooltip("Perfect判定颜色（金色）")]
    public Color perfectColor = new Color(1f, 0.84f, 0f, 1f); // 金色
    [Tooltip("Good判定颜色（绿色）")]
    public Color goodColor = new Color(0f, 1f, 0f, 1f); // 绿色
    [Tooltip("Bad判定颜色（红色）")]
    public Color badColor = new Color(1f, 0f, 0f, 1f); // 红色

    [Header("动画配置")]
    [Tooltip("圆形消失时间（秒）")]
    public float fadeDuration = 0.2f;
    [Tooltip("圆形初始大小")]
    public float initialScale = 1.5f;

    private SpriteRenderer spriteRenderer;
    private Coroutine fadeCoroutine;
    private bool isPulsing = false;

    void Awake()
    {
        // 确保有SpriteRenderer组件
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        // 创建一个圆形精灵（如果还没有的话）
        if (spriteRenderer.sprite == null)
        {
            spriteRenderer.sprite = CreateCircleSprite();
        }

        // 初始隐藏
        spriteRenderer.enabled = false;
    }

    /// <summary>
    /// 显示判定结果圆形
    /// </summary>
    /// <param name="position">显示位置（按键位置）</param>
    /// <param name="result">判定结果</param>
    public void ShowJudgeCircle(Vector2 position, JudgeResult result)
    {
        ShowJudgeCircle(position, result, false);
    }

    /// <summary>
    /// 显示判定结果圆形
    /// </summary>
    /// <param name="position">显示位置（按键位置）</param>
    /// <param name="result">判定结果</param>
    /// <param name="isHoldNote">是否为Hold音符</param>
    public void ShowJudgeCircle(Vector2 position, JudgeResult result, bool isHoldNote)
    {
        // 停止之前的动画
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        isPulsing = false;

        // 设置位置
        transform.position = position;

        // 根据判定结果设置颜色
        Color targetColor = result switch
        {
            JudgeResult.Perfect => perfectColor,
            JudgeResult.Good => goodColor,
            JudgeResult.Bad => badColor,
            _ => Color.white // Miss或其他情况使用白色
        };

        // 重置状态
        spriteRenderer.color = new Color(targetColor.r, targetColor.g, targetColor.b, 1f);
        transform.localScale = Vector3.one * initialScale;
        spriteRenderer.enabled = true;

        // 开始动画
        if (isHoldNote)
        {
            // Hold音符：启动循环淡出淡入动画
            isPulsing = true;
            fadeCoroutine = StartCoroutine(PulseAnimation(targetColor));
        }
        else
        {
            // 普通音符：单次淡出动画
            fadeCoroutine = StartCoroutine(FadeOut());
        }
    }

    /// <summary>
    /// 立即隐藏圆形（用于Hold音符提前结束等情况）
    /// </summary>
    public void HideImmediately()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
        isPulsing = false;
        spriteRenderer.enabled = false;
    }

    /// <summary>
    /// 淡出动画协程
    /// </summary>
    private IEnumerator FadeOut()
    {
        float elapsed = 0f;
        Color startColor = spriteRenderer.color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;
            
            // 线性插值透明度
            float alpha = Mathf.Lerp(1f, 0f, t);
            spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            
            yield return null;
        }

        // 确保完全透明
        spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, 0f);
        spriteRenderer.enabled = false;
        fadeCoroutine = null;
    }

    /// <summary>
    /// 循环淡出淡入动画（用于Hold音符）
    /// 周期为 0.2*2 = 0.4秒
    /// </summary>
    private IEnumerator PulseAnimation(Color baseColor)
    {
        float halfCycle = fadeDuration; // 0.2秒
        
        while (isPulsing)
        {
            // 淡出阶段：从完全不透明到完全透明（0.2秒）
            float elapsed = 0f;
            while (elapsed < halfCycle && isPulsing)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfCycle;
                float alpha = Mathf.Lerp(1f, 0f, t);
                spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }

            if (!isPulsing) break;

            // 淡入阶段：从完全透明到完全不透明（0.2秒）
            elapsed = 0f;
            while (elapsed < halfCycle && isPulsing)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfCycle;
                float alpha = Mathf.Lerp(0f, 1f, t);
                spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }
        }

        fadeCoroutine = null;
    }

    /// <summary>
    /// 创建一个简单的圆形精灵
    /// </summary>
    private Sprite CreateCircleSprite()
    {
        // 创建一个2x2的纹理
        Texture2D texture = new Texture2D(2, 2);
        
        // 填充为白色（颜色由SpriteRenderer控制）
        Color[] pixels = new Color[4];
        for (int i = 0; i < 4; i++)
        {
            pixels[i] = Color.white;
        }
        texture.SetPixels(pixels);
        texture.Apply();

        // 创建精灵
        return Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
    }

    void OnDestroy()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
    }
}
