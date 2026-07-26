using UnityEngine;

/// <summary>
/// 高亮器：始终运行射线检测 → 找到最近的 InteractiveObjectBase → 通知选中/取消。
/// 不直接操作描边组件（Outlines），描边由 InteractiveObjectBase 自己管理。
/// 瞄准和非瞄准状态下均生效。
/// </summary>
public class ObjectHighlighter : MonoBehaviour
{
    [Header("===== 检测设置 =====")]
    [Tooltip("最大检测距离")]
    public float maxDistance = 5f;

    [Tooltip("可交互物的层，设置为 Container")]
    public LayerMask targetLayer;

    private InteractiveObjectBase _selectedTarget;
    private Camera _mainCamera;

    // 用于 RaycastAll 结果排序，避免每帧分配
    private static System.Comparison<RaycastHit> _distanceComparison = (a, b) => a.distance.CompareTo(b.distance);

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        if (_mainCamera == null) return;

        Ray ray = new Ray(_mainCamera.transform.position, _mainCamera.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, targetLayer);

        // 按距离排序，确保选到最近的
        System.Array.Sort(hits, _distanceComparison);

        // 取第一个挂有 InteractiveObjectBase 的物体
        InteractiveObjectBase nearest = null;
        foreach (var hit in hits)
        {
            nearest = hit.collider.GetComponent<InteractiveObjectBase>();
            if (nearest != null) break;
        }

        // 只在选中目标变化时才通知
        if (nearest != _selectedTarget)
        {
            if (_selectedTarget != null)
                _selectedTarget.OnDeselected();

            _selectedTarget = nearest;

            if (_selectedTarget != null)
                _selectedTarget.OnSelected();
        }
    }

    /// <summary>
    /// 获取当前瞄准的可交互物体
    /// </summary>
    public GameObject GetSelectedObject()
    {
        return _selectedTarget != null ? _selectedTarget.gameObject : null;
    }
}
