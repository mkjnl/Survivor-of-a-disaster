using System;
using System.Collections.Generic;
using Cholopol.TIS.MVVM.ViewModels;

/// <summary>
/// 容器物品操作接口。物品生成管理器通过此接口与容器网格交互。
/// 由 ContainerGridController 实现。
/// </summary>
public interface IContainerItemProvider
{
    /// <summary>尝试在指定位置放置物品。成功返回 true。</summary>
    bool TryPlaceItem(TetrisItemVM item, int posX, int posY);

    /// <summary>从容器中移除物品。成功返回 true。</summary>
    bool TryRemoveItem(TetrisItemVM item);

    /// <summary>检查指定位置是否可以放置该物品。</summary>
    bool CanPlaceAt(TetrisItemVM item, int posX, int posY);

    /// <summary>获取当前容器网格 VM。</summary>
    TetrisGridVM GetGridVM();

    /// <summary>获取容器中所有物品。</summary>
    IEnumerable<TetrisItemVM> GetAllItems();

    /// <summary>物品变化时触发。</summary>
    event Action<TetrisItemVM> OnItemChanged;
}
