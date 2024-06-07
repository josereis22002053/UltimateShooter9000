using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.Text;
using TMPro;
using System;

public class Player : NetworkBehaviour
{
    private const float MAX_HEALTH = 100.0f;

    public ulong    PlayerId;
    public string   UserName;
    public int      Elo;
    public Team     Team;

    [SerializeField] private float              _moveSpeed = 5.0f;
    [SerializeField] private float              _turnSpeed = 5.0f;
    [SerializeField] private float              _invulnerabilityTime = 3.0f;
    [SerializeField] private LayerMask          _mouseDetectionLayer;
    [SerializeField] private LayerMask          _canShootCheckLayer;
    [SerializeField] private Transform          _shootPoint;
    [SerializeField] private Transform          _canShootCheckPoint;
    [SerializeField] private Bullet             _localBulletPrefab;
    [SerializeField] private Bullet             _networkBulletPrefab;
    [SerializeField] private TextMeshProUGUI    _hpText;
    [SerializeField] private Renderer           _bodyRenderer;
    [SerializeField] private Material           _normalBodyMaterial;
    [SerializeField] private Material           _damagedBodyMaterial;
    
    private GameObject          _normalVXF;
    private GameObject          _invulnerableVXF;
    private Vector3             _movementVelocity;
    private CharacterController _controller;
    private NetworkObject       _networkObject;
    private int                 _projectileId;
    private int                 _deaths;
    private List<Transform>     _spawnPositions;
    private bool                _canMove;

    private NetworkVariable<float> _health = new();
    public NetworkVariable<bool> CanTakeDamage = new();

    public event Action<Team, int> PlayerDied;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _networkObject = GetComponent<NetworkObject>();
        _projectileId = 0;
        _deaths = 0;
        
        _normalVXF = transform.GetChild(0).gameObject;
        _invulnerableVXF = transform.GetChild(1).gameObject;

        _normalVXF.SetActive(true);
        _invulnerableVXF.SetActive(false);

