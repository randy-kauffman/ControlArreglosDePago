# Procedimiento de instalación en un entorno nuevo (Test o Prod)

Inventario 13.15. Versión 2 (2026-11-04), revisada contra el repositorio después de los cambios del 2026-11-03 y 2026-11-04 (documento firmado, regla D-43, etiquetas en español). Escrita a partir de lo construido y probado en Dev. **Todavía no se ejecutó en un entorno nuevo**: la primera corrida es la que lo valida; se corrige este documento con lo que salga.

**Resumen del orden:** 1 prerrequisitos, 2 importar, 3 usuarios y seguridad, 4 variables y conexiones, 5 cola y buzón, 6 plantilla, 7 flujos de monitoreo, 8 purgas, 9 humo. El modo flujos (sección 11) reemplaza solo el paso 5.3.

## 0. Reglas antes de empezar

- **Qué viaja y qué no.** La solución `sanic_carp_sol_controlarreglospago` viaja **managed** con todos sus componentes:
  - tablas y choices;
  - roles y perfil de columnas;
  - paquete de plugins, Custom API y steps;
  - web resources, formularios, vistas, gráficos, panel y app;
  - variables de entorno (sin valor), referencias de conexión y flujos.

  **No viajan**, porque son datos o configuración del entorno: la BU, los teams (y la pertenencia de los teams al perfil de columnas), la cola, el buzón, los usuarios, la plantilla con su mapeo, las conexiones, los valores de las variables, los trabajos de purga y los ajustes de la organización (pasos 3.5 y 5.4). Ver la definición, §6 "Entornos y despliegue".
- **Qué no se necesita de Dev.** Para instalar no hace falta nada de lo que solo existió en Dev para pruebas: los spikes (`spikes/`), el rol `SPK Oficial Correo`, las variables `sanic_carp_ev_spk*`, la tabla descartable `sanic_carp_tbl_zzarchivo` ni los datos `HUMO-`. Ninguno de esos componentes debe viajar en la solución (ver la comprobación previa del paso 2.1).
- **Herramientas.** Las de `herramientas/construir/` **construyen solo en Dev**. En Test y Prod solo se usan:
  - los scripts de datos y de configuración que dice cada paso;
  - las consultas de verificación (solo lectura).
- **Apuntar los scripts al entorno nuevo.** Los scripts leen la conexión de un archivo de configuración: `PP_SECRETS_FILE` o `~/projects/pp_secrets.env`, con `DATAVERSE_URL`, `PP_TENANT_ID`, `PP_CLIENT_ID` y `PP_CLIENT_SECRET`. Use un archivo **aparte por entorno** y exporte `PP_SECRETS_FILE` antes de cada sesión. Nunca imprima ni copie su contenido. El service principal de esos scripts necesita un rol con privilegios de administración en el entorno nuevo (por ejemplo System Administrator), y el script de la cola (paso 5.2) además lee del mismo archivo la clave `PP_MAIL_BUZON_1` (la dirección del buzón del área).
- **Los scripts escriben en el repositorio.** Los de `pruebas-humo/` guardan su evidencia en carpetas del repositorio (por ejemplo `pruebas-humo/lote-0/evidencia/` y `estado.json`), que ya contienen la evidencia de Dev, y el repositorio es público. Ejecútelos en una copia de trabajo, copie la salida a `pruebas-humo/instalacion-<entorno>/` y descarte el resto (`git restore`). No suba direcciones de correo ni identificadores del entorno.
- **Las herramientas de `herramientas/construir/` se niegan a trabajar sobre una solución managed.** Todas comprueban primero que la solución no sea managed y, si lo es, devuelven `bloqueado` (`herramientas/construir/_comun.py`, `comprobar_solucion_e_idioma`). Eso afecta a `datos.py` (paso 6), a `flujo.py` y a `importar_solucion.py`. Cada paso lo dice donde corresponde.
- **Orden.** Los pasos van en orden de dependencia. No avance si la verificación de un paso no da lo esperado.
- **Evidencia.** Guarde la salida de cada verificación en `pruebas-humo/instalacion-<entorno>/`, sin secretos ni datos de clientes.

## 1. Prerrequisitos (13.1 a 13.3, 13.6, 13.10, 13.16, 13.17)

