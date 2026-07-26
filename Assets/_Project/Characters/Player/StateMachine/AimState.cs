using UnityEngine;
using StarterAssets;

public class AimState : BaseState
{
    private ThirdPersonController _tpc;
    private Transform _mainCamera;
    private WeaponController _weapon;                     // ★ 武器控制器
    private float _lastFireInputTime = -999f;             // 上次检测到"按下"的时间，用于半自动
    private bool _wasFiring;                               // 上一帧是否在开枪，用于检测松手

    public AimState(CharacterStateMachine context) : base(context)
    {
        _tpc = context.ThirdPersonController;
        _mainCamera = Camera.main?.transform;
        _weapon = _tpc?.GetComponent<WeaponController>(); // 和 ThirdPersonController 挂在同一个物体上
    }

    public override void Enter()
    {
        context.Animator.SetBool("IsAiming", true);
        context.CameraController?.EnterAim();
        context.IsAiming = true;

        // ✅ 进入瞄准时，让角色立即朝向相机方向
        if (_mainCamera != null && _tpc != null)
        {
            float cameraYaw = _mainCamera.eulerAngles.y;
            _tpc.transform.rotation = Quaternion.Euler(0, cameraYaw, 0);
        }

        Debug.Log("【状态机】进入：瞄准状态");
    }

    public override void Update()
    {
        if (!context.Input.aim)
        {
            context.ChangeState(new DefaultState(context));
            return;
        }

        // 物理更新（地面检测 + 重力 + 跳跃）
        if (_tpc != null)
        {
            typeof(ThirdPersonController)
                .GetMethod("GroundedCheck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(_tpc, null);

            typeof(ThirdPersonController)
                .GetMethod("JumpAndGravity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(_tpc, null);
        }

        HandleAimingMovement();

        // 应用垂直速度（跳跃/下落）
        var controller = _tpc?.GetComponent<CharacterController>();
        if (controller != null && _tpc != null)
        {
            var verticalVelField = typeof(ThirdPersonController)
                .GetField("_verticalVelocity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            float verticalVelocity = verticalVelField != null ? (float)verticalVelField.GetValue(_tpc) : 0f;
            controller.Move(new Vector3(0, verticalVelocity * Time.deltaTime, 0));
        }

        // ✅ 每帧更新相机瞄准目标点（你的 AimTarget 跟随相机中心）
        context.CameraController?.UpdateAimTarget();

        // ★★★ 开枪检测 ★★★
        HandleShooting();
    }

    /// <summary>
    /// ★ 处理瞄准状态下的左键开枪
    /// </summary>
    private void HandleShooting()
    {
        if (_weapon == null) return;

        bool firePressed = context.Input.leftclick;

        // ★ 松手检测：上一帧在开枪，这一帧松开了 → 停止所有枪声音效
        if (_wasFiring && !firePressed)
        {
            _weapon.StopFiringEffects();
        }
        _wasFiring = firePressed;

        if (_weapon.isAutomatic)
        {
            // 自动模式：按住连发，内部有射速限制
            if (firePressed)
            {
                _weapon.TryShoot();
            }
        }
        else
        {
            // 半自动模式：只有"按下瞬间"才开枪（松开再按才能打下一枪）
            if (firePressed && Time.time - _lastFireInputTime > (1f / _weapon.fireRate))
            {
                _lastFireInputTime = Time.time;
                _weapon.TryShoot();
            }
        }
    }

    private void HandleAimingMovement()
    {
        Vector2 input = context.Input.move;
        float inputMagnitude = input.magnitude;

        if (_mainCamera == null) return;

        // 计算相对于相机的移动方向
        Vector3 camForward = _mainCamera.forward;
        Vector3 camRight = _mainCamera.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = (camRight * input.x + camForward * input.y).normalized;

        // 驱动角色实际移动
        if (_tpc != null && inputMagnitude > 0.01f)
        {
            float targetSpeed = context.Input.sprint ? _tpc.SprintSpeed : _tpc.MoveSpeed;

            var verticalVelField = typeof(ThirdPersonController)
                .GetField("_verticalVelocity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            float verticalVelocity = 0f;
            if (verticalVelField != null)
            {
                verticalVelocity = (float)verticalVelField.GetValue(_tpc);
            }

            Vector3 motion = moveDir * targetSpeed * Time.deltaTime;

            var controller = _tpc.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.Move(motion + new Vector3(0, verticalVelocity * Time.deltaTime, 0));
            }
        }

        // 角色朝向持续跟随摄像机方向（仅 Y 轴）
        if (_tpc != null && _mainCamera != null)
        {
            Vector3 cameraForward = _mainCamera.forward;
            cameraForward.y = 0;
            cameraForward.Normalize();

            Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
            _tpc.transform.rotation = Quaternion.Slerp(
                _tpc.transform.rotation,
                targetRotation,
                Time.deltaTime * 12f); // 12f 为旋转速度，可调整
        }

        // 设置 Aiming Blend Tree 参数（带阻尼平滑）
        float targetAimingX = input.x;
        float targetAimingY = input.y;

        context.Animator.SetFloat("AimingX", targetAimingX, 0.15f, Time.deltaTime);
        context.Animator.SetFloat("AimingY", targetAimingY, 0.15f, Time.deltaTime);
    }

    public override void Exit()
    {
        context.Animator.SetBool("IsAiming", false);
        context.CameraController?.ExitAim();
        context.IsAiming = false;

        context.Animator.SetFloat("AimingX", 0f);
        context.Animator.SetFloat("AimingY", 0f);

        // ★ 退出瞄准时停止所有枪声（防止右键松开但左键还按着时音效残留）
        _weapon?.StopFiringEffects();
        _wasFiring = false;

        Debug.Log("【状态机】退出：瞄准状态");
    }
}