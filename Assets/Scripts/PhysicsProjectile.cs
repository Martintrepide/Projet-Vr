using UnityEngine;

// Balle physique. En plus des triggers, chaque pas physique est vérifié par un rayon :
// les balles rapides ne traversent plus les murs fins, et l'impact est placé au bon endroit.
[RequireComponent(typeof(Rigidbody))]
public class PhysicsProjectile : Projectile
{
    [SerializeField] private float lifeTime;
    [SerializeField] private bool showTrail = true;
    private Rigidbody rigidBody;
    private bool consumed;
    private Vector3 lastPosition;

    private void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();
    }

    public override void Init(Weapon weapon)
    {
        base.Init(weapon);
        lastPosition = transform.position;
        if (showTrail)
            ProceduralFx.AddTrail(gameObject, new Color(1f, 0.85f, 0.5f, 0.9f), 0.012f, 0.06f);
        Destroy(gameObject, lifeTime > 0f ? lifeTime : 2f);
    }

    public override void Launch()
    {
        base.Launch();
        rigidBody.AddRelativeForce(Vector3.forward * weapon.GetShootingForce(), ForceMode.Impulse);
    }

    private void FixedUpdate()
    {
        if (consumed) return;
        Vector3 current = rigidBody.position;
        Vector3 d = current - lastPosition;
        float len = d.magnitude;
        if (len > 0.0001f)
        {
            RaycastHit[] hits = Physics.RaycastAll(lastPosition, d / len, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in hits)
                if (TryHit(h.collider, h.point, h.normal)) return;
        }
        lastPosition = current;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHit(other, transform.position, -transform.forward);
    }

    private bool TryHit(Collider other, Vector3 point, Vector3 normal)
    {
        if (consumed || other == null) return false;
        if (CombatUtils.IgnoredByShots(other)) return false;
        ITakeDamage[] damageTakers = other.GetComponentsInParent<ITakeDamage>();
        if (other.isTrigger && damageTakers.Length == 0) return false;

        consumed = true;
        if (damageTakers.Length == 0)
            ProceduralFx.Impact(point, normal);
        foreach (ITakeDamage taker in damageTakers)
            taker.TakeDamage(weapon, this, point);
        Destroy(gameObject);
        return true;
    }
}
