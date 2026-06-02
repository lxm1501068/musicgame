# 谱面右键重命名功能

## 🎯 功能说明
在 CreateTableScene 中，**右键点击**谱面条目即可重命名。

## 📝 使用步骤
1. 进入谱面列表（CreateTableScene）
2. **右键点击**任意谱面
3. 输入新名称 → 点击确认
4. 列表自动刷新

## ⚙️ Unity配置
为 `CreateTableManager` 绑定：
- `renameDialogPanel` - 对话框面板
- `renameInputField` - 输入框
- `renameConfirmBtn` - 确认按钮
- `renameCancelBtn` - 取消按钮

## 🔒 安全特性
- ✅ 自动检测非法字符
- ✅ 防止文件名冲突
- ✅ 自动添加 .txt 扩展名
- ✅ 完善的错误处理

## 💡 快捷键
- **Enter** - 确认
- **Esc** - 取消

## 📁 相关文件
- `CreateTableManager.cs` - 核心实现
- 新增方法：`ShowRenameDialog()`, `RenameChartFile()`
