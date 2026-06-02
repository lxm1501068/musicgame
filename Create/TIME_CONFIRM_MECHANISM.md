# 时间预览确认机制

## 功能说明

为了避免在修改时间预览时出现循环调用和性能问题，实现了"确认按钮"机制。

## 工作原理

### 1. 时间修改流程

当用户通过以下方式修改时间时：
- 修改小节输入框（measureInputField）
- 拖动小节滑块（measureSlider）
- 修改节拍输入框（beatInputField）
- 拖动节拍滑块（beatSlider）

系统会：
1. 计算新的时间值并保存到 `pendingTime`
2. 设置 `isTimeChanged = true` 标记
3. **显示**"确认"按钮（timeConfirmBtn）
4. 在信息文本中提示用户点击确认按钮

**此时不会立即更新音符和按键的位置**。

### 2. 确认更新流程

当用户点击"确认"按钮时：
1. 检查 `isTimeChanged` 标志
2. 调用 `UpdateObjectsPositionAtTime(pendingTime)` 更新所有对象位置
3. 更新 `currentDisplayTime` 记录当前实际显示的时间
4. 重置状态（`isTimeChanged = false`, `pendingTime = 0f`）
5. **检查UI时间与显示时间是否一致**：
   - 计算当前UI（小节+节拍）对应的时间
   - 与 `currentDisplayTime` 比较
   - 如果一致（误差 < 0.001s），隐藏确认按钮
   - 如果不一致，保持按钮显示
6. 显示成功信息

## 优势

### 1. 避免循环调用
- 使用 `onEndEdit` 而非 `onValueChanged` 监听输入框
- 输入框的值变化不会触发多次更新
- 程序修改输入框值不会再次触发事件

### 2. 提升性能
- 减少不必要的计算和位置更新
- 用户可以连续调整多个参数后一次性确认
- 避免频繁的 Debug.Log 输出

### 3. 更好的用户体验
- 用户可以预览将要应用的时间值
- 确认前可以取消或修改
- 明确的操作反馈

## 实现细节

### 新增变量
```csharp
private bool isTimeChanged = false;      // 标记时间是否已更改但未确认
private float pendingTime = 0f;          // 待确认的时间值
private float currentDisplayTime = 0f;   // 当前实际显示的时间
```

### 新增 UI 组件
```csharp
public Button timeConfirmBtn;  // 时间预览确认按钮
```

### 关键方法

#### OnTimeConfirmClicked()
确认按钮的点击处理函数，执行实际的位置更新。

#### CheckAndHideConfirmBtn()
检查UI时间与实际显示时间是否一致，如果一致则隐藏确认按钮。

#### UpdateObjectsPositionAtTime(float time)
在更新位置后，记录 `currentDisplayTime = time`。

#### 修改的回调函数
- `OnMeasureChanged(string value)` - 小节输入框变化
- `OnMeasureSliderChanged(float value)` - 小节滑块变化
- `OnBeatInputChanged(string value)` - 节拍输入框变化
- `OnBeatSliderChanged(float value)` - 节拍滑块变化

这些函数现在只计算待确认的时间并显示确认按钮，不直接更新位置。

#### Deselect()
取消选择时重置时间状态并隐藏确认按钮。

## 使用场景

### 场景 1：精确调整时间
1. 用户在输入框中输入精确的小节和节拍值
2. 系统计算时间但不立即更新
3. 用户确认无误后点击确认按钮
4. 位置更新

### 场景 2：快速浏览
1. 用户拖动滑块快速浏览不同时间点
2. 每次拖动只显示提示信息
3. 找到目标位置后点击确认
4. 避免拖动过程中的频繁更新

### 场景 3：批量调整
1. 用户先调整小节
2. 再调整节拍
3. 最后点击一次确认
4. 只进行一次位置更新计算

## 注意事项

1. **确认按钮的显示逻辑**
   - 时间更改后：**始终显示**
   - 确认后：检查UI时间与显示时间是否一致
     - 一致：隐藏按钮
     - 不一致：保持显示（用户可能又修改了UI）
   - 取消选择后：隐藏

2. **信息文本提示**
   - 时间更改后显示：“请点击确认按钮更新预览”
   - 确认后显示：“已更新预览”

3. **与选中对象的关系**
   - 无论是否选中对象，时间预览都可用
   - 取消选择时会重置时间状态

4. **浮点数比较**
   - 使用误差范围 0.001s 进行比较
   - 避免浮点数精度问题导致的误判