| # | Tarea | Responsable | Verificación |
|---|---|---|---|
| 1.1 | BU `Arreglos de Pago` como hija de la BU raíz (13.1) | Admin. de Power Platform | `GET businessunits?$filter=name eq 'Arreglos de Pago'` da una fila |
| 1.2 | Team del sistema de la BU (se crea con la BU, se llama `Arreglos de Pago`) y team `Arreglos de Pago - Jefatura` dentro de la BU (13.2, 13.3). En Test y Prod, `Arreglos de Pago - Jefatura` es un team de grupo de Entra ID (tipo Grupo de seguridad de Microsoft Entra) | Admin. de Power Platform | `GET teams?$filter=_businessunitid_value eq <bu>&$select=name,isdefault,teamtype` da el team por defecto (`isdefault = true`) y `Arreglos de Pago - Jefatura` |
| 1.3 | Grupo de Entra para los datos sensibles (cédula y cuenta), con los usuarios de ARP | Admin. de Azure | El grupo existe con los miembros correctos |
| 1.4 | **Cuenta de servicio dedicada (13.6):** usuario con licencia, **dentro de la BU**, con `SR - CARP - Servicio` (6.4) y `SR - CARP - Purga` (6.5); dueña de los flujos y de los trabajos de purga. En Dev se usó la cuenta de administración en la BU raíz como **desviación**; en Test y Prod no se repite | Admin. de Power Platform | `GET systemusers(<id>)?$select=isdisabled,_businessunitid_value&$expand=systemuserroles_association($select=name)` |
| 1.5 | Capacidad de base de datos y de archivos del entorno (13.16) | Admin. de Power Platform | Reporte de capacidad del tenant |
| 1.6 | **Descargas y OneDrive (13.17).** Confirmar con TI si la carpeta Descargas está redirigida a OneDrive. Si lo está, el gesto Generar formato no se habilita con datos reales hasta resolverlo (RNF-03) | Equipo de automatización, TI | Respuesta escrita de TI |
| 1.7 | **Ajustes de la organización que la solución usa y no cambia (13.10).** Con el administrador, confirmar: la correlación de correos por encabezados (`In-Reply-To`) está activa (es el valor por defecto); `MaxUploadFileSize` (5 MB por defecto) y la lista de extensiones bloqueadas son aceptables para los adjuntos del área (RNF-11, D-34); y **la auditoría de la organización está iniciada** (Configuración → Auditoría → Iniciar auditoría): sin ella la solución marca las tablas y columnas como auditadas pero no se registra nada, y la prueba 7 de la sección 9 falla. La retención de los logs de auditoría la decide Cumplimiento (C-02, 13.11) y sigue abierta | Admin. de Power Platform | `GET organizations?$select=isauditenabled,maxuploadfilesize,blockedattachments` da `isauditenabled = true` y valores aceptados por el área |

## 2. Importar la solución managed

1. **Comprobar en Dev, antes de exportar,** que la solución no arrastra restos de pruebas:
   - `GET solutioncomponents` de la solución no incluye la tabla descartable `sanic_carp_tbl_zzarchivo` (ensayo del 2026-11-04, `playbooks/tabla/ensayos/sanic_carp_tbl_zzarchivo.md`), que en Dev pertenece a la solución y la borra el usuario; ni ninguna tabla, columna o variable cuyo nombre empiece por `zz` o `spk`;
   - los 38 steps están habilitados (ver la verificación de abajo).
2. Exportar desde Dev la solución como **managed**, después de publicar todas las personalizaciones. Anote la versión de la solución exportada.
3. Importarla en el entorno nuevo con `pac solution import` o con el portal, con **"Activar procesos y complementos"**.
4. Durante la importación, el portal pide los **valores de las variables** y las **conexiones**. Puede dejarlos para el paso 4 (hay 6 variables obligatorias y 2 referencias de conexión).
5. Publicar todas las personalizaciones.

**Verificación:**
- `GET solutions?$filter=uniquename eq 'sanic_carp_sol_controlarreglospago'&$select=version,ismanaged` da `ismanaged = true` y la versión exportada.
- **Paquete de plugins.** El paquete viaja dentro de la solución. Hay que comprobar dos cosas:
  - `GET pluginassemblies?$filter=name eq 'Sanic.Carp.Plugins'&$select=version` da **1.0.4.0** (`playbooks/paquete-plugins/Sanic.Carp.Plugins.md`; `src/Sanic.Carp.Plugins/Sanic.Carp.Plugins.csproj`, `AssemblyVersion`). Si se desplegó otra versión en Dev después de escribir este documento, el valor esperado es el del `.csproj`;
  - la versión del paquete (`pluginpackages`, `sanic_carp_pkg_plugins`) sigue en 1.0.0.
- **Steps.** Tienen que estar habilitados los **38** steps de `playbooks/steps/sanic_carp_pkg_plugins.md` (22 clases) y los 5 que la plataforma crea para las Custom API (43 en total).
  - `GET sdkmessageprocessingsteps?$filter=startswith(name,'STEP - CARP')&$select=name,statecode` da 38 filas, todas con `statecode = 0`. Ninguna con `statecode = 1`.
  - Los 5 steps de las Custom API no llevan ese prefijo: se cuentan por su tipo de plugin (clases `Api.*` del paquete). Si no sabe cuáles son, compare contra Dev.
  - En Dev, una importación de ribbon apagó los steps (los 37 de entonces y después los 38). `herramientas/construir/importar_solucion.py` los rehabilita (`statecode 0`, `statuscode 1`), pero esa herramienta **no corre sobre un entorno managed**. En el entorno nuevo, si alguno quedó deshabilitado: portal → Soluciones → Pasos de procesamiento de mensajes SDK → Activar. No verificado: si la plataforma deja activar desde el portal un step que viene managed; si no deja, hay que volver a importar la solución.
