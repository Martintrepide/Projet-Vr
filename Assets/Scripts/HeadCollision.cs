using UnityEngine;

// Empêche la tête de traverser les murs quand le joueur se déplace physiquement (ou avec le simulateur) :
// si la caméra rentre dans un collider, le XR Origin est repoussé horizontalement.
// À placer sur la racine du XR Origin.
public class HeadCollision : MonoBehaviour
{
    [SerializeField] private Transform head;
    [SerializeField] private float radius = 0.18f;
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private float teleportThreshold = 1.0f; // au-delà, on considère que c'est une téléportation

    private SphereCollider probe;
    private readonly Collider[] overlaps = new Collider[24];
    private readonly RaycastHit[] hits = new RaycastHit[24];
    private Vector3 lastHead;
    private bool hasLast;

    private void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        GameObject go = new GameObject("Head Collision Probe");
        go.layer = 2; // Ignore Raycast : invisible pour le rayon de téléportation et les tirs
        probe = go.AddComponent<SphereCollider>();
        probe.isTrigger = true;
        probe.radius = radius;
    }

    private void OnDestroy()
    {
        if (probe != null) Destroy(probe.gameObject);
    }

    private void LateUpdate()
    {
        if (head == null) return;
        Vector3 p = head.position;

        if (!hasLast || (p - lastHead).magnitude > teleportThreshold)
        {
            lastHead = p;
            hasLast = true;
            return;
        }

        Vector3 correction = Vector3.zero;

        // 1. Mouvement rapide : on bloque au premier mur rencontré (évite de traverser les murs fins).
        Vector3 move = p - lastHead;
        float dist = move.magnitude;
        if (dist > 0.001f)
        {
            int n = Physics.SphereCastNonAlloc(lastHead, radius * 0.9f, move / dist, hits, dist, collisionMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (Ignore(hits[i].collider) || hits[i].distance <= 0f) continue;
                if (hits[i].distance < best) best = hits[i].distance;
            }
            if (best < float.MaxValue)
            {
                Vector3 allowed = lastHead + move / dist * Mathf.Max(0f, best - 0.01f);
                correction = allowed - p;
            }
        }

        // 2. Résolution des chevauchements restants.
        Vector3 resolved = p + correction;
        int count = Physics.OverlapSphereNonAlloc(resolved, radius, overlaps, collisionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = overlaps[i];
            if (Ignore(c)) continue;
            Vector3 dir;
            float depth;
            if (Physics.ComputePenetration(probe, resolved, Quaternion.identity, c, c.transform.position, c.transform.rotation, out dir, out depth))
            {
                resolved += dir * depth;
                correction += dir * depth;
            }
        }

        correction.y = 0f;
        if (correction.sqrMagnitude > 1e-8f)
        {
            transform.position += correction;
            p += correction;
        }
        lastHead = p;
        probe.transform.position = p;
    }

    private bool Ignore(Collider c)
    {
        if (c == null || c == probe || c.isTrigger) return true;
        if (c.transform.IsChildOf(transform)) return true;           // le rig lui-même
        if (c.GetComponentInParent<Weapon>() != null) return true;    // armes tenues
        if (c.GetComponentInParent<Projectile>() != null) return true;
        if (c.GetComponentInParent<EnemyAI1>() != null) return true;
        if (c.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>() != null) return true; // plans de téléportation
        return false;
    }
}
