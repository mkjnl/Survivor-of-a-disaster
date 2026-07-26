using UnityEngine;

// 所有角色的状态都要继承这个类
public abstract class BaseState
{
    protected CharacterStateMachine context; // 持有状态机的引用，方便调用切换方法
    public BaseState(CharacterStateMachine context)
    {
        this.context = context;
    }

    // 进入该状态时调用（比如：开始瞄准时，设置参数）
    public abstract void Enter();

    // 每一帧更新该状态时调用（比如：在瞄准时持续锁定敌人）
    public abstract void Update();

    // 退出该状态时调用（比如：结束瞄准时，恢复参数）
    public abstract void Exit();
}