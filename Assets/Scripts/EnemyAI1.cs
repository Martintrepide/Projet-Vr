using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// IA ennemie : rejoint un point de couverture, s'y abrite, puis tire en rafales sur le joueur.
// Fonctionne avec ou sans Animator Controller :
// - avec contrôleur : déclencheurs Run / Crouch / Shoot, et Shoot() appelé par un Animation Event ;
// - sans contrôleur : les tirs sont cadencés par le script (shotInterval).
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI1 : MonoBehaviour, ITakeDamage
{
    const string RUN_TRIGGER = "Run";
    const string CROUCH_TRIGGER = "Crouch";
    const string SHOOT_TRIGGER = "Shoot";

    [SerializeField] private float startingHealth = 100f;
    [SerializeField] private float minTimeUnderCover = 2f;
    [SerializeField] private float maxTimeUnderCover = 4f;
    [SerializeField] private int minShotsToTake = 3;
    [SerializeField] private int maxShotsToTake = 6;
    [SerializeField] private float rotationSpeed = 240f;
    [SerializeField] private float damage = 8f;
    [Range(0, 100)]
    [SerializeField] private float shootingAccuracy = 35f;

    [SerializeField] private Transform shootingPosition;
    [SerializeField] private ParticleSystem bloodSplatterFX;

    [Header("Ajouts")]
    [SerializeField] private float shotInterval = 0.5f;
    [SerializeField] private GameObject muzzleFlashFX;
    [SerializeField] private bool showTracers = true;
    [SerializeField] private int scoreValue = 100;

    public event Action<EnemyAI1> Died;
    // Fourni par le spawner : renvoie un nouvel abri d'où le joueur est visible.
    public Func<EnemyAI1, Transform> CoverProvider;
    public int ScoreValue => scoreValue;
    public bool IsDead { get; private set; }

    private bool isShooting;
    private bool reachedCover;
    private int currentShotsTaken;
    private int currentMaxShotsToTake;
    private NavMeshAgent agent;
    private Player player;
    private Transform occupiedCoverSpot;
    private Animator animator;
    private bool hasAnimController;
    private Coroutine combatRoutine;
    private Collider[] ownColliders;
    private int blockedShots;

    private float _health;
    public float health
    {
        get { return _health; }
        set { _health = Mathf.Clamp(value, 0, startingHealth); }
    }

    public Transform CoverSpot => occupiedCoverSpot;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        hasAnimController = animator != null && animator.runtimeAnimatorController != null;
        agent = GetComponent<NavMeshAgent>();
        ownColliders = GetComponentsInChildren<Collider>();
        if (startingHealth <= 0f) startingHealth = 100f;
        _health = startingHealth;
        if (shootingPosition == null)
        {
            GameObject sp = new GameObject("ShootingPosition (auto)");
            sp.transform.SetParent(transform, false);
            sp.transform.localPosition = new Vector3(0.25f, 1.4f, 0.5f);
            shootingPosition = sp.transform;
        }
        SetTrigger(RUN_TRIGGER);
    }

    private void Start()
    {
        // Ennemi placé à la main dans la scène, sans passer par le spawner.
        if (player == null)
        {
            Player p = FindAnyObjectByType<Player>();
            if (p != null)
            {
                GameObject spot = new GameObject(name + " Cover (auto)");
                spot.transform.position = transform.position;
                Init(p, spot.transform);
            }
        }
    }

    public void Init(Player player, Transform coverSpot)
    {
        occupiedCoverSpot = coverSpot;
        this.player = player;
        EnsureOnNavMesh();
        GetToCover();
    }

    // Applique la difficulté de la vague (appelé par le spawner).
    public void ScaleDifficulty(float damageMultiplier, float accuracyBonus)
    {
        damage *= damageMultiplier;
        shootingAccuracy = Mathf.Clamp(shootingAccuracy + accuracyBonus, 0f, 95f);
    }

    private void EnsureOnNavMesh()
    {
        if (agent.isOnNavMesh) return;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
            agent.Warp(hit.position);
        else
            Debug.LogWarning(name + " : aucun NavMesh à proximité. Lancez VR Shooter > Configurer la scène.", this);
    }

    private void GetToCover()
    {
        if (!agent.isOnNavMesh || occupiedCoverSpot == null) return;
        reachedCover = false;
        agent.isStopped = false;
        agent.SetDestination(occupiedCoverSpot.position);
    }

    private void Update()
    {
        if (IsDead || player == null) return;

        if (!reachedCover && agent.isOnNavMesh && !agent.pathPending)
        {
            bool arrived = agent.remainingDistance <= agent.stoppingDistance + 0.3f;
            bool stuck = agent.hasPath == false && agent.velocity.sqrMagnitude < 0.01f;
            if (arrived || stuck)
            {
                reachedCover = true;
                agent.isStopped = true;
                combatRoutine = StartCoroutine(InitializeShootingCO());
            }
        }

        if (isShooting || reachedCover)
            RotateTowardsPlayer();
    }

    private IEnumerator InitializeShootingCO()
    {
        while (!IsDead)
        {
            HideBehindCover();
            yield return new WaitForSeconds(UnityEngine.Random.Range(minTimeUnderCover, maxTimeUnderCover));
            StartShooting();

            if (hasAnimController)
                yield break; // la suite est pilotée par les Animation Events (Shoot)

            for (int i = 0; i < currentMaxShotsToTake && !IsDead; i++)
            {
                FireAtPlayer();
                yield return new WaitForSeconds(shotInterval * UnityEngine.Random.Range(0.8f, 1.2f));
            }
            isShooting = false;
        }
    }

    private void HideBehindCover()
    {
        isShooting = false;
        SetTrigger(CROUCH_TRIGGER);
    }

    private void StartShooting()
    {
        isShooting = true;
        currentMaxShotsToTake = UnityEngine.Random.Range(minShotsToTake, maxShotsToTake + 1);
        currentShotsTaken = 0;
        SetTrigger(SHOOT_TRIGGER);
    }

    // Appelé par un Animation Event quand un contrôleur d'animation est utilisé.
    public void Shoot()
    {
        if (IsDead) return;
        FireAtPlayer();
        currentShotsTaken++;
        if (hasAnimController && currentShotsTaken >= currentMaxShotsToTake)
            combatRoutine = StartCoroutine(InitializeShootingCO());
    }

    private void FireAtPlayer()
    {
        if (player == null || player.IsDead) return;
        if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

        Vector3 origin = shootingPosition.position;
        Vector3 target = player.GetHeadPosition() + Vector3.down * 0.25f;

        // Pas de ligne de vue : pas de tir. Après plusieurs essais, l'ennemi change d'abri.
        if (!CombatUtils.HasLineOfSight(origin, target, transform))
        {
            blockedShots++;
            if (blockedShots >= 3)
            {
                blockedShots = 0;
                Relocate();
            }
            return;
        }
        blockedShots = 0;

        bool hit = UnityEngine.Random.Range(0f, 100f) < shootingAccuracy;
        if (hit)
            player.TakeDamage(damage);

        // Tir raté : la balle part à côté de la tête et s'arrête sur le premier obstacle.
        Vector3 end = target;
        if (!hit)
        {
            Vector3 dir = (target - origin).normalized;
            Vector3 missDir = (dir + UnityEngine.Random.insideUnitSphere * 0.08f).normalized;
            end = origin + missDir * 60f;
            RaycastHit obstacle;
            if (CombatUtils.FirstObstacle(origin, end, transform, out obstacle))
            {
                end = obstacle.point;
                ProceduralFx.Impact(obstacle.point, obstacle.normal);
            }
        }

        Vector3 toTarget = target - origin;
        ProceduralFx.MuzzleFlash(origin, toTarget.normalized, 0.15f);
        if (muzzleFlashFX != null)
            Destroy(Instantiate(muzzleFlashFX, origin, Quaternion.LookRotation(toTarget)), 1f);
        GameSfx.Play(GameSfx.EnemyShot, origin, 0.8f);
        if (showTracers)
            ProceduralFx.Tracer(origin, end, new Color(1f, 0.6f, 0.25f, 1f), 0.02f, 0.07f);
    }

    // Change d'abri (ou se rapproche du joueur si aucun abri n'est disponible).
    private void Relocate()
    {
        if (IsDead || !agent.isOnNavMesh) return;
        Transform next = CoverProvider != null ? CoverProvider(this) : null;
        if (next != null)
        {
            occupiedCoverSpot = next;
        }
        else
        {
            Vector3 toward = Vector3.Lerp(transform.position, player.transform.position, 0.4f);
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(toward, out hit, 4f, NavMesh.AllAreas)) return;
            if (occupiedCoverSpot == null) occupiedCoverSpot = new GameObject(name + " Cover (auto)").transform;
            occupiedCoverSpot.position = hit.position;
        }
        if (combatRoutine != null) StopCoroutine(combatRoutine);
        isShooting = false;
        SetTrigger(RUN_TRIGGER);
        GetToCover();
    }

    private void RotateTowardsPlayer()
    {
        Vector3 direction = player.GetHeadPosition() - transform.position;
        direction.y = 0;
        if (direction.sqrMagnitude < 0.0001f) return;
        Quaternion rotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, rotationSpeed * Time.deltaTime);
    }

    public void TakeDamage(Weapon weapon, Projectile projectile, Vector3 contactPoint)
    {
        if (IsDead) return;
        health -= weapon != null ? weapon.GetDamage() : 25f;

        Vector3 from = weapon != null ? weapon.transform.position : contactPoint - transform.forward;
        ProceduralFx.Blood(contactPoint, from - contactPoint);
        if (bloodSplatterFX != null)
        {
            ParticleSystem effect = Instantiate(bloodSplatterFX, contactPoint, Quaternion.LookRotation(from - contactPoint));
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = effect.main;
            main.startDelay = 0f;               // pas de délai avant l'apparition du sang
            effect.Play(true);
            Destroy(effect.gameObject, 2f);
        }

        if (health <= 0)
            Die();
    }

    private void Die()
    {
        IsDead = true;
        isShooting = false;
        StopAllCoroutines();
        if (agent.isOnNavMesh) agent.isStopped = true;
        agent.enabled = false;
        foreach (Collider c in ownColliders) c.enabled = false;
        if (animator != null) animator.enabled = false;
        Died?.Invoke(this);
        StartCoroutine(FallCO());
    }

    private IEnumerator FallCO()
    {
        Quaternion start = transform.rotation;
        Quaternion end = start * Quaternion.Euler(-90f, 0f, 0f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.35f;
            transform.rotation = Quaternion.Slerp(start, end, t);
            yield return null;
        }
        yield return new WaitForSeconds(2f);
        Destroy(gameObject);
    }

    private void SetTrigger(string trigger)
    {
        if (hasAnimController)
            animator.SetTrigger(trigger);
    }
}
