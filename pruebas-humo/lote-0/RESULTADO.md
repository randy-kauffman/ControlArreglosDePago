# Lote 0, parte de correo (2026-10-20, Dev)

Aprobado por el usuario el 2026-10-20: queue `Arreglos de Pago` sobre el buzón de prueba `PP_MAIL_BUZON_1` con sincronización del servidor. No se envió ningún correo, no se tocó el buzón de BAC y no se borró nada. En la evidencia la dirección va como el nombre de la variable.

| Inventario | Qué | Estado | Evidencia |
|---|---|---|---|
| 10.8 | Variable `sanic_carp_ev_buzonarea` = buzón 1 (solo Dev) | **creada** y verificada `ya_existia` (`0d2bd3b9-adcc-f111-a05b-00224808ffcd`, componenttype 380 en la solución) | `playbooks/variable-entorno/sanic_carp_ev_buzonarea.md` |
| 13.4 | Queue `Arreglos de Pago`, dueño el team del sistema de la BU | **creada** (`88b4a845-b1cc-f111-a05b-00224808ffcd`): dueño el team del sistema (`2ebc18e7-…`), owning BU `Arreglos de Pago` (`2dbc18e7-…`), pública, filtro "All email messages", activa | `evidencia/01_cola_buzon.json`, `evidencia/02_verificacion.json` |
| 13.5 | Mailbox de la queue | **configurado** (`8ab4a845-b1cc-f111-a05b-00224808ffcd`): sincronización del servidor de entrada y salida, perfil `Microsoft Exchange Online` (`7eedeb22-…`), correo **aprobado por API** (HTTP 204, sin paso manual; `isemailaddressapprovedbyo365admin` = true), Test & Enable: **entrada Success, salida Success** (2026-10-20T18:09Z) | ídem |
| 13.7 | Usuarios de ARP en la BU con roles y en Jefatura | **hecho en Dev** (2026-10-20, decisión del usuario: User 8 y User 9): `# User 8` (Oficial) y `# User 9` (Jefe) movidos a la BU con `PATCH businessunitid` (204), User 9 sumado a Jefatura con `AddMembersTeam` (204). Sin roles directos, como pide la matriz 04 §1.2: Oficial por el team del sistema y Jefe por Jefatura; el perfil de columnas les llega por los dos teams (los dos leen `sanic_cuentadebitar` impersonados: antes 403, ahora 200 con valor). Segunda pasada `--solo-verificar`: `ya_existia` | `scripts/l03_usuarios.py`, `evidencia/04_usuarios_linea_base.json` (antes), `evidencia/05_usuarios_verificacion.json` (después); el estado previo de solo lectura sigue en `evidencia/03_usuarios_bu.json` |

## Cómo se destrabó 13.4

El primer intento dio 403: la plataforma exige que el dueño de una queue tenga `prvReadMailbox` sobre su Mailbox ("Read Privilege Check For Owner"), y el team del sistema solo tiene `SR - CARP - Oficial`, que no lo tenía (`evidencia/01_cola_buzon_intento.json`).

**Decisión del usuario (2026-10-20, opción A):** se agregó `prvReadMailbox` con alcance **Básico** a `SR - CARP - Oficial` (`rol.py --solo-verificar` `difiere` → `--completar` "se agregaron 1" → `ya_existia`, 111 privilegios; `muestra_rol.py --guardar` OK con `<RolePrivilege name="prvReadMailbox" level="Basic" />`). Básico solo deja leer el registro Mailbox propio o del team; no da acceso a ningún correo. Se descartó la opción B (team dueño aparte con `SR - CARP - Servicio`). No hizo falta volver a asociar el rol al team: la queue se creó al primer intento después del cambio.

## Alertas del mailbox (`tracelogs`)

- Test de configuración completado con éxito para entrada y para salida.
- Aviso (nivel 1): "Appointments, contacts and tasks can't be synchronized". Es esperado: la queue solo sincroniza correo; citas, contactos y tareas no son parte del diseño. No se cambia.
- `lastsuccessfulsynccompletedon` todavía null al verificar (la primera sincronización corre después del Test & Enable).

## Correcciones del script

- `queues` devuelve `_businessunitid_value` null; la BU del registro se lee de `_owningbusinessunit_value`.
- Las alertas del mailbox están en el conjunto `tracelogs`, no `traces` (404).

## 13.7: lo que conviene saber

- **Mover de BU les quitó los roles de la BU raíz** (`Basic User`, `CH-SR-Hub`, `CH-SR-Digitizer` en User 8; `Basic User`, `CH-SR-Hub`, `CH-SR-Receptionist` en User 9), como avisa Learn (`DoNotRemoveRolesOnChangeBusinessUnit` = false por defecto). Están en la línea base por si otro proyecto de Dev los necesita de vuelta: asignarlos de nuevo es un paso del administrador, no de esta tanda.
- **Siguen en teams de otros proyectos** de la BU raíz (WFE y otros). Se revisaron sus privilegios efectivos (`RetrieveUserPrivileges`): ninguno de esos teams les da privilegios sobre Email, Queue, Mailbox ni actividades; lo único de correo que tienen es el `prvReadMailbox` Básico del Oficial. 13.9 (revisión de roles ajenos con el administrador) sigue pendiente igual.
- Con `AlwaysMoveRecordToOwnerBusinessUnit` = true (por defecto), los registros que esos usuarios poseían pasaron a la BU nueva. No se midió qué registros de otros proyectos eran.

## Pendiente

- 13.7 en Test y Prod, con los usuarios reales de ARP.
- La tanda 3 de humo de correo se corrió el mismo día: `pruebas-humo/tanda3/RESULTADO.md`.
