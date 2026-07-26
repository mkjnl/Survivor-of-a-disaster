using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cholopol.TIS.MVVM.ViewModels;

/// <summary>
/// 容器面板总控 — 管理 Menu_Container 的完整生命周期。
/// 自动查找子对象绑定，零手动配置。
/// 实现 IContainerItemProvider，委托给 ContainerGridView。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ContainerPanel : MonoBehaviour, IContainerItemProvider
{
    [Header("===== 自动绑定（按名称查找，无需手动拖拽） =====")]
    [Tooltip("容器名称 Text")]
    [SerializeField] private Text _titleText;

    [Tooltip("容量 Text（如 \"5/25 格\"）")]
    [SerializeField] private Text _capacityText;

    [Tooltip("关闭按钮")]
    [SerializeField] private Button _closeButton;

    [Tooltip("网格视图（子对象上的 ContainerGridView）")]
    [SerializeField] private ContainerGridView _gridView;

    [Header("===== 物品信息面板 =====")]
    [Tooltip("信息面板根节点")]
    [SerializeField] private GameObject _infoPanel;

    [Tooltip("物品大图标")]
    [SerializeField] private Image _infoIcon;

    [Tooltip("物品名称")]
    [SerializeField] private Text _infoName;

    [Tooltip("物品描述")]
    [SerializeField] private Text _infoDesc;

    [Tooltip("物品属性")]
    [SerializeField] private Text _infoStats;

    [Header("===== 面板设置 =====")]
    [Tooltip("面板背景 Image")]
    [SerializeField] private Image _panelBackground;

    [Tooltip("面板显示/隐藏动画时长（秒）")]
    [SerializeField] private float _animDuration = 0.2f;

    [Tooltip("玩家背包面板（打开容器时隐藏）")]
    [SerializeField] private GameObject _playerBagPanel;

    // ====================== IContainerItemProvider ======================
    public event Action<TetrisItemVM> OnItemChanged;

    // ====================== 运行时状态 ======================
    private ContainerBase _activeContainer;
    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private bool _isShowing;
    private ItemTooltip _itemTooltip;

    public ContainerBase ActiveContainer => _activeContainer;
    public ContainerGridView GridView => _gridView;
    public TetrisGridVM GridVM => _gridView?.GridVM;

    // ====================== IContainerItemProvider 实现（委托给 GridView） ======================

    public bool TryPlaceItem(TetrisItemVM item, int posX, int posY)
    {
        bool ok = _gridView != null && _gridView.TryPlaceItem(item, posX, posY);
        if (ok)
        {
            UpdateCapacityText();
            OnItemChanged?.Invoke(item);
        }
        return ok;
    }

    public bool TryRemoveItem(TetrisItemVM item)
    {
        bool ok = _gridView != null && _gridView.TryRemoveItem(item);
        if (ok)
        {
            UpdateCapacityText();
            OnItemChanged?.Invoke(item);
        }
        return ok;
    }

    public bool CanPlaceAt(TetrisItemVM item, int posX, int posY)
    {
        return _gridView != null && _gridView.CanPlaceAt(item, posX, posY);
    }

    public TetrisGridVM GetGridVM() => _gridView?.GridVM;

    public IEnumerable<TetrisItemVM> GetAllItems()
    {
        return _gridView?.GetAllItems() ?? Array.Empty<TetrisItemVM>();
    }

    // ====================== 生命周期 ======================

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _rectTransform = GetComponent<RectTransform>();

        // 自动查找绑定
        AutoBind();

        _isShowing = false;

        // 关闭按钮（持久化注册，不受 Enable/Disable 影响）
        if (_closeButton != null)
            _closeButton.onClick.AddListener(Hide);
    }

    /// <summary>
    /// 自动按名称查找子对象，零手动配置。
    /// 如果某个字段未在 Inspector 中设置，则通过 transform.Find() 查找。
    /// </summary>
    private void AutoBind()
    {
        if (_panelBackground == null)
            _panelBackground = GetComponent<Image>();

        if (_gridView == null)
            _gridView = GetComponentInChildren<ContainerGridView>(true);

        // 标题栏
        if (_titleText == null)
            _titleText = transform.Find("TitleBar/ContainerNameText")?.GetComponent<Text>();

        if (_capacityText == null)
            _capacityText = transform.Find("TitleBar/CapacityText")?.GetComponent<Text>();

        if (_closeButton == null)
            _closeButton = transform.Find("TitleBar/CloseButton")?.GetComponent<Button>();

        // 信息面板
        if (_infoPanel == null)
        {
            var ip = transform.Find("InfoPanel");
            if (ip != null) _infoPanel = ip.gameObject;
        }

        if (_infoIcon == null)
            _infoIcon = transform.Find("InfoPanel/ItemIcon")?.GetComponent<Image>();

        if (_infoName == null)
            _infoName = transform.Find("InfoPanel/ItemNameText")?.GetComponent<Text>();

        if (_infoDesc == null)
            _infoDesc = transform.Find("InfoPanel/ItemDescText")?.GetComponent<Text>();

        if (_infoStats == null)
            _infoStats = transform.Find("InfoPanel/ItemStatsText")?.GetComponent<Text>();

        // 面板背景色
        if (_panelBackground != null && _panelBackground.sprite == null)
            _panelBackground.color = new Color(0.353f, 0.345f, 0.224f, 0.85f); // #5A5839 85%

        // 自动查找玩家背包面板（同级 GameObject）
        if (_playerBagPanel == null && transform.parent != null)
        {
            var bag = transform.parent.Find("Menu_PlayerBag");
            if (bag != null) _playerBagPanel = bag.gameObject;
        }
    }

    private void OnEnable()
    {
        // 确保有 ItemTooltip（全局悬浮提示）
        EnsureItemTooltip();

        // 订阅 GridView 事件
        TrySubscribeGridViewEvents();
    }

    private void OnDisable()
    {
        TryUnsubscribeGridViewEvents();

        if (_itemTooltip != null)
            _itemTooltip.Hide();
    }

    private void TrySubscribeGridViewEvents()
    {
        if (_gridView == null) return;
        TryUnsubscribeGridViewEvents(); // 防止重复订阅
        _gridView.OnItemClicked += OnGridItemClicked;
        _gridView.OnItemHoverEnter += OnGridItemHoverEnter;
        _gridView.OnItemHoverExit += OnGridItemHoverExit;
    }

    private void TryUnsubscribeGridViewEvents()
    {
        if (_gridView == null) return;
        _gridView.OnItemClicked -= OnGridItemClicked;
        _gridView.OnItemHoverEnter -= OnGridItemHoverEnter;
        _gridView.OnItemHoverExit -= OnGridItemHoverExit;
    }

    // ====================== 公开 API ======================

    /// <summary>显示容器面板。</summary>
    public void Show(ContainerBase container)
    {
        if (container == null) return;
        if (_isShowing && _activeContainer == container) return;

        _activeContainer = container;

        // 首次激活前确保初始状态
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        _isShowing = true;

        // 更新标题
        if (_titleText != null)
            _titleText.text = container.containerDisplayName ?? container.objectName ?? "容器";

        // 构建网格
        if (_gridView != null)
            _gridView.BuildGrid(container.gridWidth, container.gridHeight, container.containerId);

        // 更新容量
        UpdateCapacityText();

        // 隐藏信息面板
        if (_infoPanel != null)
            _infoPanel.SetActive(false);

        // 隐藏玩家背包
        if (_playerBagPanel != null)
            _playerBagPanel.SetActive(false);

        // 淡入动画
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            StartCoroutine(FadeIn());
        }

        UnityEngine.Debug.Log($"[ContainerPanel] 显示容器: {container.containerDisplayName} ({container.gridWidth}×{container.gridHeight})");
    }

    /// <summary>隐藏容器面板。</summary>
    public void Hide()
    {
        if (!_isShowing) return;

        _isShowing = false;

        // 销毁网格
        if (_gridView != null)
            _gridView.DestroyGrid();

        // 隐藏信息面板
        if (_infoPanel != null)
            _infoPanel.SetActive(false);

        // 恢复玩家背包
        if (_playerBagPanel != null)
            _playerBagPanel.SetActive(true);

        _activeContainer = null;
        gameObject.SetActive(false);

        UnityEngine.Debug.Log("[ContainerPanel] 容器面板已隐藏");
    }

    /// <summary>更新信息面板显示的物品信息。</summary>
    public void UpdateItemInfo(TetrisItemVM item)
    {
        if (_infoPanel != null)
            _infoPanel.SetActive(item != null);

        if (item == null) return;

        if (_infoIcon != null)
        {
            _infoIcon.sprite = item.ItemDetails?.itemIcon;
            _infoIcon.enabled = item.ItemDetails?.itemIcon != null;
        }

        if (_infoName != null)
            _infoName.text = item.ItemName ?? item.ItemDetails?.localizedName?.GetLocalizedString() ?? "未知物品";

        if (_infoDesc != null)
            _infoDesc.text = item.ItemDetails?.localizedDescription?.GetLocalizedString() ?? "";

        if (_infoStats != null)
        {
            var details = item.ItemDetails;
            if (details != null)
            {
                var parts = new System.Text.StringBuilder();
                if (details.weight > 0) parts.Append($"重量: {details.weight:F1}kg");
                if (details.itemDamage > 0) parts.Append($" | 伤害: {details.itemDamage}");
                _infoStats.text = parts.ToString();
            }
            else
            {
                _infoStats.text = "";
            }
        }
    }

    // ====================== 内部方法 ======================

    private void UpdateCapacityText()
    {
        if (_capacityText == null || _gridView == null) return;

        int used = _gridView.GridVM?.OwnerItemsDic?.Count ?? 0;
        int total = (_gridView.GridWidth * _gridView.GridHeight);
        _capacityText.text = $"{used}/{total} 格";
    }

    private void OnGridItemClicked(TetrisItemVM item)
    {
        UpdateItemInfo(item);
    }

    private void OnGridItemHoverEnter(TetrisItemVM item)
    {
        if (_itemTooltip != null && item != null)
            _itemTooltip.Show(item, Input.mousePosition);
    }

    private void OnGridItemHoverExit(TetrisItemVM item)
    {
        if (_itemTooltip != null)
            _itemTooltip.Hide();
    }

    /// <summary>确保场景中有 ItemTooltip，没有则自动创建。</summary>
    private void EnsureItemTooltip()
    {
        if (_itemTooltip != null) return;

        _itemTooltip = FindObjectOfType<ItemTooltip>(true);
        if (_itemTooltip == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                _itemTooltip = ItemTooltip.CreateDefault(canvas.transform);
        }
    }

    private System.Collections.IEnumerator FadeIn()
    {
        float elapsed = 0f;
        while (elapsed < _animDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / _animDuration);
            yield return null;
        }
        _canvasGroup.alpha = 1f;
    }

    /// <summary>
    /// 在 Scene 中自动创建 Menu_Container 完整 UI 层级（如果不存在）。
    /// 由 ContainerPanel.SetupDefaultHierarchy() 静态方法调用。
    /// </summary>
    public static ContainerPanel CreateDefaultHierarchy(Transform parent)
    {
        // 根节点
        var rootGo = new GameObject("Menu_Container", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        rootGo.transform.SetParent(parent, false);
        rootGo.layer = parent.gameObject.layer;
        rootGo.SetActive(false);

        var rootRt = rootGo.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = Vector2.zero;
        rootRt.sizeDelta = new Vector2(600, 500);

        var panelBg = rootGo.GetComponent<Image>();
        panelBg.color = new Color(0.353f, 0.345f, 0.224f, 0.85f); // #5A5839 85%

        var panel = rootGo.AddComponent<ContainerPanel>();

        // === TitleBar ===
        var titleBar = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
        titleBar.transform.SetParent(rootGo.transform, false);
        titleBar.layer = rootGo.layer;
        var titleRt = titleBar.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 1);
        titleRt.anchorMax = new Vector2(1, 1);
        titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.anchoredPosition = Vector2.zero;
        titleRt.sizeDelta = new Vector2(0, 36);
        titleBar.GetComponent<Image>().color = new Color(0.239f, 0.231f, 0.149f, 1f); // #3D3B26

        // 容器名称
        var nameGo = new GameObject("ContainerNameText", typeof(RectTransform), typeof(Text));
        nameGo.transform.SetParent(titleBar.transform, false);
        nameGo.layer = rootGo.layer;
        var nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0, 0.5f);
        nameRt.anchorMax = new Vector2(0.7f, 0.5f);
        nameRt.pivot = new Vector2(0, 0.5f);
        nameRt.anchoredPosition = new Vector2(12, 0);
        nameRt.sizeDelta = new Vector2(0, 24);
        var nameText = nameGo.GetComponent<Text>();
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = 14;
        nameText.fontStyle = FontStyle.Bold;
        nameText.color = new Color(0.91f, 0.933f, 0.808f); // #E8EECE
        nameText.alignment = TextAnchor.MiddleLeft;

        // 容量文字
        var capGo = new GameObject("CapacityText", typeof(RectTransform), typeof(Text));
        capGo.transform.SetParent(titleBar.transform, false);
        capGo.layer = rootGo.layer;
        var capRt = capGo.GetComponent<RectTransform>();
        capRt.anchorMin = new Vector2(0.7f, 0.5f);
        capRt.anchorMax = new Vector2(0.88f, 0.5f);
        capRt.pivot = new Vector2(0, 0.5f);
        capRt.anchoredPosition = Vector2.zero;
        capRt.sizeDelta = new Vector2(60, 24);
        var capText = capGo.GetComponent<Text>();
        capText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        capText.fontSize = 11;
        capText.color = new Color(0.91f, 0.933f, 0.808f, 0.7f); // #E8EECE 70%
        capText.alignment = TextAnchor.MiddleLeft;

        // 关闭按钮
        var closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGo.transform.SetParent(titleBar.transform, false);
        closeGo.layer = rootGo.layer;
        var closeRt = closeGo.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1, 0.5f);
        closeRt.anchorMax = new Vector2(1, 0.5f);
        closeRt.pivot = new Vector2(1, 0.5f);
        closeRt.anchoredPosition = new Vector2(-8, 0);
        closeRt.sizeDelta = new Vector2(24, 24);
        closeGo.GetComponent<Image>().color = new Color(0.91f, 0.933f, 0.808f, 0.3f); // #E8EECE 30%
        var closeBtn = closeGo.GetComponent<Button>();

        // 关闭按钮 × 文字
        var xGo = new GameObject("XText", typeof(RectTransform), typeof(Text));
        xGo.transform.SetParent(closeGo.transform, false);
        xGo.layer = rootGo.layer;
        var xRt = xGo.GetComponent<RectTransform>();
        xRt.anchorMin = Vector2.zero;
        xRt.anchorMax = Vector2.one;
        xRt.sizeDelta = Vector2.zero;
        var xText = xGo.GetComponent<Text>();
        xText.text = "×";
        xText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        xText.fontSize = 16;
        xText.color = new Color(0.91f, 0.933f, 0.808f); // #E8EECE
        xText.alignment = TextAnchor.MiddleCenter;

        // === Scroll View (网格区域) ===
        var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(Mask));
        scrollGo.transform.SetParent(rootGo.transform, false);
        scrollGo.layer = rootGo.layer;
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0, 0.22f);
        scrollRt.anchorMax = new Vector2(1, 0.92f);
        scrollRt.offsetMin = new Vector2(10, 0);
        scrollRt.offsetMax = new Vector2(-10, -36);
        scrollGo.GetComponent<Image>().color = new Color(0.239f, 0.231f, 0.149f, 0.3f); // #3D3B26 30%
        var scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = true;

        // Viewport
        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        viewportGo.layer = rootGo.layer;
        var viewportRt = viewportGo.GetComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.sizeDelta = Vector2.zero;
        viewportGo.GetComponent<Image>().color = Color.clear;

        // GridContent (挂 ContainerGridView)
        var gridContentGo = new GameObject("GridContent", typeof(RectTransform));
        gridContentGo.transform.SetParent(viewportGo.transform, false);
        gridContentGo.layer = rootGo.layer;
        var gridContentRt = gridContentGo.GetComponent<RectTransform>();
        gridContentRt.anchorMin = new Vector2(0, 1);
        gridContentRt.anchorMax = new Vector2(0, 1);
        gridContentRt.pivot = new Vector2(0, 1);
        gridContentRt.anchoredPosition = Vector2.zero;
        gridContentRt.sizeDelta = new Vector2(100, 100); // 会在 BuildGrid 时动态调整

        var gridView = gridContentGo.AddComponent<ContainerGridView>();

        scrollRect.viewport = viewportRt;
        scrollRect.content = gridContentRt;

        // === InfoPanel ===
        var infoGo = new GameObject("InfoPanel", typeof(RectTransform), typeof(Image));
        infoGo.transform.SetParent(rootGo.transform, false);
        infoGo.layer = rootGo.layer;
        var infoRt = infoGo.GetComponent<RectTransform>();
        infoRt.anchorMin = new Vector2(0, 0);
        infoRt.anchorMax = new Vector2(1, 0.22f);
        infoRt.offsetMin = new Vector2(10, 4);
        infoRt.offsetMax = new Vector2(-10, 0);
        infoGo.GetComponent<Image>().color = new Color(0.239f, 0.231f, 0.149f, 0.5f); // #3D3B26 50%
        infoGo.SetActive(false);

        // 物品图标
        var iconGo = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(infoGo.transform, false);
        iconGo.layer = rootGo.layer;
        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0, 0.5f);
        iconRt.anchorMax = new Vector2(0, 0.5f);
        iconRt.pivot = new Vector2(0, 0.5f);
        iconRt.anchoredPosition = new Vector2(8, 0);
        iconRt.sizeDelta = new Vector2(48, 48);
        iconGo.GetComponent<Image>().preserveAspect = true;

        // 物品名称
        var iNameGo = new GameObject("ItemNameText", typeof(RectTransform), typeof(Text));
        iNameGo.transform.SetParent(infoGo.transform, false);
        iNameGo.layer = rootGo.layer;
        var iNameRt = iNameGo.GetComponent<RectTransform>();
        iNameRt.anchorMin = new Vector2(0, 1);
        iNameRt.anchorMax = new Vector2(1, 1);
        iNameRt.pivot = new Vector2(0, 1);
        iNameRt.anchoredPosition = new Vector2(64, -6);
        iNameRt.sizeDelta = new Vector2(-72, 18);
        var iNameText = iNameGo.GetComponent<Text>();
        iNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        iNameText.fontSize = 12;
        iNameText.fontStyle = FontStyle.Bold;
        iNameText.color = new Color(0.91f, 0.933f, 0.808f); // #E8EECE
        iNameText.alignment = TextAnchor.UpperLeft;

        // 物品描述
        var iDescGo = new GameObject("ItemDescText", typeof(RectTransform), typeof(Text));
        iDescGo.transform.SetParent(infoGo.transform, false);
        iDescGo.layer = rootGo.layer;
        var iDescRt = iDescGo.GetComponent<RectTransform>();
        iDescRt.anchorMin = new Vector2(0, 0.3f);
        iDescRt.anchorMax = new Vector2(1, 0.7f);
        iDescRt.anchoredPosition = new Vector2(64, 0);
        iDescRt.sizeDelta = new Vector2(-72, 0);
        var iDescText = iDescGo.GetComponent<Text>();
        iDescText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        iDescText.fontSize = 10;
        iDescText.color = new Color(0.91f, 0.933f, 0.808f, 0.7f); // #E8EECE 70%
        iDescText.alignment = TextAnchor.UpperLeft;

        // 物品属性
        var iStatsGo = new GameObject("ItemStatsText", typeof(RectTransform), typeof(Text));
        iStatsGo.transform.SetParent(infoGo.transform, false);
        iStatsGo.layer = rootGo.layer;
        var iStatsRt = iStatsGo.GetComponent<RectTransform>();
        iStatsRt.anchorMin = new Vector2(0, 0);
        iStatsRt.anchorMax = new Vector2(1, 0.3f);
        iStatsRt.anchoredPosition = new Vector2(64, 4);
        iStatsRt.sizeDelta = new Vector2(-72, 0);
        var iStatsText = iStatsGo.GetComponent<Text>();
        iStatsText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        iStatsText.fontSize = 10;
        iStatsText.color = new Color(0.91f, 0.933f, 0.808f, 0.6f); // #E8EECE 60%
        iStatsText.alignment = TextAnchor.LowerLeft;

        // 回填引用
        panel._titleText = nameText;
        panel._capacityText = capText;
        panel._closeButton = closeBtn;
        panel._gridView = gridView;
        panel._infoPanel = infoGo;
        panel._infoIcon = iconGo.GetComponent<Image>();
        panel._infoName = iNameText;
        panel._infoDesc = iDescText;
        panel._infoStats = iStatsText;
        panel._panelBackground = panelBg;

        return panel;
    }
}
