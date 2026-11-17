using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class InputReliabilityValidation
{
    const string Pending = "InputReliabilityValidation.Pending";
    static InputReliabilityValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            Verify();
        };
    }

    [MenuItem("Tools/Input/Enable full reload and verify input cleanup")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before verifying input cleanup.");
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.None;
        EditorSettings.enterPlayModeOptionsEnabled = false;
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static void Verify()
    {
        var initialDevices = InputSystem.devices.ToArray();
        var settings = InputSystem.settings;
        var background = settings.backgroundBehavior;
        var editorInput = settings.editorInputBehaviorInPlayMode;
        var update = settings.updateMode;
        var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Input/PlayerInputAction.inputactions");
        var originalJson = source.ToJson();
        var fixture = new GameObject("Input cleanup verification");
        fixture.SetActive(false);
        var sink = fixture.AddComponent<PlayerInputComponent>();
        var player = fixture.AddComponent<UnityEngine.InputSystem.PlayerInput>();
        player.neverAutoSwitchControlSchemes = true;
        player.notificationBehavior = PlayerNotifications.SendMessages;
        player.defaultActionMap = "Movement";
        try
        {
            try
            {
                using var scope = new ValidationInputScope();
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                player.actions = scope.CloneActions(source);
                fixture.SetActive(true);
                var keyboard = scope.AddDevice<Keyboard>();
                var mouse = scope.AddDevice<Mouse>();
                player.SwitchCurrentControlScheme(keyboard, mouse);
                player.ActivateInput();
                player.SwitchCurrentActionMap("Movement");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space, Key.LeftShift));
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(12, 4) }.WithButton(MouseButton.Left));
                InputSystem.Update();
                Require(sink.input.movement.y > 0, "W reaches OnMove through PlayerInput");
                Require(sink.input.look.sqrMagnitude > 0, "mouse delta reaches OnLook through PlayerInput");
                Require(sink.input.jumpTriggered && sink.input.runPressed && sink.input.attackPressed, "button callbacks reach PlayerInputComponent");
                sink.SendMessage("OnApplicationFocus", false);
                Require(sink.input.movement == Vector2.zero && sink.input.look == Vector2.zero && !sink.input.runPressed && !sink.input.jumpTriggered && !sink.input.attackPressed, "focus loss clears all held input");
                sink.input.movement = Vector2.one;
                sink.input.look = Vector2.one;
                sink.enabled = false;
                Require(sink.input.movement == Vector2.zero && sink.input.look == Vector2.zero, "disable clears held input");
                fixture.SetActive(false);
                throw new VerificationException();
            }
            catch (VerificationException) { }
            Require(initialDevices.SequenceEqual(InputSystem.devices), "exception cleanup removes only test devices");
            Require(settings.backgroundBehavior == background && settings.editorInputBehaviorInPlayMode == editorInput && settings.updateMode == update, "exception cleanup restores global input settings");
            Require(source.ToJson() == originalJson, "validation does not modify shared action asset");
            Debug.Log("INPUT_RELIABILITY_PASS: full domain/scene reload enabled; keyboard, mouse, focus/disable clearing and exception cleanup verified.");
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture); }
    }

    static void Require(bool ok, string name)
    {
        if (!ok) throw new Exception("INPUT_RELIABILITY_FAIL: " + name);
        Debug.Log("INPUT_RELIABILITY_CHECK: " + name);
    }

    sealed class VerificationException : Exception { }
}
