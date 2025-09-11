using UnityEngine;
using System.Collections.Generic;

public class DroneMaster : MonoBehaviour
{
    [SerializeField] private List<DroneController> drones = new List<DroneController>();
    [SerializeField] private float formationSpread = 5f;

    void Start()
    {
        if (drones.Count == 0)
        {
            DroneController[] foundDrones = FindObjectsByType<DroneController>(FindObjectsSortMode.None);
            foreach (DroneController drone in foundDrones)
            {
                drones.Add(drone);
                drone.Deactivate(); // ✅ Ya no usamos enabled = false
            }

            Debug.Log($"Encontrados {drones.Count} drones en la escena");
        }
        else
        {
            foreach (DroneController drone in drones)
            {
                if (drone != null) drone.Deactivate(); // ✅
            }
        }
    }

    public void StartMission(float x, float z, string description)
    {
        Debug.Log($"Iniciando misión en coordenadas ({x}, {z}) - Objetivo: {description}");
        //  analyzeText(description);

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

            drones[i].Activate(); // ✅ Activar lógica
            drones[i].GoToXZ(x + offsetX, z + offsetZ);

            Debug.Log($"Dron {i} enviado a ({x + offsetX}, {z + offsetZ})");
        }
    }

    /*public void analyzeText(description){

    }*/
}
