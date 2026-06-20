using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractionBtn : MonoBehaviour
{
    public CanvasGroup canvasGroup;       // 要控制显示隐藏的 CanvasGroup
    public AnimationCurve showCurve;      // 显示动画曲线
    public AnimationCurve hideCurve;      // 隐藏动画曲线
    public float animationSpeed = 1f;     // 动画速度

    private PlayerInteraction playerInteraction;
    private Coroutine currentAnimation;
    private bool isVisible = false;

    void Awake()
    {
        // 初始状态：完全隐藏
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    void Start()
    {
        playerInteraction = GetComponentInParent<PlayerInteraction>();
    }

    void Update()
    {
        if (playerInteraction == null) return;

        bool shouldShow = playerInteraction.IsContainerNearby;

        // 只在状态改变时才切换，避免重复启动协程
        if (shouldShow != isVisible)
        {
            isVisible = shouldShow;

            // 停止当前动画
            if (currentAnimation != null)
                StopCoroutine(currentAnimation);

            // 启动新的动画
            currentAnimation = StartCoroutine(shouldShow ? ShowPanel() : HidePanel());
        }
    }

    IEnumerator ShowPanel()
    {
        // 显示时确保可以交互
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsed < 1f)
        {
            elapsed += Time.deltaTime * animationSpeed;
            float t = showCurve.Evaluate(elapsed);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, t);
            yield return null;
        }

        canvasGroup.alpha = 1f;
    }
    /// <summary>
    /// 隐藏面板的协程，使用动画曲线控制淡出效果，最后将 CanvasGroup 设置为不可交互和不阻挡射线，以确保 UI 不再响应输入事件
    /// 这个方法会在玩家离开交互范围时被调用，确保交互按钮平滑地淡出并且不再干扰玩家的操作
    /// 注意：如果在隐藏过程中玩家再次进入交互范围，当前的隐藏动画会被停止，新的显示动画会立即开始，确保 UI 的响应性和流畅性
    /// 你可以根据需要调整动画曲线和速度，以获得最佳的视觉效果。
    /// </summary>
    /// <returns></returns>
    IEnumerator HidePanel()
    {
        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsed < 1f)
        {
            elapsed += Time.deltaTime * animationSpeed;
            float t = hideCurve.Evaluate(elapsed);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
    /// <summary>
    /// 外部调用的方法，用于直接显示或隐藏交互按钮面板，适用于需要在特定事件（如剧情触发、特殊交互等）中强制控制 UI 显示状态的情况
    /// </summary>

    public void ShowPanelExternally()
    {
        if (currentAnimation != null)
            StopCoroutine(currentAnimation);

        currentAnimation = StartCoroutine(ShowPanel());
        isVisible = true;
    }
    /// <summary>
    /// 外部调用的方法，用于直接隐藏交互按钮面板，适用于需要在特定事件（如剧情触发、特殊交互等）中强制控制 UI 显示状态的情况
     /// 注意：调用此方法会立即停止当前的显示动画并开始隐藏动画，确保 UI 的响应性和流畅性
     /// 你可以根据需要在调用此方法前检查当前状态，以避免不必要的动画切换。
     /// 例如：如果当前已经不可见，则无需调用此方法。
     /// 这个方法提供了一个灵活的接口，让其他脚本或事件能够直接控制交互按钮的显示状态，而不依赖于玩家的接近检测逻辑。
     /// 这对于一些特殊场景非常有用，比如剧情发展、特定任务触发等，可以确保玩家在正确的时机看到交互提示。
     /// 同时，这也增强了 UI 的可控性，使得游戏设计更加灵活和丰富。
     /// 你可以根据实际需求在游戏中适当调用此方法，以提升玩家的体验和游戏的整体表现。
    /// </summary>
    public void HidePanelExternally()
    {
        if (currentAnimation != null)
            StopCoroutine(currentAnimation);

        currentAnimation = StartCoroutine(HidePanel());
        isVisible = false;
    }
}