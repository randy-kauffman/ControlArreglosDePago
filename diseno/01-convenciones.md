# 01. Convenciones de nombres y estructura

Proyecto: 2026-003-control-arreglos-pago · Etapa 3 · Estado: **secciones 0 a 6 acordadas con el usuario (Desarrollador de soluciones de automatización) el 2026-10-09**

> Este documento **instancia** para este proyecto el estándar del estudio, que vive en `power-platform-practicas/references/convenciones-nombres.md` (reglas **BP-PP-181 a BP-PP-197**, origen `estandar`). Si algo de acá contradice al estándar y no figura como excepción en la sección 0, manda el estándar. Cumple BP-PP-181: se escribe antes de crear el primer componente.

## 0. Parámetros de este proyecto

| Parámetro | Valor |
|---|---|
| Publisher | **Sistemas Abiertos Nicaragua**, unique name `Sistemas_Abiertos_Nicaragua` (verificado en Dev el 2026-10-09; es el mismo que usó el proyecto 2026-001). **Cuidado: en Dev hay otro publisher con el mismo prefijo `sanic`, "Sanic Corp" (unique name `sanic`, prefijo de opciones 10000, el mismo número que el publisher por defecto del entorno).** Todo componente se crea fijando el publisher por su unique name, nunca por el prefijo |
| Prefijo numérico de opciones | `15946` → los valores de choice son `159460001`, `159460002`… (verificado en Dev el 2026-10-09) |
| Prefijo de publisher | `sanic` |
| Abreviatura de la solución | `CARP` / `carp` (Control de Arreglos de Pago; decisión del usuario del 2026-10-09) |
| Nombre completo | Control de Arreglos de Pago |
| Solución | `sanic_carp_sol_controlarreglospago` · display name `SOL - CARP - Control de Arreglos de Pago` |
| Idioma base de Dev | **1033 (inglés)**; idiomas provisionados en Dev: **1033 y 3082** (verificado con `RetrieveProvisionedLanguages` el 2026-10-09) |
| Idiomas del proyecto | **`idiomas: [1033, 3082]`**. **Etiquetas de metadatos** (tablas, columnas, choices, vistas, formularios): se escriben en **los dos** LCID, en español. **Etiquetas embebidas** en el XML de un componente (títulos del sitemap, textos de comandos y las que demuestre cada export real): una por cada idioma de la lista, porque ahí la plataforma no cae al idioma base. Origen: tropiezo A-28 del proyecto 2026-001, una solución con base en inglés importada en un entorno con base en español no mostró ninguna página |
| Idioma base de Dev, Test y Prod de BAC Nicaragua | **Pendiente de confirmar con el administrador de BAC.** Si alguno tiene base 3082, las etiquetas en 3082 son las que se ven; por eso se escriben siempre las dos |
| Excepciones al estándar | **Una**, decidida por el usuario el 2026-10-09. El lookup a `systemuser` que guarda quién hizo un evento se llama `sanic_<evento>porid` (por ejemplo `sanic_anuladoporid`, `sanic_formalizadoporid`), y no `sanic_<evento>por` como dice BP-PP-191. Motivo: BP-PP-187 es la regla de todos los lookups ("todo lookup termina en `id`") y se prefiere una regla sin casos especiales a una excepción que obligue a recordar cuáles lookups no terminan en `id`. Nota: el proyecto 2026-001 usó `...por` sin `id`, así que los dos proyectos difieren en este nombre. El resto de BP-PP-191 (fechas de auditoría `sanic_fecha<evento>` y comportamiento Usuario local) se cumple tal cual |

## 1. Las tres reglas que gobiernan todo

1. **Display name**: `TIPO - CARP - Nombre`. Ejemplo: `CH - CARP - Resultado de conversacion`.
2. **Nombre lógico / schema / unique name**: `prefijo_abreviatura_tipo_nombre`, un solo orden para todos los componentes. Ejemplo: `sanic_carp_ch_resultadoconversacion`.
3. **Todo nombre lógico va en minúscula**, en español, sin tildes ni ñ, sin separadores dentro del nombre. Schema name y logical name quedan idénticos: en Web API, `@odata.bind` y las propiedades de navegación respetan las mayúsculas del schema name. Para leer está el display name.

Si un componente **no tiene nombre lógico** en la plataforma, aplica solo la regla 1.

## 2. Tabla de componentes

