# CLAUDE.md

本文件为 Claude Code（claude.ai/code）在此仓库中工作时提供指导。

## 项目概述

**Survivor of a Disaster（劫后余生）** — 后战争时代佣兵回收游戏，世界观设定于 WCC 2773 年。核心循环：交战区搜集物资 → MRAP 装甲车撤离 → 安全区出售换取 EC 货币 → 购买装备 → 重复循环。

- **引擎：** Unity 6.3 (6000.3)，URP
- **语言：** C# (.NET Standard 2.1)
- **输入：** Unity New Input System (1.19)
- **主场景：** `Scenes/Home.unity`

## 关键包

| 包 | 用途 |
|---|---|
| Cinemachine 3.1.7 | 玩家相机（第三人称跟随 + 瞄准 FOV/Lens） |
| Input System 1.19.0 | 全部玩家输入 |
| Unity Localization 1.5.11 | UI 本地化 |
| Animation Rigging 1.4.1 | 武器瞄准 IK 绑定 |
| Addressables 2.9.1 | 资产管理 |
| Loxodon Framework | 物品栏 UI 的 MVVM 绑定（嵌入式本地副本，位于 `LoxodonFramework/`） |
| QuickOutline（资源商店） | 网格描边高亮（`Outlines` 组件，位于 `Plugins/QuickOutline/`） |

### 第三方插件

| 插件 | 路径 | 用途 |
|---|---|---|
| EffectCore | `Plugins/EffectCore/` | 粒子特效系统（血液、火花、烟雾等材质和预设） |
| AllSky | `Plugins/Allsky/` | HDRI Haven 天空系统（天空盒材质和云层纹理） |
| YSA Toon | `Plugins/YSA Toon/` | 卡通渲染着色器（Toon Lit + Perfect Outline） |
| Loxodon Framework | `LoxodonFramework/` | MVVM 绑定框架（**已嵌入本地**，非仅 OpenUPM 依赖） |

OpenUPM 注册表：`com.vovgou`（Loxodon Framework 包）。

## 代码架构

### 两处代码根目录

- **`_Project/`** — 游戏特定代码：玩家、状态机、武器、相机、音频、物品、容器 UI
- **`Survivor_of_a_disaster/Scripts/`** — 交互系统、物品栏（CTIS）、工具类

没有 `.asmdef` 文件（Loxodon 自身的除外）——所有脚本共享默认的 Assembly-CSharp。

### 状态机

Core 目录下的 `IState.cs` 和 `StateMachine.cs` 是**空壳**——不要使用它们。

真正的状态机在 `_Project/Characters/Player/`：

```
CharacterStateMachine (MonoBehaviour)
  ├── BaseState（抽象类，位于 StateMachine/ 子目录）
  │     构造函数接收 CharacterStateMachine 上下文
  │     抽象方法 Enter() / Update() / Exit()
  ├── DefaultState（StateMachine/ 子目录）— 普通移动，检测到 Input.aim 时切换到 AimState
  └── AimState（StateMachine/ 子目录）— 瞄准移动，通过 WeaponController 处理射击
```

状态通过 `new DefaultState(this)` 创建——不使用对象池，不基于 ScriptableObject。`CharacterStateMachine.ChangeState()` 先调用当前状态的 `Exit()`，再调用新状态的 `Enter()`。

**重要：** `ThirdPersonController` 拥有公共方法（`GroundedCheck`、`JumpAndGravity`、`Move`，原为 private 后改为 public）。`DefaultState` 和 `AimState` 都通过 **System.Reflection** 调用它们，因为角色控制器原本设计为运行自己的 Update。但 `AimState` 仅调用 `GroundedCheck` 和 `JumpAndGravity`，不调用 `Move`——瞄准移动通过自己的 `HandleAimingMovement()` 实现。如果你修改 TPC 的方法签名，务必同步更新两个状态文件。

