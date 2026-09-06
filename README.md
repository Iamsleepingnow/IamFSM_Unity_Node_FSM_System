# IamFSM 状态机系统

基于 **NodeGraphProcessor** 的可视化`有限状态机（Finite State Machine）`节点编辑器与运行时执行引擎。在 Unity 中通过拖拽连线搭建状态逻辑，并与代码结合，最终完成对象行为、流程编排与逻辑控制。

此项目的制作使用到AI辅助，不完全由个人完成。做此项目的主要是**为了好玩**，做起来很爽，能适配一些比较简单的状态机逻辑。但对于大项目而言可能就会有些捉襟见肘。

**License: MIT**：由于NodeGraphProcessor协议是MIT，所以这里也是MIT。

---

下面是AI生成的介绍（人工逐字校对版）↓↓↓

## 整体介绍

IamFSM 由两部分组成：

- **编辑器**：可视化节点画布，右键/拖线创建节点，`Executes` 控制流端口与数据端口分离，支持分组、便签、复制粘贴、撤销重做。
- **运行时**：节点表 SO（ScriptableObject） 转json DTO，`FsmEngine` 逐帧执行控制流，维护状态切换、条件监听与进度阻碍；`FsmObjectBase` 组件挂载到游戏对象即可驱动。

核心特点：

- 可视化搭建状态机与流程，热编辑、可调试。
- 数据源灵活：节点表 SO、导出 JSON（StreamingAssets / AppData / 自定义路径）、Addressables。
- 内置输入（鼠标 / 键盘 / 新输入系统）、物理（碰撞 / 触发 / 射线 / 重叠检测）、调试输出等节点。
- 控制流支持并行支线、分支排序、延时等待与状态切换中断。

## 环境要求

- 测试过的版本：2022.3.62f3c1（团结1.10.2），6000.3.23f1。
- 通过 Package Manager 安装以下官方包：
  | 包                                      | 用途                  |
  | -------------------------------------- | ------------------- |
  | `com.unity.render-pipelines.universal` | URP 渲染管线（仅Demo场景需要） |
  | `com.unity.nuget.newtonsoft-json`      | JSON 序列化（图数据与导出）    |
  | `com.unity.addressables`               | Addressables 数据源    |
  | `com.unity.inputsystem`                | 新输入系统（监听节点）         |

## 用途开发

- **对象行为**：简单游戏 AI、动画状态切换等。
- **逻辑控制**：按键绑定、技能 / 道具 / 事件处理。
- **可集成**：将状态机作为无代码逻辑工具挂载到任意脚本驱动的对象上。

适合"状态驱动 + 事件 / 条件"的场景；不兼容行为树。

## 使用方法

### 1. 创建节点表

菜单 `Assets/Create/Node Graph 节点画布/New Basic Graph` 创建节点表 SO。双击文件，或在其 Inspector 面板顶部点击 **打开节点图窗口 Open Graph** 进入画布。

### 2. 搭建最小状态机

右键画布创建节点并按 `Executes` 端口连线：

```
入口状态 Entry State → 切换状态 Go to State(Idle)
进入状态 In State(Idle) → 条件满足 Performed(鼠标左键) → 日志输出 Debug Log("Hello") → 切换状态 Go to State(Idle)
```

`EntryState` 是入口，`GoToState` 指定进入的状态，`InState` 描述某状态的入口挂起点，其后挂条件/等待节点来决定该状态下做什么。

![](.\_PICS_\Pic001.png)


### 3. 运行

把 **Fsm Object（FsmObjectBase）** 组件挂到任意游戏对象，指定数据源：

- JSON 数据文件（StreamingAssets / AppData / 自定义路径，可在 Inspector 一键导出）；
- Addressables 引用。

点击组件的 **播放** 按钮，或在 `Play` 模式下自动运行，也兼容通过代码控制运行。

![](.\_PICS_\Pic002.png)


### 4. 导出与调试

- Inspector 顶部 **导出 JSON**，将当前节点表转换为运行时 JSON 文件。
- 播放期间可在 Inspector 的"调试可视化"分区查看当前状态与待监听条件。

### 暴露参数

节点表支持暴露参数：在图形中把某个数据节点暴露为参数，运行时拖动/在组件 Inspector 上直接调整，作为跨节点共享的输入。

## 节点一览

### 状态 States

状态节点是这个系统的底层逻辑，所有的逻辑驱动都需要依据状态节点来运行。

| 节点                     | 说明                                                     |
| ------------------------ | -------------------------------------------------------- |
| NEntryState 入口状态     | 状态机唯一入口                                           |
| NGoToState 切换状态      | 切换到指定状态                                           |
| NInState 进入状态        | 描述某状态的入口挂起点                                   |
| NInAnyState 进入任意状态 | 每次状态切换都会执行的全局入口，仅当切换状态时会触发一遍 |

### 流程控制 Flow Control

| 节点                | 说明                                                   |
| ------------------- | ------------------------------------------------------ |
| NBranch 分支        | 多路输出分支，用于对数据管线进行分流                   |
| NIfElse 条件判断    | 按布尔条件走 true / false 分支，用于对执行管线进行分流 |
| NNodeOrder 分支排序 | 用于控制多条平行管线的执行顺序，数值越低越先执行       |
| Relay 节点接续      | 通用中继节点                                           |

### 条件 Conditions

