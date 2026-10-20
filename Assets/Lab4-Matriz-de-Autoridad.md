# Lab 4 - Matriz de autoridad

| Dato o accion | Decide | Replica o envia | Visualiza | Mecanismo |
|---|---|---|---|---|
| Dueno, nombre y color del arbol | Servidor | Servidor | Todos | `NetworkVariable` persistente |
| Vida del arbol | Servidor | Servidor | Todos | `NetworkVariable<int>` persistente |
| Solicitud de reclamar | Cliente dueno de `JugadorCombate` | Cliente -> servidor | Servidor valida | RPC fiable con permiso `Owner` |
| Solicitud de golpe | Cliente dueno de `JugadorCombate` | Cliente -> servidor | Servidor valida | RPC fiable con permiso `Owner` |
| Dano y vida restante | Servidor | Servidor -> todos | Todos | RPC `Unreliable` con ambos valores empaquetados |
| Muerte | Se deriva de `health <= 0` | No se replica otro booleano | Todos | Propiedad local `IsDead` |
| Efecto puntual de muerte | Servidor | Servidor -> todos | Todos | RPC `Reliable` |
| Barra de vida | Estado local derivado | No envia datos | Cada maquina | `health.OnValueChanged` |
| Liberacion del arbol destruido | Servidor | Servidor | Todos | Cambio inmediato a estado libre y materiales naturales |

## Reglas del servidor

- El cliente nunca envia la vida final; solo identifica el arbol que desea golpear.
- El servidor rechaza golpes contra arboles libres, muertos o pertenecientes al atacante.
- El servidor limita la frecuencia de ataques y calcula `Mathf.Max(0, vida - dano)`.
- El texto flotante recibe `dano` y `vidaRestante` dentro del RPC para no depender del orden de llegada de la `NetworkVariable`.
- Un jugador usa un solo clic: reclama si el arbol esta libre y ataca si pertenece a un rival.
