using System.Collections;
using UnityEngine;

/// <summary>
/// 环境音区域：玩家进入触发器 → 淡入环境音，离开 → 淡出。
/// 挂载在每个环境音触发器 GameObject 上，每个区域配一段 AudioClip。
/// </summary>
public class AmbientSoundSystem : MonoBehaviour
{
    [Header("===== 音频 =====")]
    [Tooltip("该区域的环境音效")]
    public AudioClip ambientClip;

    [Tooltip("最大音量（0~1）")]
    [Range(0f, 1f)]
    public float maxVolume = 1f;

    [Header("===== 过渡 =====")]
    [Tooltip("淡入 / 淡出时长（秒）")]
    [Range(0.5f, 5f)]
    public float fadeDuration = 1.5f;

    [Header("===== 空间（可选）=====")]
    [Tooltip("0 = 纯 2D（全屏均匀），1 = 纯 3D（随距离衰减）。环境音一般用 0")]
    [Range(0f, 1f)]
    public float spatialBlend = 0f;

    private AudioSource _audioSource;
    private Coroutine _fadeRoutine;

    private void Awake()
    {
        // 确保碰撞体为 Trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        // 动态挂载 AudioSource
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.clip = ambientClip;
        _audioSource.loop = true;
        _audioSource.volume = 0f;
        _audioSource.spatialBlend = spatialBlend;
        _audioSource.playOnAwake = false;

        if (ambientClip != null)
            _audioSource.Play();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            FadeTo(maxVolume);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            FadeTo(0f);
    }

    private void FadeTo(float target)
    {
        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine(target));
    }

    private IEnumerator FadeRoutine(float target)
    {
        float start = _audioSource.volume;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            _audioSource.volume = Mathf.Lerp(start, target, elapsed / fadeDuration);
            yield return null;
        }

        _audioSource.volume = target;
    }
}
