﻿using UnityEngine;

public class KeySpriteSwitcher : MonoBehaviour
{
    [Header("要监听的按键组编号（1-11）")]
    public int targetGroupNumber;

    [Header("是否按住期间保持显示")]
    public bool keepVisibleWhileHeld = true;

    [Header("切换的Sprite资源")]
    public Sprite sprite1; // 收到信号时显示的Sprite
    public Sprite sprite2; // 未收到信号时显示的Sprite

    private SpriteRenderer spriteRenderer;

    void Start()
    {
        // 获取SpriteRenderer组件并校验资源
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (spriteRenderer == null)
        {
            Debug.LogError($"【{gameObject.name}】物体上未挂载SpriteRenderer组件！");
            enabled = false;
            return;
        }
        
        if (sprite1 == null || sprite2 == null)
        {
            Debug.LogError($"【{gameObject.name}】请为sprite1和sprite2赋值Sprite资源！");
            enabled = false;
            return;
        }

        // 初始状态默认显示sprite2
        spriteRenderer.sprite = sprite2;
    }

    void Update()
    {
        // 仅当游戏处于播放状态时执行逻辑
        if (GameManager.Instance != null && GameManager.Instance.IsPlaying)
        {
            // 空引用保护
            if (spriteRenderer == null || sprite1 == null || sprite2 == null) return;

            // 监听输入并切换Sprite
            if (InputManager.Instance != null)
            {
                bool hasSignal = keepVisibleWhileHeld 
                    ? InputManager.Instance.IsGroupHeld(targetGroupNumber)
                    : InputManager.Instance.IsGroupPressed(targetGroupNumber);
                
                UpdateSprite(hasSignal);
            }
        }
    }

    /// <summary>
    /// 根据信号状态更新Sprite
    /// </summary>
    private void UpdateSprite(bool hasSignal)
    {
        if (hasSignal && spriteRenderer.sprite != sprite1)
        {
            spriteRenderer.sprite = sprite1;
        }
        else if (!hasSignal && spriteRenderer.sprite != sprite2)
        {
            spriteRenderer.sprite = sprite2;
        }
    }
}