**⚠️ 已知问题：双重执行路径** — 在默认状态下，`ThirdPersonController.Update()` 自身也会调用 `GroundedCheck`、`JumpAndGravity`、`Move`，而 `DefaultState.Update()` 又通过 Reflection 再次调用同一组方法。这意味着**移动逻辑在默认状态下执行两次**（速度翻倍）。原因在于 TPC 原被设计为独立运行 Update，目前通过 TPC 内的 `IsAiming` 守卫逻辑来规避（瞄准时跳过 Move），但默认状态下仍为双重调用。

### 交互系统（解耦设计）

```
PlayerInteraction（OverlapSphere，基于距离）
  └── 找到最近的 InteractiveObjectBase → CanInteract()? → OnInteract()
        └── 交互成功后调用 InventoryManager.ToggleInventorySystem()

ObjectHighlighter（RaycastAll，始终运行，基于射线）
  └── 找到最近的 InteractiveObjectBase → 调用 OnSelected() / OnDeselected()

InteractiveObjectBase（抽象类）
  ├── 持有自身的 Outlines 组件 → 在 OnSelected/OnDeselected 中开关
  ├── 子类：ContainerBase、OldSate、WeaponPickup、AmmoPickup、KeliPickup、MRAP
  ├── CanInteract() — 默认检查 isUsed，容器类改为只检查 isInteractable
  ├── pickUpItemID — 对应 CTIS 物品数据库的 itemID，拾取时自动添加到玩家背包（0=不关联库存）
  ├── InteractableType 枚举 — Container、Item、Door、Switch、NPC、Readable、Pickup、Custom
  ├── InteractableMethod 枚举 — Open、PickUp、Use、Read、Push、Talk、Examine、Custom
  ├── InteractableInfo 结构体（[System.Serializable]）— 封装交互元数据（名称、描述、类型、方式、范围等），通过 GetInfo() 获取
  ├── CanShowUI() — 检查是否显示交互提示 UI
  └── InteractionBtn 监听 PlayerInteraction.IsContainerNearby 控制 UI 淡入淡出
```

**交互流程（已实现）：**
```
按E → PlayerInteraction.OnInteract()
  ├── UI已打开? → ToggleInventorySystem() 关闭 → return
  ├── 附近无容器? → 日志提示 → return
  └── GetNearestInteractable()
        ├── target.CanInteract()? → target.OnInteract()
        │     ├── 拾取物（pickUpItemID > 0）→ AddItemToPlayerBag() 添加到 CTIS 背包
        │     └── 容器（Container 类型）→ 注册缓存 + ToggleInventorySystem() 打开 UI
        └── 否则 → 日志提示
```

**解耦规则（用户指令）：** 每个模块管理自身的视觉/音频反馈。中央控制器只负责找到目标并调用接口方法。不要让一个模块直接操作另一个模块的组件。添加新系统时遵循此模式：
- `InteractiveObjectBase` 管理自身的描边——`ObjectHighlighter` 不触碰 `Outlines`
- `AimState` 不持有 `ObjectHighlighter` 的引用——高亮器独立运行
- 如果要给交互添加音频反馈，放在 `InteractiveObjectBase.OnInteract()` 里，不要放在 `PlayerInteraction` 里

### 容器系统（世界容器 → 自定义 Canvas UI 网格）

**继承链：** `InteractiveObjectBase → ContainerBase → OldSate`

**ContainerBase**（`_Project/Container/Base/ContainerBase.cs`）— 世界容器与 CTIS 的桥梁：
- 可配置 `gridWidth` × `gridHeight`（默认 5×5）、`containerId`（DataGUID，自动生成）
- 可配置 `containerDisplayName`（显示名称）、`containerThemeColor`（主题色）、`containerIcon`（图标）、`showRarityBorder`（稀有度边框）
- `OnInteract()` → 注册容器配置到 `IInventoryTreeCache` → 设置 `InventoryManager.ActiveWorldContainer = this` → `PrePopulateCache()` 从持久化 SO 加载物品
- `CanInteract()` 忽略 `isUsed`，容器可反复打开
- 子类（如 OldSate）只需继承，无需重复实现交互逻辑

