using UnityEngine;
using TMPro;

public class Inicio : MonoBehaviour
{
    [SerializeField] private DroneMaster droneMaster;
    //[SerializeField] private PrintBounds groundCoords;
    [SerializeField] private GameObject startScreen;
    [SerializeField] private TMP_InputField xInput;
    [SerializeField] private TMP_InputField zInput;
    [SerializeField] private TMP_InputField descriptionInput;

    private void Start()
    {
        // Obtener la referencia al DroneMaster si no está asignada
        if (droneMaster == null)
        {
            droneMaster = FindAnyObjectByType<DroneMaster>();
            if (droneMaster == null)
            {
                Debug.LogError("No se encontró el DroneMaster en la escena!");
            }
        }
        
        startScreen.SetActive(true);
    }

    public void StartMission()
    {
        if (droneMaster == null)
        {
            Debug.LogError("DroneMaster no está asignado!");
            return;
        }

        if (float.TryParse(xInput.text, out float x) && float.TryParse(zInput.text, out float z))
        {
            if (!droneMaster.IsInsideBounds(x, z))
            {
                Debug.LogError("❌ Coordenadas fuera de los límites del terreno!");
                return; // No iniciar misión
            }
            startScreen.SetActive(false);
            droneMaster.StartMission(x, z, descriptionInput.text);
        }
        else
        {
            Debug.LogError("Coordenadas inválidas! Por favor ingrese números válidos.");
        }
    }

    public void ValidateXInput(string value)
    {
        if (!float.TryParse(value, out _) && !string.IsNullOrEmpty(value))
        {
            xInput.text = "";
            Debug.LogWarning("Por favor ingrese un número válido para la coordenada X");
        }
    }

    public void ValidateZInput(string value)
    {
        if (!float.TryParse(value, out _) && !string.IsNullOrEmpty(value))
        {
            zInput.text = "";
            Debug.LogWarning("Por favor ingrese un número válido para la coordenada Z");
        }
    }
}