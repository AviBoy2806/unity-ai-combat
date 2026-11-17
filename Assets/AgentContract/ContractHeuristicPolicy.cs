using System;
using UnityEngine;
namespace GameCombat.AgentContract {
// Explicit, package-free test policy; consumes only the same arrays as ML.
// Never selected by a prefab or ordinary gameplay by default.
public sealed class ContractHeuristicPolicy : MonoBehaviour,ICombatPolicy {
 public int movement,combat; public bool holdCallback; public DecisionFrame LastFrame {get;private set;}
 public Action<uint,uint,int,int> LastCallback {get;private set;}
 public bool CanActivate(string hash,out string reason){reason="Contract heuristic test";return isActiveAndEnabled;}
 public void Decide(DecisionFrame frame,Action<uint,uint,int,int> receive){LastFrame=frame;LastCallback=receive;if(!holdCallback)receive(frame.generation,frame.sequence,frame.movementAllowed[movement]?movement:0,frame.combatAllowed[combat]?combat:0);}
 public void Deactivate(){} // Keep old callback deliberately so validation can try it.
}
}
