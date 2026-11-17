using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Keep synthetic validation input isolated, including assertion/exception paths.
internal sealed class ValidationInputScope : IDisposable
{
    readonly InputSettings settings = InputSystem.settings;
    readonly InputSettings.BackgroundBehavior background;
    readonly InputSettings.EditorInputBehaviorInPlayMode editorInput;
    readonly InputSettings.UpdateMode updateMode;
    readonly List<InputDevice> devices = new();
    readonly List<InputActionAsset> assets = new();
    readonly UnityEngine.InputSystem.PlayerInput player;
    readonly InputDevice[] previousDevices;
    readonly string previousMap;
    readonly string previousScheme;
    readonly bool wasActive;

    public ValidationInputScope(UnityEngine.InputSystem.PlayerInput player = null)
    {
        background = settings.backgroundBehavior;
        editorInput = settings.editorInputBehaviorInPlayMode;
        updateMode = settings.updateMode;
        this.player = player;
        if (player)
        {
            previousDevices = player.devices.ToArray();
            previousMap = player.currentActionMap?.name;
            previousScheme = player.currentControlScheme;
            wasActive = player.inputIsActive;
        }
    }

    public T AddDevice<T>() where T : InputDevice
    {
        var device = InputSystem.AddDevice<T>();
        devices.Add(device);
        return device;
    }

    public InputActionAsset CloneActions(InputActionAsset source)
    {
        var clone = UnityEngine.Object.Instantiate(source);
        assets.Add(clone);
        return clone;
    }

    public void Dispose()
    {
        try
        {
            if (player)
            {
                if (string.IsNullOrEmpty(previousScheme))
                    player.SwitchCurrentControlScheme(previousDevices);
                else
                    player.SwitchCurrentControlScheme(previousScheme, previousDevices);
                if (!string.IsNullOrEmpty(previousMap)) player.SwitchCurrentActionMap(previousMap);
                if (wasActive) player.ActivateInput(); else player.DeactivateInput();
            }
        }
        finally
        {
            try
            {
                foreach (var device in devices)
                    if (device.added) InputSystem.RemoveDevice(device);
                foreach (var asset in assets)
                    if (asset) UnityEngine.Object.DestroyImmediate(asset);
            }
            finally
            {
                settings.backgroundBehavior = background;
                settings.editorInputBehaviorInPlayMode = editorInput;
                settings.updateMode = updateMode;
            }
        }
    }
}
