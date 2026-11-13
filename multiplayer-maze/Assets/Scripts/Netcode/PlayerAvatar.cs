using Unity.Netcode;
using UnityEngine;

/// <summary>
/// A player's body in the maze. The owner sends only a move direction; the server moves the
/// Rigidbody2D (so walls stop it) and NetworkTransform shows the result to everyone.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(LocalMoveInput))]
public class PlayerAvatar : NetworkBehaviour
{
    [SerializeField] private float _speed = 8.0f;
    [SerializeField] private SpriteRenderer _renderer;

    private Rigidbody2D _body;
    private LocalMoveInput _input;
    private Vector2 _direction;
    private bool _canMove;

    public ulong PlayerId => OwnerClientId;

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _input = GetComponent<LocalMoveInput>();
        _input.enabled = false;

        if (_renderer == null)
            _renderer = GetComponentInChildren<SpriteRenderer>();

        if (_renderer != null && _renderer.sprite == null)
            _renderer.sprite = PlainSprites.Circle;
    }

    public override void OnNetworkSpawn()
    {
        if (_renderer != null)
            _renderer.color = Color.HSVToRGB((OwnerClientId * 0.19f) % 1.0f, 0.65f, 1.0f);

        if (!IsOwner)
            return;

        _input.enabled = true;
        _input.MoveChanged += OnMoveChanged;

        var follow = FindAnyObjectByType<CameraFollow>();
        if (follow != null)
            follow.SetTarget(transform);
    }

    public override void OnNetworkDespawn()
    {
        _input.MoveChanged -= OnMoveChanged;
        _input.enabled = false;
    }

    // Server only: the round decides when players may move.
    public void SetMovementEnabled(bool enabled)
    {
        _canMove = enabled;
        if (!enabled)
            _direction = Vector2.zero;
    }

    private void FixedUpdate()
    {
        if (IsServer)
            _body.linearVelocity = _canMove ? _direction * _speed : Vector2.zero;
    }

    private void OnMoveChanged(Vector2 direction) => MoveRpc(direction);

    // Anyone may call it, so the server checks the sender and the length.
    [Rpc(SendTo.Server)]
    public void MoveRpc(Vector2 direction, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        _direction = Vector2.ClampMagnitude(direction, 1.0f);
    }
}
