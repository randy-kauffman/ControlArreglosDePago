# 06. Inventario de componentes

Proyecto: 2026-003-control-arreglos-pago · Etapa 3 · Estado: **borrador** (2026-10-09). Completo respecto de lo que las fuentes dicen. Lo que la definición describe sin diseño detallado queda marcado `diseño detallado pendiente` y no se resuelve acá.

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
| 6 | Seguridad: perfil de columnas (1) y roles (4) | 5 | 1 | 0 | 4 |
| 7 | Código de plugins (C#, TDD estricto) | 22 | 0 | 9 | 13 |
| 8 | Registro de paquete, Custom API y steps | 7 | 0 | 1 | 6 |
| 9 | Datos semilla y de configuración | 3 | 0 | 2 | 1 |
| 10 | Configuración entre entornos (EV y connection references) | 6 | 0 | 2 | 4 |
| 11 | Flujos | 3 | 0 | 0 | 3 |
| 12 | App model-driven (vistas, dashboard, formularios, web resource, comandos, sitemap, app) | 25 | 0 | 0 | 25 |
| 13 | Identidades y puesta en marcha | 17 | 0 | 14 | 3 |
| **Total** | | **128** | **10** | **59** | **59** |

Notas del resumen:

- Las filas 13.1 a 13.3 están hechas **solo en Dev** (2026-10-09, `01-convenciones.md` §7). Faltan Test y Prod, por eso cuentan como `pendiente`.
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
| 6.1 | `CSP - CARP - Datos sensibles` (3 columnas: `sanic_cedula` leer, crear y actualizar; `sanic_cuentadebitar` leer, crear y actualizar; `sanic_instantaneacedula` solo leer). Sin nombre lógico | `playbooks/perfil-columnas/csp_carp_datos_sensibles.md` | 3.1 y 3.3 con `protegida: true` en las tres columnas | RNF-03, RNF-10, D-19, P-16; diccionario §8 | hecho (creado en Dev el 2026-10-09; `ya_existia` y muestra exportada coinciden) |

La asociación de los teams al perfil y su prueba de punta a punta no viajan en la solución: están en 13.8.

### 6.2 Roles de seguridad

Se crean con profundidad BU, sin Delete humano sobre el agregado (definición §6 Seguridad). Los privilegios por tabla están descritos en la definición; la matriz de privilegios del diseño (`diseno/`) todavía no existe. Los privilegios exactos de Attachment "se confirman en la construcción". Ninguno tiene nombre lógico.

| # | Componente | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 6.2 | `SR - CARP - Oficial` (definición: "ARP Oficial") | `playbooks/rol/sr_carp_oficial.md` | 3.1 a 3.8 y las tablas estándar Email, Attachment, Queue | RNF-04, D-17, D-28, D-31 | diseño detallado pendiente |
| 6.3 | `SR - CARP - Jefe` (definición: "ARP Jefe"; "suma" privilegios al Oficial) | `playbooks/rol/sr_carp_jefe.md` | ídem; 6.2 como base de lectura | RF-09, RF-04, RNF-04 | diseño detallado pendiente |
| 6.4 | `SR - CARP - Servicio` (definición: "ARP Servicio"; cuenta de servicio, flujos y reconciliación) | `playbooks/rol/sr_carp_servicio.md` | ídem; Mailbox, Queue Item | D-21, D-26; Create de Conversación es el privilegio de ejecución de 8.3 | diseño detallado pendiente |
| 6.5 | `SR - CARP - Purga` (definición: "ARP Purga"; trabajos de eliminación) | `playbooks/rol/sr_carp_purga.md` | ídem; Email con adjuntos y Queue Item | RNF-07, D-22 | diseño detallado pendiente |

Los roles son previos a las Custom API: cada API se restringe con el privilegio de una tabla propia, presente solo en los roles que corresponden (D-17). Las API dependen de los roles, no al revés.

## 7. Código de plugins (C#, TDD estricto, sin FakeXrmEasy)

Un solo paquete, `sanic_carp_pkg_plugins` (namespace raíz `Sanic.Carp.Plugins`, inmutable tras el primer registro). Solución `src/Sanic.Carp.sln`; pruebas `Sanic.Carp.Plugins.Tests` (xUnit, `net8.0`); librerías `net462;net8.0`. El dominio no conoce Dataverse. Se construye de adentro hacia afuera: las piezas 7.1 a 7.9 no tocan el entorno.

Estas filas **no tienen playbook**. Cada pieza se especifica con pruebas de aceptación en `src/Sanic.Carp.Plugins.Tests/Aceptacion/` y firmas públicas (ciclo de `herramientas/agentes/orden_constructor_codigo.md`). El directorio `src/` todavía no existe. El reparto en clases y carpetas es diseño detallado pendiente: las filas siguen las responsabilidades que nombra la definición, no una estructura de clases.

| # | Pieza (responsabilidad) | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 7.1 | Solución .NET, proyecto de pruebas y enums generados de los choices (`Dominio/Choices.g.cs`, vía `herramientas/codigo/generar_enums.py`) | — (convenciones §5) | 2.1 a 2.9 | Convenciones §5 y §6 | pendiente |
| 7.2 | Dominio: estados y transiciones permitidas de Conversación, Arreglo y Versión de plantilla; qué columnas son solo-sistema y quién actúa (sistema, cuenta de servicio, usuario) | — | 7.1 | D-10, D-26, D-30, diccionario §3.2 | pendiente |
| 7.3 | Dominio: normalización (número de producto, cédula, correo, Message-ID) y huella SHA-256 | — | 7.1 | P-06, D-03, ítem 1 | pendiente |
| 7.4 | Dominio: montos del arreglo (saldo remanente, total a pagar, última cuota) y generador del calendario de hasta 60 cuotas. La regla de fechas de vencimiento sigue abierta (R-03) | — | 7.1 | RF-06, RF-08, DD-12, R-04, ítems 7 y 8 | pendiente |
| 7.5 | Dominio: consecutivo por crédito (`max+1`) y comparación con el número provisional impreso | — | 7.1 | RF-07, D-09, ítem 9 | pendiente |
| 7.6 | Dominio: tiempos de la conversación y de cada respuesta (sin respuesta desde, primera respuesta, total, cantidad), en minutos | — | 7.1 | RF-03, RF-04, DD-05, ítems 1, 3 y 6 | pendiente |
| 7.7 | Dominio: validación de publicación de plantilla y mapeo (celdas A1 bien formadas, sin dos campos en la misma celda, bloque de cuotas completo, capacidad coherente) y huella del archivo | — | 7.1, 7.3 | RF-09, D-15, ítem 15 | pendiente |
| 7.8 | Generador del formato con Open XML SDK (DocumentFormat.OpenXml como ensamblado dependiente): mapeo, valores simples, totales, bloque de cuotas, limpieza de filas sobrantes, recálculo al abrir, número provisional en borrador | — | 7.1, 7.4, 7.7 | RF-08, D-13, D-16, ítem 14; spikes 1 y 1b | pendiente |
| 7.9 | Doble de prueba en memoria de `IOrganizationService` (proyecto de pruebas), validado contra respuestas reales exportadas | — | 7.1 | Convenciones §5 | pendiente |
| 7.10 | Datos: repositorios sobre `IOrganizationService` | — | 7.1 a 7.9, 3.x, 4.x | §6 Lógica | diseño detallado pendiente |
| 7.11 | Steps de Email: registro de correos entrantes en conversaciones (asíncrono), registro de respuesta enviada y cálculo de tiempos (asíncrono), remitente fijado a la queue del área (pre-create) | — | 7.2, 7.3, 7.6, 7.10, 3.8 | RF-01, RF-03, D-03, D-05, D-28, ítems 1 y 3 | diseño detallado pendiente |
| 7.12 | Steps de Conversación: columnas solo-sistema, asignación (vacío → uno mismo; otro cambio solo Jefatura), finalizar y reabrir | — | 7.2, 7.10 | RF-02, RF-15, D-04, D-30, ítems 2 y 6 | diseño detallado pendiente |
| 7.13 | Steps de Cliente: normalización, unicidad de la cédula al crear, vinculación por correo | — | 7.3, 7.10 | RF-05, D-07, P-06 | diseño detallado pendiente |
| 7.14 | Steps de Arreglo: registro en borrador (versión Vigente, canal, montos), calendario de cuotas, congelamiento, desbloqueo, formalización, descarte, anulación, reapertura | — | 7.2, 7.4, 7.5, 7.10 | RF-06, RF-07, RF-12, RF-16, D-08 a D-10, D-25, D-30, ítems 7 a 12 | diseño detallado pendiente |
| 7.15 | Steps de Cuota: solo pagos y solo con el arreglo Vigente; paso del arreglo a Terminado | — | 7.2, 7.10 | RF-10, D-10, ítem 11 | diseño detallado pendiente |
| 7.16 | Steps de Versión de plantilla y Mapeo: publicación, inmutabilidad (Update y subida por bloques), borrado de mapeo solo en Borrador | — | 7.7, 7.10 | RF-09, D-15, ítem 15 | diseño detallado pendiente |
| 7.17 | Propietario: fijar el team del sistema de la BU como dueño y rechazar otro. La definición lo fija para Cliente, Conversación y Arreglo; el diccionario lo amplía a las siete tablas (P-11) | — | 7.10 | D-31, P-11 | diseño detallado pendiente |
| 7.18 | Plugin de la Custom API de generación del formato (8.2) | — | 7.8, 7.10 | RF-08, D-13, ítem 14 | diseño detallado pendiente |
| 7.19 | Plugin de la Custom API de reconciliación de correos sin conversación (8.3), con la misma lógica de 7.11 | — | 7.11 | RF-01, D-03 | diseño detallado pendiente |
| 7.20 | Plugin de la Custom API "Tomar" conversación, con concurrencia optimista (8.4) | — | 7.12 | RF-02, D-04 | diseño detallado pendiente |
| 7.21 | Plugin de la Custom API "Unir conversaciones" (8.5), en una transacción; captura al actor antes de elevar | — | 7.12, 7.10 | RF-14, D-32, ítem 5 | diseño detallado pendiente |
| 7.22 | Plugin de la Custom API "Iniciar conversación nueva" con el correo llegado a una finalizada (8.6) | — | 7.12, 7.10 | RF-15, D-32, ítem 6 | diseño detallado pendiente |

Regla de implementación que cruza el grupo: toda regla que debe cumplirse sin importar el punto de entrada vive en el servidor (definición §6 Lógica). El cliente solo da feedback.

## 8. Registro del paquete, Custom API y steps

El paquete se compila (`dotnet pack`, fuera de este inventario) y se registra antes que cualquier API o step. Los nombres de las cinco Custom API son *(prov.)*: la definición las nombra por función (§6 Componentes, lista de cinco) y no fija nombre ni parámetros. El diseño detallado de contratos no existe todavía.

| # | Componente | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 8.1 | Paquete `sanic_carp_pkg_plugins` (registro; dependencia Open XML SDK) | `playbooks/paquete-plugins/sanic_carp_pkg_plugins.md` | 7.1 a 7.22 compilados | Convenciones §5; D-13, spike 1b | pendiente |
| 8.2 | `sanic_carp_capi_generarformato` (`CAPI - CARP - Generar formato`). Privilegio de ejecución: Read de Generación de formato (Oficial y Jefe). Parámetros: diseño detallado pendiente | `playbooks/custom-api/sanic_carp_capi_generarformato.md` | 8.1, 7.18, 3.7, 6.2, 6.3 | RF-08, D-13, D-17 | diseño detallado pendiente |
| 8.3 | `sanic_carp_capi_reconciliarcorreos` *(prov.)*. Privilegio de ejecución: Create de Conversación (solo Servicio) | `playbooks/custom-api/sanic_carp_capi_reconciliarcorreos.md` | 8.1, 7.19, 3.2, 6.4 | RF-01, D-03, D-17 | diseño detallado pendiente |
| 8.4 | `sanic_carp_capi_tomarconversacion` *(prov.)*. Privilegio de ejecución: Write de Conversación | `playbooks/custom-api/sanic_carp_capi_tomarconversacion.md` | 8.1, 7.20, 3.2, 6.2, 6.3 | RF-02, D-04, D-17 | diseño detallado pendiente |
| 8.5 | `sanic_carp_capi_unirconversaciones` *(prov.)*. Privilegio de ejecución: Write de Conversación | `playbooks/custom-api/sanic_carp_capi_unirconversaciones.md` | 8.1, 7.21, 3.2, 6.2, 6.3 | RF-14, D-17, D-32 | diseño detallado pendiente |
| 8.6 | `sanic_carp_capi_iniciarconversacionnueva` *(prov.)*. Privilegio de ejecución: Write de Conversación | `playbooks/custom-api/sanic_carp_capi_iniciarconversacionnueva.md` | 8.1, 7.22, 3.2, 6.2, 6.3 | RF-15, D-17, D-32 | diseño detallado pendiente |
| 8.7 | Steps del paquete (un solo playbook con todos los steps; nombre `STEP - CARP - tabla - mensaje - etapa`). Lista cerrada: diseño detallado pendiente | `playbooks/steps/sanic_carp_pkg_plugins.md` | 8.1, 7.11 a 7.17 | D-26, D-30, D-31, §6 Lógica | diseño detallado pendiente |

Familias de steps que la definición implica (contenido de 8.7; la lista cerrada con etapa, orden, filtros e imágenes no existe):

| Tabla o mensaje | Qué cubre | Pieza de código |
|---|---|---|
| Email, Create (asíncrono, post) | Registrar el correo entrante en su conversación | 7.11 |
| Email, Create (síncrono, pre) | Fijar la queue del área como remitente de toda respuesta con Regarding a una Conversación | 7.11 |
| Email, estado Enviado (asíncrono) | Tiempo de la respuesta, quién respondió, recalcular la conversación | 7.11 |
| Conversación, Update (pre) | Columnas solo-sistema, asignación, gestos Finalizar y Reabrir, propietario | 7.12, 7.17 |
| Cliente, Create y Update | Normalización, unicidad de la cédula, vinculación, propietario | 7.13, 7.17 |
| Arreglo, Create (pre y post) | Propietario, versión Vigente, canal, montos; calendario de cuotas | 7.14, 7.17 |
| Arreglo, Update (pre) | Solo-sistema, congelamiento, gestos con motivo, reemplazo de cuotas | 7.14 |
| Cuota, Update (pre) y paso a Terminado | Solo pagos, arreglo Vigente | 7.15 |
| Versión de plantilla, Update y subida por bloques; Mapeo, Create, Update y Delete | Publicación, inmutabilidad, borrado solo en Borrador | 7.16 |

## 9. Datos semilla y de configuración

La BU, los teams, la queue, el mailbox y las versiones de plantilla con su mapeo son **datos, no componentes de solución**: no viajan con ella (definición §6, Entornos y despliegue). Sin una versión Vigente el alta de un arreglo se rechaza (ítem 7). La forma de promover la plantilla de Test a Prod (herramienta de migración de configuración o recarga controlada por el jefe) "se decide en etapa 3" y sigue abierta.

| # | Qué | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 9.1 | Versión de plantilla v07 (fila y archivo `.xlsx`), publicada como Vigente por el flujo normal de publicación. v07 aprobada por Héctor Romero el 2026-10-09 | `playbooks/datos/versionplantilla.md` | 3.5, 8.7 (publicación), 9.2 | RF-09, C-01, D-15 | pendiente |
| 9.2 | Mapeo de celda de v07 (el diccionario §9 cuenta unas 27 filas) | `playbooks/datos/mapeocelda.md` | 3.6, 2.8, 2.9, 4.8 | RF-09, D-14, D-16 | pendiente |
| 9.3 | Datos de prueba para Dev y Test (nunca datos reales de Prod). Contenido no definido | `playbooks/datos/humo_*.md` | 3.1 a 3.7, 4.x | Definición §6 Seguridad (Datos en entornos no productivos) | diseño detallado pendiente |

## 10. Configuración entre entornos

Las **definiciones** viajan en la solución; los **valores** y las **conexiones**, no (se cargan en 13.13). La definición enumera las variables así: "nombres de los dos teams, destinatarios de alertas y umbrales de monitoreo", y aparte dice que el nombre de la BU y el del team Jefatura vienen de environment variables. Esas dos listas no coinciden del todo (Inconsistencias, punto 3). Los nombres de las variables son *(prov.)*.

| # | Componente | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 10.1 | Environment variable `sanic_carp_ev_nombrebu` *(prov.)*: nombre de la BU, por el que los plugins resuelven la BU | `playbooks/variable-entorno/sanic_carp_ev_nombrebu.md` | 1.1 | D-23, §6 Entornos | diseño detallado pendiente |
| 10.2 | Environment variable `sanic_carp_ev_nombreteamjefatura` *(prov.)*: nombre del team Jefatura | `playbooks/variable-entorno/sanic_carp_ev_nombreteamjefatura.md` | 1.1 | D-04, D-23 | diseño detallado pendiente |
| 10.3 | Environment variable `sanic_carp_ev_destinatariosalertas` *(prov.)*: destinatarios de alertas | `playbooks/variable-entorno/sanic_carp_ev_destinatariosalertas.md` | 1.1 | §6 Entornos, Operación | diseño detallado pendiente |
| 10.4 | Environment variable `sanic_carp_ev_umbralesmonitoreo` *(prov.)*: umbrales de monitoreo (puede ser más de una variable) | `playbooks/variable-entorno/sanic_carp_ev_umbralesmonitoreo.md` | 1.1 | §6 Entornos, Operación | diseño detallado pendiente |
| 10.5 | Connection reference `sanic_carp_conr_outlook` (`CONR - CARP - Outlook`), conector Office 365 Outlook | `playbooks/variable-entorno/sanic_carp_conr_outlook.md` (misma carpeta de tipo; ruta a confirmar) | 1.1 | §6 Integraciones; convenciones §2 | pendiente |
| 10.6 | Connection reference `sanic_carp_conr_dataverse` *(prov.)*, conector Dataverse | `playbooks/variable-entorno/sanic_carp_conr_dataverse.md` (ídem) | 1.1 | §6 Integraciones | pendiente |

Ambos conectores deben estar en el mismo grupo de DLP (definición §6, Integraciones).

## 11. Flujos

Solo monitoreo. La captura de correos no pasa por flujos (D-21). Son propiedad de la cuenta de servicio (13.6), con secure inputs y outputs en todas las acciones sobre tablas de ARP. Un cloud flow no tiene nombre lógico: se identifica por su display name, `Cloud Flow - CARP - COD - Nombre`, con `COD` de tres letras (BP-PP-196).

| # | Componente | Disparador | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|---|
| 11.1 | `Cloud Flow - CARP - COD - Avisar estado del mailbox` *(prov.; `COD` por definir)*: avisa por Outlook al jefe y al equipo cuando la entrada o la salida del buzón pasan a fallo | cambio de estado del Mailbox | `playbooks/flujo/<cod>.md` | 10.1 a 10.6, 6.4, 13.5, 13.6 | D-21, §6 Operación | diseño detallado pendiente |
| 11.2 | `Cloud Flow - CARP - SAL - Salud del buzon` (ID `CARP-SAL`): cada 30 minutos en horario hábil comprueba correos entrantes recientes, correos sin conversación y respuestas con error de envío; si hay correos sin conversación invoca 8.3 | recurrente | `playbooks/flujo/carp-sal.md` | 8.3, 10.3 a 10.6, 6.4, 13.5, 13.6 | D-21, RNF-08, §6 Operación | diseño detallado pendiente |
| 11.3 | Flujo diario de marcado de cuotas vencidas sin pago (respaldo de D-11). **Condicional: no se activa**, el spike 2 mostró que el operador de fecha relativa sirve. Requeriría una Custom API que la definición no lista entre las cinco | recurrente diario | — | D-11, D-21; escribe Cuota con rol Servicio | diseño detallado pendiente (condicional; no se construye salvo que se active) |

## 12. App model-driven

La definición difiere el diseño de pantallas a la etapa 3 como entregable obligatorio (§6 Componentes, app). El mockup validado (v3, `mockup/index.html`) es la referencia funcional. Todo este grupo es `diseño detallado pendiente`. La lista sigue lo que la definición **nombra o implica**; las vistas y formularios que sean estándar de cada tabla no se listan.

Áreas de navegación de la app: Conversaciones, Clientes y arreglos, Supervisión (jefe) y Administración del formato (jefe).

### 12.1 Vistas

Nombre de vista: texto de negocio, sin prefijo y sin tildes. Los nombres son *(prov.)* y salen de la frase de la definición.

| # | Vista | Tabla | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|---|
| 12.1 | Correos sin respuesta | Conversación | `playbooks/vista/correos_sin_respuesta.md` | 3.2 (incluye `sanic_antiguedad`) | RF-04, D-27 | diseño detallado pendiente |
| 12.2 | Correos llegados a conversaciones finalizadas | Conversación | `playbooks/vista/correos_en_finalizadas.md` | 3.2 | RF-15, D-32 | diseño detallado pendiente |
| 12.3 | Finalizadas sin respuesta | Conversación | `playbooks/vista/finalizadas_sin_respuesta.md` | 3.2 | RF-04, D-06 | diseño detallado pendiente |
| 12.4 | Respuestas con error de envio | Email | `playbooks/vista/respuestas_con_error_de_envio.md` | 3.8 | RF-03, §6 Lógica ítem 3 | diseño detallado pendiente |
| 12.5 | Borradores | Arreglo | `playbooks/vista/borradores.md` | 3.3 | RF-16 | diseño detallado pendiente |
| 12.6 | Borradores sin movimiento (más de 30 días) | Arreglo | `playbooks/vista/borradores_sin_movimiento.md` | 3.3 | RF-16, D-22, §6 Operación | diseño detallado pendiente |
| 12.7 | Arreglos en riesgo (vigentes con una cuota vencida sin pago; filtro relacionado, `olderthan-x-days 1` o `yesterday`) | Arreglo | `playbooks/vista/arreglos_en_riesgo.md` | 3.3, 3.4, 4.5 | RF-11, D-11 | diseño detallado pendiente |
| 12.8 | Arreglos de prestamo | Arreglo | `playbooks/vista/arreglos_de_prestamo.md` | 3.3, 2.3 | RF-17, D-35 | diseño detallado pendiente |
| 12.9 | Arreglos de tarjeta | Arreglo | `playbooks/vista/arreglos_de_tarjeta.md` | 3.3, 2.3 | RF-17, D-35 | diseño detallado pendiente |
| 12.10 | Busqueda rapida de Cliente (cédula, nombre, correo, celular) | Cliente | `playbooks/vista/busqueda_rapida_cliente.md` | 3.1, 6.1 | RF-13, D-12 | diseño detallado pendiente |
| 12.11 | Busqueda rapida de Arreglo (número de producto) | Arreglo | `playbooks/vista/busqueda_rapida_arreglo.md` | 3.3 | RF-13, D-12 | diseño detallado pendiente |
| 12.12 | Arreglos del cliente (subgrid con todos los estados) | Arreglo | `playbooks/vista/arreglos_del_cliente.md` | 3.3, 4.2 | RF-13, ítem 13 | diseño detallado pendiente |
| 12.13 | Otras vistas "por perfil" (oficial y jefe). La definición no las enumera | varias | `playbooks/vista/` (por definir) | 3.x, 4.x | §6 Componentes (app) | diseño detallado pendiente |

### 12.2 Dashboard y gráficos

Los gráficos agregan promedios por oficial; las vistas no (D-27). En el proyecto no hay herramienta ni tipo de playbook para dashboards o gráficos (`herramientas/README.md`). Se decide al diseñarlos.

| # | Componente | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 12.14 | `DASH - CARP - Supervision` (dashboard del jefe) | `playbooks/dashboard/supervision.md` | 12.15, 12.16 | RF-04, D-27 | diseño detallado pendiente |
| 12.15 | `CHT - CARP - Promedio por respuesta` *(prov.)*: promedio de `sanic_tiemporespuesta` sobre Email, agrupado por `sanic_respondidoporid` | `playbooks/grafico/promedio_por_respuesta.md` | 3.8, 4.19 | RF-03, RF-04, D-27 | diseño detallado pendiente |
| 12.16 | `CHT - CARP - Tiempo total` *(prov.)*: promedio de `sanic_tiempototal` sobre Conversación, agrupado por oficial asignado, sin las finalizadas "Sin respuesta" (a confirmar con negocio) | `playbooks/grafico/tiempo_total.md` | 3.2, 4.10 | RF-04, D-27 | diseño detallado pendiente |

### 12.3 Formularios principales

Nombre: `FRM - CARP - Tabla - Tipo`. La definición no nombra un formulario para Cuota (los pagos se registran sobre la cuota) ni para Generación de formato (bitácora); no se listan. La Conversación muestra el timeline con el editor de correo nativo, y avisa si el cliente tiene otra conversación abierta.

| # | Formulario | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 12.17 | `FRM - CARP - Cliente - Principal` (subgrid 12.12) | `playbooks/formulario/cliente.md` | 3.1, 12.12 | RF-05, RF-13 | diseño detallado pendiente |
| 12.18 | `FRM - CARP - Conversacion - Principal` (timeline, aviso de otra conversación abierta) | `playbooks/formulario/conversacion.md` | 3.2, 3.8 | RF-01, RF-03, RF-14 | diseño detallado pendiente |
| 12.19 | `FRM - CARP - Arreglo - Principal` (subgrid de cuotas) | `playbooks/formulario/arreglo.md` | 3.3, 3.4 | RF-06, RF-10, RF-16 | diseño detallado pendiente |
| 12.20 | `FRM - CARP - Version de plantilla - Principal` (subgrid de mapeo) | `playbooks/formulario/versionplantilla.md` | 3.5, 3.6 | RF-09 | diseño detallado pendiente |
| 12.21 | `FRM - CARP - Mapeo de celda - Principal` | `playbooks/formulario/mapeocelda.md` | 3.6 | RF-09 | diseño detallado pendiente |

### 12.4 Web resource, comandos, sitemap y app

| # | Componente | Playbook | Depende de | Origen | Estado |
|---|---|---|---|---|---|
| 12.22 | Web resource JavaScript mínimo `WR - CARP - JS - Comandos` *(prov.; `sanic_carp_wr_js_comandos`)*: solo invoca las Custom API, pide la confirmación previa y dispara la descarga. Sin lógica de negocio | `playbooks/web-resource/js_comandos.md` | 8.2 a 8.6 | D-17, §6 Componentes | diseño detallado pendiente |
| 12.23 | Sitemap `sanic_carp_sm_controlarreglospago` (cuatro áreas, arriba) | `playbooks/app/controlarreglospago.md` (comparte con 12.25) | 12.1 a 12.13, 12.14, 12.17 a 12.21 | §6 Componentes (app) | diseño detallado pendiente |
| 12.24 | Comandos del command bar para los gestos de abajo (un solo playbook). El mecanismo de cada gesto (botón, captura de motivo, confirmación) no está definido | `playbooks/importacion/comandos.md` (ruta a confirmar) | 12.22, 8.2 a 8.6, 7.12, 7.14 a 7.16 | D-17, RF-02, RF-14 a RF-16 | diseño detallado pendiente |
| 12.25 | App `sanic_carp_mda_controlarreglospago` (`MDA - CARP - Control de Arreglos de Pago`), una sola app para oficial y jefe | `playbooks/app/controlarreglospago.md` | 12.23, 12.24, 6.2 a 6.5 | D-01, RNF-04 | diseño detallado pendiente |

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
| 13.4 | Queue `Arreglos de Pago` dentro de la BU, con el team del sistema como dueño | Admin. de Power Platform | 13.1, 13.2 | D-02, §6 Componentes | pendiente |
| 13.5 | Buzón compartido de Exchange (uno por entorno; solo Prod usa el real), su registro Mailbox sobre la queue, filtro "All email messages", Send As de la identidad de sincronización y acceso restringido en Exchange | Mensajería, TI y admin. de Power Platform | 13.4 | D-02, D-23, D-28, §6 Integraciones | pendiente |
| 13.6 | Cuenta de servicio licenciada, dentro de la BU, con los roles 6.4 y 6.5; propietaria de los flujos y de los trabajos de purga | Admin. de Power Platform | 13.1, 6.4, 6.5 | RNF-06, D-21, §6 Componentes | pendiente |
| 13.7 | Usuarios de ARP movidos a la BU, con los roles 6.2 (oficiales) y 6.3 (jefe), y membresía del team Jefatura. Mover usuarios les quita los roles actuales | Admin. de Power Platform | 13.1, 13.3, 6.2, 6.3 | RNF-04, §6 Seguridad | pendiente |
| 13.8 | Asociar el team del sistema y el team Jefatura al perfil 6.1 y probar de punta a punta: entrar al grupo de Entra permite leer la cédula, salir del grupo deja de permitirlo. La prueba por team (y no por usuario) sigue abierta | Admin. de Azure y de Power Platform | 6.1, 13.2, 13.3, 13.7 | D-19, diccionario §8 | pendiente |
| 13.9 | Verificar con el administrador que ningún rol ajeno tiene profundidad Organización o BU padre sobre Email, Attachment, `msdyn_richtextfiles`, Queue, Queue Item, Mailbox ni las tablas propias. Guardar evidencia. Antes de cargar datos reales y antes de cada pase a Prod | Admin. de Power Platform | 13.1, 13.7, 6.2 a 6.5 | D-18, RNF-04 | pendiente |
| 13.10 | Verificar la configuración de organización que la solución usa y no cambia: correlación por encabezados (`In-Reply-To`), `MaxUploadFileSize` y extensiones bloqueadas | Admin. de Power Platform | — | RNF-11, D-34, §6 Entornos | pendiente |
| 13.11 | Auditoría de Dataverse: habilitarla y definir la retención de sus logs. **Bloqueada por C-02**: la retención de los logs es del entorno y no se purga con los trabajos de la solución | Admin. de Power Platform y Cumplimiento | 3.x (auditoría por tabla y columna) | RNF-10, D-20, C-02 | diseño detallado pendiente |
| 13.12 | Trabajos recurrentes de BulkDelete para la retención de 2 años (arreglos; Emails y luego conversaciones; clientes). Se crean por entorno en Prod con la identidad de la cuenta de servicio, y en Test para validarlos. La cantidad de trabajos no está cerrada (Inconsistencias, punto 2) | Equipo de automatización | 13.6, 6.5, 3.x | RNF-07, D-22 | diseño detallado pendiente |
| 13.13 | Cargar los valores de las environment variables 10.1 a 10.4 y crear las conexiones de 10.5 y 10.6 en cada entorno | Equipo de automatización | 10.x, 13.1 a 13.6 | D-23 | pendiente |
| 13.14 | Power Platform pipelines (host y configuración). Si no existe el host, coordinar con el administrador; plan B Azure DevOps o GitHub Actions | Admin. de Power Platform | 1.1 | D-23, §6 Supuestos | pendiente |
| 13.15 | Procedimiento de instalación en un entorno nuevo (importar la solución managed, registrar el paquete, publicar personalizaciones, cargar 9.1 y 9.2, tareas 13.1 a 13.13). El procedimiento no está escrito | Equipo de automatización | todos los grupos | D-23, §6 Entornos | diseño detallado pendiente |
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

1. **Cantidad de Custom API.** `definicion.md:145-150` lista cinco (generación, reconciliación, Tomar, unir, iniciar conversación nueva). `definicion.md:620` (riesgos) habla de "plugins y 3 Custom API". El inventario usa cinco (8.2 a 8.6).
2. **Purga de imágenes del editor y cantidad de trabajos BulkDelete.** `definicion.md:219` incluye en el orden de purga "las imágenes de esos correos en `msdyn_richtextfiles`". `definicion.md:347` y D-22 (`definicion.md:480`) dicen que viven dentro del Email y no necesitan trabajo propio. `definicion.md:566` estima "cuatro trabajos BulkDelete (incluidas las imágenes del editor)". `definicion.md:395` no da al rol Purga ningún permiso sobre esa tabla. Afecta 6.5 y 13.12.
3. **Environment variables.** `definicion.md:415` dice "nombres de los dos teams, destinatarios de alertas y umbrales". `definicion.md:418` dice que el nombre de la **BU** y el del team Jefatura vienen de variables, y que el team propietario se resuelve por `isdefault`, sin nombre. `01-convenciones.md:79-80` confirma que el propietario no se resuelve por nombre. Además `01-convenciones.md:44` usa como ejemplo `sanic_carp_ev_teamarea` ("Team del area"), una variable que la decisión del propietario deja sin uso. Afecta 10.1 a 10.4.
4. **Cómo identifican los plugins a la cuenta de servicio y a la queue.** `definicion.md:249` y `definicion.md:545` exigen que el plugin distinga la identidad de la cuenta de servicio. `definicion.md:270` exige fijar "la queue del área" como remitente. Ni la identidad ni la queue aparecen entre las variables de `definicion.md:415`. El mecanismo no está definido. En el proyecto 2026-001 esto era un parámetro de configuración.
5. **Flujo de aviso de fallo de purga.** `definicion.md:450` dice "si falla, el flujo avisa". Los flujos listados son dos más uno condicional (`definicion.md:152-155`, `definicion.md:444-446`) y ninguno cubre los trabajos de purga.
6. **Gestos "con motivo".** `definicion.md:143` llama "gestos con motivo" a finalizar, formalizar, desbloquear, descartar, anular y reabrir. El diccionario solo tiene columnas de motivo para descartar, anular y reabrir (`02-diccionario-datos.md:389-393`); formalizar y desbloquear no llevan motivo (`definicion.md:300-303`). Tampoco se define cómo se captura el motivo en la pantalla. Afecta 12.24.
7. **Vistas de sin respuesta.** `definicion.md:122` lista como vistas separadas "correos sin respuesta" y "correos llegados a conversaciones finalizadas". `definicion.md:276` describe una sola vista que reúne ambos casos. El inventario las deja como dos (12.1 y 12.2). Además `definicion.md:122` lista "borradores" y `definicion.md:447` describe "borradores sin cambios en más de 30 días": no queda claro si es una vista o dos (12.5 y 12.6).
8. **Moneda: pregunta cerrada en el diccionario, abierta en la definición.** `definicion.md:494` (D-36) deja abierta la pregunta de la tarjeta con saldo en dos monedas, con propuesta "sí, un arreglo por moneda". `02-diccionario-datos.md:742` (P-22) la decide al revés: "un solo arreglo, en una sola moneda". Cuando decida el dueño, la definición debe actualizarse.
9. **Textos del diccionario que quedaron viejos.**
   - `02-diccionario-datos.md:26`, `:647` y `:736` dicen que P-22 sigue abierta; `:3` y `:742` dicen que está decidida.
   - `02-diccionario-datos.md:719` (R-05) dice "no hay columna de canal de origen en el modelo"; `:48` (DD-13), `:371` y el choice 2.6 la definen.
   - `02-diccionario-datos.md:766` dice "Choices: 8 de 8"; son 9 (`:134` y `:776`).
10. **Convenciones desactualizadas o con ejemplos distintos del diccionario.** `01-convenciones.md:146` dice que la solución existe en Dev "pendiente: se crea con su playbook", y 1.1 está hecho. `01-convenciones.md:39` da como ejemplo de clave `sanic_carp_key_arreglo_producto_numero_nroarreglo`, y el diccionario fija `sanic_carp_key_arreglo_producto_numeroproducto_numeroarreglo` (`02-diccionario-datos.md:566`). `01-convenciones.md:55` ejemplifica `WR - CARP - JS - Comandos arreglo`, pero el web resource único sirve también a Conversación (`definicion.md:151`). Manda el diccionario.
11. **Columnas sobre `email`.** `02-diccionario-datos.md:507-508` y `:776` ("2 columnas sobre `email`, una de ellas lookup") ponen `sanic_respondidoporid` junto a `sanic_tiemporespuesta`. `02-diccionario-datos.md:549` dice que REL-19 crea ese lookup, y los lookups nacen con la relación (`:279`). El inventario deja 3.8 con una columna y 4.19 con el lookup; hay que confirmarlo al redactar `playbooks/tabla/email.md`.
12. **Propietario en siete tablas.** `definicion.md:253` y D-31 (`:489`) fijan el team como propietario solo en Cliente, Conversación y Arreglo. `02-diccionario-datos.md:663` (P-11, aceptada) lo amplía a las siete. La definición no se actualizó; el inventario sigue P-11 (7.17).
13. **Roles en la BU hija frente a componentes de solución.** `definicion.md:381` dice que los roles "se crean en la BU ARP". `definicion.md:424` dice que la BU no es componente de solución y no viaja. No está definido cómo un rol que viaja en la solución queda disponible en la BU hija. Afecta 6.2 a 6.5 y 13.7.
14. **Alcance del rol Jefe.** `definicion.md:393` dice que el Jefe "suma" privilegios a los del Oficial, sin decir si incluye todos los del Oficial. La matriz de privilegios no existe todavía (6.2 a 6.5).
15. **Promoción de la plantilla entre entornos.** `definicion.md:424` deja abierta la herramienta de migración de configuración o la recarga controlada por el jefe ("se decide en etapa 3"). El diseño no la fija (9.1 y 9.2).