**ContainerPanel**（`_Project/Container/UI/ContainerPanel.cs`）— 容器面板全生命周期管理器：
- 挂载在 `Menu_Container` GameObject 上，实现 `IContainerItemProvider`
- `Show(ContainerBase)` — 构建网格、显示面板、淡入动画、隐藏玩家背包
- `Hide()` — 销毁网格、恢复玩家背包、隐藏面板
- 自动按名称查找子对象绑定（`_titleText`、`_capacityText`、`_gridView`、`_infoPanel` 等），零手动配置
- 无预制体时通过 `CreateDefaultHierarchy(Transform parent)` 静态方法代码生成完整 UI 层级（TitleBar → ContainerNameText、CapacityText、CloseButton；ScrollView → GridContent；InfoPanel → InfoIcon、InfoName、InfoDesc、InfoStats）
- 管理 `ItemTooltip` 的显示/隐藏

**ContainerGridView**（`_Project/Container/UI/ContainerGridView.cs`）— 自定义 Canvas 网格渲染核心：
- **绕过 CTIS 原生 TetrisGridView/Loxodon 绑定做 UI 渲染**，使用原生 Canvas Image 绘制格子背景和网格线
- 创建**隐藏的** `TetrisGridView`（仅用于 `TetrisGridFactory.RegisterAssociation()` 兼容注册）
- 使用 CTIS 数据层 `TetrisGridVM`，监听 `PlaceItemViewRequested`/`RemoveItemViewRequested` 事件同步显示
- 通过 `IPointerMoveHandler`/`IPointerClickHandler`/`IPointerExitHandler` 处理鼠标交互
- 维护 `ContainerItemOverlay` 字典，支持物品叠加层的创建/刷新/销毁
- 触发事件：`OnItemClicked`、`OnItemRightClicked`、`OnItemHoverEnter`、`OnItemHoverExit`、`OnGridReady`、`OnGridDestroyed`

**ContainerItemOverlay**（`_Project/Container/UI/ContainerItemOverlay.cs`）— 物品可视化叠加层：
- 支持跨越多格显示（根据 `TetrisItemVM` 的宽高计算 RectTransform）
- 显示物品图标、稀有度边框（底部 3px 色条）、堆叠数量文字
- 悬浮/选中高亮状态切换
- `CreateDefault()` 静态方法在无预制体时自动生成

**ItemTooltip**（`_Project/Container/UI/ItemTooltip.cs`）— 全局悬浮提示：
- 挂载在 Canvas 根节点上，全局复用
- 显示物品名称、稀有度色条、描述文字
- 自动贴边定位（避免超出屏幕边界）
- `CreateDefault(Transform canvasParent)` 静态方法在无预制体时自动生成

**IContainerItemProvider**（`_Project/Container/IContainerItemProvider.cs`）— 容器物品操作接口：
- `TryPlaceItem(TetrisItemVM, int posX, int posY)` — 尝试放置物品
- `TryRemoveItem(TetrisItemVM)` — 移除物品
- `CanPlaceAt(TetrisItemVM, int posX, int posY)` — 检查位置是否可用
- `GetGridVM()` — 获取当前网格 ViewModel
- `GetAllItems()` — 获取所有物品
- `OnItemChanged` 事件 — 物品变化通知
- 由 `ContainerPanel` 实现，委托给 `ContainerGridView`

