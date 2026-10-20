using System.Linq;
using System.IO;
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
    public static class ConfiguradorLab5
    {
        private const string RutaEscena = "Assets/Scenes/Lab5.unity";
        private const string CarpetaPrefabs = "Assets/Prefabs";
        private const string CarpetaMateriales = "Assets/Materials/Lab5";
        private const string RutaJugador = CarpetaPrefabs + "/JugadorLab5.prefab";
        private const string RutaCubo = CarpetaPrefabs + "/CuboAgarrableLab5.prefab";
        private const string RutaJugadorPreferida = CarpetaPrefabs + "/Player.prefab";
        private const string RutaCuboPreferida = CarpetaPrefabs + "/Cubito.prefab";
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

        [MenuItem("Tools/Bosque compartido/Preparar Lab 5")]
        public static void PrepararDesdeMenu()
        {
            Scene escena = SceneManager.GetActiveScene();
            if (!EsEscenaLab5(escena))
            {
                escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
            }

            PrepararEscena(escena, true);
        }

        private static void PrepararEnSegundoPlano()
        {
            Scene activa = SceneManager.GetActiveScene();

            // También acepta copias como "Lab5 1". Esto evita modificar otra
            // escena distinta de la que el usuario está editando.
            if (EsEscenaLab5(activa))
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

        private static bool EsEscenaLab5(Scene escena)
        {
            if (!escena.IsValid() || string.IsNullOrEmpty(escena.path))
            {
                return false;
            }

            string nombre = Path.GetFileNameWithoutExtension(escena.path);
            return nombre.StartsWith("Lab5", System.StringComparison.OrdinalIgnoreCase);
        }

        private static void PrepararEscena(Scene escena, bool guardar)
        {
            AsegurarCarpeta("Assets", "Prefabs");
            AsegurarCarpeta("Assets", "Materials");
            AsegurarCarpeta("Assets/Materials", "Lab5");

            Material materialJugador = CrearMaterial(
                CarpetaMateriales + "/Jugador.mat",
                new Color(0.9f, 0.18f, 0.14f));
            Material materialCubo = CrearMaterial(
                CarpetaMateriales + "/Cubo.mat",
                new Color(0.12f, 0.72f, 0.95f));
            Material materialSuelo = CrearMaterial(
                CarpetaMateriales + "/Suelo.mat",
                new Color(0.16f, 0.4f, 0.2f));
            Material materialCofre = CrearMaterial(
                CarpetaMateriales + "/Cofre.mat",
                new Color(0.5f, 0.22f, 0.06f));

            GameObject prefabJugador = CrearPrefabJugador(materialJugador);
            GameObject prefabCubo = CrearPrefabCubo(materialCubo);
            RegistrarPrefabs(prefabJugador, prefabCubo);
            ConfigurarNetworkManager(escena, prefabJugador);
            ConfigurarEntorno(escena, prefabCubo.GetComponent<CuboAgarrable>(), materialSuelo, materialCofre);
            ConfigurarCamara(escena);
            AgregarEscenaAlBuild();

            EditorSceneManager.MarkSceneDirty(escena);
            if (guardar)
            {
                EditorSceneManager.SaveScene(escena);
                AssetDatabase.SaveAssets();
            }

            Debug.Log("Lab 5 preparado: movimiento Owner, cofre replicado, cubos agarrables y teleport configurados.");
        }

        private static GameObject CrearPrefabJugador(Material material)
        {
            GameObject existente = AssetDatabase.LoadAssetAtPath<GameObject>(RutaJugadorPreferida) ??
                                  AssetDatabase.LoadAssetAtPath<GameObject>(RutaJugador);
            if (existente != null)
            {
                return existente;
            }

            GameObject temporal = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            temporal.name = "JugadorLab5";
            Object.DestroyImmediate(temporal.GetComponent<Collider>());
            temporal.GetComponent<Renderer>().sharedMaterial = material;

            temporal.AddComponent<NetworkObject>();
            NetworkTransform red = temporal.AddComponent<NetworkTransform>();
            ConfigurarTransformJugador(red);

            CharacterController controlador = temporal.AddComponent<CharacterController>();
            controlador.height = 2f;
            controlador.radius = 0.45f;
            controlador.center = Vector3.zero;
            controlador.stepOffset = 0.25f;

            JugadorLab5 jugador = temporal.AddComponent<JugadorLab5>();
            SerializedObject serializado = new(jugador);
            serializado.FindProperty("rendererJugador").objectReferenceValue = temporal.GetComponent<Renderer>();
            serializado.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporal, RutaJugador);
            Object.DestroyImmediate(temporal);
            return prefab;
        }

        private static void ConfigurarTransformJugador(NetworkTransform red)
        {
            red.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            red.SyncPositionX = true;
            red.SyncPositionY = true; // Se usa para detectar caidas y reaparecer.
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
            red.SwitchTransformSpaceWhenParented = false;
            red.TickSyncChildren = true;
        }

        private static GameObject CrearPrefabCubo(Material material)
        {
            GameObject existente = AssetDatabase.LoadAssetAtPath<GameObject>(RutaCuboPreferida) ??
                                  AssetDatabase.LoadAssetAtPath<GameObject>(RutaCubo);
            if (existente != null)
            {
                return existente;
            }

            GameObject temporal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            temporal.name = "CuboAgarrableLab5";
            temporal.transform.localScale = Vector3.one * 0.7f;
            temporal.GetComponent<Renderer>().sharedMaterial = material;

            Rigidbody cuerpo = temporal.AddComponent<Rigidbody>();
            cuerpo.mass = 0.8f;
            cuerpo.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            temporal.AddComponent<NetworkObject>();
            NetworkTransform red = temporal.AddComponent<NetworkTransform>();
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
            red.UseHalfFloatPrecision = true;
            red.UseUnreliableDeltas = false;
            red.SwitchTransformSpaceWhenParented = true;
            temporal.AddComponent<CuboAgarrable>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporal, RutaCubo);
            Object.DestroyImmediate(temporal);
            return prefab;
        }

        private static void RegistrarPrefabs(GameObject jugador, GameObject cubo)
        {
            NetworkPrefabsList lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(RutaListaPrefabs);
            if (lista == null)
            {
                lista = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(lista, RutaListaPrefabs);
            }

            if (!lista.Contains(jugador))
            {
                lista.Add(new NetworkPrefab { Prefab = jugador });
            }

            if (!lista.Contains(cubo))
            {
                lista.Add(new NetworkPrefab { Prefab = cubo });
            }

            EditorUtility.SetDirty(lista);
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

            UnityTransport transporte = objeto.GetComponent<UnityTransport>() ??
                                        objeto.AddComponent<UnityTransport>();
            NetworkManager manager = objeto.GetComponent<NetworkManager>() ??
                                     objeto.AddComponent<NetworkManager>();
            manager.NetworkConfig.NetworkTransport = transporte;
            manager.NetworkConfig.PlayerPrefab = prefabJugador;

            NetworkPrefabsList lista = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(RutaListaPrefabs);
            if (lista != null && !manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(lista))
            {
                manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(lista);
            }

            if (objeto.GetComponent<InicioRedGUI>() == null)
            {
                objeto.AddComponent<InicioRedGUI>();
            }

            EditorUtility.SetDirty(objeto);
            EditorUtility.SetDirty(manager);
        }

        private static void ConfigurarEntorno(
            Scene escena,
            CuboAgarrable prefabCubo,
            Material materialSuelo,
            Material materialCofre)
        {
            GameObject[] plataformas = escena.GetRootGameObjects()
                .SelectMany(raiz => raiz.GetComponentsInChildren<MeshRenderer>(true))
                .Where(renderer =>
                    renderer.sharedMaterial == materialSuelo ||
                    EsPlataformaGrande(renderer.transform))
                .Select(renderer => renderer.gameObject)
                .Distinct()
                .ToArray();

            GameObject suelo = plataformas
                .OrderBy(objeto => objeto.transform.root.name == "Ground" ? 0 : 1)
                .FirstOrDefault();

            if (suelo == null)
            {
                suelo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(suelo, escena);
            }

            suelo.transform.SetParent(null, true);
            suelo.name = "Ground";
            suelo.transform.SetPositionAndRotation(new Vector3(0f, -0.35f, 0f), Quaternion.identity);
            suelo.transform.localScale = new Vector3(24f, 0.7f, 18f);
            suelo.GetComponent<MeshRenderer>().sharedMaterial = materialSuelo;

            foreach (GameObject duplicado in plataformas)
            {
                if (duplicado != suelo)
                {
                    Object.DestroyImmediate(duplicado, true);
                }
            }

            // Retira los contenedores vacíos de versiones anteriores. El suelo
            // queda como un único objeto raíz llamado simplemente Ground.
            foreach (GameObject raiz in escena.GetRootGameObjects().ToArray())
            {
                if (raiz != suelo &&
                    (raiz.name == "Lab5_Entorno" || raiz.name == "Ground") &&
                    raiz.transform.childCount == 0 &&
                    raiz.GetComponents<Component>().Length == 1)
                {
                    Object.DestroyImmediate(raiz, true);
                }
            }

            CofreLab5[] cofres = escena.GetRootGameObjects()
                .SelectMany(raiz => raiz.GetComponentsInChildren<CofreLab5>(true))
                .ToArray();
            CofreLab5 cofre = cofres
                .OrderBy(comportamiento => comportamiento.name == "Cofre" ? 0 : 1)
                .FirstOrDefault();

            if (cofre == null)
            {
                cofre = CrearCofre(escena, materialCofre).GetComponent<CofreLab5>();
            }

            foreach (CofreLab5 duplicado in cofres)
            {
                if (duplicado != cofre)
                {
                    Object.DestroyImmediate(duplicado.gameObject, true);
                }
            }

            GameObject cofreObjeto = cofre.gameObject;
            cofreObjeto.name = "Cofre";
            SerializedObject serializado = new(cofre);
            serializado.FindProperty("cuboPrefab").objectReferenceValue = prefabCubo;
            serializado.FindProperty("duracionApertura").floatValue = 3f;
            serializado.FindProperty("distanciaMaxima").floatValue = 3.5f;
            Transform bisagra = cofreObjeto.transform.Find("Bisagra") ??
                                cofreObjeto.transform.Find("BisagraTapa");
            if (bisagra != null)
            {
                serializado.FindProperty("bisagraTapa").objectReferenceValue = bisagra;
            }
            serializado.FindProperty("textoEstado").objectReferenceValue =
                cofreObjeto.GetComponentInChildren<TMP_Text>(true);
            serializado.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cofreObjeto);
            EditorUtility.SetDirty(cofre);
        }

        private static bool EsPlataformaGrande(Transform transformacion)
        {
            Vector3 escala = transformacion.localScale;
            return Mathf.Abs(escala.x - 24f) < 0.01f &&
                   Mathf.Abs(escala.y - 0.7f) < 0.01f &&
                   Mathf.Abs(escala.z - 18f) < 0.01f;
        }

        private static GameObject CrearCofre(Scene escena, Material material)
        {
            GameObject cofre = new("Cofre");
            SceneManager.MoveGameObjectToScene(cofre, escena);
            cofre.transform.position = new Vector3(0f, 0f, 4f);
            cofre.AddComponent<NetworkObject>();
            cofre.AddComponent<CofreLab5>();

            BoxCollider colision = cofre.AddComponent<BoxCollider>();
            colision.center = new Vector3(0f, 0.55f, 0f);
            colision.size = new Vector3(2.4f, 1.2f, 1.6f);

            GameObject baseVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseVisual.name = "Base";
            baseVisual.transform.SetParent(cofre.transform, false);
            baseVisual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            baseVisual.transform.localScale = new Vector3(2.4f, 1f, 1.5f);
            baseVisual.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(baseVisual.GetComponent<Collider>());

            GameObject bisagra = new("BisagraTapa");
            bisagra.transform.SetParent(cofre.transform, false);
            bisagra.transform.localPosition = new Vector3(0f, 1.05f, -0.7f);

            GameObject tapa = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tapa.name = "Tapa";
            tapa.transform.SetParent(bisagra.transform, false);
            tapa.transform.localPosition = new Vector3(0f, 0f, 0.7f);
            tapa.transform.localScale = new Vector3(2.5f, 0.25f, 1.6f);
            tapa.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(tapa.GetComponent<Collider>());

            GameObject etiqueta = new("TextoEstado");
            etiqueta.transform.SetParent(cofre.transform, false);
            etiqueta.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            etiqueta.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
            TextMeshPro texto = etiqueta.AddComponent<TextMeshPro>();
            texto.text = "COFRE\nF para abrir";
            texto.fontSize = 3f;
            texto.alignment = TextAlignmentOptions.Center;
            texto.rectTransform.sizeDelta = new Vector2(5f, 2f);

            return cofre;
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
