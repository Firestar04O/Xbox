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
    public static class ConfiguradorLab4
    {
        private const string RutaEscena = "Assets/Scenes/Lab4.unity";
        private const string CarpetaPrefabs = "Assets/Prefabs";
        private const string RutaPrefabJugador = CarpetaPrefabs + "/JugadorCombate.prefab";

        [InitializeOnLoadMethod]
        private static void Inicializar()
        {
            EditorApplication.playModeStateChanged -= AlCambiarEstadoPlay;
            EditorApplication.playModeStateChanged += AlCambiarEstadoPlay;

            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    PrepararEscenaActiva(true);
                }
            };
        }

        private static void AlCambiarEstadoPlay(PlayModeStateChange estado)
        {
            if (estado == PlayModeStateChange.EnteredEditMode ||
                estado == PlayModeStateChange.ExitingEditMode)
            {
                PrepararEscenaActiva(true);
            }
        }

        [MenuItem("Tools/Bosque compartido/Preparar Lab 4")]
        public static void PrepararDesdeMenu()
        {
            Scene escena = SceneManager.GetActiveScene();
            if (escena.path != RutaEscena)
            {
                escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
            }

            PrepararEscena(escena, true);
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
            GameObject prefabJugador = CrearOEncontrarPrefabJugador();
            ConfigurarNetworkManager(escena, prefabJugador);
            int cantidadArboles = ConfigurarArboles(escena);
            AgregarEscenaAlBuild();

            if (cantidadArboles == 0)
            {
                Debug.LogWarning("Lab 4 no contiene objetos llamados Arbol o Arbol (n).");
                return;
            }

            EditorSceneManager.MarkSceneDirty(escena);
            if (guardar)
            {
                EditorSceneManager.SaveScene(escena);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"Lab 4 preparado: {cantidadArboles} arbol(es) de combate y jugador de red configurados.");
        }

        private static GameObject CrearOEncontrarPrefabJugador()
        {
            if (!AssetDatabase.IsValidFolder(CarpetaPrefabs))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefabJugador);
            if (prefab != null &&
                prefab.GetComponent<NetworkObject>() != null &&
                prefab.GetComponent<JugadorCombate>() != null)
            {
                return prefab;
            }

            GameObject temporal = new GameObject("JugadorCombate");
            temporal.AddComponent<NetworkObject>();
            temporal.AddComponent<JugadorCombate>();

            prefab = PrefabUtility.SaveAsPrefabAsset(temporal, RutaPrefabJugador);
            UnityEngine.Object.DestroyImmediate(temporal);
            return prefab;
        }

        private static void ConfigurarNetworkManager(Scene escena, GameObject prefabJugador)
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
            manager.NetworkConfig.PlayerPrefab = prefabJugador;

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
            ArbolCompartido comportamientoAnterior = arbol.GetComponent<ArbolCompartido>();
            if (comportamientoAnterior != null)
            {
                UnityEngine.Object.DestroyImmediate(comportamientoAnterior, true);
            }

            if (arbol.GetComponent<NetworkObject>() == null)
            {
                arbol.AddComponent<NetworkObject>();
            }

            if (arbol.GetComponent<BarraVidaArbol>() == null)
            {
                arbol.AddComponent<BarraVidaArbol>();
            }

            ArbolCombate comportamiento = arbol.GetComponent<ArbolCombate>();
            if (comportamiento == null)
            {
                comportamiento = arbol.AddComponent<ArbolCombate>();
            }

            Renderer[] renderers = arbol.GetComponentsInChildren<Renderer>(true);
            Renderer tronco = renderers.FirstOrDefault(
                renderer => renderer.name.Equals("Tronco", StringComparison.OrdinalIgnoreCase));
            Renderer[] hojas = renderers
                .Where(renderer => renderer.name.StartsWith("Hoja", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            TMP_Text texto = arbol.GetComponentInChildren<TMP_Text>(true);

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

        private static void AgregarEscenaAlBuild()
        {
            if (EditorBuildSettings.scenes.Any(escena => escena.path == RutaEscena))
            {
                return;
            }

            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Concat(new[] { new EditorBuildSettingsScene(RutaEscena, true) })
                .ToArray();
        }
    }
}
