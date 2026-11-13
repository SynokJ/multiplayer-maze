using UnityEngine;

/// <summary>
/// View: turns a character's movement into the move_left / move_right bools that the
/// character animators use. It watches the transform, so it behaves the same on every peer
/// (NetworkTransform moves the object there, the Rigidbody2D velocity is not available).
/// </summary>
public class MoveAnimator : MonoBehaviour
{
    private const float _IDLE_SPEED = 0.2f;
    private const float _TURN_SPEED = 0.1f;

    private static readonly int _MOVE_LEFT = Animator.StringToHash("move_left");
    private static readonly int _MOVE_RIGHT = Animator.StringToHash("move_right");

    private Animator _animator;
    private Vector2 _last;
    private bool _left;
    private bool _right;

    public void SetAnimator(Animator animator)
    {
        _animator = animator;
        _last = transform.position;
        _left = false;
        _right = false;
    }

    private void LateUpdate()
    {
        if (_animator == null || Time.deltaTime <= 0.0f)
            return;

        Vector2 position = transform.position;
        Vector2 velocity = (position - _last) / Time.deltaTime;
        _last = position;

        if (velocity.sqrMagnitude < _IDLE_SPEED * _IDLE_SPEED)
            Apply(false, false);
        else if (velocity.x > _TURN_SPEED)
            Apply(false, true);
        else if (velocity.x < -_TURN_SPEED)
            Apply(true, false);
        // Moving straight up or down keeps the last side, like the old game.
    }

    private void Apply(bool left, bool right)
    {
        if (left == _left && right == _right)
            return;

        _left = left;
        _right = right;
        _animator.SetBool(_MOVE_LEFT, left);
        _animator.SetBool(_MOVE_RIGHT, right);
    }
}