- **Custom API.** Las 5 `customapis` con `uniquename` `sanic_carp_capi_*` existen.
- **Claves alternativas.** Las 3 claves (`sanic_carp_key_*`) quedan en estado Activa; la plataforma construye sus índices en segundo plano y puede tardar. Se ve en Soluciones → Tablas → Claves, o con `GET EntityDefinitions(LogicalName='<tabla>')/Keys?$select=SchemaName,EntityKeyIndexStatus` (se espera `Active`). No siga con la carga de datos hasta que estén activas.
- **Regla de un solo Vigente por crédito (D-43).** La imponen los plugins (`CARP-ARR-030` a `-032`); no hay clave ni restricción de base de datos. Un entorno nuevo no tiene datos, así que no hace falta ninguna comprobación previa. Si algún día se importan arreglos de otro entorno, hay que revisar antes que no haya dos Vigentes del mismo crédito ni un número usado con dos productos.
- **Etiquetas de la app.** Abra la app con un oficial: los botones, el menú y los tooltips tienen que estar en español aunque la interfaz de Dynamics esté en inglés (decisión del 2026-11-03, `diseno/07` §13). Si algo sale en inglés (por ejemplo "Void" en lugar de "Anular"), recargue con Ctrl+F5. Si persiste, un administrador abre la app con `&ribbondebug=true` en la URL, Command Checker, **Regenerate ribbon metadata**, y recarga con Ctrl+F5 (`pruebas-humo/lote-b/RESULTADO.md`). No se probó en un entorno nuevo si esa regeneración hace falta tras importar la solución managed.

## 3. Usuarios y seguridad (13.7, 13.8, 13.9)

1. **Mover a los usuarios de ARP a la BU** (13.7):
   - Antes de mover a nadie, guarde la línea base de cada uno (BU, roles y teams). **Mover a un usuario de BU le quita los roles que tiene hoy.** Avíseles antes.
   - El script `pruebas-humo/lote-0/scripts/l03_usuarios.py` es **solo una referencia de cómo se hizo en Dev**: tiene escritos los dos usuarios de prueba de Dev y una comprobación que se detiene si el entorno no es el de Dev (`assert dv.org == ...`). **No se ejecuta tal cual en un entorno nuevo**; hay que copiarlo y cambiar esas constantes, o hacer los pasos a mano en el centro de administración (Usuarios → Cambiar unidad de negocio; Teams → Administrar miembros).
2. **Roles por team, no por usuario** (matriz `diseno/04-matriz-privilegios.md` §1.2):
   - el team del sistema de la BU (`Arreglos de Pago`) lleva `SR - CARP - Oficial`;
   - `Arreglos de Pago - Jefatura` lleva `SR - CARP - Jefe`;
   - los oficiales los heredan por estar en la BU, y el jefe además entra a `Arreglos de Pago - Jefatura` (AddMembersTeam; si el team es de grupo de Entra ID, se entra al grupo).
   - Los roles de la solución son `SR - CARP - Oficial`, `Jefe`, `Servicio`, `Purga` y `Servicio flujos`; este último solo se asigna en modo flujos (sección 11). **Verificación:** `GET roles?$filter=startswith(name,'SR - CARP')&$select=name,ismanaged` da los 5 roles, `ismanaged = true`, y los dos de los teams quedan asociados a su team (`GET teams(<id>)/teamroles_association?$select=name`).
3. **Perfil de columnas** `CSP - CARP - Datos sensibles` (13.8). El perfil y sus **cuatro** permisos viajan en la solución; **sus miembros no**: los teams `Arreglos de Pago` (el del sistema) y `Arreglos de Pago - Jefatura` no son parte de la solución y hay que asociarlos al perfil **a mano en cada entorno**: centro de administración → Entorno → Configuración → Usuarios y permisos → Perfiles de seguridad de columnas → `CSP - CARP - Datos sensibles` → Teams → Agregar teams.
   - Las columnas protegidas son cuatro: `sanic_cedula` (Cliente), y `sanic_cuentadebitar`, `sanic_instantaneacedula` y `sanic_documentofirmado` (Arreglo). La última es el documento firmado (RF-18, 2026-11-03). Permisos: leer en las cuatro; crear en `sanic_cedula` y `sanic_cuentadebitar`; actualizar en las cuatro. "Leer sin enmascarar" queda en No (`playbooks/perfil-columnas/csp_carp_datos_sensibles.md`).
   - **Verificación de los permisos:** `GET fieldsecurityprofiles?$select=fieldsecurityprofileid&$filter=name eq 'CSP - CARP - Datos sensibles'` da una fila, y `GET fieldpermissions?$select=entityname,attributelogicalname,canread,cancreate,canupdate&$filter=_fieldsecurityprofileid_value eq <id>` da **4** permisos (los de la lista de arriba).
   - **Verificación de la pertenencia:** `GET fieldsecurityprofiles(<id>)?$expand=teamprofiles_association($select=name)` da los dos teams.
   - Sin la pertenencia, un oficial no ve la cédula y **tampoco puede adjuntar el documento firmado ni formalizar** un arreglo (la plataforma rechaza la escritura sobre una columna protegida).
   - Pruebe de punta a punta lo que dice 13.8 (entrar al grupo de Entra permite leer la cédula, salir deja de permitirlo). Ver la pregunta abierta sobre qué grupo gobierna el acceso.
   - **Verificación:** con un oficial, `GET sanic_carp_tbl_arreglos?$select=sanic_cuentadebitar&$top=1` da 200 con valor; sin el perfil, el valor viene vacío o la lectura falla. La prueba 8 de la sección 9 repite esto con la cédula, y la 12 con el documento firmado.
