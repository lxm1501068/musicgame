# Create Scene 时间控制系统更新说明

## 概述

已将原有的 `timeSlider` 和 `timeInputField` 替换为双滑块+双输入框系统：`measureSlider`/`measureInputField`（小节控制）和 `beatSlider`/`beatInputField`（节拍控制）。节拍控制会根据当前小节的拍号分母自动吸附到合适的精度（如 1/16 拍或 1/12 拍）。

## 主要改动

### 1. UI 组件变化

#### 删除的组件
- **`timeInputField`** (TMP_InputField): 原有的时间显示框已删除
- **`timeSlider`** (Slider): 原有的节拍位置滑块已删除

#### 新增组件
- **`measureSlider`** (Slider): 小节序数滑块
  - 用于快速切换当前查看的小节
  - 范围：0 到 (总小节数 - 1)
  - 与 `measureInputField` 同步更新

- **`beatInputField`** (TMP_InputField): 节拍位置输入框
  - 用于手动输入小节内的具体节拍位置
  - 支持小数（如 0.5, 1.25, 2.75）
  - 与 `beatSlider` 双向同步
  - 自动限制在当前小节的拍数范围内
  - 支持吸附功能

- **`beatSlider`** (Slider): 节拍位置滑块
  - 用于滑动选择小节内的具体节拍位置
  - 范围：0 到当前小节的总拍数（根据拍号动态调整）
  - 与 `beatInputField` 双向同步
  - 支持吸附功能（整拍/半拍/关闭）

#### 保留的组件
- **`measureInputField`** (TMP_InputField): 小节序数输入框
  - 可以手动输入小节序号
  - 与 `measureSlider` 双向同步

#### 自动吸附机制
- **无需手动控制**：`beatSlider` 和 `beatInputField` 始终启用自动吸附
- **智能双精度吸附**：同时支持两种吸附精度，自动选择最接近的
  - **1/4 拍**（0.25）：标准四分音符及其细分
    - 示例值：0, 0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2...
  - **1/3 拍**（≈0.333）：三连音节奏
    - 示例值：0, 0.33, 0.67, 1, 1.33, 1.67, 2, 2.33...
- **自适应选择**：系统会自动计算距离，选择最近的吸附点

### 2. 工作流程

#### 浏览谱面时的操作步骤：
1. **选择小节**：通过以下任一方式选择要查看的小节
   - 在 `measureInputField` 中直接输入小节号
   - 拖动 `measureSlider` 滑动到目标小节
2. **调整节拍位置**：通过以下任一方式选择小节内的具体节拍位置
   - 在 `beatInputField` 中直接输入节拍位置
   - 拖动 `beatSlider` 滑动到目标节拍位置
   - 系统会自动吸附到最近的 1/4 拍或 1/3 拍
   - 滑块范围自动适配当前小节的拍号（如 4/4 拍则为 0-4）
3. **自动跳转**：系统会自动计算实际时间，并将所有音符和按键更新到对应位置
4. **信息提示**：InfoText 会显示：`小节 X | 节拍 Y/Z | 时间: T.TTs`
   - X = 当前小节序数
   - Y = 当前节拍位置
   - Z = 该小节总拍数
   - T.TT = 计算后的实际时间（秒）

#### 放置音符时：
- 音符会被放置在当前选中的**小节 + 节拍位置**对应的实际时间
- 可以通过调整小节和节拍来精确定位时间点
- 系统会自动将节拍位置吸附到最近的 1/4 拍或 1/3 拍

### 3. 核心功能

#### 双滑块+双输入框时间控制
- **小节控制**：
  - `measureInputField`：手动输入小节序号
  - `measureSlider`：滑动选择小节
  - 两者双向同步
  
- **节拍控制**：
  - `beatInputField`：手动输入节拍位置（支持小数）
  - `beatSlider`：滑动选择节拍位置
  - 两者双向同步
  - 自动限制在当前小节拍数范围内
  - **双精度自动吸附**：同时支持 1/4 拍和 1/3 拍，自动选择最近
  
- **切换小节时**：节拍位置自动重置为 0

