using UnityEngine;
using Loxodon.Framework.Contexts;
using Cholopol.TIS;
using Cholopol.TIS.MVVM;
using Cholopol.TIS.SaveLoadSystem;

/// <summary>
/// 容器桥梁类——将世界中的容器物体与 CTIS 物品栏网格系统连接。
/// 继承自 InteractiveObjectBase，使容器可被交互系统检测。
/// 子类（如 OldSate）只需挂载此脚本并配置网格尺寸即可。
/// </summary>
public class ContainerBase : InteractiveObjectBase
{
    [Header("===== 容器网格配置 =====")]
    [Tooltip("容器唯一标识符（首次 Awake 自动生成，序列化后保持稳定）")]
    public string containerId;

    [Tooltip("网格宽度（格子数）")]
    [Range(1, 20)]
    public int gridWidth = 5;

    [Tooltip("网格高度（格子数）")]
    [Range(1, 20)]
    public int gridHeight = 5;

    [Tooltip("容器在 UI 中的显示名称（留空则用 objectName）")]
    public string containerDisplayName;

    [Header("===== 容器外观配置 =====")]
    [Tooltip("容器主题色（影响 UI 中的面板色调）")]
    public Color containerThemeColor = new Color(0.353f, 0.345f, 0.224f); // #5A5839

    [Tooltip("容器类型图标（可选）")]
    public Sprite containerIcon;

    [Tooltip("是否显示物品稀有度边框")]
    public bool showRarityBorder = true;

    protected override void Awake()
    {
        base.Awake();

        // 确保有稳定的唯一标识符
        var guidComp = GetComponent<DataGUID>();
        if (guidComp == null)
            guidComp = gameObject.AddComponent<DataGUID>();

        if (string.IsNullOrEmpty(containerId))
            containerId = guidComp.guid;

        if (string.IsNullOrEmpty(containerDisplayName))
            containerDisplayName = objectName;

        // 容器类型固定
        objectType = InteractableType.Container;
        interactMethod = InteractableMethod.Open;
    }

    public override void OnInteract()
    {
        // 1. 向运行时缓存注册容器配置，并从持久化存储预填充物品数据
        var cache = Context.GetApplicationContext().GetService<IInventoryTreeCache>();
        if (cache != null)
        {
            cache.SetContainerConfig(containerId, gridWidth, gridHeight,
                Settings.gridTileSizeWidth, Settings.gridTileSizeHeight);

            // 从 SO 持久化数据预填充运行时缓存，确保后续 PrimeFromCache() 能找到物品
            PrePopulateCache(cache);
        }

        // 2. 设置当前激活容器，InventoryManager 在打开/切换 UI 时绑定此容器的网格面板
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.ActiveWorldContainer = this;

        Debug.Log($"[ContainerBase] 打开容器: {containerDisplayName} ({containerId}) {gridWidth}×{gridHeight}");
    }

    /// <summary>
    /// 容器可反复打开，不受 isUsed 限制。
    /// </summary>
    public override bool CanInteract()
    {
        return isInteractable;
    }

    /// <summary>
    /// 从持久化存储（InventoryData_SO）预填充运行时缓存。
    /// 确保 BindContainer 中的 PrimeFromCache() 能在全局 BuildRuntimeCache 之前找到物品。
    /// </summary>
    private void PrePopulateCache(IInventoryTreeCache cache)
    {
        var saveLoadService = InventorySaveLoadService.Instance;
        if (saveLoadService == null || saveLoadService.inventoryData_SO == null) return;

        var itemList = saveLoadService.inventoryData_SO.inventoryItemList;
        if (itemList == null) return;

        foreach (var item in itemList)
        {
            if (item != null && item.persistentGridGuid == containerId)
            {
                cache.PlaceItem(containerId, item);
            }
        }
    }

    /// <summary>
    /// 获取容器信息，覆盖基类以填入容器专属数据。
    /// </summary>
    public override InteractableInfo GetInfo()
    {
        var info = base.GetInfo();
        info.name = containerDisplayName;
        return info;
    }
}
