using UnityEngine;

/// <summary>
/// 网络配置（服务器地址等）
/// </summary>
public class NetworkConfig : MonoBehaviour
{
    private static NetworkConfig _instance;
    public static NetworkConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("NetworkConfig");
                _instance = obj.AddComponent<NetworkConfig>();
                DontDestroyOnLoad(obj);
            }
            return _instance;
        }
    }

    [Header("服务器配置")]
    public string baseUrl = "http://localhost:3000/api";  // 服务器基础URL
    public int timeout = 10;                                // 请求超时时间（秒）
    
    [Header("调试模式")]
    public bool useMockServer = true;  // 是否使用Mock服务器（无真实服务器时启用）
    
    [Header("本地存储Key")]
    public string tokenKey = "user_token";
    public string userIdKey = "user_id";
    public string usernameKey = "username";

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 获取完整的API URL
    /// </summary>
    public string GetApiUrl(string endpoint)
    {
        return $"{baseUrl}/{endpoint.TrimStart('/')}";
    }
}
