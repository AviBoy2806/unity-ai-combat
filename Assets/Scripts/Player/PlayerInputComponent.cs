
using UnityEngine;
using UnityEngine.InputSystem;
using GameCombat;
using System.Collections.Generic;

public class PlayerInputComponent : MonoBehaviour
{
    [System.Serializable]
    public class PlayerInput
    {
        public Vector2 movement;
        public Vector2 look;

        public bool attackPressed;
        public bool attackJustPressed;

        public bool jumpTriggered;

        public bool runPressed;
        public bool runJustPressed;

        public bool lockOnJustPressed;
    }

    [HideInInspector]
    public PlayerInput input = new PlayerInput();
    public bool HudInputActive { get; private set; }
    public void SetHudInput(bool active){if(HudInputActive==active)return;HudInputActive=active;ResetEpisodeState();}
    public void ResetEpisodeState(){ClearCombat();guardEdges.Clear();guardHeld=false;input=new PlayerInput();}

    readonly Queue<(CombatRequest request,bool selectAttack)> combatRequests=new();
    readonly Queue<bool> guardEdges=new(); bool guardHeld;
    public void QueueCombat(CombatRequest request){if(combatRequests.Count<8)combatRequests.Enqueue((request,false));}
    public CombatRequest ConsumeCombat(){if(combatRequests.Count==0)return CombatRequest.None;var next=combatRequests.Dequeue();return next.selectAttack?(input.runPressed && input.movement.sqrMagnitude>0?CombatRequest.Heavy:CombatRequest.Light):next.request;}
    public void ClearCombat(){combatRequests.Clear();input.attackJustPressed=false;}
    void OnDisable(){ResetEpisodeState();}

    public bool ConsumeGuard(){if(guardEdges.Count>0)guardHeld=guardEdges.Dequeue();return guardHeld;}
    public void OnBlock(InputValue value){if(!HudInputActive)SetBlock(value.isPressed);}
    public void SetBlock(bool value){if(guardEdges.Count<16)guardEdges.Enqueue(value);}
    // Discard held movement/look as well as combat edges when focus is lost.
    // A release event may arrive while the Game view is not receiving input.
    void OnApplicationFocus(bool focus){if(!focus)ResetEpisodeState();}
    private void LateUpdate()
    {
        input.attackJustPressed = false;
        input.runJustPressed = false;
        input.lockOnJustPressed = false;
    }

    public void OnMove(InputValue value)
    {
        if(HudInputActive)return;
        input.movement = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        if(HudInputActive)return;
        input.look = value.Get<Vector2>();
    }

    public void OnAttack(InputValue value)
    {
        if(HudInputActive)return;
        input.attackPressed = value.isPressed;

        if (value.isPressed)
        {
            input.attackJustPressed = true;
            if(combatRequests.Count<8)combatRequests.Enqueue((CombatRequest.Light,true));
        }
    }

    public void OnRun(InputValue value)
    {
        if(HudInputActive)return;
        input.runPressed = value.isPressed;

        if (value.isPressed)
        {
            input.runJustPressed = true;
            QueueCombat(CombatRequest.Dodge);
        }
    }

    public void OnJump(InputValue value)
    {
        if(HudInputActive)return;
        input.jumpTriggered = value.isPressed;
    }

    public void OnLockOn(InputValue value)
    {
        if(HudInputActive)return;
        if (value.isPressed)
            input.lockOnJustPressed = true;
    }
}
