# Procedimiento de instalación en un entorno nuevo (Test o Prod)

Inventario 13.15. Versión 1 (2026-10-20), escrita a partir de lo construido y probado en Dev. **Todavía no se ejecutó en Test**: la primera corrida en Test es la que lo valida; se corrige este documento con lo que salga.

## 0. Reglas antes de empezar

- **Qué viaja y qué no.** La solución `sanic_carp_sol_controlarreglospago` viaja **managed** con todos sus componentes:
  - tablas y choices;
  - roles y perfil de columnas;
  - paquete de plugins, Custom API y steps;
  - web resources, formularios, vistas, gráficos, panel y app;
  - variables de entorno (sin valor), referencias de conexión y flujos.

  **No viajan**, porque son datos o configuración del entorno: la BU, los teams, la cola, el buzón, los usuarios, la plantilla con su mapeo, las conexiones y los trabajos de purga. Ver la definición, §6 "Entornos y despliegue".
- **Herramientas.** Las de `herramientas/construir/` **construyen solo en Dev**. En Test y Prod solo se usan:
  - los scripts de datos y de configuración que dice cada paso;
  - las consultas de verificación (solo lectura).
- **Apuntar los scripts al entorno nuevo.** Los scripts leen la conexión de un archivo de configuración: `PP_SECRETS_FILE` o `~/projects/pp_secrets.env`, con `DATAVERSE_URL`, `PP_TENANT_ID`, `PP_CLIENT_ID` y `PP_CLIENT_SECRET`. Use un archivo **aparte por entorno** y exporte `PP_SECRETS_FILE` antes de cada sesión. Nunca imprima ni copie su contenido.
- **Orden.** Los pasos van en orden de dependencia. No avance si la verificación de un paso no da lo esperado.
- **Evidencia.** Guarde la salida de cada verificación en `pruebas-humo/instalacion-<entorno>/`, sin secretos ni datos de clientes.

## 1. Prerrequisitos (13.1 a 13.3, 13.6, 13.16, 13.17)

| # | Tarea | Responsable | Verificación |
|---|---|---|---|
| 1.1 | BU `Arreglos de Pago` como hija de la BU raíz (13.1) | Admin. de Power Platform | `GET businessunits?$filter=name eq 'Arreglos de Pago'` da una fila |
| 1.2 | Team del sistema de la BU (se crea con la BU) y team `Jefatura` dentro de la BU (13.2, 13.3) | Admin. de Power Platform | `GET teams?$filter=_businessunitid_value eq <bu>` da el team por defecto y `Jefatura` |
| 1.3 | Grupo de Entra para los datos sensibles (cédula y cuenta), con los usuarios de ARP | Admin. de Azure | El grupo existe con los miembros correctos |
| 1.4 | **Cuenta de servicio dedicada (13.6):** usuario con licencia, **dentro de la BU**, con `SR - CARP - Servicio` (6.4) y `SR - CARP - Purga` (6.5); dueña de los flujos y de los trabajos de purga. En Dev se usó la cuenta de administración en la BU raíz como **desviación**; en Test y Prod no se repite | Admin. de Power Platform | `GET systemusers(<id>)?$select=isdisabled,_businessunitid_value&$expand=systemuserroles_association($select=name)` |
| 1.5 | Capacidad de base de datos y de archivos del entorno (13.16) | Admin. de Power Platform | Reporte de capacidad del tenant |
| 1.6 | **Descargas y OneDrive (13.17).** Confirmar con TI si la carpeta Descargas está redirigida a OneDrive. Si lo está, el gesto Generar formato no se habilita con datos reales hasta resolverlo (RNF-03) | Equipo de automatización, TI | Respuesta escrita de TI |

## 2. Importar la solución managed

1. Exportar desde Dev la solución como **managed**, después de publicar todas las personalizaciones.
2. Importarla en el entorno nuevo con `pac solution import` o con el portal, con **"Activar procesos y complementos"**.
3. Durante la importación, el portal pide los **valores de las variables** y las **conexiones**. Puede dejarlos para el paso 4.
4. Publicar todas las personalizaciones.

**Verificación:**
- `GET solutions?$filter=uniquename eq 'sanic_carp_sol_controlarreglospago'&$select=version,ismanaged` da `ismanaged = true` y la versión exportada.
- **Paquete de plugins.** El paquete viaja dentro de la solución. Hay que comprobar dos cosas:
  - `GET pluginassemblies?$filter=name eq 'Sanic.Carp.Plugins'&$select=version` da **1.0.1.0** (`playbooks/paquete-plugins/Sanic.Carp.Plugins.md`);
  - la versión del paquete (`pluginpackages`, `sanic_carp_pkg_plugins`) sigue en 1.0.0.
