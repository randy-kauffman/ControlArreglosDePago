# Lote 0, parte de correo (2026-10-20, Dev)

Aprobado por el usuario el 2026-10-20: queue `Arreglos de Pago` sobre el buzón de prueba `PP_MAIL_BUZON_1` con sincronización del servidor. No se envió ningún correo, no se tocó el buzón de BAC y no se borró nada.

| Inventario | Qué | Estado | Evidencia |
|---|---|---|---|
| 10.8 | Variable `sanic_carp_ev_buzonarea` = buzón 1 (solo Dev) | **creada** y verificada `ya_existia` (`0d2bd3b9-adcc-f111-a05b-00224808ffcd`, componenttype 380 en la solución) | `playbooks/variable-entorno/sanic_carp_ev_buzonarea.md` |
| 13.4 | Queue `Arreglos de Pago`, dueño el team del sistema de la BU | **BLOQUEADA**: 403, el dueño de una queue tiene que poder leer su Mailbox y el team solo tiene `SR - CARP - Oficial`, que no tiene `prvReadMailbox` (diseño: "Oficial sin acceso al Mailbox"). No se creó nada | `evidencia/01_cola_buzon_intento.json` |
| 13.5 | Mailbox de la queue (SSS entrada/salida, perfil Microsoft Exchange Online, aprobar, Test & Enable) | **no empezado** (depende de 13.4) | script listo: `scripts/l01_cola_buzon.py` (idempotente) |
| 13.7 | Usuarios de ARP en la BU con roles y en Jefatura | **solo verificado**: en la BU hay un único usuario, la aplicación `SPIKE4 SP - DEV - Pruebas de Correo` (sin roles directos, hereda Oficial por el team del sistema, fuera de Jefatura). Ninguna persona. Jefatura sin miembros | `evidencia/03_usuarios_bu.json` |

## Hallazgo de diseño

La plataforma exige que el dueño de una queue tenga `prvReadMailbox` sobre el Mailbox de esa queue (comprobación "Read Privilege Check For Owner" al crear). La matriz (`diseno/04-matriz-privilegios.md`) solo da `prvReadMailbox` a `SR - CARP - Servicio` (L) y dice expresamente que el Oficial no tiene acceso al Mailbox, pero 13.4 pone como dueño al team del sistema, cuyo único rol es el Oficial. Las dos cosas no pueden ser ciertas a la vez.

El dueño no puede quedar fuera de la BU: H-26 y S-34 (`CARP-ENT-004`) necesitan que el correo entrante quede en la BU del área.

Opciones (decisión del usuario, tocan roles o el diseño):

- **A.** Agregar `prvReadMailbox` con alcance **Básico** a `SR - CARP - Oficial`. Con Básico el oficial solo lee el Mailbox de lo que es suyo o de su team, o sea el del área (no ve correos por esto; es el registro de configuración). Cambio mínimo, respeta 13.4.
- **B.** Un team de dueño aparte en la BU (por ejemplo `Arreglos de Pago - Buzon`), sin miembros, con `SR - CARP - Servicio` (ya tiene `prvReadMailbox` L). No modifica roles, pero cambia 13.4 y agrega un team; los Queue Items y correos quedarían de ese team (dentro de la BU).

## Pendiente manual para el usuario (después de 13.4)

Si la aprobación programática del correo falla por permisos de Exchange: Power Platform admin center → Dev → Settings → Email → Mailboxes → Active Mailboxes → `Arreglos de Pago` → **Approve Email** → OK, y después **Test & Enable Mailbox**. Luego `python3 pruebas-humo/lote-0/scripts/l01_cola_buzon.py --solo-verificar`.
