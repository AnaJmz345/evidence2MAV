## Installation, Configuration & Execution of the System

The following steps describe how to set up and run the simulation:

### Prerequisites

- **Unity Hub** installed.
- **Unity version 6000.1.15f1** (the simulation was tested on this version).
- **Recommended OS:** Windows 10/11.

### Cloning the Project

Clone the repository:

```bash
git clone https://github.com/AnaJmz345/evidence2MAV.git
```

Open the project folder in Unity Hub and select Unity version **6000.1.15f1** to load it.

### Scene Configuration

- Open the main Unity scene at:  
  `Assets\project_Mav\Escena\main`
- Ensure the following layers exist and are assigned:
  - `Obstacles` &rarr; for environmental blocks.
  - `Water` &rarr; for areas where drones cannot land.
  - `Persona` &rarr; for spawned person prefabs.
- **Bake the NavMesh:**  
  Select the `NavMeshSurface` component in the scene and press **Bake**. This enables autonomous navigation for the drones.

### Execution

1. Enter **Play Mode** in Unity.
2. Enter the coordinates and description of the Person of Interest on the start screen, then press the mission start button.
3. Drones will:
   - Navigate to the assigned zone.
   - Scan for the person of interest.
   - Report candidate to the master.
   - Land autonomously near the correct person or fallback to a safe landing.