**容器面板生命周期：**
```
ContainerBase.OnInteract()
  → InventoryManager.ActiveWorldContainer = this

ToggleInventorySystem() [打开]
  → BindWorldContainerGrid(container)
    → containerPanel.Show(container)
      → gridView.BuildGrid(width, height, containerId)
        → new TetrisGridVM(w, h)   ← CTIS 数据层
        → 创建隐藏 TetrisGridView + TetrisGridFactory.RegisterAssociation   ← 兼容注册
        → hiddenGridView.ViewModel = vm   ← 触发 CTIS ApplyConfig → PrimeFromCache
        → 创建 Canvas Image 格子背景 + 网格线
        → RefreshItems()   ← 从 CTIS 缓存恢复物品，创建 ContainerItemOverlay
      → 更新标题、容量文字
      → FadeIn 淡入动画
      → 隐藏玩家背包面板

ToggleInventorySystem() [关闭]
  → UnbindWorldContainerGrid()
    → containerPanel.Hide()
      → gridView.DestroyGrid()
        → 解绑事件、清理 TetrisGridFactory 注册
        → Destroy(隐藏 GridView)
        → 清理所有格子 Image 和 ContainerItemOverlay
      → 恢复玩家背包面板
      → ActiveWorldContainer = null
```

**Editor 配置：**
- `InventoryManager` → `Container Panel` 字段指向 `Menu_Container` 上的 `ContainerPanel` 组件（如未赋值，运行时通过 `FindObjectOfType<ContainerPanel>()` 查找，查找失败则调用 `CreateDefaultHierarchy()` 自动创建）
- 无需手动配置 Scroll Rect、Viewport、GridContent：`ContainerPanel.AutoBind()` 自动按名称查找子对象
- 物品生成推荐方式：通过 `IContainerItemProvider.TryPlaceItem()` 或 `IInventoryTreeCache.PlaceItem()` 注入物品

### 武器系统

`WeaponController`（挂载在玩家 GameObject 上，`_Project/Characters/Player/WeaponController.cs`）：
- 基于射线检测的 hitscan，从枪口(muzzlePoint)/相机射向瞄准目标点(aimTarget)
- 可配置字段：`damage`、`maxRange`、`fireRate`、`spreadAngle`、`isAutomatic`（自动/半自动）
- 每次射击创建**临时 GameObject**（`"GunfireSFX_temp"`）播放音频，挂载 `AudioReverbFilter`（Generic 预设，混响由 `reverbLevel` 控制，0~1）
- `StopFiringEffects()` 启动协程 `FadeOutAndDestroy()`，使用 ease-out 曲线（`1 - (1-t)³`），时长由 `gunfireFadeOutDuration` 配置
- `OnDestroy()` 硬停止所有音源（销毁过程中协程不会运行）
- 使用 `AudioResource`（ARC，Audio Random Container）而非 `AudioClip` 来播放枪声
- 可选特效：`muzzleFlash`（ParticleSystem）、`hitEffectPrefab`（命中特效预制体）、`bulletTrailPrefab`（弹道拖尾）
- 可选 `outputAudioMixerGroup` 用于音频路由
- UnityEvents：`OnShoot`、`OnHit`、`OnDryFire`
- 伤害应用链：`IDamageable` 接口 → `GetComponentInParent` → `SendMessageUpwards("TakeDamage")` 回退

### 物品栏系统（CTIS）

第三方 Cholopol Tetris Inventory System（俄罗斯方块式物品栏）。MVVM 架构 + Loxodon 绑定：
- `InventoryManager` 是 `Singleton<InventoryManager>`
- `PlayerInteraction.OnInteract()` → `InventoryManager.ToggleInventorySystem()`
- `IsInventoryOpen` 属性 — 其他系统（状态机、TPC）通过此属性判断是否暂停角色输入
- 切换操作处理：鼠标锁定/解锁、StarterAssetsInputs 的 `cursorInputForLook`、UI 显示/隐藏、通过 `EventBus` 实例化/回收物品栏 UI
- 物品位置基于网格（俄罗斯方块风格），通过 `TetrisItemGhostVM` 拖拽，按 R 键旋转
- `InventoryManager` 引用 `ContainerPanel`（`_Project/Container/UI/ContainerPanel.cs`），`BindWorldContainerGrid`/`UnbindWorldContainerGrid` 都委托给 `ContainerPanel.Show`/`Hide` 处理
- `AddItemToPlayerBag(int itemID)` — 将指定 ID 的物品添加到玩家背包网格（自动找空位、支持堆叠）。拾取物（WeaponPickup/AmmoPickup/KeliPickup）的 `OnInteract()` 中调用此方法完成"场景拾取→库存"链路

