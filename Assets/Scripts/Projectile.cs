using UnityEngine;

public class Projectile : MonoBehaviour
{
    protected Weapon weapon;

    public virtual void Init(Weapon weapon)
    {
        this.weapon = weapon;
    }

    public virtual void Launch()
    {

    }
}