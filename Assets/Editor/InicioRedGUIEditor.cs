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
            EditorGUILayout.PropertyField(serializedObject.FindProperty("mostrarInterfaz"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("direccionServidor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("puerto"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("iniciarAutomaticamente"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("modoInicio"));

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Durante el juego aparece un panel para iniciar Host, Cliente o Servidor y regresar al menu.",
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
