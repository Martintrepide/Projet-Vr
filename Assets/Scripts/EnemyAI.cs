using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// =====================================================================
//  PARTIE 2 – IA de l'ennemi
//  D'après le projet GameDevChef « VRShooter » (Unity 2019.3, 2020),
//  adapté à notre projet : Unity 6000.3 + XR Interaction Toolkit 3.5.1.
//
//  Comportement : l'ennemi court vers un point de couverture,
//  s'accroupit, attend, se relève et tire une rafale sur le joueur,
//  puis recommence. Il perd des PV quand on lui tire dessus.
//
//  À placer sur le MÊME GameObject que l'Animator de l'ennemi
//  (sinon l'Animation Event « Shoot » ne trouve pas la méthode Shoot()).
// =====================================================================
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour, ITakeDamage
{
    // Noms des Triggers de l'Animator Controller (doivent être identiques)
    const string RUN_TRIGGER = "Run";
    const string CROUCH_TRIGGER = "Crouch";
    const string SHOOT_TRIGGER = "Shoot";

    // Valeurs par défaut = celles du prefab Enemy du créateur de la vidéo
    [Header("Vie")]
    [SerializeField] private float startingHealth = 100f;

    [Header("Couverture (secondes)")]
    [SerializeField] private float minTimeUnderCover = 10f;
    [SerializeField] private float maxTimeUnderCover = 15f;

    [Header("Rafales")]
    [SerializeField] private int minShotsToTake = 5;
    [SerializeField] private int maxShotsToTake = 10;
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private float damage = 20f;
    [Range(0, 100)]
    [SerializeField] private float shootingAccuracy = 100f;

    [Header("Références")]
    [Tooltip("Objet vide placé au bout du canon de l'arme de l'ennemi")]
    [SerializeField] private Transform shootingPosition;
    [Tooltip("Effet de sang (ex. FX_BloodSplatter du pack Simple FX)")]
    [SerializeField] private ParticleSystem bloodSplatterFX;

    [Header("Ajout (absent de la vidéo)")]
    [Tooltip("Distance à partir de laquelle on considère que l'ennemi est arrivé à son abri")]
    [SerializeField] private float arrivalTolerance = 0.3f;

    private bool isShooting;
    private bool isDead;
    private int currentShotsTaken;
    private int currentMaxShotsToTake;
    private NavMeshAgent agent;
    private Player player;
    private Transform occupiedCoverSpot;
    private Animator animator;

    private float _health;
    public float health
    {
        get { return _health; }
        set { _health = Mathf.Clamp(value, 0, startingHealth); }
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        agent = GetComponent<NavMeshAgent>();
        animator.SetTrigger(RUN_TRIGGER);
        _health = startingHealth;
    }

    // Appelée par l'EnemySpawner juste après l'Instantiate
    public void Init(Player player, Transform coverSpot)
    {
        occupiedCoverSpot = coverSpot;
        this.player = player;
        GetToCover();
    }

    private void GetToCover()
    {
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"{name} n'est pas posé sur le NavMesh : as-tu fait le Bake, et le spawner est-il sur la zone bleue ?", this);
            return;
        }
        agent.isStopped = false;
        agent.SetDestination(occupiedCoverSpot.position);
    }

    private void Update()
    {
        if (player == null || occupiedCoverSpot == null || !agent.isOnNavMesh)
            return;

        // Vidéo : (transform.position - occupiedCoverSpot.position).sqrMagnitude <= 0.1f
        // Remplacé par remainingDistance : même idée, mais insensible à un écart de
        // hauteur entre le point de couverture et le sol (fréquent avec nos cartes .glb).
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
        // Ajout : on arrête de viser pendant qu'on est accroupi
        // (évite aussi qu'un tir « en trop » relance plusieurs coroutines)
        isShooting = false;
        animator.SetTrigger(CROUCH_TRIGGER);
    }

    private void StartShooting()
    {
        isShooting = true;
        currentMaxShotsToTake = Random.Range(minShotsToTake, maxShotsToTake);
        currentShotsTaken = 0;
        animator.SetTrigger(SHOOT_TRIGGER);
    }

    // Appelée par l'Animation Event « Shoot » du clip de tir (? 0,33 s dans la vidéo)
    public void Shoot()
    {
        if (!isShooting || player == null)
            return;

        bool hitPlayer = Random.Range(0f, 100f) < shootingAccuracy;
        if (hitPlayer && HasLineOfSightToPlayer())
        {
            player.TakeDamage(damage);
        }

        currentShotsTaken++;
        if (currentShotsTaken >= currentMaxShotsToTake)
        {
            StartCoroutine(InitializeShootingCO());
        }
    }

    // Ajout : dans la vidéo, le rayon devait toucher un collider du joueur.
    // Dans le XR Origin d'XRI 3, le seul collider (CharacterController) est très fin
    // et ne monte pas toujours jusqu'à la caméra : on vérifie donc plutôt que rien
    // ne bloque la ligne de tir entre l'arme de l'ennemi et la tête du joueur.
    private bool HasLineOfSightToPlayer()
    {
        Vector3 origin = shootingPosition != null ? shootingPosition.position : transform.position + Vector3.up * 1.5f;
        Vector3 direction = player.GetHeadPosition() - origin;
        float distance = direction.magnitude;

        // QueryTriggerInteraction.Ignore : les hitboxes (triggers) de l'ennemi ne bloquent pas son propre tir
        if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            // Un obstacle est touché avant la tête : c'est un tir réussi seulement si c'est le joueur lui-même
            return hit.collider.GetComponentInParent<Player>() != null;
        }
        return true; // rien entre l'ennemi et la tête du joueur
    }

    private void RotateTowardsPlayer()
    {
        Vector3 direction = player.GetHeadPosition() - transform.position;
        direction.y = 0;
        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion rotation = Quaternion.LookRotation(direction);
        rotation = Quaternion.RotateTowards(transform.rotation, rotation, rotationSpeed * Time.deltaTime);
        transform.rotation = rotation;
    }

    // Appelée par les projectiles (interface ITakeDamage, partie 1)
    public void TakeDamage(Weapon weapon, Projectile projectile, Vector3 contactPoint)
    {
        if (isDead)
            return;

        health -= weapon.GetDamage();

        if (bloodSplatterFX != null)
        {
            ParticleSystem effect = Instantiate(bloodSplatterFX, contactPoint,
                Quaternion.LookRotation(weapon.transform.position - contactPoint));
            effect.Stop();
            effect.Play();
            Destroy(effect.gameObject, 2f); // ajout : on nettoie l'effet après 2 s
        }

        if (health <= 0)
        {
            isDead = true;
            Destroy(gameObject);
        }
    }
}