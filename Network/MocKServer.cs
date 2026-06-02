using System;
using System.Threading.Tasks;

/// <summary>
/// Mock服务器（用于无真实服务器时的测试）
/// </summary>
public class MockServer
{
    /// <summary>
    /// 模拟登录
    /// </summary>
    public static async Task<ApiResponse<TokenResponse>> Login(string username, string password)
    {
        await Task.Delay(500); // 模拟网络延迟

        // 简单验证（实际应该查询数据库）
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return new ApiResponse<TokenResponse>
            {
                code = 400,
                message = "用户名或密码不能为空"
            };
        }

        // 模拟成功响应
        var tokenResponse = new TokenResponse
        {
            accessToken = $"mock_access_token_{username}_{DateTime.Now.Ticks}",
            refreshToken = $"mock_refresh_token_{username}_{DateTime.Now.Ticks}",
            expiresIn = 3600
        };

        return new ApiResponse<TokenResponse>
        {
            code = 200,
            message = "登录成功",
            data = tokenResponse
        };
    }

    /// <summary>
    /// 模拟注册
    /// </summary>
    public static async Task<ApiResponse<TokenResponse>> Register(string username, string password, string email)
    {
        await Task.Delay(500); // 模拟网络延迟

        // 简单验证
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(email))
        {
            return new ApiResponse<TokenResponse>
            {
                code = 400,
                message = "所有字段都不能为空"
            };
        }

        if (username.Length < 3)
        {
            return new ApiResponse<TokenResponse>
            {
                code = 400,
                message = "用户名至少3个字符"
            };
        }

        if (password.Length < 6)
        {
            return new ApiResponse<TokenResponse>
            {
                code = 400,
                message = "密码至少6个字符"
            };
        }

        // 模拟成功响应
        var tokenResponse = new TokenResponse
        {
            accessToken = $"mock_access_token_{username}_{DateTime.Now.Ticks}",
            refreshToken = $"mock_refresh_token_{username}_{DateTime.Now.Ticks}",
            expiresIn = 3600
        };

        return new ApiResponse<TokenResponse>
        {
            code = 200,
            message = "注册成功",
            data = tokenResponse
        };
    }

    /// <summary>
    /// 模拟获取用户信息
    /// </summary>
    public static async Task<ApiResponse<User>> GetUserInfo(string userId)
    {
        await Task.Delay(300); // 模拟网络延迟

        // 模拟用户数据
        var user = new User
        {
            userId = userId,
            username = "Player" + userId.Substring(0, Math.Min(4, userId.Length)),
            email = $"player{userId}@example.com",
            level = UnityEngine.Random.Range(1, 50),
            totalScore = UnityEngine.Random.Range(1000, 100000),
            playCount = UnityEngine.Random.Range(10, 500),
            avatarUrl = "",
            createTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 86400 * 30,
            lastLoginTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        return new ApiResponse<User>
        {
            code = 200,
            message = "获取成功",
            data = user
        };
    }
}
