using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BosqueCompartido
{
    public sealed class MenuLaboratorios : MonoBehaviour
    {
        private bool cargando;

        public void AbrirLab3() => Cargar("Lab3");
        public void AbrirLab4() => Cargar("Lab4");
        public void AbrirLab5() => Cargar("Lab5");
        public void AbrirLab6() => Cargar("Lab6");

        public void Salir()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Cargar(string escena)
        {
            if (!cargando)
            {
                StartCoroutine(LimpiarRedYCargar(escena));
            }
        }

        private IEnumerator LimpiarRedYCargar(string escena)
        {
            cargando = true;
            NetworkManager red = NetworkManager.Singleton;

            if (red != null && red.IsListening)
            {
                red.Shutdown();
                yield return null;
            }

            if (red != null)
            {
                Destroy(red.gameObject);
                yield return null;
            }

            SceneManager.LoadScene(escena);
        }
    }
}
