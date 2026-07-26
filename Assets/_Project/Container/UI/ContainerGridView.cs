using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Cholopol.TIS;
using Cholopol.TIS.MVVM.ViewModels;
using Cholopol.TIS.MVVM.Views;

/// <summary>
/// 容器网格渲染核心 — 基于 CTIS 数据层（TetrisGridVM），使用原生 Canvas UI 渲染网格和物品。
/// 不依赖 CTIS 的 TetrisGridView / Loxodon 绑定来做 UI 渲染。
/// </summary>
public class ContainerGridView : MonoBehaviour, IPointerMoveHandler, IPointerClickHandler, IPointerExitHandler
{
    [Header("===== 网格配置 =====")]
    [Tooltip("每格像素大小")]
    [SerializeField] private float _cellSize = 48f;

    [Tooltip("空格子颜色")]
    [SerializeField] private Color _emptyColor = new Color(0.239f, 0.231f, 0.149f, 0.5f); // #3D3B26 50%

    [Tooltip("网格线颜色")]
    [SerializeField] private Color _gridLineColor = new Color(0.91f, 0.933f, 0.808f, 0.2f); // #E8EECE 20%

    [Tooltip("悬浮高亮色")]
    [SerializeField] private Color _hoverColor = new Color(0.91f, 0.933f, 0.808f, 0.25f); // #E8EECE 25%

    [Tooltip("物品覆盖层预制体（可选，为空则自动生成）")]
    [SerializeField] private GameObject _itemOverlayPrefab;

    [Header("===== 事件（供外部订阅） =====")]
    [System.NonSerialized] public Action<TetrisItemVM> OnItemClicked;
    [System.NonSerialized] public Action<TetrisItemVM> OnItemRightClicked;
    [System.NonSerialized] public Action<TetrisItemVM> OnItemHoverEnter;
    [System.NonSerialized] public Action<TetrisItemVM> OnItemHoverExit;
    [System.NonSerialized] public Action OnGridReady;
    [System.NonSerialized] public Action OnGridDestroyed;

    // ====================== 运行时状态 ======================
    private TetrisGridVM _gridVM;
    private TetrisGridView _hiddenGridView; // 仅用于 TetrisGridFactory 兼容注册
    private Image[,] _cellImages;
    private readonly Dictionary<string, ContainerItemOverlay> _itemOverlays = new();
    private ContainerItemOverlay _selectedOverlay;
    private ContainerItemOverlay _hoveredOverlay;
    private RectTransform _rectTransform;
    private Canvas _parentCanvas;

    public TetrisGridVM GridVM => _gridVM;
    public float CellSize => _cellSize;
    public int GridWidth => _gridVM?.GridSizeWidth ?? 0;
    public int GridHeight => _gridVM?.GridSizeHeight ?? 0;

    // ====================== IContainerItemProvider 兼容（通过 parent ContainerPanel 暴露） ======================

    public bool TryPlaceItem(TetrisItemVM item, int posX, int posY)
    {
        if (_gridVM == null || item == null) return false;
        bool ok = _gridVM.TryPlaceTetrisItem(item, posX, posY);
        if (ok) RefreshItems();
        return ok;
    }

    public bool TryRemoveItem(TetrisItemVM item)
    {
        if (_gridVM == null || item == null) return false;
        var coord = item.LocalGridCoordinate;
        _gridVM.RemoveTetrisItem(item, coord.x, coord.y, item.RotationOffset, item.TetrisCoordinateSet, true);
        RefreshItems();
        return true;
    }

    public bool CanPlaceAt(TetrisItemVM item, int posX, int posY)
    {
        if (_gridVM == null || item == null) return false;
        return _gridVM.IsAreaVacantForItem(item, posX, posY);
    }

    public IEnumerable<TetrisItemVM> GetAllItems()
    {
        if (_gridVM?.OwnerItemsDic == null) yield break;
        foreach (var kv in _gridVM.OwnerItemsDic)
            yield return kv.Value;
    }

    // ====================== 网格构建/销毁 ======================

