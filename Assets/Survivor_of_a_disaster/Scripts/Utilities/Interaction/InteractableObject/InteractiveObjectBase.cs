using UnityEngine;

public abstract class InteractiveObjectBase : MonoBehaviour
{
    [Header("基础信息")]
    public string objectName = "nullName";
    [TextArea(2, 4)]
    public string objectDescription;

    [Header("交互设置")]
    public InteractableType objectType;
    public InteractableMethod interactMethod;
    public float interactRange = 3f;
    public bool isInteractable = true;

    [Header("UI设置")]
    public bool showInteractionUI = true;         // 是否显示交互UI
    public string customInteractHint;              // 自定义交互提示文字（为空则用默认）

    [Header("拾取设置")]
    public bool isPickupable;
    public bool needKeyItem;
    public string requiredItemName;

    /// <summary>
    /// isUsed是可交互状态，true是已经交互过了，再次交互的时候就不会执行相应的脚本了。
    /// </summary>
    [Header("状态")]
    public bool isUsed;//是否可交互
    public int maxUseCount = 1;
    public int currentUseCount;

    /// <summary>
    /// 交互方法，子类必须实现
     /// 这里可以放置交互逻辑，比如打开容器、拾取物品、触发机关等
     /// 也可以在子类中添加额外的方法来处理特定的交互行为
     /// 例如：OpenContainer()、PickUpItem()、ActivateSwitch() 等
    /// </summary>

    public enum InteractableType
    {
        Container,
        Item,
        Door,
        Switch,
        NPC,
        Readable,
        Pickup,
        Custom
    }

    /// <summary>
    /// 交互方式，定义了玩家与物体交互时的具体行为
     /// 例如：Open 表示打开容器或门，PickUp 表示拾取物品，Use 表示使用物体，Read 表示阅读文本，Push 表示推动物体，Talk 表示与NPC对话，Examine表示检查物体，Custom表示自定义交互方式
     /// 在具体的交互逻辑中，可以根据 interactMethod 来执行不同的操作，例如在 OnInteract() 方法中使用 switch 语句来区分不同的交互方式
     /// 这样可以让交互系统更加灵活和可扩展，方便后续添加新的交互类型和方式
     /// 例如：
     /// switch (interactMethod)
     /// {
     ///     case InteractableMethod.Open:
     ///         OpenContainer();
     ///         break;
     ///     case InteractableMethod.PickUp:
     ///         PickUpItem();
     ///         break;
     ///     // 其他交互方式...
     /// }
    /// </summary>

    public enum InteractableMethod
    {
        Open,
        PickUp,
        Use,
        Read,
        Push,
        Talk,
        Examine,
        Custom
    }
    /// <summary>
    /// 获取物体的交互信息，返回一个 InteractableInfo 结构体，包含了物体的名称、描述、类型、交互方式、是否可交互、是否可拾取、交互范围、是否需要钥匙物品、所需钥匙物品名称、是否已使用、最大使用次数、当前使用次数、是否显示交互UI、自定义交互提示文字以及物体的 GameObject 引用
     /// 这个方法可以在玩家靠近物体时调用，用于显示交互提示信息或者判断玩家是否可以与物体进行交互
     /// 例如，在 PlayerInteraction 脚本中，当玩家靠近一个 InteractiveObjectBase 物体时，可以调用 GetInfo() 方法来获取该物体的交互信息，并根据返回的信息来决定是否显示交互UI或者执行交互操作
     /// 这样可以让交互系统更加模块化和数据驱动，方便后续添加新的物体类型和交互逻辑
     /// 例如：
     /// InteractableInfo info = nearbyObject.GetInfo();
     /// if (info.CanShowUI())
     /// {
     ///     ShowInteractionUI(info);
     /// }
     /// if (playerInput.InteractPressed && info.CanInteract())
     /// {
     ///     nearbyObject.OnInteract();
     /// }
     /// }
    /// </summary>
    /// <returns></returns>

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

    public virtual bool CanInteract()
    {
        return isInteractable && !isUsed;
    }

    public virtual bool CanShowUI()
    {
        return showInteractionUI && CanInteract();
    }

    public virtual bool CanBeUsedMultipleTimes()
    {
        return currentUseCount < maxUseCount;
    }

    public virtual void ResetState()
    {
        isUsed = false;
        currentUseCount = 0;
    }

    public abstract void OnInteract();

    public virtual void OnSelected() { }
    public virtual void OnDeselected() { }
}

/// <summary>
/// 交互信息结构体，用于封装物体的交互相关数据，方便在交互系统中传递和使用
/// </summary>
[System.Serializable]

public struct InteractableInfo
{
    public string name;
    public string description;
    public InteractiveObjectBase.InteractableType type;
    public InteractiveObjectBase.InteractableMethod method;
    public bool isInteractable;
    public bool isPickupable;
    public float interactRange;
    public bool needKeyItem;
    public string requiredItemName;
    public bool isUsed;
    public int maxUseCount;
    public int currentUseCount;
    public bool showInteractionUI;
    public string customInteractHint;
    public GameObject gameObject;
}