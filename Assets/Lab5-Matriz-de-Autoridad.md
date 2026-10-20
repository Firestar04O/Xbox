# Laboratorio 5 - Matriz de autoridad

| Elemento | Decide / escribe | Replica | Visualiza | Configuracion clave |
|---|---|---|---|---|
| Movimiento del avatar | Cliente dueño | `NetworkTransform` | Todos | Authority `Owner`, X/Y/Z, rotación Y, `SmoothDampening`, unreliable deltas y half float |
| Estado del cofre | Servidor | `NetworkVariable<EstadoCofre>`, `NetworkVariable<ulong>` y hora inicial | Todos | Reserva exclusiva durante la carga; el porcentaje se deriva localmente y sincroniza a jugadores tardíos |
| Creación de cubos | Servidor | `NetworkObject.Spawn()` | Todos | El prefab está registrado en `DefaultNetworkPrefabs` |
| Física del cubo libre | Servidor | `NetworkTransform` | Todos | Authority `Server` y half float |
| Recoger / soltar / lanzar | Cliente pide; servidor valida | RPC de intención + parenting | Todos | `TrySetParent`/`TryRemoveParent` solamente en servidor |
| Cubo sostenido | Servidor | Parenting + `NetworkVariable<ulong>` | Todos | `Switch Transform Space When Parented`; unreliable deltas desactivados por incompatibilidad de NGO |
| Cubo fuera del mapa | Servidor | `NetworkTransform.Teleport` | Todos | Solo la autoridad Server llama a `Teleport` |
| Jugador fuera del mapa | Servidor detecta; dueño ejecuta | RPC `SendTo.Owner` | Todos | El dueño llama a `Teleport` porque el avatar usa autoridad Owner |

## Controles

- `WASD`: mover el avatar.
- Mantener `F`: reservar y cargar la apertura del cofre. Soltar cancela la carga.
- `F`: recoger el cubo libre más cercano cuando el cofre ya está abierto.
- `Q`: soltar suavemente el cubo sostenido.
- `E`: lanzar el cubo sostenido.

## Prueba de latencia

La simulación artificial no forma parte del estado de la escena. En Multiplayer PlayMode/Network Simulator configura 200 ms durante la prueba y compara `LegacyLerp`, `Lerp` y `SmoothDampening`. El prefab queda preparado con `SmoothDampening`.

La posición Y del avatar permanece habilitada porque este laboratorio sí utiliza caída y reaparición. Se desactivan las rotaciones X/Z y toda la escala, que no cambian durante el juego.
