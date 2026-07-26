using UnityEngine;
using UnityEngine.UI;
using Cholopol.TIS.MVVM.ViewModels;

/// <summary>
/// 全局物品悬浮提示 — 鼠标悬浮在容器物品上时显示详细信息。
/// 挂载在 Canvas 根节点上，全局复用。
/// </summary>
public class ItemTooltip : MonoBehaviour
{
    [Header("===== UI 组件 =====")]
    [SerializeField] private RectTransform _tooltipRect;
    [SerializeField] private Text _nameText;
    [SerializeField] private Image _rarityBar;
    [SerializeField] private Text _descText;

    [Header("===== 设置 =====")]
    [Tooltip("距鼠标的偏移（像素）")]
    [SerializeField] private Vector2 _offset = new Vector2(16, -16);

    [Tooltip("自动贴边距")]
    [SerializeField] private float _screenMargin = 10f;

    private RectTransform _canvasRect;
    private bool _isShowing;

    private void Awake()
    {
        if (_tooltipRect == null)
            _tooltipRect = GetComponent<RectTransform>();

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            _canvasRect = canvas.GetComponent<RectTransform>();

        // 初始隐藏
        gameObject.SetActive(false);
        _isShowing = false;
    }

    /// <summary>显示物品提示。</summary>
    public void Show(TetrisItemVM item, Vector2 screenPos)
    {
        if (item == null) return;

        gameObject.SetActive(true);
        _isShowing = true;

        // 物品名称
        if (_nameText != null)
        {
            _nameText.text = item.ItemName ?? item.ItemDetails?.localizedName?.GetLocalizedString() ?? "???";
        }

        // 稀有度色条
        if (_rarityBar != null && item.ItemDetails != null)
        {
            _rarityBar.color = item.RarityColor;
            _rarityBar.enabled = item.RarityColor.a > 0.01f;
        }

        // 描述
        if (_descText != null)
        {
            _descText.text = item.ItemDetails?.localizedDescription?.GetLocalizedString() ?? "";
        }

        // 定位到鼠标附近
        PositionAt(screenPos);
    }

    /// <summary>隐藏提示。</summary>
    public void Hide()
    {
        if (!_isShowing) return;
        _isShowing = false;
        gameObject.SetActive(false);
    }

    private void PositionAt(Vector2 screenPos)
    {
        if (_tooltipRect == null) return;

        // 将屏幕坐标转换为 Canvas 局部坐标
        if (_canvasRect != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, screenPos, null, out Vector2 localPos);

            localPos += _offset;

            // 自适应贴边
            float halfW = _tooltipRect.rect.width * 0.5f;
            float halfH = _tooltipRect.rect.height * 0.5f;

            if (_canvasRect != null)
            {
                float canvasW = _canvasRect.rect.width;
                float canvasH = _canvasRect.rect.height;

                // 左边界
                if (localPos.x - halfW < -canvasW * 0.5f + _screenMargin)
                    localPos.x = -canvasW * 0.5f + _screenMargin + halfW;
                // 右边界
                if (localPos.x + halfW > canvasW * 0.5f - _screenMargin)
                    localPos.x = canvasW * 0.5f - _screenMargin - halfW;
                // 上边界
                if (localPos.y + halfH > canvasH * 0.5f - _screenMargin)
                    localPos.y = canvasH * 0.5f - _screenMargin - halfH;
                // 下边界
                if (localPos.y - halfH < -canvasH * 0.5f + _screenMargin)
                    localPos.y = -canvasH * 0.5f + _screenMargin + halfH;
            }

            _tooltipRect.anchoredPosition = localPos;
        }
    }

    /// <summary>创建默认 ItemTooltip 层级（无预制体时自动生成）。</summary>
    public static ItemTooltip CreateDefault(Transform canvasParent)
    {
        var go = new GameObject("ItemTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvasParent, false);
        go.layer = canvasParent.gameObject.layer;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(200, 80);

        // 背景
        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.239f, 0.231f, 0.149f, 0.92f); // #3D3B26 92%

        var tooltip = go.AddComponent<ItemTooltip>();

        // 物品名称
        var nameGo = new GameObject("NameText", typeof(RectTransform), typeof(Text));
        nameGo.transform.SetParent(go.transform, false);
        nameGo.layer = go.layer;
        var nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0, 1);
        nameRt.anchorMax = new Vector2(1, 1);
        nameRt.pivot = new Vector2(0, 1);
        nameRt.anchoredPosition = new Vector2(8, -6);
        nameRt.sizeDelta = new Vector2(-16, 18);
        var nameText = nameGo.GetComponent<Text>();
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = 12;
        nameText.fontStyle = FontStyle.Bold;
        nameText.color = new Color(0.91f, 0.933f, 0.808f); // #E8EECE

        // 稀有度色条
        var rarityGo = new GameObject("RarityBar", typeof(RectTransform), typeof(Image));
        rarityGo.transform.SetParent(go.transform, false);
        rarityGo.layer = go.layer;
        var rarityRt = rarityGo.GetComponent<RectTransform>();
        rarityRt.anchorMin = new Vector2(0, 1);
        rarityRt.anchorMax = new Vector2(1, 1);
        rarityRt.pivot = new Vector2(0, 1);
        rarityRt.anchoredPosition = new Vector2(0, -24);
        rarityRt.sizeDelta = new Vector2(0, 2);

        // 描述
        var descGo = new GameObject("DescText", typeof(RectTransform), typeof(Text));
        descGo.transform.SetParent(go.transform, false);
        descGo.layer = go.layer;
        var descRt = descGo.GetComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0, 0);
        descRt.anchorMax = new Vector2(1, 1);
        descRt.offsetMin = new Vector2(8, 8);
        descRt.offsetMax = new Vector2(-8, -30);
        var descText = descGo.GetComponent<Text>();
        descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        descText.fontSize = 10;
        descText.color = new Color(0.91f, 0.933f, 0.808f, 0.7f); // #E8EECE 70%

        tooltip._tooltipRect = rt;
        tooltip._nameText = nameText;
        tooltip._rarityBar = rarityGo.GetComponent<Image>();
        tooltip._descText = descText;

        return tooltip;
    }
}