**关键 CTIS 基础设施：**
- `TetrisGridVM(int width, int height)` — 网格 ViewModel 构造函数
- `TetrisGridFactory` — 静态注册表，按 GUID 管理 VM/View 的绑定
- `IInventoryTreeCache`（命名空间 `Cholopol.TIS.MVVM`）— 运行时容器-物品关系缓存
- `TetrisGridView.ViewModel = vm` — 设置时触发 `Bind()`，调用 `ApplyConfig()` + 从缓存恢复物品
- `DataGUID` — 稳定 GUID 组件，标识持久化网格

**⚠️ 命名空间陷阱：** 在 `Cholopol.TIS` 命名空间内写代码时，`Debug.Log` 会解析为 `Cholopol.TIS.Debug.Log` 而非 `UnityEngine.Debug.Log`。必须使用完整限定名 `UnityEngine.Debug.Log(...)`。

### 音频

- `AudioManager` — **空壳**，不要使用
- `AmbientSoundSystem` — **完全实现**：`Awake()` 中添加 AudioSource + 确保 Collider 为触发器；`OnTriggerEnter/Exit` 检测 "Player" 标签，通过协程淡入淡出音量。每个区域是一个挂载此脚本 + Collider + AudioClip 的 GameObject。可配置字段：`ambientClip`、`maxVolume`、`fadeDuration`、`spatialBlend`
- 武器音频由 `WeaponController` 直接管理（临时 AudioSource + AudioReverbFilter + AudioResource ARC）
- 枪支 SFX：`Survivor_of_a_disaster/Audio/SFX/Gun/` — `hk416d_shoot.ogg`（第一人称）、`hk416d_shoot_3p.ogg`（第三人称）、`hk416.asset`（ARC）
- 环境音效：`Survivor_of_a_disaster/Audio/SFX/Environment/` — `Wind.ogg`、`Thunderstorm.ogg`

### 露天物资点刷新系统

核心游戏循环的关键支撑：地图上的露天物资点，每个有多个槽位，物品被拾取后独立冷却刷新。

**SupplyPoint**（`_Project/GamePlaye/SupplyPoint/SupplyPoint.cs`）— 物资点主组件：
- 挂载在场景中的物资点 GameObject 上，代表一个物资刷新区
- 引用 `ItemDataList_SO` 作为物品数据库
- 稀有度权重 `commonWeight`~`artifactWeight`（越高越常见，0 表示不出现在此物资点）
- 配置散布半径 `randomRadius`
- 持有 `List<SupplySlot>` 槽位列表
- `RefreshAllSlots()` — 清空并重新生成全部槽位
- `ForceRefreshSlot(int index)` — 强制刷新指定槽位（忽略冷却）
- `OnDrawGizmosSelected()` — 编辑器可视化：绿球=有物品，黄球=冷却中

**SupplySlot**（SupplyPoint 内嵌 [System.Serializable] 类）— 单个生成槽位：
- `localPosition` — 相对物资点的位置
- `refreshCooldown` — 拾取后冷却秒数（1~600）
- 运行时状态：`spawnedItem`（当前生成的 GameObject）、`isOnCooldown`、`cooldownRemaining`

**刷新流程：**
```
Start() → RefreshAllSlots() → 为每个槽位 SpawnItem()

Update() 每帧遍历槽位：
  ├── spawnedItem.GetComponent<InteractiveObjectBase>().isUsed == true?
  │     → spawnedItem = null，进入冷却
  ├── isOnCooldown? → cooldownRemaining -= dt
  └── 冷却完毕? → SpawnItem()
        ├── SupplyLootHelper.PickRandomItem(数据库, 权重字典)
        │     ├── 按稀有度分组 → 各组有效权重 = 配置权重 × 物品数
        │     ├── 轮盘赌选稀有度 → 等概率选具体物品
        └── Instantiate(itemEntity, slotPosition, rotation)
```

