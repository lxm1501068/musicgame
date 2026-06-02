# 登录界面Unity设置步骤

## 🎯 完整设置流程

### 第1步：创建Canvas和基础UI

1. **创建Canvas**
   - Hierarchy → 右键 → UI → Canvas
   - 命名为 "LoginCanvas"

2. **设置Canvas Scaler**
   - 选择Canvas
   - Canvas Scaler组件：
     - UI Scale Mode: Scale With Screen Size
     - Reference Resolution: 1920 x 1080

---

### 第2步：创建登录面板

1. **创建登录Panel**
   - Canvas下创建 Panel
   - 命名为 "LoginPanel"
   - 设置大小：400x300
   - 居中显示

2. **添加标题文本**
   - LoginPanel下创建 Text - TextMeshPro
   - 文本："用户登录"
   - 字体大小：24
   - 居中对齐

3. **添加用户名输入框**
   - LoginPanel下创建 InputField - TextMeshPro
   - 命名为 "UsernameInput"
   - Placeholder文本："请输入用户名"
   - 位置：标题下方

4. **添加密码输入框**
   - LoginPanel下创建 InputField - TextMeshPro
   - 命名为 "PasswordInput"
   - Placeholder文本："请输入密码"
   - Content Type: Password
   - 位置：用户名下方

5. **添加登录按钮**
   - LoginPanel下创建 Button - TextMeshPro
   - 命名为 "LoginButton"
   - 按钮文本："登录"
   - 位置：密码下方

6. **添加切换注册按钮**
   - LoginPanel下创建 Button - TextMeshPro
   - 命名为 "SwitchToRegisterButton"
   - 按钮文本："还没有账号？注册"
   - 位置：登录按钮下方

7. **添加消息文本**
   - LoginPanel下创建 Text - TextMeshPro
   - 命名为 "MessageText"
   - 初始文本：空
   - 位置：底部

---

### 第3步：创建注册面板

复制登录面板，修改为注册面板：

1. **复制LoginPanel**
   - 右键LoginPanel → Duplicate
   - 重命名为 "RegisterPanel"
   - 初始设置为隐藏（取消勾选）

2. **修改标题**
   - 文本改为："用户注册"

3. **添加邮箱输入框**
   - 在密码输入框下方添加新的InputField
   - 命名为 "EmailInput"
   - Placeholder文本："请输入邮箱"
   - Content Type: Email Address

4. **修改按钮文本**
   - 登录按钮 → "注册"
   - 切换按钮 → "已有账号？登录"

5. **添加消息文本**
   - 同登录面板

---

### 第4步：创建加载面板

1. **创建LoadingPanel**
   - Canvas下创建 Panel
   - 命名为 "LoadingPanel"
   - 设置为全屏覆盖
   - 背景：半透明黑色（RGBA: 0,0,0,150）
   - 初始隐藏

2. **添加加载文本**
   - LoadingPanel下创建 Text - TextMeshPro
   - 命名为 "LoadingText"
   - 文本："加载中..."
   - 居中显示
   - 字体大小：20

3. **（可选）添加旋转图标**
   - 可以添加一个Image作为loading图标
   - 添加旋转动画

---

### 第5步：挂载脚本

1. **创建空GameObject**
   - Hierarchy → 右键 → Create Empty
   - 命名为 "LoginManager"

2. **挂载LoginUIManager脚本**
   - 选中LoginManager
   - Inspector → Add Component
   - 搜索并添加 "LoginUIManager"

3. **绑定UI引用**
   
   **面板引用：**
   - Login Panel → 拖入LoginPanel
   - Register Panel → 拖入RegisterPanel
   
   **登录面板UI：**
   - Login Username Input → UsernameInput的TMP_InputField组件
   - Login Password Input → PasswordInput的TMP_InputField组件
   - Login Button → LoginButton的Button组件
   - Switch To Register Button → SwitchToRegisterButton的Button组件
   - Login Message Text → MessageText的TextMeshProUGUI组件
   
   **注册面板UI：**
   - Register Username Input → 注册面板的用户名输入框
   - Register Password Input → 注册面板的密码输入框
   - Register Email Input → 注册面板的邮箱输入框
   - Register Button → 注册按钮
   - Switch To Login Button → 切换到登录按钮
   - Register Message Text → 注册面板的消息文本
   
   **加载提示：**
   - Loading Panel → LoadingPanel
   - Loading Text → LoadingText的TextMeshProUGUI组件

---

### 第6步：配置NetworkConfig

1. **NetworkConfig会自动创建**
   - 运行游戏时会自动创建NetworkConfig GameObject
   - 或者手动创建：
     - 创建空GameObject
     - 命名为 "NetworkConfig"
     - 挂载 NetworkConfig 脚本

2. **配置服务器地址**
   ```
   Base URL: http://localhost:3000/api  （开发时用Mock）
   Use Mock Server: ✓ （勾选，启用Mock模式）
   Timeout: 10
   ```

---

### 第7步：测试

1. **运行游戏**
   - 点击Play按钮

2. **测试登录**
   - 输入任意用户名（如：testuser）
   - 输入任意密码（如：123456）
   - 点击登录
   - 应该显示"登录成功"

3. **测试注册**
   - 点击"还没有账号？注册"
   - 输入用户名、密码、邮箱
   - 点击注册
   - 应该显示"注册成功"

4. **查看控制台**
   - 应该看到类似日志：
     ```
     [AuthService] 尝试登录: testuser
     [AuthService] 登录成功
     ```

---

## 🎨 UI美化建议

### 配色方案
- **主色调**：蓝色 (#4A90E2)
- **背景色**：深色渐变 (#1a1a2e → #16213e)
- **输入框**：白色半透明 (RGBA: 255,255,255,50)
- **按钮**：渐变色 (#4A90E2 → #357ABD)
- **成功消息**：绿色 (#4CAF50)
- **失败消息**：红色 (#F44336)

### 样式设置
- **圆角**：所有元素使用圆角（Radius: 10）
- **阴影**：Panel添加阴影效果
- **动画**：按钮悬停放大效果
- **过渡**：面板切换使用淡入淡出

### 布局优化
- 使用 Vertical Layout Group 自动排列
- 添加 Content Size Fitter 自适应
- 保持合理的间距（Spacing: 10-15）

---

## ⚠️ 常见问题

### Q: 找不到TextMeshPro组件？
A: Window → TextMeshPro → Import TMP Essentials

### Q: UI显示不正常？
A: 检查Canvas的Render Mode是否为Screen Space - Overlay

### Q: 按钮点击没反应？
A: 确保EventSystem存在（创建Canvas时会自动创建）

### Q: Mock模式不工作？
A: 检查NetworkConfig的Use Mock Server是否勾选

---

## 📸 预期效果

```
┌──────────────────────────────────┐
│                                  │
│      ┌─────────────────┐        │
│      │   用户登录       │        │
│      ├─────────────────┤        │
│      │ 用户名: [_____] │        │
│      │ 密  码: [_____] │        │
│      │                 │        │
│      │  [  登  录  ]   │        │
│      │                 │        │
│      │还没有账号？[注册]│        │
│      └─────────────────┘        │
│                                  │
└──────────────────────────────────┘
```

完成以上步骤后，你就有了一个完整的登录界面！🎉
