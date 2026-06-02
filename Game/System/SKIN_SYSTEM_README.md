# 音符皮肤系统

## 快速开始

### 1. 创建皮肤
- Unity编辑器右键 → `Create > Note Skin`
- 在 Inspector 中配置各音符类型的精灵

### 2. 添加UI
- 创建 Dropdown + Button + Text
- 挂载 `SkinSelectorUI` 脚本并绑定引用

### 3. 使用API
```csharp
SkinManager.Instance.SwitchSkin("SkinName"); // 切换皮肤
```

## 核心特性
- ✅ 自动从 `Resources/Skins/` 加载皮肤
- ✅ PlayerPrefs 持久化保存选择
- ✅ 预制体精灵优先级高于皮肤
- ✅ 支持 Tap/Hold/Drag/Flick/Mtap/Key

## 文件结构
```
Game/System/
├── NoteSkin.cs         # 皮肤数据
├── SkinManager.cs      # 管理器（单例）
└── SkinAssetCreator.cs # 编辑器工具

Setting/
└── SkinSelectorUI.cs   # UI控制器
```

## 注意事项
- 皮肤资源必须放在 `Assets/Resources/Skins/`
- 切换皮肤只影响新创建的音符
- 预制体上手动设置的精灵优先使用
