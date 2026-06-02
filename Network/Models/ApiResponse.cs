/// <summary>
/// 统一API响应格式
/// </summary>
[System.Serializable]
public class ApiResponse<T>
{
    public int code;            // 状态码：200成功，400客户端错误，500服务器错误
    public string message;      // 响应消息
    public T data;              // 响应数据
    
    public bool IsSuccess => code == 200;
    
    public ApiResponse()
    {
        code = 0;
        message = "";
    }
}

/// <summary>
/// 登录请求数据
/// </summary>
[System.Serializable]
public class LoginRequest
{
    public string username;
    public string password;
    
    public LoginRequest(string username, string password)
    {
        this.username = username;
        this.password = password;
    }
}

/// <summary>
/// 注册请求数据
/// </summary>
[System.Serializable]
public class RegisterRequest
{
    public string username;
    public string password;
    public string email;
    
    public RegisterRequest(string username, string password, string email)
    {
        this.username = username;
        this.password = password;
        this.email = email;
    }
}

/// <summary>
/// Token响应数据
/// </summary>
[System.Serializable]
public class TokenResponse
{
    public string accessToken;      // 访问令牌
    public string refreshToken;     // 刷新令牌
    public long expiresIn;          // 过期时间（秒）
}