**SupplyLootHelper**（`_Project/GamePlaye/SupplyPoint/SupplyLootHelper.cs`）— 静态工具：
- `PickRandomItem(ItemDataList_SO, Dictionary<ItemRarity, float>)` — 按权重加权随机选物品
- `PickRandomItemDefault(ItemDataList_SO)` — 使用默认权重（Common=100→Artifact=0）
- 纯逻辑，无 MonoBehavior 依赖

**WorldItemSpawnManager**（`_Project/Core/WorldItemSpawnManager/WorldItemSpawnManager.cs`）— 总控：
- `Start()` 时收集场景中全部 `SupplyPoint` 和旧版 `ItemSystem`
- `InitializeAll()` — 初始化所有物资点和生成系统
- `RefreshAllSupplyPoints()` — 刷新全部 SupplyPoint
- 兼容旧版 ItemSystem（`RefreshAllItemSystems()`）

**旧版（保留但不再推荐使用）：**
- `ItemSystem.cs` — 旧版单槽位生成器，已被 SupplyPoint 取代
- `ItemAsset.cs` — 旧版 ScriptableObject（仅 itemID），保留

### IDamageable 接口

**`_Project/Characters/IDamageable.cs`**：
- `void TakeDamage(float amount, Vector3 hitPoint)` — 任何可被子弹击中的物体实现此接口即可

### 相机系统

**PlayerCameraController**（`_Project/Cameras/PlayerCameraController.cs`）— Cinemachine 3.x 相机过渡：
- 管理 FOV 过渡（normalFOV=60 / aimFOV=35）、相机距离（normal=4m / aim=2.2m）、肩部偏移
- `UpdateAimTarget()` 从相机中心发射射线定位武器 IK 瞄准目标点
- 可配置 `targetLayer` 过滤射线检测层
- `EnterAim()` / `ExitAim()` 由 AimState 调用，运行在 `LateUpdate()` 中避免抖动
- 旧版 `PlayerCamera.cs` 和 `PlayerCameraRecenteringUtility.cs` **已删除**

### 可交互物预制体配置

`InteractiveThing/` 中的所有可交互预制体需要：
1. 一个继承自 `InteractiveObjectBase` 的脚本组件（WeaponPickup、AmmoPickup、KeliPickup、MRAP 等）
2. `Outlines` 组件（QuickOutline 插件）
3. Collider（是否设为触发器取决于交互类型）
4. 层设置为 **Container**
5. 在 Inspector 中填写 `objectName` 及其他字段

**现有预制体：**
- 容器类：`MRAP.prefab`、`Oldsate.prefab`
- 武器类：`AWP_geo.prefab`、`ak47_geo.prefab`、`aug_geo.prefab`、`mk14_geo.prefab`、`p320_geo.prefab`
- 子弹类：`762x39.prefab`、`9mm.prefab`
- 特效类：`SpecialEffects/BulletEffect/`、`SpecialEffects/HurtEffect/`
- 生成器：`ItemGenerator/Generator.prefab`

### 空壳文件（待实现 / 已废弃不要使用）

| 文件 | 路径 | 说明 |
|---|---|---|
| AudioManager | `_Project/Core/AudioManager.cs` | 空壳 |
| GameManager | `_Project/Core/GameManager.cs` | 空壳 |
| SceneLoader | `_Project/Core/SceneLoader.cs` | 空壳 |
| InteractionAPI | `Survivor_of_a_disaster/Scripts/Utilities/Interaction/InteractionAPI.cs` | 空壳 |
| IState | `_Project/Core/StateMachine/IState.cs` | 空壳（不要使用） |
| StateMachine | `_Project/Core/StateMachine/StateMachine.cs` | 空壳（不要使用） |
| IdleState | `_Project/Characters/StateMachines/Movement/IdleState.cs` | 空壳（与正在使用的状态机无关） |
| JumpState | `_Project/Characters/StateMachines/Movement/JumpState.cs` | 空壳（与正在使用的状态机无关） |
| OldSafe | `_Project/Container/Cora/oldSafe/OldSafe.cs` | 死空壳（已被 `OldSate.cs` 取代） |