| Componente | Display name | Nombre lógico | Ejemplo |
|---|---|---|---|
| Solución | `SOL - CARP - Nombre completo` | `sanic_carp_sol_nombre` | `SOL - CARP - Control de Arreglos de Pago` · `sanic_carp_sol_controlarreglospago` |
| Tabla | **Nombre limpio de negocio** (BP-PP-186) | `sanic_carp_tbl_nombre`, en singular | `Conversacion` / `Conversaciones` · `sanic_carp_tbl_conversacion` |
| Columna | Texto de negocio | `sanic_nombre` (sin abreviatura ni tipo) | `Sin respuesta desde` · `sanic_sinrespuestadesde` |
| Lookup | Texto de negocio | `sanic_<destino>id` | `sanic_clienteid`, `sanic_conversacionid` |
| Choice global | `CH - CARP - Nombre` | `sanic_carp_ch_nombre` | `sanic_carp_ch_productocrediticio` |
| Clave alternativa | `KEY - CARP - Tabla - Campos` | `sanic_carp_key_<tabla>_<campos>` | `sanic_carp_key_arreglo_producto_numero_nroarreglo` |
| Relación 1:N | no tiene | `sanic_carp_<padre>_<hijo>`; con más de una relación entre el mismo par, sufijo con el lookup sin prefijo | `sanic_carp_arreglo_cuota` · `sanic_carp_systemuser_arreglo_anuladoporid` |
| Custom API | `CAPI - CARP - Nombre` | `sanic_carp_capi_nombre` | `CAPI - CARP - Generar formato` · `sanic_carp_capi_generarformato` |
| Parámetro de entrada de Custom API | `CAPI_IP - CARP - Nombre` | `uniquename` **corto**: `nombre` · `name`: `sanic_carp_capiip_<api>_<nombre>` | `arregloid` · `sanic_carp_capiip_generarformato_arregloid` |
| Parámetro de salida de Custom API | `CAPI_OP - CARP - Nombre` | `uniquename` corto · `name`: `sanic_carp_capiop_<api>_<nombre>` | `archivo` · `sanic_carp_capiop_generarformato_archivo` |
| Environment variable | `EV - CARP - Nombre` | `sanic_carp_ev_nombre` | `EV - CARP - Team del area` · `sanic_carp_ev_teamarea` |
| Connection reference | `CONR - CARP - Nombre` | `sanic_carp_conr_nombre` | `CONR - CARP - Outlook` · `sanic_carp_conr_outlook` |
| Cloud flow | `Cloud Flow - CARP - COD - Nombre`, con `CARP-COD` como identificador corto de tres letras (BP-PP-196) | **no tiene**: Power Automate no popula `uniquename` al crear un cloud flow (verificado en el proyecto 2026-001) | `Cloud Flow - CARP - SAL - Salud del buzon` · ID `CARP-SAL` |
| Security role | `SR - CARP - Nombre` | **no tiene** | `SR - CARP - Oficial` |
| Column security profile | `CSP - CARP - Nombre` | **no tiene** | `CSP - CARP - Datos sensibles` |
| App model-driven | `MDA - CARP - Nombre` | `sanic_carp_mda_nombre` | `MDA - CARP - Control de Arreglos de Pago` · `sanic_carp_mda_controlarreglospago` |
| Site map | no aplica | `sanic_carp_sm_nombre` | `sanic_carp_sm_controlarreglospago` |
| Vista | Texto de negocio, sin prefijo y sin tildes | no tiene | `Correos sin respuesta` |
| Formulario | `FRM - CARP - Tabla - Tipo` | no tiene | `FRM - CARP - Conversacion - Principal` |
| Regla de negocio | `BR - CARP - Tabla - Nombre` | no tiene | |
| Dashboard / gráfico | `DASH - CARP - Nombre` / `CHT - CARP - Nombre` | no tiene | `DASH - CARP - Supervision` |
| Web resource | `WR - CARP - TIPO - Nombre` | `sanic_carp_wr_<tipo>_nombre` | `WR - CARP - JS - Comandos arreglo` · `sanic_carp_wr_js_comandosarreglo` |
| Paquete de plugins | no aplica | `sanic_carp_pkg_plugins` | |
| Step de plugin | `STEP - CARP - tabla - mensaje - etapa` | no tiene | `STEP - CARP - email - Create - PreOperation` |
| Usuario de aplicación | `APP - CARP - Nombre` | no aplica | |

### Equivalencia con los nombres de `definicion.md`

La definición usa nombres de negocio. Al construir mandan estos:

| En `definicion.md` | Nombre en la plataforma |
|---|---|
| Rol "ARP Oficial" | `SR - CARP - Oficial` |
| Rol "ARP Jefe" | `SR - CARP - Jefe` |
| Rol "ARP Servicio" | `SR - CARP - Servicio` |
| Rol "ARP Purga" | `SR - CARP - Purga` |
| Tablas Cliente, Conversación, Arreglo, Cuota, Versión de plantilla, Mapeo de celda, Generación de formato | `sanic_carp_tbl_cliente`, `_conversacion`, `_arreglo`, `_cuota`, `_versionplantilla`, `_mapeocelda`, `_generacionformato`. Los definitivos, con sus display names, los fija el diccionario de datos (`02-diccionario-datos.md`) |

### Fuera de la tabla de tipos (aprobados el 2026-10-09)

La business unit, los teams, la queue y el mailbox **no son componentes de solución**: son datos de configuración por entorno (definición §6, Entornos y despliegue), y el estándar no les da `TIPO`. Por la regla del estándar, no se improvisa un tipo dentro del proyecto. Nombres aprobados por el usuario el 2026-10-09:

| Elemento | Nombre | Motivo |
|---|---|---|
| Business unit hija | `Arreglos de Pago` | Es el nombre del área; lo ven los administradores del entorno compartido |
| Team propietario de los registros | **Pendiente de decisión.** Al crear una business unit, Dataverse crea solo un team propietario con su mismo nombre (`Arreglos de Pago`), cuya membresía administra el sistema: todos los usuarios de la business unit. El nombre de un team es único dentro de su business unit, así que no puede existir otro `Arreglos de Pago` en ella (Learn, *Teams in Dataverse*, verificado el 2026-10-09). Opción propuesta: usar ese team del sistema como propietario | Ver decisión pendiente |
| Team de jefatura | `Arreglos de Pago - Jefatura` | Lo resuelven los plugins por nombre (environment variable), así que tiene que ser estable |
| Queue | `Arreglos de Pago` | Es lo que ve el cliente como nombre del remitente en algunos clientes de correo |

**Tipo de team por entorno.** En el entorno de desarrollo del estudio, el team de jefatura es un team normal (propietario), para probar. En los entornos de BAC es un team de grupo de Entra ID, como pide la definición (D-18).

## 3. Versionado de la solución

`MAYOR.MENOR.PARCHE.BUILD` (BP-PP-190).

| Dígito | Sube cuando… | Al subir |
|---|---|---|
| **MAYOR** | Hay un rediseño que rompe compatibilidad (contratos de Custom API, modelo de datos). Arranca en `1`. | MENOR, PARCHE y BUILD vuelven a 0 |
| **MENOR** | Se entrega funcionalidad nueva a Prod. **No está atado a las fases.** | PARCHE y BUILD vuelven a 0 |
| **PARCHE** | Se corrige algo ya entregado, sin funcionalidad nueva. | BUILD vuelve a 0 |
| **BUILD** | Cada export desde Dev. | — |

Lo que llega a Prod queda etiquetado en git con su versión.

## 4. Idioma y forma

- **Nombres lógicos**: regla 3 de la sección 1.
- **Nombres visibles**: en español, **sin tildes ni ñ** (BP-PP-197, confirmado por el usuario el 2026-10-09): tablas, columnas, choices y sus opciones, claves, lookups, roles, perfiles, flujos, vistas, formularios, steps y variables. Se escribe la palabra sin la tilde (`Conversacion`, `Version de plantilla`, `Anulacion`). Las **descripciones** son prosa y sí llevan ortografía completa.
- **Código C#**: identificadores técnicos en inglés y términos del dominio en español tal como los nombra el negocio (`Conversacion`, `Arreglo`, `Cuota`), nunca traducidos. PascalCase en el código.
- **Fechas de auditoría propias**: `sanic_fecha<evento>` y quién lo hizo `sanic_<evento>porid` (lookup a `systemuser`; excepción a BP-PP-191 declarada en la sección 0, para cumplir BP-PP-187). Ejemplo: `sanic_fechaanulacion`, `sanic_anuladoporid`. Toda columna de fecha y hora usa el comportamiento **Usuario local** (BP-PP-191): guarda el instante en UTC y permite restar fechas para medir tiempos. No se puede cambiar después de crear la columna. Las columnas que solo representan un día (vencimiento de una cuota) son **Solo fecha** y el diccionario lo declara columna por columna.
- **Columna primaria** de cada tabla: `sanic_nombre`, nunca texto libre (BP-PP-192). El diccionario declara para cada tabla si es calculada, autonumérica o la clave de negocio.

