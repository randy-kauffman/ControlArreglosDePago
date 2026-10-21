# Lote D en Dev (2026-10-20): cuenta de servicio, variables, connection references y flujos

Inventario 13.6, 10.3 a 10.6, 11.1 y 11.2; diseño `diseno/07-diseno-app-y-flujos.md` §8. GUIDs en `estado.json`. No se envió ningún correo, no se borró nada.

## Resultado

| Componente | Estado | Detalle |
|---|---|---|
| 13.6 Cuenta de servicio de Dev | desviación documentada | `admin@55xljh.onmicrosoft.com` (indicada por el usuario): System Administrator, licenciada, en la BU raíz. **No se movió** (le quitaría los roles y es la admin del entorno) y **no se le asignaron roles CARP**: System Administrator ya cubre `Servicio` y `Purga`. En Test y Prod: cuenta dedicada dentro de la BU con 6.4 y 6.5 |
| 10.3 `sanic_carp_ev_destinatariosalertas` | `creado` → `ya_existia` | Valor de Dev: **solo el buzón de prueba 3** (ni el 1, que es el buzón del área, ni el 2, que hace de cliente en la tanda 3) |
| 10.4 `sanic_carp_ev_umbralesmonitoreo` | `creado` → `ya_existia` | JSON `{"horasSinEntrada":4,"ventanaErroresMin":35,"horasPurga":24,"minutosReconciliar":10}` |
| 10.6 `sanic_carp_conr_dataverse` | `creado` → `ya_existia` | Asociada a la conexión de Dataverse que ya existía en Dev (`bc82127e…`, la que usan otras referencias de la cuenta de servicio) |
| 10.5 `sanic_carp_conr_outlook` | `bloqueado` | Creada en la solución **sin conexión**: la plataforma rechaza asociar una conexión de Office 365 Outlook de otra persona (`ConnectionAuthorizationFailed`, probado con `95bbcb03…` y `ebcf70b7…`, también impersonando con `CallerObjectId`) |
| 11.1 `Cloud Flow - CARP - EST - Avisar estado del mailbox` | `bloqueado` (creado y apagado) | En la solución (componenttype 29), dueño la cuenta de servicio. Se enciende cuando 10.5 tenga conexión |
| 11.2 `Cloud Flow - CARP - SAL - Salud del buzon` | `bloqueado` (creado y apagado) | Ídem |
| D-09 | resuelta sin cambio de rol | `prvReadAsyncOperation` Básico alcanza: el no administrador lee `bulkdeleteoperations` con 200 y ve solo los propios (`evidencia/02_d09_purga.json`) |

## Paso manual que te queda (5 minutos)

1. Entrá a https://make.powerapps.com con **admin@55xljh.onmicrosoft.com**, entorno de Dev.
2. **Soluciones** → `sanic_carp_sol_controlarreglospago` → **Referencias de conexión** → `CONR - CARP - Outlook`.
3. En **Conexión**, elegí una conexión de Office 365 Outlook de esa cuenta (o **+ Nueva conexión** e iniciá sesión con esa misma cuenta). Guardá.
4. Avisame: corro `python3 pruebas-humo/lote-d/scripts/como_servicio.py flujo playbooks/flujo/carp-est.md` y lo mismo con `carp-sal.md`, que **encienden** los dos flujos y verifican.

### Intento de encendido (2026-10-21 03:50 UTC, 21:50 del 2026-10-20 en Nicaragua)

- Antes de encender: `sanic_carp_ev_destinatariosalertas` tiene un solo destinatario y es el buzón de prueba 3 (comparado sin imprimirlo). `sanic_carp_conr_outlook` y `sanic_carp_conr_dataverse` ya tienen conexión.
- `como_servicio.py flujo` con los dos playbooks: `bloqueado`, `ConnectionAuthorizationFailed`. La activación la autoriza el servicio de Power Automate contra la identidad del token (el service principal), y la cabecera `CallerObjectId` solo vale para Dataverse: no alcanza con hacerse pasar por la cuenta de servicio, aunque sea la dueña de la conexión de Outlook.
- `--solo-verificar`: los dos dan `difiere` **solo por el estado** (`statecode` 0/1 frente a 1/2); la definición coincide con el archivo. No quedó nada a medias.
- 11.2 no corrió: sigue apagado, y a esa hora está fuera del horario hábil.

