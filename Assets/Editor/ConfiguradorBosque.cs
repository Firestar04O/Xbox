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
        private const string RutaEscena = "Assets/Scenes/SampleScene.unity";

        // Configura una sola vez el proyecto actual cuando Unity termina de compilar
        // estos scripts. El guard evita volver a tocar escenas ya configuradas.
        [InitializeOnLoadMethod]
        private static void ProgramarConfiguracionInicial()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                Scene escenaActiva = SceneManager.GetActiveScene();
                GameObject arbol = GameObject.Find("Arbol");

                if (escenaActiva.path == RutaEscena &&
                    arbol != null &&
                    arbol.GetComponent<BosqueCompartido.ArbolCompartido>() == null)
                {
                    ConfigurarEscena();
                }
            };
        }

        [MenuItem("Tools/Bosque compartido/Configurar escena")]
        public static void ConfigurarEscena()
        {
            Scene escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);

            ConfigurarNetworkManager();
            ConfigurarArbol();
            AgregarEscenaAlBuild();

            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            AssetDatabase.SaveAssets();
            Debug.Log("Bosque compartido configurado correctamente.");
        }

        private static void ConfigurarNetworkManager()
        {
            GameObject objeto = GameObject.Find("NetworkManager");
            if (objeto == null)
            {
                objeto = new GameObject("NetworkManager");
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

        private static void ConfigurarArbol()
        {
            GameObject arbol = GameObject.Find("Arbol");
            if (arbol == null)
            {
                throw new InvalidOperationException("No se encontro un GameObject llamado 'Arbol'.");
            }

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
