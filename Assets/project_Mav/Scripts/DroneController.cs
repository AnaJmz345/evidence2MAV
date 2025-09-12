using UnityEngine;

public class DroneController : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 6f;
    public float altitude = 15f;

    [Header("Escaneo")]
    public float scanRadius = 10f;
    public float scanInterval = 5f;

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

    public void AssignMaster(DroneMaster m) => master = m;

    public void Activate()
    {
        isActive = true;
        PickNewRandomTarget();
        transform.position = new Vector3(transform.position.x, altitude, transform.position.z);
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
        Vector3 moveDir = (new Vector3(targetXZ.x, altitude, targetXZ.z) - transform.position).normalized;
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
            StartCoroutine(LandSequence());
        }
        else if (msg.Performative == "Inform" && msg.Content == "StopSearch")
        {
            Debug.Log($"🟡 {name} recibió orden de detener búsqueda y aterrizar donde está.");
            missionComplete = true;
            landing = true;
            StartCoroutine(LandSequence());
        }
    }

    private System.Collections.IEnumerator LandSequence()
    {
        float descendSpeed = 3f;
        Vector3 groundTarget = new Vector3(transform.position.x, 0.5f, transform.position.z);

        while (Vector3.Distance(transform.position, groundTarget) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, groundTarget, descendSpeed * Time.deltaTime);
            yield return null;
        }

        Debug.Log($"🛬 {name} aterrizó con éxito.");
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
