using UnityEngine;

public static class CombatUtils
{
    // Colliders que les tirs traversent : joueur, armes, balles, zones de téléportation (plans invisibles au-dessus du sol).
    public static bool IgnoredByShots(Collider c)
    {
        if (c.GetComponentInParent<Player>() != null) return true;
        if (c.GetComponentInParent<Weapon>() != null) return true;
        if (c.GetComponentInParent<Projectile>() != null) return true;
        if (c.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>() != null) return true;
        return false;
    }

    // Premier obstacle entre deux points (en ignorant joueur, armes, ennemis, balles, triggers et la racine "ignoreRoot").
    public static bool FirstObstacle(Vector3 from, Vector3 to, Transform ignoreRoot, out RaycastHit obstacle)
    {
        obstacle = default(RaycastHit);
        Vector3 d = to - from;
        float len = d.magnitude;
        if (len < 0.001f) return false;
        RaycastHit[] hits = Physics.RaycastAll(from, d / len, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        foreach (RaycastHit h in hits)
        {
            Collider c = h.collider;
            if (ignoreRoot != null && c.transform.IsChildOf(ignoreRoot)) continue;
            if (IgnoredByShots(c)) continue;
            if (c.GetComponentInParent<EnemyAI1>() != null) continue;
            if (h.distance < best)
            {
                best = h.distance;
                obstacle = h;
                found = true;
            }
        }
        return found;
    }

    public static bool HasLineOfSight(Vector3 from, Vector3 to, Transform ignoreRoot)
    {
        RaycastHit unused;
        return !FirstObstacle(from, to, ignoreRoot, out unused);
    }
}
