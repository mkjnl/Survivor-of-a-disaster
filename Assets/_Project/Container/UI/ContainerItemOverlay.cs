using UnityEngine;
using UnityEngine.UI;
using Cholopol.TIS.MVVM.ViewModels;

/// <summary>
/// 物品在容器网格上的可视化覆盖层，可跨越多格。
/// 由 ContainerGridView 创建和管理。
/// </summary>
public class ContainerItemOverlay : MonoBehaviour
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private Image _rarityBorder;
    [SerializeField] private Text _stackText;
    [SerializeField] private GameObject _hoverHighlight;
    [SerializeField] private GameObject _selectedHighlight;

    private TetrisItemVM _itemVM;
    private float _cellSize;
    private bool _isHovered;
    private bool _isSelected;

    public TetrisItemVM ItemVM => _itemVM;
    public Vector2Int GridPosition => _itemVM?.LocalGridCoordinate ?? Vector2Int.zero;
    public int GridWidth => _itemVM?.Width ?? 1;
    public int GridHeight => _itemVM?.Height ?? 1;

    /// <summary>初始化物品覆盖层。</summary>
    public void Setup(TetrisItemVM item, float cellSize)
    {
        _itemVM = item;
        _cellSize = cellSize;

        if (_iconImage != null && item.ItemDetails?.itemIcon != null)
        {
            _iconImage.sprite = item.ItemDetails.itemIcon;
            _iconImage.preserveAspect = true;
        }

        UpdateRarityBorder();
        UpdateStackText();
        UpdatePosition();

        gameObject.name = $"ItemOverlay_{item.Guid}";
    }

    /// <summary>刷新位置和尺寸（物品旋转后调用）。</summary>
    public void UpdatePosition()
    {
        if (_itemVM == null) return;

        var rt = transform as RectTransform;
        if (rt == null) return;

        int px = _itemVM.LocalGridCoordinate.x * Mathf.RoundToInt(_cellSize);
        int py = -_itemVM.LocalGridCoordinate.y * Mathf.RoundToInt(_cellSize);
        int itemW = _itemVM.Width * Mathf.RoundToInt(_cellSize);
        int itemH = _itemVM.Height * Mathf.RoundToInt(_cellSize);

        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(px, py);
        rt.sizeDelta = new Vector2(itemW, itemH);
    }

    public void SetHovered(bool hovered)
    {
        _isHovered = hovered;
        if (_hoverHighlight != null)
            _hoverHighlight.SetActive(hovered);
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        if (_selectedHighlight != null)
            _selectedHighlight.SetActive(selected);
    }

    private void UpdateRarityBorder()
    {
        if (_rarityBorder == null || _itemVM?.ItemDetails == null) return;
        _rarityBorder.color = _itemVM.RarityColor;
        _rarityBorder.enabled = _itemVM.RarityColor.a > 0.01f;
    }

    private void UpdateStackText()
    {
        if (_stackText == null || _itemVM == null) return;
        if (_itemVM.CurrentStack > 1)
        {
            _stackText.text = _itemVM.CurrentStack.ToString();
            _stackText.enabled = true;
        }
        else
        {
            _stackText.enabled = false;
        }
    }

    /// <summary>创建默认 UI 层级（无预制体时自动生成）。</summary>
    public static ContainerItemOverlay CreateDefault(Transform parent)
    {
        var go = new GameObject("ItemOverlay", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;

        var overlay = go.AddComponent<ContainerItemOverlay>();

        // 图标 Image（占满）
        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(go.transform, false);
        iconGo.layer = parent.gameObject.layer;
        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.sizeDelta = Vector2.zero;
        overlay._iconImage = iconGo.AddComponent<Image>();
        overlay._iconImage.raycastTarget = true;
        overlay._iconImage.preserveAspect = true;

        // 稀有度边框（底部 3px 色条）
        var rarityGo = new GameObject("RarityBorder", typeof(RectTransform));
        rarityGo.transform.SetParent(go.transform, false);
        rarityGo.layer = parent.gameObject.layer;
        var rarityRt = rarityGo.GetComponent<RectTransform>();
        rarityRt.anchorMin = new Vector2(0, 0);
        rarityRt.anchorMax = new Vector2(1, 0);
        rarityRt.pivot = new Vector2(0.5f, 0);
        rarityRt.anchoredPosition = Vector2.zero;
        rarityRt.sizeDelta = new Vector2(0, 3);
        overlay._rarityBorder = rarityGo.AddComponent<Image>();
        overlay._rarityBorder.raycastTarget = false;

        // 堆叠数量文字（右下角）
        var stackGo = new GameObject("StackText", typeof(RectTransform));
        stackGo.transform.SetParent(go.transform, false);
        stackGo.layer = parent.gameObject.layer;
        var stackRt = stackGo.GetComponent<RectTransform>();
        stackRt.anchorMin = new Vector2(1, 0);
        stackRt.anchorMax = new Vector2(1, 0);
        stackRt.pivot = new Vector2(1, 0);
        stackRt.anchoredPosition = new Vector2(-2, 2);
        stackRt.sizeDelta = new Vector2(40, 20);
        overlay._stackText = stackGo.AddComponent<Text>();
        overlay._stackText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        overlay._stackText.fontSize = 12;
        overlay._stackText.alignment = TextAnchor.LowerRight;
        overlay._stackText.color = new Color(0.91f, 0.933f, 0.808f); // #E8EECE
        overlay._stackText.raycastTarget = false;

        // 悬浮高亮（覆盖层）
        var hoverGo = new GameObject("HoverHighlight", typeof(RectTransform));
        hoverGo.transform.SetParent(go.transform, false);
        hoverGo.layer = parent.gameObject.layer;
        var hoverRt = hoverGo.GetComponent<RectTransform>();
        hoverRt.anchorMin = Vector2.zero;
        hoverRt.anchorMax = Vector2.one;
        hoverRt.sizeDelta = Vector2.zero;
        var hoverImg = hoverGo.AddComponent<Image>();
        hoverImg.color = new Color(0.91f, 0.933f, 0.808f, 0.25f); // #E8EECE 25%
        hoverImg.raycastTarget = false;
        overlay._hoverHighlight = hoverGo;
        hoverGo.SetActive(false);

        // 选中高亮
        var selGo = new GameObject("SelectedHighlight", typeof(RectTransform));
        selGo.transform.SetParent(go.transform, false);
        selGo.layer = parent.gameObject.layer;
        var selRt = selGo.GetComponent<RectTransform>();
        selRt.anchorMin = Vector2.zero;
        selRt.anchorMax = Vector2.one;
        selRt.sizeDelta = Vector2.zero;
        var selImg = selGo.AddComponent<Image>();
        selImg.color = new Color(0.91f, 0.933f, 0.808f, 0.15f); // #E8EECE 15%
        selImg.raycastTarget = false;
        overlay._selectedHighlight = selGo;
        selGo.SetActive(false);

        return overlay;
    }
}
