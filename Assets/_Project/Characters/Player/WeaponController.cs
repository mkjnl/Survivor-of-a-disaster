using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Audio;   // 用于 AudioResource

/// <summary>
/// 武器控制器：负责开枪、射线检测、伤害判定等核心逻辑
/// 挂载在玩家 GameObject 上
/// </summary>
public class WeaponController : MonoBehaviour
{
    [Header("===== 武器基础配置 =====")]
    [Tooltip("当前武器的名称（仅用于调试）")]
    public string weaponName = "默认武器";

    [Tooltip("单发伤害")]
    public float damage = 25f;

    [Tooltip("有效射程（米）")]
    public float maxRange = 200f;

    [Tooltip("射速：每秒最多开几枪")]
    public float fireRate = 10f;

    [Tooltip("是否允许按住连发（true=自动步枪，false=半自动）")]
    public bool isAutomatic = true;

    [Header("===== 散布 / 精准度 =====")]
    [Tooltip("最大散布角度（度），0 表示绝对精准")]
    [Range(0f, 10f)]
    public float spreadAngle = 0.5f;

    [Header("===== 引用 =====")]
    [Tooltip("枪口位置（子弹射线从这里发出），不拖的话默认用主摄像机")]
    public Transform muzzlePoint;

    [Tooltip("射击目标点（瞄准时自动更新），不拖的话用摄像机正前方")]
    public Transform aimTarget;

    [Tooltip("可以被打中的层")]
    public LayerMask targetLayer = ~0;

    [Header("===== 开枪特效（可选） =====")]
    [Tooltip("枪口火焰粒子")]
    public ParticleSystem muzzleFlash;

    [Tooltip("弹孔 / 命中特效预制体")]
    public GameObject hitEffectPrefab;

    [Tooltip("弹道拖尾预制体（如 TrailRenderer）")]
    public GameObject bulletTrailPrefab;

    [Header("===== 音效（可选） =====")]
    [Tooltip("开枪音效（Audio Random Container），拖 ARC 资源进来，每次开枪随机选一个变体播放")]
    public AudioResource gunfireARC;

    [Tooltip("开枪音量（0~1）")]
    [Range(0f, 1f)]
    public float gunfireVolume = 0.8f;

    [Tooltip("停止射击后音量衰减时间（秒），让枪声有自然尾音而不是戛然而止")]
    [Range(0.1f, 3f)]
    public float gunfireFadeOutDuration = 0.5f;

    [Tooltip("混响强度（0=干声，1=最强混响），模拟环境回音 / 尾音效果")]
    [Range(0f, 1f)]
    public float reverbLevel = 0.6f;

    [Tooltip("Audio Mixer Group（可选），拖入带 Reverb 效果的 Mixer Group 可实现更丰富的空间混响")]
    public AudioMixerGroup outputAudioMixerGroup;

    [Header("===== 事件（方便 UI 等监听） =====")]
    public UnityEvent OnShoot;       // 每次开枪触发
    public UnityEvent OnHit;         // 命中目标时触发
    public UnityEvent OnDryFire;     // 没子弹时触发（当前代码未使用，可自行扩展）

    // ===== 内部状态 =====
    private float _nextFireTime;                   // 下一次可以开枪的时间
    private Camera _mainCamera;                    // 主摄像机缓存
    private System.Collections.Generic.List<AudioSource> _activeGunfireSources
        = new System.Collections.Generic.List<AudioSource>();  // 正在播放的枪声音效列表

