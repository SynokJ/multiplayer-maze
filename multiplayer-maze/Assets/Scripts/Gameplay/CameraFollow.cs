using UnityEngine;

/// <summary>
/// View: keeps the camera on the local player's avatar.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private float _smoothTime = 0.1f;

    private Transform _target;
    private Vector3 _velocity;

    public void SetTarget(Transform target)
    {
        _target = target;
        if (target != null)
            transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
    }

    private void LateUpdate()
    {
        if (_target == null)
            return;

        Vector3 goal = new Vector3(_target.position.x, _target.position.y, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, goal, ref _velocity, _smoothTime);
    }
}
