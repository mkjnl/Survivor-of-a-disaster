using UnityEngine;

public class ObjectHighlighter : MonoBehaviour
{
    public float maxDistance = 5f;
    public LayerMask targetLayer;

    private GameObject selectedObj;

    void Update()
    {
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, targetLayer);

        foreach (RaycastHit hit in hits)
        {
            Outlines outline = hit.collider.GetComponent<Outlines>();
            if (outline != null)
            {
                if (hit.collider.gameObject != selectedObj)
                {
                    ClearOutline();
                    selectedObj = hit.collider.gameObject;
                    outline.enabled = true;

                    InteractiveObjectBase interactable = hit.collider.GetComponent<InteractiveObjectBase>();
                    if (interactable != null)
                    {
                        interactable.OnSelected();
                    }


                }
                return;
            }
        }

        // 所有物体都没挂 Outlines，清空选中
        ClearOutline();
        selectedObj = null;

    }
    void ClearOutline()
    {
        if (selectedObj != null)
        {
            selectedObj.GetComponent<Outlines>().enabled = false;
            InteractiveObjectBase interactable = selectedObj.GetComponent<InteractiveObjectBase>();
            if (interactable != null)
            {
                interactable.OnDeselected();
            }
        }
    }
}