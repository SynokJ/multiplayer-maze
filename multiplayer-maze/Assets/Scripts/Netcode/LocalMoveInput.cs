using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads the local player's wish to move (WASD or arrows, or the on-screen joystick) and raises
/// it when it changes. Only the local player's avatar enables it; it decides nothing about the game.
/// </summary>
public class LocalMoveInput : MonoBehaviour
{
    // Steps of 0.1 keep a slowly moving finger from sending a message every frame.
    private const float _STEP = 0.1f;

    private FloatingJoystick _joystick;
    private Vector2 _last;

    public event Action<Vector2> MoveChanged;

    private void OnEnable()
    {
        _joystick = FindAnyObjectByType<FloatingJoystick>();
        if (_joystick != null)
            _joystick.SetListening(true);
    }

    private void OnDisable()
    {
        if (_joystick != null)
            _joystick.SetListening(false);

        _last = Vector2.zero;
    }

    private void Update()
    {
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