    /// <summary>构建 gridWidth×gridHeight 的网格。</summary>
    public void BuildGrid(int width, int height, string containerGuid)
    {
        DestroyGrid();

        _rectTransform = GetComponent<RectTransform>();
        _parentCanvas = GetComponentInParent<Canvas>();

        // 1. 创建 TetrisGridVM（CTIS 数据层）
        _gridVM = new TetrisGridVM(width, height);
        _gridVM.GridGuid = containerGuid;
        _gridVM.PlaceItemViewRequested += OnPlaceItemViewRequested;
        _gridVM.RemoveItemViewRequested += OnRemoveItemViewRequested;

        // 2. 创建隐藏的 TetrisGridView 用于 TetrisGridFactory 兼容注册
        _hiddenGridView = CreateHiddenGridView(width, height);
        TetrisGridFactory.RegisterAssociation(_hiddenGridView, _gridVM);

        // 3. 触发绑定（ApplyConfig → PrimeFromCache 从缓存恢复物品）
        _hiddenGridView.ViewModel = _gridVM;

        // 4. 创建格子背景
        CreateCellImages(width, height);

        // 5. 刷新物品显示
        RefreshItems();

        OnGridReady?.Invoke();

        UnityEngine.Debug.Log($"[ContainerGridView] 网格已构建: {width}×{height}, GUID={containerGuid}, 物品数={_gridVM.OwnerItemsDic.Count}");
    }

    /// <summary>销毁网格，清理所有 UI 和 VM。</summary>
    public void DestroyGrid()
    {
        // 取消事件
        if (_gridVM != null)
        {
            _gridVM.PlaceItemViewRequested -= OnPlaceItemViewRequested;
            _gridVM.RemoveItemViewRequested -= OnRemoveItemViewRequested;
        }

        // 解绑 ViewModel
        if (_hiddenGridView != null)
            _hiddenGridView.ViewModel = null;

        // 清理工厂注册
        if (_gridVM != null && !string.IsNullOrEmpty(_gridVM.GridGuid))
        {
            TetrisGridFactory.UnregisterVM(_gridVM.GridGuid);
            TetrisGridFactory.UnregisterView(_gridVM.GridGuid);
        }

        // 销毁隐藏 GridView
        if (_hiddenGridView != null)
        {
            if (Application.isPlaying) Destroy(_hiddenGridView.gameObject);
            _hiddenGridView = null;
        }

        // 销毁格子
        ClearAllCells();
        ClearAllOverlays();

        // 取消选中
        _selectedOverlay = null;
        _hoveredOverlay = null;

        _gridVM = null;

        OnGridDestroyed?.Invoke();
        UnityEngine.Debug.Log("[ContainerGridView] 网格已销毁");
    }

    // ====================== 物品显示刷新 ======================

    /// <summary>从 OwnerItemsDic 刷新所有物品覆盖层。</summary>
    public void RefreshItems()
    {
        ClearAllOverlays();

        if (_gridVM?.OwnerItemsDic == null) return;

        foreach (var kv in _gridVM.OwnerItemsDic)
        {
            var item = kv.Value;
            if (item == null) continue;
            CreateItemOverlay(item);
        }
    }

    /// <summary>选中物品。</summary>
    public void SelectItem(ContainerItemOverlay overlay)
    {
        if (_selectedOverlay != null && _selectedOverlay != overlay)
            _selectedOverlay.SetSelected(false);

        _selectedOverlay = overlay;

        if (_selectedOverlay != null)
            _selectedOverlay.SetSelected(true);
    }

    // ====================== 鼠标交互 ======================

    public void OnPointerMove(PointerEventData eventData)
    {
        if (_gridVM == null || _cellImages == null) return;

        // 计算鼠标所在格子坐标
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform, eventData.position, _parentCanvas?.worldCamera, out Vector2 localPos);

        int cellX = Mathf.FloorToInt((localPos.x - _rectTransform.rect.xMin) / _cellSize);
        int cellY = Mathf.FloorToInt((_rectTransform.rect.yMax - localPos.y) / _cellSize);

        // 边界检查
        if (cellX < 0 || cellX >= GridWidth || cellY < 0 || cellY >= GridHeight)
        {
            ClearHover();
            return;
        }

        // 查找该格子的物品
        var itemVM = _gridVM.GetTetrisItemVM(cellX, cellY);
        ContainerItemOverlay overlay = null;
        if (itemVM != null)
            _itemOverlays.TryGetValue(itemVM.Guid, out overlay);

