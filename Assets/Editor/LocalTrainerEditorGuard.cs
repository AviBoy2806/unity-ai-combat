using UnityEditor;
using Unity.MLAgents;

// Also runs after an Editor script reload while Play Mode is already active.
// Ordinary Play Mode never opts into Python training.
[InitializeOnLoad]
public static class LocalTrainerEditorGuard {
 static LocalTrainerEditorGuard(){CommunicatorFactory.Enabled=false;}
}
