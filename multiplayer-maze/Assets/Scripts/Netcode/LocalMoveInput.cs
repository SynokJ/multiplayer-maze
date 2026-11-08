using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads the keyboard (WASD or arrows) and raises the move direction when it changes.
/// Only the local player's avatar enables it; it decides nothing about the game.
/// </summary>
public class LocalMoveInput : MonoBehaviour
{
    private Vector2 _last;

    public event Action<Vector2> MoveChanged;

    private void OnDisable()
    {
        _last = Vector2.zero;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        Vector2 move = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1.0f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1.0f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1.0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1.0f;

        move = Vector2.ClampMagnitude(move, 1.0f);
        if (move == _last)
            return;

        _last = move;
        MoveChanged?.Invoke(move);
    }
}
