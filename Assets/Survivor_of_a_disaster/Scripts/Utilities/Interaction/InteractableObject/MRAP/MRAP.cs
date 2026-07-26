using UnityEngine;

/// <summary>
/// MRAP 装甲车交互脚本。
/// </summary>
public class MRAP : InteractiveObjectBase
{
    public override void OnInteract()
    {
        Debug.Log($"与 {objectName} 交互");
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
