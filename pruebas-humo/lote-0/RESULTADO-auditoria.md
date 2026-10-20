# M7: historial de auditoría con el Oficial no administrador (2026-10-20)

Decisión del usuario del 2026-10-20 (opción A de M7): agregar View Audit History (`prvReadRecordAuditHistory`) a `SR - CARP - Oficial`. Es el único cambio de rol de este trabajo. No se envió correo, no se tocó buzón ni cola, no se borró nada, no se corrió dotnet.

## Cambio aplicado

| Elemento | Cambio |
|---|---|
| Playbook | `playbooks/rol/sr_carp_oficial.md`: `prvReadRecordAuditHistory` = `organizacion` (Global) en `otros_privilegios`, la decisión en §2, la precondición 5 y la tabla de verificación |
| Matriz | `diseno/04-matriz-privilegios.md` §3 (fila nueva), §6 (Oficial 29 = 20 + 9) y §8 (nota fechada) |
| Alcance | **Solo admite Global** (Dev: `privileges`, `canbeglobal = true` y los otros tres alcances `false`). Qué registros ve lo sigue limitando el Read de cada tabla (Local) |
| Dev | `rol.py --solo-verificar`: `difiere` ("falta prvReadRecordAuditHistory (Global)") → `rol.py --completar`: `ya_existia`, "se agregaron 1 (prvReadRecordAuditHistory)" → `--solo-verificar` y corrida normal: `ya_existia`, 110 privilegios, pertenece a la solución |
| Muestra | `muestra_rol.py --guardar`: `OK`, 1 de 1. `playbooks/rol/muestras/sr_carp_oficial.solucion.xml` trae `<RolePrivilege name="prvReadRecordAuditHistory" level="Global" />` (única línea nueva) |
| Herramienta | Ninguna. `rol.py --completar` ya agregaba privilegios que faltan a un rol existente |

## Prueba (`auditoria/m7_auditoria.py`, evidencia `auditoria/evidencia/m7_auditoria.json`)

Mismo usuario y mismo arreglo que la tanda 2b: `SPIKE4 SP - DEV - Pruebas de Correo` (no administrador; Oficial por el team por defecto de la BU; fuera de Jefatura), arreglo HUMO `arreglo-t2-gen`. SPIKE4 se **habilitó temporalmente** y se deshabilitó al final (en `finally`).

| Llamada | Antes (tanda 2b) | Ahora |
|---|---|---|
| `RetrieveRecordChangeHistory` sobre el arreglo (lo que usa la pestaña de auditoría del formulario) | 403 | **200, 3 cambios** (al primer intento, a los 30 s; no hizo falta reasociar el rol) |
| Leer el arreglo | 200 | 200 |
| `GET audits?$filter=_objectid_value eq …` | 403 | 403 "Caller does not have privilege for ReadAuditSummary" |

El 403 de `audits` es esperado: consultar la tabla Audit directamente pide `prvReadAuditSummary` (resumen de auditoría de toda la organización), que **no** se agregó a propósito. La app no la usa.

## Estado final (releído)

SPIKE4: `isdisabled = true`, fuera de Jefatura, sin roles directos, solo en el team por defecto. `identico_a_linea_base = true` contra la línea base de `../tanda2b/estado.json`.

## Notas

- El Jefe lo recibe por el Oficial (team del sistema). También las cuentas de servicio de la BU, como el resto del Oficial (`04` §1 punto 2).
- `diseno/07` DA-21 sigue diciendo "no se tocan los roles"; queda por actualizar.
