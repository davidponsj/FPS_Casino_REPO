using UnityEngine;

/// <summary>
/// Pistola: disparo semi-automático de un único raycast por pulsación.
/// El raycast sale del centro de la cámara (no del muzzlePoint) para que la dispersión
/// se calcule respecto a hacia dónde miras de verdad; el muzzlePoint se usa solo para
/// el origen visual del trazador y el fogonazo.
/// </summary>
public class PistolWeapon : WeaponBase
{
    [Header("Pistola")]
    [SerializeField] private float damage = 25f;

    [Header("Efectos")]
    [SerializeField] private BulletTracerEffect tracerPrefab;
    [SerializeField] private GameObject impactEffectPrefab;
    [SerializeField] private MuzzleFlash muzzleFlash;

    protected override void Fire()
    {
        Vector3 origin = playerCamera.transform.position;
        Vector3 direction = ApplySpread(playerCamera.transform.forward);

        Vector3 tracerEndPoint;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(damage);

            if (impactEffectPrefab != null)
                Instantiate(impactEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));

            tracerEndPoint = hit.point;
        }
        else
        {
            tracerEndPoint = origin + direction * range;
        }

        SpawnTracer(tracerEndPoint);
        muzzleFlash?.Flash();
    }

    private void SpawnTracer(Vector3 endPoint)
    {
        if (tracerPrefab == null || muzzlePoint == null) return;

        BulletTracerEffect tracer = Instantiate(tracerPrefab, muzzlePoint.position, Quaternion.identity);
        tracer.Play(muzzlePoint.position, endPoint);
    }
}