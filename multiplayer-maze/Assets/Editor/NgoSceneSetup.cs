using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// One-click setup of the scene for Netcode for GameObjects. Safe to run again: it only
/// creates what is missing and re-links the rest. Menu: Maze > Set Up NGO Scene.
/// </summary>
public static class NgoSceneSetup
{
    private const string _SCENE_PATH = "Assets/Scenes/SampleScene.unity";
    private const string _NETWORK_FOLDER = "Assets/Prefabs/Network";
    private const string _GAME_PREFAB_PATH = _NETWORK_FOLDER + "/GameNetwork.prefab";
    private const string _PLAYER_PREFAB_PATH = _NETWORK_FOLDER + "/Player.prefab";
    private const string _ITEM_PREFAB_PATH = _NETWORK_FOLDER + "/Item.prefab";
    private const string _HATCH_PREFAB_PATH = _NETWORK_FOLDER + "/Hatch.prefab";
    private const string _PREFAB_LIST_PATH = _NETWORK_FOLDER + "/MazeNetworkPrefabs.asset";
    private static readonly string[] _PLAYER_SKIN_PATHS =
    {
        "Assets/Prefabs/Characters/Player/Player_GFX.prefab",
        "Assets/Prefabs/Characters/Player/Ellie_GFX.prefab",
        "Assets/Prefabs/Characters/Player/Joil_GFX.prefab",
    };
    private const string _HATCH_SPRITE_PATH = "Assets/Sprites/Hatch.png";
    private static readonly (string path, string name)[] _ITEM_SPRITES =
    {
        ("Assets/Sprites/Colectables_template.png", "Colectables_template_3"),
        ("Assets/Sprites/Colectables_template.png", "Colectables_template_1"),
        ("Assets/Sprites/Colectables_template.png", "Colectables_template_0"),
        ("Assets/Sprites/Wallet_Item.png", null),
        ("Assets/Sprites/Sneakers_Item.png", null),
    };
    private const string _LEVEL_PATH = "Assets/Data/Levels/Level_01.asset";
    private const string _LEVEL_LIST_PATH = "Assets/Data/Levels/LevelList.asset";

    private const int _BACKGROUND_DISCONNECT_TIMEOUT_MS = 90000;

    [MenuItem("Maze/Set Up NGO Scene")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            scene = EditorSceneManager.OpenScene(_SCENE_PATH, OpenSceneMode.Single);

        SetUp(scene);
        Debug.Log("NGO scene set up: " + scene.path);
    }

