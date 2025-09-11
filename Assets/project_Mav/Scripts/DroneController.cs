using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public class DroneController : MonoBehaviour
{
    [Header("Refs")]
    public Transform droneVisual;
    public NavMeshSurface surface;

    [Header("Vuelo")]
    public float cruiseAltitude = 120f;
    public float speed = 8f;
    public float turnSpeed = 5f;

    [Header("Aterrizaje (detección)")]
    public float landingCheckRadius = 1.2f;
    public float landingClearHeight = 2.0f;
    public LayerMask landingBlockMask;
    public float safeSearchRadius = 10f;
    public int safeSamples = 40;

    [Header("Estrategia de desvío")]
    public float avoidanceDistance = 2f;
    public int maxAvoidanceAttempts = 10;

    [Header("Debug")]
    public bool drawPath = true;
    public bool logLandingDebug = true;

    private readonly List<Vector3> _pathPoints = new();
    private int _pathIndex = 0;
    private bool _enRoute = false;
    private bool _landing = false;
    private Vector3 _missionXZ;
    private int _avoidanceAttempts = 0;

    private bool _isActive = false;

    // 🔗 Referencia al DroneMaster
    private DroneMaster master;

    public void Activate() => _isActive = true;
    public void Deactivate() => _isActive = false;

    void Awake()
    {
        if (surface != null) surface.BuildNavMesh();
        if (droneVisual == null) droneVisual = transform;

        int maskObstacles = LayerMask.GetMask("Obstacles");
        int maskWater     = LayerMask.GetMask("Water");
        int maskPersona   = 1 << LayerMask.NameToLayer("Persona");

        landingBlockMask = maskObstacles | maskWater | maskPersona;

        Debug.Log($"[Drone] landingBlockMask={landingBlockMask} (debe incluir Obstacles + Water + Persona)");
    }

    void Start()
    {
        master = FindObjectOfType<DroneMaster>();
    }

    public void GoToXZ(float x, float z)
    {
        _missionXZ = new Vector3(x, 0f, z);
        _avoidanceAttempts = 0;

        Debug.Log($"🛰 Intentando ir a (X={x}, Z={z})");
        Debug.DrawRay(new Vector3(x, 50f, z), Vector3.down * 100f, Color.magenta, 5f);

        float localRadius = 5f;

        if (NavMesh.SamplePosition(_missionXZ, out var hit, localRadius, NavMesh.AllAreas))
        {
            float distToTarget = Vector3.Distance(_missionXZ, hit.position);
            Debug.Log($"✅ Punto proyectado en NavMesh: {hit.position} (distancia desde objetivo: {distToTarget:F2})");

            var startXZ = new Vector3(transform.position.x, 0f, transform.position.z);
            if (!NavMesh.SamplePosition(startXZ, out var startHit, localRadius, NavMesh.AllAreas))
            {
                Debug.LogWarning("❌ No se pudo proyectar el punto de inicio en la NavMesh.");
                return;
            }

            var path = new NavMeshPath();
            if (NavMesh.CalculatePath(startHit.position, hit.position, NavMesh.AllAreas, path) &&
                path.corners != null && path.corners.Length > 0)
            {
                Debug.Log($"🛫 Ruta válida con {path.corners.Length} puntos.");
                _pathPoints.Clear();
                foreach (var p in path.corners)
                    _pathPoints.Add(new Vector3(p.x, cruiseAltitude, p.z));

                _pathIndex = 0;
                _enRoute = true;
                _landing = false;
            }
            else
            {
                Debug.LogWarning("⚠️ No se pudo calcular un camino válido en la NavMesh.");
            }
        }
        else
        {
            Debug.LogWarning($"❌ Destino ({x}, {z}) fuera de la NavMesh o no alcanzable (radio={localRadius}).");
        }
    }

    void Update()
    {
        if (!_isActive) return;

        if (_enRoute && _pathIndex < _pathPoints.Count)
        {
            FlyAlongPath();
            return;
        }

        if (_enRoute && _pathIndex >= _pathPoints.Count && !_landing)
        {
            TryLandNearMissionXZ();
        }
    }

    void FlyAlongPath()
    {
        Vector3 target = _pathPoints[_pathIndex];
        Vector3 dir = (target - transform.position);

        if (dir.sqrMagnitude < 1e-6f) { _pathIndex++; return; }

        float dist = dir.magnitude;
        if (dist < 0.3f) { _pathIndex++; return; }

        Vector3 step = dir.normalized * speed * Time.deltaTime;
        transform.position += step;

        if (step.sqrMagnitude > 1e-6f)
        {
            var flat = new Vector3(step.x, 0, step.z);
            if (flat.sqrMagnitude > 1e-6f)
            {
                Quaternion look = Quaternion.LookRotation(flat, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
            }
        }
    }

    void TryLandNearMissionXZ()
    {
        _landing = true;
        _avoidanceAttempts = 0;
        StartCoroutine(LandCoroutine());
    }

    System.Collections.IEnumerator LandCoroutine()
    {
        float hoverHeight = 0.05f;
        float descendSpeed = speed * 0.6f;
        float groundCheckDistance = 120f;
        float repelForce = 0.2f;

        bool avoiding = false;
        Collider droneCollider = GetComponentInChildren<Collider>();
        Vector3 descendTarget = Vector3.zero;
        bool hasValidGround = false;

        while (true)
        {
            Collider[] overlapping = Physics.OverlapBox(
                droneCollider.bounds.center,
                droneCollider.bounds.extents,
                transform.rotation,
                landingBlockMask
            );

            if (overlapping.Length > 0)
            {
                if (!avoiding) Debug.LogWarning("⚠️ ¡Colisión durante el descenso! Evadiendo...");
                avoiding = true;

                foreach (var obstacle in overlapping)
                {
                    if (obstacle.gameObject == gameObject) continue;

                    Vector3 repelDir = (transform.position - obstacle.ClosestPoint(transform.position)).normalized;
                    transform.position += repelDir * repelForce;
                }

                hasValidGround = false;
                yield return null;
                continue;
            }
            else
            {
                avoiding = false;
            }

            RaycastHit hitInfo;
            if (Physics.Raycast(transform.position, Vector3.down, out hitInfo, groundCheckDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                Vector3 groundHitPoint = hitInfo.point;

                if (NavMesh.SamplePosition(groundHitPoint, out var navHit, 1.0f, NavMesh.AllAreas))
                {
                    descendTarget = new Vector3(transform.position.x, navHit.position.y + hoverHeight, transform.position.z);
                    hasValidGround = true;
                }
                else
                {
                    transform.position += transform.forward * speed * Time.deltaTime;
                    hasValidGround = false;
                    yield return null;
                    continue;
                }
            }
            else
            {
                transform.position += transform.forward * speed * Time.deltaTime;
                hasValidGround = false;
                yield return null;
                continue;
            }

            if (hasValidGround)
            {
                float distToTarget = Vector3.Distance(transform.position, descendTarget);
                if (distToTarget <= 0.05f)
                {
                    Debug.Log("🟢 Aterrizaje exitoso.");
                    _enRoute = false;
                    _landing = false;

                    // 🔍 Buscar personas cercanas tras el aterrizaje
                    Collider[] hits = Physics.OverlapSphere(transform.position, 10f);
                    foreach (var hit in hits)
                    {
                        if (hit.CompareTag("Person"))
                        {
                            PersonController pc = hit.GetComponent<PersonController>();
                            if (pc != null && master != null)
                            {
                                master.ReportPersonFound(pc);
                            }
                        }
                    }

                    yield break;
                }

                transform.position = Vector3.MoveTowards(transform.position, descendTarget, descendSpeed * Time.deltaTime);
            }

            yield return null;
        }
    }

    void OnDrawGizmos()
    {
        if (!drawPath || _pathPoints == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < _pathPoints.Count - 1; i++)
        {
            Gizmos.DrawLine(_pathPoints[i], _pathPoints[i + 1]);
            Gizmos.DrawSphere(_pathPoints[i], 0.15f);
        }
        if (_pathPoints.Count > 0)
            Gizmos.DrawSphere(_pathPoints[^1], 0.2f);
    }
}
