using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.Text;

public class Player : NetworkBehaviour
{
    [SerializeField] private float      _moveSpeed = 5.0f;
    [SerializeField] private float      _turnSpeed = 5.0f;
    [SerializeField] private LayerMask  _mouseDetectionLayer;
    [SerializeField] private Transform  _shootPoint;
    [SerializeField] private Bullet     _localBulletPrefab;
    [SerializeField] private Bullet     _networkBulletPrefab;

    private Vector3             _movementVelocity;
    private CharacterController _controller;
    private NetworkObject       _networkObject;
    private int                 _projectileId;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _networkObject = GetComponent<NetworkObject>();
        _projectileId = 0;
        //Cursor.lockState = CursorLockMode.Confined;
    }

    private void Start()
    {
        _controller.enabled = true;
    }

    private void Update()
    {
        if (_networkObject.IsLocalPlayer)
        {
            RotateToMouse();

            if (Input.GetMouseButtonDown(0)) Shoot(_shootPoint.position, _shootPoint.rotation);
        }
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
}
