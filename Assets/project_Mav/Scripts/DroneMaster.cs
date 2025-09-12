using UnityEngine;
using System.Collections.Generic;

public class DroneMaster : MonoBehaviour
{
    [SerializeField] private List<DroneController> drones = new List<DroneController>();
    [SerializeField] private float formationSpread = 5f;

    [Header("Prefabs")]
    public List<GameObject> personPrefabs; // arrastra tu FarmGuy.prefab aquí
    // coordenadas extra para spawnear distractores
    public float distractorRadius = 30f;
    public int distractorCount = 3; // cuántos distraer máximo



    // Objetivo
    private string targetColor = "blue";
    private bool targetHasHat = false;
    private PersonController targetPerson;

    private GameObject SelectTargetPrefab(string color, bool hasHat)
    {
        foreach (var prefab in personPrefabs)
        {
            PersonController pc = prefab.GetComponent<PersonController>();
            if (pc == null) continue;

            if (pc.shirtColor.ToLower() == color.ToLower() && pc.hasHat == hasHat)
            {
                Debug.Log($"🎯 Seleccionado prefab: {pc.prefabName} ({pc.shirtColor}, hat={pc.hasHat})");
                return prefab;
            }
        }

        return null; // si no hay match
    }

        // DroneMaster.cs
    [Header("Límites del terreno")]
    public Vector3 groundMin;  // Min (-151.90, -28.93, -150.00)
    public Vector3 groundMax;  // Max (151.08, 19.29, 159.84)

     // 📐 Calcula los límites del terreno
    private void CalculateGroundBounds()
    {
        Debug.Log("Holamiamor");
        Terrain t = FindObjectOfType<Terrain>();
        if (t != null)
        {
            Vector3 pos = t.GetPosition();
            Vector3 size = t.terrainData.size;
            groundMin = pos;
            groundMax = pos + size;
            Debug.Log($"🌍 Bounds obtenidos del Terrain: Min={groundMin}, Max={groundMax}");
            return;
        }

        Renderer r = GameObject.Find("Ground")?.GetComponent<Renderer>();
        if (r != null)
        {
            Bounds b = r.bounds;
            groundMin = b.min;
            groundMax = b.max;
            Debug.Log($"🟫 Bounds obtenidos de Ground Renderer: Min={groundMin}, Max={groundMax}");
        }
        else
        {
            Debug.LogWarning("⚠️ No se encontró Terrain ni Renderer llamado 'Ground'. Usa valores por defecto.");
            groundMin = new Vector3(-100, 0, -100);
            groundMax = new Vector3(100, 0, 100);
        }
    }

    // ✅ Validar coordenadas
    public bool IsInsideBounds(float x, float z)
    {
        CalculateGroundBounds();
        return x >= groundMin.x && x <= groundMax.x &&
               z >= groundMin.z && z <= groundMax.z;
    }

    // ✅ Clamp para asegurar spawn dentro del terreno
    public Vector3 ClampToGround(Vector3 pos)
    {
        return new Vector3(
            Mathf.Clamp(pos.x, groundMin.x, groundMax.x),
            pos.y,
            Mathf.Clamp(pos.z, groundMin.z, groundMax.z)
        );
    }




    void Start()
    {
        if (drones.Count == 0)
        {
            DroneController[] foundDrones = FindObjectsByType<DroneController>(FindObjectsSortMode.None);
            foreach (DroneController drone in foundDrones)
            {
                drones.Add(drone);
                drone.Deactivate();
            }

            Debug.Log($"Encontrados {drones.Count} drones en la escena");
        }
        else
        {
            foreach (DroneController drone in drones)
            {
                if (drone != null) drone.Deactivate();
            }
        }
    }