- **Steps.** `GET sdkmessageprocessingsteps?$filter=statecode eq 1&$select=name` sobre los steps del ensamblado **no debe devolver ninguno**. Tienen que estar habilitados los 37 steps de `playbooks/steps/sanic_carp_pkg_plugins.md` y los 5 que la plataforma crea para las Custom API.
  - En Dev, una importación de ribbon apagó los 37 steps. `herramientas/construir/importar_solucion.py` los rehabilita (`statecode 0`, `statuscode 1`), y la función que lista los deshabilitados está en el mismo archivo.
  - En un entorno managed, rehabilitarlos con el portal: Soluciones → Pasos de procesamiento de mensajes SDK → Activar.
- **Custom API.** Las 5 `customapis` con `uniquename` `sanic_carp_capi_*` existen.

## 3. Usuarios y seguridad (13.7, 13.8, 13.9)

1. **Mover a los usuarios de ARP a la BU** (13.7):
   - Antes de mover a nadie, guarde la línea base de cada uno (BU, roles y teams). **Mover a un usuario de BU le quita los roles que tiene hoy.** Avíseles antes.
   - Script de referencia: `pruebas-humo/lote-0/scripts/l03_usuarios.py`, idempotente, con `--solo-verificar`. Ajuste los nombres de usuario del entorno antes de usarlo.
2. **Roles por team, no por usuario** (matriz `diseno/04-matriz-privilegios.md` §1.2):
   - el team del sistema de la BU lleva `SR - CARP - Oficial`;
   - `Jefatura` lleva `SR - CARP - Jefe`;
   - los oficiales los heredan por estar en la BU, y el jefe además entra a `Jefatura` (AddMembersTeam).
3. **Perfil de columnas** `csp_carp_datos_sensibles` (13.8). Se asocia al team del sistema y a `Jefatura`.
   - Pruebe de punta a punta: entrar al grupo de Entra permite leer la cédula, salir del grupo deja de permitirlo.
   - **Verificación:** con un oficial, `GET sanic_carp_tbl_arreglos?$select=sanic_cuentadebitar&$top=1` da 200 con valor; sin el perfil, el valor viene vacío o la lectura falla.
4. **Verificación 13.9.** Con el administrador, confirmar que **ningún rol ajeno** tiene profundidad Organización o BU padre sobre Email, Attachment, `msdyn_richtextfiles`, Queue, Queue Item, Mailbox ni las tablas `sanic_carp_*`. Guarde la evidencia. Se hace antes de cargar datos reales y antes de cada pase a Prod.

**Privilegios que se agregaron en Dev y viajan en el rol Oficial:**
- `prvReadRecordAuditHistory` (Global): ver el historial de auditoría del arreglo.
- `prvReadMailbox` (Básico): el dueño de una cola tiene que poder leer su Mailbox.

No hace falta tocarlos, pero conviene confirmarlos en la muestra del rol (`playbooks/rol/muestras/sr_carp_oficial.solucion.xml`).

## 4. Variables de entorno y conexiones (13.13)

| Inv. | Variable o referencia | Valor en el entorno |
|---|---|---|
| 10.1 | `sanic_carp_ev_nombrebu` | `Arreglos de Pago` (nombre exacto de la BU) |
| 10.2 | `sanic_carp_ev_nombreteamjefatura` | `Jefatura` |
| 10.3 | `sanic_carp_ev_destinatariosalertas` | Correos del jefe y del equipo de automatización, separados por `;`. En Test, solo buzones de prueba |
| 10.4 | `sanic_carp_ev_umbralesmonitoreo` | `{"horasSinEntrada":4,"ventanaErroresMin":35,"horasPurga":24,"minutosReconciliar":10}`; `horasSinEntrada` a confirmar con el jefe (DA-03) |
| 10.8 | `sanic_carp_ev_buzonarea` | Dirección del buzón del área **de este entorno** (en Prod, el buzón real; en Test, uno de prueba) |
| 10.6 | `CONR - CARP - Dataverse` (`sanic_carp_conr_dataverse`) | Conexión de Dataverse **creada por la cuenta de servicio** |
| 10.5 | `CONR - CARP - Outlook` (`sanic_carp_conr_outlook`) | Conexión de Office 365 Outlook **creada por la cuenta de servicio** |

- Los valores se cargan en el portal: Soluciones → Variables de entorno → Valor actual.
- Las conexiones **solo las puede crear la propia cuenta de servicio**, iniciando sesión en make.powerapps.com: Soluciones → Referencias de conexión → elegir o crear la conexión → Guardar.
- No se pueden enlazar por API con una conexión de otra persona: Dev lo rechazó con `ConnectionAuthorizationFailed`. Los playbooks están en `playbooks/variable-entorno/` y `playbooks/referencia-conexion/`.