    // ===== 公开属性（供 UI 读取） =====
    public bool CanFire => Time.time >= _nextFireTime;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    /// <summary>
    /// 尝试开枪。
    /// 由 AimState（或其他状态）调用。
    /// </summary>
    /// <returns>true = 成功开枪，false = 被射速限制或其他原因阻止</returns>
    public bool TryShoot()
    {
        // ----- 1. 射速限制 -----
        if (Time.time < _nextFireTime)
            return false;

        // ----- 2. 确定射击起点与方向 -----
        Vector3 shootOrigin;
        Vector3 shootDirection;

        if (muzzlePoint != null)
        {
            shootOrigin = muzzlePoint.position;
        }
        else if (_mainCamera != null)
        {
            shootOrigin = _mainCamera.transform.position;
        }
        else
        {
            shootOrigin = transform.position;
        }

        if (aimTarget != null)
        {
            shootDirection = (aimTarget.position - shootOrigin).normalized;
        }
        else if (_mainCamera != null)
        {
            shootDirection = _mainCamera.transform.forward;
        }
        else
        {
            shootDirection = transform.forward;
        }

        // ----- 3. 施加散布 -----
        shootDirection = ApplySpread(shootDirection);

        // ----- 4. 更新下一次可射击时间 -----
        _nextFireTime = Time.time + (1f / fireRate);

        // ----- 5. 播放枪口特效 & 音效 -----
        PlayMuzzleEffects();

        // ----- 6. 射线检测 -----
        Ray ray = new Ray(shootOrigin, shootDirection);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxRange, targetLayer))
        {
            Vector3 hitPoint = hit.point;
            Vector3 hitNormal = hit.normal;

            DealDamage(hit.collider, hitPoint, damage);
            PlayHitEffect(hitPoint, hitNormal);
            DrawBulletTrail(shootOrigin, hitPoint);

            OnHit?.Invoke();

            Debug.Log($"[武器] 命中: {hit.collider.name} | 伤害: {damage} | 距离: {hit.distance:F1}m");
        }
        else
        {
            Vector3 missPoint = shootOrigin + shootDirection * maxRange;
            DrawBulletTrail(shootOrigin, missPoint);
            Debug.Log($"[武器] 打空了 | 方向: {shootDirection}");
        }

        OnShoot?.Invoke();
        return true;
    }

    /// <summary>
    /// 在基础方向上叠加随机散布
    /// </summary>
    private Vector3 ApplySpread(Vector3 baseDirection)
    {
        if (spreadAngle <= 0f)
            return baseDirection;

        float halfAngle = spreadAngle * 0.5f * Mathf.Deg2Rad;
        Vector3 spreadOffset = Random.insideUnitSphere * Mathf.Tan(halfAngle);

        Vector3 result = baseDirection + spreadOffset;
        return result.normalized;
    }

    /// <summary>
    /// 对命中的目标造成伤害
    /// </summary>
    private void DealDamage(Collider targetCollider, Vector3 hitPoint, float dmg)
    {
        var damageable = targetCollider.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(dmg, hitPoint);
            return;
        }

        damageable = targetCollider.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(dmg, hitPoint);
            return;
        }

        targetCollider.SendMessageUpwards("TakeDamage", dmg, SendMessageOptions.DontRequireReceiver);
    }

    /// <summary>
    /// 播放枪口火焰 / 音效
    /// </summary>
    private void PlayMuzzleEffects()
    {
        if (muzzleFlash != null)
            muzzleFlash.Play();

        if (gunfireARC != null)
        {
            // 清理已播放完毕的 AudioSource，避免列表无限增长
            _activeGunfireSources.RemoveAll(s => s == null || !s.isPlaying);

            // 每次开枪创建一个临时 AudioSource，支持连发时音效叠加
            GameObject tempGO = new GameObject("GunfireSFX_temp");
            tempGO.transform.position = muzzlePoint != null
                ? muzzlePoint.position
                : transform.position;
            tempGO.transform.parent = transform;

            AudioSource tempSource = tempGO.AddComponent<AudioSource>();
            tempSource.spatialBlend = 1f;
            tempSource.volume = gunfireVolume;
            tempSource.resource = gunfireARC;

            // 应用 Audio Mixer Group（带 Reverb 效果的总线）
            if (outputAudioMixerGroup != null)
            {
                tempSource.outputAudioMixerGroup = outputAudioMixerGroup;
            }

            // 添加混响滤镜，模拟环境回音 / 尾音
            if (reverbLevel > 0.001f)
            {
                AudioReverbFilter reverbFilter = tempGO.AddComponent<AudioReverbFilter>();
                reverbFilter.reverbPreset = AudioReverbPreset.Generic;
                // 用 dryLevel 控制干湿比：reverbLevel 越大，干声越少、湿声越多
                reverbFilter.dryLevel = Mathf.Lerp(0f, -2000f, reverbLevel);
                reverbFilter.room = Mathf.Lerp(0f, 0f, reverbLevel);        // room 保持 0，靠 decay 模拟回音
                reverbFilter.decayTime = Mathf.Lerp(0.2f, 2.5f, reverbLevel);
                reverbFilter.reflectionsLevel = Mathf.Lerp(-10000f, 200f, reverbLevel);
            }

            tempSource.Play();

            // 加入追踪列表
            _activeGunfireSources.Add(tempSource);

            // 播放完毕后自动销毁（给足时间让混响尾音自然衰减）
            float lifetime = Mathf.Max(3f, gunfireFadeOutDuration + 2f + reverbLevel * 3f);
            Destroy(tempGO, lifetime);
        }
    }

    /// <summary>
    /// 停止所有正在播放的枪声音效（带淡出，避免戛然而止）。
    /// 由 AimState 在松开左键时调用。
    /// </summary>
    public void StopFiringEffects()
    {
        // 对每个正在播放的 AudioSource 启动淡出协程
        foreach (var source in _activeGunfireSources)
        {
            if (source != null && source.isPlaying)
            {
                StartCoroutine(FadeOutAndDestroy(source, gunfireFadeOutDuration));
            }
        }
        _activeGunfireSources.Clear();

        // 同时停止枪口火焰（火焰可以立即停，视觉上没问题）
        if (muzzleFlash != null)
            muzzleFlash.Stop();
    }

    /// <summary>
    /// 平滑降低音量 → 停止播放，模拟枪声在环境中的自然衰减 / 回音尾音。
    /// </summary>
    private IEnumerator FadeOutAndDestroy(AudioSource source, float duration)
    {
        if (source == null || !source.isPlaying)
            yield break;

        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < duration && source != null && source.isPlaying)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // 使用 ease-out 曲线：开始衰减慢（保留尾音），后期加快
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            source.volume = Mathf.Lerp(startVolume, 0f, easedT);
            yield return null;
        }

        // 淡出完成后彻底停止并清理
        if (source != null)
        {
            source.Stop();
            // 销毁挂载的临时 GameObject
            if (source.gameObject.name.Contains("GunfireSFX_temp"))
            {
                Destroy(source.gameObject);
            }
        }
    }

    /// <summary>
    /// 在销毁时清理所有残留音效（直接硬停止，因为即将销毁，淡出无意义）
    /// </summary>
    private void OnDestroy()
    {
        foreach (var source in _activeGunfireSources)
        {
            if (source != null)
                source.Stop();
        }
        _activeGunfireSources.Clear();

        if (muzzleFlash != null)
            muzzleFlash.Stop();
    }

    /// <summary>
    /// 在命中点生成命中特效
    /// </summary>
    private void PlayHitEffect(Vector3 point, Vector3 normal)
    {
        if (hitEffectPrefab == null)
            return;

        Quaternion rotation = Quaternion.LookRotation(normal);
        GameObject effect = Instantiate(hitEffectPrefab, point, rotation);
        Destroy(effect, 5f);
    }

    /// <summary>
    /// 绘制子弹飞行轨迹
    /// </summary>
    private void DrawBulletTrail(Vector3 from, Vector3 to)
    {
        if (bulletTrailPrefab != null)
        {
            // TODO: 实现自定义弹道拖尾
            // GameObject trail = Instantiate(bulletTrailPrefab, from, Quaternion.identity);
        }

        Debug.DrawLine(from, to, Color.yellow, 0.3f);
    }

    // ===== Scene 视图调试 =====
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 origin = muzzlePoint != null ? muzzlePoint.position : 
                        (_mainCamera != null ? _mainCamera.transform.position : transform.position);
        Vector3 dir = muzzlePoint != null ? muzzlePoint.forward : 
                     (_mainCamera != null ? _mainCamera.transform.forward : transform.forward);

        Gizmos.DrawRay(origin, dir * maxRange);
    }
}