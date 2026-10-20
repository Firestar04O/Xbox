using TMPro;
using UnityEngine;

namespace BosqueCompartido
{
    public sealed class TextoDanoFlotante : MonoBehaviour
    {
        private const float Duracion = 0.9f;

        private TextMeshPro texto;
        private float tiempoRestante;

        public static void Crear(Vector3 posicion, int dano, int vidaRestante)
        {
            GameObject objeto = new GameObject("DanoFlotante");
            objeto.transform.position = posicion;

            TextMeshPro componenteTexto = objeto.AddComponent<TextMeshPro>();
            componenteTexto.text = $"-{dano}  ({vidaRestante})";
            componenteTexto.alignment = TextAlignmentOptions.Center;
            componenteTexto.fontSize = 4f;
            componenteTexto.color = new Color(1f, 0.25f, 0.1f);
            componenteTexto.sortingOrder = 30;

            TextoDanoFlotante comportamiento = objeto.AddComponent<TextoDanoFlotante>();
            comportamiento.texto = componenteTexto;
            comportamiento.tiempoRestante = Duracion;
        }

        public static void CrearMensaje(Vector3 posicion, string mensaje, Color color)
        {
            GameObject objeto = new GameObject("MensajeFlotante");
            objeto.transform.position = posicion;

            TextMeshPro componenteTexto = objeto.AddComponent<TextMeshPro>();
            componenteTexto.text = mensaje;
            componenteTexto.alignment = TextAlignmentOptions.Center;
            componenteTexto.fontSize = 3f;
            componenteTexto.color = color;
            componenteTexto.sortingOrder = 30;

            TextoDanoFlotante comportamiento = objeto.AddComponent<TextoDanoFlotante>();
            comportamiento.texto = componenteTexto;
            comportamiento.tiempoRestante = Duracion;
        }

        private void Update()
        {
            tiempoRestante -= Time.deltaTime;
            transform.position += Vector3.up * (0.65f * Time.deltaTime);

            Camera camara = Camera.main;
            if (camara != null)
            {
                transform.rotation = Quaternion.LookRotation(
                    transform.position - camara.transform.position,
                    Vector3.up);
            }

            if (texto != null)
            {
                Color color = texto.color;
                color.a = Mathf.Clamp01(tiempoRestante / Duracion);
                texto.color = color;
            }

            if (tiempoRestante <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
