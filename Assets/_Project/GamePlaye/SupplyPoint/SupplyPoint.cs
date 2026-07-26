using UnityEngine;
using System.Collections.Generic;
using Cholopol.TIS;

/// <summary>
/// 露天物资点 — 管理一个物资刷新区域。
/// 挂载在场景中的物资点 GameObject 上。
/// 每个槽位独立冷却：物品被拾取后进入冷却，冷却完毕后按稀有度权重随机生成新物品。
/// </summary>
public class SupplyPoint : MonoBehaviour
{
    [Header("===== 基本信息 =====")]
    [Tooltip("物资点名称（仅用于调试）")]
    public string supplyPointName = "未命名物资点";

    [Header("===== 物品配置 =====")]
    [Tooltip("物品数据库（ItemDataList_SO）")]
    public ItemDataList_SO itemDatabase;

    [Header("===== 稀有度权重（越高越常见） =====")]
    [Range(0f, 200f)]
    public float commonWeight = 100f;
    [Range(0f, 200f)]
    public float uncommonWeight = 50f;
    [Range(0f, 200f)]
    public float rareWeight = 20f;
    [Range(0f, 200f)]
    public float epicWeight = 5f;
    [Range(0f, 200f)]
    public float legendaryWeight = 1f;
    [Range(0f, 200f)]
    public float artifactWeight = 0f;

    [Header("===== 生成设置 =====")]
    [Tooltip("物品散布半径（在槽位位置基础上的随机偏移）")]
    public float randomRadius = 0.3f;

    [Header("===== 槽位列表 =====")]
    public List<SupplySlot> slots = new List<SupplySlot>();

    // 缓存的权重字典，避免每帧分配
    private Dictionary<ItemRarity, float> _weights;

    // ====================== 生命周期 ======================

    private void Start()
    {
        BuildWeights();
        RefreshAllSlots();
    }

    private void Update()
    {
        if (itemDatabase == null || slots.Count == 0) return;

        // 背包打开时暂停冷却计时，物品不会在玩家整理背包时刷新
        bool isInventoryOpen = InventoryManager.Instance != null
            && InventoryManager.Instance.IsInventoryOpen;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];

            // 检查物品是否已被拾取（背包打开时跳过，避免玩家看背包时物品消失）
            if (slot.spawnedItem != null && !isInventoryOpen)
            {
                var interactable = slot.spawnedItem.GetComponentInChildren<InteractiveObjectBase>();
                if (interactable == null || interactable.isUsed)
                {
                    // 拾取后先销毁场景中的物品，再清空引用
                    if (slot.spawnedItem != null)
                        Destroy(slot.spawnedItem);

                    slot.spawnedItem = null;
                    slot.isOnCooldown = true;
                    slot.cooldownRemaining = slot.refreshCooldown;
                }
            }

            // 冷却中（背包打开时暂停计时）
            if (slot.isOnCooldown && !isInventoryOpen)
            {
                slot.cooldownRemaining -= Time.deltaTime;
                if (slot.cooldownRemaining <= 0f)
                {
                    slot.cooldownRemaining = 0f;
                    slot.isOnCooldown = false;
                    SpawnItem(slot);
                }
            }
        }
    }

    // ====================== 公开方法 ======================

    /// <summary>强制刷新所有槽位（清空现有物品并重新生成）。</summary>
    public void RefreshAllSlots()
    {
        BuildWeights();

        if (slots == null || slots.Count == 0)
        {
            UnityEngine.Debug.LogWarning($"[SupplyPoint] {supplyPointName}: 槽位列表为空！请在 Inspector 的 Slots 列表中点击 + 添加至少一个槽位。");
            return;
        }

        if (itemDatabase == null)
        {
            UnityEngine.Debug.LogWarning($"[SupplyPoint] {supplyPointName}: 未设置物品数据库（ItemDataList_SO）！");
            return;
        }

        foreach (var slot in slots)
        {
            // 清理旧物品
            if (slot.spawnedItem != null)
            {
                var interactable = slot.spawnedItem.GetComponentInChildren<InteractiveObjectBase>();
                if (interactable != null && !interactable.isUsed)
                    Destroy(slot.spawnedItem);
            }

            slot.spawnedItem = null;
            slot.isOnCooldown = false;
            slot.cooldownRemaining = 0f;

            SpawnItem(slot);
        }

        UnityEngine.Debug.Log($"[SupplyPoint] {supplyPointName}: 刷新全部 {slots.Count} 个槽位");
    }

    /// <summary>强制刷新指定槽位（忽略冷却）。</summary>
    public void ForceRefreshSlot(int index)
    {
        if (index < 0 || index >= slots.Count) return;

        var slot = slots[index];

        if (slot.spawnedItem != null)
        {
            var interactable = slot.spawnedItem.GetComponentInChildren<InteractiveObjectBase>();
            if (interactable != null && !interactable.isUsed)
                Destroy(slot.spawnedItem);
        }

        slot.spawnedItem = null;
        slot.isOnCooldown = false;
        slot.cooldownRemaining = 0f;

        SpawnItem(slot);
    }

    // ====================== 内部方法 ======================

    private void BuildWeights()
    {
        _weights = new Dictionary<ItemRarity, float>
        {
            { ItemRarity.Common, commonWeight },
            { ItemRarity.Uncommon, uncommonWeight },
            { ItemRarity.Rare, rareWeight },
            { ItemRarity.Epic, epicWeight },
            { ItemRarity.Legendary, legendaryWeight },
            { ItemRarity.Artifact, artifactWeight },
        };
    }

    private void SpawnItem(SupplySlot slot)
    {
        if (itemDatabase == null || _weights == null) return;

        ItemDetails selected = SupplyLootHelper.PickRandomItem(itemDatabase, _weights);
        if (selected == null)
        {
            UnityEngine.Debug.LogWarning($"[SupplyPoint] {supplyPointName}: 未能从数据库中选择物品");
            return;
        }

        if (selected.itemEntity == null)
        {
            UnityEngine.Debug.LogWarning($"[SupplyPoint] {supplyPointName}: 物品 \"{selected.localizedName}\" 没有 itemEntity 预制体");
            return;
        }

        // 计算生成位置
        Vector3 spawnPos = transform.position + slot.localPosition;

        if (randomRadius > 0f)
        {
            Vector2 random2D = Random.insideUnitCircle * randomRadius;
            spawnPos.x += random2D.x;
            spawnPos.z += random2D.y;
        }

        Quaternion prefabRot = selected.itemEntity.transform.rotation;
        GameObject instance = Instantiate(selected.itemEntity, spawnPos, prefabRot);
        slot.spawnedItem = instance;

        // 自动注入 itemID，拾取时直接关联 CTIS 库存
        var pickup = instance.GetComponentInChildren<InteractiveObjectBase>(true);
        if (pickup != null)
        {
            pickup.pickUpItemID = selected.itemID;
            UnityEngine.Debug.Log($"[SupplyPoint] 注入 pickUpItemID={selected.itemID} → {pickup.GetType().Name} on {instance.name}");
        }
        else
        {
            // 诊断：列出实例上所有组件帮助定位问题
            var allComponents = instance.GetComponentsInChildren<MonoBehaviour>(true);
            var compNames = new System.Text.StringBuilder();
            foreach (var c in allComponents)
            {
                if (compNames.Length > 0) compNames.Append(", ");
                compNames.Append(c.GetType().Name);
            }
            UnityEngine.Debug.LogError(
                $"[SupplyPoint] {supplyPointName}: 生成 {selected.localizedName.GetLocalizedString()} (ID={selected.itemID}) " +
                $"但实例上没有找到 InteractiveObjectBase！\n" +
                $"  根节点: {instance.name}, 子节点数: {instance.transform.childCount}\n" +
                $"  全部组件: [{compNames}]");
        }

        UnityEngine.Debug.Log($"[SupplyPoint] {supplyPointName}: 槽位生成 {selected.localizedName.GetLocalizedString()} (ID={selected.itemID}, rarity={selected.itemRarity}) @ {spawnPos}");
    }

    // ====================== 编辑器辅助 ======================

