using System;
using UnityEngine;

// Arme semi-automatique : une balle par pression de gâchette.
// Par défaut le tir est instantané (rayon) : l'impact et le sang apparaissent dès le tir.
// Décochez "hitscan" pour revenir aux balles physiques (bulletPrefab).
public class Pistol : Weapon
{
    [SerializeField] protected Projectile bulletPrefab;
    [SerializeField] protected bool hitscan = true;
    [SerializeField] protected float range = 150f;

    protected override void OnTriggerPulled()
    {
        TryFire();
    }

    protected void TryFire()
    {
        if (TryConsumeAmmo())
            Shoot();
    }

    protected override void Shoot()
    {
        base.Shoot();
        FireProjectile(bulletSpawn.rotation);
    }

    protected void FireProjectile(Quaternion rotation)
    {
        if (!hitscan)
        {
            if (bulletPrefab == null)
            {
                Debug.LogWarning(name + " : aucun bulletPrefab assigné.", this);
                return;
            }
            Projectile projectileInstance = Instantiate(bulletPrefab, bulletSpawn.position, rotation);
            projectileInstance.Init(this);
            projectileInstance.Launch();
            return;
        }

        Vector3 origin = bulletSpawn.position;
        Vector3 dir = rotation * Vector3.forward;
        Vector3 end = origin + dir * range;

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            Collider c = h.collider;
            if (c.transform.IsChildOf(transform)) continue;
            if (CombatUtils.IgnoredByShots(c)) continue;
            ITakeDamage[] takers = c.GetComponentsInParent<ITakeDamage>();
            if (c.isTrigger && takers.Length == 0) continue;

            end = h.point;
            if (takers.Length == 0)
                ProceduralFx.Impact(h.point, h.normal);
            foreach (ITakeDamage t in takers)
                t.TakeDamage(this, null, h.point);
            break;
        }

        ProceduralFx.Tracer(origin, end, new Color(1f, 0.85f, 0.5f, 0.9f), 0.012f, 0.05f);
    }
}
