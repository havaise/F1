using System;
using UnityEngine;

public class CarSuspension : MonoBehaviour
{
    [Header("Suspension Points")]
    [SerializeField] private Transform fl;
    [SerializeField] private Transform fr;
    [SerializeField] private Transform rl;
    [SerializeField] private Transform rr;

    [Header("Suspension Settings")]
    [SerializeField] private float restLength = 0.4f;
    [SerializeField] private float springTravel = 0.2f;
    [SerializeField] private float springStiffness = 20000f;
    [SerializeField] private float damperStiffness = 3500f;
    [SerializeField] private float wheelRadius = 0.35f;

    private Rigidbody rb;
    private float lastFLcompression;
    private float lastFRcompression;
    private float lastRLcompression;
    private float lastRRcompression;

    [Header("Anti-Roll Bar")]
    [SerializeField] private float frontAntiRollStiffness = 8000f;
    [SerializeField] private float rearAntiRollStiffness = 6000f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        SimulateWheel(fl, ref lastFLcompression);
        SimulateWheel(fr, ref lastFRcompression);
        SimulateWheel(rl, ref lastRLcompression);
        SimulateWheel(rr, ref lastRRcompression);

        ApplyAntiRollBars();
    }

    private void ApplyAntiRollBars()
    {
        float frontDiff = lastFLcompression - lastFRcompression;
        float frontForce = frontDiff * frontAntiRollStiffness;
        if (lastFLcompression > -0.0001f) rb.AddForceAtPosition(-transform.up * frontForce, fl.position, ForceMode.Force);
        if (lastFRcompression > -0.0001f) rb.AddForceAtPosition(transform.up * frontForce, fr.position, ForceMode.Force);

        float rearDiff = lastRLcompression - lastRRcompression;
        float rearForce = rearDiff * rearAntiRollStiffness;
        if (lastRLcompression > -0.0001f) rb.AddForceAtPosition(-transform.up * rearForce, rl.position, ForceMode.Force);
        if (lastRRcompression > -0.0001f) rb.AddForceAtPosition(transform.up * rearForce, rr.position, ForceMode.Force);
    }

    private void SimulateWheel(Transform pivot, ref float lastCompression)
    {
        Vector3 origin = pivot.position;
        Vector3 direction = -pivot.up;
        float maxDist = restLength + springTravel + wheelRadius;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDist))
        {
            float currentLength = hit.distance - wheelRadius;
            currentLength = Mathf.Clamp(currentLength, restLength - springTravel, restLength + springTravel);

            float compression = restLength - currentLength;
            float springForce = compression * springStiffness;

            float compressionVelocity = (compression - lastCompression) / Time.fixedDeltaTime;
            float damperForce = compressionVelocity * damperStiffness;

            lastCompression = compression;
            float totalForce = springForce + damperForce;

            Vector3 force = pivot.up * totalForce;
            rb.AddForceAtPosition(force, pivot.position, ForceMode.Force);
        }
    }

    private void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.green;

        // Отрисовка телеметрии подвески в правом углу
        float screenWidth = Screen.width;
        GUI.Box(new Rect(screenWidth - 220, 10, 210, 120), "SUSPENSION DATA");
        
        GUILayout.BeginArea(new Rect(screenWidth - 210, 35, 200, 100));
        GUILayout.Label($"FL Compression: {lastFLcompression:F3}m", style);
        GUILayout.Label($"FR Compression: {lastFRcompression:F3}m", style);
        GUILayout.Label($"RL Compression: {lastRLcompression:F3}m", style);
        GUILayout.Label($"RR Compression: {lastRRcompression:F3}m", style);
        GUILayout.EndArea();
    }
}
