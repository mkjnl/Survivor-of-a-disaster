using UnityEngine;
#if ENABLE_INPUT_SYSTEM 
using UnityEngine.InputSystem;
#endif

/* 注意：动画是通过控制器调用角色和胶囊体的 Animator，并使用空检查来避免报错
 */

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM 
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Player 玩家设置")]
        [Tooltip("角色普通移动速度 (m/s)")]
        public float MoveSpeed = 2.0f;

        [Tooltip("角色奔跑速度 (m/s)")]
        public float SprintSpeed = 5.335f;

        [Tooltip("角色转向面对移动方向的速度")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("加速度和减速度")]
        public float SpeedChangeRate = 10.0f;

        [Header("音频")]
        public AudioSource AudioFootsteps;      // 脚步声
        public AudioSource LandingAudio;        // 落地声
        public AudioSource AudioFoley;          // 衣服/装备摩擦声
        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Header("跳跃与重力")]
        [Tooltip("跳跃高度")]
        public float JumpHeight = 1.2f;

        [Tooltip("自定义重力值（引擎默认是 -9.81f）")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("跳跃冷却时间，设为 0 可以立即连跳")]
        public float JumpTimeout = 0.50f;

        [Tooltip("进入坠落状态前的缓冲时间（走下楼梯时很有用）")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded 地面检测")]
        [Tooltip("角色是否在地面上（自定义检测，不使用 CharacterController 自带检测）")]
        public bool Grounded = true;

        [Tooltip("地面检测偏移（适应不平整地面）")]
        public float GroundedOffset = -0.14f;

        [Tooltip("地面检测球体半径，应与 CharacterController 半径匹配")]
        public float GroundedRadius = 0.28f;

        [Tooltip("哪些层被视为地面")]
        public LayerMask GroundLayers;

        [Header("Cinemachine 相机设置")]
        [Tooltip("Cinemachine 虚拟相机跟随的目标")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("相机向上最大旋转角度")]
        public float TopClamp = 70.0f;

        [Tooltip("相机向下最大旋转角度")]
        public float BottomClamp = -30.0f;

        [Tooltip("额外相机角度偏移")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("是否锁定相机位置")]
        public bool LockCameraPosition = false;

        // Cinemachine 相关变量
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // 玩家状态变量
        private float _speed;                    // 当前移动速度
        private float _animationBlend;           // 用于动画混合
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;         // 垂直速度（跳跃和下落）
        private float _terminalVelocity = 53.0f; // 最大下落速度

        // 超时计时器
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // 动画参数 ID（使用 StringToHash 提升性能）
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

#if ENABLE_INPUT_SYSTEM 
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;

        private const float _threshold = 0.01f;

        private bool _hasAnimator;

        // 判断当前是否使用鼠标键盘控制方案
        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            // 获取主摄像机引用
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        private void Start()
        {
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;

            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();

#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#else
            Debug.LogError("Starter Assets 包缺少依赖，请使用 Tools/Starter Assets/Reinstall Dependencies 修复");
#endif

            AssignAnimationIDs();

            // 重置超时计时器
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            // 物品栏打开时暂停所有角色逻辑
            if (Cholopol.TIS.InventoryManager.Instance != null
                && Cholopol.TIS.InventoryManager.Instance.IsInventoryOpen)
                return;

            var stateMachine = GetComponent<CharacterStateMachine>();

            GroundedCheck();
            JumpAndGravity();

            // 瞄准状态下只保留必要物理（地面检测 + 重力 + 跳跃）
            if (stateMachine != null && stateMachine.IsAiming)
            {
                return;
            }
            Move();
        }

        private void LateUpdate()
        {
            // 物品栏打开时跳过相机旋转
            if (Cholopol.TIS.InventoryManager.Instance != null
                && Cholopol.TIS.InventoryManager.Instance.IsInventoryOpen)
                return;

            CameraRotation();
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void GroundedCheck()
        {
            // 计算地面检测球体位置
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);

            // 更新动画参数
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        private void CameraRotation()
        {
            // 如果有视角输入且相机未锁定
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
                _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
            }

            // 限制相机旋转角度
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // 更新 Cinemachine 跟随目标的旋转
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
                _cinemachineTargetYaw, 0.0f);
        }

        private void Move()
        {
            var stateMachine = GetComponent<CharacterStateMachine>();

            // 根据是否按下奔跑键设置目标速度
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

            // 如果没有移动输入，目标速度为 0
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            // 当前水平移动速度
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            // 平滑加速/减速
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // 归一化输入方向
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            // 根据输入方向旋转角色（相对相机方向）
            if (_input.move != Vector2.zero)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  _mainCamera.transform.eulerAngles.y;

                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, RotationSmoothTime);
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }

            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            // 执行移动（包含垂直速度）
            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            // 更新动画参数
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;

                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                // 防止在地上时垂直速度无限下降
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // 执行跳跃
                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDJump, true);
                    }
                    _input.jump = false;
                }

                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                _jumpTimeoutDelta = JumpTimeout;

                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else if (_hasAnimator)
                {
                    _animator.SetBool(_animIDFreeFall, true);
                }

                _input.jump = false; // 不在地面时禁止跳跃
            }

            // 应用重力
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            // 在编辑器中可视化地面检测范围
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            Gizmos.color = Grounded ? transparentGreen : transparentRed;
            Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
        }

        // 动画事件：脚步声
        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (AudioFootsteps != null) AudioFootsteps.Play();
                if (AudioFoley != null) AudioFoley.Play();
            }
        }

        // 动画事件：落地声
        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (LandingAudio != null)
                    LandingAudio.Play();
            }
        }
    }
}