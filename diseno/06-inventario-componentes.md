# 06. Inventario de componentes

Proyecto: 2026-003-control-arreglos-pago · Etapa 3 · Estado: **borrador** (2026-10-09; ajustado el 2026-10-10 por las decisiones OA-1 a OA-12 de `03-contratos-custom-api.md`; actualizado el 2026-10-20: grupo 7 hecho en código). Completo respecto de lo que las fuentes dicen. Lo que la definición describe sin diseño detallado queda marcado `diseño detallado pendiente` y no se resuelve acá.

Todo lo que hay que crear, en el orden en que se puede crear. Cada componente se construye a partir de un **playbook**: una especificación en Markdown, cerrada y sin ambigüedad, que ejecuta un agente constructor y audita un agente revisor.

Fuentes: `definicion.md` (§6, D-01 a D-36, RF y RNF), `diseno/01-convenciones.md` y `diseno/02-diccionario-datos.md`. Si una fuente no cierra, no se decide acá: queda en "Inconsistencias encontradas" (al final), con cita de línea.

## Cómo leer las tablas

- **Numeración fija.** 1.1, 2.1 a 2.9 y 3.1 a 3.8 ya se usan en playbooks y en Dev. Desde el grupo 4 numera este documento: una fila por componente con playbook propio. En el grupo 4, la fila `4.n` es la relación `REL-nn` del diccionario §6.2. En el grupo 5, `5.n` es la clave `K-0n` del diccionario §7.
- **Playbook.** Ruta `playbooks/<tipo>/<nombre>.md`. Los grupos 1 a 6 usan el nombre lógico del componente. En los grupos 7 a 13 el tipo de carpeta sigue el del proyecto 2026-001 (`custom-api`, `steps`, `paquete-plugins`, `variable-entorno`, `flujo`, `vista`, `formulario`, `app`, `datos`) y el nombre del archivo se confirma al redactar cada playbook. Donde no hay playbook se dice por qué.
- **Estado.** `hecho` (con fecha) · `pendiente` (el diseño alcanza para escribir el playbook) · `diseño detallado pendiente` (la definición nombra o implica el componente, pero faltan contratos, listas cerradas o parámetros).
- **Nombres provisionales.** Un nombre marcado *(prov.)* sale del texto de la definición aplicando las convenciones. No es un contrato: lo fija el diseño detallado.
- **Fase.** Una sola, F1 (definición §3, decisión del 2026-10-07).
- **Mecanismo de creación.** Web API de Dataverse con una herramienta por tipo de playbook (`herramientas/construir/`). Construyen solo en Dev; a Test y a Prod llega la solución managed. Excepciones: los datos de configuración y las tareas del grupo 13 se hacen en cada entorno (definición §6, Entornos y despliegue).

## Resumen

