using System.Collections;
using UnityEngine;
using Controller; // para CharacterMover

[RequireComponent(typeof(CharacterMover))]
[DisallowMultipleComponent]
public class PersonCirclePatrol : MonoBehaviour
{
    [Header("Patrulla")]
    public float radius = 20f;                  // radio del círculo
    public bool clockwise = true;               // sentido
    [Tooltip("Grados avanzados por segundo (curva suave)")]
    public float degreesPerSecond = 18f;        // ~20°/s ≈ vuelta en 20s
    [Tooltip("Pausas entre tramos (Idle)")]
    public Vector2 pauseSeconds = new Vector2(0.8f, 2f);
    [Tooltip("Tramo continuo de marcha antes de pausar")]
    public Vector2 burstSeconds = new Vector2(2f, 4f);
    [Tooltip("Probabilidad de trotar/correr en un tramo")]
    public float runChance = 0.15f;

    [Header("Altura/terreno")]
    public bool snapToGround = true;
    public LayerMask groundMask = ~0;
    public float groundCheckDown = 50f;

    [Header("Centro (opcional)")]
    public bool autoCenterAtStart = true;       // Modo A: usa posición inicial
    public Vector3 explicitCenter;              // Modo B: setear desde DroneMaster

    private CharacterMover mover;
    private Transform tr;
    private Vector3 centerXZ;
    private float centerY;
    private float angleDeg;
    private bool running;

    void Awake()
    {
        mover = GetComponent<CharacterMover>();
        tr = transform;
    }

    void Start()
    {
        if (autoCenterAtStart)
            SetCenter(tr.position);
        else if (explicitCenter != Vector3.zero)
            SetCenter(explicitCenter);
        else
            SetCenter(tr.position); // fallback

        StartCoroutine(PatrolLoop());
    }

    public void SetCenter(Vector3 center)
    {
        centerXZ = new Vector3(center.x, 0f, center.z);

        if (snapToGround && Physics.Raycast(center + Vector3.up * 10f, Vector3.down, out var hit, groundCheckDown, groundMask))
            centerY = hit.point.y;
        else
            centerY = center.y;

        Vector3 flat = new Vector3(tr.position.x - centerXZ.x, 0f, tr.position.z - centerXZ.z);
        if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward * radius;

        angleDeg = Mathf.Atan2(flat.z, flat.x) * Mathf.Rad2Deg;
    }

    IEnumerator PatrolLoop()
    {
        while (true)
        {
            // tramo de marcha
            running = Random.value < runChance;
            float burst = Random.Range(burstSeconds.x, burstSeconds.y);
            float t = 0f;

            while (t < burst)
            {
                float dir = clockwise ? -1f : 1f;
                angleDeg += dir * degreesPerSecond * Time.deltaTime;

                float rad = angleDeg * Mathf.Deg2Rad;
                Vector3 target = new Vector3(
                    centerXZ.x + Mathf.Cos(rad) * radius,
                    centerY,
                    centerXZ.z + Mathf.Sin(rad) * radius
                );

                if (snapToGround && Physics.Raycast(target + Vector3.up * 10f, Vector3.down, out var hit, groundCheckDown, groundMask))
                    target.y = hit.point.y;
                else
                    target.y = centerY;

                // Avanzar al waypoint sobre el círculo. CharacterMover rota y anima.
                mover.SetInput(new Vector2(0f, 1f), target, running, false);

                t += Time.deltaTime;
                yield return null;
            }

            // pausa (Idle)
            mover.SetInput(Vector2.zero, tr.position + tr.forward, false, false);
            yield return new WaitForSeconds(Random.Range(pauseSeconds.x, pauseSeconds.y));
        }
    }

    // Llamable desde fuera si quieres detener la patrulla
    public void StopPatrol()
    {
        StopAllCoroutines();
        mover.SetInput(Vector2.zero, tr.position + tr.forward, false, false);
    }
}