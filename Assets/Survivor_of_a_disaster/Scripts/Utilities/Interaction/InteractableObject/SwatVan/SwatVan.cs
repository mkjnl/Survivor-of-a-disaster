using UnityEngine;

public class SwatVan : InteractiveObjectBase
{
    /// <summary>
    /// 交互方法，子类必须实现
    /// 这里可以放置交互逻辑，比如打开容器、拾取物品、触发机关等
    /// 也可以在子类中添加额外的方法来处理特定的交互行为
    /// 例如：OpenContainer()、PickUpItem()、ActivateSwitch() 等
    /// </summary>
    public override void OnInteract()
    {
        Debug.Log($"打开了 {objectName}");
        isUsed = true;
    }

    public override void OnSelected()
    {
        if (isUsed == true)
        {
            Debug.Log($"{objectName}使用中");
        }
    }

    public override void OnDeselected()
    {
        Debug.Log($"{objectName} 被取消选中");
    }
}
