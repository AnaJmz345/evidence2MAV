using UnityEngine;
using UnityEngine.UI;

namespace Findex.UI
{
    [RequireComponent(typeof(RawImage))]
    public class GridPlot : MonoBehaviour
    {
        [Header("Tamaño textura")]
        public int width = 300;
        public int height = 300;

        [Header("Estilo")]
        public Color bgColor = Color.white;
        public Color gridColor = new Color(0.85f, 0.85f, 0.85f);
        public Color axisColor = Color.black;
        public Color lineColor = Color.red;
        public int gridStep = 30;       // px entre líneas de la retícula
        public int lineThickness = 2;

        [Header("Línea roja (y = valor)")]
        [Range(-10, 10)] public float yValue = 0f;
        public float unitsPerStep = 1f; // 1 unidad = 1 cuadrito

        private Texture2D tex;
        private RawImage raw;

        void Awake()
        {
            raw = GetComponent<RawImage>();
        }

        void Start()
        {
            Draw();
        }

        public void Draw()
        {
            if (width <= 0 || height <= 0) return;

            tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            // Fondo
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, y, bgColor);

            int cx = width / 2;
            int cy = height / 2;

            // Retícula
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (x % gridStep == 0 || y % gridStep == 0)
                        tex.SetPixel(x, y, gridColor);

            // Ejes
            DrawLine(tex, 0, cy, width - 1, cy, axisColor, 2);   // eje X
            DrawLine(tex, cx, 0, cx, height - 1, axisColor, 2);  // eje Y

            // Línea roja a y = yValue (en "unidades")
            int pixelsPerUnit = gridStep;
            int yPix = cy + Mathf.RoundToInt(yValue * pixelsPerUnit / unitsPerStep);
            DrawLine(tex, 0, yPix, width - 1, yPix, lineColor, lineThickness);

            tex.Apply();
            raw.texture = tex;
        }

        static void DrawLine(Texture2D t, int x0, int y0, int x1, int y1, Color c, int thickness = 1)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                for (int ox = -thickness/2; ox <= thickness/2; ox++)
                    for (int oy = -thickness/2; oy <= thickness/2; oy++)
                    {
                        int px = x0 + ox;
                        int py = y0 + oy;
                        if (px >= 0 && px < t.width && py >= 0 && py < t.height)
                            t.SetPixel(px, py, c);
                    }

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 <  dx) { err += dx; y0 += sy; }
            }
        }
    }
}
