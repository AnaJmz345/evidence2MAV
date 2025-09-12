using UnityEngine;
using System.Collections.Generic;
using TMPro;
public class DroneMaster : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI statusText; 

    [SerializeField] private List<DroneController> drones = new List<DroneController>();
    [SerializeField] private float formationSpread = 5f;

    [Header("Prefabs")]
    public List<GameObject> personPrefabs; // arrastra tu FarmGuy.prefab aquí
    // coordenadas extra para spawnear distractores
    public float distractorRadius = 30f;
    public int distractorCount = 3; // cuántos distraer máximo

    private bool missionAccomplished = false;
    private DroneController assignedDrone = null;

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
            //Debug.LogWarning("⚠️ No se encontró Terrain ni Renderer llamado 'Ground'. Usa valores por defecto.");
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
            drones.AddRange(FindObjectsByType<DroneController>(FindObjectsSortMode.None));
        }

        foreach (var drone in drones)
        {
            drone.Deactivate();
            drone.AssignMaster(this);
        }
        if (statusText != null)
        {
            statusText.gameObject.SetActive(false); 
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
        targetGO.tag = "Objetivo";
        targetGO.layer = LayerMask.NameToLayer("Objetivo");

       /* BoxCollider bc = targetGO.GetComponent<BoxCollider>();
        if (bc == null) bc = targetGO.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.center = new Vector3(0, 1.5f, 0);
        bc.size = new Vector3(1, 100f, 1);*/

        targetPerson = targetGO.GetComponentInChildren<PersonController>();

        targetPerson.shirtColor = targetColor;
        targetPerson.hasHat = targetHasHat;

        Debug.Log($"👤 Target person spawneada en {targetPos} con {targetColor} shirt, hat={targetHasHat}");

         // Spawn de distractores
        foreach (var prefab in personPrefabs)
        {
            if (prefab == targetPrefab) continue; // saltar el target

            // 🔸 Generar posición aleatoria en un radio de 20 m alrededor del target
            Vector2 circle = Random.insideUnitCircle * 40f; // 20 metros
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

        Vector3 fixedSearchCenter = targetGO.transform.position;

        foreach (var drone in drones)
        {
            drone.searchCenter = fixedSearchCenter;
            drone.searchRadius = 40f;
            drone.Activate();
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

     public void ReceiveACL(ACLMessage msg)
    {
        if (msg.Performative == "Inform" && msg.Content == "Found")
        {
            Debug.Log($"📩 ACL recibido de {msg.Sender.name}: Inform(Finding)");
            if (!missionAccomplished)
            {
                missionAccomplished = true;
                assignedDrone = msg.Sender;

                // ✅ Mostrar mensaje en UI
                if (statusText != null)
                {
                    statusText.gameObject.SetActive(true);
                    statusText.text = $"{msg.Sender.name} has found the target person!";
                }

                // ✅ detener el dron y la persona
                msg.Sender.StopMovement();
                if (targetPerson != null)
                {
                    var patrol = targetPerson.GetComponent<PersonCirclePatrol>();
                    if (patrol != null) patrol.StopPatrol();
                }

                // ACL de aterrizaje
                ACLMessage permit = new ACLMessage("DroneMaster", msg.Sender, "Permit", "Land");
                msg.Sender.ReceiveACL(permit);

                foreach (var drone in drones)
                {
                    if (drone != msg.Sender)
                    {
                        ACLMessage landHere = new ACLMessage("DroneMaster", drone, "Inform", "StopSearch");
                        drone.ReceiveACL(landHere);
                    }
                }
            }
        }

        if (msg.Performative == "Request" && msg.Content == "Land?")
        {
            Debug.Log($"🛬 {msg.Sender.name} solicita aterrizar.");
        }
    }


    public bool IsMissionDone() => missionAccomplished;
}