## 5. Ensamblados y código

| Elemento | Valor |
|---|---|
| Repositorio | este (`ControlArreglosDePago`), rama `main`; inicializado el 2026-10-09, sin remoto |
| Solución .NET | `src/Sanic.Carp.sln` |
| Paquete de plugins (ensamblados dependientes, por Open XML SDK) | `Sanic.Carp.Plugins` (.NET Framework 4.6.2, **sin firmar**: en un paquete no hace falta y obligaría a firmar sus dependencias); en Dataverse `sanic_carp_pkg_plugins`. Validado en el spike 1b con DocumentFormat.OpenXml 2.20.0 |
| Namespace raíz | `Sanic.Carp.Plugins`: **inmutable tras el primer registro** (renombrar tipos rompe el registro en silencio) |
| Frameworks de destino | Librerías: **`net462;net8.0`**. net462 es lo que ejecuta el sandbox; net8.0 existe para que las pruebas corran en el puesto de trabajo Linux. Compilar net462 en Linux requiere `Microsoft.NETFramework.ReferenceAssemblies`. Prohibido usar APIs que no existan en net462 |
| Pruebas | `Sanic.Carp.Plugins.Tests` (`net8.0`, xUnit, TDD estricto). **Sin FakeXrmEasy**: sus versiones 2.x y 3.x exigen licencia comercial (regla BP-PP-159). El dominio se prueba con pruebas unitarias puras; la capa que toca `IOrganizationService` con un doble propio en memoria, validado contra respuestas reales exportadas |
| Solución desempaquetada | `solution/` (`pac solution unpack`), sin binarios en git |

```
ControlArreglosDePago/
├── ficha-solicitud.md, validacion.md, factibilidad-tecnica.md, definicion.md, charter.md, tropiezos.md
├── material/      ← formato F-NIC-0000052 original. Sin datos de clientes
├── mockup/        ← mockup funcional validado (v3)
├── spikes/        ← código descartable y conclusiones de los spikes
├── diseno/        ← diseño detallado: convenciones, diccionario, matriz de privilegios, contratos, inventario
├── playbooks/     ← un playbook por componente (power-platform-especificar)
├── herramientas/  ← herramientas de construcción y verificación
├── src/           ← C# (plugins y pruebas)
├── solution/      ← solución desempaquetada
└── local/         ← NO versionado: secretos y documentos de contexto
```

## 6. Valores de choice

Prefijo de opciones del publisher más correlativo de 4 dígitos: `159460001`, `159460002`… (BP-PP-193). El código C# nunca usa el literal: usa un `enum` generado desde el diccionario. Los correlativos son estables y no se reordenan; una opción que deja de usarse se retira de las vistas, no se borra.

## 7. Verificado y pendiente de verificar

| Hecho | Estado |
|---|---|
| Unique name del publisher y prefijo numérico de opciones | Verificado en Dev el 2026-10-09: `Sistemas_Abiertos_Nicaragua`, `sanic`, `15946`. Existe un segundo publisher con prefijo `sanic` ("Sanic Corp", `10000`) |
| Idiomas provisionados en Dev | Verificado el 2026-10-09: `[1033, 3082]`, base 1033 |
| Idioma base de los entornos de BAC Nicaragua | **Pendiente**, con el administrador de BAC |
| Nombre de step: máximo 256 caracteres; schema name de relación: máximo 100; identificador de tabla: máximo 95 | Verificado en el proyecto 2026-001 (Learn y Dev) |
| El `uniquename` de un parámetro de Custom API no exige prefijo y puede repetirse entre APIs | Verificado en el proyecto 2026-001 |
| Cloud flows sin `uniquename` | Verificado en el proyecto 2026-001: se identifican solo por display name |
| La solución existe en Dev | **Pendiente**: se crea con su playbook, primer componente del inventario |
| Nombres de la business unit, los teams y la queue | Aprobados el 2026-10-09 (sección 2), salvo el team propietario, pendiente de decisión. En Dev (`org36e60d9d`) se crearon el 2026-10-09 la business unit `Arreglos de Pago` (hija de la raíz) y el team `Arreglos de Pago - Jefatura` (propietario, en esa business unit); el team `Arreglos de Pago` lo creó la plataforma con la business unit. Verificado releyendo |
