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
using Cholopol.TIS.SaveLoadSystem;
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
        private struct PendingItem { public int itemID; public TetrisItemPersistentData data; }
        private List<PendingItem> _pendingItems = new();

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

            // Ctrl 键 丢弃鼠标悬停的物品
            if (Keyboard.current.leftCtrlKey.wasPressedThisFrame && IsInventoryOpen)
            {
                var itemUnderMouse = GetTetrisItemViewUnderMouse();
                if (itemUnderMouse != null && itemUnderMouse.ViewModel != null)
                {
                    DiscardItem(itemUnderMouse.ViewModel);
                }
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

            // 打开背包 → 彻底禁用角色输入组件，防止任何按键泄露到游戏操作
            // 关闭背包 → 恢复输入组件
            if (starterAssetsInputs != null)
            {
                if (willOpen)
                {
                    // 先清零所有输入状态，防止残留值在组件禁用期间被读取
                    starterAssetsInputs.move = Vector2.zero;
                    starterAssetsInputs.jump = false;
                    starterAssetsInputs.sprint = false;
                    starterAssetsInputs.aim = false;
                    starterAssetsInputs.leftclick = false;
                    starterAssetsInputs.cursorInputForLook = false;
                    starterAssetsInputs.enabled = false;
                }
                else
                {
                    starterAssetsInputs.cursorInputForLook = true;
                    starterAssetsInputs.enabled = true;
                }
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
            int index = (int)shape;
            if (tetrisItemPointSet_SO == null || tetrisItemPointSet_SO.TetrisPieceShapeList == null)
            {
                UnityEngine.Debug.LogError($"[InventoryManager] TetrisItemPointSet_SO 未配置！");
                return new List<Vector2Int>();
            }
            if (index < 0 || index >= tetrisItemPointSet_SO.TetrisPieceShapeList.Count)
            {
                var list = tetrisItemPointSet_SO.TetrisPieceShapeList;
                var existing = new System.Text.StringBuilder();
                var missing = new System.Text.StringBuilder();
                int total = System.Enum.GetValues(typeof(TetrisPieceShape)).Length;
                int have = 0, lack = 0;

                for (int i = 0; i < total; i++)
                {
                    var s = (TetrisPieceShape)i;
                    bool ok = i < list.Count && list[i] != null && list[i].points != null && list[i].points.Count > 0;
                    if (ok)
                    {
                        existing.Append($"  [{i}] {s} ({list[i]!.points!.Count} pts)\n");
                        have++;
                    }
                    else
                    {
                        missing.Append($"  [{i}] {s}\n");
                        lack++;
                    }
                }

                UnityEngine.Debug.LogError(
                    $"[InventoryManager] 形状 {shape} (index={index}) 不存在！\n" +
                    $"列表容量: {list.Count}/{total}，已定义: {have}，缺失: {lack}\n\n" +
                    $"=== 已定义 ===\n{existing}\n" +
                    $"=== 缺失 ===\n{missing}\n" +
                    $"→ 右键 InventoryManager → Debug: Fill Missing Shapes 一键补全");
                return new List<Vector2Int>();
            }
            var pts = tetrisItemPointSet_SO.TetrisPieceShapeList[index]?.points;
            return pts ?? new List<Vector2Int>();
        }

        /// <summary>
        /// [ContextMenu] 一键填充/覆盖全部 23 个形状到 TetrisItemPointSet_SO。
        /// 实用化定义：手枪(1-4格)、工艺品(3-5格异形)、步枪(6-12格矩形)。
        /// 已有定义也会被覆盖，完成后自动保存。
        /// </summary>
        [ContextMenu("Debug: Fill All Shapes (实用化覆盖)")]
        public void FillMissingShapes()
        {
            if (tetrisItemPointSet_SO == null)
            {
                UnityEngine.Debug.LogError("[InventoryManager] tetrisItemPointSet_SO 未配置！");
                return;
            }

            var list = tetrisItemPointSet_SO.TetrisPieceShapeList;
            if (list == null)
            {
                list = new List<PointSet>();
                tetrisItemPointSet_SO.TetrisPieceShapeList = list;
            }

            int total = System.Enum.GetValues(typeof(TetrisPieceShape)).Length;
            int created = 0, overwritten = 0;

            for (int i = 0; i < total; i++)
            {
                var shape = (TetrisPieceShape)i;
                while (list.Count <= i) list.Add(null);

                bool existed = list[i] != null && list[i].points != null && list[i].points.Count > 0;
                if (existed) overwritten++; else created++;

                list[i] = new PointSet { tetrisPieceShape = shape, points = GetDefaultPoints(shape) };
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(tetrisItemPointSet_SO);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
            UnityEngine.Debug.Log(
                $"[InventoryManager] 形状已实用化: 共 {total} 个（新增 {created}，覆盖 {overwritten}），已保存。\n" +
                $"  手枪: Frame~Tetromino_O | 工艺品: Tetromino_T~Pentomino_P | 步枪: Cells9_Square~S_Rifle");
        }

        /// <summary>获取实用化形状坐标点（步枪/手枪/工艺品，最大 12 格）。</summary>
        private static List<Vector2Int> GetDefaultPoints(TetrisPieceShape shape)
        {
            return shape switch
            {
                // === 手枪/小件 (1-4格) ===
                TetrisPieceShape.Frame     => Rect(1, 1),       // 1格  戒指/钥匙
                TetrisPieceShape.Domino    => Rect(1, 2),       // 2格  弹匣
                TetrisPieceShape.Tromino_I => Rect(1, 3),       // 3格  长弹匣/手电
                TetrisPieceShape.Tromino_L => L(2, false),      // 3格  附件L
                TetrisPieceShape.Tromino_J => L(2, true),       // 3格  附件反L
                TetrisPieceShape.Tetromino_I => Rect(1, 4),     // 4格  消音器/长剑
                TetrisPieceShape.Tetromino_O => Rect(2, 2),     // 4格  手枪

                // === 工艺品 (3-5格，异形) ===
                TetrisPieceShape.Tetromino_T => T(),            // 4格  T形
                TetrisPieceShape.Tetromino_J => L(3, false),    // 4格  L形
                TetrisPieceShape.Tetromino_L => L(3, true),     // 4格  反L形
                TetrisPieceShape.Tetromino_S => ZigZag(false),  // 4格  S锯齿
                TetrisPieceShape.Tetromino_Z => ZigZag(true),   // 4格  Z锯齿
                TetrisPieceShape.Pentomino_I => Rect(1, 5),     // 5格  卷轴
                TetrisPieceShape.Pentomino_L => L3(false),      // 5格  L长
                TetrisPieceShape.Pentomino_J => L3(true),       // 5格  反L长
                TetrisPieceShape.Pentomino_U => U(),            // 5格  U形
                TetrisPieceShape.Pentomino_T => T5(),           // 5格  T形长柄
                TetrisPieceShape.Pentomino_P => P(),            // 5格  P形

                // === 步枪/大型 (6-12格，矩形) ===
                TetrisPieceShape.Cells9_Square  => Rect(3, 2),  // 6格  SMG
                TetrisPieceShape.Cells16_Square => Rect(4, 2),  // 8格  卡宾枪
                TetrisPieceShape.S_Sword        => Rect(5, 2),  // 10格 突击步枪
                TetrisPieceShape.S_Shotgun      => Rect(3, 3),  // 9格  冲锋枪/大工艺品
                TetrisPieceShape.S_Rifle        => Rect(6, 2),  // 12格 狙击步枪

                _ => new List<Vector2Int>()
            };
        }

        // ---- 形状工具方法 ----
        private static List<Vector2Int> Rect(int w, int h) { var p = new List<Vector2Int>(w * h); for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) p.Add(new(x, y)); return p; }
        private static List<Vector2Int> L(int h, bool mir) { var p = new List<Vector2Int>(); for (int y = 0; y < h; y++) p.Add(new(mir ? 1 : 0, y)); p.Add(new(mir ? 0 : 1, h - 1)); return p; }
        private static List<Vector2Int> L3(bool mir) { var p = new List<Vector2Int>(); for (int y = 0; y < 3; y++) p.Add(new(mir ? 1 : 0, y)); p.Add(new(mir ? 0 : 1, 2)); return p; }
        private static List<Vector2Int> T() => new() { new(1, 0), new(0, 1), new(1, 1), new(2, 1) };
        private static List<Vector2Int> T5() => new() { new(0, 0), new(1, 0), new(2, 0), new(1, 1), new(1, 2) };
        private static List<Vector2Int> U() => new() { new(0, 0), new(2, 0), new(0, 1), new(1, 1), new(2, 1) };
        private static List<Vector2Int> P() => new() { new(0, 0), new(1, 0), new(0, 1), new(1, 1), new(0, 2) };
        private static List<Vector2Int> ZigZag(bool z) => z ? new() { new(0, 0), new(1, 0), new(1, 1), new(2, 1) } : new() { new(1, 0), new(2, 0), new(0, 1), new(1, 1) };

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

        /// <summary>
        /// 获取鼠标下方的 TetrisItemView（用于快捷键丢弃等操作）。
        /// </summary>
        private TetrisItemView GetTetrisItemViewUnderMouse()
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Mouse.current.position.ReadValue();

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            for (int i = 0; i < results.Count; i++)
            {
                var go = results[i].gameObject;
                var view = go.GetComponentInParent<TetrisItemView>();
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
        /// 获取当前可用的背包网格 VM。
        /// 优先 depositoryGridView.ViewModel（最新），回退到缓存，最后才返回 null。
        /// </summary>
        private TetrisGridVM GetPlayerBagGridVM()
        {
            // 优先使用 GridView 上最新的 ViewModel（CTIS 重开背包会换新 VM）
            if (depositoryGridView != null && depositoryGridView.ViewModel != null)
            {
                depositoryGrid = depositoryGridView.ViewModel;
                return depositoryGrid;
            }
            // 回退：缓存的引用（仅在 GridView 未就绪时使用）
            if (depositoryGrid != null) return depositoryGrid;
            return null;
        }

        /// <summary>
        /// 将物品添加到玩家背包（无实例数据，创建全新 VM）。
        /// </summary>
        public bool AddItemToPlayerBag(int itemID)
        {
            return AddItemToPlayerBag(itemID, null);
        }

        /// <summary>
        /// 将物品添加到玩家背包，携带实例数据（Guid、堆叠数等）。
        /// 丢弃→拾取链路中保持物品身份和未来词条/附魔不丢失。
        /// </summary>
        public bool AddItemToPlayerBag(int itemID, TetrisItemPersistentData data)
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

            // 背包关闭 → 一律走暂存队列，等开背包时 CTIS 创建完整的 VM+View 后再放入。
            if (!IsInventoryOpen)
            {
                _pendingItems.Add(new PendingItem { itemID = itemID, data = data });
                UnityEngine.Debug.Log($"[InventoryManager] 背包关闭，暂存 itemID={itemID}（队列 {_pendingItems.Count} 件），打开背包时放入");
                return true;
            }

            var gridVM = GetPlayerBagGridVM();

            // 网格不可用 → 暂存
            if (gridVM == null)
            {
                _pendingItems.Add(new PendingItem { itemID = itemID, data = data });
                UnityEngine.Debug.Log($"[InventoryManager] 网格未就绪，暂存 itemID={itemID}（队列 {_pendingItems.Count} 件），打开背包时放入");
                return true;
            }

            return TryPlaceItemOnGrid(details, itemID, data, gridVM);
        }

        /// <summary>
        /// 在指定网格上放置物品。data 为 null 时创建全新 VM，非 null 时用持久化数据恢复实例状态。
        /// </summary>
        private bool TryPlaceItemOnGrid(ItemDetails details, int itemID, TetrisItemPersistentData data, TetrisGridVM gridVM)
        {
            // 物品尺寸检查
            if (details.xWidth <= 0 || details.yHeight <= 0)
            {
                UnityEngine.Debug.LogWarning(
                    $"[InventoryManager] 物品 {details.localizedName.GetLocalizedString()} (ID:{itemID}) " +
                    $"尺寸为 {details.xWidth}×{details.yHeight}，在网格中将不可见！请在 ItemDataList_SO 中设置 xWidth/yHeight");
            }

            var itemVM = TetrisItemFactory.GetOrCreateVM(details, data, gridVM);
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

        /// <summary>
        /// 丢弃物品：从背包网格中移除物品，并在玩家附近生成对应的 3D 预制体。
        /// 保留物品实例数据（Guid、堆叠数、CustomData 等），未来加词条/附魔不会丢。
        /// </summary>
        public void DiscardItem(TetrisItemVM itemVM)
        {
            if (itemVM == null)
            {
                UnityEngine.Debug.LogError("[InventoryManager] DiscardItem: itemVM 为 null");
                return;
            }

            var details = itemVM.ItemDetails;
            if (details == null)
            {
                UnityEngine.Debug.LogError("[InventoryManager] DiscardItem: ItemDetails 为 null");
                return;
            }

            // 0. 在清理 VM 前捕获实例数据，丢弃→拾取链路中保持不变
            var instanceData = new TetrisItemPersistentData
            {
                itemID = details.itemID,
                itemGuid = itemVM.Guid,
                direction = itemVM.Direction,
                stack = itemVM.CurrentStack,
            };

            // 1. 从网格中移除（会清理 OccupiedCells、OwnerItemsDic，并触发视图回收）
            var gridVM = itemVM.CurrentTetrisContainer as TetrisGridVM;
            if (gridVM != null)
            {
                var pos = itemVM.LocalGridCoordinate;
                var coords = itemVM.TetrisCoordinateSet;
                var offset = itemVM.RotationOffset;
                gridVM.RemoveTetrisItem(itemVM, pos.x, pos.y, offset, coords, destroyView: true);
            }

            // 2. 从持久化数据中删除（否则重开背包会恢复）
            if (!string.IsNullOrEmpty(itemVM.Guid))
            {
                var saveLoad = InventorySaveLoadService.Instance;
                if (saveLoad != null && saveLoad.inventoryData_SO != null)
                {
                    saveLoad.inventoryData_SO.RemovePersistentDataByGuid(itemVM.Guid);
                }
                TetrisItemFactory.UnregisterVM(itemVM.Guid, removeViews: true);
            }

            // 3. 在世界中生成预制体，注入 pickUpItemID + 实例数据
            if (details.itemEntity != null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                Vector3 dropPos;
                if (player != null)
                {
                    // 在玩家前方 1.5m 处生成
                    dropPos = player.transform.position + player.transform.forward * 1.5f + Vector3.up * 0.3f;
                }
                else
                {
                    dropPos = Vector3.zero;
                    UnityEngine.Debug.LogWarning("[InventoryManager] DiscardItem: 找不到 Player，在原点生成");
                }

                var spawned = Instantiate(details.itemEntity, dropPos, details.itemEntity.transform.rotation);

                // 注入 pickUpItemID，使丢弃物可被再次拾取
                var pickup = spawned.GetComponent<InteractiveObjectBase>();
                if (pickup != null)
                {
                    pickup.pickUpItemID = details.itemID;
                    pickup.discardData = instanceData;
                }
                else
                {
                    UnityEngine.Debug.LogWarning(
                        $"[InventoryManager] 丢弃的预制体 {details.itemEntity.name} 上没有 InteractiveObjectBase 组件，" +
                        $"物品将无法被拾取！");
                }

                UnityEngine.Debug.Log(
                    $"[InventoryManager] 丢弃: {details.localizedName.GetLocalizedString()} " +
                    $"(ID:{details.itemID} | {details.itemRarity} | Guid:{instanceData.itemGuid}) → 世界坐标 {dropPos}");
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    $"[InventoryManager] 丢弃: {details.localizedName.GetLocalizedString()} " +
                    $"(ID:{details.itemID}) 没有 itemEntity 预制体，物品已销毁");
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
            var copy = new List<PendingItem>(_pendingItems);
            _pendingItems.Clear();

            foreach (var p in copy)
            {
                var details = itemDataList_SO.GetItemDetailsByID(p.itemID);
                if (details == null) continue;

                if (TryPlaceItemOnGrid(details, p.itemID, p.data, gridVM))
                {
                    UnityEngine.Debug.Log($"[InventoryManager] 暂存物品入背包: {details.localizedName.GetLocalizedString()} (ID={p.itemID})");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[InventoryManager] 暂存物品放置失败: {details.localizedName.GetLocalizedString()} (ID={p.itemID})");
                }
            }
        }
    }
}