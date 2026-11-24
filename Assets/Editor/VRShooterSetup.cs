using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using Object = UnityEngine.Object;

// Menu "VR Shooter" : câble la scène ouverte en un clic.
// Le script peut être relancé : il ne recrée pas ce qui existe déjà.
public static class VRShooterSetup
{
    const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
    const string BloodFxPath = "Assets/SimpleFX/Prefabs/FX_BloodSplatter.prefab";
    const string MuzzleFxPath = "Assets/SimpleFX/Prefabs/FX_GunFlash.prefab";
    const string RiflePath = "Assets/ithappy/Weapons_FREE/Prefabs/rifle_001.prefab";
    const string ShotgunPath = "Assets/ithappy/Weapons_FREE/Prefabs/shotgun_001.prefab";
    const string MusicSetPath = "Assets/Dynamic Music/Music Sets/Battlefield Parts.asset";

    static readonly List<string> report = new List<string>();

    [MenuItem("VR Shooter/Configurer la scène")]
    public static void SetupScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("VR Shooter", "Enregistrez d'abord la scène.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("VR Shooter",
                "Configurer la scène « " + scene.name + " » pour le shooter ?\n\n"
                + "Le prefab Enemy sera complété, des objets seront ajoutés à la scène et le NavMesh sera calculé. "
                + "La scène sera enregistrée à la fin.",
                "Configurer", "Annuler"))
            return;

        report.Clear();
        try
        {
            Step(0.05f, "Build Settings");
            AddSceneToBuild(scene);
            Step(0.10f, "Joueur");
            Player player = SetupPlayer();
            Step(0.20f, "Prefab Enemy");
            FixEnemyPrefab();
            Step(0.30f, "Ennemis placés à la main");
            DisablePlacedEnemies();
            Step(0.35f, "Spawner");
            Enemy_Spawner spawner = SetupSpawner(player);
            Step(0.40f, "Game Manager");
            SetupGameManager(player, spawner);
            Step(0.45f, "Musique");
            SetupMusic();
            Step(0.50f, "Armes");
            SetupWeapons();
            FixGrab();
            Step(0.55f, "Téléportation");
            FixTeleport();
            Step(0.58f, "Réglages de jeu");
            ApplyGameplaySettings(player);
            Step(0.60f, "NavMesh (peut prendre un moment)");
            BakeNavMesh(scene);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            report.Add("ERREUR : " + e.Message + " (détails dans la Console)");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Finish("Configuration terminée");
    }

    [MenuItem("VR Shooter/Recalculer le NavMesh")]
    public static void RebakeNavMesh()
    {
        report.Clear();
        Scene scene = SceneManager.GetActiveScene();
        try { BakeNavMesh(scene); }
        finally { EditorUtility.ClearProgressBar(); }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Finish("NavMesh");
    }

    static void Step(float progress, string label)
    {
        EditorUtility.DisplayProgressBar("VR Shooter", label, progress);
    }

    static void Finish(string title)
    {
        string msg = string.Join("\n", report);
        Debug.Log("[VR Shooter] " + title + " :\n" + msg);
        EditorUtility.DisplayDialog("VR Shooter : " + title, msg, "OK");
    }

    // ---------------------------------------------------------------- Build Settings

    static void AddSceneToBuild(Scene scene)
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
            .Where(s => !string.IsNullOrEmpty(s.path) && File.Exists(s.path) && s.path != scene.path)
            .ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(scene.path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        report.Add("- Scène ajoutée en tête des Build Settings (scènes manquantes retirées).");
    }

    // ---------------------------------------------------------------- Joueur

    static Player SetupPlayer()
    {
        Player player = FindAll<Player>().FirstOrDefault();
        if (player == null)
        {
            GameObject origin = GameObject.Find("XR Origin (XR Rig)");
            if (origin != null) player = origin.AddComponent<Player>();
        }
        if (player == null)
        {
            report.Add("- ATTENTION : XR Origin introuvable, composant Player non ajouté.");
            return null;
        }

        SerializedObject so = new SerializedObject(player);
        SerializedProperty head = so.FindProperty("head");
        if (head.objectReferenceValue == null)
        {
            Camera cam = player.GetComponentInChildren<Camera>(true);
            if (cam == null) cam = Camera.main;
            if (cam != null) head.objectReferenceValue = cam.transform;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        if (player.GetComponent<HeadCollision>() == null) player.gameObject.AddComponent<HeadCollision>();
        report.Add("- Player vérifié (tête = caméra du casque), collision de la tête avec les murs ajoutée.");
        return player;
    }

    // ---------------------------------------------------------------- Ennemi

    static void FixEnemyPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath) == null)
        {
            report.Add("- ATTENTION : " + EnemyPrefabPath + " introuvable.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        try
        {
            EnemyAI1 ai = root.GetComponent<EnemyAI1>();
            if (ai == null) ai = root.AddComponent<EnemyAI1>();

            NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = root.AddComponent<NavMeshAgent>();
                agent.speed = 3.5f;
                agent.angularSpeed = 360f;
                agent.acceleration = 10f;
                agent.stoppingDistance = 0.3f;
                agent.radius = 0.35f;
                agent.height = 1.8f;
            }

            if (root.GetComponentInChildren<Collider>(true) == null)
            {
                Bounds b = LocalBounds(root.transform, true);
                float height = b.size.y;
                Vector3 center = b.center;
                if (height < 0.8f || height > 4f) { height = 1.8f; center = new Vector3(0f, 0.9f, 0f); }
                CapsuleCollider cap = root.AddComponent<CapsuleCollider>();
                cap.direction = 1;
                cap.height = height;
                cap.radius = 0.3f;
                cap.center = new Vector3(0f, center.y, 0f);
            }

            SerializedObject so = new SerializedObject(ai);
            SetIfZero(so, "startingHealth", 100f);
            SetIfZero(so, "minTimeUnderCover", 2f);
            SetIfZero(so, "maxTimeUnderCover", 4f);
            SetIfZero(so, "minShotsToTake", 3);
            SetIfZero(so, "maxShotsToTake", 6);
            SetIfZero(so, "rotationSpeed", 240f);
            SetIfZero(so, "damage", 8f);
            SetIfZero(so, "shootingAccuracy", 35f);
            SetIfZero(so, "shotInterval", 0.5f);

            SerializedProperty shootPos = so.FindProperty("shootingPosition");
            if (shootPos.objectReferenceValue == null)
            {
                Transform existing = root.transform.Find("ShootingPosition");
                if (existing == null)
                {
                    existing = new GameObject("ShootingPosition").transform;
                    existing.SetParent(root.transform, false);
                    existing.localPosition = new Vector3(0.25f, 1.4f, 0.5f);
                }
                shootPos.objectReferenceValue = existing;
            }

            SerializedProperty blood = so.FindProperty("bloodSplatterFX");
            if (blood.objectReferenceValue == null)
            {
                GameObject fx = AssetDatabase.LoadAssetAtPath<GameObject>(BloodFxPath);
                ParticleSystem ps = fx != null ? fx.GetComponentInChildren<ParticleSystem>() : null;
                if (ps != null) blood.objectReferenceValue = ps;
            }

            SerializedProperty muzzle = so.FindProperty("muzzleFlashFX");
            if (muzzle.objectReferenceValue == null)
                muzzle.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(MuzzleFxPath);

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            report.Add("- Prefab Enemy complété : NavMeshAgent, collider, valeurs par défaut, point de tir, effets.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void DisablePlacedEnemies()
    {
        int count = 0;
        foreach (EnemyAI1 e in FindAll<EnemyAI1>())
        {
            if (!e.gameObject.activeSelf) continue;
            e.gameObject.SetActive(false);
            if (!e.name.Contains("(désactivé)")) e.name += " (désactivé)";
            count++;
        }
        if (count > 0)
            report.Add("- " + count + " ennemi(s) placé(s) à la main désactivé(s) : le spawner les remplace.");
    }

    static Enemy_Spawner SetupSpawner(Player player)
    {
        Enemy_Spawner spawner = FindAll<Enemy_Spawner>().FirstOrDefault();
        if (spawner == null)
        {
            GameObject go = GameObject.Find("Enemy_Spawner");
            if (go == null) go = new GameObject("Enemy_Spawner");
            spawner = go.AddComponent<Enemy_Spawner>();
        }
        GameObject sg = spawner.gameObject;
        sg.SetActive(true);

        NavMeshAgent stray = sg.GetComponent<NavMeshAgent>();
        if (stray != null) Object.DestroyImmediate(stray);
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(sg);

        Transform spawnRoot = ChildOrCreate(sg.transform, "Spawn Points");
        Transform coverRoot = ChildOrCreate(sg.transform, "Cover Spots");

        SerializedObject so = new SerializedObject(spawner);
        GameObject enemyAsset = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        if (enemyAsset != null)
            so.FindProperty("enemyPrefab").objectReferenceValue = enemyAsset.GetComponent<EnemyAI1>();
        if (player != null) so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("spawnPointsRoot").objectReferenceValue = spawnRoot;
        so.FindProperty("coverSpotsRoot").objectReferenceValue = coverRoot;
        so.ApplyModifiedPropertiesWithoutUndo();

        report.Add("- Enemy_Spawner configuré (vagues). Points d'apparition et de couverture automatiques ; "
                 + "ajoutez des objets vides sous « Spawn Points » / « Cover Spots » pour les choisir vous-même.");
        return spawner;
    }

    // ---------------------------------------------------------------- Game Manager, UI, musique

    static void SetupGameManager(Player player, Enemy_Spawner spawner)
    {
        GameManager gm = FindAll<GameManager>().FirstOrDefault();
        if (gm == null) gm = new GameObject("Game Manager").AddComponent<GameManager>();
        GameObject go = gm.gameObject;
        if (go.GetComponent<DamageFeedback>() == null) go.AddComponent<DamageFeedback>();
        if (go.GetComponent<WristHUD>() == null) go.AddComponent<WristHUD>();

        SerializedObject so = new SerializedObject(gm);
        if (player != null) so.FindProperty("player").objectReferenceValue = player;
        if (spawner != null) so.FindProperty("spawner").objectReferenceValue = spawner;
        so.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject df = new SerializedObject(go.GetComponent<DamageFeedback>());
        if (player != null) df.FindProperty("player").objectReferenceValue = player;
        df.ApplyModifiedPropertiesWithoutUndo();

        report.Add("- Game Manager ajouté : menu, fin de partie, score, retour de dégâts, écran au poignet gauche.");
    }

    static void SetupMusic()
    {
        if (FindAll<MusicDirector>().Any()) return;
        MusicSet set = AssetDatabase.LoadAssetAtPath<MusicSet>(MusicSetPath);
        if (set == null)
        {
            report.Add("- Musique ignorée : " + MusicSetPath + " introuvable.");
            return;
        }
        GameObject go = new GameObject("Music");
        DynamicPlayer dp = go.AddComponent<DynamicPlayer>();
        dp.musicSet = set;
        dp.playOnStart = true;
        dp.fadeTime = 2f;
        go.AddComponent<MusicDirector>();
        report.Add("- Musique dynamique ajoutée (« Battlefield Parts », s'intensifie à chaque vague).");
    }

    // ---------------------------------------------------------------- Armes

    static void SetupWeapons()
    {
        GameObject muzzle = AssetDatabase.LoadAssetAtPath<GameObject>(MuzzleFxPath);
        Weapon[] weapons = FindAll<Weapon>();
        Weapon reference = weapons.FirstOrDefault(w => w.GetType() == typeof(Pistol));

        int cleaned = 0;
        foreach (Weapon w in weapons)
        {
            cleaned += RemoveNestedGrabs(w);
            SerializedObject so = new SerializedObject(w);
            if (so.FindProperty("muzzleFlashPrefab").objectReferenceValue == null && muzzle != null)
                so.FindProperty("muzzleFlashPrefab").objectReferenceValue = muzzle;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        report.Add("- Armes existantes : flash de tir ajouté" + (cleaned > 0 ? ", " + cleaned + " saisie(s) en double retirée(s) sur les modèles enfants." : "."));

        if (reference == null)
        {
            report.Add("- ATTENTION : aucun Pistol de référence dans la scène, fusils non créés.");
            return;
        }
        if (!weapons.OfType<Rifle>().Any())
            CreateWeapon<Rifle>(RiflePath, reference, 0.45f, "Fusil", 30, 1.8f, 15f, muzzle);
        if (!weapons.OfType<Shotgun>().Any())
            CreateWeapon<Shotgun>(ShotgunPath, reference, 0.9f, "Pompe", 6, 2.2f, 14f, muzzle);
    }

    static int RemoveNestedGrabs(Weapon w)
    {
        int n = 0;
        foreach (XRGrabInteractable g in w.GetComponentsInChildren<XRGrabInteractable>(true))
        {
            if (g.gameObject == w.gameObject) continue;
            GameObject child = g.gameObject;
            try
            {
                Object.DestroyImmediate(g);
                Rigidbody rb = child.GetComponent<Rigidbody>();
                if (rb != null) Object.DestroyImmediate(rb);
                n++;
            }
            catch (Exception e)
            {
                report.Add("- Impossible de retirer la saisie en double sur " + child.name + " : " + e.Message);
            }
        }
        return n;
    }

    static void CreateWeapon<T>(string prefabPath, Weapon reference, float sideOffset, string displayName,
                                int magazine, float reload, float damage, GameObject muzzle) where T : Pistol
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            report.Add("- " + displayName + " non créé : " + prefabPath + " introuvable.");
            return;
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = displayName;
        Transform rt = reference.transform;
        go.transform.SetPositionAndRotation(rt.position + rt.right * sideOffset, rt.rotation);

        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || mf.GetComponent<Collider>() != null) continue;
            MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = true;
        }

        T weapon = go.AddComponent<T>();
        Rigidbody rb = go.GetComponent<Rigidbody>();
        XRGrabInteractable grab = go.GetComponent<XRGrabInteractable>();
        EditorUtility.CopySerialized(reference.GetComponent<Rigidbody>(), rb);
        EditorUtility.CopySerialized(reference.GetComponent<XRGrabInteractable>(), grab);

        // Bouche du canon et point de saisie, déduits de la disposition du pistolet de référence.
        SerializedObject refSo = new SerializedObject(reference);
        Transform refSpawn = refSo.FindProperty("bulletSpawn").objectReferenceValue as Transform;
        Transform refGrip = reference.GetComponent<XRGrabInteractable>().attachTransform;

        Bounds refB = LocalBounds(rt, false);
        Bounds newB = LocalBounds(go.transform, false);

        Transform spawn = new GameObject("bullet spawn").transform;
        spawn.SetParent(go.transform, false);
        Transform grip = new GameObject("GripPoint").transform;
        grip.SetParent(go.transform, false);

        int axis = 2;
        float sign = 1f;
        if (refSpawn != null && refB.size != Vector3.zero)
        {
            Vector3 rel = Normalized(rt.InverseTransformPoint(refSpawn.position), refB);
            axis = MaxAxis(rel);
            sign = Mathf.Sign(rel[axis]);
            spawn.localPosition = Map(rel, newB, axis, sign);
            spawn.localRotation = Quaternion.Inverse(rt.rotation) * refSpawn.rotation;
        }
        else
        {
            spawn.localPosition = new Vector3(newB.center.x, newB.center.y + newB.extents.y * 0.3f, newB.max.z);
        }

        if (refGrip != null && refGrip != rt && refB.size != Vector3.zero)
        {
            Vector3 relGrip = Normalized(rt.InverseTransformPoint(refGrip.position), refB);
            Vector3 g = newB.center;
            for (int i = 0; i < 3; i++) g[i] += Mathf.Clamp(relGrip[i], -1f, 1f) * newB.extents[i];
            grip.localPosition = g;
            grip.localRotation = Quaternion.Inverse(rt.rotation) * refGrip.rotation;
        }
        else
        {
            grip.localPosition = newB.center;
        }
        grab.attachTransform = grip;

        SerializedObject so = new SerializedObject(weapon);
        so.FindProperty("bulletPrefab").objectReferenceValue = refSo.FindProperty("bulletPrefab").objectReferenceValue;
        so.FindProperty("shootingForce").floatValue = Mathf.Max(1f, refSo.FindProperty("shootingForce").floatValue);
        so.FindProperty("recoilForce").floatValue = refSo.FindProperty("recoilForce").floatValue;
        so.FindProperty("damage").floatValue = damage;
        so.FindProperty("bulletSpawn").objectReferenceValue = spawn;
        so.FindProperty("weaponName").stringValue = displayName;
        so.FindProperty("magazineSize").intValue = magazine;
        so.FindProperty("reloadTime").floatValue = reload;
        so.FindProperty("muzzleFlashPrefab").objectReferenceValue = muzzle;
        so.ApplyModifiedPropertiesWithoutUndo();

        report.Add("- " + displayName + " (" + typeof(T).Name + ") créé à côté du pistolet. "
                 + "Vérifiez l'emplacement de « bullet spawn » (bout du canon) et de « GripPoint » (poignée).");
    }

    static Vector3 Normalized(Vector3 local, Bounds b)
    {
        Vector3 r = local - b.center;
        for (int i = 0; i < 3; i++) r[i] = b.extents[i] > 1e-5f ? r[i] / b.extents[i] : 0f;
        return r;
    }

    static int MaxAxis(Vector3 v)
    {
        int a = 0;
        for (int i = 1; i < 3; i++) if (Mathf.Abs(v[i]) > Mathf.Abs(v[a])) a = i;
        return a;
    }

    static Vector3 Map(Vector3 rel, Bounds b, int axis, float sign)
    {
        Vector3 p = b.center;
        for (int i = 0; i < 3; i++)
        {
            if (i == axis) p[i] += sign * (b.extents[i] + 0.02f);
            else p[i] += Mathf.Clamp(rel[i], -1f, 1f) * b.extents[i];
        }
        return p;
    }

    // Boîte englobante des maillages, exprimée dans le repère local de root.
    static Bounds LocalBounds(Transform root, bool activeOnly)
    {
        bool init = false;
        Bounds result = new Bounds();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(!activeOnly))
        {
            Mesh mesh = null;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf != null) mesh = mf.sharedMesh;
            SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
            if (smr != null) mesh = smr.sharedMesh;
            if (mesh == null) continue;

            Bounds mb = mesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = root.InverseTransformPoint(r.transform.TransformPoint(corner));
                if (!init) { result = new Bounds(p, Vector3.zero); init = true; }
                else result.Encapsulate(p);
            }
        }
        return result;
    }

    [MenuItem("VR Shooter/Appliquer les réglages de jeu")]
    public static void ApplyGameplayMenu()
    {
        report.Clear();
        Scene scene = SceneManager.GetActiveScene();
        try
        {
            FixGrab();
            ApplyGameplaySettings(FindAll<Player>().FirstOrDefault());
            BakeNavMesh(scene);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            report.Add("ERREUR : " + e.Message + " (détails dans la Console)");
        }
        finally { EditorUtility.ClearProgressBar(); }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Finish("Réglages de jeu");
    }

    static void ApplyGameplaySettings(Player player)
    {
        CreateFxMaterial();
        ClearPrefabFx();
        SetGrabToggle(player);
        PlaceSpawnNearWeapons(player);
    }

    // Matériau des effets dans Resources : garantit que son shader est inclus dans le build.
    static void CreateFxMaterial()
    {
        const string folder = "Assets/Resources";
        const string path = folder + "/VRShooterFX.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Resources");
        Shader shader = Shader.Find("Sprites/Default");
        AssetDatabase.CreateAsset(new Material(shader), path);
        report.Add("- Matériau des effets créé : " + path + ".");
    }

    // Les effets générés par code remplacent les prefabs de flash.
    static void ClearPrefabFx()
    {
        foreach (Weapon w in FindAll<Weapon>())
        {
            SerializedObject so = new SerializedObject(w);
            so.FindProperty("muzzleFlashPrefab").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath) != null)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            try
            {
                EnemyAI1 ai = root.GetComponent<EnemyAI1>();
                if (ai != null)
                {
                    SerializedObject so = new SerializedObject(ai);
                    so.FindProperty("muzzleFlashFX").objectReferenceValue = null;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        report.Add("- Effets de tir : flash, traçantes et impacts générés par code (prefabs de flash retirés).");
    }

    // Saisie en bascule : une pression sur le grip prend l'arme, une seconde la lâche.
    static void SetGrabToggle(Player player)
    {
        if (player == null) return;
        int count = 0;
        foreach (MonoBehaviour mb in player.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string type = mb.GetType().Name;
            if (type != "NearFarInteractor" && type != "XRDirectInteractor" && type != "XRRayInteractor") continue;
            if (mb.name.Contains("Teleport")) continue;
            SerializedObject so = new SerializedObject(mb);
            SerializedProperty trig = so.FindProperty("m_SelectActionTrigger");
            if (trig == null) continue;
            trig.intValue = 2; // Toggle
            so.ApplyModifiedPropertiesWithoutUndo();
            count++;
        }
        report.Add("- Saisie en bascule sur " + count + " interacteur(s) : grip une fois pour prendre, une fois pour lâcher.");
    }

    // Râtelier devant le point de départ, armes posées dessus, joueur face au râtelier.
    static void PlaceSpawnNearWeapons(Player player)
    {
        Weapon[] weapons = FindAll<Weapon>().Where(w => w.gameObject.activeInHierarchy).ToArray();
        if (player == null || weapons.Length == 0) return;

        GameObject rack = GameObject.Find("Weapon Rack");
        Vector3 center;
        if (rack != null)
        {
            center = rack.transform.position;
        }
        else
        {
            center = Vector3.zero;
            foreach (Weapon w in weapons) center += w.transform.position;
            center /= weapons.Length;
        }

        float floorY = FloorHeight(center, weapons, rack);

        if (rack == null)
        {
            rack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rack.name = "Weapon Rack";
        }
        rack.transform.rotation = Quaternion.identity;
        rack.transform.localScale = new Vector3(1.5f, 0.9f, 0.6f);
        rack.transform.position = new Vector3(center.x, floorY + 0.45f, center.z);

        float top = floorY + 0.9f;
        for (int i = 0; i < weapons.Length; i++)
        {
            float x = weapons.Length == 1 ? 0f : Mathf.Lerp(-0.5f, 0.5f, (float)i / (weapons.Length - 1));
            Transform wt = weapons[i].transform;
            Bounds b = LocalBounds(wt, false);
            float lift = b.size == Vector3.zero ? 0.05f : b.extents.magnitude * 0.6f;
            wt.position = new Vector3(center.x + x, top + lift, center.z);
        }

        player.transform.position = new Vector3(center.x, floorY, center.z - 0.9f);
        player.transform.rotation = Quaternion.identity;
        report.Add("- Départ : joueur placé face à un râtelier (« Weapon Rack ») où sont posées les " + weapons.Length + " armes.");
    }

    static float FloorHeight(Vector3 around, Weapon[] weapons, GameObject rack)
    {
        RaycastHit[] hits = Physics.RaycastAll(around + Vector3.up * 3f, Vector3.down, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        foreach (RaycastHit h in hits)
        {
            Collider c = h.collider;
            if (rack != null && c.gameObject == rack) continue;
            if (c.GetComponentInParent<Weapon>() != null) continue;
            if (c.GetComponentInParent<Player>() != null) continue;
            if (c.GetComponentInParent<TeleportationArea>() != null) continue;
            if (h.point.y > around.y + 0.5f) continue; // toit, branche...
            if (h.point.y > best) best = h.point.y;
        }
        return float.IsNegativeInfinity(best) ? 0f : best;
    }

    [MenuItem("VR Shooter/Réparer saisie et téléportation")]
    public static void RepairGrabAndTeleport()
    {
        report.Clear();
        Scene scene = SceneManager.GetActiveScene();
        FixGrab();
        FixTeleport();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Finish("Saisie et téléportation");
    }

    // Une seule saisie par arme (sur la racine), et l'arme vient toujours dans la main, même saisie à distance.
    static void FixGrab()
    {
        int removed = 0, armes = 0;
        foreach (Weapon w in FindAll<Weapon>())
        {
            removed += RemoveNestedGrabs(w);
            XRGrabInteractable grab = w.GetComponent<XRGrabInteractable>();
            if (grab == null) continue;
            SerializedObject so = new SerializedObject(grab);
            SerializedProperty far = so.FindProperty("m_FarAttachMode");
            if (far != null) far.intValue = 1;          // 1 = Near : ramène l'arme dans la main
            SerializedProperty dyn = so.FindProperty("m_UseDynamicAttach");
            if (dyn != null) dyn.boolValue = false;      // utilise le GripPoint, pas le point touché
            so.ApplyModifiedPropertiesWithoutUndo();

            // Un MeshCollider non convexe est refusé sur un objet physique : l'arme ne pourrait pas être saisie.
            foreach (MeshCollider mc in w.GetComponentsInChildren<MeshCollider>(true))
                mc.convex = true;

            if (w.GetComponentInChildren<Collider>(true) == null)
            {
                BoxCollider box = w.gameObject.AddComponent<BoxCollider>();
                Bounds b = LocalBounds(w.transform, false);
                box.center = b.center;
                box.size = b.size;
            }
            armes++;
        }
        report.Add("- Saisie : " + armes + " arme(s) ramenée(s) dans la main au point GripPoint"
                 + (removed > 0 ? ", " + removed + " saisie(s) en double retirée(s) (elle bloquait la prise du pistolet)." : "."));
    }

    // Hauteur des zones de téléportation (plans invisibles), en mètres.
    const float TeleportHeight = 0.2f;

    // Les plans de téléportation sont placés à une hauteur fixe et rendus invisibles :
    // le rayon les touche avant le sol d'origine, qui reste visible.
    static void FixTeleport()
    {
        int fixedCount = 0;
        foreach (TeleportationArea a in FindAll<TeleportationArea>())
        {
            Vector3 p = a.transform.position;
            p.y = TeleportHeight;
            a.transform.position = p;
            Renderer r = a.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            fixedCount++;
        }
        report.Add("- Téléportation : " + fixedCount + " zone(s) placée(s) à y = " + TeleportHeight + " m et rendue(s) invisible(s).");
    }

    // ---------------------------------------------------------------- NavMesh

    static void BakeNavMesh(Scene scene)
    {
        Type surfaceType = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
        if (surfaceType == null)
        {
            report.Add("- NavMesh NON calculé : le package AI Navigation (com.unity.ai.navigation) n'est pas chargé. "
                     + "Ouvrez Window > Package Manager, vérifiez qu'il est installé, puis lancez VR Shooter > Recalculer le NavMesh.");
            return;
        }

        Component surface = Object.FindAnyObjectByType(surfaceType) as Component;
        if (surface == null)
            surface = new GameObject("NavMesh Surface").AddComponent(surfaceType);

        EditorUtility.DisplayProgressBar("VR Shooter", "Calcul du NavMesh…", 0.7f);
        surfaceType.GetMethod("BuildNavMesh", Type.EmptyTypes).Invoke(surface, null);

        PropertyInfo dataProp = surfaceType.GetProperty("navMeshData");
        NavMeshData data = dataProp != null ? dataProp.GetValue(surface) as NavMeshData : null;
        if (data == null)
        {
            report.Add("- NavMesh : le calcul n'a rien produit. Sélectionnez « NavMesh Surface » et cliquez Bake.");
            return;
        }

        string folder = Path.Combine(Path.GetDirectoryName(scene.path), scene.name).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(scene.path).Replace('\\', '/'), scene.name);
        string assetPath = folder + "/NavMesh-" + scene.name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath) != null)
            AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(data, assetPath);
        EditorUtility.SetDirty(surface);

        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        report.Add("- NavMesh calculé (" + (tri.indices.Length / 3) + " triangles) et enregistré dans " + assetPath + ".");
    }

    // ---------------------------------------------------------------- Utilitaires

    static T[] FindAll<T>() where T : Object
    {
        return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    static Transform ChildOrCreate(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return t;
        t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static void SetIfZero(SerializedObject so, string prop, float value)
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p != null && Mathf.Approximately(p.floatValue, 0f)) p.floatValue = value;
    }

    static void SetIfZero(SerializedObject so, string prop, int value)
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p != null && p.intValue == 0) p.intValue = value;
    }
}
