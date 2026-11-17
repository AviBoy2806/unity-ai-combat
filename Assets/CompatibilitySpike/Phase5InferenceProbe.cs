using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;

// Installed only in the standalone compatibility example by its build command.
public sealed class Phase5InferenceProbe : MonoBehaviour {
 static int total; static bool moved; int previous=-1; Vector3 origin;
 void Start(){origin=transform.position;var b=GetComponent<BehaviorParameters>();if(b.BehaviorType!=BehaviorType.InferenceOnly || !b.Model || Academy.Instance.IsCommunicatorOn){Debug.LogError("PHASE5_INFERENCE_FAIL configuration");Application.Quit(1);}}
 void FixedUpdate(){var a=GetComponent<Agent>();if(previous==a.StepCount)return;previous=a.StepCount;total++;moved|=(transform.position-origin).sqrMagnitude>.01f;if(total>=128){bool pass=moved && !Academy.Instance.IsCommunicatorOn;Debug.Log(pass?"PHASE5_INFERENCE_PASS 128 steps, exported model, movement, no communicator":"PHASE5_INFERENCE_FAIL no movement");Application.Quit(pass?0:1);}}
}
