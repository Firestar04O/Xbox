using UnityEngine;
using UnityEngine.UI;

namespace BosqueCompartido
{
    public sealed class BarraVidaArbol : MonoBehaviour
    {
        private const string NombreRaiz = "BarraVida";

        private GameObject raiz;
        private RectTransform relleno;
        private Image imagenRelleno;

        private void Awake()
        {
            CrearSiHaceFalta();
            Mostrar(false);
        }

        public void Actualizar(int vidaActual, int vidaMaxima, bool visible)
        {
            CrearSiHaceFalta();

            float proporcion = vidaMaxima <= 0
                ? 0f
                : Mathf.Clamp01((float)vidaActual / vidaMaxima);

            relleno.anchorMax = new Vector2(proporcion, 1f);
            imagenRelleno.color = proporcion > 0.6f
                ? new Color(0.2f, 0.9f, 0.25f)
                : proporcion > 0.3f
                    ? new Color(1f, 0.75f, 0.1f)
                    : new Color(0.95f, 0.15f, 0.1f);

            Mostrar(visible);
        }

        private void Mostrar(bool visible)
        {
            if (raiz != null)
            {
                raiz.SetActive(visible);
            }
        }

        private void CrearSiHaceFalta()
        {
            if (raiz != null)
            {
                return;
            }

            Transform existente = transform.Find(NombreRaiz);
            raiz = existente != null
                ? existente.gameObject
                : new GameObject(NombreRaiz, typeof(RectTransform), typeof(Canvas));
            raiz.transform.SetParent(transform, false);
            raiz.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            raiz.transform.localScale = Vector3.one * 0.01f;

            Canvas canvas = ObtenerOCrear<Canvas>(raiz);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;

            RectTransform rectRaiz = raiz.GetComponent<RectTransform>();
            rectRaiz.sizeDelta = new Vector2(120f, 16f);

            if (raiz.GetComponent<TextoOrientadoACamara>() == null)
            {
                raiz.AddComponent<TextoOrientadoACamara>();
            }

            GameObject fondo = ObtenerOCrearHijo(raiz.transform, "Fondo");
            RectTransform rectFondo = PrepararRectTransform(fondo);
            rectFondo.anchorMin = Vector2.zero;
            rectFondo.anchorMax = Vector2.one;
            rectFondo.offsetMin = Vector2.zero;
            rectFondo.offsetMax = Vector2.zero;
            ObtenerOCrear<Image>(fondo).color = new Color(0.03f, 0.03f, 0.03f, 0.9f);

            GameObject objetoRelleno = ObtenerOCrearHijo(fondo.transform, "Relleno");
            relleno = PrepararRectTransform(objetoRelleno);
            relleno.anchorMin = Vector2.zero;
            relleno.anchorMax = Vector2.one;
            relleno.offsetMin = new Vector2(2f, 2f);
            relleno.offsetMax = new Vector2(-2f, -2f);
            relleno.pivot = new Vector2(0f, 0.5f);
            imagenRelleno = ObtenerOCrear<Image>(objetoRelleno);
        }

        private static GameObject ObtenerOCrearHijo(Transform padre, string nombre)
        {
            Transform existente = padre.Find(nombre);
            if (existente != null)
            {
                return existente.gameObject;
            }

            GameObject hijo = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer));
            hijo.transform.SetParent(padre, false);
            return hijo;
        }

        private static RectTransform PrepararRectTransform(GameObject objeto)
        {
            RectTransform rect = objeto.GetComponent<RectTransform>();
            if (rect != null)
            {
                return rect;
            }

            return objeto.AddComponent<RectTransform>();
        }

        private static T ObtenerOCrear<T>(GameObject objeto) where T : Component
        {
            T componente = objeto.GetComponent<T>();
            return componente != null ? componente : objeto.AddComponent<T>();
        }
    }
}
