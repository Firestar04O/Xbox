using BosqueCompartido;
using UnityEditor;
using UnityEngine;

namespace BosqueCompartidoEditor
{
    [CustomEditor(typeof(InicioRedGUI))]
    public sealed class InicioRedGUIEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("nombreJugador"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("iniciarAutomaticamente"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("modoInicio"));

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Esta configuracion no dibuja ningun Canvas ni interfaz durante el juego.",
                MessageType.Info);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Entra en Play para habilitar los controles manuales de red.",
                    MessageType.None);
                return;
            }

            InicioRedGUI inicio = (InicioRedGUI)target;
            EditorGUILayout.LabelField("Estado", inicio.EstadoActual);

            using (new EditorGUI.DisabledScope(inicio.EstadoActual != "Desconectado"))
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Host"))
                {
                    inicio.IniciarHost();
                }

                if (GUILayout.Button("Cliente"))
                {
                    inicio.IniciarCliente();
                }

                if (GUILayout.Button("Servidor"))
                {
                    inicio.IniciarServidor();
                }

                EditorGUILayout.EndHorizontal();
            }

            using (new EditorGUI.DisabledScope(inicio.EstadoActual == "Desconectado"))
            {
                if (GUILayout.Button("Desconectar"))
                {
                    inicio.Desconectar();
                }
            }

            Repaint();
        }
    }
}
