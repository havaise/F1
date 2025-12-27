using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class ControllerModule : MonoBehaviour
{
    [Header("Import parametrs")]
    [SerializeField] private bool _import = false;
    [SerializeField] private KartConfig _kartConfig;

    [Header("Wheel attachment points")]
    [SerializeField] private Transform _frontLeftWheel;
    [SerializeField] private Transform _frontRightWheel;
    [SerializeField] private Transform _rearLeftWheel;
    [SerializeField] private Transform _rearRightWheel;

    [Header("Input (New Input System)")]
    [SerializeField] private InputActionAsset _playerInput;

    [Header("Weight distribution")]
    [SerializeField, Range(0, 1)] private float _frontAxisShare = 0.5f;

    [Header("Engine & drivetrain")]
    [SerializeField] private EngineModule _engine;
    [SerializeField] private float _gearRatio = 8f;
    [SerializeField] private float _drivetrainEfficiency = 0.9f;

    [Header("Handbrake")]
    [SerializeField] private KeyCode handbrakeKey = KeyCode.Space;
    [SerializeField] private float handbrakeBrakeForce = 2500f;

    private InputAction _moveAction;

    private float _throttleInput;
    private float _steepInput;
    private bool _handbrakePressed;

    private float _frontLeftNormalForce;
    private float _frontRightNormalForce;
    private float _rearLeftNormalForce;
    private float _rearRightNormalForce;

    private Rigidbody _rigidbody;
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
        _playerInput.Enable();
        _rigidbody = GetComponent<Rigidbody>();

        var map = _playerInput.FindActionMap("Kart");
        _moveAction = map.FindAction("Move");

        if (_import)
            Initialize();

        frontLeftInitialRot = _frontLeftWheel.localRotation;
        frontRightInitialRot = _frontRightWheel.localRotation;

        ComputeStaticWheelLoad();
    }

    private void Initialize()
    {
        if (_kartConfig != null)
        {
            _rigidbody.mass = _kartConfig.mass;
            frictionCoefficient = _kartConfig.frictionCoefficient;
            rollingResistance = _kartConfig.rollingResistance;
            maxSteeringAngle = _kartConfig.maxSteerAngle;
            _gearRatio = _kartConfig.gearRatio;
            wheelRadius = _kartConfig.wheelRadius;
            lateralStiffnes = _kartConfig.lateralStiffness;
        }
    }

    private void OnDisable()
    {
        _playerInput.Disable();
    }

    private void Update()
    {
        PollInput();
        ApplyVisualSteer();
    }

    private void PollInput()
    {
        Vector2 move = _moveAction.ReadValue<Vector2>();

        _steepInput = Mathf.Clamp(move.x, -1, 1);
        _throttleInput = Mathf.Clamp(move.y, -1, 1);

        // Ручник
        _handbrakePressed = Input.GetKey(handbrakeKey);
    }

    private void ApplyVisualSteer()
    {
        float steerAngle = maxSteeringAngle * _steepInput;
        Quaternion steerRot = Quaternion.Euler(0, steerAngle, 0);

        _frontLeftWheel.localRotation = frontLeftInitialRot * steerRot;
        _frontRightWheel.localRotation = frontRightInitialRot * steerRot;
    }

    private void ComputeStaticWheelLoad()
    {
        float mass = _rigidbody.mass;
        float totalWeight = mass * Mathf.Abs(g.y);

        float frontWeight = totalWeight * _frontAxisShare;
        float rearWeight = totalWeight - frontWeight;

        _frontRightNormalForce = frontWeight * 0.5f;
        _frontLeftNormalForce = _frontRightNormalForce;

        _rearRightNormalForce = rearWeight * 0.5f;
        _rearLeftNormalForce = _rearRightNormalForce;
    }

    private void FixedUpdate()
    {
        ApplyEngineForces();

        ApplyWheelForce(_frontLeftWheel, _frontLeftNormalForce, isSteer: true, isDrive: false);
        ApplyWheelForce(_frontRightWheel, _frontRightNormalForce, isSteer: true, isDrive: false);
        ApplyWheelForce(_rearLeftWheel, _rearLeftNormalForce, isSteer: false, isDrive: true);
        ApplyWheelForce(_rearRightWheel, _rearRightNormalForce, isSteer: false, isDrive: true);
    }

    private void ApplyEngineForces()
    {
        Vector3 fwd = transform.forward;

        float along = Vector3.Dot(_rigidbody.linearVelocity, fwd);
        if (_throttleInput > 0 && along > maxSpeed)
            return;

        float driveTorque = engineTorque * _throttleInput;
        float driveForcePerWheel = driveTorque / wheelRadius / 2f;

        Vector3 rearForce = fwd * driveForcePerWheel;
        _rigidbody.AddForceAtPosition(rearForce, _rearLeftWheel.position, ForceMode.Force);
        _rigidbody.AddForceAtPosition(rearForce, _rearRightWheel.position, ForceMode.Force);
    }

    private void ApplyWheelForce(Transform wheel, float normalForce, bool isSteer, bool isDrive)
    {
        Vector3 wheelPos = wheel.position;
        Vector3 wheelFwd = wheel.forward;
        Vector3 wheelRight = wheel.right;

        Vector3 pointVel = _rigidbody.GetPointVelocity(wheelPos);

        float vlong = Vector3.Dot(pointVel, wheelFwd);
        float vlat = Vector3.Dot(pointVel, wheelRight);

        Fx = 0f;
        Fy = 0f;

        if (isDrive)
        {
            speedAlongForward = Vector3.Dot(_rigidbody.linearVelocity, transform.forward);

            float engineTorqueOut = _engine.Simulate(_throttleInput, speedAlongForward, Time.fixedDeltaTime);
            float totalWheelTorque = engineTorqueOut * _gearRatio * _drivetrainEfficiency;

            float wheelTorque = totalWheelTorque * 0.5f;
            Fx += wheelTorque / wheelRadius;

            if (_handbrakePressed)
            {
                float brakeDir = vlong > 0 ? -1f : (vlong < 0 ? 1f : -1f);
                Fx += brakeDir * handbrakeBrakeForce;
            }
        }
        else if (isSteer)
        {
            float rooling = -rollingResistance * vlong;
            Fx += rooling;
        }

        float fyRaw = -lateralStiffnes * vlat;
        Fy += fyRaw;

        float frictionlimit = frictionCoefficient * normalForce;
        float forceLenght = Mathf.Sqrt(Fx * Fx + Fy * Fy);

        if (forceLenght > frictionlimit)
        {
            float scale = frictionlimit / forceLenght;

            // В исходнике было Fy += scale; Fx += scale; — оставлено как есть.
            Fy += scale;
            Fx += scale;
        }

        Vector3 force = wheelFwd * Fx + wheelRight * Fy;
        _rigidbody.AddForceAtPosition(force, wheel.position, ForceMode.Force);
    }

    // Телеметрия: та же информация, но по-другому сверстана.
    private void OnGUI()
    {
        var panel = new Rect(12f, 12f, 520f, 260f);

        // фон-плашка
        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.Box(panel, GUIContent.none);
        GUI.color = oldColor;

        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        var rowStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            normal = { textColor = Color.white }
        };

        float x = panel.x + 14f;
        float y = panel.y + 12f;
        float w = panel.width - 28f;

        GUI.Label(new Rect(x, y, w, 24f), "KART TELEMETRY", titleStyle);
        y += 34f;

        string speedLine = $"Speed   {speedAlongForward:0.0} m/s   ({(speedAlongForward * 3.6f):0.0} km/h)";
        string rpmLine = $"RPM     {_engine.CurrentRpm:0}";
        string tqLine = $"Torque  {_engine.CurrentTorque:0.0} N·m";
        string fxLine = $"Fx      {Fx:0.0} N";
        string fyLine = $"Fy      {Fy:0.0} N";

        GUI.Label(new Rect(x, y, w, 20f), speedLine, rowStyle); y += 22f;
        GUI.Label(new Rect(x, y, w, 20f), rpmLine, rowStyle); y += 22f;
        GUI.Label(new Rect(x, y, w, 20f), tqLine, rowStyle); y += 28f;

        GUI.Label(new Rect(x, y, w, 20f), "Wheel forces", rowStyle); y += 22f;
        GUI.Label(new Rect(x, y, w, 20f), fxLine, rowStyle); y += 22f;
        GUI.Label(new Rect(x, y, w, 20f), fyLine, rowStyle); y += 22f;

        if (_handbrakePressed)
        {
            var warnStyle = new GUIStyle(rowStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.35f, 0.35f, 1f) }
            };
            GUI.Label(new Rect(x, panel.yMax - 30f, w, 22f), "HANDBRAKE ON", warnStyle);
        }
    }
}
