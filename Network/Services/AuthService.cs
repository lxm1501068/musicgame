using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 认证服务（登录/注册/用户信息）
/// </summary>
public class AuthService
{
    private static AuthService _instance;
    public static AuthService Instance
    {
        get
        {
            if (_instance == null)
                _instance = new AuthService();
            return _instance;
        }
    }

    /// <summary>
    /// 用户登录
    /// </summary>
    public async Task<ApiResponse<TokenResponse>> Login(string username, string password)
    {
        Debug.Log($"[AuthService] 尝试登录: {username}");

        try
        {
            ApiResponse<TokenResponse> response;

            if (NetworkConfig.Instance.useMockServer)
            {
                // 使用Mock服务器
                response = await MockServer.Login(username, password);
            }
            else
            {
                // 使用真实服务器
                string url = NetworkConfig.Instance.GetApiUrl("auth/login");
                var requestData = new LoginRequest(username, password);
                response = await HttpRequest.PostAsync<TokenResponse>(url, requestData);
            }

            if (response.IsSuccess)
            {
                // 保存Token到本地
                SaveToken(response.data.accessToken);
                Debug.Log("[AuthService] 登录成功");
            }
            else
            {
                Debug.LogWarning($"[AuthService] 登录失败: {response.message}");
            }

            return response;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AuthService] 登录异常: {ex.Message}");
            return new ApiResponse<TokenResponse>
            {
                code = 500,
                message = $"登录异常: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 用户注册
    /// </summary>
    public async Task<ApiResponse<TokenResponse>> Register(string username, string password, string email)
    {
        Debug.Log($"[AuthService] 尝试注册: {username}");

        try
        {
            ApiResponse<TokenResponse> response;

            if (NetworkConfig.Instance.useMockServer)
            {
                // 使用Mock服务器
                response = await MockServer.Register(username, password, email);
            }
            else
            {
                // 使用真实服务器
                string url = NetworkConfig.Instance.GetApiUrl("auth/register");
                var requestData = new RegisterRequest(username, password, email);
                response = await HttpRequest.PostAsync<TokenResponse>(url, requestData);
            }

            if (response.IsSuccess)
            {
                // 保存Token到本地
                SaveToken(response.data.accessToken);
                Debug.Log("[AuthService] 注册成功");
            }
            else
            {
                Debug.LogWarning($"[AuthService] 注册失败: {response.message}");
            }

            return response;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AuthService] 注册异常: {ex.Message}");
            return new ApiResponse<TokenResponse>
            {
                code = 500,
                message = $"注册异常: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 获取用户信息
    /// </summary>
    public async Task<ApiResponse<User>> GetUserInfo()
    {
        string userId = PlayerPrefs.GetString(NetworkConfig.Instance.userIdKey, "");
        
        if (string.IsNullOrEmpty(userId))
        {
            Debug.LogWarning("[AuthService] 未找到用户ID，请先登录");
            return new ApiResponse<User>
            {
                code = 401,
                message = "未登录"
            };
        }

        try
        {
            ApiResponse<User> response;

            if (NetworkConfig.Instance.useMockServer)
            {
                // 使用Mock服务器
                response = await MockServer.GetUserInfo(userId);
            }
            else
            {
                // 使用真实服务器
                string url = NetworkConfig.Instance.GetApiUrl($"user/{userId}");
                response = await HttpRequest.GetAsync<User>(url);
            }

            if (response.IsSuccess)
            {
                // 保存用户信息到本地
                SaveUserInfo(response.data);
                Debug.Log($"[AuthService] 获取用户信息成功: {response.data.username}");
            }

            return response;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AuthService] 获取用户信息异常: {ex.Message}");
            return new ApiResponse<User>
            {
                code = 500,
                message = $"获取用户信息异常: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 登出
    /// </summary>
    public void Logout()
    {
        PlayerPrefs.DeleteKey(NetworkConfig.Instance.tokenKey);
        PlayerPrefs.DeleteKey(NetworkConfig.Instance.userIdKey);
        PlayerPrefs.DeleteKey(NetworkConfig.Instance.usernameKey);
        PlayerPrefs.Save();
        
        Debug.Log("[AuthService] 已登出");
    }

    /// <summary>
    /// 检查是否已登录
    /// </summary>
    public bool IsLoggedIn()
    {
        return !string.IsNullOrEmpty(PlayerPrefs.GetString(NetworkConfig.Instance.tokenKey, ""));
    }

    /// <summary>
    /// 获取当前用户名
    /// </summary>
    public string GetCurrentUsername()
    {
        return PlayerPrefs.GetString(NetworkConfig.Instance.usernameKey, "Guest");
    }

    #region 本地存储辅助方法

    /// <summary>
    /// 保存Token
    /// </summary>
    private void SaveToken(string token)
    {
        PlayerPrefs.SetString(NetworkConfig.Instance.tokenKey, token);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 保存用户信息
    /// </summary>
    private void SaveUserInfo(User user)
    {
        PlayerPrefs.SetString(NetworkConfig.Instance.userIdKey, user.userId);
        PlayerPrefs.SetString(NetworkConfig.Instance.usernameKey, user.username);
        PlayerPrefs.Save();
    }

    #endregion
}
