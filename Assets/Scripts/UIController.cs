using TMPro;
using UnityEngine;

namespace Findex.UI
{
    public class UIController : MonoBehaviour
    {
        [Header("Referencias")]
        public TMP_InputField inputCoords; // el input de la “píldora”
        public GridPlot grid;              // el GridArea con GridPlot

        // Llama esto desde el evento On Value Changed del TMP_InputField
        public void OnCoordsChanged(string s)
        {
            if (grid == null) return;
            if (string.IsNullOrWhiteSpace(s)) return;

            // Formatos aceptados:
            //  - "y=2"  -> línea roja en y = 2
            //  - "2"    -> interpreta como y=2
            s = s.Trim().ToLower();

            float y;
            if (s.StartsWith("y="))
            {
                if (float.TryParse(s.Substring(2), out y))
                {
                    grid.yValue = y;
                    grid.Draw();
                }
            }
            else if (float.TryParse(s, out y))
            {
                grid.yValue = y;
                grid.Draw();
            }
            // Si luego quieres parsear rangos x0,y0 — x1,y1, aquí puedes extenderlo.
        }
    }
}
