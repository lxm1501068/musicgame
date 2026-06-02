using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Threading.Tasks;

/// <summary>
/// HTTP请求封装类
/// </summary>
public class HttpRequest
{
    /// <summary>
    /// GET请求
    /// </summary>
    public static async Task<ApiResponse<T>> GetAsync<T>(string url)
    {
        try
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = NetworkConfig.Instance.timeout;
                
                // 异步发送请求
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

#if UNITY_2020_1_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isNetworkError || request.isHttpError)
#endif
                {
                    Debug.LogError($"GET请求失败: {request.error} | URL: {url}");
                    return new ApiResponse<T> 
                    { 
                        code = 500, 
                        message = $"网络错误: {request.error}" 
                    };
                }

                string json = request.downloadHandler.text;
                ApiResponse<T> response = JsonUtility.FromJson<ApiResponse<T>>(json);
                return response;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"GET请求异常: {ex.Message}");
            return new ApiResponse<T> 
            { 
                code = 500, 
                message = $"请求异常: {ex.Message}" 
            };
        }
    }

    /// <summary>
    /// POST请求
    /// </summary>
    public static async Task<ApiResponse<T>> PostAsync<T>(string url, object data)
    {
        try
        {
            string json = JsonUtility.ToJson(data);
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = NetworkConfig.Instance.timeout;

                // 异步发送请求
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

#if UNITY_2020_1_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isNetworkError || request.isHttpError)
#endif
                {
                    Debug.LogError($"POST请求失败: {request.error} | URL: {url}");
                    return new ApiResponse<T> 
                    { 
                        code = 500, 
                        message = $"网络错误: {request.error}" 
                    };
                }

                string responseJson = request.downloadHandler.text;
                ApiResponse<T> response = JsonUtility.FromJson<ApiResponse<T>>(responseJson);
                return response;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"POST请求异常: {ex.Message}");
            return new ApiResponse<T> 
            { 
                code = 500, 
                message = $"请求异常: {ex.Message}" 
            };
        }
    }
}