| Grupo | Tipo | Filas | hecho | pendiente | diseño detallado pendiente |
|---|---|---|---|---|---|
| 1 | Base (solución) | 1 | 1 | 0 | 0 |
| 2 | Choices globales | 9 | 9 | 0 | 0 |
| 3 | Tablas con sus columnas (7 propias + columnas sobre `email`) | 8 | 8 | 0 | 0 |
| 4 | Relaciones 1:N (con su lookup) | 19 | 19 | 0 | 0 |
| 5 | Claves alternativas | 3 | 3 | 0 | 0 |
| 6 | Seguridad: perfil de columnas (1) y roles (4) | 5 | 5 | 0 | 0 |
| 7 | Código de plugins (C#, TDD estricto) | 22 | 22 | 0 | 0 |
| 8 | Registro de paquete, Custom API y steps | 7 | 7 | 0 | 0 |
| 9 | Datos semilla y de configuración | 3 | 2 | 0 | 1 |
| 10 | Configuración entre entornos (EV y connection references; 10.7 retirada, no cuenta) | 7 | 7 | 0 | 0 |
| 11 | Flujos | 3 | 0 | 2 | 1 |
| 12 | App model-driven (vistas, dashboard, formularios, web resource, comandos, sitemap, app) | 26 | 26 | 0 | 0 |
| 13 | Identidades y puesta en marcha | 17 | 0 | 15 | 2 |
| **Total** | | **128** | **74** | **46** | **8** |

Notas del resumen:

- 2026-10-20: 13.15 pasó de `diseño detallado pendiente` a `pendiente` (`manual/procedimiento-instalacion.md` escrito; falta ejecutarlo en Test). El grupo 13 pasó de 14 pendientes y 3 de diseño detallado pendiente a 15 y 2; el total, de 45 y 9 a 46 y 8.

- 2026-10-20 (Dev, encendido del lote D): con las dos connection references ya enlazadas (la de Outlook la enlazó el usuario con la cuenta de servicio), activar 11.1 y 11.2 por la herramienta da `ConnectionAuthorizationFailed`: Power Automate autoriza contra el service principal y `CallerObjectId` no alcanza. Las definiciones coinciden con los playbooks (`difiere` solo por el estado). 11.1 y 11.2 siguen `pendiente` hasta que la cuenta de servicio las active desde el portal; el resumen no cambia (`pruebas-humo/lote-d/RESULTADO.md`).
- 2026-10-20 (Dev, 13.7 y tanda 3 de correo): 13.7 quedó **hecha en Dev** con `# User 8` (Oficial) y `# User 9` (Jefe), decisión del usuario del 2026-10-20: movidos a la BU `Arreglos de Pago` (la plataforma les quitó sus roles de la BU raíz; línea base en `pruebas-humo/lote-0/evidencia/04_usuarios_linea_base.json`), sin roles directos (Oficial por el team del sistema, Jefe por Jefatura, a la que se sumó User 9; perfil de columnas por los dos teams), y verificados `ya_existia` con `l03_usuarios.py --solo-verificar`. Como 13.1 a 13.5, sigue contando como `pendiente` porque faltan Test y Prod: el resumen no cambia. La tanda 3 de humo (`pruebas-humo/tanda3/RESULTADO.md`) confirmó con correo real H-5, H-6, H-25, H-26, H-27 (a, b, c), H-28 y el camino feliz de Iniciar conversación nueva; H-7 quedó parcial (la API como la cuenta de servicio, sin el flujo).
- 2026-10-20 (Dev, lote D): 10.3, 10.4, 10.5 y 10.6 pasaron a `hecho` (dos variables y dos connection references; herramienta nueva `referencia_conexion.py`, 12 pruebas). 10.5 quedó sin conexión: la asocia el usuario desde el portal. 11.1 (`CARP-EST`) y 11.2 (`CARP-SAL`) están construidos en la solución a nombre de la cuenta de servicio pero **apagados** hasta esa conexión, y siguen `pendiente`. 13.6 se cumple en Dev con desviación (admin del entorno en la BU raíz) y sigue contando como pendiente. El grupo 10 pasó de 3 hechas, 2 pendientes y 2 de diseño pendiente a 7, 0 y 0; el total, de 70, 47 y 11 a 74, 45 y 9.
- 2026-10-20 (Dev, lote E de app): 12.14 (`DASH - CARP - Supervision`), 12.15 (`CHT - CARP - Promedio por respuesta`) y 12.16 (`CHT - CARP - Tiempo total`) pasaron a `hecho`, construidos por Web API con un script versionado (DA-05 cambiada) y verificados `ya_existia`; la solución exportada trae los dos gráficos, el dashboard con sus etiquetas 3082 y el dashboard como componente de la app. La entrada `panel` del sitemap quedó con `app.py` (20 entradas, `ya_existia`). El grupo 12 pasó de 23 hechas y 3 pendientes a 26 y 0; el total, de 67 y 50 a 70 y 47 (`pruebas-humo/lote-e/RESULTADO.md`).
- 2026-10-20 (Dev, lote 0, correo): 13.4 y 13.5 quedaron **hechas en Dev** (queue y mailbox sobre el buzón de prueba 1, entrada y salida Success, `pruebas-humo/lote-0/RESULTADO.md`). Como 13.1 a 13.3, siguen contando como `pendiente` porque faltan Test y Prod: el resumen no cambia (67 hechas, 50 pendientes). Para destrabar 13.4 se agregó `prvReadMailbox` Básico a `SR - CARP - Oficial` (decisión del usuario, opción A; `04` §3 y §8).
- 2026-10-20 (Dev, lote 0): 9.1 y 9.2 pasaron a `hecho` (v07 aprobada por Héctor cargada y publicada como Vigente, la v95 de humo pasó a Histórica; 28 de 28 mapeos `ya_existia`; el formato generado llena las 28 celdas, `pruebas-humo/lote-0/RESULTADO-plantilla.md`) y 10.8 también. El grupo 9 pasó de 0 hechas y 2 pendientes a 2 y 0; el grupo 10, de 2 y 3 a 3 y 2; el total, de 64 y 53 a 67 y 50. 13.4 quedó bloqueada: el dueño de una queue tiene que poder leer su Mailbox (403 sin `prvReadMailbox`) y el team del sistema solo tiene el rol Oficial, sin acceso al Mailbox según `04`; espera decisión del usuario (`pruebas-humo/lote-0/RESULTADO.md`).

- 2026-10-20 (Dev, lote C de app): 12.3, 12.4, 12.6, 12.7, 12.8 y 12.9 pasaron a `hecho` (6 vistas: `creado` y luego `ya_existia` con `--solo-verificar`; el `?savedQuery=` da 200 en las 6). 12.4 no admite el filtro `regardingobjecttypecode` con el nombre de la tabla (HTTP 400, espera el entero ObjectTypeCode, que cambia de entorno a entorno): se usó el respaldo del diseño, un enlace `inner` a Conversación. Además se reconstruyeron con `--completar`, todos `ya_existia`: el web resource `sanic_carp_wr_js_comandos` (38591 bytes, `SanicCarp.Conversacion.alCargar` con el aviso de otra conversación abierta, DA-06), 12.18 (manejador `onload`), 12.19 (cuatro pestañas) y 12.23/12.25 (19 entradas, las tres nuevas de Supervisión con `Privilege`). El grupo 12 pasó de 17 hechas y 9 pendientes a 23 y 3 (quedan 12.14 a 12.16, lote E); el total, de 58 y 59 a 64 y 53. No se hizo "ocultar botones estándar" (DA-15, G-I3) porque el lote lo pedía como opcional y no tiene fila propia. Los 42 steps del paquete siguen con `statecode` 0. Detalle en `pruebas-humo/lote-c/RESULTADO.md`.

- 2026-10-20 (Dev, lote B de app): 12.24 pasó a `hecho`: el web resource se actualizó con los 14 gestos (`difiere` por contenido y descripción, `--completar`, `actualizado`, `ya_existia`, publicado) y el ribbon se importó con `importar_solucion.py` (`difiere`, `creado`, `ya_existia`): 13 botones en Conversación (5), Arreglo (6) y Versión de plantilla (2), con comando, `PrimaryControl` en los de Form y regla, y las 26 funciones referenciadas existen en el JS publicado. 12.22 sigue `hecho` (contenido actualizado). El grupo 12 pasó de 16 hechas y 10 pendientes a 17 y 9; el total, de 57 y 60 a 58 y 59. Pendiente fuera de la fila: el paso manual de regenerar la metadata del ribbon y la prueba en el navegador. Prueba funcional por Web API: 28 de 30 comprobaciones; no se pudo probar con el administrador Reabrir arreglo (CARP-ARR-016, solo jefatura) ni el camino feliz de Iniciar conversación nueva (necesita un correo real). Detalle en `pruebas-humo/lote-b/RESULTADO.md`.

- 2026-10-20 (Dev, cierre del lote A de app): 12.17 a 12.21, 12.23, 12.25 y la fila 12.26 (formulario de Cuota, que `diseno/07` DA-09 agregó y esta tabla no tenía) pasaron a `hecho`, las 8 verificadas `ya_existia` con `--solo-verificar`. Las dos causas de que quedaran en `difiere` eran de las herramientas, no del entorno: (1) `GET systemforms` devuelve el `formxml` solo con las etiquetas del idioma del usuario (1033); las 3082 SÍ estaban guardadas (la solución exportada trae las dos), y `formulario.py` ahora las verifica contra la exportación y publica también en el alta; (2) `AddAppComponents` con la tabla `entity` ignora el GUID y agrega siempre la tabla de metadatos `entity`, así que `app.py` ya no lo usa para tablas: las mete por `ImportSolution` (`importar_solucion.importar_tablas_de_la_app`). El grupo 12 pasó de 8 hechas y 17 pendientes (25 filas) a 16 y 10 (26 filas); el total, de 49 y 67 (127 filas) a 57 y 60 (128). Detalle en `pruebas-humo/lote-a/RESULTADO.md`.

- 2026-10-20 (Dev, construcción del lote A de app): 12.22 pasó a `hecho` (el JS y los 4 SVG, verificados `ya_existia`; los SVG no tienen fila propia, ver la fila 12.27 propuesta). El grupo 12 pasó de 7 hechas y 18 pendientes a 8 y 17 (el total, de 48 y 68 a 49 y 67). Las filas 12.17 a 12.21 (6 formularios), 12.23 (sitemap) y 12.25 (app) **se construyeron y publicaron en Dev pero siguen `pendiente`** porque la herramienta no las da por verificadas: `formulario.py` exige la etiqueta embebida 3082 y la plataforma solo conserva la 1033 en el `formxml` de `systemforms`; `app.py` ve el sitemap bien pero `AddAppComponents` devuelve 204 sin agregar las 8 tablas. Detalle en `pruebas-humo/lote-a/RESULTADO.md`.

- 2026-10-20 (Dev, cierre del lote A de vistas): 12.10, 12.11 y 12.12 pasaron a `hecho` tras corregir `vista.py` (búsqueda rápida: `PATCH` con los cuatro campos; pertenencia a la solución por la fila propia de la vista) y volver a verificar `Respuestas enviadas` (`ya_existia`). El grupo 12 pasó de 4 hechas y 21 pendientes a 7 y 18 (el total, de 45 y 71 a 48 y 68).

- 2026-10-20 (Dev, vistas del lote A): 18 vistas creadas y verificadas `ya_existia` con `vista.py` (12.1, 12.2, 12.5 y las 15 de 12.13, de las que `Respuestas enviadas` queda en `difiere` por un falso positivo de la herramienta sobre `email`, ver la fila 12.13); las 4 filas pasaron a `hecho`. Quedan sin construir 12.10 y 12.11 (búsqueda rápida: defecto de `vista.py`, ver sus filas) y 12.12 (hueco de diseño). El resumen del grupo 12 pasó de 0 hechas y 25 pendientes a 4 y 21 (el total, de 41 y 75 a 45 y 71).

- 2026-10-20: `diseno/07-diseno-app-y-flujos.md` cierra el diseño detallado de los grupos 11 y 12. Las filas 11.1, 11.2 y 12.1 a 12.25 pasaron de `diseño detallado pendiente` a `pendiente`; el total pasó de 48 pendientes y 38 de diseño detallado pendiente a 75 y 11 (127 filas, 41 hechas, sin cambio). 11.3 sigue como condicional (no se construye). «Pendiente» no quiere decir que el playbook se pueda escribir ya: varias filas necesitan antes una extensión de herramienta (`diseno/07` §2, G-V1 a G-I3). El diseño propone tres filas nuevas que **no suman** hasta que se confirmen: 12.26 formulario de Cuota, 12.27 cuatro iconos SVG y 12.28 importación de las tablas de la app (`diseno/07` §11).

- 2026-10-20 (Dev): el grupo 8 (8.1 a 8.7) pasó a 7 hechas: paquete 1.0.0, 5 Custom API y 37 steps registrados y verificados `ya_existia`. Además se cargaron las variables 10.1 (`sanic_carp_ev_nombrebu`) y 10.2 (`sanic_carp_ev_nombreteamjefatura`), que pasaron de `diseño detallado pendiente` a hechas. El total cambió de 32 hechas, 55 pendientes y 40 de diseño detallado pendiente a 41, 48 y 38 (127 filas, sin cambio). Quedan sin cargar 10.8 (`sanic_carp_ev_buzonarea`, depende del buzón real de la cola del área; la exigen los correos salientes, no S-34), 10.3 y 10.4 (sin valor definido).

- 2026-10-20: el grupo 7 pasó de 0 hechos, 9 pendientes y 13 de diseño detallado pendiente a 22 hechos, porque el código está construido y probado (5820 pruebas en verde, commit `397088d`). El total cambió de 10 hechos, 58 pendientes y 59 de diseño detallado pendiente a 32, 49 y 46; las 127 filas no cambian. "Hecho" del grupo 7 no incluye la verificación en la plataforma real, que sigue pendiente en el grupo 8 y en las pruebas de humo de `03-contratos-custom-api.md` §8.1.
- 2026-10-20: las filas 8.2 a 8.7 pasaron de `diseño detallado pendiente` a `pendiente`, porque `diseno/03-contratos-custom-api.md` §1, §4.1 y §5 ya fijan los contratos de las cinco API, la estructura del paquete y la lista cerrada de 37 steps. El grupo 8 pasó de 1 pendiente y 6 de diseño detallado pendiente a 7 pendientes; el total, de 49 pendientes y 46 de diseño detallado pendiente a 55 y 40 (127 filas, 32 hechas, sin cambio). «Pendiente» no quiere decir que el playbook esté listo: los pilotos de 8.1, 8.4 y 8.7 tienen huecos de diseño abiertos (`playbooks/`).
- Las filas 13.1 a 13.3 están hechas **solo en Dev** (2026-10-09, `01-convenciones.md` §7). Faltan Test y Prod, por eso cuentan como `pendiente`.
- La fila 10.7 quedó **retirada** el 2026-10-10 (OA-12) y no cuenta como fila activa; no se renumeraron las demás. Antes del cambio el total era 128 filas (59 pendientes) y el grupo 10 tenía 8 (4 pendientes).
- La fila 11.3 es condicional (el respaldo de D-11 no se activa) y cuenta como fila. Si no se activa, no se construye.
- Los pasos de familias de steps (8.7) y los gestos (12.24) son contenido de una sola fila: no suman filas.
- Cifras del diccionario que respaldan los grupos 2 a 6: 9 choices con 50 opciones; 7 tablas propias con 73 columnas y 18 lookups; 2 columnas sobre `email` (una es lookup); 19 relaciones que se crean y 1 nativa; 3 claves; 1 perfil con 3 columnas; 8 motivos de estado (diccionario §13, última línea).

## 1. Base

| # | Componente | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 1.1 | Solución `sanic_carp_sol_controlarreglospago` (display name `SOL - CARP - Control de Arreglos de Pago`), publisher `Sistemas_Abiertos_Nicaragua` | `playbooks/solucion/sanic_carp_sol_controlarreglospago.md` | publisher (creado) | Convenciones §0 y §2; definición §6 Entornos y despliegue | **hecho** (2026-10-09, creada y verificada en Dev) |

## 2. Choices globales

Sin dependencias entre sí; van primero porque las columnas los usan. Valores = `15946` + correlativo de 4 dígitos (`159460001`, …). Todos se crearon, se verificaron y tienen su muestra exportada (9 de 9) el 2026-10-09. Los playbooks y las muestras están en `playbooks/choice-global/`.

| # | Componente | Playbook | Opciones | Lo usa | Origen | Estado |
|---|---|---|---|---|---|---|
| 2.1 | `sanic_carp_ch_resultadoconversacion` | `playbooks/choice-global/sanic_carp_ch_resultadoconversacion.md` | 3 | 3.2 | RF-15, D-06 | **hecho** (2026-10-09) |
| 2.2 | `sanic_carp_ch_motivosinrespuesta` | `playbooks/choice-global/sanic_carp_ch_motivosinrespuesta.md` | 4 | 3.2 | RF-15, D-06 | **hecho** (2026-10-09) |
| 2.3 | `sanic_carp_ch_productocrediticio` | `playbooks/choice-global/sanic_carp_ch_productocrediticio.md` | 2 | 3.3, 5.2 | RF-06, RF-17 | **hecho** (2026-10-09) |
| 2.4 | `sanic_carp_ch_tipoarreglo` | `playbooks/choice-global/sanic_carp_ch_tipoarreglo.md` | 2 | 3.3 | RF-06 | **hecho** (2026-10-09) |
| 2.5 | `sanic_carp_ch_moneda` | `playbooks/choice-global/sanic_carp_ch_moneda.md` | 2 | 3.3 | RF-06, RF-08, D-36, DD-11 | **hecho** (2026-10-09) |
| 2.6 | `sanic_carp_ch_canalorigen` | `playbooks/choice-global/sanic_carp_ch_canalorigen.md` | 4 | 3.3 | RF-06, DD-13 | **hecho** (2026-10-09) |
| 2.7 | `sanic_carp_ch_estadoversionplantilla` | `playbooks/choice-global/sanic_carp_ch_estadoversionplantilla.md` | 3 | 3.5 | D-14, D-15, DD-03 | **hecho** (2026-10-09) |
| 2.8 | `sanic_carp_ch_tipomapeo` | `playbooks/choice-global/sanic_carp_ch_tipomapeo.md` | 3 | 3.6 | D-16, RF-09 | **hecho** (2026-10-09) |
| 2.9 | `sanic_carp_ch_campomapeable` | `playbooks/choice-global/sanic_carp_ch_campomapeable.md` | 27 | 3.6 | D-14, P-18, D-36 | **hecho** (2026-10-09) |

Los motivos de estado (`statecode`/`statuscode`) de Conversación y Arreglo no son choices globales: van en el playbook de su tabla (3.2 y 3.3; diccionario §3.2 y hueco H-03).

## 3. Tablas con sus columnas

Siete tablas propias (todas `UserOwned`, DD-01) y las columnas propias sobre `email`. Los lookups **no** se crean acá: nacen con la relación del grupo 4. Estado de los playbooks: **en redacción**.

| # | Componente | Playbook | Depende de | Qué lleva (diccionario §4 y §5) | Origen | Estado |
|---|---|---|---|---|---|---|
| 3.1 | `sanic_carp_tbl_cliente` | `playbooks/tabla/sanic_carp_tbl_cliente.md` | — | 5 columnas (primaria calculada incluida); 0 lookups; `sanic_cedula` con `protegida: true`; sin auditoría de tabla | RF-05, D-07, D-19, D-20 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 3.2 | `sanic_carp_tbl_conversacion` | `playbooks/tabla/sanic_carp_tbl_conversacion.md` | 2.1, 2.2 | 15 columnas (primaria autonumérica `CONV-{SEQNUM:6}`; `sanic_antiguedad` es de fórmula y se crea a mano en el editor de Power Apps, P-20 y H-01); 4 lookups (4.1, 4.9, 4.10, 4.11); actividades activadas (irreversible; crea la relación nativa Regarding Email → Conversación, NAT-01); concurrencia optimista activa; motivos de estado Abierta, Finalizada, Unida (H-03); con auditoría | RF-01 a RF-04, RF-14, RF-15, D-03, D-04, D-27, D-30 | hecho (creada en Dev el 2026-10-09; fórmula `sanic_antiguedad` creada a mano en el editor y etiquetas 3082 completadas con `--corregir-nombres`; `ya_existia` y muestra exportada coinciden) |
| 3.3 | `sanic_carp_tbl_arreglo` | `playbooks/tabla/sanic_carp_tbl_arreglo.md` | 2.3, 2.4, 2.5, 2.6 | 33 columnas (primaria autonumérica `ARR-{SEQNUM:6}`; 7 montos `decimal(2)`, H-01); 8 lookups (4.2, 4.3, 4.4, 4.12 a 4.16); `sanic_cuentadebitar` y `sanic_instantaneacedula` con `protegida: true`; motivos de estado Borrador, Vigente, Terminado, Anulado, Descartado (H-03); con auditoría por columna | RF-06, RF-07, RF-12, RF-16, RF-17, D-09, D-10, D-25, D-30, D-33, D-36 | hecho (creada en Dev el 2026-10-09; `ya_existia` y verificada con la muestra exportada) |
| 3.4 | `sanic_carp_tbl_cuota` | `playbooks/tabla/sanic_carp_tbl_cuota.md` | — | 6 columnas (primaria calculada por plugin; fechas `fecha DO`); 1 lookup (4.5); con auditoría de las columnas de pago | RF-08, RF-10, RF-11, D-08, D-11 | hecho (creada en Dev el 2026-10-09; `ya_existia` y verificada con la muestra exportada) |
| 3.5 | `sanic_carp_tbl_versionplantilla` | `playbooks/tabla/sanic_carp_tbl_versionplantilla.md` | 2.7 | 6 columnas (primaria = clave de negocio `v07`; archivo con `MaxSizeInKB` 5120); 1 lookup (4.17); con auditoría | RF-08, RF-09, D-14, D-15, P-14 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 3.6 | `sanic_carp_tbl_mapeocelda` | `playbooks/tabla/sanic_carp_tbl_mapeocelda.md` | 2.8, 2.9 | 6 columnas (primaria calculada `<hoja>!<celda>`); 1 lookup (4.8); todas auditadas | RF-09, D-14, D-15, D-16 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 3.7 | `sanic_carp_tbl_generacionformato` | `playbooks/tabla/sanic_carp_tbl_generacionformato.md` | — | 2 columnas (primaria autonumérica `GEN-{SEQNUM:8}`); 3 lookups (4.6, 4.7, 4.18); todas auditadas | RF-08, D-13, D-20, D-26 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 3.8 | Columnas propias sobre `email` (tipo `columnas-estandar`) | `playbooks/tabla/email.md` | tabla estándar `email` | `sanic_tiemporespuesta` (entero, solo-sistema). El lookup `sanic_respondidoporid` nace con 4.19 (ver Inconsistencias, punto 11) | RF-03, RF-04, D-05, D-27, H-02 | hecho (creada en Dev el 2026-10-09; `ya_existia` y verificada por la herramienta; la muestra por tabla no aplica a columnas sobre una estándar) |

## 4. Relaciones 1:N

Cada una crea su lookup en la tabla hija. Todas llevan el nombre y el del lookup como **irreversibles**. Comportamientos: `restringido` (Delete = Restrict), `parental` (cascada), `quitar_vinculo` (RemoveLink). Una relación con `systemuser` como padre nunca es parental.

| # | Componente (relación) | Playbook | Lookup que crea → hija | Comportamiento | Depende de | Origen | Estado |
|---|---|---|---|---|---|---|---|
| 4.1 | `sanic_carp_cliente_conversacion` (REL-01) | `playbooks/relacion/sanic_carp_cliente_conversacion.md` | `sanic_clienteid` → Conversación (opcional) | restringido | 3.1, 3.2 | RF-01, P-12 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 4.2 | `sanic_carp_cliente_arreglo` (REL-02) | `playbooks/relacion/sanic_carp_cliente_arreglo.md` | `sanic_clienteid` → Arreglo (requerido) | restringido | 3.1, 3.3 | RF-06, §6 Datos | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 4.3 | `sanic_carp_conversacion_arreglo` (REL-03) | `playbooks/relacion/sanic_carp_conversacion_arreglo.md` | `sanic_conversacionid` → Arreglo (opcional) | restringido | 3.2, 3.3 | RF-06, §6 Datos | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 4.4 | `sanic_carp_versionplantilla_arreglo` (REL-04) | `playbooks/relacion/sanic_carp_versionplantilla_arreglo.md` | `sanic_versionplantillaid` → Arreglo (requerido, auditado) | restringido | 3.5, 3.3 | RF-08, D-15 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 4.5 | `sanic_carp_arreglo_cuota` (REL-05) | `playbooks/relacion/sanic_carp_arreglo_cuota.md` | `sanic_arregloid` → Cuota (requerido) | **parental** | 3.3, 3.4 | RF-08, D-08 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 4.6 | `sanic_carp_arreglo_generacionformato` (REL-06) | `playbooks/relacion/sanic_carp_arreglo_generacionformato.md` | `sanic_arregloid` → Generación de formato (requerido, auditado) | **parental** | 3.3, 3.7 | RF-08, §6 Datos | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 4.7 | `sanic_carp_versionplantilla_generacionformato` (REL-07) | `playbooks/relacion/sanic_carp_versionplantilla_generacionformato.md` | `sanic_versionplantillaid` → Generación de formato (requerido, auditado) | restringido | 3.5, 3.7 | RF-08, P-12 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 4.8 | `sanic_carp_versionplantilla_mapeocelda` (REL-08) | `playbooks/relacion/sanic_carp_versionplantilla_mapeocelda.md` | `sanic_versionplantillaid` → Mapeo de celda (requerido, auditado) | **parental** | 3.5, 3.6 | RF-09, P-12 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 4.9 | `sanic_carp_conversacion_conversacion` (REL-09) | `playbooks/relacion/sanic_carp_conversacion_conversacion.md` | `sanic_unidaaid` → Conversación (autorreferida, no jerárquica, H-04) | quitar_vinculo | 3.2 | RF-14, P-12 | hecho (creada en Dev el 2026-10-09 tras ensayar su variante; `ya_existia` y muestra exportada coinciden) |
| 4.10 | `sanic_carp_systemuser_conversacion_oficialasignadoid` (REL-10) | `playbooks/relacion/sanic_carp_systemuser_conversacion_oficialasignadoid.md` | `sanic_oficialasignadoid` → Conversación (auditado) | restringido | 3.2 | RF-02, D-04, P-01 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 4.11 | `sanic_carp_systemuser_conversacion_finalizadoporid` (REL-11) | `playbooks/relacion/sanic_carp_systemuser_conversacion_finalizadoporid.md` | `sanic_finalizadoporid` → Conversación (auditado) | restringido | 3.2 | RF-15, D-30, P-01 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 4.12 | `sanic_carp_systemuser_arreglo_oficialid` (REL-12) | `playbooks/relacion/sanic_carp_systemuser_arreglo_oficialid.md` | `sanic_oficialid` → Arreglo (requerido) | restringido | 3.3 | RF-06, P-01 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 4.13 | `sanic_carp_systemuser_arreglo_formalizadoporid` (REL-13) | `playbooks/relacion/sanic_carp_systemuser_arreglo_formalizadoporid.md` | `sanic_formalizadoporid` → Arreglo (auditado, solo sistema) | restringido | 3.3 | RF-16, D-30, P-01 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 4.14 | `sanic_carp_systemuser_arreglo_descartadoporid` (REL-14) | `playbooks/relacion/sanic_carp_systemuser_arreglo_descartadoporid.md` | `sanic_descartadoporid` → Arreglo (auditado, solo sistema) | restringido | 3.3 | RF-16, D-30, P-01 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 4.15 | `sanic_carp_systemuser_arreglo_anuladoporid` (REL-15) | `playbooks/relacion/sanic_carp_systemuser_arreglo_anuladoporid.md` | `sanic_anuladoporid` → Arreglo (auditado, solo sistema) | restringido | 3.3 | RF-12, D-30, P-01 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 4.16 | `sanic_carp_systemuser_arreglo_reabiertoporid` (REL-16) | `playbooks/relacion/sanic_carp_systemuser_arreglo_reabiertoporid.md` | `sanic_reabiertoporid` → Arreglo (auditado, solo sistema) | restringido | 3.3 | RF-12, D-30, P-01 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 4.17 | `sanic_carp_systemuser_versionplantilla` (REL-17) | `playbooks/relacion/sanic_carp_systemuser_versionplantilla.md` | `sanic_aprobadoporid` → Versión de plantilla (auditado) | restringido | 3.5 | §6 Datos, P-01 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 4.18 | `sanic_carp_systemuser_generacionformato` (REL-18) | `playbooks/relacion/sanic_carp_systemuser_generacionformato.md` | `sanic_generadoporid` → Generación de formato (requerido, auditado) | restringido | 3.7 | D-26, P-01 | hecho (creada en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 4.19 | `sanic_carp_systemuser_email` (REL-19) | `playbooks/relacion/sanic_carp_systemuser_email.md` | `sanic_respondidoporid` → Email (tabla hija estándar, H-02) | restringido | 3.8 | RF-03, RF-04, D-05 | hecho (creada en Dev el 2026-10-09 tras ensayar su variante; `ya_existia` y muestra exportada coinciden) |

Relación nativa, **no se crea** (NAT-01): Regarding de Email → Conversación. La plataforma la crea al activar actividades en 3.2. Propaga Assign, Share y Delete de la conversación a sus correos (diccionario §6.3).

## 5. Claves alternativas

Las tres usan solo columnas de texto, choice y entero, y ninguna usa un lookup. Por eso dependen de su tabla y no de las relaciones. El índice se activa en segundo plano: el playbook espera y verifica.

| # | Componente | Playbook | Columnas | Depende de | Origen | Estado |
|---|---|---|---|---|---|---|
| 5.1 | `sanic_carp_key_conversacion_huellamessageid` (K-01) | `playbooks/clave/sanic_carp_key_conversacion_huellamessageid.md` | `sanic_huellamessageid` (128 de 900 bytes) | 3.2 | D-03, ítem 1 | hecho (creado en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 5.2 | `sanic_carp_key_arreglo_producto_numeroproducto_numeroarreglo` (K-02) | `playbooks/clave/sanic_carp_key_arreglo_producto_numeroproducto_numeroarreglo.md` | `sanic_producto`, `sanic_numeroproducto`, `sanic_numeroarreglo` (68 de 900 bytes) | 3.3, 2.3 | RF-07, D-09, D-33 | hecho (creado en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |
| 5.3 | `sanic_carp_key_versionplantilla_nombre` (K-03) | `playbooks/clave/sanic_carp_key_versionplantilla_nombre.md` | `sanic_nombre` (20 de 900 bytes) | 3.5 | BP-PP-192, P-19 | hecho (creado en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |

La cédula del Cliente no lleva clave (D-07): una columna con seguridad por columna no puede ser parte de una clave (spike 2). No hay clave en Cuota (P-19).

## 6. Seguridad

### 6.1 Perfil de seguridad de columnas

| # | Componente | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 6.1 | `CSP - CARP - Datos sensibles` (3 columnas: `sanic_cedula` leer, crear y actualizar; `sanic_cuentadebitar` leer, crear y actualizar; `sanic_instantaneacedula` leer y actualizar, sin crear). Sin nombre lógico | `playbooks/perfil-columnas/csp_carp_datos_sensibles.md` | 3.1 y 3.3 con `protegida: true` en las tres columnas | RNF-03, RNF-10, D-19, P-16; diccionario §8 | hecho (creado en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |

La asociación de los teams al perfil y su prueba de punta a punta no viajan en la solución: están en 13.8.

Motivo del permiso de actualizar sobre `sanic_instantaneacedula` (2026-10-20): Desbloquear y Reabrir fallaban con 403 para todo usuario no administrador porque el plugin (`VaciarInstantanea`, vía el Target del llamador) escribe null en `sanic_instantaneacedula` y la plataforma exige permiso de actualizar sobre una columna protegida. Es la única columna protegida que esos gestos escriben. La regla de negocio sigue en el plugin: S-21 rechaza el cambio manual de esa columna (CARP-ARR-009 y CARP-ARR-011). Se aplica con `perfil_columnas.py --corregir`.

### 6.2 Roles de seguridad

Se crean con profundidad BU, sin Delete humano sobre el agregado (definición §6 Seguridad). Los privilegios por tabla están descritos en la definición; la matriz de privilegios del diseño (`diseno/`) todavía no existe. Los privilegios exactos de Attachment "se confirman en la construcción". Ninguno tiene nombre lógico.

| # | Componente | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 6.2 | `SR - CARP - Oficial` (definición: "ARP Oficial") | `playbooks/rol/sr_carp_oficial.md` | 3.1 a 3.8 y las tablas estándar Email, Attachment, Queue | RNF-04, D-17, D-28, D-31 | hecho (creado en Dev el 2026-10-09 desde `diseno/04-matriz-privilegios.md`; `ya_existia` y muestra exportada coinciden; falta la prueba con un usuario no administrador, matriz §7) |
| 6.3 | `SR - CARP - Jefe` (definición: "ARP Jefe"; "suma" privilegios al Oficial) | `playbooks/rol/sr_carp_jefe.md` | ídem; 6.2 como base de lectura | RF-09, RF-04, RNF-04 | hecho (creado en Dev el 2026-10-09 desde `diseno/04-matriz-privilegios.md`; `ya_existia` y muestra exportada coinciden; falta la prueba con un usuario no administrador, matriz §7) |
| 6.4 | `SR - CARP - Servicio` (definición: "ARP Servicio"; cuenta de servicio, flujos y reconciliación) | `playbooks/rol/sr_carp_servicio.md` | ídem; Mailbox, Queue Item | D-21, D-26; Create de Conversación es el privilegio de ejecución de 8.3 | hecho (creado en Dev el 2026-10-09 desde `diseno/04-matriz-privilegios.md`; `ya_existia` y muestra exportada coinciden; falta la prueba con un usuario no administrador, matriz §7) |
| 6.5 | `SR - CARP - Purga` (definición: "ARP Purga"; trabajos de eliminación) | `playbooks/rol/sr_carp_purga.md` | ídem; Email con adjuntos y Queue Item | RNF-07, D-22 | hecho (creado en Dev el 2026-10-09 desde `diseno/04-matriz-privilegios.md`; `ya_existia` y muestra exportada coinciden; falta la prueba con un usuario no administrador, matriz §7) |

Los roles son previos a las Custom API: cada API se restringe con el privilegio de una tabla propia, presente solo en los roles que corresponden (D-17). Las API dependen de los roles, no al revés.

## 7. Código de plugins (C#, TDD estricto, sin FakeXrmEasy)

Un solo paquete, `sanic_carp_pkg_plugins` (namespace raíz `Sanic.Carp.Plugins`, inmutable tras el primer registro). Solución `src/Sanic.Carp.sln`; pruebas `Sanic.Carp.Plugins.Tests` (xUnit, `net8.0`); librerías `net462;net8.0`. El dominio no conoce Dataverse. Se construye de adentro hacia afuera: las piezas 7.1 a 7.9 no tocan el entorno.

Estas filas **no tienen playbook**. Cada pieza se especifica con pruebas de aceptación en `src/Sanic.Carp.Plugins.Tests/Aceptacion/` y firmas públicas (ciclo de `herramientas/agentes/orden_constructor_codigo.md`). El directorio `src/` todavía no existe. El reparto en clases y carpetas es diseño detallado pendiente: las filas siguen las responsabilidades que nombra la definición, no una estructura de clases.

| # | Pieza (responsabilidad) | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 7.1 | Solución .NET, proyecto de pruebas y enums generados de los choices (`Dominio/Choices.g.cs`, vía `herramientas/codigo/generar_enums.py`) | — (convenciones §5) | 2.1 a 2.9 | Convenciones §5 y §6 | hecho (código, 2026-10-19) |
| 7.2 | Dominio: estados y transiciones permitidas de Conversación, Arreglo y Versión de plantilla; qué columnas son solo-sistema y quién actúa (sistema, usuario; ninguna cuenta de servicio se reconoce, OA-12) | — | 7.1 | D-10, D-26, D-30, diccionario §3.2 | hecho (código, 2026-10-19) |
| 7.3 | Dominio: normalización (número de producto, cédula, correo, Message-ID) y huella SHA-256 | — | 7.1 | P-06, D-03, ítem 1 | hecho (código, 2026-10-19) |
| 7.4 | Dominio: montos del arreglo (saldo remanente, total a pagar, última cuota) y generador del calendario de hasta 60 cuotas. La regla de fechas de vencimiento está decidida (R-03, OA-5: mismo día de cada mes desde la fecha de inicio; el plugin sobrescribe la fecha de fin) | — | 7.1 | RF-06, RF-08, DD-12, R-04, ítems 7 y 8 | hecho (código, 2026-10-19) |
| 7.5 | Dominio: consecutivo por crédito (`max+1`) y comparación con el número provisional impreso | — | 7.1 | RF-07, D-09, ítem 9 | hecho (código, 2026-10-19) |
| 7.6 | Dominio: tiempos de la conversación y de cada respuesta (sin respuesta desde, primera respuesta, total, cantidad), en minutos | — | 7.1 | RF-03, RF-04, DD-05, ítems 1, 3 y 6 | hecho (código, 2026-10-19) |
| 7.7 | Dominio: validación de publicación de plantilla y mapeo (celdas A1 bien formadas, sin dos campos en la misma celda, bloque de cuotas completo, capacidad coherente) y huella del archivo | — | 7.1, 7.3 | RF-09, D-15, ítem 15 | hecho (código, 2026-10-19) |
| 7.8 | Generador del formato con Open XML SDK (DocumentFormat.OpenXml como ensamblado dependiente): mapeo, valores simples, totales, bloque de cuotas, limpieza de filas sobrantes, recálculo al abrir, número provisional en borrador | — | 7.1, 7.4, 7.7 | RF-08, D-13, D-16, ítem 14; spikes 1 y 1b | hecho (código, 2026-10-19) |
| 7.9 | Doble de prueba en memoria de `IOrganizationService` (proyecto de pruebas), validado contra respuestas reales exportadas | — | 7.1 | Convenciones §5 | hecho (código, 2026-10-19) |
| 7.10 | Datos: repositorios sobre `IOrganizationService` | — | 7.1 a 7.9, 3.x, 4.x | §6 Lógica | hecho (código, 2026-10-19) |
| 7.11 | Steps de Email: registro de correos entrantes en conversaciones (asíncrono), registro de respuesta enviada y cálculo de tiempos (asíncrono), remitente fijado a la queue del área (pre-create) | — | 7.2, 7.3, 7.6, 7.10, 3.8 | RF-01, RF-03, D-03, D-05, D-28, ítems 1 y 3 | hecho (código, 2026-10-19) |
| 7.12 | Steps de Conversación: columnas solo-sistema (el número autonumérico `sanic_nombre` también: el Create rechaza un valor sin el formato `CONV-` más seis a quince dígitos, `ConversacionCrearStep`, S-36, y el Update rechaza cualquier cambio de una persona, `CARP-CNV-001`, D-41, 2026-10-11), asignación (vacío → uno mismo; otro cambio solo Jefatura), finalizar y reabrir (Reabrir no admite otro cambio en el mismo guardado, `CARP-CNV-010`, D-40, 2026-10-11), y una Finalizada o Unida que una persona no modifica sin reabrir (`CARP-CNV-009`, D-39, 2026-10-11) | — | 7.2, 7.10 | RF-02, RF-15, D-04, D-30, ítems 2 y 6 | hecho (código, 2026-10-19) |
| 7.13 | Steps de Cliente: normalización, unicidad de la cédula al crear, vinculación por correo, y el `statecode`/`statuscode` nativos del cliente solo los cambia SYSTEM (`CARP-CLI-004`; el Update lleva filtro `statecode,statuscode` y pre-imagen, D-40, 2026-10-11) | — | 7.3, 7.10 | RF-05, D-07, P-06 | hecho (código, 2026-10-19) |
| 7.14 | Steps de Arreglo: registro en borrador (versión Vigente, canal, montos; rechazo de un `sanic_nombre` que no cumpla el formato del autonumérico, V-8, D-40, 2026-10-11), calendario de cuotas, congelamiento, desbloqueo, formalización, descarte, anulación, reapertura (sin otro cambio en el mismo guardado, `CARP-ARR-012`, D-40) | — | 7.2, 7.4, 7.5, 7.10 | RF-06, RF-07, RF-12, RF-16, D-08 a D-10, D-25, D-30, ítems 7 a 12 | hecho (código, 2026-10-19) |
| 7.15 | Steps de Cuota: solo pagos y solo con el arreglo Vigente (el `statecode` nativo solo lo cambia SYSTEM, D-39, 2026-10-11); paso del arreglo a Terminado | — | 7.2, 7.10 | RF-10, D-10, ítem 11 | hecho (código, 2026-10-19) |
| 7.16 | Steps de Versión de plantilla y Mapeo: publicación, inmutabilidad (Update, incluido un step de Update filtrado por `sanic_archivo`: la subida por bloques no admite steps propios pero provoca ese Update; V-4, 2026-10-11), borrado de mapeo solo en Borrador, y el `statecode` nativo de la versión solo lo cambia SYSTEM (`CARP-VER-014`, D-39, 2026-10-11), igual que el de cada mapeo (`CARP-MAP-007`, D-40, 2026-10-11); el nombre calculado del mapeo (`sanic_nombre`) tampoco lo cambia una persona (`CARP-MAP-008`, D-41, 2026-10-11); una versión que queda en Borrador sin publicarse se reutiliza, no se retira (D-41) | — | 7.7, 7.10 | RF-09, D-15, ítem 15 | hecho (código, 2026-10-19) |
| 7.17 | Propietario: fijar el team del sistema de la BU como dueño y rechazar otro. La definición lo fija para Cliente, Conversación y Arreglo; el diccionario lo amplía a las siete tablas (P-11) | — | 7.10 | D-31, P-11 | hecho (código, 2026-10-19) |
| 7.18 | Plugin de la Custom API de generación del formato (8.2) | — | 7.8, 7.10 | RF-08, D-13, ítem 14 | hecho (código, 2026-10-19) |
| 7.19 | Plugin de la Custom API de reconciliación de correos sin conversación (8.3), con la misma lógica de 7.11 | — | 7.11 | RF-01, D-03 | hecho (código, 2026-10-19) |
| 7.20 | Plugin de la Custom API "Tomar" conversación, con concurrencia optimista (8.4) | — | 7.12 | RF-02, D-04 | hecho (código, 2026-10-19) |
| 7.21 | Plugin de la Custom API "Unir conversaciones" (8.5), en una transacción; captura al actor antes de elevar | — | 7.12, 7.10 | RF-14, D-32, ítem 5 | hecho (código, 2026-10-19) |
| 7.22 | Plugin de la Custom API "Iniciar conversación nueva" con el correo llegado a una finalizada (8.6) | — | 7.12, 7.10 | RF-15, D-32, ítem 6 | hecho (código, 2026-10-19) |

**Estado del código (2026-10-20).** Las 22 filas (7.1 a 7.22) están construidas con TDD estricto (arquitecto, constructor, revisor y orquestador) y `dotnet build -f net462` en 0 avisos y 0 errores. Reparto por lotes: lote 1, dominio puro (7.1 a 7.7); lote 2, generador del formato (7.8); lotes 3 y 3b, dobles validados contra Dev (7.9); lote 4, Infraestructura y Datos (7.10); lote 5 en sublotes 5a a 5f, los steps (7.11 a 7.17), con los 37 registros de `03-contratos-custom-api.md` §5; lote 6, las 5 Custom API (7.18 a 7.22). Resultado: 5820 pruebas de 5820 en verde; último commit `397088d`. "Hecho" significa construido y probado contra el doble validado en Dev; la verificación en la plataforma real es el grupo 8 y las pruebas de humo H-1 a H-32 de `03-contratos-custom-api.md` §8.1. Faltan también correr las pruebas en net462 sobre Windows y abrir un formato generado en Excel de escritorio. Lo que el diseño final cambió respecto del texto de las filas (que no se reescriben): el registro de steps pasó de 36 a 37 con S-37 (`RemitenteDelAreaStep` también en el `Update` de `email`, orden 20, 2026-10-19, cierre del sublote 5f), de modo que 7.11 fija la queue del área como remitente en el Create y en el Update; ninguna cuenta de servicio se reconoce (OA-12); y la introducción de este grupo ("el directorio `src/` todavía no existe", reparto en clases pendiente) quedó vieja: el código está en `src/` con su reparto en clases fijado en `03-contratos-custom-api.md`.

Regla de implementación que cruza el grupo: toda regla que debe cumplirse sin importar el punto de entrada vive en el servidor (definición §6 Lógica). El cliente solo da feedback.

## 8. Registro del paquete, Custom API y steps

El paquete se compila (`dotnet pack`, fuera de este inventario) y se registra antes que cualquier API o step. La definición nombra las cinco Custom API por función (§6 Componentes, lista de cinco) y no fija nombre ni parámetros; el diseño detallado sí los fija: nombres, parámetros, privilegios y errores de cada API en `diseno/03-contratos-custom-api.md` §1, la estructura del paquete en §4.1 y la lista cerrada de steps (37 registros sobre 21 clases) en §5. El código de las cinco API y de los 21 steps está construido y probado (grupo 7); lo que sigue pendiente de este grupo es registrarlo en el entorno y verificarlo en la plataforma real (`diseno/03` §8.1).

| # | Componente | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 8.1 | Paquete `sanic_carp_pkg_plugins` (registro; dependencia Open XML SDK) | `playbooks/paquete-plugins/Sanic.Carp.Plugins.md` | 7.1 a 7.22 compilados | Convenciones §5; D-13, spike 1b | hecho (Dev, 2026-10-20). Paquete 1.0.0 registrado, verificado `ya_existia`; muestra en `playbooks/paquete-plugins/muestras` |
| 8.2 | `sanic_carp_capi_generarformato` (`CAPI - CARP - Generar formato`). Privilegio de ejecución: Read de Generación de formato (Oficial y Purga; Jefe y Servicio por el Oficial, `diseno/03` §0.1). Parámetros: `diseno/03` §1.1 | `playbooks/custom-api/sanic_carp_capi_generarformato.md` | 8.1, 7.18, 3.7, 6.2, 6.3 | RF-08, D-13, D-17 | hecho (Dev, 2026-10-20). Verificada `ya_existia`; muestra en `playbooks/custom-api/muestras` |
| 8.3 | `sanic_carp_capi_reconciliarcorreos`. Privilegio de ejecución: Create de Conversación (solo Servicio). Parámetros: `diseno/03` §1.2 | `playbooks/custom-api/sanic_carp_capi_reconciliarcorreos.md` | 8.1, 7.19, 3.2, 6.4 | RF-01, D-03, D-17 | hecho (Dev, 2026-10-20). Verificada `ya_existia`; muestra en `playbooks/custom-api/muestras` |
| 8.4 | `sanic_carp_capi_tomarconversacion`. Privilegio de ejecución: Write de Conversación. Parámetros: `diseno/03` §1.3 | `playbooks/custom-api/sanic_carp_capi_tomarconversacion.md` | 8.1, 7.20, 3.2, 6.2, 6.3 | RF-02, D-04, D-17 | hecho (Dev, 2026-10-20). Verificada `ya_existia`; muestra en `playbooks/custom-api/muestras` |
| 8.5 | `sanic_carp_capi_unirconversaciones`. Privilegio de ejecución: Write de Conversación. Parámetros: `diseno/03` §1.4 | `playbooks/custom-api/sanic_carp_capi_unirconversaciones.md` | 8.1, 7.21, 3.2, 6.2, 6.3 | RF-14, D-17, D-32 | hecho (Dev, 2026-10-20). Verificada `ya_existia`; muestra en `playbooks/custom-api/muestras` |
| 8.6 | `sanic_carp_capi_iniciarconversacionnueva`. Privilegio de ejecución: Write de Conversación. Parámetros: `diseno/03` §1.5 | `playbooks/custom-api/sanic_carp_capi_iniciarconversacionnueva.md` | 8.1, 7.22, 3.2, 6.2, 6.3 | RF-15, D-17, D-32 | hecho (Dev, 2026-10-20). Verificada `ya_existia`; muestra en `playbooks/custom-api/muestras` |
| 8.7 | Steps del paquete (nombre `STEP - CARP - tabla - mensaje - etapa - clase`, `diseno/03` DC-23). Lista cerrada: `diseno/03` §5 (37 registros sobre 21 clases) | `playbooks/steps/sanic_carp_pkg_plugins.md` (un solo playbook con los 37 registros; decidido el 2026-10-20; S-18 fue el piloto del tipo y quedó integrado, el piloto se borró) | 8.1, 7.11 a 7.17 | D-26, D-30, D-31, §6 Lógica | hecho (Dev, 2026-10-20). 37 steps verificados `ya_existia`; muestra en `playbooks/steps/muestras` |

Familias de steps que la definición implica (contenido de 8.7; la lista cerrada con etapa, orden, filtros e imágenes está en `diseno/03` §5):

| Tabla o mensaje | Qué cubre | Pieza de código |
|---|---|---|
| Email, Create (asíncrono, post) | Registrar el correo entrante en su conversación | 7.11 |
| Email, Create (síncrono, pre) | Fijar la queue del área como remitente de toda respuesta con Regarding a una Conversación | 7.11 |
| Email, estado Enviado (asíncrono) | Tiempo de la respuesta, quién respondió, recalcular la conversación | 7.11 |
| Conversación, Create (pre) | Formato del número autonumérico `sanic_nombre` (`CONV-` más seis a quince dígitos; las altas del sistema pasan), propietario (D-41, 2026-10-11) | 7.12, 7.17 |
| Conversación, Update (pre) | Columnas solo-sistema (incluido el número `sanic_nombre`, D-41), asignación, gestos Finalizar y Reabrir, inmutabilidad de Finalizada y Unida, propietario | 7.12, 7.17 |
| Cliente, Create y Update | Normalización, unicidad de la cédula, vinculación, estado nativo solo de SYSTEM, propietario | 7.13, 7.17 |
| Arreglo, Create (pre y post) | Propietario, versión Vigente, canal, montos; calendario de cuotas | 7.14, 7.17 |
| Arreglo, Update (pre) | Solo-sistema, congelamiento, gestos con motivo, reemplazo de cuotas | 7.14 |
| Cuota, Update (pre) y paso a Terminado | Solo pagos, arreglo Vigente | 7.15 |
| Versión de plantilla, Update (con un step filtrado por `sanic_archivo` para la subida de archivo, V-4); Mapeo, Create, Update y Delete | Publicación, inmutabilidad, borrado solo en Borrador | 7.16 |

## 9. Datos semilla y de configuración

La BU, los teams, la queue, el mailbox y las versiones de plantilla con su mapeo son **datos, no componentes de solución**: no viajan con ella (definición §6, Entornos y despliegue). Sin una versión Vigente el alta de un arreglo se rechaza (ítem 7). La forma de promover la plantilla de Test a Prod quedó decidida el 2026-10-10 (OA-4): **recarga controlada** con un script versionado (9.1 y 9.2) que crea la versión en Borrador en Prod, sube el archivo y carga el mapeo; el jefe publica con el gesto normal y se compara la huella con la de Test. La herramienta de migración de configuración queda descartada (escribiría la versión ya Vigente y saltaría la validación de publicar).

| # | Qué | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 9.1 | Versión de plantilla v07 (fila y archivo `.xlsx`), publicada como Vigente por el flujo normal de publicación. v07 aprobada por Héctor Romero el 2026-10-09. En Prod el script crea la versión en Borrador y sube el archivo; el jefe publica y se compara la huella con la de Test (OA-4, 2026-10-10) | `playbooks/datos/versionplantilla.md` | 3.5, 8.7 (publicación), 9.2 | RF-09, C-01, D-15 | hecha (2026-10-20, Dev: `datos.py` con columna de archivo, `ya_existia`; publicada como Vigente con huella = SHA-256 del archivo aprobado, v95 HUMO pasó a Histórica; `pruebas-humo/lote-0/RESULTADO-plantilla.md`) |
| 9.2 | Mapeo de celda de v07 (28 filas: `MAP_V07` del mockup, medido en la tanda 1 de humo del 2026-10-20). El mismo script lo carga en Prod antes de que el jefe publique (OA-4, 2026-10-10) | `playbooks/datos/mapeocelda.md` | 3.6, 2.8, 2.9, 4.8 | RF-09, D-14, D-16 | hecha (2026-10-20, Dev: 28 de 28 `ya_existia`; el formato generado sobre un arreglo HUMO Vigente con una cuota pagada llena las 28 celdas) |
| 9.3 | Datos de prueba para Dev y Test (nunca datos reales de Prod). Contenido no definido | `playbooks/datos/humo_*.md` | 3.1 a 3.7, 4.x | Definición §6 Seguridad (Datos en entornos no productivos) | diseño detallado pendiente |

## 10. Configuración entre entornos

Las **definiciones** viajan en la solución; los **valores** y las **conexiones**, no (se cargan en 13.13). La definición enumera las variables así: "nombres de los dos teams, destinatarios de alertas y umbrales de monitoreo", y aparte dice que el nombre de la BU y el del team Jefatura vienen de environment variables. Esas dos listas no coinciden del todo (Inconsistencias, punto 3). Los nombres de las variables son *(prov.)*.

| # | Componente | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 10.1 | Environment variable `sanic_carp_ev_nombrebu` (nombre confirmado por el usuario el 2026-10-10): nombre de la BU, por el que los plugins resuelven la BU | `playbooks/variable-entorno/sanic_carp_ev_nombrebu.md` | 1.1 | D-23, §6 Entornos | hecho (Dev, 2026-10-20). Definición en la solución y valor `Arreglos de Pago` cargado; verificada `ya_existia` |
| 10.2 | Environment variable `sanic_carp_ev_nombreteamjefatura` *(prov.)*: nombre del team Jefatura | `playbooks/variable-entorno/sanic_carp_ev_nombreteamjefatura.md` | 1.1 | D-04, D-23 | hecho (Dev, 2026-10-20). Definición en la solución y valor `Arreglos de Pago - Jefatura` cargado; verificada `ya_existia` |
| 10.3 | Environment variable `sanic_carp_ev_destinatariosalertas` *(prov.)*: destinatarios de alertas | `playbooks/variable-entorno/sanic_carp_ev_destinatariosalertas.md` | 1.1 | §6 Entornos, Operación | hecho (2026-10-20, Dev: `creado` y `ya_existia`; valor de Dev = buzón de prueba 3, el contenido sale de `diseno/07` §8.2) |
| 10.4 | Environment variable `sanic_carp_ev_umbralesmonitoreo` *(prov.)*: umbrales de monitoreo (puede ser más de una variable) | `playbooks/variable-entorno/sanic_carp_ev_umbralesmonitoreo.md` | 1.1 | §6 Entornos, Operación | hecho (2026-10-20, Dev: una sola variable JSON con las cuatro claves de `diseno/07` §8.2; `creado` y `ya_existia`; `horasSinEntrada` = 4 sigue a confirmar con el jefe, DA-03) |
| 10.7 | ~~Environment variable `sanic_carp_ev_upncuentaservicio` *(prov.)*: UPN de la cuenta de servicio licenciada (RNF-06), por el que los plugins reconocen las escrituras del sistema (`domainname`)~~ | ~~`playbooks/variable-entorno/sanic_carp_ev_upncuentaservicio.md`~~ | ~~1.1~~ | ~~Decisión del usuario del 2026-10-09 (inconsistencia 4), D-23~~ | **RETIRADA (OA-12, 2026-10-10).** Ningún plugin reconoce a la cuenta de servicio: solo SYSTEM escribe columnas solo-sistema. La fila 10.7 no se reutiliza ni se renumera |
| 10.8 | Environment variable `sanic_carp_ev_buzonarea` *(prov.)*: dirección del buzón del área, por la que se resuelve la queue remitente | `playbooks/variable-entorno/sanic_carp_ev_buzonarea.md` | 1.1 | Decisión del usuario del 2026-10-09 (inconsistencia 4), D-23 | hecho (2026-10-20, Dev: valor = buzón de prueba 1; `ya_existia` con `--solo-verificar`. En Test y Prod el valor se carga por entorno, 13.13) |
| 10.5 | Connection reference `sanic_carp_conr_outlook` (`CONR - CARP - Outlook`), conector Office 365 Outlook | `playbooks/referencia-conexion/sanic_carp_conr_outlook.md` | 1.1 | §6 Integraciones; convenciones §2 | hecho (2026-10-20, Dev: referencia creada en la solución con `referencia_conexion.py`, **sin conexión**: la plataforma no deja que la identidad de la herramienta asocie una conexión de Outlook de otra persona (`ConnectionAuthorizationFailed`); la asocia el usuario desde el portal, paso manual de `pruebas-humo/lote-d/RESULTADO.md`) |
| 10.6 | Connection reference `sanic_carp_conr_dataverse` *(prov.)*, conector Dataverse | `playbooks/referencia-conexion/sanic_carp_conr_dataverse.md` | 1.1 | §6 Integraciones | hecho (2026-10-20, Dev: `creado` y `ya_existia`, asociada a una conexión de Dataverse que ya existía en Dev a nombre de la cuenta de servicio) |

Ambos conectores deben estar en el mismo grupo de DLP (definición §6, Integraciones).

## 11. Flujos

Solo monitoreo. La captura de correos no pasa por flujos (D-21). Son propiedad de la cuenta de servicio (13.6), con secure inputs y outputs en todas las acciones sobre tablas de ARP. Un cloud flow no tiene nombre lógico: se identifica por su display name, `Cloud Flow - CARP - COD - Nombre`, con `COD` de tres letras (BP-PP-196).

| # | Componente | Disparador | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|---|
| 11.1 | `Cloud Flow - CARP - COD - Avisar estado del mailbox` *(prov.; `COD` por definir)*: avisa por Outlook al jefe y al equipo cuando la entrada o la salida del buzón pasan a fallo | `playbooks/flujo/carp-est.md` | `playbooks/flujo/<cod>.md` | 10.1 a 10.6, 6.4, 13.5, 13.6 | D-21, §6 Operación | pendiente (2026-10-20, Dev: construido como `Cloud Flow - CARP - EST - Avisar estado del mailbox`, en la solución, a nombre de la cuenta de servicio, **apagado** hasta que la referencia 10.5 tenga conexión; `playbooks/flujo/carp-est.md`) |
| 11.2 | `Cloud Flow - CARP - SAL - Salud del buzon` (ID `CARP-SAL`): cada 30 minutos en horario hábil comprueba correos entrantes recientes, correos sin conversación, respuestas con error de envío y **trabajos de eliminación masiva fallidos en las últimas 24 h** (cuarta comprobación, OA-3, 2026-10-10); si hay correos sin conversación invoca 8.3. El privilegio con que `SR - CARP - Servicio` lee esos trabajos está PENDIENTE de verificar en Dev al construir este flujo (`04-matriz-privilegios.md`, duda D-09). Hasta que el flujo exista, revisión manual semanal de System Jobs | recurrente | `playbooks/flujo/carp-sal.md` | 8.3, 10.3 a 10.6, 6.4, 13.5, 13.6 | D-21, RNF-08, §6 Operación | pendiente (2026-10-20, Dev: construido en la solución, a nombre de la cuenta de servicio, **apagado** hasta que la referencia 10.5 tenga conexión; D-09 resuelta sin cambio de rol; `playbooks/flujo/carp-sal.md`) |
| 11.3 | Flujo diario de marcado de cuotas vencidas sin pago (respaldo de D-11). **Condicional: no se activa**, el spike 2 mostró que el operador de fecha relativa sirve. Requeriría una Custom API que la definición no lista entre las cinco | recurrente diario | — | D-11, D-21; escribe Cuota con rol Servicio | diseño detallado pendiente (condicional; no se construye salvo que se active) |

## 12. App model-driven

La definición difiere el diseño de pantallas a la etapa 3 como entregable obligatorio (§6 Componentes, app). El mockup validado (v3, `mockup/index.html`) es la referencia funcional. Todo este grupo es `diseño detallado pendiente`. La lista sigue lo que la definición **nombra o implica**; las vistas y formularios que sean estándar de cada tabla no se listan.

Áreas de navegación de la app: Conversaciones, Clientes y arreglos, Supervisión (jefe) y Administración del formato (jefe).

### 12.1 Vistas

Nombre de vista: texto de negocio, sin prefijo y sin tildes. Los nombres son *(prov.)* y salen de la frase de la definición.

| # | Vista | Tabla | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|---|
| 12.1 | Correos sin respuesta (todo `sanic_sinrespuestadesde` no vacío; para el jefe; incluye las finalizadas con correo nuevo, OA-2) | Conversación | `playbooks/vista/correos_sin_respuesta.md` | 3.2 (incluye `sanic_antiguedad`) | RF-04, D-27 | hecho (Dev, 2026-10-20) (diseño completo en `diseno/07` §3.2) |
| 12.2 | Correos llegados a conversaciones finalizadas (indicador = sí; para el oficial, OA-2) | Conversación | `playbooks/vista/correos_en_finalizadas.md` | 3.2 | RF-15, D-32 | hecho (Dev, 2026-10-20) (diseño completo en `diseno/07` §3.2) |
| 12.3 | Finalizadas sin respuesta (resultado = Sin respuesta) | Conversación | `playbooks/vista/finalizadas_sin_respuesta.md` | 3.2 | RF-04, D-06 | hecho (Dev, 2026-10-20, lote C) (`creado`, `ya_existia`; el `?savedQuery=` da 200 con 0 filas) (diseño completo en `diseno/07` §3.2) |
| 12.4 | Respuestas con error de envio | Email | `playbooks/vista/respuestas_con_error_de_envio.md` | 3.8 | RF-03, §6 Lógica ítem 3 | hecho (Dev, 2026-10-20, lote C) (el filtro `regardingobjecttypecode` con el nombre de la tabla da HTTP 400 en el alta, espera un entero; se usó el respaldo del diseño, un enlace `inner` a Conversación por `regardingobjectid`; `creado`, `ya_existia`; el `?savedQuery=` da 200 con 0 filas) (diseño completo en `diseno/07` §3.3) |
| 12.5 | Borradores | Arreglo | `playbooks/vista/borradores.md` | 3.3 | RF-16 | hecho (Dev, 2026-10-20) (diseño completo en `diseno/07` §3.4) |
| 12.6 | Borradores sin movimiento (más de 30 días sin cambios; vista aparte de 12.5, OA-2) | Arreglo | `playbooks/vista/borradores_sin_movimiento.md` | 3.3 | RF-16, D-22, §6 Operación | hecho (Dev, 2026-10-20, lote C) (`creado`, `ya_existia`; `?savedQuery=` 200 con 0 filas) (diseño completo en `diseno/07` §3.4) |
| 12.7 | Arreglos en riesgo (vigentes con una cuota vencida sin pago; filtro relacionado, `olderthan-x-days 1` o `yesterday`) | Arreglo | `playbooks/vista/arreglos_en_riesgo.md` | 3.3, 3.4, 4.5 | RF-11, D-11 | hecho (Dev, 2026-10-20, lote C) (enlace `any` con `olderthan-x-days 1` o `yesterday`; `creado`, `ya_existia`; `?savedQuery=` 200 con 0 filas, así que el filtro lo acepta el servidor pero no se vio una fila en riesgo) (diseño completo en `diseno/07` §3.4) |
| 12.8 | Arreglos de prestamo | Arreglo | `playbooks/vista/arreglos_de_prestamo.md` | 3.3, 2.3 | RF-17, D-35 | hecho (Dev, 2026-10-20, lote C) (`creado`, `ya_existia`; `?savedQuery=` 200 con 19 filas) (diseño completo en `diseno/07` §3.4) |
| 12.9 | Arreglos de tarjeta | Arreglo | `playbooks/vista/arreglos_de_tarjeta.md` | 3.3, 2.3 | RF-17, D-35 | hecho (Dev, 2026-10-20, lote C) (`creado`, `ya_existia`; `?savedQuery=` 200 con 0 filas) (diseño completo en `diseno/07` §3.4) |
| 12.10 | Busqueda rapida de Cliente (cédula, nombre, correo, celular) | Cliente | `playbooks/vista/busqueda_rapida_cliente.md` | 3.1, 6.1 | RF-13, D-12 | hecho (Dev, 2026-10-20) (`Quick Find Active Clientes` reparada con `vista.py --completar`: el `PATCH` manda `{fetchxml, layoutxml, name, returnedtypecode}` juntos; `--solo-verificar` da `ya_existia`; el `?savedQuery=` da 200 y la búsqueda real por la cédula HUMO encuentra la fila) (diseño completo en `diseno/07` §3.5) |
| 12.11 | Busqueda rapida de Arreglo (número de producto) | Arreglo | `playbooks/vista/busqueda_rapida_arreglo.md` | 3.3 | RF-13, D-12 | hecho (Dev, 2026-10-20) (`Quick Find Active Arreglos` reparada con `vista.py --completar`; `--solo-verificar` da `ya_existia`; el `?savedQuery=` da 200 y la búsqueda real por el número de producto HUMO encuentra la fila) (diseño completo en `diseno/07` §3.4) |
| 12.12 | Arreglos del cliente (subgrid con todos los estados) | Arreglo | `playbooks/vista/arreglos_del_cliente.md` | 3.3, 4.2 | RF-13, ítem 13 | hecho (Dev, 2026-10-20) (se agregó `createdon` 150 como última columna, decisión del orquestador; `--solo-verificar` da `ya_existia`; el `?savedQuery=` da 200 con 15 filas) (diseño completo en `diseno/07` §3.4) |
| 12.13 | Otras vistas "por perfil" (oficial y jefe). La definición no las enumera | varias | `playbooks/vista/<nombre>.md` (15 playbooks) | 3.x, 4.x | §6 Componentes (app) | hecho (Dev, 2026-10-20) (las 15 vistas; `Respuestas enviadas` sobre `email` la verifica `vista.py` como `difiere` por un falso positivo: la tabla está en la solución con `rootcomponentbehavior` 1, pero la vista figura en la solución como componente propio, tipo 26) (diseño completo en `diseno/07` §3.5 y §3.6 (15 vistas)) |

### 12.2 Dashboard y gráficos

Los gráficos agregan promedios por oficial; las vistas no (D-27). En el proyecto no hay herramienta ni tipo de playbook para dashboards o gráficos (`herramientas/README.md`). Se decide al diseñarlos.

| # | Componente | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 12.14 | `DASH - CARP - Supervision` (dashboard del jefe) | `playbooks/dashboard/supervision.md` | 12.15, 12.16 | RF-04, D-27 | hecho (2026-10-20, Dev, lote E: script `pruebas-humo/lote-e/scripts/e01_graficos_panel.py`, `ya_existia` con `--solo-verificar`, en la solución exportada) |
| 12.15 | `CHT - CARP - Promedio por respuesta` *(prov.)*: promedio de `sanic_tiemporespuesta` sobre Email, agrupado por `sanic_respondidoporid` | `playbooks/grafico/promedio_por_respuesta.md` | 3.8, 4.19 | RF-03, RF-04, D-27 | hecho (2026-10-20, Dev, lote E: script `pruebas-humo/lote-e/scripts/e01_graficos_panel.py`, `ya_existia` con `--solo-verificar`, en la solución exportada) |
| 12.16 | `CHT - CARP - Tiempo total` *(prov.)*: promedio de `sanic_tiempototal` sobre Conversación, agrupado por oficial asignado, sin las finalizadas "Sin respuesta" (a confirmar con negocio) | `playbooks/grafico/tiempo_total.md` | 3.2, 4.10 | RF-04, D-27 | hecho (2026-10-20, Dev, lote E: script `pruebas-humo/lote-e/scripts/e01_graficos_panel.py`, `ya_existia` con `--solo-verificar`, en la solución exportada) |

### 12.3 Formularios principales

Nombre: `FRM - CARP - Tabla - Tipo`. La definición no nombra un formulario para Cuota (los pagos se registran sobre la cuota; el diseño lo agrega como 12.26, `diseno/07` DA-09) ni para Generación de formato (bitácora); ese no se lista. La Conversación muestra el timeline con el editor de correo nativo, y avisa si el cliente tiene otra conversación abierta.

| # | Formulario | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 12.17 | `FRM - CARP - Cliente - Principal` (subgrid 12.12) | `playbooks/formulario/cliente.md` | 3.1, 12.12 | RF-05, RF-13 | hecho (Dev, 2026-10-20) (diseño completo en `diseno/07` §4.3) |
| 12.18 | `FRM - CARP - Conversacion - Principal` (timeline, aviso de otra conversación abierta) | `playbooks/formulario/conversacion.md` | 3.2, 3.8 | RF-01, RF-03, RF-14 | hecho (Dev, 2026-10-20; lote C: `OnLoad` con el aviso de otra conversación abierta, `--completar`, `ya_existia`, 2 manejadores) (diseño completo en `diseno/07` §4.2) |
| 12.19 | `FRM - CARP - Arreglo - Principal` (subgrid de cuotas) | `playbooks/formulario/arreglo.md` | 3.3, 3.4 | RF-06, RF-10, RF-16 | hecho (Dev, 2026-10-20; lote C: cuatro pestañas General, Cuotas, Formatos y Cierre, `--completar`, `ya_existia`) (diseño completo en `diseno/07` §4.4) |
| 12.20 | `FRM - CARP - Version de plantilla - Principal` (subgrid de mapeo) | `playbooks/formulario/versionplantilla.md` | 3.5, 3.6 | RF-09 | hecho (Dev, 2026-10-20) (diseño completo en `diseno/07` §4.5) |
| 12.21 | `FRM - CARP - Mapeo de celda - Principal` | `playbooks/formulario/mapeocelda.md` | 3.6 | RF-09 | hecho (Dev, 2026-10-20) (diseño completo en `diseno/07` §4.6) |
| 12.26 | `FRM - CARP - Cuota - Principal` (registro de pago: fecha y monto pagado juntos; el resto de solo lectura) | `playbooks/formulario/cuota.md` | 3.4, 12.13 | RF-10 | hecho (Dev, 2026-10-20) (diseño completo en `diseno/07` §4.7) |

### 12.4 Web resource, comandos, sitemap y app

| # | Componente | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 12.22 | Web resource JavaScript mínimo `WR - CARP - JS - Comandos` *(prov.; `sanic_carp_wr_js_comandos`)*: solo invoca las Custom API, pide la confirmación previa y dispara la descarga. Sin lógica de negocio | `playbooks/web-resource/js_comandos.md` | 8.2 a 8.6 | D-17, §6 Componentes | hecho (Dev, 2026-10-20; actualizado el mismo día con las funciones de los 14 gestos, 35193 bytes, `ya_existia` con `--solo-verificar`, publicado) |
| 12.23 | Sitemap `sanic_carp_sm_controlarreglospago` (cuatro áreas, arriba) | `playbooks/app/controlarreglospago.md` (comparte con 12.25) | 12.1 a 12.13, 12.14, 12.17 a 12.21 | §6 Componentes (app) | hecho (Dev, 2026-10-20; lote C: 19 entradas, las 6 diferidas agregadas, las tres de Supervisión nuevas con `Privilege Create` sobre Versión de plantilla; falta solo el panel, lote E) (diseño completo en `diseno/07` §7.2) |
| 12.24 | Comandos del command bar para los gestos de abajo (un solo playbook). El mecanismo de cada gesto (botón, captura de motivo, confirmación) no está definido | `playbooks/importacion/comandos.md` | 12.22, 8.2 a 8.6, 7.12, 7.14 a 7.16 | D-17, RF-02, RF-14 a RF-16 | hecho (Dev, 2026-10-20): 13 botones en 3 tablas (14 elementos `Button`: Tomar va en Form y en grilla), `ya_existia` con `--solo-verificar`; falta la prueba manual en el navegador y el paso manual de regenerar la metadata del ribbon (`pruebas-humo/lote-b/RESULTADO.md`) (diseño completo en `diseno/07` §5.4 a §5.6) |
| 12.25 | App `sanic_carp_mda_controlarreglospago` (`MDA - CARP - Control de Arreglos de Pago`), una sola app para oficial y jefe | `playbooks/app/controlarreglospago.md` | 12.23, 12.24, 6.2 a 6.5 | D-01, RNF-04 | hecho (Dev, 2026-10-20) (las 8 tablas entran por `ImportSolution`, no por `AddAppComponents`; diseño completo en `diseno/07` §7.1) |

Gestos que la definición nombra (contenido de 12.24, no suman filas):

| Gesto | Quién | Lo respalda |
|---|---|---|
| Tomar conversación | Oficial, Jefe | 8.4 |
| Finalizar conversación (resultado; motivo y detalle si es "Sin respuesta") | Oficial, Jefe | 7.12 |
| Unir conversaciones | Oficial, Jefe | 8.5 |
| Reabrir conversación finalizada | Oficial, Jefe | 7.12 |
| Iniciar conversación nueva con el correo llegado a una finalizada | Oficial, Jefe | 8.6 |
| Generar formato para firma (congela el borrador) | Oficial, Jefe | 8.2 |
| Desbloquear (solo borrador) | Oficial | 7.14 |
| Formalizar (solo borrador congelado) | Oficial | 7.14 |
| Descartar (solo borrador; motivo) | Oficial | 7.14 |
| Registrar pago (fecha y monto de la cuota) | Oficial | 7.15 |
| Anular (solo vigente; motivo) | Oficial | 7.14 |
| Reabrir arreglo congelado o terminado (motivo) | solo Jefatura | 7.14 |
| Publicar versión de plantilla | Jefe | 7.16 |
| Vista previa de plantilla (versión Borrador, sobre un arreglo existente; no congela) | Jefe | 8.2 |

"Responder con adjuntos" usa el editor de correo nativo del timeline y no requiere comando propio. Solo exige la regla de remitente de 7.11 (D-28).

## 13. Identidades y puesta en marcha

Tareas de administración por entorno. No son componentes de solución ni tienen playbook de componente: se documentan en un procedimiento por entorno (definición §6, Entornos y despliegue). Responsables según la definición: administrador de Power Platform y Exchange, mensajería o TI, jefe del área, equipo de automatización. Los entornos son Dev, Test y Prod de Nicaragua, compartidos.

Nombres de BU, teams y queue (aprobados el 2026-10-09, `01-convenciones.md` §2): BU hija `Arreglos de Pago`; team propietario `Arreglos de Pago` (el team del sistema de la BU, resuelto por `isdefault`); team de jefatura `Arreglos de Pago - Jefatura`; queue `Arreglos de Pago`.

| # | Tarea | Quién | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 13.1 | Business unit hija `Arreglos de Pago` (hija de la raíz) | Admin. de Power Platform | — | D-18, convenciones §2 | pendiente (**hecha en Dev** 2026-10-09; faltan Test y Prod) |
| 13.2 | Team del sistema `Arreglos de Pago` (lo crea la plataforma con la BU; membresía = usuarios de la BU). Verificar `teamtype` propietario e `isdefault` | Admin. de Power Platform | 13.1 | D-18, D-31 | pendiente (**verificado en Dev** 2026-10-09; faltan Test y Prod) |
| 13.3 | Team `Arreglos de Pago - Jefatura`: en Dev, team propietario normal; en Test y Prod, team de grupo de Entra ID | Admin. de Power Platform y de Azure | 13.1 | D-04, D-18 | pendiente (**hecho en Dev** 2026-10-09; faltan Test y Prod) |
| 13.4 | Queue `Arreglos de Pago` dentro de la BU, con el team del sistema como dueño | Admin. de Power Platform | 13.1, 13.2 | D-02, §6 Componentes | pendiente (**hecha en Dev** 2026-10-20: queue `88b4a845-b1cc-f111-a05b-00224808ffcd`, dueño el team del sistema, owning BU `Arreglos de Pago`, filtro "All email messages". Exigió `prvReadMailbox` Básico en el Oficial, decisión del usuario; faltan Test y Prod) |
| 13.5 | Buzón compartido de Exchange (uno por entorno; solo Prod usa el real), su registro Mailbox sobre la queue, filtro "All email messages", Send As de la identidad de sincronización y acceso restringido en Exchange | Mensajería, TI y admin. de Power Platform | 13.4 | D-02, D-23, D-28, §6 Integraciones | pendiente (**hecha en Dev** 2026-10-20 sobre el buzón de prueba 1: mailbox `8ab4a845-b1cc-f111-a05b-00224808ffcd`, sincronización del servidor de entrada y salida, aprobado por API, Test & Enable con entrada y salida Success; el acceso restringido en Exchange no se configuró en el buzón de prueba; faltan Test y Prod con el buzón real) |
| 13.6 | Cuenta de servicio licenciada, dentro de la BU, con los roles 6.4 y 6.5; propietaria de los flujos y de los trabajos de purga | Admin. de Power Platform | 13.1, 6.4, 6.5 | RNF-06, D-21, §6 Componentes | pendiente (2026-10-20, **hecha solo en Dev y con desviación**: la cuenta de servicio de Dev es `admin@55xljh.onmicrosoft.com` (indicación del usuario), System Administrator en la BU raíz; no se movió ni se le asignaron roles CARP (System Administrator ya los cubre). En Test y Prod va una cuenta dedicada dentro de la BU con 6.4 y 6.5) |
| 13.7 | Usuarios de ARP movidos a la BU, con los roles 6.2 (oficiales) y 6.3 (jefe), y membresía del team Jefatura. Mover usuarios les quita los roles actuales | Admin. de Power Platform | 13.1, 13.3, 6.2, 6.3 | RNF-04, §6 Seguridad | pendiente (**hecha en Dev** 2026-10-20: `# User 8` Oficial y `# User 9` Jefe en la BU, roles solo por los teams, User 9 en Jefatura; `pruebas-humo/lote-0/scripts/l03_usuarios.py`, `ya_existia`. Faltan Test y Prod con los usuarios reales de ARP) |
| 13.8 | Asociar el team del sistema y el team Jefatura al perfil 6.1 y probar de punta a punta: entrar al grupo de Entra permite leer la cédula, salir del grupo deja de permitirlo. La prueba por team (y no por usuario) sigue abierta | Admin. de Azure y de Power Platform | 6.1, 13.2, 13.3, 13.7 | D-19, diccionario §8 | pendiente |
| 13.9 | Verificar con el administrador que ningún rol ajeno tiene profundidad Organización o BU padre sobre Email, Attachment, `msdyn_richtextfiles`, Queue, Queue Item, Mailbox ni las tablas propias. Guardar evidencia. Antes de cargar datos reales y antes de cada pase a Prod | Admin. de Power Platform | 13.1, 13.7, 6.2 a 6.5 | D-18, RNF-04 | pendiente |
| 13.10 | Verificar la configuración de organización que la solución usa y no cambia: correlación por encabezados (`In-Reply-To`), `MaxUploadFileSize` y extensiones bloqueadas | Admin. de Power Platform | — | RNF-11, D-34, §6 Entornos | pendiente |
| 13.11 | Auditoría de Dataverse: habilitarla y definir la retención de sus logs. **Bloqueada por C-02**: la retención de los logs es del entorno y no se purga con los trabajos de la solución | Admin. de Power Platform y Cumplimiento | 3.x (auditoría por tabla y columna) | RNF-10, D-20, C-02 | diseño detallado pendiente |
| 13.12 | Trabajos recurrentes de BulkDelete para la retención de 2 años (arreglos; Emails y luego conversaciones; clientes). Se crean por entorno en Prod con la identidad de la cuenta de servicio, y en Test para validarlos. La cantidad de trabajos no está cerrada (Inconsistencias, punto 2) | Equipo de automatización | 13.6, 6.5, 3.x | RNF-07, D-22 | diseño detallado pendiente |
| 13.13 | Cargar los valores de las environment variables 10.1 a 10.4 y crear las conexiones de 10.5 y 10.6 en cada entorno | Equipo de automatización | 10.x, 13.1 a 13.6 | D-23 | pendiente |
| 13.14 | Power Platform pipelines (host y configuración). Si no existe el host, coordinar con el administrador; plan B Azure DevOps o GitHub Actions | Admin. de Power Platform | 1.1 | D-23, §6 Supuestos | pendiente |
| 13.15 | Procedimiento de instalación en un entorno nuevo (importar la solución managed, registrar el paquete, publicar personalizaciones, cargar 9.1 y 9.2, tareas 13.1 a 13.13). Procedimiento escrito el 2026-10-20 en `manual/procedimiento-instalacion.md`; falta ejecutarlo en Test, y esa corrida es la que lo valida | Equipo de automatización | todos los grupos | D-23, §6 Entornos | pendiente (2026-10-20: pasó de `diseño detallado pendiente` a `pendiente`; el recuento del resumen lo ajusta el orquestador) |
| 13.16 | Verificar capacidad de base de datos y de archivos de Dataverse en el reporte del tenant (adjuntos e imágenes son el mayor consumo de archivos) | Admin. de Power Platform | — | RNF-02, RNF-11, §6 Operación | pendiente |
| 13.17 | Consultar a TI o Seguridad si la carpeta Descargas está redirigida a OneDrive (Known Folder Move) | Equipo de automatización, TI | — | RNF-03, §6 Integraciones | pendiente |

Las condiciones de la sección 4 de la definición (C-01 a C-04) son aprobaciones de negocio y de Cumplimiento, no tareas de construcción; no tienen fila. C-02 sí condiciona 13.11 y la forma final de 3.3 (R-09, enmascarado del número de tarjeta).

## Orden de construcción

Se respeta la numeración de los grupos salvo donde una dependencia exige otra cosa.

1. `1.1` solución y `2` choices (hechos).
2. `3` tablas. Antes del perfil 6.1, las columnas protegidas (3.1, 3.3) ya llevan `protegida: true`. En 3.2, la columna de fórmula se crea a mano.
3. `4` relaciones, después de las dos tablas de cada una (4.19 después de 3.8).
4. `5` claves, después de su tabla. No usan lookups, así que podrían ir antes que el grupo 4.
5. `6.1` perfil de columnas.
6. `7.1` a `7.9` código sin Dataverse. **Puede ir en paralelo con todo lo anterior**: solo necesita los choices para generar los enums.
7. `7.10` a `7.22`, después del modelo de datos y de sus contratos.
8. `8.1` paquete registrado, `8.2` a `8.6` Custom API, `8.7` steps.
9. `6.2` a `6.5` roles. Las API se restringen con un privilegio de tabla que los roles ya deben tener; los roles no dependen de las API.
10. `9.1` y `9.2` plantilla v07 (necesitan el step de publicación de 8.7).
11. `10` configuración, `11` flujos, `12` app (vistas, formularios, web resource, comandos, sitemap y app, en ese orden; el dashboard después de las vistas).
12. `13` en cada entorno. `13.1` a `13.4` y `13.6`, `13.7` antes de probar sincronización, roles o flujos. `13.9` y `13.8` antes de cargar datos reales.

## Inconsistencias encontradas

Citas en la forma `archivo:línea`. No se resolvieron: este inventario sigue la fuente más específica y lo dice.

1. **[Resuelta el 2026-10-09: volumen de Custom API: 5 (definición corregida).] Cantidad de Custom API.** `definicion.md:145-150` lista cinco (generación, reconciliación, Tomar, unir, iniciar conversación nueva). `definicion.md:620` (riesgos) habla de "plugins y 3 Custom API". El inventario usa cinco (8.2 a 8.6).
2. **[Resuelta el 2026-10-09: sin trabajo de `msdyn_richtextfiles` (definición corregida).] Purga de imágenes del editor y cantidad de trabajos BulkDelete.** `definicion.md:219` incluye en el orden de purga "las imágenes de esos correos en `msdyn_richtextfiles`". `definicion.md:347` y D-22 (`definicion.md:480`) dicen que viven dentro del Email y no necesitan trabajo propio. `definicion.md:566` estima "cuatro trabajos BulkDelete (incluidas las imágenes del editor)". `definicion.md:395` no da al rol Purga ningún permiso sobre esa tabla. Afecta 6.5 y 13.12.
3. **[Resuelta el 2026-10-09: variables: BU y team Jefatura (definición corregida).] Environment variables.** `definicion.md:415` dice "nombres de los dos teams, destinatarios de alertas y umbrales". `definicion.md:418` dice que el nombre de la **BU** y el del team Jefatura vienen de variables, y que el team propietario se resuelve por `isdefault`, sin nombre. `01-convenciones.md:79-80` confirma que el propietario no se resuelve por nombre. Además `01-convenciones.md:44` usa como ejemplo `sanic_carp_ev_teamarea` ("Team del area"), una variable que la decisión del propietario deja sin uso. Afecta 10.1 a 10.4.
4. **[Resuelta el 2026-10-09: variables 10.7 y 10.8. La 10.7 se retiró el 2026-10-10 por OA-12; sigue la 10.8.] Cómo identifican los plugins a la cuenta de servicio y a la queue.** `definicion.md:249` y `definicion.md:545` exigen que el plugin distinga la identidad de la cuenta de servicio. `definicion.md:270` exige fijar "la queue del área" como remitente. Ni la identidad ni la queue aparecen entre las variables de `definicion.md:415`. El mecanismo no está definido. En el proyecto 2026-001 esto era un parámetro de configuración.
5. **[Resuelta el 2026-10-10 por OA-3: la comprobación entra al flujo 11.2; hasta entonces, revisión manual semanal de System Jobs.] Flujo de aviso de fallo de purga.** `definicion.md:450` dice "si falla, el flujo avisa". Los flujos listados son dos más uno condicional (`definicion.md:152-155`, `definicion.md:444-446`) y ninguno cubre los trabajos de purga.
6. **[Resuelta el 2026-10-10 por OA-1: formalizar y desbloquear no piden motivo; `definicion.md` corregida.] Gestos "con motivo".** `definicion.md:143` llama "gestos con motivo" a finalizar, formalizar, desbloquear, descartar, anular y reabrir. El diccionario solo tiene columnas de motivo para descartar, anular y reabrir (`02-diccionario-datos.md:389-393`); formalizar y desbloquear no llevan motivo (`definicion.md:300-303`). Tampoco se define cómo se captura el motivo en la pantalla. Afecta 12.24.
7. **[Resuelta el 2026-10-10 por OA-2: cinco vistas, 12.1 a 12.3, 12.5 y 12.6 tal como están; `definicion.md` alineada.] Vistas de sin respuesta.** `definicion.md:122` lista como vistas separadas "correos sin respuesta" y "correos llegados a conversaciones finalizadas". `definicion.md:276` describe una sola vista que reúne ambos casos. El inventario las deja como dos (12.1 y 12.2). Además `definicion.md:122` lista "borradores" y `definicion.md:447` describe "borradores sin cambios en más de 30 días": no queda claro si es una vista o dos (12.5 y 12.6).
8. **[Resuelta el 2026-10-09: P-22 decidida y D-36 actualizada.] Moneda: pregunta cerrada en el diccionario, abierta en la definición.** `definicion.md:494` (D-36) deja abierta la pregunta de la tarjeta con saldo en dos monedas, con propuesta "sí, un arreglo por moneda". `02-diccionario-datos.md:742` (P-22) la decide al revés: "un solo arreglo, en una sola moneda". Cuando decida el dueño, la definición debe actualizarse.
9. **[Resuelta el 2026-10-09: textos del diccionario corregidos.] Textos del diccionario que quedaron viejos.**
   - `02-diccionario-datos.md:26`, `:647` y `:736` dicen que P-22 sigue abierta; `:3` y `:742` dicen que está decidida.
   - `02-diccionario-datos.md:719` (R-05) dice "no hay columna de canal de origen en el modelo"; `:48` (DD-13), `:371` y el choice 2.6 la definen.
   - `02-diccionario-datos.md:766` dice "Choices: 8 de 8"; son 9 (`:134` y `:776`).
10. **[Resuelta el 2026-10-09: convenciones corregidas.] Convenciones desactualizadas o con ejemplos distintos del diccionario.** `01-convenciones.md:146` dice que la solución existe en Dev "pendiente: se crea con su playbook", y 1.1 está hecho. `01-convenciones.md:39` da como ejemplo de clave `sanic_carp_key_arreglo_producto_numero_nroarreglo`, y el diccionario fija `sanic_carp_key_arreglo_producto_numeroproducto_numeroarreglo` (`02-diccionario-datos.md:566`). `01-convenciones.md:55` ejemplifica `WR - CARP - JS - Comandos arreglo`, pero el web resource único sirve también a Conversación (`definicion.md:151`). Manda el diccionario.
11. **[Resuelta el 2026-10-09: 3.8 la columna, 4.19 el lookup, construidos así.] Columnas sobre `email`.** `02-diccionario-datos.md:507-508` y `:776` ("2 columnas sobre `email`, una de ellas lookup") ponen `sanic_respondidoporid` junto a `sanic_tiemporespuesta`. `02-diccionario-datos.md:549` dice que REL-19 crea ese lookup, y los lookups nacen con la relación (`:279`). El inventario deja 3.8 con una columna y 4.19 con el lookup; hay que confirmarlo al redactar `playbooks/tabla/email.md`.
12. **[Resuelta el 2026-10-09: definición corregida por P-11.] Propietario en siete tablas.** `definicion.md:253` y D-31 (`:489`) fijan el team como propietario solo en Cliente, Conversación y Arreglo. `02-diccionario-datos.md:663` (P-11, aceptada) lo amplía a las siete. La definición no se actualizó; el inventario sigue P-11 (7.17).
13. **[Resuelta el 2026-10-09: roles heredados por la BU hija, verificado en Dev.] Roles en la BU hija frente a componentes de solución.** `definicion.md:381` dice que los roles "se crean en la BU ARP". `definicion.md:424` dice que la BU no es componente de solución y no viaja. No está definido cómo un rol que viaja en la solución queda disponible en la BU hija. Afecta 6.2 a 6.5 y 13.7.
14. **[Resuelta el 2026-10-09: el Jefe lleva solo lo adicional; Oficial va al team del sistema de la BU.] Alcance del rol Jefe.** `definicion.md:393` dice que el Jefe "suma" privilegios a los del Oficial, sin decir si incluye todos los del Oficial. La matriz de privilegios no existe todavía (6.2 a 6.5).
15. **[Resuelta el 2026-10-10 por OA-4: recarga controlada con script versionado y publicación por el jefe.] Promoción de la plantilla entre entornos.** `definicion.md:424` deja abierta la herramienta de migración de configuración o la recarga controlada por el jefe ("se decide en etapa 3"). El diseño no la fija (9.1 y 9.2).
