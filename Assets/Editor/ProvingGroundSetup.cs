using System;
using System.IO;
using System.Linq;
using GameCombat;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

// Rebuilds only the environment in GameScene. Actors, policy bindings and camera rigs survive.
public static class ProvingGroundSetup {
    const string Folder = "Assets/Level/ProvingGround";
    const float Ground = .5f;
    static Transform geometry, details;
    static Material floor, wall, orange, dark, teal, white, amber;
    static readonly Color Teal = new(.16f,.76f,.7f), Orange = new(.93f,.43f,.16f);
    public static void InstallAndBuild() { Install(); RuntimeEvaluationSetup.BuildDemo(); }

    [MenuItem("Combat/Level/Rebuild Proving Ground")]
    public static void Install() {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        floor = Grid("Floor Grid", new Color(.29f,.37f,.42f), new Color(.46f,.58f,.62f));
        wall = Grid("Wall Grid", new Color(.23f,.29f,.35f), new Color(.42f,.52f,.6f));
        orange = Grid("Ramp Grid", new Color(.52f,.25f,.13f), new Color(.92f,.57f,.28f));
        dark = Solid("Graphite", new Color(.09f,.13f,.17f));
        teal = Solid("Teal Markings", Teal);
        amber = Solid("Amber Markings", Orange);
        white = Solid("Lettering", new Color(.82f,.91f,.92f));

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects()) {
            if (root.name == "floor" || root.name == "wall" || root.name == "Ground" || root.name == "Proving Ground")
                UnityEngine.Object.DestroyImmediate(root);
        }
        foreach (var surface in UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None)) {
            surface.RemoveData(); UnityEngine.Object.DestroyImmediate(surface.gameObject);
        }
        var level = new GameObject("Proving Ground");
        geometry = new GameObject("Geometry - collision and navigation").transform; geometry.SetParent(level.transform);
        details = new GameObject("Markings and architecture").transform; details.SetParent(level.transform);
        Box("Arena foundation", new Vector3(0,0,0), new Vector3(36,1,44), floor, 7);
        Box("North grid wall", new Vector3(0,3.25f,22), new Vector3(37,5.5f,1), wall);
        Box("South grid wall", new Vector3(0,3.25f,-22), new Vector3(37,5.5f,1), wall);
        Box("West grid wall", new Vector3(-18,3.25f,0), new Vector3(1,5.5f,44), wall);
        Box("East grid wall", new Vector3(18,3.25f,0), new Vector3(1,5.5f,44), wall);

        // Open central court, with room for dodges and a complete loop around the cover.
        foreach (int side in new[] {-1,1}) {
            foreach (float z in new[] {-6f,6f}) {
                Box("Cover pier", new Vector3(side*10,2,z), new Vector3(2,3,2), wall);
                Detail("Pier cap", new Vector3(side*10,3.55f,z), new Vector3(2.16f,.1f,2.16f), dark);
                Detail("Pier identification band", new Vector3(side*10,2.8f,z), new Vector3(2.03f,.18f,2.03f), side<0?teal:amber);
            }
            Box("Low cover", new Vector3(side*12,1.1f,-13), new Vector3(4,1.2f,1.25f), orange);
            Detail("Cover top", new Vector3(side*12,1.72f,-13), new Vector3(4.03f,.04f,1.28f), dark);
            // A broad solid wedge meets the deck exactly, with no vertical lip.
            Box("Raised observation deck", new Vector3(side*12,.75f,15), new Vector3(6,1.5f,8), floor, 7);
            Ramp("Access ramp", side*12, 5, 11, 6, 1);
            Detail("Deck leading edge", new Vector3(side*12,1.515f,11.1f), new Vector3(6,.02f,.16f), amber);
            Box("Deck rear stop", new Vector3(side*12,2,19), new Vector3(6,1,0.4f), wall);
            for (int k=0;k<6;k++) Detail("Ramp caution tread", new Vector3(side*12,.5f+k/6f+.018f,5+k), new Vector3(5.8f,.025f,.12f), amber,
                Quaternion.Euler(-Mathf.Atan(1f/6f)*Mathf.Rad2Deg,0,0));
            for (int z=-20;z<=20;z+=8) {
                Detail("Wall buttress", new Vector3(side*17.36f,3.25f,z), new Vector3(.3f,5.5f,.5f), dark);
                Detail("Wall light strip", new Vector3(side*17.18f,3.3f,z), new Vector3(.025f,2.8f,.12f), side<0?teal:amber);
            }
            Detail("Long court boundary", new Vector3(side*8,Ground+.012f,0), new Vector3(.09f,.02f,20), teal);
            Detail("Perimeter datum", new Vector3(side*17.45f,.8f,0), new Vector3(.03f,.18f,43), teal);
        }
        foreach (int end in new[] {-1,1}) {
            Detail("Short court boundary", new Vector3(0,Ground+.012f,end*10), new Vector3(16,.02f,.09f), teal);
            Detail("End wall cap", new Vector3(0,6.05f,end*22), new Vector3(37,.15f,1.2f), dark);
            Detail("End datum", new Vector3(0,.8f,end*21.48f), new Vector3(35,.18f,.025f), teal);
        }
        // Flush markings avoid collider seams, and never enter the NavMesh bake.
        for (int i=-3;i<=3;i++) {
            Detail("Center calibration tick", new Vector3(i,Ground+.018f,0), new Vector3(.045f,.018f,.5f), white);
            Detail("Range marker", new Vector3(0,Ground+.018f,i*2), new Vector3(.5f,.018f,.045f), white);
        }
        SpawnMark("Player start", new Vector3(0,Ground+.02f,-7), teal);
        SpawnMark("Opponent start", new Vector3(0,Ground+.02f,4), amber);
        Detail("North sign panel", new Vector3(0,3.45f,21.43f), new Vector3(12,2.1f,.12f), dark);
        Label("Arena title", "PROVING GROUND", new Vector3(0,3.8f,21.33f), Quaternion.identity, .15f, white);
        Label("Arena subtitle", "01   /   COMBAT SYSTEMS", new Vector3(0,3.05f,21.32f), Quaternion.identity, .075f, white);
        Label("West lane label", "A  /  FLANK", new Vector3(-17.45f,4.65f,0), Quaternion.Euler(0,-90,0), .1f, white);
        Label("East lane label", "B  /  ELEVATION", new Vector3(17.45f,4.65f,0), Quaternion.Euler(0,90,0), .1f, white);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.5f,.6f,.7f);
        RenderSettings.ambientEquatorColor = new Color(.35f,.42f,.5f);
        RenderSettings.ambientGroundColor = new Color(.2f,.23f,.27f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.19f,.25f,.32f); RenderSettings.fogStartDistance=35; RenderSettings.fogEndDistance=100;
        var sun = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional);
        if (sun) { sun.transform.rotation=Quaternion.Euler(48,-32,0); sun.intensity=1.5f; sun.color=new Color(1,.95f,.85f); sun.shadows=LightShadows.Soft; }

        var nav = new GameObject("NavMesh Surface").AddComponent<NavMeshSurface>();
        nav.collectObjects=CollectObjects.All; nav.layerMask=(1<<6)|(1<<7);
        nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
        nav.overrideVoxelSize=true; nav.voxelSize=.1f; nav.BuildNavMesh();
        if (!nav.navMeshData) throw new Exception("Arena NavMesh bake failed");
        const string navPath=Folder+"/ArenaNavMesh.asset";
        var saved=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
        if (saved) { EditorUtility.CopySerialized(nav.navMeshData,saved); nav.RemoveData(); nav.navMeshData=saved; nav.AddData(); EditorUtility.SetDirty(saved); }
        else AssetDatabase.CreateAsset(nav.navMeshData,navPath);

        var player = UnityEngine.Object.FindFirstObjectByType<Player>();
        Place(player.gameObject,new Vector3(0,Ground,-7),Quaternion.identity);
        var host = UnityEngine.Object.FindFirstObjectByType<EnemyBrainHost>();
        Place(host.gameObject,new Vector3(0,Ground,4),Quaternion.Euler(0,180,0));
        host.mode=EnemyControlMode.Normal; host.showCombatDiagnostics=false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(host);
        if (!player.GetComponent<CombatHud>()) player.gameObject.AddComponent<CombatHud>();
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity",true)};
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        ValidateRoutes();
        Debug.Log("PROVING_GROUND_INSTALL_PASS");
    }

    static Material Grid(string name,Color surface,Color lines) {
        var m=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+name+".mat");
        if(!m) { m=new Material(Shader.Find("GameCombat/Prototype Grid")); AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat"); }
        m.SetColor("_BaseColor",surface);m.SetColor("_GridColor",lines);m.SetColor("_MajorColor",Color.Lerp(lines,Color.white,.2f));
        m.SetFloat("_GridSize",1);m.SetFloat("_LineWidth",.012f);m.SetFloat("_Checker",.045f);EditorUtility.SetDirty(m);return m;
    }
    static Material Solid(string name,Color color) {
        var m=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+name+".mat");
        if(!m) {m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.15f);EditorUtility.SetDirty(m);return m;
    }
    static GameObject Box(string name,Vector3 position,Vector3 scale,Material material,int layer=6) {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.layer=layer;go.isStatic=true;
        go.transform.SetParent(geometry);go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go;
    }
    static void Detail(string name,Vector3 position,Vector3 scale,Material material,Quaternion? rotation=null) {
        var go=Box(name,position,scale,material,2);go.transform.SetParent(details);UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        if(rotation.HasValue)go.transform.rotation=rotation.Value;
    }
    static void SpawnMark(string name,Vector3 center,Material mat) {
        foreach(int sign in new[]{-1,1}) {
            Detail(name,center+Vector3.right*sign, new Vector3(.06f,.02f,2),mat);
            Detail(name,center+Vector3.forward*sign,new Vector3(2,.02f,.06f),mat);
        }
    }
    static void Label(string name,string value,Vector3 position,Quaternion rotation,float size,Material color) {
        var go=new GameObject(name);go.layer=2;go.transform.SetParent(details);go.transform.SetPositionAndRotation(position,rotation);
        var text=go.AddComponent<TextMesh>();text.text=value;text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize=96;text.characterSize=size;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
        text.color=color.GetColor("_BaseColor");go.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
    }
    static void Ramp(string name,float x,float start,float end,float width,float rise) {
        // Closed triangular prism. Ground and deck are on the same Ground layer.
        float left=x-width/2,right=x+width/2,bottom=Ground-.05f,top=Ground+rise;
        var v=new[]{new Vector3(left,bottom,start),new Vector3(right,bottom,start),new Vector3(left,Ground,start),new Vector3(right,Ground,start),
            new Vector3(left,bottom,end),new Vector3(right,bottom,end),new Vector3(left,top,end),new Vector3(right,top,end)};
        int[] tris={2,6,3,3,6,7,0,1,4,1,5,4,0,2,1,1,2,3,4,5,6,5,7,6,0,4,2,2,4,6,1,3,5,3,7,5};
        var mesh=new Mesh{name="Solid access ramp"};mesh.vertices=v;mesh.triangles=tris;mesh.RecalculateNormals();mesh.RecalculateBounds();
        string path=Folder+"/Ramp"+(x<0?"West":"East")+".asset";
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);mesh=saved;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);
        var go=new GameObject(name);go.layer=7;go.isStatic=true;go.transform.SetParent(geometry);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=orange;go.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
    static void Place(GameObject go,Vector3 feet,Quaternion rotation) {
        var collider=go.GetComponent<Collider>();Physics.SyncTransforms();float offset=go.transform.position.y-collider.bounds.min.y;
        go.transform.SetPositionAndRotation(feet+Vector3.up*(offset+.03f),rotation);
        var rb=go.GetComponent<Rigidbody>();if(rb){rb.position=go.transform.position;rb.rotation=rotation;}
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
    }
    public static void ValidateRoutes() {
        Physics.SyncTransforms();
        Vector3[] points={new(0,.5f,-7),new(0,.5f,4),new(-14,.5f,-17),new(14,.5f,-17),new(-12,1.5f,15),new(12,1.5f,15),new(0,.5f,19)};
        foreach(var destination in points) {
            if(!NavMesh.SamplePosition(points[0],out var from,1,NavMesh.AllAreas) || !NavMesh.SamplePosition(destination,out var to,.4f,NavMesh.AllAreas))throw new Exception("Off-mesh arena route "+destination);
            var path=new NavMeshPath();if(!NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("Incomplete arena route "+destination);
            Debug.Log("ARENA_ROUTE_PASS "+destination);
        }
    }
}
