using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Bullet : NetworkBehaviour
{
    public ulong    PlayerId;
    public int      ProjectileId;
    public float    ShotTime;
    public Vector3  Origin;
    public Vector3  Direction;
    

    // public NetworkVariable<ulong> RemotePlayerId;
    // public NetworkVariable<int> RemoteProjectileId;
    // public NetworkVariable<float> RemoteShotTime = new();
    // public NetworkVariable<Vector3> RemoteOrigin;
    // public NetworkVariable<Vector3> RemoteDirection;
    
    [SerializeField] private float _damage  = 50.0f;
    [SerializeField] private float _speed   = 20.0f;
    [SerializeField] private LayerMask _hitDetectionLayer;

    private Vector3 _prevPos;


    private void Start()
    {
        // if (ShotTime > 0)
        //     _prevPos = Origin;
        // else
        //     _prevPos = RemoteOrigin.Value;
    }
    private void Update()
    {
        ComputePosition();

        if (NetworkManager.Singleton.IsServer)
        {
            var hitCheckDir = transform.position - _prevPos;
            var hits = Physics.RaycastAll(_prevPos, hitCheckDir, hitCheckDir.magnitude, _hitDetectionLayer);

            if (hits.Length > 0) Destroy(gameObject);

            _prevPos = transform.position;
        }
    }

    private void ComputePosition()
    {
        float elapsedTime = NetworkManager.Singleton.ServerTime.TimeAsFloat - ShotTime;
        transform.position = Origin + Direction * elapsedTime * _speed;
    }

    // private void OnTriggerEnter(Collider other)
    // {
    //     if (!NetworkManager.Singleton.IsServer) return;

    //     Debug.Log(other.transform.name);
    //     Destroy(gameObject);
    // }

    private void DestroyLocalProjectile(int projectileId, ulong playerId)
    {
        Bullet[] bullets = FindObjectsOfType<Bullet>();
        foreach (var proj in bullets)
        {
            // Use the new value of ProjectileId to find the predicted projectile
            if ((proj.PlayerId == playerId) && (proj.ProjectileId == projectileId) && (proj != this))
            {
                Destroy(proj.gameObject);
                break;
            }
        }
    }

    public void SyncClients()
    {
        SyncClientsClientRpc(Origin, Direction, ShotTime, ProjectileId, PlayerId);
    }

    [ClientRpc]
    private void SyncClientsClientRpc(Vector3 origin, Vector3 dir, float shotTime, int projectileId, ulong playerId)
    {
        Origin = _prevPos = origin;
        Direction = dir;
        ShotTime = shotTime;;
        ProjectileId = projectileId;
        PlayerId = playerId;

        DestroyLocalProjectile(ProjectileId, PlayerId);
    }
}
