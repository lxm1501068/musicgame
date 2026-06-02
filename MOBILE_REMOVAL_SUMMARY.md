# 移除移动端适配代码

## 📝 修改概述
已删除所有 Android/iOS 适配代码，项目现仅支持 PC 平台。

## 🔧 修改文件

### 1. CreateTableManager.cs
- **位置**：`ParseChartInfo()` 方法
- **删除**：27行移动端 UnityWebRequest 代码
- **保留**：PC端 `File.ReadAllText()`

### 2. LoadChart.cs
- **位置**：`LoadChartFileAsync()` 方法
- **删除**：29行移动端异步加载代码
- **保留**：PC端 `File.ReadAllTextAsync()`

**总计删除**：56行代码

## ✅ 优势
- 代码更简洁（减少56行）
- 性能更好（直接文件I/O）
- 依赖更少（移除UnityWebRequest）
- 更易维护（单一平台逻辑）

## 🔍 测试清单
- [ ] 谱面列表正常显示
- [ ] 谱面能正常加载和播放
- [ ] Move指令JSON文件正常读取
- [ ] 谱面导出功能正常

## 📌 保留的跨平台代码
以下代码仍保留（对PC也必要）：
- `StartUIManager.cs` - 编辑器退出功能
- `SkinAssetCreator.cs` - 编辑器工具
