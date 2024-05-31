using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.Text;

public class Player : MonoBehaviour
{
    [SerializeField] private float      _moveSpeed = 5.0f;
    [SerializeField] private float      _turnSpeed = 5.0f;
    [SerializeField] private LayerMask  _mouseDetectionLayer;

    private Vector3             _movementVelocity;
    private CharacterController _controller;
    private NetworkObject       _networkObject;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _networkObject = GetComponent<NetworkObject>();
        //Cursor.lockState = CursorLockMode.Confined;
    }

    private void Start()
    {
        _controller.enabled = true;
    }

    private void Update()
    {
        if (_networkObject.IsLocalPlayer)
            RotateToMouse();
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
}