    public void StartMission(float x, float z, string description)
    {
        Debug.Log($"Iniciando misión en coordenadas ({x}, {z}) - Objetivo: {description}");

        AnalyzeText(description);
         // 🧍 Buscar prefab correcto según descripción
        GameObject targetPrefab = SelectTargetPrefab(targetColor, targetHasHat);
        if (targetPrefab == null)
        {
            Debug.LogError("❌ No se encontró un prefab que coincida con la descripción!");
            return;
        }

        // 🧍 Spawn de la target person EXACTAMENTE en la coordenada
        Vector3 targetPos = new Vector3(x, 0, z);
        targetPos = ClampToGround(targetPos);
        GameObject targetGO = Instantiate(targetPrefab, targetPos, Quaternion.identity);
        targetGO.layer = LayerMask.NameToLayer("Objetivo");
        targetPerson = targetGO.GetComponent<PersonController>();

        targetPerson.shirtColor = targetColor;
        targetPerson.hasHat = targetHasHat;

        Debug.Log($"👤 Target person spawneada en {targetPos} con {targetColor} shirt, hat={targetHasHat}");

         // Spawn de distractores
        foreach (var prefab in personPrefabs)
        {
            if (prefab == targetPrefab) continue; // saltar el target

            // 🔸 Generar posición aleatoria en un radio de 20 m alrededor del target
            Vector2 circle = Random.insideUnitCircle * 20f; // 20 metros
            Vector3 randomPos = new Vector3(
                targetPos.x + circle.x,
                0,
                targetPos.z + circle.y
            );

            // 🔸 Clampear dentro de los límites del terreno
            randomPos = new Vector3(
                Mathf.Clamp(randomPos.x, groundMin.x, groundMax.x),
                0,
                Mathf.Clamp(randomPos.z, groundMin.z, groundMax.z)
            );

            GameObject distractor = Instantiate(prefab, randomPos, Quaternion.identity);
            distractor.tag = "Person";
            distractor.layer = LayerMask.NameToLayer("Persona");

            Debug.Log($"👤 Distractor spawneado en {randomPos}");
        }






        if (drones.Count == 0)
        {
            Debug.LogError("No hay drones asignados para la misión!");
            return;
        }

        for (int i = 0; i < drones.Count; i++)
        {
            if (drones[i] == null) continue;

            float offsetX = (i % 2 == 0) ? formationSpread : -formationSpread;
            float offsetZ = (i < 2) ? formationSpread : -formationSpread;

            drones[i].Activate();
            drones[i].GoToXZ(x + offsetX, z + offsetZ);

            Debug.Log($"Dron {i} enviado a ({x + offsetX}, {z + offsetZ})");
        }
    }

    private void AnalyzeText(string description)
    {
        string desc = description.ToLower();

        // 🎨 Paleta de colores soportados
        string[] colors = { "red", "green", "blue", "yellow", "black", "white" };
        targetColor = "blue"; // default

        foreach (var color in colors)
        {
            if (desc.Contains(color))
            {
                targetColor = color;
                break;
            }
        }

        // 👒 Sombrero
        targetHasHat = desc.Contains("hat") || desc.Contains("cap") || desc.Contains("sombrero");

        Debug.Log($"🎯 Target esperado: {targetColor} shirt, hat={targetHasHat}");
    }

    // ✅ Comparar atributos
    public void ReportPersonFound(PersonController pc, DroneController drone)
    {
        if (pc == null) return;

        Debug.Log($"🔎 {drone.name} verificando persona -> Person=({pc.shirtColor}, hat={pc.hasHat}) | Target=({targetColor}, hat={targetHasHat})");

        if (pc.shirtColor.Trim().ToLower() == targetColor.Trim().ToLower() 
            && pc.hasHat == targetHasHat)
        {
            Debug.Log($"✅ {drone.name} encontró a la target en {pc.transform.position}");
            drone.LandAtTarget(pc.transform.position);
        }
        else
        {
            Debug.Log($"👤 Persona encontrada pero no coincide -> {pc.shirtColor}, hat={pc.hasHat}");
        }
    }
}
