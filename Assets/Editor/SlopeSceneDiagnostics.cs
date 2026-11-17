using System.IO;
using System.Collections.Generic;
using GameCombat;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Read-only inspection of the scene actually loaded in the interactive Editor.
[InitializeOnLoad]
public static class SlopeSceneDiagnostics {
    static double next;
    static SlopeSceneDiagnostics() {
        if(Application.isBatchMode)return;
        EditorApplication.update+=CheckRequest;
    }
    static void CheckRequest() {
        if(EditorApplication.timeSinceStartup<next || EditorApplication.isCompiling)return;
        next=EditorApplication.timeSinceStartup+.5;
        const string cancel="Logs/ProvingGround/slope-cancel-editor.request";
        if(File.Exists(cancel)){
            File.Delete(cancel);EnemyElevationValidation.CancelLoadedCheck();
            return;
        }
        const string request="Logs/ProvingGround/slope-snapshot.request";
        if(File.Exists(request)){File.Delete(request);Capture();}
    }
    [MenuItem("Combat/Diagnostics/Write Loaded Slope Snapshot")]
    public static void Capture() {
        if(Application.isBatchMode)return;
        var lines=new List<string>();var scene=SceneManager.GetActiveScene();
        lines.Add($"time={System.DateTime.UtcNow:O} scene={scene.path} dirty={scene.isDirty} playing={EditorApplication.isPlaying} fixedDelta={Time.fixedDeltaTime}");
        var settings=NavMesh.GetSettingsByIndex(0);
        lines.Add($"currentBakeSettings radius={settings.agentRadius} climb={settings.agentClimb} slope={settings.agentSlope}");
        foreach(var surface in Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))
            lines.Add($"SURFACE {surface.name} active={surface.isActiveAndEnabled} mask={surface.layerMask.value} data={AssetDatabase.GetAssetPath(surface.navMeshData)} triangles={NavMesh.CalculateTriangulation().indices.Length}");
        foreach(var host in Object.FindObjectsByType<EnemyBrainHost>(FindObjectsSortMode.None)) {
            var body=host.GetComponent<enemy>();var nav=host.GetComponent<EnemyPerceptionNav>();var agent=host.GetComponent<NavMeshAgent>();var motor=host.GetComponent<EnemyMotor>();
            var rb=host.GetComponent<Rigidbody>();var capsule=host.GetComponent<CapsuleCollider>();
            lines.Add($"ENEMY {host.name} p={host.transform.position} rb={rb.position} velocity={rb.linearVelocity} feet={capsule.bounds.min.y} ground={body.grounded} kinematic={rb.isKinematic} constraints={rb.constraints} state={body.CurrentStateName} mode={host.ActiveMode} combat={host.InCombat} action={host.GetComponent<Combatant>().Runner?.Action}");
            lines.Add($"MOVEMENT {motor.MovementDiagnostic} step={motor.StepBlockReason} groundMask={body.GroundMask.value} obstacleMask={host.GetComponent<Combatant>().tuning.obstacles.value}");
            lines.Add($"NAV active={agent.enabled} onMesh={agent.isOnNavMesh} radius={agent.radius} offset={agent.baseOffset} nav={nav.NavigationDiagnostic}");
            if(EditorApplication.isPlaying)lines.Add($"TARGET p={(nav.Target?nav.Target.position:Vector3.zero)} visible={nav.CanSeePlayer()} engage={nav.CanEngagePlayer()}");
            if(agent.isOnNavMesh)lines.Add($"PATH native={agent.nextPosition} destination={agent.destination} desired={agent.desiredVelocity} stopped={agent.isStopped} hasPath={agent.hasPath} status={agent.pathStatus}");
        }
        foreach(var col in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)) {
            string name=col.name.ToLowerInvariant();
            if(!(name.Contains("ramp") || name.Contains("slope") || name.Contains("deck") || name.Contains("floor") || name.Contains("foundation")))continue;
            lines.Add($"GEOMETRY {col.name} type={col.GetType().Name} layer={col.gameObject.layer} enabled={col.enabled} trigger={col.isTrigger} p={col.transform.position} scale={col.transform.lossyScale} angles={col.transform.eulerAngles} bounds={col.bounds}");
        }
        Directory.CreateDirectory("Logs/ProvingGround");File.WriteAllLines("Logs/ProvingGround/live-editor-slope.txt",lines);
        if(EditorApplication.isPlaying)File.AppendAllLines("Logs/ProvingGround/live-editor-slope-history.txt",lines);
    }
}
