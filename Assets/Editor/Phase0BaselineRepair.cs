using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class Phase0BaselineRepair
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        var foe = UnityEngine.Object.FindFirstObjectByType<enemy>();
        if (!player || !foe) throw new Exception("Baseline actors missing");
        if (!player.GetComponent<PlayerInputComponent>())
            player.gameObject.AddComponent<PlayerInputComponent>();
        ApplyAdded(player.GetComponent<PlayerInputComponent>());
        ApplyAdded(player.GetComponent<HealthComponent>());
        ApplyAdded(player.GetComponent<LockOn>());
        var sword = player.GetComponentInChildren<Sword>(true);
        if (!sword) throw new Exception("Sword missing");
        if (PrefabUtility.IsAddedGameObjectOverride(sword.gameObject))
            PrefabUtility.ApplyAddedGameObject(sword.gameObject, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player.gameObject), InteractionMode.AutomatedAction);
        var nav = foe.GetComponent<NavMeshAgent>();
        var perception = foe.GetComponent<EnemyPerceptionNav>();
        if (!nav || !perception) throw new Exception("Navigation dependencies missing");
        // Promote the eye first so perception's reference can map into the prefab.
        if (perception.eye && PrefabUtility.IsAddedGameObjectOverride(perception.eye.gameObject))
            PrefabUtility.ApplyAddedGameObject(perception.eye.gameObject, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(foe.gameObject), InteractionMode.AutomatedAction);
        nav.radius = foe.enemyCollider.radius * Mathf.Max(Mathf.Abs(foe.transform.lossyScale.x), Mathf.Abs(foe.transform.lossyScale.z));
        ApplyAdded(nav);
        ApplyAdded(perception);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        CheckPrefab("Assets/Prefabs/Player.prefab", true);
        CheckPrefab("Assets/Models/Enemy/Enemy.prefab", false);
        Debug.Log("PHASE0_STRUCTURAL_PASS");
    }
    static void ApplyAdded(Component component)
    {
        if (!component) throw new Exception("Required component missing");
        if (PrefabUtility.IsAddedComponentOverride(component))
            PrefabUtility.ApplyAddedComponent(component, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(component.gameObject), InteractionMode.AutomatedAction);
    }
    static void CheckPrefab(string path, bool isPlayer)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try {
            if (isPlayer) {
                if (!root.GetComponent<PlayerInputComponent>() || !root.GetComponent<LockOn>() || !root.GetComponent<HealthComponent>() || !root.GetComponentInChildren<Sword>(true)) throw new Exception("Incomplete player prefab");
            } else {
                if (!root.GetComponent<NavMeshAgent>() || !root.GetComponent<EnemyPerceptionNav>() || !root.GetComponent<HealthComponent>()) throw new Exception("Incomplete enemy prefab");
            }
        } finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
