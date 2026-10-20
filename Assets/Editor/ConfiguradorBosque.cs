using System;
using System.Linq;
using BosqueCompartido;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BosqueCompartidoEditor
{
    public static class ConfiguradorBosque
    {
        private const string RutaEscena = "Assets/Scenes/Lab3.unity";

        // Mantiene sincronizada la escena que se usara en Multiplayer Play Mode.
        // Los NetworkObject colocados en escena deben existir en el archivo guardado
        // antes de que Host y Cliente carguen sus respectivas copias.
        [InitializeOnLoadMethod]
        private static void ProgramarConfiguracionInicial()
        {
            EditorApplication.playModeStateChanged -= AlCambiarEstadoPlay;
            EditorApplication.playModeStateChanged += AlCambiarEstadoPlay;

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                PrepararEscenaActiva(true);
            };
        }

        private static void AlCambiarEstadoPlay(PlayModeStateChange estado)
        {
            // EnteredEditMode cubre el caso en que se copiaron arboles antes de
            // detener una sesion. ExitingEditMode garantiza que todos los jugadores
            // carguen exactamente la misma version guardada de la escena.
            if (estado == PlayModeStateChange.EnteredEditMode ||
                estado == PlayModeStateChange.ExitingEditMode)
            {
                PrepararEscenaActiva(true);
            }
        }

        [MenuItem("Tools/Bosque compartido/Configurar escena")]
        public static void ConfigurarEscena()
        {
            Scene escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
            PrepararEscena(escena, true);
        }

        [MenuItem("Tools/Bosque compartido/Preparar y guardar todos los arboles")]
        public static void PrepararYGuardarTodosLosArboles()
        {
            if (!PrepararEscenaActiva(true))
            {
                Debug.LogWarning($"Abre la escena {RutaEscena} antes de preparar el bosque.");
            }
        }

        private static bool PrepararEscenaActiva(bool guardar)
        {
            Scene escena = SceneManager.GetActiveScene();
            if (!escena.IsValid() || escena.path != RutaEscena)
            {
                return false;
            }

            PrepararEscena(escena, guardar);
            return true;
        }

        private static void PrepararEscena(Scene escena, bool guardar)
        {
            ConfigurarNetworkManager(escena);
            int cantidadArboles = ConfigurarArboles(escena);
            AgregarEscenaAlBuild();

            if (cantidadArboles == 0)
            {
                Debug.LogWarning("No se encontro ningun GameObject llamado 'Arbol' o 'Arbol (n)'.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(escena);
            if (guardar)
            {
                EditorSceneManager.SaveScene(escena);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"Bosque compartido preparado: {cantidadArboles} arbol(es) guardados y listos para red.");
        }

        private static void ConfigurarNetworkManager(Scene escena)
        {
            GameObject objeto = escena.GetRootGameObjects()
                .FirstOrDefault(go => go.name == "NetworkManager");

            if (objeto == null)
            {
                objeto = new GameObject("NetworkManager");
                SceneManager.MoveGameObjectToScene(objeto, escena);
            }

            UnityTransport transporte = objeto.GetComponent<UnityTransport>();
            if (transporte == null)
            {
                transporte = objeto.AddComponent<UnityTransport>();
            }

            NetworkManager manager = objeto.GetComponent<NetworkManager>();
            if (manager == null)
            {
                manager = objeto.AddComponent<NetworkManager>();
            }

            manager.NetworkConfig.NetworkTransport = transporte;

            if (objeto.GetComponent<InicioRedGUI>() == null)
            {
                objeto.AddComponent<InicioRedGUI>();
            }

            EditorUtility.SetDirty(objeto);
            EditorUtility.SetDirty(manager);
        }

        private static int ConfigurarArboles(Scene escena)
        {
            GameObject[] arboles = escena.GetRootGameObjects()
                .SelectMany(raiz => raiz.GetComponentsInChildren<Transform>(true))
                .Select(transformacion => transformacion.gameObject)
                .Where(EsArbol)
                .OrderBy(arbol => arbol.name)
                .ToArray();

            foreach (GameObject arbol in arboles)
            {
                ConfigurarArbol(arbol);
            }

            return arboles.Length;
        }

        private static bool EsArbol(GameObject objeto)
        {
            return objeto.name.Equals("Arbol", StringComparison.OrdinalIgnoreCase) ||
                   objeto.name.StartsWith("Arbol (", StringComparison.OrdinalIgnoreCase);
        }

        private static void ConfigurarArbol(GameObject arbol)
        {
            if (arbol.GetComponent<NetworkObject>() == null)
            {
                arbol.AddComponent<NetworkObject>();
            }

            BosqueCompartido.ArbolCompartido comportamiento = arbol.GetComponent<BosqueCompartido.ArbolCompartido>();
            if (comportamiento == null)
            {
                comportamiento = arbol.AddComponent<BosqueCompartido.ArbolCompartido>();
            }

            TMP_Text texto = CrearOEncontrarTexto(arbol.transform);
            Renderer[] renderers = arbol.GetComponentsInChildren<Renderer>(true);
            Renderer tronco = renderers.FirstOrDefault(r => r.name.Equals("Tronco", StringComparison.OrdinalIgnoreCase));
            Renderer[] hojas = renderers
                .Where(r => r.name.StartsWith("Hoja", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            SerializedObject serializado = new SerializedObject(comportamiento);
            serializado.FindProperty("rendererTronco").objectReferenceValue = tronco;
            serializado.FindProperty("textoDueno").objectReferenceValue = texto;

            SerializedProperty listaHojas = serializado.FindProperty("renderersHojas");
            listaHojas.arraySize = hojas.Length;
            for (int i = 0; i < hojas.Length; i++)
            {
                listaHojas.GetArrayElementAtIndex(i).objectReferenceValue = hojas[i];
            }

            serializado.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(arbol);
            EditorUtility.SetDirty(comportamiento);
        }

        private static TMP_Text CrearOEncontrarTexto(Transform arbol)
        {
            Transform existente = arbol.Find("NombreDueno");
            GameObject objetoTexto;

            if (existente == null)
            {
                objetoTexto = new GameObject("NombreDueno");
                objetoTexto.transform.SetParent(arbol, false);
                objetoTexto.transform.localPosition = new Vector3(0f, 1.35f, 0f);
                objetoTexto.transform.localScale = Vector3.one * 0.2f;
            }
            else
            {
                objetoTexto = existente.gameObject;
            }

            TextMeshPro texto = objetoTexto.GetComponent<TextMeshPro>();
            if (texto == null)
            {
                texto = objetoTexto.AddComponent<TextMeshPro>();
            }

            texto.text = "Libre";
            texto.alignment = TextAlignmentOptions.Center;
            texto.fontSize = 5f;
            texto.color = Color.white;

            if (objetoTexto.GetComponent<TextoOrientadoACamara>() == null)
            {
                objetoTexto.AddComponent<TextoOrientadoACamara>();
            }

            EditorUtility.SetDirty(objetoTexto);
            return texto;
        }

        private static void AgregarEscenaAlBuild()
        {
            if (EditorBuildSettings.scenes.Any(e => e.path == RutaEscena))
            {
                return;
            }

            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Concat(new[] { new EditorBuildSettingsScene(RutaEscena, true) })
                .ToArray();
        }
    }
}