#### 智能双精度吸附机制
- **无需手动开关**：吸附功能始终启用
- **双精度支持**：同时支持 1/4 拍和 1/3 拍（三连音）
  - **1/4 拍精度**（0.25 间隔）：
    - 适用：标准节奏、流行音乐、摇滚等
    - 示例值：0, 0.25, 0.5, 0.75, 1, 1.25, 1.5...
  - **1/3 拍精度**（≈0.333 间隔）：
    - 适用：三连音节奏、摇摆节奏、爵士乐等
    - 示例值：0, 0.33, 0.67, 1, 1.33, 1.67, 2...
- **智能选择**：系统会计算距离，自动选择最近的吸附点
  - 例如：输入 0.3 → 距离 0.25 更近 → 吸附到 0.25
  - 例如：输入 0.35 → 距离 0.333 更近 → 吸附到 0.333

#### 双向同步
- **小节控制**：
  - 修改 `measureInputField` → 自动更新 `measureSlider`
  - 拖动 `measureSlider` → 自动更新 `measureInputField`
  - 切换小节时 → `beatInputField` 和 `beatSlider` 重置为 0
  
- **节拍控制**：
  - 修改 `beatInputField` → 自动更新 `beatSlider`
  - 拖动 `beatSlider` → 自动更新 `beatInputField`
  - 切换小节时 → `beatSlider` 范围自动调整为新小节的拍数

#### 实时时间计算
- 系统根据小节序数和节拍位置自动计算实际时间（秒）
- 计算公式：`时间 = 前面所有小节的总时长 + (节拍位置 * 60 / BPM)`
- 实时更新所有对象的显示位置

### 4. 代码改动详情

#### CreateSceneManager.cs
- 删除字段：`timeInputField`, `timeSlider`
- 新增字段：`measureSlider`, `beatInputField`, `beatSlider`
- 删除方法监听：`OnTimeChanged`, `OnTimeSliderChanged`
- 新增方法监听：`OnMeasureSliderChanged`, `OnBeatInputChanged`, `OnBeatSliderChanged`
- 更新 `InitializeTimeDisplay()` 方法
- 更新 `RefreshDisplayAtTime()` 方法
- 新增方法：
  - `InitializeBeatSlider()`: 初始化节拍滑块和输入框
  - `UpdateBeatSliderRange()`: 更新节拍滑块范围

#### CreateSceneManager.Editor.cs
- 删除方法：
  - `UpdateSliderRangeForCurrentMeasure()`
  - `CalculateAndDisplayTime()`
  - `OnTimeChanged()`
  - `OnTimeSliderChanged()`
- 新增方法：
  - `OnBeatInputChanged()`: 处理节拍输入框变化（包含范围限制和吸附逻辑）
  - `OnBeatSliderChanged()`: 处理节拍滑块变化（包含吸附逻辑）
  - `CalculateTimeFromMeasureAndBeat()`: 根据小节和节拍计算时间
  - `CalculateCurrentTime()`: 计算当前时间
  - `GetMeasureData()`: 获取指定小节的数据
- 更新方法：
  - `OnMeasureChanged()`: 添加节拍控件范围更新和重置
  - `OnMeasureSliderChanged()`: 添加节拍控件范围更新和重置
  - `Deselect()`: 更新 UI 显示逻辑
  - `UpdateInfoPanelForNote()`: 隐藏小节控件
  - `UpdateInfoPanelForKey()`: 隐藏小节控件

#### CreateSceneManager.Placement.cs
- 更新 `PlaceNote()` 方法：使用小节序数和节拍位置计算时间，支持吸附功能

#### ChartData.cs
- 新增方法：`GetMeasureIndexAtTime()`: 根据时间获取对应的小节索引

### 5. Unity Editor 设置

在 Unity Editor 中需要进行以下操作：

1. **移除旧组件引用**：
   - 在 CreateSceneManager 组件中，清除 `Time Input Field` 和 `Time Slider` 的引用

2. **添加新组件引用**：
   - 将场景中的第一个 Slider 组件拖拽到 `Measure Slider` 字段
   - 将场景中的第二个 Slider 组件拖拽到 `Beat Slider` 字段
   - 将场景中的 TMP Input Field 组件拖拽到 `Beat Input Field` 字段
   - 配置组件的属性：
     - **Measure Slider**:
       - Min Value: 0
       - Max Value: 根据谱面小节数动态设置
       - Whole Numbers: true（建议设置为整数）
     - **Beat Input Field**:
       - Content Type: Decimal Number（允许小数）
       - Placeholder: "0.00"
     - **Beat Slider**:
       - Min Value: 0
       - Max Value: 根据当前小节拍数动态设置
       - Whole Numbers: false（允许小数，支持半拍）

