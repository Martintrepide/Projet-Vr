using UnityEngine;

// Fusil à pompe : plusieurs plombs avec dispersion, cadence lente.
public class Shotgun : Pistol
{
    [SerializeField] private int pelletsPerShot = 8;
    [SerializeField] private float spreadAngle = 6f;

    protected override void Awake()
    {
        base.Awake();
        fireCooldown = Mathf.Max(fireCooldown, 0.7f);
    }

    protected override void Shoot()
    {
        PlayShotEffects();
        for (int i = 0; i < pelletsPerShot; i++)
        {
            Vector2 offset = Random.insideUnitCircle * spreadAngle;
            Quaternion spread = Quaternion.Euler(offset.y, offset.x, 0f);
            FireProjectile(bulletSpawn.rotation * spread);
        }
    }

    protected override AudioClip DefaultShotSound()
    {
        return GameSfx.Shotgun;
    }
}
