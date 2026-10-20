# Lote 0, parte de correo (2026-10-20, Dev)

Aprobado por el usuario el 2026-10-20: queue `Arreglos de Pago` sobre el buzón de prueba `PP_MAIL_BUZON_1` con sincronización del servidor. No se envió ningún correo, no se tocó el buzón de BAC y no se borró nada. En la evidencia la dirección va como el nombre de la variable.

| Inventario | Qué | Estado | Evidencia |
|---|---|---|---|
| 10.8 | Variable `sanic_carp_ev_buzonarea` = buzón 1 (solo Dev) | **creada** y verificada `ya_existia` (`0d2bd3b9-adcc-f111-a05b-00224808ffcd`, componenttype 380 en la solución) | `playbooks/variable-entorno/sanic_carp_ev_buzonarea.md` |
| 13.4 | Queue `Arreglos de Pago`, dueño el team del sistema de la BU | **creada** (`88b4a845-b1cc-f111-a05b-00224808ffcd`): dueño el team del sistema (`2ebc18e7-…`), owning BU `Arreglos de Pago` (`2dbc18e7-…`), pública, filtro "All email messages", activa | `evidencia/01_cola_buzon.json`, `evidencia/02_verificacion.json` |
| 13.5 | Mailbox de la queue | **configurado** (`8ab4a845-b1cc-f111-a05b-00224808ffcd`): sincronización del servidor de entrada y salida, perfil `Microsoft Exchange Online` (`7eedeb22-…`), correo **aprobado por API** (HTTP 204, sin paso manual; `isemailaddressapprovedbyo365admin` = true), Test & Enable: **entrada Success, salida Success** (2026-10-20T18:09Z) | ídem |
| 13.7 | Usuarios de ARP en la BU con roles y en Jefatura | **solo verificado**: en la BU hay un único usuario, la aplicación `SPIKE4 SP - DEV - Pruebas de Correo` (sin roles directos, hereda Oficial por el team del sistema, fuera de Jefatura). Ninguna persona. Jefatura sin miembros | `evidencia/03_usuarios_bu.json` |

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

## Pendiente

- 13.7: mover a la BU los usuarios reales de ARP y sumar el jefe a Jefatura (decisión del usuario: quiénes; moverlos les quita los roles actuales).
- Tanda 3 de humo (correo entre los buzones 1 a 4).
