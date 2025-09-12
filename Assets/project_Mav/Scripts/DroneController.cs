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

    [Header("Escaneo")]
    public float scanRadius = 25f;   // radio horizontal
    public float scanTime = 10f;     // tiempo máximo de escaneo
    private bool scanning = false;
    private float scanTimer = 0f;

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
    }

    void Start()
    {
        master = FindObjectOfType<DroneMaster>();
    }

    public void GoToXZ(float x, float z)
    {
        _missionXZ = new Vector3(x, 0f, z);
        _avoidanceAttempts = 0;

        if (NavMesh.SamplePosition(_missionXZ, out var hit, 5f, NavMesh.AllAreas))
        {
            var startXZ = new Vector3(transform.position.x, 0f, transform.position.z);
            if (!NavMesh.SamplePosition(startXZ, out var startHit, 5f, NavMesh.AllAreas))
            {
                Debug.LogWarning("❌ No se pudo proyectar el punto de inicio en la NavMesh.");
                return;
            }

            var path = new NavMeshPath();
            if (NavMesh.CalculatePath(startHit.position, hit.position, NavMesh.AllAreas, path) &&
                path.corners != null && path.corners.Length > 0)
            {
                _pathPoints.Clear();
                foreach (var p in path.corners)
                    _pathPoints.Add(new Vector3(p.x, cruiseAltitude, p.z));

                _pathIndex = 0;
                _enRoute = true;
                _landing = false;
            }
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

        if (_enRoute && _pathIndex >= _pathPoints.Count && !scanning && !_landing)
        {
            scanning = true;
            scanTimer = 0f;
            Debug.Log($"🔎 {name} llegó a la zona, iniciando escaneo...");
        }

        if (scanning) ScanForPersons();
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

    void ScanForPersons()
    {
        scanTimer += Time.deltaTime;

        Vector3 top = transform.position;
        Vector3 bottom = new Vector3(transform.position.x, 0f, transform.position.z);

        Collider[] hits = Physics.OverlapCapsule(top, bottom, scanRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Person"))
            {
                PersonController pc = hit.GetComponent<PersonController>();
                if (pc != null && master != null)
                {
                    master.ReportPersonFound(pc, this);
                }
            }
        }

        if (scanTimer >= scanTime)
        {
            Debug.Log($"⌛ {name} no encontró a la target. Procediendo a aterrizar...");
            scanning = false;
            TryLandNearMissionXZ();
        }
    }

    public void LandAtTarget(Vector3 pos)
    {
        if (_landing) return;

        Debug.Log($"🛬 {name} aterrizando en posición de target {pos}");
        _landing = true;
        _enRoute = false;
        StartCoroutine(LandAtPosition(pos));
    }

    private System.Collections.IEnumerator LandAtPosition(Vector3 targetPos)
    {
        float descendSpeed = speed * 0.6f;
        Vector3 descendTarget = new Vector3(targetPos.x, 0.1f, targetPos.z);

        while (true)
        {
            float dist = Vector3.Distance(transform.position, descendTarget);
            if (dist <= 0.05f)
            {
                Debug.Log($"🟢 {name} aterrizó junto a la target.");
                _landing = false;
                yield break;
            }

            transform.position = Vector3.MoveTowards(transform.position, descendTarget, descendSpeed * Time.deltaTime);
            yield return null;
        }
    }
    
    public void LandNearTarget(Vector3 targetPos, float standOffDistance)
    {
        if (_landing) return;
        StartCoroutine(LandNearTargetCo(targetPos, standOffDistance));
    }

    private System.Collections.IEnumerator LandNearTargetCo(Vector3 targetPos, float standOffDistance)
    {
        _landing = true;
        _enRoute = false;

        // 1) Candidatos alrededor del objetivo (círculo)
        List<Vector3> candidates = new List<Vector3>();
        int samples = Mathf.Max(12, safeSamples); // usa tu safeSamples como mínimo
        for (int i = 0; i < samples; i++)
        {
            float ang = (360f / samples) * i;
            Vector3 dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
            Vector3 c = targetPos + dir * standOffDistance;
            candidates.Add(new Vector3(c.x, 0f, c.z));
        }

        // 2) Valida candidatos: NavMesh + colisiones
        Vector3? safe = null;
        foreach (var c in candidates)
        {
            Vector3 test = c;
            // Proyectar a NavMesh si tienes superficie:
            if (NavMesh.SamplePosition(test, out var hit, 3f, NavMesh.AllAreas))
            test = new Vector3(hit.position.x, 0f, hit.position.z);

            // Chequeo de bloqueos en círculo de aterrizaje
            bool blocked = Physics.CheckSphere(
                new Vector3(test.x, 0.5f, test.z),
                landingCheckRadius,
                landingBlockMask
            );
            
            if (!blocked)
            {
                safe = test;
                break;
            }
        }

        // 3) Si no hay punto seguro, aumenta radio una vez
        if (!safe.HasValue)
        {
            float extra = Mathf.Max(standOffDistance * 0.75f, 2f);
             yield return StartCoroutine(LandNearTargetCo(targetPos, standOffDistance + extra));
             yield break; 
        }

        // 4) Desciende hacia el punto seguro
        Vector3 descendTarget = new Vector3(safe.Value.x, 0.1f, safe.Value.z);
        float descendSpeed = speed * 0.6f;

        while (true)
        {
            // Seguridad: re-chequeo dinámico por si alguien entra al área
            bool nowBlocked = Physics.CheckSphere(
                new Vector3(descendTarget.x, 0.5f, descendTarget.z),
                landingCheckRadius,
                landingBlockMask
            );
            if (nowBlocked)
            {
                // vuelve a buscar otro punto cercano
                _landing = false;
                yield return StartCoroutine(LandNearTargetCo(targetPos, standOffDistance + 0.5f));
                yield break;
            }

            float dist = Vector3.Distance(transform.position, descendTarget);
            if (dist <= 0.05f)
            {
                Debug.Log($"🟢 {name} aterrizó cerca de la target.");
                _landing = false;
                yield break;
            }

            transform.position = Vector3.MoveTowards(transform.position, descendTarget, descendSpeed * Time.deltaTime);
            yield return null;
        }
    }
    //cambio radio de perimneto seguro

    void TryLandNearMissionXZ()
    {
        _landing = true;
        _avoidanceAttempts = 0;
        StartCoroutine(LandCoroutine());
    }

    System.Collections.IEnumerator LandCoroutine()
    {
        float descendSpeed = speed * 0.6f;
        Vector3 descendTarget = new Vector3(transform.position.x, 0.1f, transform.position.z);

        while (true)
        {
            float distToTarget = Vector3.Distance(transform.position, descendTarget);
            if (distToTarget <= 0.05f)
            {
                Debug.Log("🟢 Aterrizaje exitoso (fallback).");
                _enRoute = false;
                _landing = false;
                yield break;
            }

            transform.position = Vector3.MoveTowards(transform.position, descendTarget, descendSpeed * Time.deltaTime);
            yield return null;
        }
    }

    void OnDrawGizmosSelected()
    {
        // visualización del área de escaneo
        Gizmos.color = Color.yellow;
        Vector3 top = transform.position;
        Vector3 bottom = new Vector3(transform.position.x, 0f, transform.position.z);
        Gizmos.DrawWireSphere(bottom, scanRadius);
        Gizmos.DrawLine(top, bottom);
    }
}