### 已删除的文件

| 文件 | 原因 |
|---|---|
| `Scenes/SampleScene.unity` | 由 `Home.unity` 取代 |
| `_Project/Cameras/PlayerCamera.cs` | 由 `PlayerCameraController.cs` 取代 |
| `_Project/Cameras/PlayerCameraRecenteringUtility.cs` | 功能合并到 `PlayerCameraController` |
| `_Project/Characters/Player/Player.cs` | 不再需要 |
| `InteractableObject/SwatVan/`（SwatVan.cs + 模型） | 由 MRAP 取代 |
| `_Project/UI/Image/Items/`（物品 PNG 图标） | 替换为 3D 模型预制体 |

### 输入绑定

`StarterAssetsInputs` 在标准 StarterAssets 基础上新增了自定义字段：
- `aim`（bool）— 鼠标右键，切换瞄准状态
- `leftclick`（bool）— 鼠标左键，射击

输入 Action 资产：`InputSystem_Actions.inputactions`（项目根目录）和 `StarterAssets/InputSystem/StarterAssets.inputactions`。

### 玩家预制体架构

玩家预制体（`PlayerArmature`）挂载以下关键组件：
- `ThirdPersonController` — 移动、跳跃、重力
- `CharacterStateMachine` — 状态管理（Default/Aim）
- `StarterAssetsInputs` — 输入解析
- `WeaponController` — 射击
- `PlayerCameraController` — Cinemachine 相机过渡（替换已删除的 `PlayerCamera.cs`）
- `ObjectHighlighter` — 瞄准目标检测
- `PlayerInteraction` — 近距离交互
- `CharacterController` — Unity 物理移动

**物品栏打开时输入封锁：** `CharacterStateMachine.Update()` 和 `ThirdPersonController.Update()/LateUpdate()` 在 `InventoryManager.IsInventoryOpen` 为 true 时直接 return，阻止所有角色输入（移动、瞄准、射击、跳跃、冲刺、相机旋转）。使用完整限定名 `Cholopol.TIS.InventoryManager.Instance` 避免添加 using。

相机设置使用 Cinemachine，搭配 `CinemachineThirdPersonFollow` 和由 `PlayerCameraController.UpdateAimTarget()` 驱动的瞄准目标 Transform。

### 瞄准动画

`StarterAssets/ThirdPersonController/Character/Animations/Aiming/` 下有 9 个瞄准动画 .fbx 文件：
`@idle aiming`、`@walk forward`、`@walk backward`、`@walk left`、`@walk right`、`@walk forward left`、`@walk forward right`、`@walk backward left`、`@walk backward right`。通过 `AimingX`/`AimingY` Blend Tree 参数驱动。

### 新增模型

- `MRAP.fbx`（装甲车辆，`Survivor_of_a_disaster/Models/SceneModel/mrap-vehicle/`）+ 材质和贴图
- `Models/ItemModel/` 下的子弹、收藏品、枪支模型子目录

### 开发流程

- 没有 CLI 构建——所有工作都在 Unity Editor 中进行
- 场景文件位于 `Scenes/` 和 `_Project/Scenes/`
- 物品数据通过 ScriptableObject 配置：`ItemDataList_SO`、`TetrisItemPointSet_SO`、`InventoryPlacementConfig_SO`、`InventoryData_SO`
- Play 模式测试：打开 `Scenes/Home.unity` 场景，按 Play。按 B 键切换背包（当前代码中已注释——改为通过交互打开）。
