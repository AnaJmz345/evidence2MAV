using UnityEngine;

public class PersonController : MonoBehaviour
{
    [Header("Identificación del prefab")]
    public string prefabName = "DefaultPerson";   // Ej: "ClownGuy", "AngryGuy"

    [Header("Características de la persona")]
    public string shirtColor = "blue";            // Ej: "red", "green", "blue"
    public bool hasHat = false;                   // ¿Tiene sombrero?
}
