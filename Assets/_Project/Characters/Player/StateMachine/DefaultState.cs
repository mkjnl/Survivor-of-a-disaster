using UnityEngine;
using StarterAssets;

public class DefaultState : BaseState
{
    private ThirdPersonController _tpc;

    public DefaultState(CharacterStateMachine context) : base(context)
    {
        _tpc = context.ThirdPersonController;
    }

    public override void Enter()
    {
        context.Animator.SetBool("IsAiming", false);
        context.IsAiming = false;

        Debug.Log("【状态机】进入：默认状态");
    }

    public override void Update()
    {
        // 检测瞄准输入
        if (context.Input.aim)
        {
            context.ChangeState(new AimState(context));
            return;
        }

        // 默认状态下让 ThirdPersonController 正常工作
        if (_tpc != null)
        {
            // 调用必要方法（保持原有移动、跳跃、重力、动画更新）
            typeof(ThirdPersonController)
                .GetMethod("GroundedCheck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(_tpc, null);

            typeof(ThirdPersonController)
                .GetMethod("JumpAndGravity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(_tpc, null);

            typeof(ThirdPersonController)
                .GetMethod("Move", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(_tpc, null);
        }
    }

    public override void Exit()
    {
        // 可选清理
    }
}