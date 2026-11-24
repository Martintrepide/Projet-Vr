using System.Collections;
using UnityEngine;

// Fusil automatique : tire en continu tant que la gâchette est enfoncée.
public class Rifle : Pistol
{
    [SerializeField] private float roundsPerSecond = 9f;
    private Coroutine fireLoop;

    protected override void Awake()
    {
        base.Awake();
        fireCooldown = 0f;
    }

    protected override void OnTriggerPulled()
    {
        if (fireLoop == null)
            fireLoop = StartCoroutine(FireLoop());
    }

    protected override void OnTriggerReleased()
    {
        if (fireLoop != null)
        {
            StopCoroutine(fireLoop);
            fireLoop = null;
        }
    }

    private IEnumerator FireLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(1f / Mathf.Max(1f, roundsPerSecond));
        while (TriggerHeld)
        {
            TryFire();
            if (CurrentAmmo <= 0)
                break;
            yield return wait;
        }
        fireLoop = null;
    }
}
