using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// Genera árboles UNA sola vez (en Editor). Los objetos quedan en la escena y se guardan al salvar.
/// No vuelve a generar a menos que pulses "Clear" y "Generate" de nuevo.
[ExecuteAlways]
public class ForestSpawnerOnce : MonoBehaviour
{
    [Header("Area")]
    [Tooltip("Renderer del Ground (se usa su bounds para definir el área)")]
    public Renderer groundRenderer;

    [Tooltip("Opcional: NavMeshSurface para rebakear al terminar")]
    public NavMeshSurface navSurface;

    [Header("Trees")]
    [Tooltip("Prefabs de árboles (arrástralos aquí)")]
    public GameObject[] treePrefabs;

    [Min(1)] public int totalTrees = 50;
    [Tooltip("Distancia mínima entre centros de árboles")]
    public float minSpacing = 5f;

    [Header("Reglas de colocación")]
    [Tooltip("Capas que NO se pueden usar (p.ej. Water)")]
    public LayerMask blockLayers;
    [Tooltip("Capa para los árboles instanciados (recomendado: Obstacles)")]
    public string treesLayerName = "Obstacles";

    [Tooltip("Si true, asegura que cada punto caiga en NavMesh")]
    public bool projectToNavMesh = false;
    [Tooltip("Radio máx. para SamplePosition si se usa NavMesh (pequeño = estricto)")]
    public float navSampleMaxDistance = 0.6f;

    [Header("Random")]
    public bool randomYaw = true;
    public Vector2 uniformScaleRange = new Vector2(0.95f, 1.1f);

    // Estado
    [SerializeField] private bool hasSpawned = false;

    // Donde colgamos lo generado para poder limpiarlo sin tocar otras cosas
    private const string GeneratedRootName = "__Forest_Generated";
    private Transform _generatedRoot;

    void OnEnable()
    {
        // Creamos/obtenemos el contenedor de lo generado
        var t = transform.Find(GeneratedRootName);
        if (t == null)
        {
            var go = new GameObject(GeneratedRootName);
            go.transform.SetParent(transform, false);
            _generatedRoot = go.transform;
        }
        else _generatedRoot = t;

        // En Play nunca auto-generes: esto es "una vez" para construir el mundo
        if (Application.isPlaying) return;
    }

    /// Genera solo si aún no se ha generado.
    public void GenerateOnce()
    {
        if (hasSpawned)
        {
            Debug.Log("[ForestSpawnerOnce] Ya fue generado. Limpia primero si quieres reintentar.");
            return;
        }

        if (groundRenderer == null)
        {
            Debug.LogError("[ForestSpawnerOnce] Asigna Ground Renderer.");
            return;
        }
        if (treePrefabs == null || treePrefabs.Length == 0)
        {
            Debug.LogError("[ForestSpawnerOnce] Agrega prefabs de árboles.");
            return;
        }

        Bounds area = groundRenderer.bounds; // mismo enfoque que su util de bounds :contentReference[oaicite:1]{index=1}
        int treesLayer = LayerMask.NameToLayer(treesLayerName);
        if (treesLayer < 0) treesLayer = 0; // Default si la capa no existe

        var placedPositions = new List<Vector3>();
        int placed = 0;
        int attempts = 0;
        int maxAttempts = totalTrees * 50;

        while (placed < totalTrees && attempts < maxAttempts)
        {
            attempts++;

            // 1) Punto aleatorio dentro del bounds (XZ)
            float x = Random.Range(area.min.x, area.max.x);
            float z = Random.Range(area.min.z, area.max.z);
            Vector3 rayOrigin = new Vector3(x, area.max.y + 50f, z);

            // 2) Raycast hacia abajo para encontrar el suelo real
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore))
                continue;

            // Evitar capas bloqueadas (p.ej. Water)
            if (((1 << hit.collider.gameObject.layer) & blockLayers) != 0)
                continue;

            Vector3 candidate = hit.point;

            // 3) Si se pide, asegurar que el punto esté en NavMesh
            if (projectToNavMesh)
            {
                if (!NavMesh.SamplePosition(candidate, out var navHit, navSampleMaxDistance, NavMesh.AllAreas))
                    continue;
                candidate = navHit.position;
            }

            // 4) Espaciado mínimo contra ya colocados
            bool tooClose = false;
            for (int i = 0; i < placedPositions.Count; i++)
            {
                if (Vector3.Distance(candidate, placedPositions[i]) < minSpacing)
                {
                    tooClose = true; break;
                }
            }
            if (tooClose) continue;

            // 5) Instanciar
            GameObject prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];
            GameObject tree = (GameObject)Instantiate(prefab, candidate, Quaternion.identity, _generatedRoot);

            // Rotación/escala aleatoria
            if (randomYaw) tree.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            if (uniformScaleRange.x != 1f || uniformScaleRange.y != 1f)
            {
                float s = Random.Range(uniformScaleRange.x, uniformScaleRange.y);
                tree.transform.localScale = Vector3.one * s;
            }

            // Capa al árbol y a todos sus hijos (colliders incluidos)
            SetLayerRecursively(tree, treesLayer);

            placedPositions.Add(candidate);
            placed++;
        }

        hasSpawned = true;
        Debug.Log($"[ForestSpawnerOnce] Generados {placed}/{totalTrees} árboles (intentos: {attempts}). Guarda la escena para persistirlos.");

        // 6) Rebake NavMesh si se asignó (para tallar huecos si tus prefabs tienen NavMeshObstacle con Carve)
        if (navSurface != null)
        {
            navSurface.BuildNavMesh();
        }

        // Opcional: deshabilita el componente para que no vuelva a tocar nada
        #if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
        #endif
        this.enabled = false;
    }

    /// Borra SOLO lo generado por este spawner (lo que cuelga de __Forest_Generated).
    public void ClearGenerated()
    {
        if (_generatedRoot == null) return;

        for (int i = _generatedRoot.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(_generatedRoot.GetChild(i).gameObject);
            else Destroy(_generatedRoot.GetChild(i).gameObject);
#else
            Destroy(_generatedRoot.GetChild(i).gameObject);
#endif
        }

        hasSpawned = false;
        Debug.Log("[ForestSpawnerOnce] Limpiado. Ya puedes volver a generar.");
    }

    private void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
            SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ForestSpawnerOnce))]
public class ForestSpawnerOnceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var spawner = (ForestSpawnerOnce)target;
        GUILayout.Space(10);
        EditorGUILayout.HelpBox("Pulsa GENERATE para sembrar árboles una vez. Guarda la escena para persistir.", MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate (Once)"))
            {
                spawner.GenerateOnce();
            }
            if (GUILayout.Button("Clear"))
            {
                spawner.ClearGenerated();
            }
        }
    }
}
#endif
