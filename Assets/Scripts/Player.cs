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
    public Team     Team;

    [SerializeField] private float      _moveSpeed = 5.0f;
    [SerializeField] private float      _turnSpeed = 5.0f;
    [SerializeField] private LayerMask  _mouseDetectionLayer;
    [SerializeField] private Transform  _shootPoint;
    [SerializeField] private Bullet     _localBulletPrefab;
    [SerializeField] private Bullet     _networkBulletPrefab;
    [SerializeField] private TextMeshProUGUI _hpText;
    [SerializeField] private Renderer _bodyRenderer;
    [SerializeField] private Material _normalBodyMaterial;
    [SerializeField] private Material _damagedBodyMaterial;

    private Vector3             _movementVelocity;
    private CharacterController _controller;
    private NetworkObject       _networkObject;
    private int                 _projectileId;
    private int                 _deaths;
    private CanvasManager       _canvasManager;

    private NetworkVariable<float> _health = new();

    public event Action<Team, int> PlayerDied;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _networkObject = GetComponent<NetworkObject>();
        _projectileId = 0;
        _deaths = 0;
    }


    private void Start()
    {
        _controller.enabled = true;
        _canvasManager = FindObjectOfType<CanvasManager>();
        Debug.Log(Team);

        if (NetworkManager.Singleton.IsServer)
            _health.Value = MAX_HEALTH;

        _health.OnValueChanged += HealthOnValueChanged;

        FindObjectOfType<MatchManager>().PlayerPrefabInstantiated();
    }

    private void Update()
    {
        if (_networkObject.IsLocalPlayer)
        {
            RotateToMouse();

            if (Input.GetMouseButtonDown(0)) Shoot(_shootPoint.position, _shootPoint.rotation);
        }

        _hpText.transform.parent.transform.LookAt(Camera.main.transform);
        _hpText.text = $"HP: {_health.Value}";
    }

    private void FixedUpdate()
    {
        if (_networkObject.IsLocalPlayer)
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
            // Vector3 target = hitInfo.point;
            // target.y = transform.position.y;
            // transform.LookAt(target);

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
        var spawnedObj = Instantiate(_networkBulletPrefab, pos, rotation);
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

    [ClientRpc]
    private void UpdateHeatlhClientRpc(float newHealth)
    {
        StartCoroutine(DisplayHitFeedback());
        _health.Value = newHealth;
    }

    [ClientRpc]
    private void UpdateScoreUIClientRpc(Team team, string score)
    {
        //_canvasManager.UpdateScoreUI(team, score);
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

    private void HealthOnValueChanged(float oldValue, float newValue)
    {
        if (oldValue > newValue) StartCoroutine(DisplayHitFeedback());

        if (_health.Value <= 0)
        {
            _deaths++;
            //_canvasManager.UpdateScoreUI(Team, _deaths.ToString());
            Team teamToUpdate = Team == Team.Blue ? Team.Green : Team.Blue;
            OnPlayerDied(teamToUpdate, _deaths);
        }

        Debug.Log($"Player {PlayerId} took damage. Old health = {oldValue} | New health = {_health.Value}");
    }

    private void OnPlayerDied(Team team, int deathAmount)
    {
        PlayerDied?.Invoke(team, deathAmount);
    }
}
