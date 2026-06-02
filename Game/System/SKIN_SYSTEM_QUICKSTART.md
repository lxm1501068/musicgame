# 皮肤系统 - 快速开始

## 🚀 3步上手

### 1️⃣ 创建皮肤
Unity编辑器右键 → `Create > Note Skin` → 配置精灵

### 2️⃣ 添加UI
Canvas + Dropdown + Button + Text → 挂载 `SkinSelectorUI`

### 3️⃣ 运行游戏
选择皮肤 → 点击应用 → 开始游戏

## 💡 核心API
```csharp
SkinManager.Instance.SwitchSkin("Neon"); // 切换皮肤
```

## ⚙️ 关键特性
- 自动加载 `Resources/Skins/` 下的所有皮肤
- PlayerPrefs 自动保存选择
- 预制体精灵优先级 > 皮肤精灵
- 支持6种音符类型

## 📁 文件清单
- `NoteSkin.cs` - 皮肤数据
- `SkinManager.cs` - 管理器
- `SkinSelectorUI.cs` - UI控制器
- `SkinAssetCreator.cs` - 编辑器工具

## ❗ 注意
- 皮肤放 `Assets/Resources/Skins/`
- 切换只影响新创建的音符
- 详细文档见 `SKIN_SYSTEM_README.md`
