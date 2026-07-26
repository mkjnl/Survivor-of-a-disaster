using UnityEngine;
using Unity.Cinemachine;
using Vector3 = UnityEngine.Vector3;
// 注意：系统自带的 System.Numerics 已经去掉了，因为使用 Vector3 = UnityEngine.Vector3 就不需要它了。

/// <summary>
/// 玩家相机控制器
/// 负责处理瞄准时的 FOV、相机距离和肩膀偏移的平滑过渡
/// 不依赖状态机，只通过 EnterAim/ExitAim 接收瞄准状态通知
/// </summary>
public class PlayerCameraController : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("主 Cinemachine 虚拟相机")]
    [SerializeField] private CinemachineCamera virtualCamera;

    [Header("Lens")]
    [Tooltip("正常状态下的视野角度")]
    [SerializeField] private float normalFOV = 60f;

    [Tooltip("瞄准状态下的视野角度（数值越小放大越明显）")]
    [SerializeField] private float aimFOV = 35f;

    [Tooltip("FOV 过渡的速度")]
    [SerializeField] private float fovSpeed = 8f;

    [Header("Third Person Follow")]
    [Tooltip("第三人称跟随组件，用于调整相机距离和肩膀偏移")]
    [SerializeField] private CinemachineThirdPersonFollow thirdPersonFollow;

    [Tooltip("正常状态下的相机距离")]
    [SerializeField] private float normalDistance = 4f;

    [Tooltip("瞄准状态下的相机距离（更靠近角色）")]
    [SerializeField] private float aimDistance = 2.2f;

    [Tooltip("正常状态下的肩膀偏移（右/上/前）")]
    [SerializeField] private Vector3 normalShoulderOffset = new Vector3(0.5f, 1.5f, 0);

    [Tooltip("瞄准状态下的肩膀偏移（略微调整以获得更好的瞄准视角）")]
    [SerializeField] private Vector3 aimShoulderOffset = new Vector3(0.8f, 1.5f, 0);

    [Header("Aim Target")]
    [Tooltip("已经绑好了Rig的武器瞄准目标点")]
    [SerializeField]
    private Transform aimTarget; // 这里拖入你的 AimTarget 物体

    [Tooltip("瞄准时，目标点距离摄像机的视距")]
    [SerializeField]
    private float aimTargetDistance = 100f; // 通常设为一个较大的值，比如100，代表无限远

    [Tooltip("AimTarget 移动到瞄准位置的速度 (越大越快)")]
    [SerializeField]
    private float aimTransitionSpeed = 15f;

    [Tooltip("射线检测的目标层（哪些物体可以被瞄准）")]
    [SerializeField]
    private LayerMask targetLayer;  // 新增：在 Inspector 中设置

    /// <summary>
    /// 当前是否处于瞄准状态
    /// </summary>
    private bool aiming;

    private Vector3 defaultAimTargetPosition;
    private Vector3 aimTargetVelocity;

    private void Start()
    {
        if (aimTarget != null)
        {
            defaultAimTargetPosition = aimTarget.localPosition;
        }
    }

    /// <summary>
    /// 进入瞄准状态，由 CharacterStateMachine 在右键按下时调用
    /// </summary>
    public void EnterAim()
    {
        aiming = true;
        // 当按下右键刚进入瞄准时，瞬间把目标点搬到屏幕正前方
        if (aimTarget != null)
        {
            // 放在相机正前方
            aimTarget.position = transform.position + transform.forward * aimTargetDistance;
        }
    }

    /// <summary>
    /// 退出瞄准状态，由 CharacterStateMachine 在右键松开时调用
    /// </summary>
    public void ExitAim()
    {
        aiming = false;
        // 退出瞄准时，让 AimTarget 回到初始位置（武器自然下垂）
        if (aimTarget != null)
        {
            aimTarget.localPosition = defaultAimTargetPosition;
        }
    }

    /// <summary>
    /// 在 LateUpdate 中进行相机参数的平滑过渡
    /// 使用 LateUpdate 确保在角色动画更新之后执行，避免画面抖动
    /// </summary>
    private void LateUpdate()
    {
        if (virtualCamera == null)
            return;

        // --- FOV 平滑过渡 ---
        var lens = virtualCamera.Lens;
        float targetFov = aiming ? aimFOV : normalFOV;

        lens.FieldOfView = Mathf.Lerp(
            lens.FieldOfView,
            targetFov,
            Time.deltaTime * fovSpeed);

        virtualCamera.Lens = lens;

        // --- 第三人称跟随参数平滑过渡 ---
        if (thirdPersonFollow != null)
        {
            thirdPersonFollow.CameraDistance = Mathf.Lerp(
                thirdPersonFollow.CameraDistance,
                aiming ? aimDistance : normalDistance,
                Time.deltaTime * fovSpeed);

            thirdPersonFollow.ShoulderOffset = Vector3.Lerp(
                thirdPersonFollow.ShoulderOffset,
                aiming ? aimShoulderOffset : normalShoulderOffset,
                Time.deltaTime * fovSpeed);
        }
    }

    /// <summary>
    /// 每帧更新 AimTarget 的位置（由 AimingState 调用）
    /// </summary>
    public void UpdateAimTarget()
    {
        if (!aiming || aimTarget == null) return;

        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        // 从实际摄像机位置向摄像机前方发射射线（而非玩家位置）
        Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, aimTargetDistance, targetLayer))
        {
            aimTarget.position = hit.point;
        }
        else
        {
            aimTarget.position = mainCam.transform.position + mainCam.transform.forward * aimTargetDistance;
        }
    }
}