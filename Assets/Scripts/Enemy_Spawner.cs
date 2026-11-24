using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Fait apparaître les ennemis par vagues.
// Points d'apparition et de couverture : enfants de "spawnPointsRoot" / "coverSpotsRoot" si renseignés,
// sinon générés automatiquement sur le NavMesh (les couvertures sont prises le long des murs).
public class Enemy_Spawner : MonoBehaviour
{
    [SerializeField] private EnemyAI1 enemyPrefab;
    [SerializeField] private Player player;

    [Header("Points (optionnels)")]
    [SerializeField] private Transform spawnPointsRoot;
    [SerializeField] private Transform coverSpotsRoot;

    [Header("Vagues")]
    [SerializeField] private int enemiesFirstWave = 3;
    [SerializeField] private int extraEnemiesPerWave = 2;
    [SerializeField] private int maxAliveAtOnce = 6;
    [SerializeField] private float timeBetweenSpawns = 2f;
    [SerializeField] private float timeBetweenWaves = 6f;
    [SerializeField] private float damageGrowthPerWave = 0.1f;
    [SerializeField] private float accuracyGrowthPerWave = 3f;

    [Header("Distances au joueur")]
    [SerializeField] private float minSpawnDistance = 18f;
    [SerializeField] private float maxSpawnDistance = 45f;
    [SerializeField] private float minCoverDistance = 7f;
    [SerializeField] private float maxCoverDistance = 20f;

    [Header("Niveau du sol")]
    [Tooltip("Hauteur max au-dessus du sol (position de départ du joueur) pour apparaître ou s'abriter : exclut les toits.")]
    [SerializeField] private float maxHeightAboveGround = 1.0f;

    public event Action<int> WaveStarted;
    public event Action<EnemyAI1> EnemyKilled;

    public int CurrentWave { get; private set; }
    public int AliveCount => alive.Count;
    public int RemainingInWave => Mathf.Max(0, toSpawnThisWave - spawnedThisWave) + alive.Count;
    public bool IsRunning => routine != null;

    private readonly List<EnemyAI1> alive = new List<EnemyAI1>();
    private readonly HashSet<Transform> occupiedCovers = new HashSet<Transform>();
    private readonly List<Transform> autoCovers = new List<Transform>();
    private Coroutine routine;
    private int toSpawnThisWave;
    private int spawnedThisWave;

    private Vector3[] navVertices;
    private int[] navIndices;
    private float[] navAreasCumulative;
    private float groundY;

    private void Awake()
    {
        if (player == null) player = FindAnyObjectByType<Player>();
    }

    public void StartWaves()
    {
        StopWaves();
        if (player != null) groundY = player.transform.position.y;
        CurrentWave = 0;
        routine = StartCoroutine(WavesCO());
    }

