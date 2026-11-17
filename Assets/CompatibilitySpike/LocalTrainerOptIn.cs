using System;
using UnityEngine;
using Unity.MLAgents;

// InferenceOnly alone does not prevent Academy's editor connection attempt.
// Local training is a deliberate standalone command, never ordinary Play Mode.
public static class LocalTrainerOptIn {
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
 static void Configure(){var args=Environment.GetCommandLineArgs();CommunicatorFactory.Enabled=Array.IndexOf(args,"--phase5-train")>=0 && Array.IndexOf(args,"--mlagents-port")>=0;}
}
