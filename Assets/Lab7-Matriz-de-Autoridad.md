# Laboratorio 7 - Matriz de autoridad

| Elemento | Decide / escribe | Replica | Lectura / visualizacion | Regla |
|---|---|---|---|---|
| Inventario | Servidor | `NetworkList<RanuraLab7>` | Solo el dueño | Guarda `TipoId` y `Cantidad`, nunca referencias a objetos físicos |
| Golpe al árbol | Cliente pide; servidor valida distancia y frecuencia | `NetworkVariable<int>` | Todos | El cliente envía una intención, no el resultado |
| Recolección | Servidor | Evento de lista + despawn | Dueño recibe inventario; todos ven desaparecer el árbol | Primero se guarda el dato y después se usa `Despawn(true)` |
| Pool | Cada máquina mantiene su stack local | `INetworkPrefabInstanceHandler` | Interno de NGO | `AddHandler` se registra en `Awake`, antes de iniciar Host o Cliente |
| Reaparición | Servidor | `Spawn()` del objeto reciclado | Todos | No se ejecuta `Instantiate` después del precalentamiento salvo que el pool se agote |
| Estado reciclado | Servidor reinicia; todos reaccionan | Vida del árbol | Todos | Suscripciones en `OnNetworkSpawn` y limpieza en `OnNetworkDespawn` |

## Controles

- `WASD`: mover el jugador local.
- `F`: golpear el árbol recolectable más cercano.
- `C`: consumir un tronco; permite observar `Value` y `RemoveAt` en la lista.

## Prueba recomendada

1. Entrar a Lab 7 con Host y Cliente.
2. Confirmar que cada pantalla solo muestra su propio inventario.
3. Golpear tres veces un árbol con F.
4. Comprobar el evento `Add` para el primer tronco y `Value` para los siguientes.
5. Verificar que el árbol desaparece y reaparece aproximadamente cuatro segundos después.
6. Pulsar C hasta vaciar una ranura y comprobar el evento `Remove` o `RemoveAt`.
7. Repetir varias veces y confirmar que se reutilizan instancias con nombre `(Pool)`.
