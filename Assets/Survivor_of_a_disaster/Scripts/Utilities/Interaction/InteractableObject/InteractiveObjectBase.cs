using UnityEngine;

/// <summary>
/// 可交互物体的抽象基类。
/// 定义了所有可交互物体共有的属性、枚举和方法。
/// 子类（ContainerBase、WeaponPickup、AmmoPickup 等）继承此类并实现 OnInteract()。
/// 自身管理 Outlines 描边组件，在 OnSelected/OnDeselected 中开关。
/// </summary>
public abstract class InteractiveObjectBase : MonoBehaviour
{
    private Outlines _outline;

    protected virtual void Awake()
    {
        _outline = GetComponent<Outlines>();

        // 默认类型设为 Pickup，避免枚举默认值 0=Container 导致拾取物误开背包
        // ContainerBase.Awake() 会覆盖为 Container
        if (objectType == InteractableType.Container && GetComponent<ContainerBase>() == null)
            objectType = InteractableType.Pickup;
    }

    // ====================== Inspector 字段 ======================

    [Header("基础信息")]
    [Tooltip("物体名称（用于 UI 提示和调试）")]
    public string objectName = "nullName";

    [TextArea(2, 4)]
    [Tooltip("物体描述文本")]
    public string objectDescription;

    [Header("交互设置")]
    [Tooltip("物体类型：容器、物品、门、开关等")]
    public InteractableType objectType;

    [Tooltip("交互方式：打开、拾取、使用、阅读等")]
    public InteractableMethod interactMethod;

    [Tooltip("交互触发距离（米）")]
    public float interactRange = 3f;

    [Tooltip("是否允许交互")]
    public bool isInteractable = true;

    [Header("UI 设置")]
    [Tooltip("是否在靠近时显示交互提示 UI")]
    public bool showInteractionUI = true;

    [Tooltip("自定义交互提示文字（为空则使用默认文字）")]
    public string customInteractHint;

    [Header("拾取设置")]
    [Tooltip("是否可被拾取到物品栏")]
    public bool isPickupable;

    [Tooltip("是否需要特定钥匙物品才能交互")]
    public bool needKeyItem;

    [Tooltip("所需钥匙物品的名称")]
    public string requiredItemName;

    [Header("物品库存关联")]
    [HideInInspector]
    public int pickUpItemID = -1; // -1=未注入，≥0 为有效 itemID

    /// <summary>
    /// 丢弃物携带的实例数据（Guid、堆叠数、CustomData 等）。
    /// 拾取时会优先用此数据还原物品状态，保证词条/附魔不丢失。
    /// </summary>
    [HideInInspector]
    public TetrisItemPersistentData discardData;

    [Header("状态")]
    [Tooltip("是否已被交互过（true=已经交互过，再次交互不会重复执行逻辑）")]
    public bool isUsed;

    [Tooltip("最大可交互次数（0 表示无限次）")]
    public int maxUseCount = 1;

    [Tooltip("当前已交互次数")]
    public int currentUseCount;

    // ====================== 枚举定义 ======================

    /// <summary>
    /// 可交互物体的类型分类。
    /// 用于 UI 提示和交互逻辑分支判断。
    /// </summary>
    public enum InteractableType
    {
        [Tooltip("容器：箱子、柜子、保险箱等，打开后显示网格物品栏")]
        Container,
        [Tooltip("物品：可交互的独立物品")]
        Item,
        [Tooltip("门：可开关的门")]
        Door,
        [Tooltip("开关：拉杆、按钮等机关")]
        Switch,
        [Tooltip("NPC：可对话的非玩家角色")]
        NPC,
        [Tooltip("可阅读物：便条、书籍、终端等")]
        Readable,
        [Tooltip("拾取物：可捡起的物品")]
        Pickup,
        [Tooltip("自定义类型")]
        Custom
    }

    /// <summary>
    /// 交互方式枚举。
    /// 定义玩家与物体交互时的具体行为。
    /// 子类可在 OnInteract() 中用 switch 判断此枚举执行不同逻辑。
    /// </summary>
    public enum InteractableMethod
    {
        [Tooltip("打开：打开容器或门")]
        Open,
        [Tooltip("拾取：捡起物品到物品栏")]
        PickUp,
        [Tooltip("使用：使用物体（如开关、设备）")]
        Use,
        [Tooltip("阅读：阅读文本内容")]
        Read,
        [Tooltip("推动：推动或移动物体")]
        Push,
        [Tooltip("对话：与 NPC 交谈")]
        Talk,
        [Tooltip("检查：仔细观察物体")]
        Examine,
        [Tooltip("自定义交互方式")]
        Custom
    }

