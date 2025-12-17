using UnityEngine;

[System.Serializable]
public class WeaponData
{
    public string bulletType = "Bullet";
    public float fireRate = 1f;
    public float range = 8f;
    public int damage = 1;
}

public class WeaponComponent : MonoBehaviour, IWeapon
{
    [Header("Weapon Settings")]
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private Transform firePoint;

    private float lastFireTime = 0f;

    void Awake()
    {
        if (firePoint == null)
        {
            // FirePoint가 없으면 자동 생성
            GameObject firePointObj = new GameObject("FirePoint");
            firePointObj.transform.SetParent(transform);
            firePointObj.transform.localPosition = Vector3.forward;
            firePoint = firePointObj.transform;
        }
    }

    public void Fire(Vector3 startPosition, Transform target, BulletOwner owner, object shooter = null)
    {
        if (!CanFire())
        {
#if !PLAYABLE_AD
            Debug.Log($"{gameObject.name} cannot fire yet. Time since last fire: {Time.time - lastFireTime}, Fire rate: {weaponData.fireRate}");
#endif
            return;
        }

        GameObject bullet = ObjectPool.Instance.SpawnFromPool(weaponData.bulletType, startPosition, firePoint.rotation);

        if (bullet != null)
        {
            BulletController bulletController = bullet.GetComponent<BulletController>();
            if (bulletController != null)
            {
                if (owner == BulletOwner.Turret && shooter is TurretController turret)
                {
                    bulletController.SetTarget(target, owner, turret);
                }
                else
                {
                    bulletController.SetTarget(target, owner);
                }
#if !PLAYABLE_AD
                Debug.Log($"{gameObject.name} fired bullet at {target.name}");
#endif
            }
        }
        else
        {
#if !PLAYABLE_AD
            Debug.LogWarning($"Failed to spawn bullet: {weaponData.bulletType}");
#endif
        }

        lastFireTime = Time.time;
    }

    public bool CanFire()
    {
        float timeSinceLastFire = Time.time - lastFireTime;
        return timeSinceLastFire >= weaponData.fireRate;
    }

    public float GetFireRate()
    {
        return weaponData.fireRate;
    }

    public float GetRange()
    {
        return weaponData.range;
    }

    public string GetBulletType()
    {
        return weaponData.bulletType;
    }

    public Transform GetFirePoint()
    {
        return firePoint;
    }
#if !PLAYABLE_AD
    public void SetWeaponData(WeaponData newData)
    {
        weaponData = newData;
    }
#endif
}