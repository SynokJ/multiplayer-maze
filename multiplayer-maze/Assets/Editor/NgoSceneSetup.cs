using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
    private const string _PREFAB_LIST_PATH = _NETWORK_FOLDER + "/MazeNetworkPrefabs.asset";
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
        GameNetwork gamePrefab = EnsureGameNetworkPrefab(levelList);
        NetworkPrefabsList prefabList = EnsurePrefabList(gamePrefab);

        RemoveMissingScripts(scene);

        NetworkManager manager = EnsureNetworkManager(prefabList);
        EnsureMatchObject(gamePrefab);

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

    private static GameNetwork EnsureGameNetworkPrefab(LevelListSO levelList)
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
            var serialized = new SerializedObject(contents.GetComponent<GameNetwork>());
            serialized.FindProperty("_levels").objectReferenceValue = levelList;
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

    private static NetworkPrefabsList EnsurePrefabList(GameNetwork gamePrefab)
    {
        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(_PREFAB_LIST_PATH);
        if (list == null)
        {
            list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            AssetDatabase.CreateAsset(list, _PREFAB_LIST_PATH);
        }

        if (!list.Contains(gamePrefab.gameObject))
            list.Add(new NetworkPrefab { Prefab = gamePrefab.gameObject });

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

        var ui = match.GetComponent<NgoMatchDebugUI>();
        if (ui == null)
            ui = match.AddComponent<NgoMatchDebugUI>();

        var uiSerialized = new SerializedObject(ui);
        uiSerialized.FindProperty("_roomServer").objectReferenceValue = roomServer;
        uiSerialized.ApplyModifiedPropertiesWithoutUndo();
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
