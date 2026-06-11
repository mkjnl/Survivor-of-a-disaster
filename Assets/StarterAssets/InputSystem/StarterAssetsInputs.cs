using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	/// <summary>
	/// Starter Assets 输入管理脚本
	/// 用于接收玩家输入：
	/// 移动、视角、跳跃、冲刺
	/// </summary>
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]

		// 玩家移动输入（WASD）
		public Vector2 move;

		// 鼠标视角输入
		public Vector2 look;

		// 是否跳跃
		public bool jump;

		// 是否冲刺
		public bool sprint;

		[Header("Movement Settings")]

		// 是否启用模拟移动（主要用于手柄）
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]

		// 鼠标是否锁定
		public bool cursorLocked = true;

		// 是否允许鼠标控制视角
		public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM

		/// <summary>
		/// 接收移动输入（WASD）
		/// </summary>
		/// <param name="value">输入值</param>
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		/// <summary>
		/// 接收鼠标视角输入
		/// </summary>
		/// <param name="value">输入值</param>
		public void OnLook(InputValue value)
		{
			// 只有允许鼠标输入时才更新视角
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		/// <summary>
		/// 接收跳跃输入
		/// </summary>
		/// <param name="value">输入值</param>
		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}

		/// <summary>
		/// 接收冲刺输入
		/// </summary>
		/// <param name="value">输入值</param>
		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}
#endif

		/// <summary>
		/// 设置移动输入值
		/// </summary>
		/// <param name="newMoveDirection">新的移动方向</param>
		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		}

		/// <summary>
		/// 设置视角输入值
		/// </summary>
		/// <param name="newLookDirection">新的视角方向</param>
		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		/// <summary>
		/// 设置跳跃状态
		/// </summary>
		/// <param name="newJumpState">是否跳跃</param>
		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		/// <summary>
		/// 设置冲刺状态
		/// </summary>
		/// <param name="newSprintState">是否冲刺</param>
		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}

		/// <summary>
		/// 当游戏窗口获得或失去焦点时调用
		/// </summary>
		/// <param name="hasFocus">是否获得焦点</param>
		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		/// <summary>
		/// 设置鼠标锁定状态
		/// true：锁定鼠标（FPS模式）
		/// false：解锁鼠标（UI模式）
		/// </summary>
		/// <param name="newState">新的鼠标状态</param>
		private void SetCursorState(bool newState)
		{
			Cursor.lockState =
				newState
					? CursorLockMode.Locked
					: CursorLockMode.None;
		}
	}
}