        _spawnPositions = new List<Transform>();
        _canMove = false;
    }


    private void Start()
    {
        _controller.enabled = true;
        Debug.Log(Team);

        if (NetworkManager.Singleton.IsServer)
        {
            _health.Value = MAX_HEALTH;
            CanTakeDamage.Value = true;
        }
        else if (IsLocalPlayer)
        {
            var playerInfo = FindObjectOfType<ConnectedClientInfo>();
            if (playerInfo == null) Debug.LogError("Couldn't find player info!");
            else
            {
                UserName = playerInfo.UserName;
                Elo = playerInfo.Elo;
                SyncClientInfoServerRpc(UserName, Elo);
            }
        }  

        _health.OnValueChanged += HealthOnValueChanged;
        CanTakeDamage.OnValueChanged += CanTakeDamageOnValueChanged;

        MatchManager matchManager = FindObjectOfType<MatchManager>();
        matchManager.PlayerPrefabInstantiated();
        matchManager.gameStarted += EnableMovement;
        matchManager.GameEnded += DisableMovement;

        var spawnPositions = GameObject.FindGameObjectsWithTag("SpawnPosition");
        foreach (var spawn in spawnPositions)
            _spawnPositions.Add(spawn.transform);
    }

    private void Update()
    {
        if (_networkObject.IsLocalPlayer && _canMove)
        {
            RotateToMouse();

            if (Input.GetMouseButtonDown(0))
            {
                if (Physics.Raycast(_canShootCheckPoint.position, _canShootCheckPoint.forward,
                    out RaycastHit hitInfo, (_shootPoint.position - _canShootCheckPoint.position).magnitude,_canShootCheckLayer))
                {
                    Debug.Log("Not gonna shoot");
                    return;
                }
                Shoot(_shootPoint.position, _shootPoint.rotation);
            }
        }

        _hpText.transform.parent.transform.LookAt(Camera.main.transform);
        _hpText.text = $"HP: {_health.Value}";
    }

    private void FixedUpdate()
    {
        if (_networkObject.IsLocalPlayer && _canMove)
        {
            UpdateMovementVelocity();
            UpdatePosition();
        }
    }

    private void RotateToMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, _mouseDetectionLayer))
        {
            Vector3 target = hitInfo.point;
            target.y = transform.position.y;
            target = target - transform.position;
            Quaternion lookRot = Quaternion.LookRotation(target);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * _turnSpeed);
        }
    }

    private void UpdateMovementVelocity()
    {
        _movementVelocity.z = Input.GetAxis("Vertical") * _moveSpeed;
        _movementVelocity.x = Input.GetAxis("Horizontal") * _moveSpeed;
    }

    private void UpdatePosition()
    {
        Vector3 motion = _movementVelocity * Time.fixedDeltaTime;
        _controller.Move(transform.TransformVector(motion));
    }

    private void Shoot(Vector3 pos, Quaternion rot)
    {
        var bullet = Instantiate(_localBulletPrefab, _shootPoint.position, _shootPoint.rotation);
        bullet.ShotTime = NetworkManager.Singleton.ServerTime.TimeAsFloat;
        bullet.Origin = _shootPoint.position;
        bullet.Direction = _shootPoint.forward;
        bullet.PlayerId = OwnerClientId;
        bullet.ProjectileId = _projectileId;

        RequestBulletServerRpc(pos, rot, _shootPoint.forward,
                               NetworkManager.Singleton.ServerTime.TimeAsFloat, OwnerClientId, _projectileId);

        _projectileId++;
    }

    [ServerRpc]
    void RequestBulletServerRpc(Vector3 pos, Quaternion rotation, Vector3 direction, float shotTime, ulong playerId, int projectileId)
    {
        var spawnedObj = Instantiate(_localBulletPrefab, pos, rotation);
        spawnedObj.ShotTime = shotTime;
        spawnedObj.Origin = pos;
        spawnedObj.Direction = direction;
        spawnedObj.PlayerId =playerId;
        spawnedObj.ProjectileId = projectileId;

        var netObj = spawnedObj.GetComponent<NetworkObject>();
        netObj.Spawn();

        spawnedObj.SyncClients();
    }

    public void TakeDamage(float damageAmount)
    {
        _health.Value = Mathf.Clamp(_health.Value - damageAmount, 0 , 100);
    }

    private IEnumerator DisplayHitFeedback()
    {
        _bodyRenderer.material = _damagedBodyMaterial;
        yield return new WaitForSeconds(0.15f);
        _bodyRenderer.material = _normalBodyMaterial;
    }

    [ClientRpc]
    public void InitializePlayerClientRpc(ulong playerId, Team team)
    {
        PlayerId = playerId;
        Team = team;
    }

    [ServerRpc]
    private void SyncClientInfoServerRpc(string userName, int elo)
    {
        UserName = userName;
        Elo = elo;
    }

    private void HealthOnValueChanged(float oldValue, float newValue)
    {
        if (oldValue > newValue) StartCoroutine(DisplayHitFeedback());

        if (_health.Value <= 0)
        {
            _deaths++;
            Team teamToUpdate = Team == Team.Blue ? Team.Green : Team.Blue;

            if (IsServer) StartCoroutine(Respawn());

            OnPlayerDied(teamToUpdate, _deaths);
        }

        Debug.Log($"Player {PlayerId} took damage. Old health = {oldValue} | New health = {_health.Value}");
    }

    private void CanTakeDamageOnValueChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            _normalVXF.SetActive(true);
            _invulnerableVXF.SetActive(false);
        }
        else
        {
            _normalVXF.SetActive(false);
            _invulnerableVXF.SetActive(true);
        }
    }

    private IEnumerator Respawn()
    {
        CanTakeDamage.Value = false;
        SetPosition(GetRandomSpawnPosition());

        yield return new WaitForSeconds(_invulnerabilityTime);

        CanTakeDamage.Value = true;
        _health.Value = MAX_HEALTH;
    }

    private void SetPosition(Vector3 newPos)
    {
        if (IsServer)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { OwnerClientId }
                }
            };

            SetPositionClientRpc(newPos, clientRpcParams);
        }
    }

    [ClientRpc]
    private void SetPositionClientRpc(Vector3 newPos, ClientRpcParams clientRpcParams = default)
    {
        _controller.enabled = false;
        transform.position = newPos;
        _controller.enabled = true;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        return  _spawnPositions[UnityEngine.Random.Range(0, _spawnPositions.Count)].position;
    }

    private void OnPlayerDied(Team team, int deathAmount)
    {
        PlayerDied?.Invoke(team, deathAmount);
    }

    private void EnableMovement() => _canMove = true;
    private void DisableMovement(Team team) => _canMove = false;
}
