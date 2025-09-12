using UnityEngine;
using System.Collections.Generic;

public class DroneMaster : MonoBehaviour
{
    [SerializeField] private List<DroneController> drones = new List<DroneController>();
    [SerializeField] private float formationSpread = 5f;

    [Header("Prefabs")]
    public GameObject personPrefab; // arrastra tu FarmGuy.prefab aquí

    // Objetivo
    private string targetColor = "blue";
    private bool targetHasHat = false;
    private PersonController targetPerson;

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

        // 🧍 Spawn de la target person EXACTAMENTE en la coordenada
        Vector3 pos = new Vector3(x, 0, z);
        GameObject person = Instantiate(personPrefab, pos, Quaternion.identity);
        person.tag = "Person";

        targetPerson = person.GetComponent<PersonController>();
        targetPerson.shirtColor = targetColor;
        targetPerson.hasHat = targetHasHat;

        Debug.Log($"👤 Target person spawneada en {pos} con {targetColor} shirt, hat={targetHasHat}");

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
            float standOff = 2.5f; 
            drone.LandNearTarget(pc.transform.position, standOff);
            //drone.LandAtTarget(pc.transform.position); agregar ubn radio
        }
        else
        {
            Debug.Log($"👤 Persona encontrada pero no coincide -> {pc.shirtColor}, hat={pc.hasHat}");
        }
    }
}
