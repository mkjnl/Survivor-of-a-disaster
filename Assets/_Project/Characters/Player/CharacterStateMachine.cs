using UnityEngine;
using StarterAssets;

public class CharacterStateMachine : MonoBehaviour
{
    [Header("依赖组件")]
    public StarterAssetsInputs Input { get; private set; }
    public Animator Animator { get; private set; }
    public PlayerCameraController CameraController { get; private set; }

    [Header("当前状态标识")]
    public bool IsAiming { get; set; }

    private BaseState currentState;
    private ThirdPersonController thirdPersonController;

    private void Awake()
    {
        Input = GetComponent<StarterAssetsInputs>();
        Animator = GetComponent<Animator>();
        CameraController = GetComponent<PlayerCameraController>();
        thirdPersonController = GetComponent<ThirdPersonController>();

        // 初始化默认状态
        currentState = new DefaultState(this);
        currentState.Enter();
    }

    private void Update()
    {
        // 物品栏打开时暂停所有角色状态逻辑
        if (Cholopol.TIS.InventoryManager.Instance != null
            && Cholopol.TIS.InventoryManager.Instance.IsInventoryOpen)
            return;

        currentState?.Update();
    }

    public void ChangeState(BaseState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();
    }

    // 供 DefaultState 使用
    public ThirdPersonController ThirdPersonController => thirdPersonController;
}