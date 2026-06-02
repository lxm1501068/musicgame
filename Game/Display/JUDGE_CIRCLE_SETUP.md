# 判定结果圆形显示系统 - 使用说明

## 功能概述

该系统会在音符判定时，在按键位置显示一个彩色圆形，表示判定结果：
- **Perfect（金色）**：RGB(255, 214, 0)
- **Good（绿色）**：RGB(0, 255, 0)
- **Bad（红色）**：RGB(255, 0, 0)

圆形会在 0.2 秒内逐渐变透明并消失。对于 Hold 音符，圆形会持续到 Hold 判定结束后才消失。

## 文件说明

### 新增文件
1. **JudgeCircle.cs** - 判定圆形组件，负责显示和淡出动画
2. **JudgeCircleManager.cs** - 判定圆形管理器（单例），使用对象池管理圆形实例

### 修改文件
1. **BaseNote.cs** - 添加了 `ShowJudgeCircle()` 和 `IsHoldNote()` 方法
2. **Hold.cs** - 重写了 `IsHoldNote()` 并在结束时隐藏圆形

## Unity 编辑器设置步骤

### 1. 创建 JudgeCircleManager

在游戏场景中：
1. 创建一个空 GameObject，命名为 "JudgeCircleManager"
2. 添加 `JudgeCircleManager` 组件
3. （可选）配置以下参数：
   - **Pool Size**: 对象池大小（默认 10，根据同时出现的音符数量调整）
   - **Judge Circle Prefab**: 可以拖入自定义的圆形预制体（可选）

### 2. 配置 JudgeCircle（可选）

如果你想自定义圆形的外观：

#### 方法 A：使用默认圆形（推荐）
不需要额外操作，系统会自动创建简单的白色圆形纹理。

#### 方法 B：使用自定义圆形预制体
1. 创建一个 Sprite（可以使用 Unity 内置的圆形精灵或导入自己的图片）
2. 创建一个空 GameObject，添加 `SpriteRenderer` 和 `JudgeCircle` 组件
3. 将 Sprite 赋值给 SpriteRenderer
4. 调整 `JudgeCircle` 组件的参数：
   - **Perfect Color**: Perfect 判定颜色（默认金色）
   - **Good Color**: Good 判定颜色（默认绿色）
   - **Bad Color**: Bad 判定颜色（默认红色）
   - **Fade Duration**: 淡出时间（默认 0.2 秒）
   - **Initial Scale**: 初始大小（默认 1.5）
5. 将此 GameObject 保存为预制体
6. 将预制体拖到 `JudgeCircleManager` 的 "Judge Circle Prefab" 字段

### 3. 确保 GameManager 场景中存在

确认你的游戏场景中已经有：
- GameManager（管理游戏状态）
- InputManager（处理输入）
- 各种 Display 组件（ScoreDisplay、ComboDisplay、JudgeResultDisplay）

`JudgeCircleManager` 应该与这些组件在同一个场景中。

## 工作原理

### 普通音符（Tap、Flick、Drag 等）
1. 音符被判定后，调用 `BaseNote.UpdateGlobalUI()`
2. `ShowJudgeCircle()` 获取按键位置并调用 `JudgeCircleManager.ShowJudgeCircle()`
3. 管理器从对象池取出一个圆形，显示在按键位置
4. 圆形开始 0.2 秒的淡出动画
5. 动画结束后，圆形返回对象池

### Hold 音符
1. Hold 开始时（首次判定成功），显示圆形
2. 圆形**不会**自动消失，而是保持在屏幕上
3. 当 Hold 结束（完成或 Miss）时，调用 `JudgeCircleManager.HideHoldJudgeCircle()`
4. 圆形立即隐藏并返回对象池

## 自定义配置

### 修改颜色
在 `JudgeCircle` 组件中调整：
```csharp
public Color perfectColor = new Color(1f, 0.84f, 0f, 1f); // 金色
public Color goodColor = new Color(0f, 1f, 0f, 1f);       // 绿色
public Color badColor = new Color(1f, 0f, 0f, 1f);        // 红色
```

### 修改淡出时间
```csharp
public float fadeDuration = 0.2f; // 改为其他值，如 0.3f
```

### 修改圆形大小
```csharp
public float initialScale = 1.5f; // 改为其他值，如 2.0f
```

## 注意事项

1. **Miss 判定不显示圆形**：根据设计，Miss 判定不会产生圆形效果
2. **对象池自动扩展**：如果同时需要的圆形超过池大小，系统会自动创建新的
3. **性能优化**：使用对象池避免频繁 Instantiate/Destroy
4. **Hold 音符特殊处理**：确保持续按住时圆形不会提前消失

## 故障排除

### 圆形不显示
- 检查 `JudgeCircleManager` 是否存在于场景中
- 确认 GameManager 正在运行（IsPlaying = true）
- 检查 Console 是否有错误信息

### 圆形颜色不对
- 检查 `JudgeCircle` 组件的颜色配置
- 如果使用预制体，确认预制体上的配置正确

### Hold 圆形提前消失
- 确认 Hold 音符正确重写了 `IsHoldNote()` 方法
- 检查 `HandleHoldMiss()` 是否正确调用了 UI 更新

## 扩展建议

如果需要更多功能，可以考虑：
1. 添加缩放动画（从小变大再变小）
2. 添加粒子效果增强视觉反馈
3. 支持更多判定等级（如 Great、Nice 等）
4. 添加音效配合视觉效果