## 5. Cola y buzón del área (13.4, 13.5)

> ⚠️ **Antes de conectar el buzón real.** La primera sincronización **importa toda la bandeja de entrada que ya tiene el buzón**, y por cada correo se crea una conversación. En Dev entraron 10 correos viejos y se crearon 10 conversaciones (`pruebas-humo/tanda3/RESULTADO.md`). En Prod hay que **vaciar o archivar la bandeja del buzón real** antes del paso 5.3, o aceptar por escrito esa carga inicial con el jefe.

1. **Buzón de Exchange** (13.5, Mensajería y TI):
   - buzón compartido del área, uno por entorno;
   - **Send As** para la identidad de sincronización;
   - acceso restringido en Exchange. En Dev no se configuró en el buzón de prueba; en Prod es obligatorio.
2. **Cola** `Arreglos de Pago` (13.4):
   - en la BU, con el **team del sistema** de la BU como dueño, pública, con filtro de entrada "All email messages";
   - si el rol Oficial no tuviera `prvReadMailbox`, la creación da 403;
   - script de referencia: `pruebas-humo/lote-0/scripts/l01_cola_buzon.py`, idempotente, con `--solo-verificar`.
3. **Registro Mailbox** de la cola:
   - perfil "Microsoft Exchange Online", sincronización del servidor para entrada y salida;
   - **Aprobar correo**, porque el entorno puede exigirlo (`requireapprovalforqueueemail`);
   - después, **Test & Enable**.
   - El script anterior hace todo esto por API. Si la aprobación falla por permisos de Exchange, hágalo en el admin center: Configuración → Correo electrónico → Buzones → Arreglos de Pago → **Aprobar correo** → **Probar y habilitar buzón**.

**Verificación:**
- La cola existe, su dueño es el team del sistema y está en la BU del área. **Ojo:** la BU de la cola se lee de `_owningbusinessunit_value`, porque `_businessunitid_value` viene vacío.
- El buzón tiene `incomingemailstatus = 1` y `outgoingemailstatus = 1` (Éxito).
- Las alertas del buzón se leen en `tracelogs`, no en `traces`, que da 404.
- `sanic_carp_ev_buzonarea` es igual a la dirección del buzón.

## 6. Plantilla del formato (9.1, 9.2)

Recarga controlada (OA-4), sin la herramienta de migración de configuración:

1. Ejecute `pruebas-humo/lote-0/scripts/l10_plantilla_v07.py`, o las dos llamadas de datos (`python3 herramientas/construir/datos.py playbooks/datos/versionplantilla.md` y después `... playbooks/datos/mapeocelda.md`). El resultado:
   - crea la versión `v07` en **Borrador**;
   - sube el archivo `F-NIC-0000052_v07.xlsx`, que se verifica bajándolo y comparando el SHA-256;
   - carga los **28 mapeos**.
2. El **jefe publica** la v07 desde la aplicación con el botón **Publicar versión** (gesto 13).
3. **Compare la huella.** `sanic_huella` de la v07 publicada tiene que ser igual a la de Test, que es el SHA-256 del archivo aprobado. Si difiere, no se usa el entorno.

**Verificación:** `python3 herramientas/construir/datos.py playbooks/datos/versionplantilla.md --solo-verificar` y lo mismo con `mapeocelda.md` dan `ya_existia`: 1 y 28 filas. Hay **una sola** versión Vigente.

## 7. Flujos de monitoreo (11.1, 11.2)

1. Confirmar que las referencias de conexión del paso 4 están enlazadas.
2. Con la **cuenta de servicio**, en make.powerapps.com → Soluciones → **Flujos de nube**, **activar** los dos flujos:
   - `Cloud Flow - CARP - EST - Avisar estado del mailbox`;
   - `Cloud Flow - CARP - SAL - Salud del buzon`.

   La activación no se puede hacer por API con el service principal: el servicio de Power Automate la autoriza contra la identidad del token (`pruebas-humo/lote-d/RESULTADO.md`).
3. **Verificación:**
   - `python3 pruebas-humo/lote-d/scripts/como_servicio.py flujo playbooks/flujo/carp-est.md --solo-verificar` da `ya_existia`, y lo mismo con `carp-sal.md`.
   - El día hábil siguiente, revise la primera corrida de `SAL` en su historial.
