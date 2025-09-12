using UnityEngine;

public class MissionStarter : MonoBehaviour
{
    public DroneController drone;

    public void StartMission()
    {
        if (drone != null)
        {
            drone.Activate();  // activa navegación aleatoria
        }
    }
}
