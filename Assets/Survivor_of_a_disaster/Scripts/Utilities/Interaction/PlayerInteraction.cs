using UnityEngine;
using UnityEngine.InputSystem;
using Cholopol.TIS;

public class PlayerInteraction : MonoBehaviour
{
    [Header("检测设置")]
    public float sphereRadius = 3f;

    [Tooltip("选择 Container 层")]
    public LayerMask containerLayer;

    private bool isContainerNearby = false;

    public bool IsContainerNearby => isContainerNearby;

    void Update()
    {
        isContainerNearby = DetectNearbyContainers();
    }
    /// <summary>
    /// 检测附近是否有可交互的容器物体
    /// </summary>
    /// <returns></returns>
    private bool DetectNearbyContainers()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, sphereRadius, containerLayer);

        if (colliders.Length > 0)
        {
            // Debug.Log($"检测到 {colliders.Length} 个容器在附近");
            return true;
        }
        return false;
    }

    // ====================== 输入交互 ======================
    /// <summary>
    /// 处理交互输入，尝试与最近的可交互物体进行交互
    /// </summary>
    /// <param name="value"></param>
    public void OnInteract(InputValue value)
    {
        if (!value.isPressed) return;                    // 只在按下时触发

        if (InventoryManager.Instance.InventorySystemRoot.activeSelf)
        {
            InventoryManager.Instance.ToggleInventorySystem();
            return;
        }

        
        if (!isContainerNearby)
        {
            Debug.Log("附近没有可交互物体！");
            return;
        }

        // 核心逻辑：找到最近的可交互物体并执行交互
        InteractiveObjectBase target = GetNearestInteractable();

        if (target != null)
        {
            if (!target.isUsed)
            {
                target.OnInteract();
                InventoryManager.Instance.ToggleInventorySystem();
                Debug.Log("打开UI");
            }
            else
            {
                Debug.Log($"{target.objectName} 已经被使用过了");
            }
        }
        else
        {
            Debug.Log("未能找到可交互物体");
        }
    }

    // ====================== 辅助方法 ======================
    /// <summary>
    /// 寻找最近的可交互物体，返回其 InteractiveObjectBase 组件
    /// </summary>
    /// <returns></returns>
    private InteractiveObjectBase GetNearestInteractable()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, sphereRadius, containerLayer);
        if (colliders.Length == 0)
            return null;

        InteractiveObjectBase nearest = null;
        float minDistance = float.MaxValue;

        foreach (var col in colliders)
        {
            // 尝试获取任何继承自 InteractiveObjectBase 的组件
            InteractiveObjectBase interactable = col.GetComponent<InteractiveObjectBase>();
            if (interactable != null)
            {
                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = interactable;
                }
            }
            else
            {
                Debug.Log($"碰撞体 {col.gameObject.name} 上没有找到 InteractiveObjectBase 子类组件");
            }
        }

        return nearest;
    }
}