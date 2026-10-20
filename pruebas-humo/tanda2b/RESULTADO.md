# Pruebas de humo en Dev, tanda 2b (2026-10-20), usuario NO administrador

Repite lo que la tanda 2 dejó fallando (hallazgo 1 de `../tanda2/RESULTADO.md`) después de dar al perfil `CSP - CARP - Datos sensibles` el permiso de **actualizar** sobre `sanic_instantaneacedula`. Mismo mecanismo y mismo usuario: `SPIKE4 SP - DEV - Pruebas de Correo` (credenciales `PP_MAIL_*`, sin mostrarlas), rol Oficial por el team por defecto; Jefe solo por el team Jefatura, temporal. Datos `HUMO-` de la tanda 2 (`arreglo-t2-gen`, `-h8`, `-h19`). No se envió correo, no se tocó buzón ni cola, no se creó ni modificó ningún rol, no se borró nada, no se corrió dotnet, no se editó código de producción.

## Qué cambió antes de probar

| Elemento | Cambio |
|---|---|
| Código de producción | Ninguno. Desbloquear y Reabrir solo escriben `sanic_instantaneacedula` entre las columnas protegidas (`ReglasDeArreglo.cs` `VaciarInstantanea`); `sanic_cuentadebitar` no se escribe en ninguno de los dos gestos, así que la decisión no se extiende a ella |
| Dev, perfil | `sanic_instantaneacedula`: `canupdate` 0 a 4 (leer 4, crear 0, `canreadunmasked` 0). Aplicado con `perfil_columnas.py --corregir`: `difiere`, luego `ya_existia`, luego `--solo-verificar` otra vez `ya_existia` |
| Herramienta | `herramientas/construir/perfil_columnas.py`: bandera nueva `--corregir` (PATCH de leer, crear y actualizar que difieren; no borra sobrantes; se combina con `--completar`; `--solo-verificar` la anula). Pruebas en `tests/test_perfil_columnas.py` (8 nuevas, rojo primero). Suite completa: `Ran 1386 tests ... OK` |
| Diseño | `diseno/02` §8 (fila del perfil, nota y P-16), `diseno/04` §8, `diseno/06` 6.1, con motivo del 2026-10-20 |
| Playbook y muestra | `playbooks/perfil-columnas/csp_carp_datos_sensibles.md` (JSON, tabla, verificación 2, sección 6 y reversa); muestra regenerada con `muestra_perfil_columnas.py --guardar` (coincide, 1 de 1) |

## Resultados (`evidencia/50_desbloquear_reabrir.json`)

| Prueba | Resultado | Detalle |
|---|---|---|
| (a) Oficial desbloquea un Borrador congelado | **PASA** | 204 (antes 403). Sin `sanic_fechacongelamiento`; las cuatro instantáneas y `sanic_numeroprovisional` quedan en nulo. El Borrador ya estaba congelado desde la tanda 2 (provisional 1), no hizo falta generar el formato |
| (b) Jefatura reabre un Vigente congelado | **PASA** | 204 al primer intento (antes 403). Descongelado, instantáneas vacías, `cantidadreaperturas` 1, `reabiertoporid` = SPIKE4 |
| (b) Jefatura reabre un Terminado | **PASA** | 204 (antes 403). Queda Vigente, sin `fechatermino`, reapertura 1 |
| (c) Cambio manual de `sanic_instantaneacedula` | **RECHAZADO** | Como Oficial con el arreglo congelado (valor nuevo y null), como Oficial sin congelar y como Jefe: 400 `CARP-ARR-009` ("Estas columnas las gestiona el sistema y no se pueden modificar: sanic_instantaneacedula"). La cédula copiada quedó intacta. Control: una edición normal del Borrador pasa (204) |
| M7 (View Audit History, `diseno/07` §12 punto 16, DA-21) | **NO LA VE** | Como Oficial no admin: `RetrieveRecordChangeHistory` 403 y `audits` 403 sobre un arreglo HUMO que sí lee (200). El administrador ve 3 cambios y 3 filas de `audits`. Con el rol Jefe sumado (SPIKE4 en Jefatura): `audits` también 403. No se cambió ningún rol: la decisión sobre la matriz `04` sigue pendiente del usuario. Evidencia: `evidencia/60_m7_historial.json` y `51_m7_auditoria.json` (en este último el primer `RetrieveRecordChangeHistory` salió 400 por un parámetro mal codificado en mi script; vale el de `60`, y solo se midió al Oficial) |

El rótulo "M7" no existe en `diseno/07`; se tomó la verificación 16 de su §12 (View Audit History con un no administrador).

## Estado final (releído, `evidencia/99_reversion_verificada.json` y `98_estado_final.json`)

| Elemento | Antes | Después |
|---|---|---|
| SPIKE4 | `isdisabled=true`, `accessmode=4`, BU Arreglos de Pago, sin roles directos, solo en el team por defecto | idéntico campo por campo (`identico_a_linea_base: true`) |
| Team Jefatura, miembros | 0 | 0 (SPIKE4 entró y salió dentro de un `try/finally`) |
| Perfil `CSP - CARP - Datos sensibles` | 0 usuarios, 2 teams (el del sistema de la BU y Jefatura) | igual en miembros; permisos con `sanic_instantaneacedula` en leer 4, actualizar 4 |

Datos HUMO que cambiaron de estado (se dejan, no se borran): `arreglo-t2-gen` quedó Borrador sin congelar; `arreglo-t2-h8` y `arreglo-t2-h19` quedaron Vigentes con una reapertura.

## Tropiezos

- Mi primera versión de la sonda de auditoría codificó dos veces el parámetro (400 de sintaxis); se repitió en `t60`.
- En los `estado.json` y la evidencia no hay tokens ni secretos.