4. **Riesgo D-10, a probar en Test antes de Prod.** Según Microsoft Learn, el dueño de un flujo con disparador de Dataverse (el flujo `EST`) necesita crear, leer, escribir y eliminar en **Callback Registration** a nivel Usuario, y `SR - CARP - Servicio` no los tiene. En Dev no se notó porque el dueño era administrador.
   - Si al activar `EST` en Test falla el registro del disparador, agregar esos privilegios es un **cambio de rol** y lo decide el responsable del proyecto. Ver `diseno/04-matriz-privilegios.md`, D-10.

**Comportamiento esperado:**
- `SAL` corre de lunes a viernes, de 07:00 a 18:30 (hora de Nicaragua), cada 30 minutos.
- Avisa a `sanic_carp_ev_destinatariosalertas` en cualquiera de estos casos:
  - no entró correo en `horasSinEntrada` horas;
  - quedaron correos sin conversación (llama a `sanic_carp_capi_reconciliarcorreos`);
  - hay respuestas con error de envío;
  - falló una purga.
- `EST` avisa cuando el buzón pasa a falla.

## 8. Trabajos de purga (13.12)

- Se crean **por entorno**, con la identidad de la cuenta de servicio, para la retención de 2 años (arreglos; correos y luego conversaciones; clientes).
- **El nombre de cada trabajo tiene que llevar `CARP`:** el flujo `SAL` solo vigila las purgas con `CARP` en el nombre.
- La cantidad de trabajos todavía no está cerrada (inventario, Inconsistencias, punto 2). El diseño detallado de 13.12 está pendiente. Hasta que exista, la revisión es manual y semanal, en System Jobs.
- `SR - CARP - Servicio` ya puede leer sus propios trabajos de purga (`prvReadAsyncOperation` Básico; D-09, verificado en Dev).

## 9. Humo mínimo después de instalar

Hágalo con un oficial y un jefe **no administradores**, con datos ficticios (prefijo `HUMO-`). En Prod, solo con un cliente ficticio y desde un buzón de prueba autorizado.

| # | Prueba | Esperado | Referencia |
|---|---|---|---|
| 1 | Enviar un correo al buzón del área desde un buzón de prueba | En menos de un minuto entra a la cola, con el team del sistema como dueño, en la BU, y se crea su conversación | `pruebas-humo/tanda3/RESULTADO.md` (H-25, H-26) |
| 2 | El oficial **toma** la conversación y **responde** | La respuesta sale con el buzón del área como remitente; se llenan "respondido por" y el tiempo de respuesta | tanda 3 (H-5, H-6, S-35) |
| 3 | El cliente responde | Entra a la misma conversación, no a una nueva | tanda 3 |
| 4 | El oficial intenta reasignar | `CARP-CNV-008`. El jefe reasigna sin error | tanda 3 (2.4) |
| 5 | Registrar un arreglo HUMO, **generar formato**, **formalizar** | El `.xlsx` se descarga con las 28 celdas llenas y las fechas con formato `dd/mm/yyyy`; el arreglo queda Vigente con número | `pruebas-humo/lote-0/RESULTADO-plantilla.md` |
| 6 | Registrar el pago de la cuota 1 y volver a generar | La fecha de pago aparece en "CONTROL DE PAGOS" | `RESULTADO-plantilla.md` |
| 7 | El oficial abre el historial de auditoría del arreglo | Se ve (200), sin error de permisos | `pruebas-humo/lote-0/RESULTADO-auditoria.md` |
| 8 | El oficial lee la cédula y la cuenta a debitar | Se ven; un usuario fuera del grupo de Entra no las ve | paso 3.3 |
| 9 | El jefe abre el **Panel de supervisión** | Se ven los dos gráficos y las dos listas | `pruebas-humo/lote-e/RESULTADO.md` |
| 10 | Revisar los steps y los trabajos asíncronos | Los 42 steps están habilitados y no hay trabajos de plugins fallidos | paso 2 |
| 11 | Recargar la aplicación con Ctrl+F5 y probar Tomar desde la lista | Toast "Conversación tomada…" | `pruebas-humo/lote-b/RESULTADO.md` |

Si alguna prueba falla, **no** se cargan datos reales. Se registra en el `RESULTADO.md` de la instalación y se corrige este procedimiento.

## 10. Pendientes que este procedimiento no cierra

- 13.12: diseño detallado de los trabajos de purga.
- 13.14: pipelines de Power Platform. Mientras tanto, la importación es manual (paso 2).
- D-10: privilegios de Callback Registration para el dueño de los flujos (paso 7.4).
- DA-03: horario hábil y `horasSinEntrada`, a confirmar con el jefe.
- D-27: si el gráfico de tiempo total excluye las finalizadas "Sin respuesta", a confirmar con negocio.
