using UnityEngine;

/// <summary>
/// 世界物品生成总控 — 收集场景中所有 SupplyPoint 和 ItemSystem，
/// 在 Start 时统一初始化刷新。同时提供公开刷新方法供外部调用。
/// </summary>
public class WorldItemSpawnManager : MonoBehaviour
{
    private SupplyPoint[] _supplyPoints;
    private ItemSystem[] _itemSystems;

    [Header("物品资产列表")]
    public ItemDataList_SO itemDatabase;

    private void Start()
    {
        _supplyPoints = FindObjectsOfType<SupplyPoint>();
        _itemSystems = FindObjectsOfType<ItemSystem>();

        InitializeAll();
    }

    /// <summary>
    /// 初始化所有物资点和生成系统。
    /// Start 自动调用一次，也可外部手动调用以重置整张地图的物资。
    /// </summary>
    public void InitializeAll()
    {
        // SupplyPoint 自己在 Start 中已经刷新过，
        // 这里只需收集引用，方便后续统一操作
        if (_supplyPoints == null || _supplyPoints.Length == 0)
            _supplyPoints = FindObjectsOfType<SupplyPoint>();

        // 旧版 ItemSystem 兼容（如果场景中还有独立使用的 ItemSystem）
        if (_itemSystems == null || _itemSystems.Length == 0)
            _itemSystems = FindObjectsOfType<ItemSystem>();

        // SupplyPoint 各自在 Start() 中自主刷新，不需要这里重复调用
        RefreshAllItemSystems();

        UnityEngine.Debug.Log($"[WorldItemSpawnManager] 初始化完成: {(_supplyPoints?.Length ?? 0)} 个物资点, {(_itemSystems?.Length ?? 0)} 个旧版 ItemSystem");
    }

    /// <summary>刷新所有 SupplyPoint 物资点。</summary>
    public void RefreshAllSupplyPoints()
    {
        if (_supplyPoints == null) return;

        foreach (var sp in _supplyPoints)
        {
            if (sp != null && sp.isActiveAndEnabled)
                sp.RefreshAllSlots();
        }
    }

    /// <summary>旧版 ItemSystem 兼容刷新。</summary>
    private void RefreshAllItemSystems()
    {
        if (_itemSystems == null || itemDatabase == null) return;
        if (itemDatabase.itemDetailsList == null || itemDatabase.itemDetailsList.Count == 0) return;

        foreach (var system in _itemSystems)
        {
            if (system == null || !system.isActiveAndEnabled) continue;

            int index = Random.Range(0, itemDatabase.itemDetailsList.Count);
            ItemDetails randomItem = itemDatabase.itemDetailsList[index];
            system.Spawn(randomItem.itemID, itemDatabase);
        }
    }
}
