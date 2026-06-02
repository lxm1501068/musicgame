# 账号登录系统

## 📁 文件结构

```
Assets/code/Network/
├── Models/
│   ├── User.cs              # 用户数据模型
│   └── ApiResponse.cs       # API响应格式
├── Services/
│   └── AuthService.cs       # 认证服务（登录/注册）
├── UI/
│   └── LoginUIManager.cs    # 登录界面管理器
├── NetworkConfig.cs         # 网络配置
├── HttpRequest.cs           # HTTP请求封装
└── MockServer.cs            # Mock服务器（测试用）
```

## 🚀 快速开始

### 1. Unity编辑器配置

#### 创建Canvas和UI元素
1. 创建 Canvas
2. 添加以下UI元素：
   - **登录面板** (loginPanel)
     - 用户名输入框 (TMP_InputField)
     - 密码输入框 (TMP_InputField)
     - 登录按钮 (Button)
     - 切换到注册按钮 (Button)
     - 消息文本 (TextMeshProUGUI)
   
   - **注册面板** (registerPanel)
     - 用户名输入框
     - 密码输入框
     - 邮箱输入框
     - 注册按钮
     - 切换到登录按钮
     - 消息文本
   
   - **加载面板** (loadingPanel)
     - 加载文本

3. 创建空GameObject，挂载 `LoginUIManager` 脚本
4. 在Inspector中绑定所有UI引用

### 2. 配置网络

编辑 `NetworkConfig.cs`：
```csharp
// 开发阶段（无服务器）
public bool useMockServer = true;  // 启用Mock模式

// 有服务器后
public string baseUrl = "http://your-server.com/api";
public bool useMockServer = false;  // 关闭Mock模式
```

### 3. 测试登录

运行游戏：
1. 输入任意用户名和密码
2. 点击登录
3. Mock服务器会模拟成功响应

## 💡 核心功能

### 登录
```csharp
var response = await AuthService.Instance.Login(username, password);
if (response.IsSuccess)
{
    // 登录成功，Token自动保存
}
```

### 注册
```csharp
var response = await AuthService.Instance.Register(username, password, email);
if (response.IsSuccess)
{
    // 注册成功
}
```

### 获取用户信息
```csharp
var response = await AuthService.Instance.GetUserInfo();
if (response.IsSuccess)
{
    User user = response.data;
    Debug.Log($"用户名: {user.username}, 等级: {user.level}");
}
```

### 检查登录状态
```csharp
if (AuthService.Instance.IsLoggedIn())
{
    // 已登录
}
```

### 登出
```csharp
AuthService.Instance.Logout();
```

## 🔧 API接口设计（供后端参考）

### 登录
- **URL**: `POST /api/auth/login`
- **请求体**:
```json
{
  "username": "player123",
  "password": "password123"
}
```
- **响应**:
```json
{
  "code": 200,
  "message": "登录成功",
  "data": {
    "accessToken": "eyJhbGci...",
    "refreshToken": "eyJhbGci...",
    "expiresIn": 3600
  }
}
```

### 注册
- **URL**: `POST /api/auth/register`
- **请求体**:
```json
{
  "username": "newplayer",
  "password": "password123",
  "email": "player@example.com"
}
```

### 获取用户信息
- **URL**: `GET /api/user/{userId}`
- **响应**:
```json
{
  "code": 200,
  "message": "获取成功",
  "data": {
    "userId": "12345",
    "username": "player123",
    "email": "player@example.com",
    "level": 10,
    "totalScore": 50000,
    "playCount": 100
  }
}
```

## 📝 错误码

| 代码 | 说明 |
|------|------|
| 200  | 成功 |
| 400  | 请求参数错误 |
| 401  | 未授权（未登录） |
| 403  | 禁止访问 |
| 404  | 资源不存在 |
| 500  | 服务器错误 |

## ⚙️ 切换真实服务器

当后端准备好后：

1. 修改 `NetworkConfig.cs`:
```csharp
public string baseUrl = "http://your-server.com/api";
public bool useMockServer = false;
```

2. 确保后端API符合上述接口规范

3. 测试登录、注册功能

## 🎨 UI设计建议

### 登录面板布局
```
┌─────────────────────┐
│     用户登录         │
├─────────────────────┤
│ 用户名: [________]  │
│ 密  码: [________]  │
│                     │
│   [  登  录  ]      │
│                     │
│ 还没有账号？[注册]   │
└─────────────────────┘
```

### 样式建议
- 背景：半透明深色
- 输入框：圆角边框，聚焦时高亮
- 按钮：渐变色，悬停效果
- 消息文本：成功绿色，失败红色

## 🔒 安全建议

1. **密码加密**：前端发送前使用HTTPS，后端存储时使用bcrypt哈希
2. **Token管理**：AccessToken短期有效，RefreshToken长期有效
3. **输入验证**：前后端都要验证输入
4. **防暴力破解**：限制登录尝试次数

## 🐛 常见问题

### Q: Mock模式下能测试所有功能吗？
A: 可以。Mock服务器模拟了登录、注册、获取用户信息的完整流程。

### Q: 如何查看网络请求日志？
A: 所有请求都会在Unity控制台输出日志。

### Q: Token保存在哪里？
A: 使用PlayerPrefs保存在本地，key为"user_token"。

### Q: 如何跳转到主场景？
A: 在 `LoginUIManager.cs` 的 `GetUserInfoAndProceed()` 方法中取消注释SceneManager.LoadScene。

## 📚 扩展功能

后续可以添加：
- 忘记密码功能
- 第三方登录（微信/QQ）
- 记住密码
- 自动登录
- Token刷新机制