    // ====================== 公开方法 ======================

    /// <summary>
    /// 获取物体的交互信息数据。
    /// 当玩家靠近物体时调用，用于显示交互提示 UI 或判断交互条件。
    /// </summary>
    /// <returns>包含全部交互元数据的 InteractableInfo 结构体</returns>
    public virtual InteractableInfo GetInfo()
    {
        return new InteractableInfo
        {
            name = objectName,
            description = objectDescription,
            type = objectType,
            method = interactMethod,
            isInteractable = isInteractable,
            isPickupable = isPickupable,
            interactRange = interactRange,
            needKeyItem = needKeyItem,
            requiredItemName = requiredItemName,
            isUsed = isUsed,
            maxUseCount = maxUseCount,
            currentUseCount = currentUseCount,
            showInteractionUI = showInteractionUI,
            customInteractHint = customInteractHint,
            gameObject = gameObject
        };
    }

    /// <summary>
    /// 判断当前是否可以交互。
    /// 默认逻辑：允许交互 且 未被使用过。
    /// 容器类（ContainerBase）重写为仅检查 isInteractable，忽略 isUsed。
    /// </summary>
    public virtual bool CanInteract()
    {
        return isInteractable && !isUsed;
    }

    /// <summary>
    /// 判断是否应该显示交互提示 UI。
    /// 默认逻辑：UI 显示开关开启 且 可以交互。
    /// </summary>
    public virtual bool CanShowUI()
    {
        return showInteractionUI && CanInteract();
    }

    /// <summary>
    /// 判断是否允许多次交互。
    /// 默认逻辑：当前使用次数 < 最大使用次数。
    /// </summary>
    public virtual bool CanBeUsedMultipleTimes()
    {
        return currentUseCount < maxUseCount;
    }

    /// <summary>
    /// 重置交互状态到初始值。
    /// 将 isUsed 设为 false，currentUseCount 归零。
    /// 用于物品刷新后重置可交互状态。
    /// </summary>
    public virtual void ResetState()
    {
        isUsed = false;
        currentUseCount = 0;
    }

    /// <summary>
    /// 执行交互逻辑。子类必须实现。
    /// 例如：打开容器、拾取物品、触发机关等。
    /// </summary>
    public abstract void OnInteract();

    /// <summary>
    /// 被玩家准星选中时调用（由 ObjectHighlighter 驱动）。
    /// 开启自身的 Outlines 描边效果。
    /// </summary>
    public virtual void OnSelected()
    {
        if (_outline != null)
            _outline.enabled = true;
    }

    /// <summary>
    /// 玩家准星移开时调用（由 ObjectHighlighter 驱动）。
    /// 关闭自身的 Outlines 描边效果。
    /// </summary>
    public virtual void OnDeselected()
    {
        if (_outline != null)
            _outline.enabled = false;
    }
}

/// <summary>
/// 交互信息数据载体。
/// 封装物体的全部交互元数据，在交互系统中传递使用。
/// 通过 InteractiveObjectBase.GetInfo() 获取。
/// </summary>
[System.Serializable]
public struct InteractableInfo
{
    [Tooltip("物体名称")]
    public string name;

    [Tooltip("物体描述")]
    public string description;

    [Tooltip("物体类型")]
    public InteractiveObjectBase.InteractableType type;

    [Tooltip("交互方式")]
    public InteractiveObjectBase.InteractableMethod method;

    [Tooltip("是否允许交互")]
    public bool isInteractable;

    [Tooltip("是否可拾取到物品栏")]
    public bool isPickupable;

    [Tooltip("交互触发距离（米）")]
    public float interactRange;

    [Tooltip("是否需要钥匙物品")]
    public bool needKeyItem;

    [Tooltip("所需钥匙物品名称")]
    public string requiredItemName;

    [Tooltip("是否已被使用过")]
    public bool isUsed;

    [Tooltip("最大使用次数")]
    public int maxUseCount;

    [Tooltip("当前已使用次数")]
    public int currentUseCount;

    [Tooltip("是否显示交互提示 UI")]
    public bool showInteractionUI;

    [Tooltip("自定义交互提示文字")]
    public string customInteractHint;

    [Tooltip("关联的 GameObject 引用")]
    public GameObject gameObject;
}
