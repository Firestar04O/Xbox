# Laboratorio 6 - Matriz de autoridad

## Preparacion de la escena

`ConfiguradorLab6` prepara automaticamente `Assets/Scenes/Lab6.unity` al refrescar
los scripts en Unity. Tambien se puede ejecutar manualmente desde
`Tools > Bosque compartido > Preparar Lab 6`.

El configurador crea y registra los prefabs `GuardianLab6`, `ExploradorLab6`,
`CofreLab6` y `CuboBotinLab6`, activa `ConnectionApproval` y deja el cofre fuera
de la escena para que nazca exclusivamente por codigo.

| Elemento | Decide / escribe | Replica | Visualiza | Regla principal |
|---|---|---|---|---|
| Rol Guardian / Explorador | Servidor durante `ConnectionApprovalCallback` | Prefab de jugador seleccionado con `PlayerPrefabHash` | Todos | El primer jugador aprobado es Guardian; los siguientes son Exploradores |
| Aparicion del cofre | Guardian pide; servidor valida y crea | `NetworkObject.InstantiateAndSpawn` | Todos | El cliente nunca usa `Instantiate` para alterar el mundo compartido |
| Reserva y apertura | Servidor | `NetworkVariable<EstadoCofreLab6>`, usuario y hora inicial | Todos | Mientras un jugador mantiene F, otro no puede reemplazar su reserva |
| Dueño inicial del botin | Servidor | `SpawnWithOwnership` y `NetworkVariable<ulong>` | Todos | Los cubos pertenecen al jugador que completo la apertura |
| Recoger, soltar y lanzar | Dueño pide; servidor valida y ejecuta | Parenting + `NetworkVariable<ulong>` del portador | Todos | Solo el dueño puede recoger su cubo; el servidor controla parenting y fisica |
| Color y etiqueta del cubo | Derivado del dueño replicado | Ownership + dueño lógico | Todos | Se repinta en `OnNetworkSpawn` y `OnOwnershipChanged` |
| Regalar un cubo | Dueño pide; servidor lo suelta y busca al jugador más cercano | `ChangeOwnership` | Todos | Solo el dueño actual puede solicitar la transferencia |
| Consumir un cubo | Dueño pide; servidor valida | `Despawn(true)` | Todos | Nunca se usa `Destroy()` local en clientes |
| Desconexion del dueño | Servidor | `DontDestroyWithOwner` + `RemoveOwnership` | Todos | El cubo queda gris y sin dueño en el suelo |

## Controles

- `WASD`: mover el jugador local.
- `R`: el Guardian solicita crear el único cofre de la sesión.
- Mantener `F`: reservar y abrir el cofre; soltar cancela. Frente a un cubo propio libre, `F` lo recoge.
- `Q`: soltar el cubo sostenido.
- `E`: lanzar el cubo sostenido.
- `G`: soltar el cubo sostenido y transferirlo al otro jugador más cercano. Tambien funciona con un cubo propio libre cercano.
- `X`: consumir el cubo propio más cercano.

## Flujo de prueba con dos instancias

1. Ambos ejecutables entran a Lab 6.
2. El primero inicia Host y recibe el prefab Guardian.
3. El segundo inicia Cliente y recibe el prefab Explorador.
4. El Guardian pulsa R; el servidor crea el cofre.
5. Un jugador mantiene F. El otro no puede robar la reserva.
6. Al completar la apertura, los cubos nacen con ownership del abridor.
7. El dueño usa G cerca del otro jugador para regalar o X para consumir.
8. Si el cliente dueño se desconecta, sus cubos permanecen en el suelo.
