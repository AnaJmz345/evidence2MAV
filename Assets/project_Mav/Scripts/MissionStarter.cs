using UnityEngine;

public class MissionStarter : MonoBehaviour
{
    public DroneController drone;
    
    public void StartMissionWithCoordinates(float x, float z)
    {
        if (drone != null)
            drone.GoToXZ(x, z);
    }
}