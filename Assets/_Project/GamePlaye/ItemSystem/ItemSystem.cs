using UnityEngine;
using System.Collections.Generic;

public class ItemSystem : MonoBehaviour
{


    [Header("生成设置")]
    public int spawnCount = 1;                // 生成数量

    [Header("位置偏移")]
    public Vector3 positionOffset = Vector3.zero;
    public float randomRadius = 0.3f;

    // 记录当前生成的所有物品（便于清理）
    private List<GameObject> spawnedItems = new List<GameObject>();

    public void Spawn(int id, ItemDataList_SO database)
    {
        if (database == null)
        {
            Debug.LogError("ItemDataList_SO 为空！");
            return;
        }

        ClearSpawnedItems();

        ItemDetails item = database.GetItemDetailsByID(id);
        if (item == null || item.itemEntity == null)
        {
            Debug.LogError($"找不到物品 ID: {id}");
            return;
        }

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 spawnPos = transform.position + positionOffset;
            if (randomRadius > 0)
            {
                Vector2 random2D = Random.insideUnitCircle * randomRadius;
                spawnPos.x += random2D.x;
                spawnPos.z += random2D.y;
            }

            Quaternion prefabRotation = item.itemEntity.transform.rotation;

            GameObject instance = Instantiate(item.itemEntity, spawnPos, prefabRotation);
            
            spawnedItems.Add(instance);
            Debug.Log($"生成物品 ID: {id} | 位置: {spawnPos}");
        }
    }

    [ContextMenu("清除所有生成的物品")]
    public void ClearSpawnedItems()
    {
        foreach (var item in spawnedItems)
        {
            if (item != null)
                Destroy(item);
        }

        spawnedItems.Clear();

        // 额外保险：清理可能残留的子物体
        foreach (Transform child in transform)
        {
            if (child != null)
                Destroy(child.gameObject);
        }
    }

    // 可选：当物体被销毁时自动清理
    private void OnDestroy()
    {
        ClearSpawnedItems();
    }
}