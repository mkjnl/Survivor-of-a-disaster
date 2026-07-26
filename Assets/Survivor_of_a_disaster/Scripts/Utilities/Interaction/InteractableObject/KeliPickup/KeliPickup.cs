using UnityEngine;

/// <summary>
/// 可收集物品拾取物（Keli 等收藏品）。
/// 拾取时将物品添加到玩家 CTIS 库存。背包满时物品留在地上。
/// </summary>
public class KeliPickup : InteractiveObjectBase
{
    public override void OnInteract()
    {
        UnityEngine.Debug.Log($"[KeliPickup] 开始拾取: {objectName} (pickUpItemID={pickUpItemID})");

        if (pickUpItemID >= 0)
        {
            var im = Cholopol.TIS.InventoryManager.Instance;
            if (im == null)
            {
                UnityEngine.Debug.LogError($"[KeliPickup] InventoryManager.Instance 为 null！");
                ForcePickup("InventoryManager 不可用");
                return;
            }

            if (im.AddItemToPlayerBag(pickUpItemID))
            {
                isUsed = true;
                UnityEngine.Debug.Log($"[KeliPickup] ✓ 拾取成功: {objectName}");
            }
            else
            {
                // 背包满 → 物品留在地上，不标记 isUsed，不触发冷却
            }
            return;
        }

        UnityEngine.Debug.LogError($"[KeliPickup] pickUpItemID=-1 (name={objectName})，SupplyPoint 未注入！强制清除");
        ForcePickup("pickUpItemID 缺失");
    }

    private void ForcePickup(string reason)
    {
        UnityEngine.Debug.LogWarning($"[KeliPickup] 强制拾取: {objectName}，原因: {reason}");
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
