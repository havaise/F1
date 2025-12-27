using System;
using UnityEngine;

public class AeroModule : MonoBehaviour
{
    [Header("Aero Drag")]
    [SerializeField] private float airDensity = 1.225f;
    [SerializeField] private float dragCoefficient = 0.9f; // Cx
    [SerializeField] private float frontalArea = 0.6f; // A (м²)

    [Header("Rear Wing")]
    [SerializeField] private Transform rearWing;
    [SerializeField] private float wingArea = 0.4f; // м²
    [SerializeField] private float liftCoefficientSlope = 0.05f; // k
    [SerializeField] private float wingAngleDeg = 10f; // угол атаки

    [Header("Ground Effect")]
    [SerializeField] private float groundEffectStrength = 3000f;
    [SerializeField] private float groundRayLength = 1.0f;

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        ApplyDragInternal();
        ApplyWingDownforceInternal();
        ApplyGroundEffectInternal();
    }

    private void ApplyDragInternal()
    {
        Vector3 velocity = _rb.linearVelocity;
        float speed = velocity.magnitude;

        if (speed < 0.01f)
            return;

        float dragForce = 0.5f * airDensity * dragCoefficient * frontalArea * speed * speed;
        Vector3 dragVector = -velocity.normalized * dragForce;

        _rb.AddForce(dragVector, ForceMode.Force);
    }

    private void ApplyWingDownforceInternal()
    {
        if (rearWing == null)
            return;

        float speed = _rb.linearVelocity.magnitude;
        if (speed < 0.01f)
            return;

        float alphaRad = wingAngleDeg * Mathf.Deg2Rad;
        float cl = liftCoefficientSlope * alphaRad;

        float downforce = 0.5f * airDensity * cl * wingArea * speed * speed;
        Vector3 downforceVector = -transform.up * downforce;

        _rb.AddForceAtPosition(downforceVector, rearWing.position, ForceMode.Force);
    }

    private void ApplyGroundEffectInternal()
    {
        if (!Physics.Raycast(transform.position, -transform.up, out RaycastHit hit, groundRayLength))
            return;

        float h = hit.distance; // высота над землёй
        if (h < 0.01f)
            h = 0.01f;

        float geForce = groundEffectStrength / h;
        Vector3 geVector = -transform.up * geForce;

        _rb.AddForce(geVector, ForceMode.Force);
    }
}
