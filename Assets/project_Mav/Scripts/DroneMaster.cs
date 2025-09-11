using UnityEngine;
using System.Collections.Generic;

public class DroneMaster : MonoBehaviour
{
    [SerializeField] private List<DroneController> drones = new List<DroneController>();
    [SerializeField] private float formationSpread = 5f;

    // 🔍 Palabras clave de la persona objetivo
    private string targetColor = "blue";
    private bool targetHasHat = false;

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

        if (drones.Count == 0)
        {
            Debug.LogError("No hay drones asignados para la misión!");
            return;
        }

        for (int i = 0; i < drones.Count; i++)
        {
            if (drones[i] == null)
            {
                Debug.LogError($"El dron en el índice {i} no existe!");
                continue;
            }

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

        // 🎨 Detectar color
        if (desc.Contains("red")) targetColor = "red";
        else if (desc.Contains("green")) targetColor = "green";
        else if (desc.Contains("blue")) targetColor = "blue";
        else targetColor = "unknown";

        // 👒 Detectar si tiene sombrero
        targetHasHat = desc.Contains("hat");

        Debug.Log($"🎯 Target esperado: {targetColor} shirt, hat={targetHasHat}");
    }

    // ✅ Comparar atributos de cada persona encontrada
    public void ReportPersonFound(PersonController pc)
    {
        if (pc == null) return;

        if (pc.shirtColor.ToLower() == targetColor && pc.hasHat == targetHasHat)
            Debug.Log("✅ ¡Target person encontrada!");
        else
            Debug.Log($"👤 Persona encontrada pero no coincide -> {pc.shirtColor}, hat={pc.hasHat}");
    }
}
