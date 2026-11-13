using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads the local player's wish to move (WASD or arrows, or the on-screen joystick) and raises
/// it when it changes. Only the local player's avatar calls Begin; it decides nothing about the game.
/// </summary>
public class LocalMoveInput : MonoBehaviour
{
    // Steps of 0.1 keep a slowly moving finger from sending a message every frame.
    private const float _STEP = 0.1f;

    private FloatingJoystick _joystick;
    private Vector2 _last;
    private bool _active;

    public event Action<Vector2> MoveChanged;

    // The joystick is shared by the whole scene, so only the avatar that owns the input may
    // switch it. (Enabling and disabling the component did it for every avatar, so another
    // player's avatar spawning or leaving turned the stick off for the local player.)
    public void Begin()
    {
        _joystick = FindAnyObjectByType<FloatingJoystick>();
        if (_joystick != null)
            _joystick.SetListening(true);

        _active = true;
    }

    public void End()
    {
        if (!_active)
            return;

        _active = false;
        _last = Vector2.zero;

        if (_joystick != null)
            _joystick.SetListening(false);
    }

    private void OnDestroy() => End();

    private void Update()
    {
        if (!_active)
            return;

        Vector2 move = ReadKeyboard();
        if (move == Vector2.zero && _joystick != null)
            move = _joystick.Value;

        move = Quantize(Vector2.ClampMagnitude(move, 1.0f));
        if (move == _last)
            return;

        _last = move;
        MoveChanged?.Invoke(move);
    }

    private static Vector2 ReadKeyboard()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return Vector2.zero;

        Vector2 move = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1.0f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1.0f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1.0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1.0f;
        return move;
    }

    private static Vector2 Quantize(Vector2 value)
        => new Vector2(Mathf.Round(value.x / _STEP) * _STEP, Mathf.Round(value.y / _STEP) * _STEP);
}