#if UNITY_EDITOR
    [ContextMenu("添加默认槽位（3个，间隔1.5m）")]
    private void AddDefaultSlots()
    {
        slots.Clear();
        slots.Add(new SupplySlot { localPosition = new Vector3(-1.5f, 0, 0), refreshCooldown = 30f });
        slots.Add(new SupplySlot { localPosition = new Vector3(0, 0, 0),      refreshCooldown = 30f });
        slots.Add(new SupplySlot { localPosition = new Vector3(1.5f, 0, 0),   refreshCooldown = 45f });
        UnityEngine.Debug.Log($"[SupplyPoint] {supplyPointName}: 已添加 3 个默认槽位。（可自行调整位置和冷却时间）");
    }
#endif

    // ====================== 编辑器可视化 ======================

    private void OnDrawGizmosSelected()
    {
        // 物资点本体
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.7f);
        Gizmos.DrawWireCube(transform.position, new Vector3(2f, 0.1f, 2f));

        if (slots == null || slots.Count == 0) return;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            Vector3 slotWorldPos = transform.position + slot.localPosition;

            // 槽位标记球
            Color stateColor;
            if (Application.isPlaying)
            {
                if (slot.spawnedItem != null)
                    stateColor = Color.green;   // 有物品
                else if (slot.isOnCooldown)
                    stateColor = Color.yellow;  // 冷却中
                else
                    stateColor = Color.white;   // 空闲
            }
            else
            {
                stateColor = new Color(0.5f, 0.8f, 0.5f, 0.8f);
            }

            Gizmos.color = stateColor;
            Gizmos.DrawSphere(slotWorldPos, 0.15f);

            // 散布范围
            Gizmos.color = new Color(stateColor.r, stateColor.g, stateColor.b, 0.2f);
            Gizmos.DrawWireSphere(slotWorldPos, randomRadius > 0f ? randomRadius : 0.2f);

            // 标签
#if UNITY_EDITOR
            UnityEditor.Handles.color = stateColor;
            UnityEditor.Handles.Label(
                slotWorldPos + Vector3.up * 0.3f,
                $"#{i}" + (Application.isPlaying && slot.isOnCooldown ? $" ({slot.cooldownRemaining:F0}s)" : "")
            );
#endif
        }
    }

    // ====================== SupplySlot 槽位定义 ======================

    [System.Serializable]
    public class SupplySlot
    {
        [Tooltip("相对物资点 Transform 的本地位置")]
        public Vector3 localPosition;

        [Tooltip("物品被拾取后，重新生成的冷却时间（秒）")]
        [Range(1f, 600f)]
        public float refreshCooldown = 30f;

        // ===== 运行时状态（不在 Inspector 中显示） =====
        [HideInInspector] public GameObject spawnedItem;
        [HideInInspector] public float cooldownRemaining;
        [HideInInspector] public bool isOnCooldown;
    }
}