    public void StopWaves()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
    }

    public void ClearEnemies()
    {
        foreach (EnemyAI1 e in alive)
            if (e != null) Destroy(e.gameObject);
        alive.Clear();
        occupiedCovers.Clear();
    }

    private IEnumerator WavesCO()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("Enemy_Spawner : aucun prefab d'ennemi assigné.", this);
            yield break;
        }
        if (!CacheNavMesh())
        {
            Debug.LogError("Enemy_Spawner : pas de NavMesh dans la scène. Lancez VR Shooter > Configurer la scène.", this);
            yield break;
        }

        while (true)
        {
            CurrentWave++;
            toSpawnThisWave = enemiesFirstWave + extraEnemiesPerWave * (CurrentWave - 1);
            spawnedThisWave = 0;
            WaveStarted?.Invoke(CurrentWave);

            while (spawnedThisWave < toSpawnThisWave)
            {
                if (alive.Count < maxAliveAtOnce && SpawnOne())
                    spawnedThisWave++;
                yield return new WaitForSeconds(timeBetweenSpawns);
            }
            while (alive.Count > 0)
                yield return null;

            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    private bool SpawnOne()
    {
        Vector3 spawnPos;
        Transform cover = PickCover();
        if (cover == null || !PickSpawnPoint(out spawnPos))
            return false;

        Vector3 look = player.transform.position - spawnPos;
        look.y = 0f;
        Quaternion rot = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;

        EnemyAI1 enemy = Instantiate(enemyPrefab, spawnPos, rot);
        enemy.name = "Enemy W" + CurrentWave + "-" + (spawnedThisWave + 1);
        enemy.ScaleDifficulty(1f + damageGrowthPerWave * (CurrentWave - 1), accuracyGrowthPerWave * (CurrentWave - 1));
        occupiedCovers.Add(cover);
        alive.Add(enemy);
        enemy.Died += OnEnemyDied;
        enemy.CoverProvider = ReassignCover;
        enemy.Init(player, cover);
        return true;
    }

    private void OnEnemyDied(EnemyAI1 enemy)
    {
        enemy.Died -= OnEnemyDied;
        alive.Remove(enemy);
        if (enemy.CoverSpot != null) occupiedCovers.Remove(enemy.CoverSpot);
        EnemyKilled?.Invoke(enemy);
    }

    // ---------- Points de couverture ----------

    private Transform ReassignCover(EnemyAI1 enemy)
    {
        Transform old = enemy.CoverSpot;
        Transform next = PickCover();
        if (next == null) return null;
        if (old != null) occupiedCovers.Remove(old);
        occupiedCovers.Add(next);
        return next;
    }

    // Vrai si, debout à cet endroit, un ennemi voit la tête du joueur.
    private bool SeesPlayer(Vector3 point)
    {
        return CombatUtils.HasLineOfSight(point + Vector3.up * 1.4f, player.GetHeadPosition(), null);
    }

    private Transform PickCover()
    {
        Vector3 p = player.transform.position;
        if (coverSpotsRoot != null && coverSpotsRoot.childCount > 0)
        {
            List<Transform> candidates = new List<Transform>();
            foreach (Transform c in coverSpotsRoot)
            {
                if (occupiedCovers.Contains(c)) continue;
                float d = Vector3.Distance(c.position, p);
                if (d >= minCoverDistance && d <= maxCoverDistance && SeesPlayer(c.position)) candidates.Add(c);
            }
            if (candidates.Count == 0)
                foreach (Transform c in coverSpotsRoot)
                    if (!occupiedCovers.Contains(c)) candidates.Add(c);
            if (candidates.Count > 0)
                return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        for (int attempt = 0; attempt < 60; attempt++)
        {
            Vector3 point = RandomNavMeshPoint();
            float d = Vector3.Distance(point, p);
            if (d < minCoverDistance || d > maxCoverDistance) continue;

            // Les bords du NavMesh longent les murs et obstacles : de bons abris.
            NavMeshHit edge;
            if (NavMesh.FindClosestEdge(point, out edge, NavMesh.AllAreas) && edge.distance < 4f)
                point = edge.position;
            if (!IsGroundLevel(point.y)) continue;
            if (IsNearOccupied(point)) continue;
            if (attempt < 45 && !SeesPlayer(point)) continue;
            return AutoCoverTransform(point);
        }
        return null;
    }

    private bool IsNearOccupied(Vector3 point)
    {
        foreach (Transform t in occupiedCovers)
            if (t != null && (t.position - point).sqrMagnitude < 4f) return true;
        return false;
    }

    private Transform AutoCoverTransform(Vector3 point)
    {
        foreach (Transform t in autoCovers)
        {
            if (!occupiedCovers.Contains(t))
            {
                t.position = point;
                return t;
            }
        }
        GameObject go = new GameObject("Cover (auto) " + autoCovers.Count);
        go.transform.SetParent(transform, false);
        go.transform.position = point;
        autoCovers.Add(go.transform);
        return go.transform;
    }

    // ---------- Points d'apparition ----------

    private bool PickSpawnPoint(out Vector3 position)
    {
        Vector3 p = player.transform.position;
        if (spawnPointsRoot != null && spawnPointsRoot.childCount > 0)
        {
            List<Transform> far = new List<Transform>();
            foreach (Transform s in spawnPointsRoot)
                if (Vector3.Distance(s.position, p) >= minSpawnDistance) far.Add(s);
            Transform pick = far.Count > 0 ? far[UnityEngine.Random.Range(0, far.Count)]
                                           : spawnPointsRoot.GetChild(UnityEngine.Random.Range(0, spawnPointsRoot.childCount));
            NavMeshHit hit;
            if (NavMesh.SamplePosition(pick.position, out hit, 3f, NavMesh.AllAreas) && IsGroundLevel(hit.position.y))
            {
                position = hit.position;
                return true;
            }
        }

        Vector3 best = Vector3.zero;
        float bestDist = -1f;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            Vector3 point = RandomNavMeshPoint();
            if (!IsGroundLevel(point.y)) continue;
            float d = Vector3.Distance(point, p);
            if (d >= minSpawnDistance && d <= maxSpawnDistance)
            {
                position = point;
                return true;
            }
            if (d > bestDist) { bestDist = d; best = point; }
        }
        position = best;
        return bestDist > 0f;
    }

    // ---------- Échantillonnage du NavMesh ----------

    private bool CacheNavMesh()
    {
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        if (tri.indices == null || tri.indices.Length < 3) return false;
        navVertices = tri.vertices;
        navIndices = tri.indices;
        int count = navIndices.Length / 3;
        navAreasCumulative = new float[count];
        float total = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector3 a = navVertices[navIndices[i * 3]];
            Vector3 b = navVertices[navIndices[i * 3 + 1]];
            Vector3 c = navVertices[navIndices[i * 3 + 2]];
            float centerY = (a.y + b.y + c.y) / 3f;
            if (IsGroundLevel(centerY))
                total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            navAreasCumulative[i] = total;
        }
        return total > 0f;
    }

    private bool IsGroundLevel(float y)
    {
        return y <= groundY + maxHeightAboveGround && y >= groundY - maxHeightAboveGround;
    }

    private Vector3 RandomNavMeshPoint()
    {
        float total = navAreasCumulative[navAreasCumulative.Length - 1];
        float r = UnityEngine.Random.value * total;
        int idx = Array.BinarySearch(navAreasCumulative, r);
        if (idx < 0) idx = ~idx;
        idx = Mathf.Clamp(idx, 0, navAreasCumulative.Length - 1);

        Vector3 a = navVertices[navIndices[idx * 3]];
        Vector3 b = navVertices[navIndices[idx * 3 + 1]];
        Vector3 c = navVertices[navIndices[idx * 3 + 2]];
        float u = UnityEngine.Random.value, v = UnityEngine.Random.value;
        if (u + v > 1f) { u = 1f - u; v = 1f - v; }
        return a + u * (b - a) + v * (c - a);
    }
}
