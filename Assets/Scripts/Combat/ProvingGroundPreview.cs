using System;
using System.IO;
using UnityEngine;

namespace GameCombat {
// Explicit, bounded standalone capture; never instantiated in ordinary gameplay.
public sealed class ProvingGroundPreview : MonoBehaviour {
    int frame;
    string folder;
    double deadline;
    Vector3 enemyStart;
    bool failed;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Requested() {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--proving-ground-preview")>=0)
            new GameObject("Proving ground visual capture").AddComponent<ProvingGroundPreview>();
    }
    void Start() {
        folder=Path.GetFullPath("Logs/ProvingGround");Directory.CreateDirectory(folder);
        deadline=Time.realtimeSinceStartupAsDouble+60;Time.captureDeltaTime=.02f;Application.runInBackground=true;
        enemyStart=FindFirstObjectByType<EnemyBrainHost>().transform.position;
    }
    void Update() {
        frame++;
        if(frame==50) {
            var host=FindFirstObjectByType<EnemyBrainHost>();var nav=host.GetComponent<UnityEngine.AI.NavMeshAgent>();
            float moved=Vector3.Distance(enemyStart,host.transform.position);
            Debug.Log($"ARENA_STANDALONE_NAV onMesh={nav.isOnNavMesh} chaseDistance={moved:F2} motor={host.GetComponent<EnemyMotor>().MovementDiagnostic}");
            if(!nav.isOnNavMesh || moved<.2f) {failed=true;Debug.LogError("Arena standalone chase failed");}
            Capture("game-state.png");
        }
        if(frame==65)FindFirstObjectByType<EnemyBrainHost>().SetModeImmediately(EnemyControlMode.ML);
        if(frame==100)Capture("game-ml.png");
        if(frame==120) {
            FindFirstObjectByType<EnemyBrainHost>().SetModeImmediately(EnemyControlMode.Normal);
            foreach(var camera in FindObjectsByType<Camera>())camera.enabled=false;
            FindFirstObjectByType<CombatHud>().enabled=false;
            var overview=new GameObject("Arena overview").AddComponent<Camera>();
            overview.transform.position=new Vector3(32,42,-40);overview.transform.LookAt(new Vector3(0,0,1));overview.fieldOfView=55;
            overview.clearFlags=CameraClearFlags.SolidColor;overview.backgroundColor=new Color(.12f,.17f,.22f);
        }
        if(frame==130)Capture("arena-overview.png");
        if(frame>150 && File.Exists(Path.Combine(folder,"arena-overview.png"))) {Debug.Log(failed?"PROVING_GROUND_PREVIEW_FAIL":"PROVING_GROUND_PREVIEW_PASS");Application.Quit(failed?1:0);}
        if(Time.realtimeSinceStartupAsDouble>deadline) {Debug.LogError("Arena capture deadline");Application.Quit(1);}
    }
    void Capture(string name) {ScreenCapture.CaptureScreenshot(Path.Combine(folder,name));}
}
}
