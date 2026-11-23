using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

// =====================================================================
//  EnemySpawner – fait apparaître des ennemis sur la carte
//  Toutes les « spawnInterval » secondes, un ennemi apparaît à un des
//  points d'apparition (ou sur le spawner lui-même) et reçoit un abri.
//  Points d'apparition et abris doivent être sur le NavMesh (zone bleue).
// =====================================================================
public class EnemiSpawner : MonoBehaviour
{
    [Tooltip("Le prefab de l'ennemi (avec le script EnemyAI)")]
    [SerializeField] private EnemyAI1 enemyPrefab;
    [Tooltip("Le XR Origin (XR Rig), qui porte le script Player")]
    [SerializeField] private Player player;

    [Header("Où les ennemis apparaissent")]
    [Tooltip("Objets vides posés sur la carte. Vide = les ennemis apparaissent sur le spawner.")]
    [SerializeField] private Transform[] spawnLocations;

    [Header("Où les ennemis vont se mettre à couvert pour tirer")]
    [FormerlySerializedAs("spawnPoints")]
    [SerializeField] private Transform[] coverPoints;

    [Header("Rythme")]
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private int maxEnemiesNumber = 3;
    [Tooltip("Coché = un ennemi tué est remplacé (le jeu continue). Décoché = maxEnemiesNumber ennemis au total.")]
    [SerializeField] private bool respawnKilledEnemies = true;

    private readonly List<EnemyAI1> spawnedEnemies = new List<EnemyAI1>();
    private float timeSinceLastSpawn;
    private int spawnCount;

    private void Start()
    {
        timeSinceLastSpawn = spawnInterval; // premier ennemi tout de suite
        if (enemyPrefab == null) Debug.LogError("EnemySpawner : champ Enemy Prefab vide.", this);
        if (player == null) Debug.LogError("EnemySpawner : champ Player vide (glisse le XR Origin).", this);
        if (coverPoints == null || coverPoints.Length == 0) Debug.LogError("EnemySpawner : ajoute au moins un Cover Point.", this);
    }

    private void Update()
    {
        if (enemyPrefab == null || player == null || coverPoints == null || coverPoints.Length == 0)
            return;

        if (respawnKilledEnemies)
            spawnedEnemies.RemoveAll(e => e == null); // les ennemis détruits libèrent leur place

        timeSinceLastSpawn += Time.deltaTime;
        if (timeSinceLastSpawn >= spawnInterval)
        {
            timeSinceLastSpawn = 0f;
            if (spawnedEnemies.Count < maxEnemiesNumber)
                SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        // Point d'apparition : un des spawnLocations au hasard, sinon le spawner
        Transform spawnFrom = transform;
        if (spawnLocations != null && spawnLocations.Length > 0)
            spawnFrom = spawnLocations[Random.Range(0, spawnLocations.Length)];

        // On « colle » la position sur le NavMesh le plus proche (2 m autour)
        Vector3 position = spawnFrom.position;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(position, out hit, 2f, NavMesh.AllAreas))
            position = hit.position;
        else
            Debug.LogWarning("EnemySpawner : pas de NavMesh près de " + spawnFrom.name + ". As-tu fait le Bake ?", spawnFrom);

        EnemyAI1 enemy = Instantiate(enemyPrefab, position, spawnFrom.rotation);

        // Abri : chacun son tour
        Transform cover = coverPoints[spawnCount % coverPoints.Length];
        spawnCount++;

        enemy.Init(player, cover);
        spawnedEnemies.Add(enemy);
    }
}