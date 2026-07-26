/*
 * Copyright 2026 Cholopol
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Cholopol.TIS.MVVM;
using Cholopol.TIS.MVVM.ViewModels;
using Cholopol.TIS.MVVM.Views;
using Cholopol.TIS.Events;
using Loxodon.Framework.Contexts;
using StarterAssets;

namespace Cholopol.TIS
{
    public class InventoryManager : Singleton<InventoryManager>
    {
        [Header("Current Tetris Item Grid")]
        public TetrisGridVM selectedTetrisItemGridVM;
        public TetrisGridView selectedTetrisItemGridView;
        [Header("Tetris Item Details Data")]
        public ItemDataList_SO itemDataList_SO;
        [Header("Tetris Item Points Set Data")]
        public TetrisItemPointSet_SO tetrisItemPointSet_SO;
        [Header("Depository")]
        public TetrisGridVM depositoryGrid;
        public TetrisGridView depositoryGridView;
        [Header("Components")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private InventoryHighlight inventoryHighlight;
        [SerializeField] private TetrisItemGhostView tetrisItemGhost;
        [SerializeField] private RightClickMenuPanel rightClickMenuPanel;
        [Header("Placement/Highlight Config")]
        [SerializeField] private InventoryPlacementConfig_SO placementConfig;
        [Header("UI Toggle (Test)")]
        [SerializeField] private GameObject inventorySystemRoot;
        [SerializeField] private GameObject startPanel;
        [SerializeField]
        private StarterAssetsInputs starterAssetsInputs;

        [Header("World Container UI")]
        [Tooltip("Menu_Container 上的 ContainerPanel 组件，负责容器面板的显示/隐藏/全生命周期管理")]
        [SerializeField] private ContainerPanel containerPanel;

        public GameObject InventorySystemRoot => inventorySystemRoot;

        /// <summary>
        /// 物品栏 UI 是否已打开。
        /// 其他系统通过此属性判断是否应暂停角色输入。
        /// </summary>
        public bool IsInventoryOpen => inventorySystemRoot != null && inventorySystemRoot.activeSelf;

        /// <summary>
        /// 当前打开的世界容器。由 ContainerBase.OnInteract() 设置。
        /// </summary>
        public ContainerBase ActiveWorldContainer { get; set; }
        private bool _containerGridBound;
        private List<int> _pendingItems = new List<int>(); // 等待放入背包的物品

        public InventoryPlacementConfig_SO PlacementConfig => placementConfig;
        [Header("Focused TetrisItem Object")]
        public TetrisItemVM selectedItemVM;
        [SerializeField] private TetrisItemView selectedItemView;
        public Vector2Int tileGridOriginPosition;

        private void Update()
        {
            // B 键 开关背包（无容器交互）
            if (Keyboard.current.bKey.wasPressedThisFrame)
            {
                ToggleInventorySystem();
            }

            // R 键 旋转物品
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                RotateItemGhost();
            }

            var underMouse = GetGridViewUnderMouse();
            if (underMouse != selectedTetrisItemGridView)
            {
                selectedTetrisItemGridView = underMouse;
                selectedTetrisItemGridVM = selectedTetrisItemGridView != null ? selectedTetrisItemGridView.ViewModel : null;
            }

            HandleHighlight(selectedTetrisItemGridVM != null && selectedTetrisItemGridView != null);
        }

        public void ToggleInventorySystem()
        {
            if (inventorySystemRoot == null) return;

            bool willOpen = !inventorySystemRoot.activeSelf;

            // 控制玩家视角
            if (starterAssetsInputs != null)
            {
                // 打开背包时禁用鼠标视角
                starterAssetsInputs.cursorInputForLook = !willOpen;
            }

            // 打开背包 → 显示鼠标
            if (willOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            // 关闭背包 → 锁定鼠标
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // 关闭背包时回收 UI & 解绑容器网格
            if (!willOpen)
            {
                UnbindWorldContainerGrid();
                EventBus.Instance.Publish(EventNames.RecycleInventoryItemUI);
            }

            // 切换背包显示状态
            inventorySystemRoot.SetActive(willOpen);

            // 开始界面显示/隐藏
            if (startPanel != null)
            {
                startPanel.SetActive(!willOpen);
            }

            // 打开背包时绑定容器网格 & 生成 UI
            if (willOpen)
            {
                if (ActiveWorldContainer != null)
                    BindWorldContainerGrid(ActiveWorldContainer);

                // 打开世界容器时不触发全局物品 UI 恢复，
                // 避免 ClearAllItemViewsAndCaches 销毁容器网格的物品视图
                if (ActiveWorldContainer == null)
                {
                    EventBus.Instance.Publish(EventNames.InstantiateInventoryItemUI);
                    // 延迟等待 CTIS 协程创建 VM 后再放入暂存物品
                    StartCoroutine(DelayedFlushPendingItems());
                }
            }
        }

        /// <summary>
        /// Gets Tetris's point coordinates
        /// </summary>
        public List<Vector2Int> GetTetrisCoordinateSet(TetrisPieceShape shape)
        {
            return tetrisItemPointSet_SO.TetrisPieceShapeList[(int)shape].points;
        }

        private void RotateItemGhost()
        {
            if (tetrisItemGhost == null) tetrisItemGhost = UnityEngine.Object.FindObjectOfType<TetrisItemGhostView>(true);
            if (tetrisItemGhost == null || tetrisItemGhost.ViewModel == null) return;
            if (!tetrisItemGhost.ViewModel.OnDragging) return;
            if (tetrisItemGhost.ViewModel.ItemDetails == null) return;
            tetrisItemGhost.ViewModel.Rotate();
        }

        /// <summary>
        /// Gets the origin grid coordinates of TetrisItemGhost, and returns the mouse location grid coordinates if the item is not picked up
        /// </summary>
        public Vector2Int GetGhostTileGridOriginPosition()
        {
            if (selectedTetrisItemGridVM != null && selectedTetrisItemGridView != null)
            {
                var gridPos = (Vector2)selectedTetrisItemGridView.RectTransform.position;
                var scale = canvas != null ? canvas.scaleFactor : 1f;
                // 替换 Mouse.current.position.ReadValue() → 新输入系统鼠标坐标
                Vector2 mousePos = Mouse.current.position.ReadValue();
                var pos = selectedTetrisItemGridVM.GetTileGridPosition(gridPos, mousePos, scale);
                if (tetrisItemGhost == null) tetrisItemGhost = FindObjectOfType<TetrisItemGhostView>(true);
                if (tetrisItemGhost != null && tetrisItemGhost.ViewModel != null && tetrisItemGhost.ViewModel.ItemDetails != null)
                {
                    int offsetX = Mathf.FloorToInt((tetrisItemGhost.ViewModel.Width - 1) / 2);
                    int offsetY = Mathf.FloorToInt((tetrisItemGhost.ViewModel.Height - 1) / 2);
                    pos.x -= offsetX;
                    pos.y -= offsetY;
                }
                return pos;
            }
            return new Vector2Int();
        }

        private void HandleHighlight(bool isShow)
        {
            Vector2Int positionOnGrid = GetGhostTileGridOriginPosition();
            if (tetrisItemGhost == null) tetrisItemGhost = FindObjectOfType<TetrisItemGhostView>(true);
            if (inventoryHighlight == null) inventoryHighlight = FindObjectOfType<InventoryHighlight>(true);
            if (tetrisItemGhost != null && tetrisItemGhost.ViewModel != null && tetrisItemGhost.ViewModel.OnDragging && isShow && inventoryHighlight != null)
            {
                inventoryHighlight.Show(true);
                inventoryHighlight.UpdateShapeHighlightMVVM(
                    tetrisItemGhost.ViewModel.SelectedItem,
                    tetrisItemGhost.ViewModel,
                    positionOnGrid,
                    selectedTetrisItemGridVM,
                    selectedTetrisItemGridView);
                inventoryHighlight.SetParent(selectedTetrisItemGridView);
                inventoryHighlight.SetPosition(selectedTetrisItemGridVM, positionOnGrid.x, positionOnGrid.y);
            }
            else if (inventoryHighlight != null)
            {
                inventoryHighlight.Show(false);
            }
        }

        private TetrisGridView GetGridViewUnderMouse()
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            // UI 射线检测改用新输入鼠标坐标
            eventData.position = Mouse.current.position.ReadValue();

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            for (int i = 0; i < results.Count; i++)
            {
                var go = results[i].gameObject;
                var view = go.GetComponent<TetrisGridView>();
                if (view != null) return view;
            }
            return null;
        }

        // ====================== 世界容器网格管理 ======================

        /// <summary>
        /// 显示容器面板，委托给 ContainerPanel。
        /// </summary>
        private void BindWorldContainerGrid(ContainerBase container)
        {
            if (container == null) return;

            // 自动查找或创建 ContainerPanel
            if (containerPanel == null)
            {
                containerPanel = FindObjectOfType<ContainerPanel>(true);

                // 场景中不存在则自动创建完整的 Menu_Container 层级
                if (containerPanel == null && inventorySystemRoot != null)
                {
                    containerPanel = ContainerPanel.CreateDefaultHierarchy(inventorySystemRoot.transform);
                    UnityEngine.Debug.Log("[InventoryManager] 自动创建 Menu_Container 层级");
                }
            }

            if (containerPanel != null)
            {
                containerPanel.Show(container);
                _containerGridBound = true;
            }
            else
            {
                UnityEngine.Debug.LogWarning("[InventoryManager] 无法创建 ContainerPanel");
            }
        }

        /// <summary>
        /// 隐藏容器面板，委托给 ContainerPanel。
        /// </summary>
        private void UnbindWorldContainerGrid()
        {
            if (!_containerGridBound) return;

            if (containerPanel != null)
                containerPanel.Hide();

            ActiveWorldContainer = null;
            _containerGridBound = false;
        }

        /// <summary>
        /// 获取当前可用的背包网格 VM（优先 depositoryGrid，其次 depositoryGridView.ViewModel）。
        /// </summary>
        private TetrisGridVM GetPlayerBagGridVM()
        {
            if (depositoryGrid != null) return depositoryGrid;
            if (depositoryGridView != null && depositoryGridView.ViewModel != null)
            {
                depositoryGrid = depositoryGridView.ViewModel;
                return depositoryGrid;
            }
            return null;
        }

        /// <summary>
        /// 将物品添加到玩家背包。如果背包网格未就绪则暂存到队列，等开背包后自动放入。
        /// 放置时优先 IInventoryService.PlaceOnGrid（DebugWindow 模式），回退 TryPlaceTetrisItem。
        /// </summary>
        public bool AddItemToPlayerBag(int itemID)
        {
            if (itemDataList_SO == null)
            {
                UnityEngine.Debug.LogError("[InventoryManager] itemDataList_SO 未配置！");
                return false;
            }

            ItemDetails details = itemDataList_SO.GetItemDetailsByID(itemID);
            if (details == null)
            {
                UnityEngine.Debug.LogError($"[InventoryManager] 找不到 itemID={itemID} 的物品！");
                return false;
            }

            var gridVM = GetPlayerBagGridVM();

            // 网格不可用 → 暂存，等开背包时 CTIS 创建 VM 后再放入
            if (gridVM == null)
            {
                _pendingItems.Add(itemID);
                UnityEngine.Debug.Log($"[InventoryManager] 网格未就绪，暂存 itemID={itemID}（队列 {_pendingItems.Count} 件），打开背包时放入");
                return true;
            }

            return TryPlaceItemOnGrid(details, itemID, gridVM);
        }

        /// <summary>
        /// 在指定网格上放置物品。返回是否成功。
        /// </summary>
        private bool TryPlaceItemOnGrid(ItemDetails details, int itemID, TetrisGridVM gridVM)
        {
            // 物品尺寸检查
            if (details.xWidth <= 0 || details.yHeight <= 0)
            {
                UnityEngine.Debug.LogWarning(
                    $"[InventoryManager] 物品 {details.localizedName.GetLocalizedString()} (ID:{itemID}) " +
                    $"尺寸为 {details.xWidth}×{details.yHeight}，在网格中将不可见！请在 ItemDataList_SO 中设置 xWidth/yHeight");
            }

            var itemVM = TetrisItemFactory.GetOrCreateVM(details, null, gridVM);
            if (itemVM == null)
            {
                UnityEngine.Debug.LogError("[InventoryManager] 创建物品 VM 失败！");
                return false;
            }

            bool placed = false;
            for (int row = 0; row < gridVM.GridSizeHeight && !placed; row++)
            {
                for (int col = 0; col < gridVM.GridSizeWidth && !placed; col++)
                {
                    if (!gridVM.IsAreaVacantForItem(itemVM, col, row))
                        continue;

                    var service = Context.GetApplicationContext().GetService<IInventoryService>();
                    if (service != null)
                        placed = service.PlaceOnGrid(itemVM, gridVM, new Vector2Int(col, row), null);
                    else
                        placed = gridVM.TryPlaceTetrisItem(itemVM, col, row);
                }
            }

            if (placed)
            {
                int used = gridVM.OwnerItemsDic?.Count ?? 0;
                int total = gridVM.GridSizeWidth * gridVM.GridSizeHeight;
                UnityEngine.Debug.Log(
                    $"[InventoryManager] + {details.localizedName.GetLocalizedString()} " +
                    $"(ID:{itemID} | {details.itemRarity} | {details.xWidth}×{details.yHeight}) " +
                    $"→ 背包 ({itemVM.LocalGridCoordinate}) [{used}/{total}]");
                return true;
            }

            // 放置失败（背包已满）— 物品留在地上
            UnityEngine.Debug.Log(
                $"[InventoryManager] 背包已满: {details.localizedName.GetLocalizedString()} " +
                $"(ID:{itemID} | {details.xWidth}×{details.yHeight})，留在地上");
            if (!string.IsNullOrEmpty(itemVM.Guid))
            {
                itemVM.Dispose();
                TetrisItemFactory.UnregisterVM(itemVM.Guid, true);
            }
            return false;
        }

        // ====================== 调试：OnGUI 网格可视化 ======================

        [Header("Debug")]
        [SerializeField] private bool _showDebugGrid = true;
        [SerializeField] private int _debugGridCellSize = 22;
        [SerializeField] private int _debugGridPadding = 8;

        private void OnGUI()
        {
            if (!_showDebugGrid) return;
            if (!IsInventoryOpen) return;

            // 获取背包网格 VM
            TetrisGridVM grid = depositoryGrid;
            if (grid == null && depositoryGridView != null && depositoryGridView.ViewModel != null)
                grid = depositoryGridView.ViewModel;
            if (grid == null)
            {
                GUI.Label(new Rect(10, 10, 400, 30), "[DEBUG GRID] 没有可用的背包网格 VM");
                return;
            }

            int w = grid.GridSizeWidth;
            int h = grid.GridSizeHeight;
            var cells = grid.TetrisItemOccupiedCells;
            var items = grid.OwnerItemsDic;

            int cellSize = _debugGridCellSize;
            int padding = _debugGridPadding;
            int panelW = w * cellSize + padding * 2;
            int panelH = h * cellSize + padding * 2 + 28;
            int startX = Screen.width - panelW - 20;
            int startY = 20;

            // 半透明背景
            GUI.Box(new Rect(startX, startY, panelW, panelH), "");
            GUI.Label(new Rect(startX + padding, startY + 4, panelW, 24),
                $"<b>背包网格 [{w}×{h}]  物品:{items?.Count ?? 0}</b>");

            int gridOriginX = startX + padding;
            int gridOriginY = startY + 28;

            if (cells == null)
            {
                GUI.Label(new Rect(gridOriginX, gridOriginY, panelW, 30),
                    "<color=red>TetrisItemOccupiedCells == null</color>");
                return;
            }

            // 存储每个 item 的颜色映射
            var colorMap = new Dictionary<string, Color>();
            var palette = new Color[] {
                new Color(0.3f, 0.7f, 0.3f),   // 绿
                new Color(0.3f, 0.5f, 0.9f),   // 蓝
                new Color(0.9f, 0.6f, 0.2f),   // 橙
                new Color(0.8f, 0.3f, 0.3f),   // 红
                new Color(0.7f, 0.3f, 0.9f),   // 紫
                new Color(0.2f, 0.8f, 0.8f),   // 青
                new Color(0.9f, 0.8f, 0.2f),   // 黄
                new Color(0.5f, 0.5f, 0.5f),   // 灰
            };
            int paletteIdx = 0;

            // 先给每个 item 分配颜色
            if (items != null)
            {
                foreach (var kv in items)
                {
                    if (!colorMap.ContainsKey(kv.Key))
                        colorMap[kv.Key] = palette[paletteIdx++ % palette.Length];
                }
            }

            // 绘制格子
            var occupiedSet = new HashSet<string>(); // 已画过的 item（只画第一个 cell 的名字）
            for (int row = 0; row < h; row++)
            {
                for (int col = 0; col < w; col++)
                {
                    int cx = gridOriginX + col * cellSize;
                    int cy = gridOriginY + row * cellSize;
                    Rect cellRect = new Rect(cx, cy, cellSize, cellSize);

                    var item = cells[col, row]; // 注意：cells 索引是 [x, y]
                    if (item != null)
                    {
                        string guid = item.Guid;
                        if (!colorMap.ContainsKey(guid))
                            colorMap[guid] = palette[paletteIdx++ % palette.Length];

                        Color bg = colorMap[guid];
                        GUI.color = bg;
                        GUI.Box(cellRect, "");
                        GUI.color = Color.white;

                        // 只在该 item 的第一个 cell 上画名字
                        if (!occupiedSet.Contains(guid))
                        {
                            occupiedSet.Add(guid);
                            string label = item.ItemDetails != null
                                ? item.ItemDetails.itemID.ToString()
                                : "?";
                            GUI.Label(new Rect(cx, cy, cellSize * item.Width, 16),
                                $"<b><size=10>{label}</size></b>");
                        }
                    }
                    else
                    {
                        GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
                        GUI.Box(cellRect, "");
                        GUI.color = Color.white;
                    }

                    // 边框线
                    GUI.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                    GUI.Box(cellRect, "");
                    GUI.color = Color.white;
                }
            }

            // 右侧物品图例
            int legendX = gridOriginX + w * cellSize + 14;
            int legendY = gridOriginY;
            if (items != null && items.Count > 0)
            {
                GUI.Label(new Rect(legendX, legendY, 180, 20), "<b>物品列表:</b>");
                legendY += 18;
                foreach (var kv in items)
                {
                    var vm = kv.Value;
                    string name = vm.ItemDetails?.localizedName?.GetLocalizedString() ?? "?";
                    int id = vm.ItemDetails?.itemID ?? -1;
                    string dim = $"{vm.Width}×{vm.Height}";
                    var pos = vm.LocalGridCoordinate;

                    GUI.color = colorMap.ContainsKey(kv.Key) ? colorMap[kv.Key] : Color.gray;
                    GUI.Box(new Rect(legendX, legendY, 10, 14), "");
                    GUI.color = Color.white;
                    GUI.Label(new Rect(legendX + 14, legendY, 200, 16),
                        $"<size=10>ID:{id} {name} {dim} ({pos.x},{pos.y})</size>");
                    legendY += 16;
                }
            }

            // 待处理队列
            if (_pendingItems.Count > 0)
            {
                legendY += 6;
                GUI.Label(new Rect(legendX, legendY, 200, 20),
                    $"<color=yellow><b>暂存队列: {_pendingItems.Count}</b></color>");
                legendY += 18;
                foreach (var pid in _pendingItems)
                {
                    GUI.Label(new Rect(legendX, legendY, 200, 14),
                        $"<size=10>ID:{pid}</size>");
                    legendY += 14;
                }
            }
        }

        /// <summary>
        /// [ContextMenu] 将背包网格状态以 ASCII 形式输出到 Console。
        /// </summary>
        [ContextMenu("Debug: Dump Grid ASCII")]
        public void DumpGridASCII()
        {
            TetrisGridVM grid = depositoryGrid;
            if (grid == null && depositoryGridView != null && depositoryGridView.ViewModel != null)
                grid = depositoryGridView.ViewModel;

            if (grid == null)
            {
                UnityEngine.Debug.Log("[GridDump] grid VM is null — no grid available.");
                return;
            }

            int w = grid.GridSizeWidth;
            int h = grid.GridSizeHeight;
            var cells = grid.TetrisItemOccupiedCells;
            var items = grid.OwnerItemsDic;

            // 给每个 item 分配一个单字符 ID
            var idMap = new Dictionary<string, char>();
            char nextChar = 'A';
            if (items != null)
            {
                foreach (var kv in items)
                    idMap[kv.Key] = nextChar++;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"\n=== 背包网格 [{w}×{h}]  物品数:{items?.Count ?? 0} ===");
            sb.Append("  ");
            for (int col = 0; col < w; col++) sb.Append($"{col,-2}");
            sb.AppendLine();

            for (int row = 0; row < h; row++)
            {
                sb.Append($"{row} ");
                for (int col = 0; col < w; col++)
                {
                    var item = cells != null ? cells[col, row] : null;
                    if (item != null && idMap.ContainsKey(item.Guid))
                        sb.Append($"{idMap[item.Guid]}  ");
                    else
                        sb.Append(".  ");
                }
                sb.AppendLine();
            }
            // 图例
            if (items != null)
            {
                foreach (var kv in items)
                {
                    char c = idMap.ContainsKey(kv.Key) ? idMap[kv.Key] : '?';
                    sb.AppendLine($"  {c} = ID:{kv.Value.ItemDetails?.itemID} {kv.Value.ItemDetails?.localizedName?.GetLocalizedString()} {kv.Value.Width}×{kv.Value.Height} @({kv.Value.LocalGridCoordinate.x},{kv.Value.LocalGridCoordinate.y})");
                }
            }
            sb.AppendLine($"  待处理队列: {_pendingItems.Count} 件");
            UnityEngine.Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 延迟等待 CTIS 协程创建 VM 后再放入暂存物品。
        /// CTIS 的 InstantiateInventoryItemUI → LazyInstantiateReservedUICoroutine 是异步的，
        /// 需要等 BindReservedPersistentGrids 执行完 VM 才就绪。
        /// </summary>
        private IEnumerator DelayedFlushPendingItems()
        {
            // 等待 CTIS 协程完成（最多等 60 帧）
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                if (GetPlayerBagGridVM() != null) break;
            }
            FlushPendingItems();
        }

        /// <summary>将暂存的待放入物品写入背包网格（背包打开后调用）。</summary>
        private void FlushPendingItems()
        {
            if (_pendingItems.Count == 0) return;

            var gridVM = GetPlayerBagGridVM();
            if (gridVM == null)
            {
                UnityEngine.Debug.LogWarning($"[InventoryManager] FlushPendingItems: 网格仍不可用，{_pendingItems.Count} 件物品保留在队列中");
                return;
            }

            UnityEngine.Debug.Log($"[InventoryManager] 处理 {_pendingItems.Count} 件暂存物品...");
            var copy = new List<int>(_pendingItems);
            _pendingItems.Clear();

            foreach (var id in copy)
            {
                var details = itemDataList_SO.GetItemDetailsByID(id);
                if (details == null) continue;

                if (TryPlaceItemOnGrid(details, id, gridVM))
                {
                    UnityEngine.Debug.Log($"[InventoryManager] 暂存物品入背包: {details.localizedName.GetLocalizedString()} (ID={id})");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[InventoryManager] 暂存物品放置失败: {details.localizedName.GetLocalizedString()} (ID={id})");
                }
            }
        }
    }
}