4. **Verificación 13.9.** Con el administrador, confirmar que **ningún rol ajeno** tiene profundidad Organización o BU padre sobre Email, Attachment, `msdyn_richtextfiles`, Queue, Queue Item, Mailbox ni las tablas `sanic_carp_*`. Guarde la evidencia. Se hace antes de cargar datos reales y antes de cada pase a Prod.

**Privilegios que se agregaron en Dev y viajan en el rol Oficial:**
- `prvReadRecordAuditHistory` (Global): ver el historial de auditoría del arreglo.
- `prvReadMailbox` (Básico): el dueño de una cola tiene que poder leer su Mailbox.

No hace falta tocarlos, pero conviene confirmarlos en la muestra del rol (`playbooks/rol/muestras/sr_carp_oficial.solucion.xml`).

## 4. Variables de entorno y conexiones (13.13)

| Inv. | Variable o referencia | Valor en el entorno |
|---|---|---|
| 10.1 | `sanic_carp_ev_nombrebu` | `Arreglos de Pago` (nombre exacto de la BU) |
| 10.2 | `sanic_carp_ev_nombreteamjefatura` | `Arreglos de Pago - Jefatura` (nombre exacto del team) |
| 10.3 | `sanic_carp_ev_destinatariosalertas` | Correos del jefe y del equipo de automatización, separados por `;`. En Test, solo buzones de prueba |
| 10.4 | `sanic_carp_ev_umbralesmonitoreo` | `{"horasSinEntrada":4,"ventanaErroresMin":35,"horasPurga":24,"minutosReconciliar":10}`; `horasSinEntrada` a confirmar con el jefe (DA-03) |
| 10.8 | `sanic_carp_ev_buzonarea` | Dirección del buzón del área **de este entorno** (en Prod, el buzón real; en Test, uno de prueba) |
| 10.9 | `sanic_carp_ev_modocorreo` | `sss` (camino de producción) o `flujos` (demo temporal, sección 11). Es obligatoria: con otro texto o vacía, el flujo ENT no hace nada |
| 10.6 | `CONR - CARP - Dataverse` (`sanic_carp_conr_dataverse`) | Conexión de Dataverse **creada por la cuenta de servicio** |
| 10.5 | `CONR - CARP - Outlook` (`sanic_carp_conr_outlook`) | Conexión de Office 365 Outlook **creada por la cuenta de servicio** |

- Son **6 variables**, todas obligatorias y sin valor por defecto: hay que cargar las seis. Los valores que aparecen en los playbooks de `playbooks/variable-entorno/` son los de Dev; no los copie.
- Los valores se cargan en el portal: Soluciones → Variables de entorno → Valor actual.
- Las conexiones **solo las puede crear la propia cuenta de servicio**, iniciando sesión en make.powerapps.com: Soluciones → Referencias de conexión → elegir o crear la conexión → Guardar.
- No se pueden enlazar por API con una conexión de otra persona: Dev lo rechazó con `ConnectionAuthorizationFailed`. Los playbooks están en `playbooks/variable-entorno/` y `playbooks/referencia-conexion/`.
- **Verificación:** `GET environmentvariabledefinitions?$select=schemaname,environmentvariabledefinitionid&$filter=startswith(schemaname,'sanic_carp_ev_')` da 6 definiciones (ninguna `sanic_carp_ev_spk*`), y `GET environmentvariablevalues?$select=value,_environmentvariabledefinitionid_value` da un valor por cada una. `GET connectionreferences?$filter=startswith(connectionreferencelogicalname,'sanic_carp_conr_')&$select=connectionreferencelogicalname,connectionid` da 2 filas con `connectionid` no vacío.

## 5. Cola y buzón del área (13.4, 13.5)

