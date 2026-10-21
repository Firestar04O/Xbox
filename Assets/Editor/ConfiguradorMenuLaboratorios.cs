using System.Collections.Generic;
using System.IO;
using BosqueCompartido;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BosqueCompartidoEditor
{
    [InitializeOnLoad]
    public static class ConfiguradorMenuLaboratorios
    {
        private const string RutaMenu = "Assets/Scenes/Menu.unity";

        static ConfiguradorMenuLaboratorios()
        {
            EditorApplication.delayCall += PrepararProyecto;
        }

        [MenuItem("Tools/Bosque Compartido/Crear o actualizar menu de laboratorios")]
        public static void PrepararProyecto()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            {
                return;
            }

            CrearOActualizarEscena();
            ConfigurarBuildSettings();
        }

        private static void CrearOActualizarEscena()
        {
            Scene escenaActiva = SceneManager.GetActiveScene();
            Scene escenaMenu;
            bool cerrarAlTerminar = false;

            if (escenaActiva.path == RutaMenu)
            {
                escenaMenu = escenaActiva;
            }
            else if (File.Exists(RutaMenu))
            {
                escenaMenu = EditorSceneManager.OpenScene(RutaMenu, OpenSceneMode.Additive);
                cerrarAlTerminar = true;
            }
            else
            {
                escenaMenu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                cerrarAlTerminar = true;
            }

            foreach (GameObject raiz in escenaMenu.GetRootGameObjects())
            {
                if (raiz.name == "MenuLaboratorios")
                {
                    Object.DestroyImmediate(raiz);
                }
            }

            GameObject raizMenu = new GameObject("MenuLaboratorios");
            SceneManager.MoveGameObjectToScene(raizMenu, escenaMenu);
            MenuLaboratorios controlador = raizMenu.AddComponent<MenuLaboratorios>();

            CrearCamara(raizMenu.transform);
            CrearEventSystem(raizMenu.transform);
            CrearCanvas(raizMenu.transform, controlador);

            EditorSceneManager.SaveScene(escenaMenu, RutaMenu);

            if (cerrarAlTerminar)
            {
                EditorSceneManager.CloseScene(escenaMenu, true);
            }
        }

        private static void CrearCamara(Transform padre)
        {
            GameObject objeto = new GameObject("Main Camera");
            objeto.transform.SetParent(padre);
            objeto.tag = "MainCamera";
            Camera camara = objeto.AddComponent<Camera>();
            camara.clearFlags = CameraClearFlags.SolidColor;
            camara.backgroundColor = new Color(0.025f, 0.055f, 0.08f);
            objeto.AddComponent<AudioListener>();
        }

        private static void CrearEventSystem(Transform padre)
        {
            GameObject objeto = new GameObject("EventSystem");
            objeto.transform.SetParent(padre);
            objeto.AddComponent<EventSystem>();
            objeto.AddComponent<InputSystemUIInputModule>();
        }

        private static void CrearCanvas(Transform padre, MenuLaboratorios controlador)
        {
            GameObject objetoCanvas = new GameObject("Canvas", typeof(RectTransform));
            objetoCanvas.transform.SetParent(padre);
            Canvas canvas = objetoCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = objetoCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            objetoCanvas.AddComponent<GraphicRaycaster>();

            Image fondo = CrearImagen(objetoCanvas.transform, "Fondo", new Color(0.025f, 0.055f, 0.08f, 1f));
            Estirar(fondo.rectTransform);

            Image panel = CrearImagen(objetoCanvas.transform, "Panel", new Color(0.06f, 0.12f, 0.16f, 0.97f));
            panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            panel.rectTransform.sizeDelta = new Vector2(900f, 930f);

            CrearTexto(panel.transform, "Titulo", "BOSQUE COMPARTIDO", 54, new Vector2(0f, 395f), new Vector2(800f, 75f), FontStyle.Bold);
            CrearTexto(panel.transform, "Subtitulo", "Selecciona el laboratorio que quieres probar", 25, new Vector2(0f, 335f), new Vector2(800f, 45f), FontStyle.Normal);

            Button lab3 = CrearBoton(panel.transform, "Lab3", "LAB 3  -  Estado replicado de los arboles", new Vector2(0f, 230f));
            Button lab4 = CrearBoton(panel.transform, "Lab4", "LAB 4  -  Vida y combate con autoridad", new Vector2(0f, 135f));
            Button lab5 = CrearBoton(panel.transform, "Lab5", "LAB 5  -  Movimiento, cofres y cubos", new Vector2(0f, 40f));
            Button lab6 = CrearBoton(panel.transform, "Lab6", "LAB 6  -  Roles, aparicion y ownership", new Vector2(0f, -55f));
            Button lab7 = CrearBoton(panel.transform, "Lab7", "LAB 7  -  Inventario y object pooling", new Vector2(0f, -150f));
            Button salir = CrearBoton(panel.transform, "Salir", "SALIR", new Vector2(0f, -275f), new Vector2(280f, 62f));

            UnityEventTools.AddPersistentListener(lab3.onClick, controlador.AbrirLab3);
            UnityEventTools.AddPersistentListener(lab4.onClick, controlador.AbrirLab4);
            UnityEventTools.AddPersistentListener(lab5.onClick, controlador.AbrirLab5);
            UnityEventTools.AddPersistentListener(lab6.onClick, controlador.AbrirLab6);
            UnityEventTools.AddPersistentListener(lab7.onClick, controlador.AbrirLab7);
            UnityEventTools.AddPersistentListener(salir.onClick, controlador.Salir);

            CrearTexto(panel.transform, "Ayuda", "Dentro de cada lab podras iniciar Host o Cliente desde la esquina superior izquierda.", 20, new Vector2(0f, -385f), new Vector2(800f, 38f), FontStyle.Italic);
        }

        private static Image CrearImagen(Transform padre, string nombre, Color color)
        {
            GameObject objeto = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            objeto.transform.SetParent(padre, false);
            Image imagen = objeto.GetComponent<Image>();
            imagen.color = color;
            return imagen;
        }

        private static Text CrearTexto(Transform padre, string nombre, string contenido, int tamano, Vector2 posicion, Vector2 medidas, FontStyle estilo)
        {
            GameObject objeto = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            objeto.transform.SetParent(padre, false);
            RectTransform rect = objeto.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = posicion;
            rect.sizeDelta = medidas;

            Text texto = objeto.GetComponent<Text>();
            texto.text = contenido;
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.fontSize = tamano;
            texto.fontStyle = estilo;
            texto.alignment = TextAnchor.MiddleCenter;
            texto.color = new Color(0.91f, 0.96f, 0.94f);
            return texto;
        }

        private static Button CrearBoton(Transform padre, string nombre, string etiqueta, Vector2 posicion, Vector2? medidas = null)
        {
            Image imagen = CrearImagen(padre, nombre, new Color(0.10f, 0.38f, 0.31f, 1f));
            RectTransform rect = imagen.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = posicion;
            rect.sizeDelta = medidas ?? new Vector2(720f, 78f);

            Button boton = imagen.gameObject.AddComponent<Button>();
            ColorBlock colores = boton.colors;
            colores.normalColor = Color.white;
            colores.highlightedColor = new Color(0.78f, 1f, 0.9f);
            colores.pressedColor = new Color(0.58f, 0.82f, 0.68f);
            colores.selectedColor = colores.highlightedColor;
            boton.colors = colores;

            Text texto = CrearTexto(imagen.transform, "Texto", etiqueta, 24, Vector2.zero, rect.sizeDelta, FontStyle.Bold);
            Estirar(texto.rectTransform);
            return boton;
        }

        private static void Estirar(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ConfigurarBuildSettings()
        {
            string[] rutas =
            {
                RutaMenu,
                "Assets/Scenes/Lab3.unity",
                "Assets/Scenes/Lab4.unity",
                "Assets/Scenes/Lab5.unity",
                "Assets/Scenes/Lab6.unity",
                "Assets/Scenes/Lab7.unity"
            };

            List<EditorBuildSettingsScene> escenas = new List<EditorBuildSettingsScene>();
            foreach (string ruta in rutas)
            {
                if (File.Exists(ruta))
                {
                    escenas.Add(new EditorBuildSettingsScene(ruta, true));
                }
            }

            EditorBuildSettings.scenes = escenas.ToArray();
        }
    }
}
