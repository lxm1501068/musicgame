using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class StartUIManager : MonoBehaviour
{
    [Header("游戏对象")]
    [SerializeField] private GameObject studioLogo;      // 工作室图标
    [SerializeField] private GameObject gameTitle;       // 游戏名称
    [SerializeField] private GameObject buttonPanel;     // 按钮面板
    
    [Header("音频")]
    [SerializeField] private AudioSource bgmAudioSource; // BGM音频源
    [SerializeField] private AudioClip bgmClip;          // BGM音频文件
    
    [Header("按钮")]
    [SerializeField] private Button levelButton;         // 关卡按钮
    [SerializeField] private Button communityButton;     // 社区按钮
    [SerializeField] private Button settingsButton;      // 设置按钮
    [SerializeField] private Button createButton;        // 制作谱面按钮
    
    [Header("场景名称")]
    [SerializeField] private string levelSceneName = "LevelScene";        // 关卡场景名
    [SerializeField] private string communitySceneName = "CommunityScene"; // 社区场景名
    [SerializeField] private string settingsSceneName = "SettingsScene";  // 设置场景名
    [SerializeField] private string createSceneName = "CreateScene";      // 制作谱面场景名
    
    // 新增警告对象
    [Header("警告")]
    [SerializeField] private GameObject warningPanel;    // 警告面板

    private void Start()
    {
        // 初始化所有对象状态
        InitializeUI();
        
        // 开始UI动画序列
        StartCoroutine(UISequence());
    }
    
    // 初始化UI状态
    private void InitializeUI()
    {
        // 开始时隐藏所有UI元素
        if (studioLogo != null)
            studioLogo.SetActive(false);
            
        if (gameTitle != null)
            gameTitle.SetActive(false);
            
        if (buttonPanel != null)
            buttonPanel.SetActive(false);

        // 新增：隐藏警告面板
        if (warningPanel != null)
            warningPanel.SetActive(false);
            
        // 添加按钮点击事件
        if (levelButton != null)
            levelButton.onClick.AddListener(() => LoadScene(levelSceneName));
            
        if (communityButton != null)
            communityButton.onClick.AddListener(() => LoadScene(communitySceneName));
            
        if (settingsButton != null)
            settingsButton.onClick.AddListener(() => LoadScene(settingsSceneName));
            
        if (createButton != null)
            createButton.onClick.AddListener(() => LoadScene(createSceneName));
    }
    
    // UI显示序列
    private IEnumerator UISequence()
    {
        // 播放BGM
        if (bgmAudioSource != null && bgmClip != null)
        {
            bgmAudioSource.clip = bgmClip;
            bgmAudioSource.loop = true;
            bgmAudioSource.Play();
        }
        
        // 显示工作室图标2.5秒
        if (studioLogo != null)
        {
            studioLogo.SetActive(true);
            yield return new WaitForSeconds(2.5f);
            studioLogo.SetActive(false);
        }

        // 新增：显示警告面板2.5秒
        if (warningPanel != null)
        {
            warningPanel.SetActive(true);
            yield return new WaitForSeconds(2.5f);
            warningPanel.SetActive(false);
        }
        
        // 显示游戏名称
        if (gameTitle != null)
        {
            gameTitle.SetActive(true);
        }
        
        // 等待0.5秒
        yield return new WaitForSeconds(0.5f);
        
        // 显示按钮面板
        if (buttonPanel != null)
        {
            buttonPanel.SetActive(true);
            
            // 可以添加按钮动画效果
            StartCoroutine(ButtonAnimation());
        }
    }
    
    // 按钮出现动画效果
    private IEnumerator ButtonAnimation()
    {
        Button[] buttons = buttonPanel.GetComponentsInChildren<Button>();
        
        foreach (Button button in buttons)
        {
            if (button != null)
            {
                // 设置初始缩放为0
                button.transform.localScale = Vector3.zero;
                
                // 播放放大动画
                float duration = 0.3f;
                float elapsedTime = 0f;
                
                while (elapsedTime < duration)
                {
                    float scale = Mathf.Lerp(0f, 1f, elapsedTime / duration);
                    button.transform.localScale = new Vector3(scale, scale, scale);
                    elapsedTime += Time.deltaTime;
                    yield return null;
                }
                
                button.transform.localScale = Vector3.one;
            }
        }
    }
    
    // 加载场景
    private void LoadScene(string sceneName)
    {
        // 停止BGM
        if (bgmAudioSource != null)
        {
            bgmAudioSource.Stop();
        }
        
        // 加载新场景
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning("场景名称未设置！");
        }
    }
    
    // 可选：在对象销毁时停止BGM
    private void OnDestroy()
    {
        if (bgmAudioSource != null)
        {
            bgmAudioSource.Stop();
        }
    }
}