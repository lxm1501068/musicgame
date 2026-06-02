using UnityEngine;

/// <summary>
/// 用户数据模型
/// </summary>
[System.Serializable]
public class User
{
    public string userId;           // 用户ID
    public string username;         // 用户名
    public string email;            // 邮箱
    public int level;               // 等级
    public long totalScore;         // 总分数
    public int playCount;           // 游玩次数
    public string avatarUrl;        // 头像URL
    public long createTime;         // 创建时间（时间戳）
    public long lastLoginTime;      // 最后登录时间
    
    public User()
    {
        userId = "";
        username = "";
        email = "";
        level = 1;
        totalScore = 0;
        playCount = 0;
        avatarUrl = "";
        createTime = 0;
        lastLoginTime = 0;
    }
}
