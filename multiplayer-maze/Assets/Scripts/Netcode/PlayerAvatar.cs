using Unity.Netcode;
using UnityEngine;

/// <summary>
/// A player's body in the maze. The owner sends only a move direction; the server moves the
/// Rigidbody2D (so walls stop it) and NetworkTransform shows the result to everyone.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(LocalMoveInput), typeof(MoveAnimator))]
public class PlayerAvatar : NetworkBehaviour
{
    [SerializeField] private float _speed = 8.0f;
    [SerializeField] private CharacterSkinsSO _skins;

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
    }

    public override void OnNetworkSpawn()
    {
        ShowSkin();

        if (!IsOwner)
            return;

        _input.Begin();
        _input.MoveChanged += OnMoveChanged;

        var follow = FindAnyObjectByType<CameraFollow>();
        if (follow != null)
            follow.SetTarget(transform);
    }

    public override void OnNetworkDespawn()
    {
        _input.MoveChanged -= OnMoveChanged;
        _input.End();
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
        GameObject prefab = _skins != null ? _skins.Prefab(OwnerClientId) : null;
        if (prefab == null)
        {
            Debug.LogWarning("PlayerAvatar has no skins assigned.", this);
            return;
        }

        GameObject skin = Instantiate(prefab, transform);

        // The art is centred on the body, whatever position the skin prefab was saved with.
        skin.transform.localPosition = Vector3.zero;

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