**Paso que falta (1 minuto, con admin@55xljh.onmicrosoft.com):** make.powerapps.com → Dev → Soluciones → `sanic_carp_sol_controlarreglospago` → **Flujos de nube** → `Cloud Flow - CARP - EST - Avisar estado del mailbox` → **Activar**; lo mismo con `Cloud Flow - CARP - SAL - Salud del buzon`. Después `python3 pruebas-humo/lote-d/scripts/como_servicio.py flujo playbooks/flujo/carp-est.md --solo-verificar` (y `carp-sal.md`) debe dar `ya_existia`, y en el día hábil siguiente se revisa la primera corrida de 11.2 en su historial.

Al encender 11.2 la recurrencia corre en horario hábil (lunes a viernes, 07:00 a 18:30 de Nicaragua). Si no entró correo al buzón del área en las últimas 4 horas, **manda un aviso al buzón de prueba 3**, y uno cada 30 minutos mientras siga así. Es esperado en Dev mientras no haya tráfico; se corta apagando el flujo.

## Desviaciones y decisiones

1. **COD de 11.1 = `EST`**, como ya fijaba `diseno/07` §8.3 (`CARP-EST`).
2. **Ámbito del disparador de 11.1 = Unidad de negocio** (el del diseño). En Dev el dueño está en la BU raíz, así que el disparador **no ve** el buzón del área (su dueño es el team de la BU hija). Se acepta en Dev; en Test y Prod la cuenta dedicada está dentro de la BU.
3. **Horario hábil con la recurrencia semanal nativa** (lunes a viernes, `hours` 7 a 18, `minutes` 0 y 30, `Central America Standard Time`), que el diseño admite, en vez de una condición inicial. Sigue a confirmar con el jefe (DA-03).
4. **Filtros agregados en 11.2**: la entrada reciente filtra por destinatario = buzón del área, los errores de envío por remitente = buzón del área, y las purgas por `CARP` en el nombre. En Dev el dueño es administrador y, sin esos filtros, vería los correos y las purgas de otros proyectos. Los trabajos de 13.12 tienen que llevar `CARP` en el nombre.
5. **Herramienta nueva** `herramientas/construir/referencia_conexion.py` (12 pruebas). **`flujo.py` cambió**: una referencia sin conexión ya no bloquea antes de escribir; el flujo se crea o actualiza **apagado** y el estado es `bloqueado` con lo que falta (3 pruebas). La suite de herramientas da 1517 OK.
6. **Construcción como la cuenta de servicio**: `scripts/como_servicio.py` llama a las herramientas con la cabecera `CallerObjectId` de la cuenta de servicio, para que los flujos queden a su nombre (13.6) y para que, con la conexión ya asociada, la plataforma deje encenderlos.
7. **Hallazgo D-10** (`diseno/04`): Learn exige al dueño de un flujo con disparador de Dataverse CRUD de nivel Usuario sobre Callback Registration, y `SR - CARP - Servicio` no lo tiene. En Dev no se nota (administrador). Antes del pase a Test hay que probarlo y, si falta, decidir el cambio de rol.

## Scripts y evidencia

- `scripts/d01_inspeccion.py` (solo lectura) → `evidencia/01_inspeccion.json`: cuenta, roles CARP, referencias, variables, metadatos de purgas.
- `scripts/d02_d09_purga.py` → `evidencia/02_d09_purga.json`: D-09 con el no administrador (habilitado solo durante la prueba y deshabilitado al final; quedó igual que antes).
- `scripts/como_servicio.py`: envoltorio de impersonación para `flujo.py` y `referencia_conexion.py`.
