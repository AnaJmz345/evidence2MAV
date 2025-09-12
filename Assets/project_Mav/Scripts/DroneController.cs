using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public class DroneController : MonoBehaviour
{

     [Header("Refs")]
    public Transform droneVisual;
    public NavMeshSurface surface;

    [Header("Movimiento")]
    public float speed = 6f;
    public float cruiseAltitude = 15f;
    public float turnSpeed = 5f;
    private float _navMeshRadius =120f;

    [Header("Aterrizaje (detección)")]
    public float landingCheckRadius = 1.2f;
    public float landingClearHeight = 2.0f;
    public LayerMask landingBlockMask;
    public float safeSearchRadius = 10f;
    public int safeSamples = 40;

    [Header("Estrategia de desvío")]
    public float avoidanceDistance = 2f;
    public int maxAvoidanceAttempts = 10;

    [Header("Escaneo")]
    public float scanRadius = 10f;
    public float scanInterval = 5f;

    [Header("Debug")]
    public bool drawPath = true;
    public bool logLandingDebug = true;


    [Header("Zona de búsqueda")]
    public float searchRadius = 40f;         // radio fijo de búsqueda
    public Vector3 searchCenter;             // centro fijo alrededor del cual buscar

    private Vector3 targetXZ;
    private bool isActive = false;
    private bool landing = false;
    private bool missionComplete = false;

    private float scanTimer = 0f;
    private DroneMaster master;

    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    private float stuckCheckInterval = 1f;
    private float stuckThreshold = 0.2f;

    /*POSIBLEMENTE BORRAR*/
     private readonly List<Vector3> _pathPoints = new();
    private int _pathIndex = 0;
    private bool _enRoute = false;
    private bool _landing = false;
    private Vector3 _missionXZ;
    private int _avoidanceAttempts = 0;

    public void AssignMaster(DroneMaster m) => master = m;

    void Awake()
    {
        if (surface != null) surface.BuildNavMesh();
        if (droneVisual == null) droneVisual = transform;

            // Obtener las máscaras individualmente
        int maskObstacles = LayerMask.GetMask("Obstacles");
        int maskWater     = LayerMask.GetMask("Water");
        int maskPersona   = 1 << LayerMask.NameToLayer("Persona");  // ¡forzado!

        landingBlockMask = maskObstacles | maskWater | maskPersona;

        Debug.Log($"[Drone] landingBlockMask={landingBlockMask} (debe incluir Obstacles + Water + Persona)");

        Debug.Log($"[Drone] landingBlockMask={landingBlockMask} (debe ser >0).");
    }

    public void Activate()
    {
        isActive = true;
        PickNewRandomTarget();
        transform.position = new Vector3(transform.position.x, cruiseAltitude, transform.position.z);
        lastPosition = transform.position;
    }

    public void Deactivate()
    {
        isActive = false;
    }

    void Update()
    {
        if (!isActive || landing || missionComplete) return;

        scanTimer += Time.deltaTime;

        if (Vector3.Distance(transform.position, targetXZ) < 1f || targetXZ == Vector3.zero)
        {
            PickNewRandomTarget();
        }

        MoveToTarget();
        CheckIfStuck();

        if (scanTimer >= scanInterval)
        {
            scanTimer = 0f;
            ScanForTarget();
        }
    }

    private void MoveToTarget()
    {
        Vector3 moveDir = (new Vector3(targetXZ.x, cruiseAltitude, targetXZ.z) - transform.position).normalized;
        transform.position += moveDir * speed * Time.deltaTime;
    }

    private void PickNewRandomTarget()
    {
        Vector2 offset = Random.insideUnitCircle * searchRadius;
        float x = searchCenter.x + offset.x;
        float z = searchCenter.z + offset.y;
        targetXZ = new Vector3(x, 0, z);
    }

    private void ScanForTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, scanRadius, LayerMask.GetMask("Objetivo"));
        foreach (var hit in hits)
        {
            PersonController pc = hit.GetComponent<PersonController>();
            if (pc != null && master != null)
            {
                Debug.Log($"🕵️ {name} detectó a alguien con ({pc.shirtColor}, hat={pc.hasHat})");

                ACLMessage inform = new ACLMessage(this, master, "Inform", "Found");
                master.ReceiveACL(inform);

                ACLMessage request = new ACLMessage(this, master, "Request", "Land?");
                master.ReceiveACL(request);

                break;
            }
        }
    }

    public void ReceiveACL(ACLMessage msg)
    {
        if (msg.Performative == "Permit" && msg.Content == "Land")
        {
            Debug.Log($"✅ {name} recibió permiso para aterrizar.");
            landing = true;
            TryLandNearMissionXZ();
        }
        else if (msg.Performative == "Inform" && msg.Content == "StopSearch")
        {
            Debug.Log($"🟡 {name} recibió orden de detener búsqueda y aterrizar donde está.");
            missionComplete = true;
            landing = true;
            TryLandNearMissionXZ();
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
    Debug.Log("✈ Iniciando rutina de aterrizaje adaptativa...");
    Debug.Log($"Layer index for 'Persona': {LayerMask.NameToLayer("Persona")}");
    Debug.Log($"Mask value for 'Persona': {1 << LayerMask.NameToLayer("Persona")}");
    Debug.Log($"landingBlockMask final: {landingBlockMask.value}");

    float minStep = 0.01f;
    float descendSpeed = speed * 0.6f;
    float hoverHeight = 0.05f; //antes era 0.2
    float collisionCheckRadius = 0.6f;
    float groundCheckDistance = 120f;
    float repelForce = 0.2f; // Reducido para suavizar evasión

    bool avoiding = false;
    Collider droneCollider = GetComponentInChildren<Collider>();
    Vector3 descendTarget = Vector3.zero;
    bool hasValidGround = false;

    while (true)
    {
        // 1️⃣ Checar colisiones con obstáculos mientras baja
        Collider[] overlapping = Physics.OverlapBox(
            droneCollider.bounds.center,
            droneCollider.bounds.extents,
            transform.rotation,
            landingBlockMask
        );

        if (overlapping.Length > 0)
        {
            if (!avoiding)
                Debug.LogWarning("⚠ ¡Colisión durante el descenso! Evadiendo...");

            avoiding = true;

            foreach (var obstacle in overlapping)
            {
                if (obstacle.gameObject == gameObject) continue;

                Vector3 repelDir = (transform.position - obstacle.ClosestPoint(transform.position)).normalized;
                transform.position += repelDir * repelForce; // Suavizado con Time.deltaTime
                Debug.Log($"🧱 Evadiendo obstáculo: {obstacle.name}");
            }

            // Después de evadir, forzar recálculo del punto de aterrizaje
            hasValidGround = false;
            yield return null;
            continue;
        }
        else
        {
            avoiding = false;
        }

        // 2️⃣ Verificar si hay suelo válido justo debajo
        RaycastHit hitInfo;
        if (Physics.Raycast(transform.position, Vector3.down, out hitInfo, groundCheckDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 groundHitPoint = hitInfo.point;
          
            if (NavMesh.SamplePosition(groundHitPoint, out var navHit, 1.0f, NavMesh.AllAreas))
            {
                // Actualizar descendTarget siempre, no solo la primera vez
                descendTarget = new Vector3(transform.position.x, navHit.position.y + hoverHeight, transform.position.z);
                hasValidGround = true;
            }
            else
            {
                Debug.Log("❌ Suelo debajo no es parte de NavMesh. Avanzando horizontalmente para buscar...");
                transform.position += transform.forward * speed * Time.deltaTime;
                hasValidGround = false;
                yield return null;
                continue;
            }
        }
        else
        {
            Debug.Log("❌ No hay suelo detectado. Avanzando horizontalmente...");
            transform.position += transform.forward * speed * Time.deltaTime;
            hasValidGround = false;
            yield return null;
            continue;
        }

        // 3️⃣ Si hay suelo válido, proceder con el descenso
        if (hasValidGround)
        {
            float distToTarget = Vector3.Distance(transform.position, descendTarget);
            if (distToTarget <= 0.05f)
            {
                Debug.Log("🟢 Aterrizaje exitoso.");
                _enRoute = false;
                _landing = false;
                yield break;
            }

            transform.position = Vector3.MoveTowards(transform.position, descendTarget, descendSpeed * Time.deltaTime);
        }

        yield return null;
    }
}

    private void CheckIfStuck()
    {
        stuckTimer += Time.deltaTime;

        if (stuckTimer >= stuckCheckInterval)
        {
            float distanceMoved = Vector3.Distance(transform.position, lastPosition);
            if (distanceMoved < stuckThreshold)
            {
                Debug.LogWarning($"🧱 {name} detectado como atascado. Eligiendo nueva dirección.");
                PickNewRandomTarget();
            }

            lastPosition = transform.position;
            stuckTimer = 0f;
        }
    }

}