> **Elija el modo antes de empezar este paso.** Este paso es el camino principal y de producción: sincronización del lado del servidor (SSS, `sanic_carp_ev_modocorreo` = `sss`). El **modo flujos** (sección 11) es una demo temporal para cuando TI de BAC todavía no aprobó la cola y la sincronización del servidor. En cada buzón se usa **uno solo** de los dos, nunca los dos a la vez. Si elige modo flujos, haga los pasos 5.1 (con Full Access y Send As, ver 11.1) y 5.2 **a mano**, **no ejecute** el script `l01_cola_buzon.py` (deja el buzón en SSS) y siga la sección 11 en lugar del paso 5.3. Todos los demás pasos de este documento son iguales en los dos modos.

> ⚠️ **Antes de conectar el buzón real.** La primera sincronización **importa toda la bandeja de entrada que ya tiene el buzón**, y por cada correo se crea una conversación. En Dev entraron 10 correos viejos y se crearon 10 conversaciones (`pruebas-humo/tanda3/RESULTADO.md`). En Prod hay que **vaciar o archivar la bandeja del buzón real** antes del paso 5.3, o aceptar por escrito esa carga inicial con el jefe.

1. **Buzón de Exchange** (13.5, Mensajería y TI):
   - buzón compartido del área, uno por entorno;
   - **Send As** para la identidad de sincronización;
   - acceso restringido en Exchange. En Dev no se configuró en el buzón de prueba; en Prod es obligatorio.
2. **Cola** `Arreglos de Pago` (13.4):
   - en la BU, con el **team del sistema** de la BU como dueño, pública, con filtro de entrada "All email messages";
   - si el rol Oficial no tuviera `prvReadMailbox`, la creación da 403;
   - script de referencia: `pruebas-humo/lote-0/scripts/l01_cola_buzon.py`, idempotente, con `--solo-verificar`. Lee del archivo de configuración la clave `PP_MAIL_BUZON_1` (la dirección del buzón del área), busca la BU `Arreglos de Pago` y su team por defecto, crea la cola y además hace **todo el paso 5.3** (SSS, aprobación y Test & Enable). Solo para SSS.
3. **Registro Mailbox** de la cola:
   - perfil "Microsoft Exchange Online", sincronización del servidor para entrada y salida;
   - **Aprobar correo**, porque el entorno puede exigirlo (`requireapprovalforqueueemail`);
   - después, **Test & Enable**.
   - El script del paso 5.2 hace todo esto por API. Si la aprobación falla por permisos de Exchange, hágalo en el admin center: Configuración → Correo electrónico → Buzones → Arreglos de Pago → **Aprobar correo** → **Probar y habilitar buzón**.
4. **Destinatarios no resueltos** (obligatorio, vale para SSS y para el modo flujos):
   - en Configuración → Administración → Configuración del sistema → pestaña Correo electrónico (en el centro de administración: Entorno → Configuración → Producto → Correo electrónico), poner *Permitir mensajes con destinatarios de correo electrónico no resueltos* en **Sí** (`organization.allowunresolvedpartiesonemailsend = true`);
   - sin esto, el Oficial **no puede responderle a ningún cliente externo**: el Cliente (`sanic_carp_tbl_cliente`) no está habilitado para correo, así que la dirección del cliente nunca se resuelve a un registro, y la app rechaza el envío con *"Your organization doesn't allow emails to be sent to unresolved email recipients"*;
   - es una configuración de toda la organización. La activa el administrador del entorno;
   - hallazgo de la prueba guiada en Dev (2026-11-03, etapa 4): F4 no lo detectó porque los buzones de prueba son contactos. Por eso la prueba 2 de la sección 9 tiene que responder a una dirección que **no** sea un contacto ni un usuario del entorno.

**Verificación:**
- La cola existe, su dueño es el team del sistema y está en la BU del área. **Ojo:** la BU de la cola se lee de `_owningbusinessunit_value`, porque `_businessunitid_value` viene vacío.
- El buzón tiene `incomingemailstatus = 1` y `outgoingemailstatus = 1` (Éxito).
- Las alertas del buzón se leen en `tracelogs`, no en `traces`, que da 404.
- `sanic_carp_ev_buzonarea` es igual a la dirección del buzón.
- `allowunresolvedpartiesonemailsend = true` en `organizations`.

## 6. Plantilla del formato (9.1, 9.2)

Recarga controlada (OA-4), sin la herramienta de migración de configuración. Requiere los pasos 3 (el jefe tiene que existir y tener `SR - CARP - Jefe`) y 2 (tablas y claves).

> **Bloqueo conocido, decidir antes de instalar.** `datos.py` (y con él `--solo-verificar`) se niega a trabajar si la solución del entorno es managed (`comprobar_solucion_e_idioma`, `herramientas/construir/_comun.py`): devuelve `bloqueado`. Como en Test y Prod la solución es managed, **el comando de abajo no corre tal cual**. No hay hoy un camino probado. Ver la pregunta abierta 1 del informe de revisión: la recomendación es agregar a `datos.py` una bandera explícita que omita solo esa comprobación (el script solo escribe filas de datos, no componentes de la solución). Mientras no exista, la alternativa sin código es instalar la primera vez como unmanaged.