    // For: Unity -batchmode -projectPath <path> -executeMethod NgoSceneSetup.RunBatch -quit
    public static void RunBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(_SCENE_PATH, OpenSceneMode.Single);
        SetUp(scene);
    }

    private static void SetUp(Scene scene)
    {
        EnsureFolder(_NETWORK_FOLDER);

        LevelListSO levelList = EnsureLevelList();
        PlayerAvatar playerPrefab = EnsurePlayerPrefab();
        ItemPickup itemPrefab = EnsureItemPrefab();
        HatchTrigger hatchPrefab = EnsureHatchPrefab();
        GameNetwork gamePrefab = EnsureGameNetworkPrefab(levelList, playerPrefab, itemPrefab, hatchPrefab);
        NetworkPrefabsList prefabList = EnsurePrefabList(gamePrefab.gameObject, playerPrefab.gameObject, itemPrefab.gameObject, hatchPrefab.gameObject);

        RemoveMissingScripts(scene);

        NetworkManager manager = EnsureNetworkManager(prefabList);
        EnsureMatchObject(gamePrefab);
        EnsureCameraFollow();
        EnsureJoystick();
        EnsureDarkAmbient();

        PlayerSettings.runInBackground = true;
        EnsureSceneInBuild(scene.path);

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static LevelListSO EnsureLevelList()
    {
        var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(_LEVEL_PATH);
        var list = AssetDatabase.LoadAssetAtPath<LevelListSO>(_LEVEL_LIST_PATH);

        if (list == null)
        {
            list = ScriptableObject.CreateInstance<LevelListSO>();
            AssetDatabase.CreateAsset(list, _LEVEL_LIST_PATH);
        }

        if (level != null)
        {
            var serialized = new SerializedObject(list);
            SerializedProperty levels = serialized.FindProperty("_levels");

            if (levels.arraySize == 0)
            {
                levels.arraySize = 1;
                levels.GetArrayElementAtIndex(0).objectReferenceValue = level;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        EditorUtility.SetDirty(list);
        return list;
    }

    // The body is physics only; the art is a skin prefab that PlayerAvatar adds as a child at spawn.
    private static PlayerAvatar EnsurePlayerPrefab()
    {
        if (!File.Exists(_PLAYER_PREFAB_PATH))
        {
            var root = new GameObject("Player");
            root.transform.localScale = Vector3.one * 1.2f;

            var body = root.AddComponent<Rigidbody2D>();
            body.gravityScale = 0.0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            root.AddComponent<CircleCollider2D>().radius = 0.5f;
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<NetworkRigidbody2D>();
            root.AddComponent<LocalMoveInput>();
            root.AddComponent<MoveAnimator>();
            AddGlow(root, Color.white, 1.0f, 2.4f, 9.6f, 0.8f, new Vector2(0.0f, 1.0f)); // the torch
            var avatar = root.AddComponent<PlayerAvatar>();

            var serialized = new SerializedObject(avatar);
            SerializedProperty skins = serialized.FindProperty("_skins");
            skins.arraySize = _PLAYER_SKIN_PATHS.Length;
            for (int i = 0; i < _PLAYER_SKIN_PATHS.Length; ++i)
                skins.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(_PLAYER_SKIN_PATHS[i]);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, _PLAYER_PREFAB_PATH);
            Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(_PLAYER_PREFAB_PATH).GetComponent<PlayerAvatar>();
    }

    // A sliced texture holds several sprites, so the name picks one; a single sprite needs no name.
    private static Sprite LoadSprite(string path, string spriteName)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            if (asset is Sprite sprite && (spriteName == null || sprite.name == spriteName))
                return sprite;

        return null;
    }

    private static ItemPickup EnsureItemPrefab()
    {
        if (!File.Exists(_ITEM_PREFAB_PATH))
        {
            var root = new GameObject("Item");
            root.transform.localScale = Vector3.one * 1.2f;

            root.AddComponent<SpriteRenderer>();

            var trigger = root.AddComponent<CircleCollider2D>();
            trigger.radius = 0.5f;
            trigger.isTrigger = true;

            root.AddComponent<NetworkObject>();
            AddGlow(root, new Color(1.0f, 0.86f, 0.0f), 1.0f, 0.0f, 2.4f, 0.8f, Vector2.zero);
            var pickup = root.AddComponent<ItemPickup>();

            var serialized = new SerializedObject(pickup);
            SerializedProperty sprites = serialized.FindProperty("_sprites");
            sprites.arraySize = _ITEM_SPRITES.Length;
            for (int i = 0; i < _ITEM_SPRITES.Length; ++i)
                sprites.GetArrayElementAtIndex(i).objectReferenceValue = LoadSprite(_ITEM_SPRITES[i].path, _ITEM_SPRITES[i].name);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, _ITEM_PREFAB_PATH);
            Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(_ITEM_PREFAB_PATH).GetComponent<ItemPickup>();
    }

    private static HatchTrigger EnsureHatchPrefab()
    {
        if (!File.Exists(_HATCH_PREFAB_PATH))
        {
            var root = new GameObject("Hatch");
            root.transform.localScale = Vector3.one * 1.2f;

            var spriteRenderer = root.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(_HATCH_SPRITE_PATH);
            spriteRenderer.sortingOrder = -2;

            var trigger = root.AddComponent<BoxCollider2D>();
            trigger.size = Vector2.one;
            trigger.isTrigger = true;
            root.AddComponent<NetworkObject>();
            root.AddComponent<HatchTrigger>();
            AddGlow(root, new Color(0.98f, 0.333f, 0.254f, 0.553f), 2.21f, 0.0f, 2.1f, 0.661f, Vector2.zero);

            PrefabUtility.SaveAsPrefabAsset(root, _HATCH_PREFAB_PATH);
            Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(_HATCH_PREFAB_PATH).GetComponent<HatchTrigger>();
    }

    private static GameNetwork EnsureGameNetworkPrefab(LevelListSO levelList, PlayerAvatar player, ItemPickup item, HatchTrigger hatch)
    {
        if (!File.Exists(_GAME_PREFAB_PATH))
        {
            var root = new GameObject("GameNetwork");
            root.AddComponent<NetworkObject>();
            root.AddComponent<GameNetwork>();
            PrefabUtility.SaveAsPrefabAsset(root, _GAME_PREFAB_PATH);
            Object.DestroyImmediate(root);
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(_GAME_PREFAB_PATH);
        try
        {
            var spawner = contents.GetComponent<RoundSpawner>();
            if (spawner == null)
                spawner = contents.AddComponent<RoundSpawner>();

            var spawnerSerialized = new SerializedObject(spawner);
            spawnerSerialized.FindProperty("_avatarPrefab").objectReferenceValue = player;
            spawnerSerialized.FindProperty("_itemPrefab").objectReferenceValue = item;
            spawnerSerialized.FindProperty("_hatchPrefab").objectReferenceValue = hatch;
            spawnerSerialized.ApplyModifiedPropertiesWithoutUndo();

            var serialized = new SerializedObject(contents.GetComponent<GameNetwork>());
            serialized.FindProperty("_levels").objectReferenceValue = levelList;
            serialized.FindProperty("_spawner").objectReferenceValue = spawner;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, _GAME_PREFAB_PATH);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.ImportAsset(_GAME_PREFAB_PATH);
        return AssetDatabase.LoadAssetAtPath<GameObject>(_GAME_PREFAB_PATH).GetComponent<GameNetwork>();
    }

    private static NetworkPrefabsList EnsurePrefabList(params GameObject[] prefabs)
    {
        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(_PREFAB_LIST_PATH);
        if (list == null)
        {
            list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            AssetDatabase.CreateAsset(list, _PREFAB_LIST_PATH);
        }

        foreach (GameObject prefab in prefabs)
            if (!list.Contains(prefab))
                list.Add(new NetworkPrefab { Prefab = prefab });

        EditorUtility.SetDirty(list);
        return list;
    }

    private static NetworkManager EnsureNetworkManager(NetworkPrefabsList prefabList)
    {
        var manager = Object.FindAnyObjectByType<NetworkManager>();
        GameObject host;

        if (manager != null)
        {
            host = manager.gameObject;
        }
        else
        {
            host = new GameObject("NetworkManager");
            manager = host.AddComponent<NetworkManager>();
        }

        var transport = host.GetComponent<UnityTransport>();
        if (transport == null)
            transport = host.AddComponent<UnityTransport>();

        // A WebGL browser can only use WebSockets. The proxy in front of the rooms handles TLS.
        transport.UseWebSockets = true;
        transport.UseEncryption = false;
        transport.DisconnectTimeoutMS = _BACKGROUND_DISCONNECT_TIMEOUT_MS;

        NetworkConfig config = manager.NetworkConfig;
        config.NetworkTransport = transport;
        config.ConnectionApproval = true;
        // GameNetwork is spawned by the server, so clients never need to load or sync scenes.
        config.EnableSceneManagement = false;
        config.PlayerPrefab = null;

        config.Prefabs.NetworkPrefabsLists.Clear();
        config.Prefabs.NetworkPrefabsLists.Add(prefabList);

        EditorUtility.SetDirty(transport);
        return manager;
    }

    private static void EnsureMatchObject(GameNetwork gamePrefab)
    {
        GameObject match = GameObject.Find("Match");
        if (match == null)
            match = new GameObject("Match");

        var roomServer = match.GetComponent<NgoRoomServer>();
        if (roomServer == null)
            roomServer = match.AddComponent<NgoRoomServer>();

        var roomServerSerialized = new SerializedObject(roomServer);
        roomServerSerialized.FindProperty("_gameNetworkPrefab").objectReferenceValue = gamePrefab;
        roomServerSerialized.ApplyModifiedPropertiesWithoutUndo();

        var view = match.GetComponent<MatchMenuView>();
        if (view == null)
            view = match.AddComponent<MatchMenuView>();

        var menu = match.GetComponent<MatchMenuController>();
        if (menu == null)
            menu = match.AddComponent<MatchMenuController>();

        var menuSerialized = new SerializedObject(menu);
        menuSerialized.FindProperty("_view").objectReferenceValue = view;
        menuSerialized.FindProperty("_roomServer").objectReferenceValue = roomServer;
        menuSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddGlow(GameObject target, Color color, float intensity, float inner, float outer, float falloff, Vector2 offset)
    {
        var serialized = new SerializedObject(target.AddComponent<LightGlow>());
        serialized.FindProperty("_color").colorValue = color;
        serialized.FindProperty("_intensity").floatValue = intensity;
        serialized.FindProperty("_innerRadius").floatValue = inner;
        serialized.FindProperty("_outerRadius").floatValue = outer;
        serialized.FindProperty("_falloff").floatValue = falloff;
        serialized.FindProperty("_offset").vector2Value = offset;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // The maze is dark and the players' torches light it, like the old game. Only a Global Light
    // still at the URP default (1) is lowered, so a value you chose yourself is kept.
    private static void EnsureDarkAmbient()
    {
        foreach (Light2D light in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include))
            if (light.lightType == Light2D.LightType.Global && Mathf.Approximately(light.intensity, 1.0f))
            {
                light.intensity = 0.0f;
                EditorUtility.SetDirty(light);
            }
    }

    private static void EnsureJoystick()
    {
        if (Object.FindAnyObjectByType<FloatingJoystick>() == null)
            new GameObject("Joystick").AddComponent<FloatingJoystick>();
    }

    private static void EnsureCameraFollow()
    {
        Camera camera = Camera.main;
        if (camera != null && camera.GetComponent<CameraFollow>() == null)
            camera.gameObject.AddComponent<CameraFollow>();
    }

    // Components whose script was deleted (the old GameController) would only log errors.
    private static void RemoveMissingScripts(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
    }

    private static void EnsureSceneInBuild(string scenePath)
    {
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            if (existing.path == scenePath)
                return;

        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
        {
            new EditorBuildSettingsScene(scenePath, true)
        };
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
