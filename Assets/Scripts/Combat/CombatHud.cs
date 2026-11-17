using UnityEngine;
namespace GameCombat {
// Display only. Reads shared combat state; never submits commands or handles player input.
public sealed class CombatHud : MonoBehaviour {
 Combatant player; EnemyBrainHost[] enemies;float refresh;
 GUIStyle title,body,small,status;Texture2D pixel;
 readonly Color ink=new(.035f,.055f,.075f,.95f), muted=new(.82f,.87f,.9f),teal=new(.35f,.87f,.76f),gold=new(.94f,.71f,.36f),red=new(.93f,.38f,.35f);
 void Update(){if(Time.unscaledTime<refresh)return;refresh=Time.unscaledTime+.5f;player=GetComponent<Combatant>();enemies=FindObjectsByType<EnemyBrainHost>(FindObjectsSortMode.InstanceID);}
 void Init(){if(pixel)return;pixel=new Texture2D(1,1);pixel.SetPixel(0,0,Color.white);pixel.Apply();title=new GUIStyle(GUI.skin.label){fontSize=22,fontStyle=FontStyle.Bold};body=new GUIStyle(GUI.skin.label){fontSize=16};small=new GUIStyle(GUI.skin.label){fontSize=15};status=new GUIStyle(body){alignment=TextAnchor.MiddleRight};foreach(var style in new[]{title,body,small,status})style.normal.textColor=Color.white;}
 void Fill(Rect r,Color c){GUI.color=c;GUI.DrawTexture(r,pixel);GUI.color=Color.white;}
 void Text(Rect r,string s,GUIStyle style,Color c){GUI.color=c;GUI.Label(r,s,style);GUI.color=Color.white;}
 void Bar(float x,float y,float w,float value,Color c){Fill(new Rect(x,y,w,7),new Color(1,1,1,.09f));Fill(new Rect(x,y,w*Mathf.Clamp01(value),7),c);}
 public static string ActionLabel(Combatant a){var r=a.Runner;return a.Dead?"DEFEATED":r.Broken?"GUARD BROKEN":r.Action==CombatAction.Recoil?"DEFLECTED":r.Action==CombatAction.Stagger?"STAGGERED":r.Action==CombatAction.Dodge?"EVADING":r.Guarding?"GUARDING":r.Action==CombatAction.Attack?(r.Phase==AttackPhase.Windup?"WINDUP":r.Phase==AttackPhase.Active?"STRIKE":"RECOVERING"):"READY";}
 void ActorPanel(Rect box,Combatant a,string label,Color accent){Fill(box,ink);Fill(new Rect(box.x,box.y,3,box.height),accent);float x=box.x+20,y=box.y+12,w=box.width-40;
  Text(new Rect(x,y,w,28),label,title,Color.white);Text(new Rect(x,y,w,28),ActionLabel(a),status,a.Runner.Broken?gold:accent);
  y+=38;Text(new Rect(x,y,w,22),"VITALITY",small,muted);Text(new Rect(x,y,w,22),$"{Mathf.CeilToInt(a.Health.Health)} / {a.Health.maxHealth:0}",status,Color.white);Bar(x,y+26,w,a.Health.Health/Mathf.Max(1,a.Health.maxHealth),accent);
  y+=43;Text(new Rect(x,y,w,22),"POSTURE",small,muted);Bar(x,y+25,w,a.Posture/Mathf.Max(1,a.tuning.maxPosture),gold);
  if(a.Runner.Broken)Text(new Rect(x,y,w,22),$"{a.Runner.Remaining:0.0}s",status,gold);
 }
 public static string ModeLabel(EnemyBrainHost h)=>h.ActiveMode==EnemyControlMode.ML?"ML  /  EVALUATION ONLY":h.ActiveMode==EnemyControlMode.Test?"TEST CONTROLLER":"NORMAL AI";
 void OnGUI(){if(!player)return;Init();var old=GUI.matrix;float scale=Mathf.Min(Screen.width/1920f,Screen.height/1080f);if(scale<=0)return;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));float w=Screen.width/scale,h=Screen.height/scale;
  Fill(new Rect(36,24,130,32),ink);Text(new Rect(48,28,110,26),"COMBAT",small,muted);
  ActorPanel(new Rect(36,h-225,420,174),player,"PLAYER",teal);
  Fill(new Rect(36,h-45,w-72,30),ink);Text(new Rect(48,h-42,w-96,24),"WASD  Move     SPACE  Jump     SHIFT  Dodge / run     LMB  Attack     RMB  Guard     Q / MMB  Lock on",small,muted);
  if(enemies!=null){int row=0;foreach(var enemy in enemies){if(!enemy || !enemy.isActiveAndEnabled)continue;var a=enemy.GetComponent<Combatant>();if(a.arenaId!=player.arenaId)continue;float y=36+row*320;row++;
   float x=w-456;ActorPanel(new Rect(x,y,420,174),a,enemies.Length>1?"OPPONENT "+row:"OPPONENT",red);
   Fill(new Rect(x,y+180,420,124),ink);Text(new Rect(x+20,y+190,380,24),ModeLabel(enemy),body,enemy.ActiveMode==EnemyControlMode.ML?gold:teal);
   string selection=$"Selected: {enemy.mode}   /   Active: {enemy.ActiveMode}"+(enemy.HasPendingSwitch?"   /   Switching…":"");Text(new Rect(x+20,y+219,380,22),selection,small,muted);
   string info=!string.IsNullOrEmpty(enemy.FallbackReason)?"Fallback: "+enemy.FallbackReason:enemy.mode==EnemyControlMode.ML || enemy.ActiveMode==EnemyControlMode.ML?"Candidate evaluation • training quality gates failed":"Authored controller • shared combat timing";
   var wrap=new GUIStyle(small){wordWrap=true};Text(new Rect(x+20,y+246,380,50),info,wrap,!string.IsNullOrEmpty(enemy.FallbackReason)?gold:muted);
  }}GUI.matrix=old;
 }
 void OnDestroy(){if(pixel)Destroy(pixel);}
}
}