1. Ejecute las dos llamadas de datos, en este orden:
   - `python3 herramientas/construir/datos.py playbooks/datos/versionplantilla.md`
   - `python3 herramientas/construir/datos.py playbooks/datos/mapeocelda.md`

   **No use** `pruebas-humo/lote-0/scripts/l10_plantilla_v07.py`: es el script de Dev que además publica la v07 como administrador, crea un arreglo HUMO y depende de archivos de estado de Dev. El campo `entorno_url` de esos playbooks es el de Dev y es solo informativo: la herramienta usa `DATAVERSE_URL` del archivo de configuración. El resultado:
   - crea la versión `v07` en **Borrador**;
   - sube el archivo `F-NIC-0000052_v07.xlsx` (de `spikes/plantilla/F-NIC-0000052_v07_borrador.xlsx`, la versión aprobada el 2026-10-09), que se verifica bajándolo y comparando el SHA-256;
   - carga los **28 mapeos**.
2. El **jefe publica** la v07 desde la aplicación con el botón **Publicar versión** (gesto 13). Publicar valida el mapeo contra las hojas del archivo, calcula la huella y deja la v07 como Vigente.
3. **Compare la huella.** `sanic_huella` de la v07 publicada tiene que ser igual al SHA-256 del archivo aprobado (`sha256sum spikes/plantilla/F-NIC-0000052_v07_borrador.xlsx`) y a la de Dev. En Prod, además, igual a la de Test. Si difiere, no se usa el entorno.

**Verificación:** `python3 herramientas/construir/datos.py playbooks/datos/versionplantilla.md --solo-verificar` y lo mismo con `mapeocelda.md` dan `ya_existia`: 1 y 28 filas (sujeto al bloqueo de arriba). Con consultas: `GET sanic_carp_tbl_versionplantillas?$select=sanic_nombre,sanic_estado,sanic_huella` muestra la v07 Vigente y **una sola** Vigente.

## 7. Flujos de monitoreo (11.1, 11.2)

1. Confirmar que las referencias de conexión del paso 4 están enlazadas.
2. **Dueño de los flujos.** Un flujo que llega con una solución managed queda a nombre de quien importó. Para que sea la cuenta de servicio (13.6), importe la solución con esa cuenta o cambie el propietario de los flujos a esa cuenta antes de activar. No verificado en un entorno nuevo: Dev usó una suplantación que no se repite aquí.
3. Con la **cuenta de servicio**, en make.powerapps.com → Soluciones → **Flujos de nube**, **activar** los dos flujos:
   - `Cloud Flow - CARP - EST - Avisar estado del mailbox`;
   - `Cloud Flow - CARP - SAL - Salud del buzon`.

   La activación no se puede hacer por API con el service principal: el servicio de Power Automate la autoriza contra la identidad del token (`pruebas-humo/lote-d/RESULTADO.md`).
4. **Verificación:**
   - `GET workflows?$select=name,statecode,ownerid&$filter=category eq 5 and startswith(name,'Cloud Flow - CARP')` da 4 flujos. `EST` y `SAL` con `statecode = 1` (activados); `ENT` y `SND` con `statecode = 0` (apagados) en modo SSS. El `ownerid` de `EST` y `SAL` es la cuenta de servicio.
   - El script `pruebas-humo/lote-d/scripts/como_servicio.py` no sirve aquí: tiene escrita la cuenta de administración de Dev y las herramientas de flujos no trabajan sobre una solución managed.
   - El día hábil siguiente, revise la primera corrida de `SAL` en su historial.