        if (overlay != _hoveredOverlay)
        {
            if (_hoveredOverlay != null)
            {
                _hoveredOverlay.SetHovered(false);
                OnItemHoverExit?.Invoke(_hoveredOverlay.ItemVM);
            }

            _hoveredOverlay = overlay;

            if (_hoveredOverlay != null)
            {
                _hoveredOverlay.SetHovered(true);
                OnItemHoverEnter?.Invoke(_hoveredOverlay.ItemVM);
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_gridVM == null) return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (_hoveredOverlay != null)
                OnItemRightClicked?.Invoke(_hoveredOverlay.ItemVM);
        }
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            SelectItem(_hoveredOverlay);
            if (_hoveredOverlay != null)
                OnItemClicked?.Invoke(_hoveredOverlay.ItemVM);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ClearHover();
    }

    private void ClearHover()
    {
        if (_hoveredOverlay != null)
        {
            _hoveredOverlay.SetHovered(false);
            OnItemHoverExit?.Invoke(_hoveredOverlay.ItemVM);
            _hoveredOverlay = null;
        }
    }

    // ====================== CTIS 事件回调 ======================

    private void OnPlaceItemViewRequested(TetrisItemVM item, int posX, int posY)
    {
        if (_itemOverlays.ContainsKey(item.Guid)) return;
        CreateItemOverlay(item);
    }

    private void OnRemoveItemViewRequested(TetrisItemVM item)
    {
        if (_itemOverlays.TryGetValue(item.Guid, out var overlay))
        {
            if (overlay != null && Application.isPlaying)
                Destroy(overlay.gameObject);
            _itemOverlays.Remove(item.Guid);
        }
    }

    // ====================== 内部 UI 构建 ======================

    private void CreateCellImages(int width, int height)
    {
        ClearAllCells();

        _cellImages = new Image[width, height];
        int cellSize = Mathf.RoundToInt(_cellSize);
        int totalW = width * cellSize;
        int totalH = height * cellSize;

        // 设置自身尺寸
        if (_rectTransform != null)
        {
            _rectTransform.sizeDelta = new Vector2(totalW, totalH);
        }

        // 创建每个格子的背景
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var go = new GameObject($"Cell_{x}_{y}", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                go.layer = gameObject.layer;

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(x * cellSize, -y * cellSize);
                rt.sizeDelta = new Vector2(cellSize, cellSize);

                var img = go.AddComponent<Image>();
                img.color = _emptyColor;
                img.raycastTarget = false;

                _cellImages[x, y] = img;
            }
        }

        // 创建网格线（用 LineRenderer 或者细条 Image）
        CreateGridLines(width, height, cellSize, totalW, totalH);
    }

    private void CreateGridLines(int width, int height, int cellSize, int totalW, int totalH)
    {
        // 垂直线
        for (int col = 0; col <= width; col++)
        {
            var go = new GameObject($"VLine_{col}", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(col * cellSize, 0);
            rt.sizeDelta = new Vector2(0.5f, totalH);

            var img = go.AddComponent<Image>();
            img.color = _gridLineColor;
            img.raycastTarget = false;
        }

        // 水平线
        for (int row = 0; row <= height; row++)
        {
            var go = new GameObject($"HLine_{row}", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(0, -row * cellSize);
            rt.sizeDelta = new Vector2(totalW, 0.5f);

            var img = go.AddComponent<Image>();
            img.color = _gridLineColor;
            img.raycastTarget = false;
        }
    }

    private void CreateItemOverlay(TetrisItemVM item)
    {
        ContainerItemOverlay overlay;

        if (_itemOverlayPrefab != null)
        {
            var go = Instantiate(_itemOverlayPrefab, transform);
            overlay = go.GetComponent<ContainerItemOverlay>();
            if (overlay == null)
                overlay = go.AddComponent<ContainerItemOverlay>();
        }
        else
        {
            overlay = ContainerItemOverlay.CreateDefault(transform);
        }

        overlay.Setup(item, _cellSize);
        _itemOverlays[item.Guid] = overlay;
    }

    /// <summary>创建隐藏的 TetrisGridView，仅用于兼容 TetrisGridFactory 的注册机制。</summary>
    private TetrisGridView CreateHiddenGridView(int w, int h)
    {
        var go = new GameObject("_HiddenGridView", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;
        go.hideFlags = HideFlags.HideAndDontSave;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        go.AddComponent<DataGUID>();
        var gv = go.AddComponent<TetrisGridView>();
        gv.SetGridDimensions(w, h);
        return gv;
    }

    private void ClearAllCells()
    {
        if (_cellImages != null)
        {
            for (int y = 0; y < _cellImages.GetLength(1); y++)
            {
                for (int x = 0; x < _cellImages.GetLength(0); x++)
                {
                    if (_cellImages[x, y] != null && Application.isPlaying)
                        Destroy(_cellImages[x, y].gameObject);
                }
            }
            _cellImages = null;
        }

        // 同时清理网格线（直接遍历子对象中所有非覆盖层的）
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name.StartsWith("Cell_") || child.name.StartsWith("VLine_") || child.name.StartsWith("HLine_"))
            {
                if (Application.isPlaying) Destroy(child.gameObject);
            }
        }
    }

    private void ClearAllOverlays()
    {
        foreach (var kv in _itemOverlays)
        {
            if (kv.Value != null && Application.isPlaying)
                Destroy(kv.Value.gameObject);
        }
        _itemOverlays.Clear();
    }

    private void OnDestroy()
    {
        DestroyGrid();
    }
}
