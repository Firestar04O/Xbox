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
    public static class ConfiguradorLab6
    {
        private const string RutaEscena = "Assets/Scenes/Lab6.unity";
        private const string CarpetaPrefabs = "Assets/Prefabs/Lab6";
        private const string CarpetaMateriales = "Assets/Materials/Lab6";
        private const string RutaGuardian = CarpetaPrefabs + "/GuardianLab6.prefab";
        private const string RutaExplorador = CarpetaPrefabs + "/ExploradorLab6.prefab";
        private const string RutaCofre = CarpetaPrefabs + "/CofreLab6.prefab";
        private const string RutaCubo = CarpetaPrefabs + "/CuboBotinLab6.prefab";
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

        [MenuItem("Tools/Bosque compartido/Preparar Lab 6")]
        public static void PrepararDesdeMenu()
        {
            Scene escena = SceneManager.GetActiveScene();
            if (!EsEscenaLab6(escena))
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
            if (EsEscenaLab6(activa))
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

        private static bool EsEscenaLab6(Scene escena)
        {
            if (!escena.IsValid() || string.IsNullOrEmpty(escena.path))
            {
                return false;
            }

            string nombre = Path.GetFileNameWithoutExtension(escena.path);
            return nombre.StartsWith("Lab6", StringComparison.OrdinalIgnoreCase);
        }

        private static void PrepararEscena(Scene escena, bool guardar)
        {
            AsegurarCarpeta("Assets", "Prefabs");
            AsegurarCarpeta("Assets/Prefabs", "Lab6");
            AsegurarCarpeta("Assets", "Materials");
            AsegurarCarpeta("Assets/Materials", "Lab6");

            Material materialJugador = CrearMaterial(
                CarpetaMateriales + "/JugadorBase.mat",
                new Color(0.75f, 0.75f, 0.75f));
            Material materialCubo = CrearMaterial(
                CarpetaMateriales + "/CuboBotin.mat",
                new Color(0.12f, 0.72f, 0.95f));
            Material materialCofre = CrearMaterial(
                CarpetaMateriales + "/CofreGuardian.mat",
                new Color(0.55f, 0.24f, 0.06f));
            Material materialSuelo = CrearMaterial(
                CarpetaMateriales + "/Suelo.mat",
                new Color(0.12f, 0.32f, 0.18f));

            GameObject guardian = CrearPrefabJugador(
                RutaGuardian,
                "GuardianLab6",
                RolJugadorLab6.Guardian,
                materialJugador);
            GameObject explorador = CrearPrefabJugador(
                RutaExplorador,
                "ExploradorLab6",
                RolJugadorLab6.Explorador,
                materialJugador);
            GameObject cubo = CrearPrefabCubo(materialCubo);
            GameObject cofre = CrearPrefabCofre(materialCofre, cubo.GetComponent<CuboBotinLab6>());

            RegistrarPrefabs(guardian, explorador, cofre, cubo);
            ConfigurarNetworkManager(escena, guardian, explorador, cofre);
            ConfigurarEntorno(escena, materialSuelo);
            ConfigurarCamara(escena);
            AgregarEscenaAlBuild();

            EditorSceneManager.MarkSceneDirty(escena);
            if (guardar)
            {
                EditorSceneManager.SaveScene(escena);
                AssetDatabase.SaveAssets();
            }

            Debug.Log("Lab 6 preparado: roles, ConnectionApproval, spawn, ownership, regalo y consumo configurados.");
        }

        private static GameObject CrearPrefabJugador(
            string ruta,
            string nombre,
            RolJugadorLab6 rol,
            Material material)
        {
            GameObject existente = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (existente != null)
            {
                return existente;
            }

            GameObject temporal = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            temporal.name = nombre;
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

            JugadorLab6 jugador = temporal.AddComponent<JugadorLab6>();
            SerializedObject serializado = new(jugador);
            serializado.FindProperty("rol").enumValueIndex = (int)rol;
            serializado.FindProperty("rendererJugador").objectReferenceValue = renderer;
            serializado.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporal, ruta);
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

        private static GameObject CrearPrefabCubo(Material material)
        {
            GameObject existente = AssetDatabase.LoadAssetAtPath<GameObject>(RutaCubo);
            if (existente != null)
            {
                NetworkObject objetoExistente = existente.GetComponent<NetworkObject>();
                if (objetoExistente != null)
                {
                    objetoExistente.DontDestroyWithOwner = true;
                    EditorUtility.SetDirty(objetoExistente);
                }

                NetworkTransform transformExistente = existente.GetComponent<NetworkTransform>();
                if (transformExistente != null)
                {
                    ConfigurarTransformCubo(transformExistente);
                    EditorUtility.SetDirty(transformExistente);
                }

                return existente;
            }

            GameObject temporal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            temporal.name = "CuboBotinLab6";
            temporal.transform.localScale = Vector3.one * 0.72f;
            Renderer renderer = temporal.GetComponent<Renderer>();
            renderer.sharedMaterial = material;

            Rigidbody cuerpo = temporal.AddComponent<Rigidbody>();
            cuerpo.mass = 0.8f;
            cuerpo.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            NetworkObject objetoRed = temporal.AddComponent<NetworkObject>();
            objetoRed.DontDestroyWithOwner = true;
            NetworkTransform red = temporal.AddComponent<NetworkTransform>();
            ConfigurarTransformCubo(red);

            GameObject etiqueta = new("EtiquetaDueno");
            etiqueta.transform.SetParent(temporal.transform, false);
            etiqueta.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            TextMeshPro texto = etiqueta.AddComponent<TextMeshPro>();
            texto.text = "SIN DUENO";
            texto.fontSize = 2.4f;
            texto.alignment = TextAlignmentOptions.Center;
            texto.rectTransform.sizeDelta = new Vector2(4f, 1f);
            etiqueta.AddComponent<TextoOrientadoACamara>();

            CuboBotinLab6 cubo = temporal.AddComponent<CuboBotinLab6>();
            SerializedObject serializado = new(cubo);
            serializado.FindProperty("rendererCubo").objectReferenceValue = renderer;
            serializado.FindProperty("etiquetaDueno").objectReferenceValue = texto;
            serializado.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporal, RutaCubo);
            UnityEngine.Object.DestroyImmediate(temporal);
            return prefab;
        }

        private static void ConfigurarTransformCubo(NetworkTransform red)
        {
            red.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            red.SyncPositionX = true;
            red.SyncPositionY = true;
            red.SyncPositionZ = true;
            red.SyncRotAngleX = true;
            red.SyncRotAngleY = true;
            red.SyncRotAngleZ = true;
            red.SyncScaleX = false;
            red.SyncScaleY = false;
            red.SyncScaleZ = false;
            red.Interpolate = true;
            red.PositionInterpolationType = NetworkTransform.InterpolationTypes.SmoothDampening;
            red.RotationInterpolationType = NetworkTransform.InterpolationTypes.Lerp;
            // El cambio automatico entre espacio global/local no es compatible
            // con deltas no fiables. Para parenting estable priorizamos el modo local.
            red.UseUnreliableDeltas = false;
            red.UseHalfFloatPrecision = true;
            red.SwitchTransformSpaceWhenParented = true;
            red.TickSyncChildren = true;
        }

        private static GameObject CrearPrefabCofre(Material material, CuboBotinLab6 prefabCubo)
        {
            GameObject existente = AssetDatabase.LoadAssetAtPath<GameObject>(RutaCofre);
            if (existente != null)
            {
                return existente;
            }

            GameObject temporal = new("CofreLab6");
            temporal.AddComponent<NetworkObject>();
            BoxCollider colision = temporal.AddComponent<BoxCollider>();
            colision.center = new Vector3(0f, 0.55f, 0f);
            colision.size = new Vector3(2.4f, 1.2f, 1.6f);

            GameObject baseVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseVisual.name = "Base";
            baseVisual.transform.SetParent(temporal.transform, false);
            baseVisual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            baseVisual.transform.localScale = new Vector3(2.4f, 1f, 1.5f);
            baseVisual.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(baseVisual.GetComponent<Collider>());

            GameObject bisagra = new("BisagraTapa");
            bisagra.transform.SetParent(temporal.transform, false);
            bisagra.transform.localPosition = new Vector3(0f, 1.05f, -0.7f);

            GameObject tapa = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tapa.name = "Tapa";
            tapa.transform.SetParent(bisagra.transform, false);
            tapa.transform.localPosition = new Vector3(0f, 0f, 0.7f);
            tapa.transform.localScale = new Vector3(2.5f, 0.25f, 1.6f);
            tapa.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(tapa.GetComponent<Collider>());

            GameObject etiqueta = new("TextoEstado");
            etiqueta.transform.SetParent(temporal.transform, false);
            etiqueta.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            TextMeshPro texto = etiqueta.AddComponent<TextMeshPro>();
            texto.text = "COFRE LAB 6\nMANTENER F";
            texto.fontSize = 2.6f;
            texto.alignment = TextAlignmentOptions.Center;
            texto.rectTransform.sizeDelta = new Vector2(5f, 2f);
            etiqueta.AddComponent<TextoOrientadoACamara>();

            CofreLab6 cofre = temporal.AddComponent<CofreLab6>();
            SerializedObject serializado = new(cofre);
            serializado.FindProperty("cuboPrefab").objectReferenceValue = prefabCubo;
            serializado.FindProperty("bisagraTapa").objectReferenceValue = bisagra.transform;
            serializado.FindProperty("textoEstado").objectReferenceValue = texto;
            serializado.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporal, RutaCofre);
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
            GameObject guardian,
            GameObject explorador,
            GameObject cofre)
        {
            GameObject objeto = escena.GetRootGameObjects()
                .FirstOrDefault(go => go.name == "NetworkManager");
            if (objeto == null)
            {
                objeto = new GameObject("NetworkManager");
                SceneManager.MoveGameObjectToScene(objeto, escena);
            }

            UnityTransport transporte = objeto.GetComponent<UnityTransport>() ??
                                        objeto.AddComponent<UnityTransport>();
            NetworkManager manager = objeto.GetComponent<NetworkManager>() ??
                                     objeto.AddComponent<NetworkManager>();
            manager.NetworkConfig.NetworkTransport = transporte;
            manager.NetworkConfig.PlayerPrefab = guardian;
            manager.NetworkConfig.ConnectionApproval = true;

            NetworkPrefabsList lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(RutaListaPrefabs);
            if (lista != null && !manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(lista))
            {
                manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(lista);
            }

            if (objeto.GetComponent<InicioRedGUI>() == null)
            {
                objeto.AddComponent<InicioRedGUI>();
            }

            GestorLab6 gestor = objeto.GetComponent<GestorLab6>() ?? objeto.AddComponent<GestorLab6>();
            SerializedObject serializado = new(gestor);
            serializado.FindProperty("prefabGuardian").objectReferenceValue = guardian.GetComponent<NetworkObject>();
            serializado.FindProperty("prefabExplorador").objectReferenceValue = explorador.GetComponent<NetworkObject>();
            serializado.FindProperty("prefabCofre").objectReferenceValue = cofre.GetComponent<NetworkObject>();
            serializado.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(objeto);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(gestor);
        }

        private static void ConfigurarEntorno(Scene escena, Material materialSuelo)
        {
            foreach (CofreLab5 viejo in escena.GetRootGameObjects()
                         .SelectMany(raiz => raiz.GetComponentsInChildren<CofreLab5>(true))
                         .ToArray())
            {
                UnityEngine.Object.DestroyImmediate(viejo.gameObject, true);
            }

            foreach (CofreLab6 cofreEnEscena in escena.GetRootGameObjects()
                         .SelectMany(raiz => raiz.GetComponentsInChildren<CofreLab6>(true))
                         .ToArray())
            {
                // El cofre de Lab 6 debe nacer por código, nunca estar colocado.
                UnityEngine.Object.DestroyImmediate(cofreEnEscena.gameObject, true);
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
            MeshRenderer renderer = suelo.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = materialSuelo;
            }

            GameObject instrucciones = escena.GetRootGameObjects()
                .FirstOrDefault(go => go.name == "InstruccionesLab6");
            if (instrucciones == null)
            {
                instrucciones = new GameObject("InstruccionesLab6");
                SceneManager.MoveGameObjectToScene(instrucciones, escena);
                TextMeshPro texto = instrucciones.AddComponent<TextMeshPro>();
                texto.text = "LAB 6\nGuardian: R crea el cofre\nTodos: mantener F para abrir";
                texto.fontSize = 2.2f;
                texto.alignment = TextAlignmentOptions.Center;
                texto.rectTransform.sizeDelta = new Vector2(10f, 3f);
                instrucciones.AddComponent<TextoOrientadoACamara>();
            }

            instrucciones.transform.position = new Vector3(0f, 0.8f, 6.8f);
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
                new Vector3(0f, 15f, -14f),
                Quaternion.Euler(43f, 0f, 0f));
            camara.fieldOfView = 60f;
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
