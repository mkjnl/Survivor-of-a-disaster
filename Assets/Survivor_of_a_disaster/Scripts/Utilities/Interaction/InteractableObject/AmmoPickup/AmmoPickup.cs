using UnityEngine;

/// <summary>
/// 弹药拾取物：762x39、9mm 等子弹的通用交互脚本。
/// 拾取时将物品添加到玩家 CTIS 库存。背包满时物品留在地上。
/// </summary>
public class AmmoPickup : InteractiveObjectBase
{
    public override void OnInteract()
    {
        UnityEngine.Debug.Log($"[AmmoPickup] 开始拾取: {objectName} (pickUpItemID={pickUpItemID})");

        if (pickUpItemID >= 0)
        {
            var im = Cholopol.TIS.InventoryManager.Instance;
            if (im == null)
            {
                UnityEngine.Debug.LogError($"[AmmoPickup] InventoryManager.Instance 为 null！");
                ForcePickup("InventoryManager 不可用");
                return;
            }

            if (im.AddItemToPlayerBag(pickUpItemID))
            {
                isUsed = true;
                UnityEngine.Debug.Log($"[AmmoPickup] ✓ 拾取成功: {objectName}");
            }
            else
            {
                // 背包满 → 物品留在地上，不标记 isUsed，不触发冷却
            }
            return;
        }

        UnityEngine.Debug.LogError($"[AmmoPickup] pickUpItemID=-1 (name={objectName})，SupplyPoint 未注入！强制清除");
        ForcePickup("pickUpItemID 缺失");
    }

    private void ForcePickup(string reason)
    {
        UnityEngine.Debug.LogWarning($"[AmmoPickup] 强制拾取: {objectName}，原因: {reason}");
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