3. **UI 布局调整**：
   - 建议的 UI 布局顺序：
     1. `measureInputField` (小节输入框)
     2. `measureSlider` (小节滑块)
     3. `beatInputField` (节拍输入框)
     4. `beatSlider` (节拍滑块)
   - 将相关控件放在一起，方便用户理解它们的关联
   - 建议在输入框和滑块之间添加标签说明

### 6. 优势

相比原有系统，新系统的优势：
- **更精确**：双滑块+双输入框系统可以精确控制到小节内的具体节拍位置
- **更智能**：自动根据拍号分母调整吸附精度，无需手动设置
- **更简洁**：去除了多余的吸附控制按钮，界面更清爽
- **更灵活**：支持输入框和滑块两种方式，适应不同操作习惯
- **更直观**：小节和节拍分开控制，符合音乐编辑习惯
- **更易用**：所有控件双向同步，操作更灵活
- **更高效**：快速跳转到任意时间点，无需手动计算时间

### 7. 智能双精度吸附机制详解

#### 工作原理
系统会同时计算吸附到 1/4 拍和 1/3 拍的位置，然后选择距离原始位置更近的那个：

```csharp
private float SnapBeatPosition(float beatPosition)
{
    // 计算吸附到 1/4 拍的位置
    float snapToQuarter = Mathf.Round(beatPosition / 0.25f) * 0.25f;
    
    // 计算吸附到 1/3 拍的位置（三连音）
    float snapToThird = Mathf.Round(beatPosition / (1f/3f)) * (1f/3f);
    
    // 选择距离原始位置更近的吸附点
    float distToQuarter = Mathf.Abs(beatPosition - snapToQuarter);
    float distToThird = Mathf.Abs(beatPosition - snapToThird);
    
    return distToQuarter <= distToThird ? snapToQuarter : snapToThird;
}
```

#### 吸附触发时机
1. 在 `beatInputField` 中输入数值并按下回车或失去焦点时
2. 拖动 `beatSlider` 时实时吸附
3. 放置音符时自动吸附
4. 手动输入小节时不触发吸附（只重置节拍为 0）

#### 实际应用示例

**示例 1：标准节奏（靠近 1/4 拍）**
- 用户拖动滑块到：0.28
- 距离 1/4 拍 (0.25)：|0.28 - 0.25| = 0.03
- 距离 1/3 拍 (0.333)：|0.28 - 0.333| = 0.053
- 结果：吸附到 **0.25**（更接近 1/4 拍）

**示例 2：三连音节奏（靠近 1/3 拍）**
- 用户拖动滑块到：0.35
- 距离 1/4 拍 (0.25)：|0.35 - 0.25| = 0.10
- 距离 1/3 拍 (0.333)：|0.35 - 0.333| = 0.017
- 结果：吸附到 **0.333**（更接近 1/3 拍）

**示例 3：正中间的情况**
- 用户输入：0.2915（恰好在 0.25 和 0.333 中间）
- 距离相等，优先选择 1/4 拍
- 结果：吸附到 **0.25**

**示例 4：不同拍号下的表现**
- **4/4 拍**：可以精确放置标准节奏和三连音
- **6/8 拍**：同样支持，不受拍号限制
- **12/8 拍**：完美支持三连音节奏
- **任何拍号**：吸附逻辑保持一致

### 8. 注意事项

- `measureSlider` 的最大值会在加载谱面后根据实际小节数动态设置
- `beatSlider` 的最大值会在切换小节时根据该小节的拍号动态调整
- `beatInputField` 输入的值会自动限制在 0 到当前小节拍数之间
- 吸附功能始终启用，无法关闭
- 吸附精度固定为 1/4 拍和 1/3 拍，与当前小节的拍号无关
- 系统会自动选择距离最近的吸附点（1/4 拍或 1/3 拍）
- 切换小节时，节拍位置会自动重置为 0
- 这种设计使得在任何拍号下都能精确放置标准节奏和三连音
- 如需修改吸附逻辑，可以编辑 `SnapBeatPosition()` 方法
