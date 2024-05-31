using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.Text;

public class Player : MonoBehaviour
{
    [SerializeField] private float _turnSpeed = 5.0f;
    [SerializeField] private float _moveSpeed = 5.0f;
    [SerializeField] private LayerMask _mouseDetectionLayer;
    Plane plane = new Plane(Vector3.up, 0);
    private Vector3 mouseWorldPosition;
    private Vector3 mouseScreenPos;
    private Vector3 _movementVelocity;
    private Vector3 _rotationVelocity;
    private CharacterController _controller;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        //if (Input.GetMouseButtonDown(0)) 
        //LookAtMouse();

        RotateToMouse();
        //UpdateRotation();
    }

    private void FixedUpdate()
    {
        UpdateMovementVelocity();
        UpdatePosition();
    }

    private void RotateToMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, _mouseDetectionLayer))
        {
            Vector3 target = hitInfo.point;
            target.y = transform.position.y;
            transform.LookAt(target);
        }
    }

    private void UpdateMovementVelocity()
    {
        _movementVelocity.z = Input.GetAxis("Vertical") * _moveSpeed;
        _movementVelocity.x = Input.GetAxis("Horizontal") * _moveSpeed;
    }

    private void UpdateRotation()
    {
        //transform.Rotate(Input.GetAxis("Horizontal") * Vector3.up * (_turnSpeed * Time.deltaTime));

        transform.rotation = Quaternion.LookRotation(_movementVelocity, Vector3.up);
    }

    private void UpdatePosition()
    {
        Vector3 motion = _movementVelocity * Time.fixedDeltaTime;
        _controller.Move(transform.TransformVector(motion));
        //UpdateRotation();
    }
}
