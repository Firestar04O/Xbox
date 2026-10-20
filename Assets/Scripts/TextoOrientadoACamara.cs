using UnityEngine;

namespace BosqueCompartido
{
    public sealed class TextoOrientadoACamara : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera camara = Camera.main;
            if (camara != null)
            {
                transform.rotation = Quaternion.LookRotation(
                    transform.position - camara.transform.position,
                    Vector3.up);
            }
        }
    }
}
