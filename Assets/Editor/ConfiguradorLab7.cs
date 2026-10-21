using System;
using System.IO;
using System.Linq;
using BosqueCompartido;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BosqueCompartidoEditor
{
    public static class ConfiguradorLab7
    {
        private const string RutaEscena = "Assets/Scenes/Lab7.unity";
        private const string CarpetaPrefabs = "Assets/Prefabs/Lab7";
        private const string CarpetaMateriales = "Assets/Materials/Lab7";
        private const string RutaJugador = CarpetaPrefabs + "/JugadorLab7.prefab";
        private const string RutaArbol = CarpetaPrefabs + "/ArbolReciclableLab7.prefab";
        private const string RutaListaPrefabs = "Assets/DefaultNetworkPrefabs.asset";

        [InitializeOnLoadMethod]
        private static void Inicializar()
        {
            EditorApplication.playModeStateChanged -= AlCambiarModo;
            EditorApplication.playModeStateChanged += AlCambiarModo;
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    PrepararEnSegundoPlano();
                }
            };
        }

        private static void AlCambiarModo(PlayModeStateChange estado)
        {
            if (estado == PlayModeStateChange.EnteredEditMode)
            {
                PrepararEnSegundoPlano();
            }
        }

        [MenuItem("Tools/Bosque Compartido/Preparar Lab 7")]
        public static void PrepararDesdeMenu()
        {
            Scene escena = SceneManager.GetActiveScene();
            if (!EsEscenaLab7(escena))
            {
                escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
            }

            PrepararEscena(escena, true);
        }

        private static void PrepararEnSegundoPlano()
        {
            if (!File.Exists(RutaEscena))
            {
                return;
            }

            Scene activa = SceneManager.GetActiveScene();
            if (EsEscenaLab7(activa))
            {
                PrepararEscena(activa, true);
                return;
            }

            Scene escena = SceneManager.GetSceneByPath(RutaEscena);
            bool cerrarAlTerminar = !escena.IsValid() || !escena.isLoaded;
            if (cerrarAlTerminar)
            {
                escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);
            }

            PrepararEscena(escena, true);

            if (cerrarAlTerminar)
            {
                EditorSceneManager.CloseScene(escena, true);
            }

            if (activa.IsValid() && activa.isLoaded)
            {
                SceneManager.SetActiveScene(activa);
            }
        }

        private static bool EsEscenaLab7(Scene escena)
        {
            return escena.IsValid() && !string.IsNullOrEmpty(escena.path) &&
                   Path.GetFileNameWithoutExtension(escena.path)
                       .StartsWith("Lab7", StringComparison.OrdinalIgnoreCase);
        }

        private static void PrepararEscena(Scene escena, bool guardar)
        {
            AsegurarCarpeta("Assets", "Prefabs");
            AsegurarCarpeta("Assets/Prefabs", "Lab7");
            AsegurarCarpeta("Assets", "Materials");
            AsegurarCarpeta("Assets/Materials", "Lab7");

            Material materialJugador = CrearMaterial(
                CarpetaMateriales + "/Jugador.mat",
                new Color(0.75f, 0.75f, 0.75f));
            Material materialTronco = CrearMaterial(
                CarpetaMateriales + "/Tronco.mat",
                new Color(0.42f, 0.19f, 0.06f));
            Material materialHojas = CrearMaterial(
                CarpetaMateriales + "/Hojas.mat",
                new Color(0.08f, 0.55f, 0.13f));
            Material materialSuelo = CrearMaterial(
                CarpetaMateriales + "/Suelo.mat",
                new Color(0.1f, 0.3f, 0.14f));

            GameObject jugador = CrearPrefabJugador(materialJugador);
            GameObject arbol = CrearPrefabArbol(materialTronco, materialHojas);
            RegistrarPrefabs(jugador, arbol);

            Transform[] puntos = ConfigurarEntorno(escena, materialSuelo);
            ConfigurarNetworkManager(escena, jugador, arbol, puntos);
            ConfigurarCamara(escena);
            AgregarEscenaAlBuild();

            EditorSceneManager.MarkSceneDirty(escena);
            if (guardar)
            {
                EditorSceneManager.SaveScene(escena);
                AssetDatabase.SaveAssets();
            }

            Debug.Log("Lab 7 preparado: NetworkList privada, bosque recolectable y pool de red configurados.");
        }

        private static GameObject CrearPrefabJugador(Material material)
        {
            GameObject existente = AssetDatabase.LoadAssetAtPath<GameObject>(RutaJugador);
            if (existente != null)
            {
                return existente;
            }

            GameObject temporal = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            temporal.name = "JugadorLab7";
            UnityEngine.Object.DestroyImmediate(temporal.GetComponent<Collider>());
            Renderer renderer = temporal.GetComponent<Renderer>();
            renderer.sharedMaterial = material;

            temporal.AddComponent<NetworkObject>();
            NetworkTransform red = temporal.AddComponent<NetworkTransform>();
            ConfigurarTransformJugador(red);

            CharacterController controlador = temporal.AddComponent<CharacterController>();
            controlador.height = 2f;
            controlador.radius = 0.45f;
            controlador.center = Vector3.zero;
            controlador.stepOffset = 0.25f;

            temporal.AddComponent<InventarioLab7>();
            JugadorLab7 jugador = temporal.AddComponent<JugadorLab7>();
            SerializedObject serializado = new(jugador);
            serializado.FindProperty("rendererJugador").objectReferenceValue = renderer;
            serializado.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporal, RutaJugador);
            UnityEngine.Object.DestroyImmediate(temporal);
            return prefab;
        }

        private static void ConfigurarTransformJugador(NetworkTransform red)
        {
            red.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            red.SyncPositionX = true;
            red.SyncPositionY = true;
            red.SyncPositionZ = true;
            red.SyncRotAngleX = false;
            red.SyncRotAngleY = true;
            red.SyncRotAngleZ = false;
            red.SyncScaleX = false;
            red.SyncScaleY = false;
            red.SyncScaleZ = false;
            red.Interpolate = true;
            red.PositionInterpolationType = NetworkTransform.InterpolationTypes.SmoothDampening;
            red.RotationInterpolationType = NetworkTransform.InterpolationTypes.Lerp;
            red.UseUnreliableDeltas = true;
            red.UseHalfFloatPrecision = true;
        }

        private static GameObject CrearPrefabArbol(Material materialTronco, Material materialHojas)
        {
            GameObject existente = AssetDatabase.LoadAssetAtPath<GameObject>(RutaArbol);
            if (existente != null)
            {
                return existente;
            }

            GameObject temporal = new("ArbolReciclableLab7");
            temporal.AddComponent<NetworkObject>();
            NetworkTransform red = temporal.AddComponent<NetworkTransform>();
            red.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            red.SyncPositionX = true;
            red.SyncPositionY = true;
            red.SyncPositionZ = true;
            red.SyncRotAngleX = false;
            red.SyncRotAngleY = true;
            red.SyncRotAngleZ = false;
            red.SyncScaleX = false;
            red.SyncScaleY = false;
            red.SyncScaleZ = false;
            red.Interpolate = false;
            red.UseUnreliableDeltas = false;
            red.UseHalfFloatPrecision = true;

            Rigidbody cuerpo = temporal.AddComponent<Rigidbody>();
            cuerpo.isKinematic = true;
            cuerpo.useGravity = false;
            CapsuleCollider colision = temporal.AddComponent<CapsuleCollider>();
            colision.center = new Vector3(0f, 1.7f, 0f);
            colision.height = 4.2f;
            colision.radius = 0.85f;

            GameObject tronco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tronco.name = "Tronco";
            tronco.transform.SetParent(temporal.transform, false);
            tronco.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            tronco.transform.localScale = new Vector3(0.55f, 1.25f, 0.55f);
            Renderer rendererTronco = tronco.GetComponent<Renderer>();
            rendererTronco.sharedMaterial = materialTronco;
            UnityEngine.Object.DestroyImmediate(tronco.GetComponent<Collider>());

            GameObject hojas = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hojas.name = "Hojas";
            hojas.transform.SetParent(temporal.transform, false);
            hojas.transform.localPosition = new Vector3(0f, 3.15f, 0f);
            hojas.transform.localScale = new Vector3(2.4f, 2.1f, 2.4f);
            Renderer rendererHojas = hojas.GetComponent<Renderer>();
            rendererHojas.sharedMaterial = materialHojas;
            UnityEngine.Object.DestroyImmediate(hojas.GetComponent<Collider>());

            GameObject etiqueta = new("TextoEstado");
            etiqueta.transform.SetParent(temporal.transform, false);
            etiqueta.transform.localPosition = new Vector3(0f, 4.7f, 0f);
            TextMeshPro texto = etiqueta.AddComponent<TextMeshPro>();
            texto.text = "ARBOL 3/3\nF: GOLPEAR";
            texto.fontSize = 2.1f;
            texto.alignment = TextAlignmentOptions.Center;
            texto.rectTransform.sizeDelta = new Vector2(5f, 1.6f);
            etiqueta.AddComponent<TextoOrientadoACamara>();

            ArbolReciclableLab7 arbol = temporal.AddComponent<ArbolReciclableLab7>();
            SerializedObject serializado = new(arbol);
            serializado.FindProperty("rendererTronco").objectReferenceValue = rendererTronco;
            serializado.FindProperty("rendererHojas").objectReferenceValue = rendererHojas;
            serializado.FindProperty("textoEstado").objectReferenceValue = texto;
            serializado.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporal, RutaArbol);
            UnityEngine.Object.DestroyImmediate(temporal);
            return prefab;
        }

        private static void RegistrarPrefabs(params GameObject[] prefabs)
        {
            NetworkPrefabsList lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(RutaListaPrefabs);
            if (lista == null)
            {
                lista = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(lista, RutaListaPrefabs);
            }

            foreach (GameObject prefab in prefabs)
            {
                if (prefab != null && !lista.Contains(prefab))
                {
                    lista.Add(new NetworkPrefab { Prefab = prefab });
                }
            }

            EditorUtility.SetDirty(lista);
        }

        private static void ConfigurarNetworkManager(
            Scene escena,
            GameObject jugador,
            GameObject arbol,
            Transform[] puntos)
        {
            GameObject objeto = escena.GetRootGameObjects()
                .FirstOrDefault(go => go.name == "NetworkManager");
            if (objeto == null)
            {
                objeto = new GameObject("NetworkManager");
                SceneManager.MoveGameObjectToScene(objeto, escena);
            }

            GestorLab6 viejo = objeto.GetComponent<GestorLab6>();
            if (viejo != null)
            {
                UnityEngine.Object.DestroyImmediate(viejo, true);
            }

            UnityTransport transporte = objeto.GetComponent<UnityTransport>() ??
                                        objeto.AddComponent<UnityTransport>();
            NetworkManager manager = objeto.GetComponent<NetworkManager>() ??
                                     objeto.AddComponent<NetworkManager>();
            manager.NetworkConfig.NetworkTransport = transporte;
            manager.NetworkConfig.PlayerPrefab = jugador;
            manager.NetworkConfig.ConnectionApproval = false;

            NetworkPrefabsList lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(RutaListaPrefabs);
            if (lista != null && !manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(lista))
            {
                manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(lista);
            }

            if (objeto.GetComponent<InicioRedGUI>() == null)
            {
                objeto.AddComponent<InicioRedGUI>();
            }

            PoolDeRedLab7 pool = objeto.GetComponent<PoolDeRedLab7>() ??
                                 objeto.AddComponent<PoolDeRedLab7>();
            SerializedObject serialPool = new(pool);
            serialPool.FindProperty("prefabArbol").objectReferenceValue = arbol.GetComponent<NetworkObject>();
            serialPool.FindProperty("cantidadInicial").intValue = Mathf.Max(10, puntos.Length + 2);
            serialPool.ApplyModifiedPropertiesWithoutUndo();

            BosqueLab7 bosque = objeto.GetComponent<BosqueLab7>() ?? objeto.AddComponent<BosqueLab7>();
            SerializedObject serialBosque = new(bosque);
            serialBosque.FindProperty("pool").objectReferenceValue = pool;
            SerializedProperty listaPuntos = serialBosque.FindProperty("puntosAparicion");
            listaPuntos.arraySize = puntos.Length;
            for (int i = 0; i < puntos.Length; i++)
            {
                listaPuntos.GetArrayElementAtIndex(i).objectReferenceValue = puntos[i];
            }
            serialBosque.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(objeto);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(pool);
            EditorUtility.SetDirty(bosque);
        }

        private static Transform[] ConfigurarEntorno(Scene escena, Material materialSuelo)
        {
            foreach (CofreLab6 cofre in escena.GetRootGameObjects()
                         .SelectMany(raiz => raiz.GetComponentsInChildren<CofreLab6>(true))
                         .ToArray())
            {
                UnityEngine.Object.DestroyImmediate(cofre.gameObject, true);
            }

            GameObject instruccionesViejas = escena.GetRootGameObjects()
                .FirstOrDefault(go => go.name == "InstruccionesLab6");
            if (instruccionesViejas != null)
            {
                UnityEngine.Object.DestroyImmediate(instruccionesViejas, true);
            }

            GameObject suelo = escena.GetRootGameObjects().FirstOrDefault(go => go.name == "Ground");
            if (suelo == null)
            {
                suelo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(suelo, escena);
            }

            suelo.name = "Ground";
            suelo.transform.SetPositionAndRotation(new Vector3(0f, -0.35f, 0f), Quaternion.identity);
            suelo.transform.localScale = new Vector3(24f, 0.7f, 18f);
            if (suelo.TryGetComponent(out MeshRenderer rendererSuelo))
            {
                rendererSuelo.sharedMaterial = materialSuelo;
            }

            GameObject raizPuntos = escena.GetRootGameObjects()
                .FirstOrDefault(go => go.name == "PuntosBosqueLab7");
            if (raizPuntos == null)
            {
                raizPuntos = new GameObject("PuntosBosqueLab7");
                SceneManager.MoveGameObjectToScene(raizPuntos, escena);
            }

            while (raizPuntos.transform.childCount > 0)
            {
                UnityEngine.Object.DestroyImmediate(raizPuntos.transform.GetChild(0).gameObject);
            }

            Vector3[] posiciones =
            {
                new(-7f, 0f, -2.5f), new(-3f, 0f, -1f),
                new(1.5f, 0f, -2.5f), new(6.5f, 0f, -2f),
                new(-6.5f, 0f, 3f), new(-2f, 0f, 4.2f),
                new(3f, 0f, 3.4f), new(7f, 0f, 2.4f)
            };

            Transform[] puntos = new Transform[posiciones.Length];
            for (int i = 0; i < posiciones.Length; i++)
            {
                GameObject punto = new($"PuntoArbol_{i + 1:00}");
                punto.transform.SetParent(raizPuntos.transform, false);
                punto.transform.position = posiciones[i];
                puntos[i] = punto.transform;
            }

            GameObject instrucciones = escena.GetRootGameObjects()
                .FirstOrDefault(go => go.name == "InstruccionesLab7");
            if (instrucciones == null)
            {
                instrucciones = new GameObject("InstruccionesLab7");
                SceneManager.MoveGameObjectToScene(instrucciones, escena);
                TextMeshPro texto = instrucciones.AddComponent<TextMeshPro>();
                texto.text = "LAB 7 - BOSQUE RECICLADO\nF golpea | C consume tronco\nInventario privado por NetworkList";
                texto.fontSize = 2f;
                texto.alignment = TextAlignmentOptions.Center;
                texto.rectTransform.sizeDelta = new Vector2(11f, 3f);
                instrucciones.AddComponent<TextoOrientadoACamara>();
            }

            instrucciones.transform.position = new Vector3(0f, 0.8f, 7f);
            return puntos;
        }

        private static void ConfigurarCamara(Scene escena)
        {
            Camera camara = escena.GetRootGameObjects()
                .SelectMany(raiz => raiz.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (camara == null)
            {
                return;
            }

            camara.transform.SetPositionAndRotation(
                new Vector3(0f, 16f, -15f),
                Quaternion.Euler(44f, 0f, 0f));
            camara.fieldOfView = 62f;
            EditorUtility.SetDirty(camara);
        }

        private static Material CrearMaterial(string ruta, Color color)
        {
            Material existente = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (existente != null)
            {
                return existente;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new(shader) { color = color };
            AssetDatabase.CreateAsset(material, ruta);
            return material;
        }

        private static void AsegurarCarpeta(string padre, string nombre)
        {
            string ruta = padre + "/" + nombre;
            if (!AssetDatabase.IsValidFolder(ruta))
            {
                AssetDatabase.CreateFolder(padre, nombre);
            }
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
