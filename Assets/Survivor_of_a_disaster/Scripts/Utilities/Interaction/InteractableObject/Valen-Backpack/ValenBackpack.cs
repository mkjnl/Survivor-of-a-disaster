using UnityEngine;

/// <summary>
/// Valen 背包拾取物。
/// 拾取时将物品添加到玩家 CTIS 库存。携带实例数据时还原 Guid/堆叠等状态。
/// 背包满时物品留在地上。
/// </summary>
public class ValenBackpack : InteractiveObjectBase
{
    public override void OnInteract()
    {
        UnityEngine.Debug.Log($"[ValenBackpack] 开始拾取: {objectName} (pickUpItemID={pickUpItemID})");

        if (pickUpItemID >= 0)
        {
            var im = Cholopol.TIS.InventoryManager.Instance;
            if (im == null)
            {
                UnityEngine.Debug.LogError($"[ValenBackpack] InventoryManager.Instance 为 null！");
                ForcePickup("InventoryManager 不可用");
                return;
            }

            // 丢弃物携带实例数据 → 还原 Guid/堆叠等；否则创建全新实例
            bool success = discardData != null
                ? im.AddItemToPlayerBag(pickUpItemID, discardData)
                : im.AddItemToPlayerBag(pickUpItemID);

            if (success)
            {
                isUsed = true;
                Destroy(gameObject);
                UnityEngine.Debug.Log($"[ValenBackpack] ✓ 拾取成功: {objectName}");
            }
            else
            {
                // 背包满 → 物品留在地上，不标记 isUsed，不触发冷却
            }
            return;
        }

        // pickUpItemID 仍为 -1，说明 SupplyPoint 没有注入 itemID。
        UnityEngine.Debug.LogError($"[ValenBackpack] pickUpItemID=-1 (name={objectName})，SupplyPoint 未注入！强制清除");
        ForcePickup("pickUpItemID 缺失");
    }

    /// <summary>强制拾取：标记已用，让 SupplyPoint 清除模型。</summary>
    private void ForcePickup(string reason)
    {
        UnityEngine.Debug.LogWarning($"[ValenBackpack] 强制拾取: {objectName}，原因: {reason}");
        isUsed = true;
    }

    public override void OnSelected()
    {
        base.OnSelected();
    }

    public override void OnDeselected()
    {
        base.OnDeselected();
    }
}