5. **Riesgo D-10, a probar en Test antes de Prod.** Según Microsoft Learn, el dueño de un flujo con disparador de Dataverse (el flujo `EST`) necesita crear, leer, escribir y eliminar en **Callback Registration** a nivel Usuario, y `SR - CARP - Servicio` no los tiene. En Dev no se notó porque el dueño era administrador.
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
| 2 | El oficial **toma** la conversación y **responde** a una dirección externa que no es contacto ni usuario del entorno | La app no rechaza el envío (comprueba `allowunresolvedpartiesonemailsend`, paso 5.4). La respuesta sale con el buzón del área como remitente; se llenan "respondido por" y el tiempo de respuesta | tanda 3 (H-5, H-6, S-35) |
| 3 | El cliente responde | Entra a la misma conversación, no a una nueva | tanda 3 |
| 4 | El oficial intenta reasignar | Mensaje "Solo la jefatura puede reasignar una conversación o dejarla sin asignar." (código interno `CARP-CNV-008`, no se muestra). El jefe reasigna sin error | tanda 3 (2.4) |
| 5 | Registrar un arreglo HUMO, **generar formato**, adjuntar el **documento firmado** (un PDF ficticio) y **formalizar** | El `.xlsx` se descarga con las 28 celdas llenas y las fechas con formato `dd/mm/yyyy`; el arreglo queda Vigente con número. Formalizar sin el documento se rechaza ("Adjunte el documento firmado (PDF, JPG o PNG) antes de formalizar.") | `pruebas-humo/lote-0/RESULTADO-plantilla.md`, `pruebas-humo/tanda4/RESULTADO.md` |
| 6 | Registrar el pago de la cuota 1 y volver a generar | La fecha de pago aparece en "CONTROL DE PAGOS" | `RESULTADO-plantilla.md` |
| 7 | El oficial abre el historial de auditoría del arreglo | Se ve (200), sin error de permisos, y muestra los cambios de las pruebas 5 y 6 (requiere la auditoría de la organización iniciada, prerrequisito 1.7) | `pruebas-humo/lote-0/RESULTADO-auditoria.md` |
| 8 | El oficial lee la cédula y la cuenta a debitar | Se ven; un usuario fuera del grupo de Entra no las ve | paso 3.3 |
| 9 | El jefe abre el **Panel de supervisión** | Se ven los dos gráficos y las dos listas | `pruebas-humo/lote-e/RESULTADO.md` |
| 10 | Revisar los steps y los trabajos asíncronos | Los 38 steps `STEP - CARP` (más los 5 de las Custom API) están habilitados y no hay trabajos de plugins fallidos | paso 2 |
| 11 | Recargar la aplicación con Ctrl+F5 y probar Tomar desde la lista. Revisar que los botones estén en español | Aviso "Conversación tomada por <nombre>."; botones y menú en español (por ejemplo "Anular", no "Void") | `pruebas-humo/lote-b/RESULTADO.md`; paso 2 |
| 12 | El oficial (no administrador) adjunta el documento firmado en un borrador congelado y lo reemplaza por otro PDF; con un archivo `.txt` intenta formalizar | Sube y baja sin error de permisos (el oficial pertenece al perfil de columnas). Formalizar con el `.txt` se rechaza ("El documento firmado debe ser PDF, JPG o PNG."); con el PDF pasa | `pruebas-humo/tanda4/RESULTADO.md` (H-35, H-36); paso 3.3 |
| 13 | Regla D-43: crear un segundo borrador del mismo número de crédito y formalizarlo mientras el primero está Vigente; luego crear un arreglo con el mismo número pero otro producto | Formalizar el segundo se rechaza ("Este crédito ya tiene un arreglo vigente (<número>). Anúlelo antes de formalizar otro."). El arreglo con otro producto se rechaza ("El número <número> ya está registrado como <producto> en otro arreglo. Revise el producto o el número.") | `pruebas-humo/tanda4/RESULTADO.md` |

Si alguna prueba falla, **no** se cargan datos reales. Se registra en el `RESULTADO.md` de la instalación y se corrige este procedimiento.

## 10. Pendientes que este procedimiento no cierra

- 13.12: diseño detallado de los trabajos de purga.
- 13.14: pipelines de Power Platform. Mientras tanto, la importación es manual (paso 2).
- D-10: privilegios de Callback Registration para el dueño de los flujos (paso 7.5).
- DA-03: horario hábil y `horasSinEntrada`, a confirmar con el jefe.
- D-27: si el gráfico de tiempo total excluye las finalizadas "Sin respuesta", a confirmar con negocio.
- 13.11 / C-02: retención de los logs de auditoría; la define Cumplimiento (prerrequisito 1.7).
- Carga de la plantilla en un entorno con la solución managed: `datos.py` está bloqueado (paso 6).
- Dueño de los flujos tras importar una solución managed y habilitación de steps desde el portal (pasos 2 y 7): no verificados en un entorno nuevo.
- Qué grupo de Entra gobierna el acceso a la cédula y a la cuenta (prerrequisito 1.3 y paso 3.3): ver las preguntas abiertas del informe de revisión.

## 11. Modo flujos (demo temporal, plan B de `diseno/08`)

El modo flujos deja que el negocio pruebe la solución y vea cómo trabajará SSS antes de que TI de BAC apruebe la cola y la sincronización del servidor. **Es una demo temporal, no el camino de producción.** La misma solución se instala en los dos casos: lo que cambia es la variable `sanic_carp_ev_modocorreo` (`sss` o `flujos`) y qué flujos están encendidos. Los plugins no cambian. **Cómo elegir:** SSS si TI de BAC ya aprobó la cola y el buzón (producción); flujos solo para una demo mientras esa aprobación no llega. En un entorno nuevo, instale todo con `sss` (valor del paso 4), haga la demo pasando a flujos con 11.2 y, cuando TI apruebe, vuelva con 11.3.

**Regla de oro: nunca SSS y flujos sobre el mismo buzón.** Duplican la entrada y la salida.

### 11.1 Prerrequisitos

