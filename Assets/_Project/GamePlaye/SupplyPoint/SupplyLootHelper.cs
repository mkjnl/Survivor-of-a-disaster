using UnityEngine;
using System.Collections.Generic;
using Cholopol.TIS;

/// <summary>
/// 物资刷新工具类：按稀有度权重从物品数据库中随机选取物品。
/// 纯静态方法，不依赖 MonoBehaviour。
/// </summary>
public static class SupplyLootHelper
{
    /// <summary>
    /// 按稀有度权重随机选取一个物品。
    /// 将数据库中的物品按稀有度分组，对每组施加对应权重后归一化抽取。
    /// </summary>
    /// <param name="database">物品数据库</param>
    /// <param name="weights">稀有度→权重映射。未配置的稀有度权重为 0，不会出现在结果中。</param>
    /// <returns>随机选取的 ItemDetails，如果数据库为空或所有权重为 0 则返回 null</returns>
    public static ItemDetails PickRandomItem(ItemDataList_SO database, Dictionary<ItemRarity, float> weights)
    {
        if (database == null || database.itemDetailsList == null || database.itemDetailsList.Count == 0)
        {
            UnityEngine.Debug.LogWarning("[SupplyLootHelper] 物品数据库为空，无法生成物品。");
            return null;
        }

        // 先按稀有度分组，同时过滤 itemID=0 的无效物品
        var byRarity = new Dictionary<ItemRarity, List<ItemDetails>>();
        foreach (var item in database.itemDetailsList)
        {
            if (!byRarity.ContainsKey(item.itemRarity))
                byRarity[item.itemRarity] = new List<ItemDetails>();
            byRarity[item.itemRarity].Add(item);
        }

        // 计算每个稀有度级别的有效权重
        // 有效权重 = 配置权重 × 该稀有度的物品数量（物品多的稀有度被抽中的概率更高）
        var rarityPool = new List<(ItemRarity rarity, float effectiveWeight, List<ItemDetails> items)>();
        float totalWeight = 0f;

        foreach (var kvp in byRarity)
        {
            float w = weights.TryGetValue(kvp.Key, out float configured) ? configured : 0f;
            if (w <= 0f || kvp.Value.Count == 0) continue;

            float effective = w * kvp.Value.Count;
            rarityPool.Add((kvp.Key, effective, kvp.Value));
            totalWeight += effective;
        }

        if (rarityPool.Count == 0 || totalWeight <= 0f)
        {
            UnityEngine.Debug.LogWarning("[SupplyLootHelper] 所有稀有度权重均为 0 或无匹配物品。");
            return null;
        }

        // 第一步：按稀有度权重抽稀有度
        float roll = Random.Range(0f, totalWeight);
        float accumulated = 0f;
        List<ItemDetails> selectedPool = null;

        foreach (var entry in rarityPool)
        {
            accumulated += entry.effectiveWeight;
            if (roll <= accumulated)
            {
                selectedPool = entry.items;
                break;
            }
        }

        // 兜底：取最后一个
        if (selectedPool == null || selectedPool.Count == 0)
            selectedPool = rarityPool[rarityPool.Count - 1].items;

        // 第二步：在选中稀有度内等概率抽具体物品
        int index = Random.Range(0, selectedPool.Count);
        return selectedPool[index];
    }

    /// <summary>
    /// 便捷方法：使用默认权重（Common=100, Uncommon=50, Rare=20, Epic=5, Legendary=1, Artifact=0）
    /// </summary>
    public static ItemDetails PickRandomItemDefault(ItemDataList_SO database)
    {
        var weights = new Dictionary<ItemRarity, float>
        {
            { ItemRarity.Common, 100f },
            { ItemRarity.Uncommon, 50f },
            { ItemRarity.Rare, 20f },
            { ItemRarity.Epic, 5f },
            { ItemRarity.Legendary, 1f },
            { ItemRarity.Artifact, 0f },
        };
        return PickRandomItem(database, weights);
    }
}
