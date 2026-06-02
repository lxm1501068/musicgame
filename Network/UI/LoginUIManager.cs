using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;

/// <summary>
/// 登录/注册界面管理器
/// </summary>
public class LoginUIManager : MonoBehaviour
{
    [Header("面板引用")]
    public GameObject loginPanel;       // 登录面板
    public GameObject registerPanel;    // 注册面板
    
    [Header("登录面板UI")]
    public TMP_InputField loginUsernameInput;   // 登录用户名输入框
    public TMP_InputField loginPasswordInput;   // 登录密码输入框
    public Button loginButton;                  // 登录按钮
    public Button switchToRegisterButton;       // 切换到注册按钮
    public TextMeshProUGUI loginMessageText;    // 登录消息文本
    
    [Header("注册面板UI")]
    public TMP_InputField registerUsernameInput; // 注册用户名输入框
    public TMP_InputField registerPasswordInput; // 注册密码输入框
    public TMP_InputField registerEmailInput;    // 注册邮箱输入框
    public Button registerButton;                // 注册按钮
    public Button switchToLoginButton;           // 切换到登录按钮
    public TextMeshProUGUI registerMessageText;  // 注册消息文本
    
    [Header("加载提示")]
    public GameObject loadingPanel;              // 加载面板
    public TextMeshProUGUI loadingText;          // 加载文本

    private void Start()
    {
        InitializeUI();
        
        // 如果已经登录，直接显示主界面
        if (AuthService.Instance.IsLoggedIn())
        {
            Debug.Log("[LoginUI] 检测到已登录状态");
            // TODO: 这里可以跳转到主场景
        }
    }

    /// <summary>
    /// 初始化UI事件
    /// </summary>
    private void InitializeUI()
    {
        // 登录按钮
        if (loginButton != null)
        {
            loginButton.onClick.AddListener(OnLoginClicked);
        }
        
        // 注册按钮
        if (registerButton != null)
        {
            registerButton.onClick.AddListener(OnRegisterClicked);
        }
        
        // 切换到注册
        if (switchToRegisterButton != null)
        {
            switchToRegisterButton.onClick.AddListener(ShowRegisterPanel);
        }
        
        // 切换到登录
        if (switchToLoginButton != null)
        {
            switchToLoginButton.onClick.AddListener(ShowLoginPanel);
        }
        
        // 初始显示登录面板
        ShowLoginPanel();
    }

    /// <summary>
    /// 显示登录面板
    /// </summary>
    private void ShowLoginPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (registerPanel != null) registerPanel.SetActive(false);
        ClearMessages();
    }

    /// <summary>
    /// 显示注册面板
    /// </summary>
    private void ShowRegisterPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (registerPanel != null) registerPanel.SetActive(true);
        ClearMessages();
    }

    /// <summary>
    /// 清除所有消息
    /// </summary>
    private void ClearMessages()
    {
        if (loginMessageText != null) loginMessageText.text = "";
        if (registerMessageText != null) registerMessageText.text = "";
    }

    /// <summary>
    /// 显示加载状态
    /// </summary>
    private void ShowLoading(string message = "加载中...")
    {
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (loadingText != null) loadingText.text = message;
    }

    /// <summary>
    /// 隐藏加载状态
    /// </summary>
    private void HideLoading()
    {
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    /// <summary>
    /// 登录按钮点击事件
    /// </summary>
    private async void OnLoginClicked()
    {
        string username = loginUsernameInput?.text.Trim();
        string password = loginPasswordInput?.text;

        // 验证输入
        if (string.IsNullOrEmpty(username))
        {
            ShowLoginMessage("请输入用户名", Color.red);
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowLoginMessage("请输入密码", Color.red);
            return;
        }

        // 显示加载状态
        ShowLoading("登录中...");

        try
        {
            // 调用登录服务
            var response = await AuthService.Instance.Login(username, password);

            if (response.IsSuccess)
            {
                ShowLoginMessage("登录成功！", Color.green);
                
                // 获取用户信息
                await GetUserInfoAndProceed();
            }
            else
            {
                ShowLoginMessage(response.message, Color.red);
            }
        }
        catch (System.Exception ex)
        {
            ShowLoginMessage($"登录失败: {ex.Message}", Color.red);
        }
        finally
        {
            HideLoading();
        }
    }

    /// <summary>
    /// 注册按钮点击事件
    /// </summary>
    private async void OnRegisterClicked()
    {
        string username = registerUsernameInput?.text.Trim();
        string password = registerPasswordInput?.text;
        string email = registerEmailInput?.text.Trim();

        // 验证输入
        if (string.IsNullOrEmpty(username))
        {
            ShowRegisterMessage("请输入用户名", Color.red);
            return;
        }

        if (username.Length < 3)
        {
            ShowRegisterMessage("用户名至少3个字符", Color.red);
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowRegisterMessage("请输入密码", Color.red);
            return;
        }

        if (password.Length < 6)
        {
            ShowRegisterMessage("密码至少6个字符", Color.red);
            return;
        }

        if (string.IsNullOrEmpty(email))
        {
            ShowRegisterMessage("请输入邮箱", Color.red);
            return;
        }

        // 显示加载状态
        ShowLoading("注册中...");

        try
        {
            // 调用注册服务
            var response = await AuthService.Instance.Register(username, password, email);

            if (response.IsSuccess)
            {
                ShowRegisterMessage("注册成功！", Color.green);
                
                // 获取用户信息
                await GetUserInfoAndProceed();
            }
            else
            {
                ShowRegisterMessage(response.message, Color.red);
            }
        }
        catch (System.Exception ex)
        {
            ShowRegisterMessage($"注册失败: {ex.Message}", Color.red);
        }
        finally
        {
            HideLoading();
        }
    }

    /// <summary>
    /// 获取用户信息并继续
    /// </summary>
    private async Task GetUserInfoAndProceed()
    {
        ShowLoading("获取用户信息...");
        
        try
        {
            var response = await AuthService.Instance.GetUserInfo();
            
            if (response.IsSuccess)
            {
                Debug.Log($"[LoginUI] 欢迎, {response.data.username}!");
                
                // TODO: 跳转到主场景
                // UnityEngine.SceneManagement.SceneManager.LoadScene("LevelScene");
            }
            else
            {
                Debug.LogWarning($"[LoginUI] 获取用户信息失败: {response.message}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LoginUI] 获取用户信息异常: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    /// <summary>
    /// 显示登录消息
    /// </summary>
    private void ShowLoginMessage(string message, Color color)
    {
        if (loginMessageText != null)
        {
            loginMessageText.text = message;
            loginMessageText.color = color;
        }
    }

    /// <summary>
    /// 显示注册消息
    /// </summary>
    private void ShowRegisterMessage(string message, Color color)
    {
        if (registerMessageText != null)
        {
            registerMessageText.text = message;
            registerMessageText.color = color;
        }
    }
}
