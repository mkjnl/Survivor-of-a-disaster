using UnityEngine;

/// <summary>
/// 旧保险箱容器——继承 ContainerBase，获得网格配置和 CTIS 关联能力。
/// OnInteract 由 ContainerBase 统一处理，此处只保留 OldSate 特有的选中反馈。
/// </summary>
public class OldSate : ContainerBase
{
    public override void OnSelected()
    {
        base.OnSelected();
        Debug.Log($"{objectName} 被选中");
    }

    public override void OnDeselected()
    {
        base.OnDeselected();
        Debug.Log($"{objectName} 被取消选中");
    }
}
