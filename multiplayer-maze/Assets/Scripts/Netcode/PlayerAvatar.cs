using Unity.Netcode;
using UnityEngine;

/// <summary>
/// A player's body in the maze. The owner sends only a move direction; the server moves the
/// Rigidbody2D (so walls stop it) and NetworkTransform shows the result to everyone.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(LocalMoveInput), typeof(MoveAnimator))]
public class PlayerAvatar : NetworkBehaviour
{
    private const int _SORTING_ORDER = 10;

    [SerializeField] private float _speed = 8.0f;
    [Tooltip("Character art (sprite + animator). Player n gets skin n, wrapping around.")]
    [SerializeField] private GameObject[] _skins;

    private Rigidbody2D _body;
    private LocalMoveInput _input;
    private MoveAnimator _moveAnimator;
    private Vector2 _direction;
    private bool _canMove;

    public ulong PlayerId => OwnerClientId;

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _input = GetComponent<LocalMoveInput>();
        _moveAnimator = GetComponent<MoveAnimator>();
        _input.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        ShowSkin();

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

    // The owner id is the same on every peer, so everyone picks the same skin without any sync.
    private void ShowSkin()
    {
        if (_skins == null || _skins.Length == 0)
        {
            Debug.LogWarning("PlayerAvatar has no skins assigned.", this);
            return;
        }

        GameObject skin = Instantiate(_skins[(int)(OwnerClientId % (ulong)_skins.Length)], transform);

        foreach (SpriteRenderer spriteRenderer in skin.GetComponentsInChildren<SpriteRenderer>())
            spriteRenderer.sortingOrder = _SORTING_ORDER;

        if (_moveAnimator != null)
            _moveAnimator.SetAnimator(skin.GetComponentInChildren<Animator>());
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
