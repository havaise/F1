using System;
using UnityEngine;

public class SuspensionModule : MonoBehaviour
{
    [Header("Suspension Points")]
    [SerializeField] private Transform fl; // Переднее левое крепление подвески
    [SerializeField] private Transform fr; // Переднее правое
    [SerializeField] private Transform rl; // Заднее левое
    [SerializeField] private Transform rr; // Заднее правое

    [Header("Suspension Settings")]
    [SerializeField] private float restLength = 0.4f;
    // Нормальная (не нагруженная) длина подвески — расстояние до колеса в покое.
    [SerializeField] private float springTravel = 0.2f;
    // Максимальное сжатие подвески: ход пружины.
    [SerializeField] private float springStiffness = 20000f;
    // Жёсткость пружины k (N/m). Чем выше — тем жестче подвеска.
    [SerializeField] private float damperStiffness = 3500f;
    // Коэффициент демпфера: сопротивление скорости сжатия/расширения.
    [SerializeField] private float wheelRadius = 0.35f;
    // Радиус колеса — нужен чтобы понять, где именно оно “касается” дороги.

    [Header("Anti-Roll Bar")]
    [SerializeField] private float frontAntiRollStiffness = 8000f; // жёсткость переднего ARB
    [SerializeField] private float rearAntiRollStiffness = 6000f; // жёсткость заднего ARB

    [Header("Telemetry")]
    [SerializeField] private bool showTelemetry = true;

    private Rigidbody _rb;

    private float _lastFL;
    private float _lastFR;
    private float _lastRL;
    private float _lastRR;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>(); // Берём Rigidbody при запуске.
    }

    private void FixedUpdate()
    {
        // 1) сначала симулируем подвески - эти методы обновляют last*compression
        StepWheel(fl, ref _lastFL);
        StepWheel(fr, ref _lastFR);
        StepWheel(rl, ref _lastRL);
        StepWheel(rr, ref _lastRR);

        // 2) затем применяем анти-ролл силы, используя уже вычисленные compression
        ApplyAntiRoll();
    }

    private void ApplyAntiRoll()
    {
        // Передняя ось
        float frontDiff = _lastFL - _lastFR;
        float frontForce = frontDiff * frontAntiRollStiffness;

        // логика: если сжатие > 0 — колесо в контакте (можно сделать флаг touched, если есть)
        if (_lastFL > -0.0001f) // простая проверка контакта
            _rb.AddForceAtPosition(-transform.up * frontForce, fl.position, ForceMode.Force);

        if (_lastFR > -0.0001f)
            _rb.AddForceAtPosition(transform.up * frontForce, fr.position, ForceMode.Force);

        // Задняя ось
        float rearDiff = _lastRL - _lastRR;
        float rearForce = rearDiff * rearAntiRollStiffness;

        if (_lastRL > -0.0001f)
            _rb.AddForceAtPosition(-transform.up * rearForce, rl.position, ForceMode.Force);

        if (_lastRR > -0.0001f)
            _rb.AddForceAtPosition(transform.up * rearForce, rr.position, ForceMode.Force);
    }

    private void StepWheel(Transform pivot, ref float lastCompression)
    {
        Vector3 origin = pivot.position;
        Vector3 dir = -pivot.up;

        float maxDist = restLength + springTravel + wheelRadius;
        if (!Physics.Raycast(origin, dir, out RaycastHit hit, maxDist))
            return;

        float currentLen = hit.distance - wheelRadius;

        // Ограничение хода подвески
        currentLen = Mathf.Clamp(currentLen, restLength - springTravel, restLength + springTravel);

        // Сжатие пружины (x = Lrest - Lcurrent)
        float compression = restLength - currentLen;

        // Сила пружины: F = k * x
        float springForce = compression * springStiffness;

        // Скорость сжатия (v)
        float compVel = (compression - lastCompression) / Time.fixedDeltaTime;

        // Сила демпфера: F = c * v
        float damperForce = compVel * damperStiffness;

        lastCompression = compression;

        float total = springForce + damperForce;

        // Направление силы вверх по оси подвески
        Vector3 force = pivot.up * total;

        // Применение силы создаёт вертикальные колебания и крен
        _rb.AddForceAtPosition(force, pivot.position, ForceMode.Force);
    }

    private void OnGUI()
    {
        if (!showTelemetry) return;
        if (_rb == null) return;

        var box = new Rect(12f, 290f, 420f, 300f);

        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.Box(box, GUIContent.none);
        GUI.color = prev;

        var s = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = Color.white }
        };

        float x = box.x + 12f;
        float y = box.y + 10f;
        float w = box.width - 24f;

        float speedMs = _rb.linearVelocity.magnitude;
        float speedKmh = speedMs * 3.6f;

        // Анти-ролл: то же, что в ApplyAntiRoll(), только для вывода
        float frontDiff = _lastFL - _lastFR;
        float frontForce = frontDiff * frontAntiRollStiffness;

        float rearDiff = _lastRL - _lastRR;
        float rearForce = rearDiff * rearAntiRollStiffness;

        // Восстанавливаем spring/damper из уже имеющихся lastCompression (и параметров)
        // lastCompression == compression на текущем шаге.
        float dt = Time.fixedDeltaTime;

        // Чтобы получить compVel без хранения прошлого значения:
        // compVel = (compression - prevCompression) / dt  =>  prevCompression = compression - compVel*dt
        // Но compVel мы тоже не храним, поэтому считаем "как будто" compVel=0 для вывода демпфера нельзя.
        // Выход: оценить compVel через то, что SpringForce/total мы не храним — значит честнее выводить только springForce (статическую часть) + compression.
        // Если нужен damper тоже — придётся хранить хотя бы прошлую compression отдельно (это уже новая переменная).
        float flCompression = _lastFL;
        float frCompression = _lastFR;
        float rlCompression = _lastRL;
        float rrCompression = _lastRR;

        float flSpring = flCompression * springStiffness;
        float frSpring = frCompression * springStiffness;
        float rlSpring = rlCompression * springStiffness;
        float rrSpring = rrCompression * springStiffness;

        GUI.Label(new Rect(x, y, w, 18f), "SUSPENSION TELEMETRY", s); y += 20f;

        GUI.Label(new Rect(x, y, w, 18f),
            $"Speed: {speedMs:0.0} m/s ({speedKmh:0.0} km/h)", s); y += 18f;

        GUI.Label(new Rect(x, y, w, 18f),
            $"ARB front: {frontForce:0} N | rear: {rearForce:0} N", s); y += 18f;

        GUI.Label(new Rect(x, y, w, 18f),
            $"FL comp: {flCompression:0.000} m | Spring: {flSpring:0} N", s); y += 18f;

        GUI.Label(new Rect(x, y, w, 18f),
            $"FR comp: {frCompression:0.000} m | Spring: {frSpring:0} N", s); y += 18f;

        GUI.Label(new Rect(x, y, w, 18f),
            $"RL comp: {rlCompression:0.000} m | Spring: {rlSpring:0} N", s); y += 18f;

        GUI.Label(new Rect(x, y, w, 18f),
            $"RR comp: {rrCompression:0.000} m | Spring: {rrSpring:0} N", s);
    }
}