| # | Qué | Responsable |
|---|---|---|
| 1 | **Full Access y Send As** del buzón compartido del área para la cuenta que será dueña de las conexiones de ENT y SND. Full Access sola no deja enviar | Administrador de Exchange |
| 2 | Esa cuenta tiene licencia de Exchange Online y **Power Automate Premium** (Dataverse es premium) | Admin. de licencias |
| 3 | DLP del entorno: **Office 365 Outlook y Dataverse en el mismo grupo** | Admin. de Power Platform |
| 4 | Las variables `sanic_carp_ev_buzonarea` y `sanic_carp_ev_nombrebu` tienen valor, y las referencias de conexión de Outlook y Dataverse están enlazadas con la cuenta del punto 1 (paso 4) | Admin. de Power Platform |
| 5 | La cola del área existe (paso 5.2, hecho a mano; no ejecute el script de la cola). Los flujos la buscan por la dirección del buzón | Admin. de Power Platform |
| 6 | El ajuste de destinatarios no resueltos del paso 5.4 está en Sí (vale también en modo flujos) | Admin. de Power Platform |

### 11.2 Pasar un entorno a modo flujos

1. **Apagar SSS del buzón del área, entrada y salida**: en el registro Mailbox de la cola, poner el método de entrega entrante y saliente en *Ninguno*. Lo hace el administrador. No se borra el buzón ni la cola. Si la cola es nueva y el buzón nunca se habilitó con SSS, no hay nada que apagar: solo confirme que ambos métodos están en *Ninguno* y que no se ejecutó Test & Enable.
2. Poner `sanic_carp_ev_modocorreo` = `flujos`.
3. **Asignar el rol `SR - CARP - Servicio flujos`** a la cuenta del prerrequisito 1, **además** de `SR - CARP - Servicio` (y del Oficial, que ya recibe por estar en la BU). Solo a esa cuenta. Si el rol ya estaba asignado y se cambió, quitarlo y volver a asociarlo (spike 4, hallazgo 1). Es un paso de administración, no de la solución.
4. **Una persona** (la dueña de las conexiones, desde make.powerapps.com → Soluciones → Flujos de nube) **enciende ENT y SND**:
   - `Cloud Flow - CARP - ENT - Capturar correo entrante`;
   - `Cloud Flow - CARP - SND - Enviar respuestas`.

   No se pueden encender por API con el service principal (`ConnectionAuthorizationFailed`).
5. **Verificación:** confirmar que `sanic_carp_ev_modocorreo` vale `flujos`; enviar un correo de prueba al buzón del área. En un minuto debe haber un email entrante con el team como dueño y su conversación. Responder desde la app: el email pasa de Pending Send a Sent y llega al buzón de prueba con el buzón del área como remitente. Si algo falla, el historial de ejecuciones del flujo dice dónde.
6. Los flujos EST y SAL pueden quedar encendidos. EST no aplica en modo flujos (sección 11.4); SAL sigue como está.

### 11.3 Volver a SSS

1. **Apagar ENT y SND.**
2. Poner `sanic_carp_ev_modocorreo` = `sss`.
3. Quitar el rol `SR - CARP - Servicio flujos` a la cuenta.
4. Volver a poner el método de entrega entrante y saliente del Mailbox en *Servidor de sincronización del lado del servidor*, aprobar el correo si el entorno lo exige y **ejecutar Probar y habilitar buzón (Test & Enable)**.

> **Test & Enable es obligatorio.** Cambiar `outgoingemaildeliverymethod` de un Mailbox con SSS, aunque se devuelva el valor original, **suspende la salida** hasta un nuevo Test & Enable (alerta `OutgoingMailboxUpdateError`, `outgoingemailstatus` = 0). La entrada sigue funcionando. Si se omite, las respuestas quedan en Pending Send (`spikes/flujos-correo/RESULTADO.md`, cierre de V-4).

### 11.4 Diferencias aceptadas frente a SSS

El negocio tiene que saber esto antes de juzgar la demo (`diseno/08` §6 y §11):

- **Correlación solo por In-Reply-To.** Una respuesta de un cliente de correo que no lo manda abre una conversación nueva. SSS correlaciona también por token y por conversación de Exchange.
- **Adjuntos `.msg`, `.eml` e `.ics`** de correos entrantes no llegan por el disparador.
- **Adjuntos de salida de 3 a 5 MB** no se envían: la respuesta termina en Failed. No hay carga por partes en el modo flujos.
- **Imágenes en línea y formato** del cuerpo pueden verse distintos; no se probaron caso por caso.
- **Latencia** de entrada y salida distinta a la de SSS, sin SLA en ninguno de los dos.
- **Monitoreo.** No hay muro de alertas del buzón ni panel de SSS. EST (aviso por estado del buzón) no aplica. Las fallas se ven en el historial de ejecuciones de ENT y SND y en la vista *Respuestas con error de envío*. SAL no se modificó para este modo.
- **Respuesta sin correo original.** Si el original no aparece en el buzón, la respuesta sale como mensaje nuevo (sin hilo en Exchange). Esa rama no está validada en Dev.
- **Operación.** El equipo mantiene los dos flujos; los flujos dependen de la cuenta y de la licencia de una persona.
