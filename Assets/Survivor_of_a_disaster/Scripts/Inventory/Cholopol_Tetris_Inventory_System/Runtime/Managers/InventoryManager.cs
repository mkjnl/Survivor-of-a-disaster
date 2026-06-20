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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Cholopol.TIS.MVVM.ViewModels;
using Cholopol.TIS.MVVM.Views;
using Cholopol.TIS.Events;
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

        public GameObject InventorySystemRoot => inventorySystemRoot;
        public InventoryPlacementConfig_SO PlacementConfig => placementConfig;
        [Header("Focused TetrisItem Object")]
        public TetrisItemVM selectedItemVM;
        [SerializeField] private TetrisItemView selectedItemView;
        public Vector2Int tileGridOriginPosition;

        private void Update()
        {
            // B 键 开关背包
            // if (Keyboard.current.bKey.wasPressedThisFrame)
            // {
            //     ToggleInventorySystem();
            // }

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

            // 关闭背包时回收 UI
            if (!willOpen)
            {
                EventBus.Instance.Publish(EventNames.RecycleInventoryItemUI);
            }

            // 切换背包显示状态
            inventorySystemRoot.SetActive(willOpen);

            // 开始界面显示/隐藏
            if (startPanel != null)
            {
                startPanel.SetActive(!willOpen);
            }

            // 打开背包时生成 UI
            if (willOpen)
            {
                EventBus.Instance.Publish(EventNames.InstantiateInventoryItemUI);
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
    }
}