| 节点                                               | 说明                                                         |
| -------------------------------------------------- | ------------------------------------------------------------ |
| NPerformed 条件满足                                | 条件成立时放行一次，条件`Condition`允许多连，`Is And`用于控制多连的条件是否是`与`逻辑，否则使用`或`逻辑进行判断。（阻塞监听） |
| NCompareNumber 数字比较                            | 数字大小比较                                                 |
| NIsEqualTo 等于                                    | 值是否相等判断                                               |
| NListenInput / NListenInputMouse / NListenInputKey | 输入 / 鼠标 / 按键监听                                       |
| NCollisionDetect / NCollisionProxyDetect           | 碰撞检测 / 远程碰撞代理                                      |
| NTriggerDetect / NTriggerProxyDetect               | 触发检测 / 远程触发代理                                      |
| NRayDetect 射线检测                                | 射线检测                                                     |
| NOverlapSphereDetect / NOverlapBoxDetect           | 球形 / 箱形重叠检测                                          |

### 延时 Delays

延时节点在计时的时候，其执行链会被阻塞，直到计时完成。

当`Is Break`为真时可以打破计时。`IsBackground`可以使该节点线路无视状态切换而运行，也就是说，当使用`GoToState`切换状态时，此线路仍然能运行。

| 节点                | 说明        |
| ----------------- | --------- |
| NWaitSeconds 等待秒数 | 等待指定秒数后继续 |
| NWaitFrames 等待帧数  | 等待指定帧数后继续 |

### 事件与调试

| 节点                  | 说明                                                         |
| --------------------- | ------------------------------------------------------------ |
| NEventInvoke 事件触发 | 触发 Unity 事件。事件节点可以触发`Fsm播放器组件`的`fsmEvent`事件，`Message`是用于传参的字符串，用于区分事件，多数情况下被用作事件名 |
| NDebugLog 日志输出    | 输出文本到`Console`控制台                                    |

### 基本类型 Primitives / 信息 Info

| 节点                          | 说明                               |
| ----------------------------- | ---------------------------------- |
| NFloat 浮点数                 | 浮点数值数据                       |
| NInteger 整数                 | 整数值数据                         |
| NString 字符串                | 字符串文本数据                     |
| NColor 颜色                   | 颜色数据（RGBA）                   |
| NVector 向量                  | 向量数据（仅四维向量）             |
| NGameTime 游戏时间            | 获取当前游戏时间                   |
| NCurrentState 当前状态        | 读取当前状态机状态                 |
| NLocalTransform 本地方位变换  | 本地方位（位置 / 旋转 / 缩放）数据 |
| NLocalGameObject 本地游戏对象 | 引用本地游戏对象                   |

### 转换 Converter

| 节点                             | 说明                   |
| -------------------------------- | ---------------------- |
| NConvertToNumber 转数字          | 把任意输入转换为数字   |
| NConvertToBool 转布尔            | 把任意输入转换为布尔   |
| NConvertToString 转字符串        | 把任意输入转换为字符串 |
| NConvertToVector 转向量          | 把任意输入转换为向量   |
| NConvertToColor 转颜色           | 把任意输入转换为颜色   |
| NConvertColorToVector 颜色转向量 | 颜色 → 向量            |
| NConvertVectorToColor 向量转颜色 | 向量 → 颜色            |

### 数学 Math

#### 浮点

| 节点                   | 说明             |
| -------------------- | -------------- |
| NSingleMath 单值计算     | 单值数学运算         |
| NMultiMath 多值计算      | 多值数学运算         |
| NClampFloat 钳制       | 将数值限制在指定区间     |
| NLerpFloat 插值        | 浮点线性插值         |
| NMapFloat 映射         | 数值从一个区间映射到另一区间 |
| NRandomFloat 随机      | 生成随机浮点数        |
| NNoiseFloat 噪声       | 数值噪声采样         |
| NTrigonometry 三角函数   | 三角函数运算         |
| NRadianToDegree 弧度角度 | 弧度与角度互转        |

#### 向量

| 节点               | 说明         |
| ---------------- | ---------- |
| NVectorMath 向量计算 | 向量数学运算     |
| NClampVector 钳制  | 向量各分量钳制到区间 |
| NLerpVector 插值   | 向量线性插值     |
| NMapVector 映射    | 向量分量区间映射   |
| NRandomVector 随机 | 生成随机向量     |
| NNoiseVector 噪声  | 向量噪声采样     |

#### 通用

| 节点                 | 说明              |
| ------------------ | --------------- |
| NMathConstant 数学常量 | 常用数学常量（π / e 等） |

### 逻辑与实用

#### 逻辑

| 节点              | 说明     |
| ----------------- | -------- |
| NLogicAnd 与      | 逻辑与   |
| NLogicOr 或       | 逻辑或   |
| NLogicXor 异或    | 逻辑异或 |
| NLogicNegate 取反 | 逻辑非   |

#### 实用

| 节点                   | 说明          |
| -------------------- | ----------- |
| NStringConcat 字符串拼接  | 拼接多个字符串     |
| NStringSplit 字符串分割   | 按分隔符拆分字符串   |
| NRegularEx 正则        | 正则表达式处理     |
| NRegularExMatch 正则匹配 | 判断字符串是否匹配正则 |
| NToJson              | 序列化为 JSON   |
| NFromJson            | 从 JSON 反序列化 |
| NVectorCombine 向量合并  | 由多个分量合并成向量  |
| NVectorSplit 向量分裂    | 拆分向量的各分量    |
| NColorFlip 颜色翻转      | 翻转颜色通道      |
| NColorWrap 颜色通道置换    | 置换颜色通道      |

## 署名与协议

本项目基于 MIT License 开源。
