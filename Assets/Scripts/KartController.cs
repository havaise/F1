using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class KartController : MonoBehaviour
{
    [Header("Import parametrs")]
    [SerializeField] private bool import = false;
    [SerializeField] private KartConfig kartConfig;

    [Header("Wheel attachment points")]
    [SerializeField] private Transform frontLeftWheel;
    [SerializeField] private Transform frontRightWheel;
    [SerializeField] private Transform frontLeftWheelTransform;
    [SerializeField] private Transform frontRightWheelTransform;
    [SerializeField] private Transform rearLeftWheel;
    [SerializeField] private Transform rearRightWheel;

    [Header("Input (New Input System)")]
    [SerializeField] private InputActionAsset playerInput;

    [Header("Weight distribution")]
    [SerializeField, Range(0, 1)] private float frontAxisShare = 0.5f;

    [Header("Engine & drivetrain")]
    [SerializeField] private KartEngine engine;
    [SerializeField] private float gearRatio = 8f;
    [SerializeField] private float drivetrainEfficiency = 0.9f;

    [Header("Handbrake")]
    [SerializeField] private KeyCode handbrakeKey = KeyCode.Space;
    [SerializeField] private float handbrakeBrakeForce = 6000f;

    private InputAction moveAction;
    private float throttleInput;
    private float steepInput;
    private bool handbrakePressed;

    private float frontLeftNormalForce, frontRightNormalForce, rearLeftNormalForce, rearRightNormalForce;
    private Rigidbody rigidbody;
    private Vector3 g = Physics.gravity;

    [SerializeField] private float engineTorque = 400f;
    [SerializeField] private float wheelRadius = 0.3f;
    [SerializeField] private float maxSpeed = 20;

    [Header("Steering")]
    [SerializeField] private float maxSteeringAngle;
    private Quaternion frontLeftInitialRot;
    private Quaternion frontRightInitialRot;

    [Header("Tyre friction")]
    [SerializeField] private float frictionCoefficient = 1f;
    [SerializeField] private float lateralStiffnes = 80f;
    [SerializeField] private float rollingResistance;

    private float speedAlongForward = 0f;
    private float Fx = 0f;
    private float Fy = 0f;

    private void Awake()
    {
        playerInput.Enable();
        rigidbody = GetComponent<Rigidbody>();
        var map = playerInput.FindActionMap("Kart");
        moveAction = map.FindAction("Move");

        if (import) Initialize();

        frontLeftInitialRot = frontLeftWheelTransform.localRotation;
        frontRightInitialRot = frontRightWheelTransform.localRotation;

        ComputeStaticWheelLoad();
    }

    private void Initialize()
    {
        if (kartConfig != null)
        {
            rigidbody.mass = kartConfig.mass;
            frictionCoefficient = kartConfig.frictionCoefficient;
            rollingResistance = kartConfig.rollingResistance;
            maxSteeringAngle = kartConfig.maxSteerAngle;
            gearRatio = kartConfig.gearRatio;
            wheelRadius = kartConfig.wheelRadius;
            lateralStiffnes = kartConfig.lateralStiffness;
        }
    }

    private void OnDisable() => playerInput.Disable();

    private void Update()
    {
        ReadInput();
        RotateFrontWheels();
    }

    private void ReadInput()
    {
        Vector2 move = moveAction.ReadValue<Vector2>();
        steepInput = Mathf.Clamp(move.x, -1, 1);
        throttleInput = Mathf.Clamp(move.y, -1, 1);
        handbrakePressed = Input.GetKey(handbrakeKey);
    }

    private void RotateFrontWheels()
    {
        float steerAngle = maxSteeringAngle * steepInput;
        Quaternion steerRot = Quaternion.Euler(0, steerAngle, 0);
        frontLeftWheelTransform.localRotation = frontLeftInitialRot * steerRot;
        frontRightWheelTransform.localRotation = frontRightInitialRot * steerRot;
    }

    private void ComputeStaticWheelLoad()
    {
        float mass = rigidbody.mass;
        float totalWeight = mass * Mathf.Abs(g.y);
        float frontWeight = totalWeight * frontAxisShare;
        float rearWeight = totalWeight - frontWeight;

        frontRightNormalForce = frontWeight * 0.5f;
        frontLeftNormalForce = frontRightNormalForce;
        rearRightNormalForce = rearWeight * 0.5f;
        rearLeftNormalForce = rearRightNormalForce;
    }

    private void ApplyEngineForces()
    {
        Vector3 forward = transform.forward;
        float speedAlongForward = Vector3.Dot(rigidbody.linearVelocity, forward);

        if (throttleInput > 0 && speedAlongForward >= maxSpeed) return;

        float driveTorque = engineTorque * throttleInput;
        float driveForcePerWheel = (driveTorque / wheelRadius) / 2;

        Vector3 forceRear = forward * driveForcePerWheel;
        rigidbody.AddForceAtPosition(forceRear, rearLeftWheel.position, ForceMode.Force);
        rigidbody.AddForceAtPosition(forceRear, rearRightWheel.position, ForceMode.Force);
    }

    private void FixedUpdate()
    {
        ApplyEngineForces();
        ApplyWheelForce(frontLeftWheel, frontLeftNormalForce, true, false);
        ApplyWheelForce(frontRightWheel, frontRightNormalForce, true, false);
        ApplyWheelForce(rearLeftWheel, rearLeftNormalForce, false, true);
        ApplyWheelForce(rearRightWheel, rearRightNormalForce, false, true);
    }

    void ApplyWheelForce(Transform wheel, float normalForce, bool isSteer, bool isDrive)
    {
        Vector3 wheelPos = wheel.position;
        Vector3 wheelForward = wheel.forward;
        Vector3 wheelRight = wheel.right;

        Vector3 velocity = rigidbody.GetPointVelocity(wheelPos);
        float vlong = Vector3.Dot(velocity, wheelForward);
        float vlat = Vector3.Dot(velocity, wheelRight);

        Fx = 0f;
        Fy = 0f;

        if (isDrive)
        {
            speedAlongForward = Vector3.Dot(rigidbody.linearVelocity, transform.forward);
            float engineTorqueOut = engine.Simulate(throttleInput, speedAlongForward, Time.fixedDeltaTime);
            float totalWheelTorque = engineTorqueOut * gearRatio * drivetrainEfficiency;
            float wheelTorque = totalWheelTorque * 0.5f;
            Fx = wheelTorque / wheelRadius;

            if (handbrakePressed)
            {
                float brakeDir = vlong > 0 ? -1f : (vlong < 0 ? 1f : -1f);
                Fx = brakeDir * handbrakeBrakeForce;
            }
        }
        else if (isSteer)
        {
            float rooling = -rollingResistance * vlong;
            Fx = rooling;
        }

        float fyRaw = -lateralStiffnes * vlat;
        Fy = fyRaw;

        float frictionlimit = frictionCoefficient * normalForce;
        float forceLenght = Mathf.Sqrt(Fx * Fx + Fy * Fy);

        if (forceLenght > frictionlimit)
        {
            float scale = frictionlimit / forceLenght;
            Fy *= scale;
            Fx *= scale;
        }

        Vector3 force = wheelForward * Fx + wheelRight * Fy;
        rigidbody.AddForceAtPosition(force, wheel.position, ForceMode.Force);
    }

    void OnGUI()
    {
        // Создаем стиль для заголовков
        GUIStyle headerStyle = new GUIStyle();
        headerStyle.fontSize = 22;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.normal.textColor = Color.cyan;

        // Создаем стиль для обычного текста
        GUIStyle labelStyle = new GUIStyle();
        labelStyle.fontSize = 18;
        labelStyle.normal.textColor = Color.white;

        // Фон для панели телеметрии
        GUI.Box(new Rect(10, 10, 300, 350), "");
        GUILayout.BeginArea(new Rect(20, 20, 280, 330));

        // Секция скорости
        GUILayout.Label("KART TELEMETRY", headerStyle);
        GUILayout.Space(10);
        
        float speedKmh = speedAlongForward * 3.6f;
        GUILayout.Label($"Speed: {speedKmh:F1} km/h", labelStyle);

        // Секция двигателя
        GUILayout.Space(10);
        GUILayout.Label("ENGINE", headerStyle);
        
        // Индикация красной зоны RPM
        if (engine.CurrentRpm > 7000) labelStyle.normal.textColor = Color.red;
        GUILayout.Label($"RPM: {engine.CurrentRpm:F0}", labelStyle);
        labelStyle.normal.textColor = Color.white;
        
        GUILayout.Label($"Torque: {engine.CurrentTorque:F1} Nm", labelStyle);

        // Состояние ручника
        if (handbrakePressed)
        {
            GUIStyle warningStyle = new GUIStyle(labelStyle);
            warningStyle.normal.textColor = Color.yellow;
            warningStyle.fontStyle = FontStyle.Bold;
            GUILayout.Label("!!! HANDBRAKE ON !!!", warningStyle);
        }

        // Силы на колесах (последние рассчитанные)
        GUILayout.Space(10);
        GUILayout.Label("WHEEL FORCES", headerStyle);
        GUILayout.Label($"Longitudinal (Fx): {Fx:F0} N", labelStyle);
        GUILayout.Label($"Lateral (Fy): {Fy:F0} N", labelStyle);

        GUILayout.EndArea();
    }
}
