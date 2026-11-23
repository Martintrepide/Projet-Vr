using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// =====================================================================
//  EnemyAI – version SANS animations (on les ajoutera plus tard)
//  L'ennemi court vers son abri, attend, puis tire des rafales sur le
//  joueur (tir par minuterie), et recommence. Il perd des PV quand on
//  lui tire dessus (interface ITakeDamage de la partie 1).
// =====================================================================
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI1 : MonoBehaviour, ITakeDamage
{
    // Noms des Triggers de l'Animator (servira quand il y aura les animations)
    const string RUN_TRIGGER = "Run";
    const string CROUCH_TRIGGER = "Crouch";
    const string SHOOT_TRIGGER = "Shoot";

    [Header("Vie")]
    [SerializeField] private float startingHealth = 100f;

    [Header("Abri (secondes d'attente)")]
    [SerializeField] private float minTimeUnderCover = 3f;
    [SerializeField] private float maxTimeUnderCover = 6f;

    [Header("Tir")]
    [SerializeField] private int minShotsToTake = 3;
    [SerializeField] private int maxShotsToTake = 6;
    [Tooltip("Secondes entre deux tirs d'une rafale")]
    [SerializeField] private float timeBetweenShots = 0.6f;
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private float damage = 10f;
    [Range(0, 100)]
    [SerializeField] private float shootingAccuracy = 50f;
    [Tooltip("Décoché = tir automatique par minuterie (pas d'animations). " +
             "Coché = tir déclenché par l'Animation Event « Shoot » (quand il y aura les animations).")]
    [SerializeField] private bool shootWithAnimationEvent = false;

    [Header("Références")]
    [Tooltip("Objet vide au bout du canon de l'arme de l'ennemi")]
    [SerializeField] private Transform shootingPosition;
    [Tooltip("Prefab FX_BloodSplatter (Simple FX) – facultatif")]
    [SerializeField] private ParticleSystem bloodSplatterFX;
    [Tooltip("Prefab FX_GunFlash (Simple FX) – facultatif, flash au canon à chaque tir")]
    [SerializeField] private ParticleSystem muzzleFlashFX;

    [Header("Trait de tir visible (tracer)")]
    [Tooltip("Matériau du trait. Vide = matériau URP Unlit créé automatiquement (suffit pour tester dans l'éditeur).")]
    [SerializeField] private Material tracerMaterial;
    [SerializeField] private Color tracerColor = Color.yellow;
    [SerializeField] private float tracerWidth = 0.02f;
    [Tooltip("Durée d'affichage du trait, en secondes")]
    [SerializeField] private float tracerDuration = 0.08f;

    [Header("Déplacement")]
    [Tooltip("Distance à partir de laquelle l'ennemi est considéré arrivé à son abri")]
    [SerializeField] private float arrivalTolerance = 0.5f;

    private bool isShooting;
    private bool isDead;
    private int currentShotsTaken;
    private int currentMaxShotsToTake;
    private NavMeshAgent agent;
    private Player player;
    private Transform occupiedCoverSpot;
    private Animator animator;
    private LineRenderer tracer;

    private float _health;
    public float health
    {
        get { return _health; }
        set { _health = Mathf.Clamp(value, 0, startingHealth); }
    }

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        _health = startingHealth;
        SetAnimTrigger(RUN_TRIGGER);
        CreateTracer();
    }

    // Appelée par l'EnemySpawner juste après l'apparition de l'ennemi
    public void Init(Player player, Transform coverSpot)
    {
        this.player = player;
        occupiedCoverSpot = coverSpot;
        GetToCover();
    }

    private void GetToCover()
    {
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning(name + " n'est pas sur le NavMesh : as-tu fait le Bake, et le spawner est-il sur la zone bleue ?", this);
            return;
        }
        agent.isStopped = false;
        agent.SetDestination(occupiedCoverSpot.position);
    }

    private void Update()
    {
        // Ennemi pas encore initialisé par le spawner (ou mort) : on ne fait rien
        if (isDead || player == null || occupiedCoverSpot == null || !agent.isOnNavMesh)
            return;

        // Arrivé à l'abri ?
        if (!agent.isStopped && !agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance + arrivalTolerance)
        {
            agent.isStopped = true;
            StartCoroutine(InitializeShootingCO());
        }

        if (isShooting)
        {
            RotateTowardsPlayer();
        }
    }

    private IEnumerator InitializeShootingCO()
    {
        HideBehindCover();
        yield return new WaitForSeconds(Random.Range(minTimeUnderCover, maxTimeUnderCover));
        StartShooting();
    }

    private void HideBehindCover()
    {
        isShooting = false;
        SetAnimTrigger(CROUCH_TRIGGER);
    }

    private void StartShooting()
    {
        isShooting = true;
        currentMaxShotsToTake = Random.Range(minShotsToTake, maxShotsToTake + 1); // max inclus
        currentShotsTaken = 0;
        SetAnimTrigger(SHOOT_TRIGGER);

        if (!shootWithAnimationEvent)
            StartCoroutine(ShootingBurstCO());
    }

    // Tir par minuterie, tant que l'on est en phase de tir
    private IEnumerator ShootingBurstCO()
    {
        while (isShooting && !isDead)
        {
            Shoot();
            yield return new WaitForSeconds(timeBetweenShots);
        }
    }

    // Appelée par la minuterie (ou plus tard par l'Animation Event « Shoot »)
    public void Shoot()
    {
        if (!isShooting || isDead || player == null)
            return;

        Vector3 origin = shootingPosition != null ? shootingPosition.position : transform.position + Vector3.up * 1.5f;
        Vector3 target = player.GetHeadPosition();
        Vector3 direction = target - origin;

        // Flash au canon (facultatif)
        if (muzzleFlashFX != null)
        {
            ParticleSystem flash = Instantiate(muzzleFlashFX, origin, Quaternion.LookRotation(direction));
            Destroy(flash.gameObject, 1f);
        }

        bool hitPlayer = Random.Range(0f, 100f) < shootingAccuracy && HasLineOfSight(origin, direction);
        if (hitPlayer)
        {
            player.TakeDamage(damage);
        }

        // Trait visible dans le jeu : il arrive sur la tête si touché, passe à côté si raté
        Vector3 end = hitPlayer ? target : target + Random.insideUnitSphere * 0.6f;
        StartCoroutine(ShowTracerCO(origin, end));

        // Trait visible dans la vue Scene : rouge = touché, jaune = raté
        Debug.DrawLine(origin, target, hitPlayer ? Color.red : Color.yellow, 0.3f);

        currentShotsTaken++;
        if (currentShotsTaken >= currentMaxShotsToTake)
        {
            StartCoroutine(InitializeShootingCO()); // retour à l'abri, la rafale s'arrête
        }
    }

    // Rien ne doit bloquer la ligne de tir entre l'arme et la tête du joueur
    private bool HasLineOfSight(Vector3 origin, Vector3 direction)
    {
        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, direction.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.collider.GetComponentInParent<Player>() != null;
        }
        return true;
    }

    private void RotateTowardsPlayer()
    {
        Vector3 direction = player.GetHeadPosition() - transform.position;
        direction.y = 0;
        if (direction.sqrMagnitude < 0.001f)
            return;
        Quaternion rotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, rotationSpeed * Time.deltaTime);
    }

    // Appelée par les balles du joueur (partie 1)
    public void TakeDamage(Weapon weapon, Projectile projectile, Vector3 contactPoint)
    {
        if (isDead)
            return;

        health -= weapon.GetDamage();

        if (bloodSplatterFX != null)
        {
            ParticleSystem effect = Instantiate(bloodSplatterFX, contactPoint,
                Quaternion.LookRotation(weapon.transform.position - contactPoint));
            effect.Play();
            Destroy(effect.gameObject, 2f);
        }

        if (health <= 0)
        {
            isDead = true;
            isShooting = false;
            Destroy(gameObject);
        }
    }

    // Crée un LineRenderer enfant qui servira à dessiner chaque tir
    private void CreateTracer()
    {
        GameObject tracerObject = new GameObject("Tracer");
        tracerObject.transform.SetParent(transform, false);
        tracer = tracerObject.AddComponent<LineRenderer>();
        tracer.positionCount = 2;
        tracer.useWorldSpace = true;
        tracer.startWidth = tracerWidth;
        tracer.endWidth = tracerWidth;

        Material material = tracerMaterial;
        if (material == null)
        {
            // Pas de matériau fourni : on en crée un, de la couleur choisie
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null)
            {
                material = new Material(unlit);
                material.color = tracerColor;
            }
        }
        if (material != null)
            tracer.material = material;
        tracer.startColor = tracerColor;
        tracer.endColor = tracerColor;
        tracer.enabled = false;
    }

    private IEnumerator ShowTracerCO(Vector3 from, Vector3 to)
    {
        if (tracer == null)
            yield break;
        tracer.SetPosition(0, from);
        tracer.SetPosition(1, to);
        tracer.enabled = true;
        yield return new WaitForSeconds(tracerDuration);
        if (tracer != null)
            tracer.enabled = false;
    }

    // Les animations ne sont pas encore prêtes : on ne déclenche un Trigger
    // que si un Animator Controller est bien branché (évite les avertissements)
    private void SetAnimTrigger(string trigger)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetTrigger(trigger);
    }
}