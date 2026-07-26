using UnityEngine;

/// <summary>
/// 可受伤害接口。
/// 任何需要被子弹打中的物体（敌人、箱子、油桶等）实现此接口即可。
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// 受到伤害
    /// </summary>
    /// <param name="amount">伤害数值</param>
    /// <param name="hitPoint">命中点（世界坐标）</param>
    void TakeDamage(float amount, Vector3 hitPoint);
}
