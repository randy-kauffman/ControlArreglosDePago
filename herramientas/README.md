# Herramientas de construcción · Control de Arreglos de Pago (2026-003)

Herramientas del puesto de trabajo para construir y verificar los componentes de la solución `sanic_carp_sol_controlarreglospago`. Son la adaptación de las del proyecto 2026-001 (M-PPP) al contrato de `power-platform-construir` (`references/herramientas/patrones.md` y `pruebas.md`) y a la skill `power-platform-especificar` **0.2.0**. Una sola carpeta de herramientas por proyecto.

Contrato, en una línea: una herramienta por tipo de playbook; entrada = la ruta del playbook; los únicos datos que lee son sus dos bloques ` ```json ` (`## 1. Identidad` y `## 2. Qué se crea`); idempotente y nunca destructiva; una sola función de verificación para los tres caminos (recién creado, ya existía, `--solo-verificar`); un 2xx no es una verificación; la última línea de la salida es un JSON `{"estado", "componente", "detalle"}` con `creado | actualizado | ya_existia | difiere | bloqueado | error`.

## Qué hay, por tipo de componente

Todas viven en `construir/` y se llaman `python3 herramientas/construir/<herramienta>.py <playbook.md> [flags]`. `--solo-verificar` solo lee.

| Componente | Herramienta | Comprobación independiente (XML exportado) | Estado |
|---|---|---|---|
| Choice global | `choice_global.py` | `muestra_choice.py` | Maduro en el proyecto anterior (14 ciclos); migrado a `idiomas` |
| Tabla y columnas propias | `tabla.py` | `muestra_tabla.py` | Maduro para los 9 tipos de la skill 0.1.0 (10 ciclos); **sin ensayar** lo nuevo (ver abajo) |
| Columnas propias sobre una tabla estándar (`tipo: columnas-estandar`) | `tabla.py` (despacha a `columnas_estandar.py`) | no hay (sin muestra real) | **Sin ensayar** (0 ciclos): siempre `bloqueado` salvo ensayo |
| Relación 1:N con su lookup | `relacion.py` | `muestra_relacion.py` | Maduro para la relación común (11 ciclos); autorreferida y hija estándar **sin ensayar** |
| Clave alternativa | `clave.py` | `muestra_clave.py` | Maduro (10 ciclos) |
| Perfil de seguridad de columna | `perfil_columnas.py` | `muestra_perfil_columnas.py` | Maduro (1 de 3 ciclos) |
| Rol de seguridad | `rol.py` | `muestra_rol.py` | Maduro (4 ciclos) |
| Custom API (con parámetros y salidas) | `custom_api.py` | — | Heredado: sin referencia en la skill todavía |
| Steps de plugin | `step.py` | — | Heredado |
| Paquete de plugins | `paquete_plugins.py` | — | Heredado |
| App model-driven y sitemap | `app.py` | — | Heredado |
| Formulario | `formulario.py` | — | Heredado |
| Vista | `vista.py` | — | Heredado |
| Cloud flow | `flujo.py` | — | Heredado |
| Environment variable | `variable_entorno.py` | — | Heredado |
| Web resource | `web_resource.py` | — | Heredado |
| Filas de datos | `datos.py` | — | Heredado |
| Cambios que solo hace `ImportSolution` (tablas de la app, ribbon) | `importar_solucion.py` | — | Heredado |
| Enums de C# desde los choices | `codigo/generar_enums.py` | — | Heredado |

"Heredado" quiere decir: la skill `power-platform-especificar` todavía no tiene referencia de formato para ese tipo (se agregan cuando un proyecto los construye por primera vez); la única especificación es el docstring de la herramienta. Se migraron la identidad (`idiomas`) y los datos del proyecto anterior, no su contrato.

Otros archivos:

| Archivo | Para qué |
|---|---|
| `construir/_comun.py` | Las piezas que el contrato exige idénticas en todas: validador de forma (`exigir_forma`), espera del bloqueo de personalización, línea JSON de salida, estados, etiquetas por idioma, carpeta de playbooks |
| `dataverse_api.py`, `graph_api.py` | Clientes mínimos del Web API de Dataverse y de Microsoft Graph (correo de prueba). **Clientes hermanos**: leen las credenciales por el mismo módulo y una corrección en uno es un pendiente del otro |
| `credenciales.py`, `proyecto.json` | Dónde están los secretos y la configuración del proyecto (ver abajo) |
| `pacx` | Corre `pac` sin colgarse en una sesión sin escritorio |
| `agentes/` | Las órdenes de los agentes constructores, verificadores y revisores (contrato sin cambios; rutas y nombres del proyecto) |
| `correr_pruebas.py` | Corre todas las pruebas que no tocan el entorno |

## Configuración y credenciales

- **Datos del proyecto** (solución, publisher por unique name, prefijo `sanic`, abreviatura `carp`, prefijo de opciones `15946`, `idiomas`, entorno): salen **solo** del bloque `## 1. Identidad` de cada playbook. Ninguna herramienta los trae escritos. `idiomas` es una lista de LCID (hoy `[1033, 3082]`, mínimo 1033) y todo nombre visible es un objeto con un texto por cada idioma.
- **Configuración versionada, sin secretos**: `herramientas/proyecto.json` es el único archivo. Dice dónde buscar los secretos (`credenciales`), la carpeta de playbooks (`playbooks`) y el destino y namespace de los enums (`codigo`). `construir/excepciones_nombres.json` es el registro de excepciones de nombres aprobadas por el aprobador y **arranca vacío** (este proyecto no tiene excepciones heredadas); está fijado por una prueba.
- **Credenciales**: se leen del archivo que indique la variable de entorno `PP_SECRETS_FILE`; si no está definida, del primero que exista de `~/projects/pp_secrets.env` y `local/pp_secrets.env` (la carpeta `local/` no se versiona). Si la variable apunta a un archivo que no existe es un error: no se cae a otra ruta. Los mensajes nombran rutas y **nombres de clave**, nunca valores; ninguna herramienta imprime el secreto ni el token. Claves que usa Dataverse: `DATAVERSE_URL`, `PP_TENANT_ID`, `PP_CLIENT_ID`, `PP_CLIENT_SECRET`; Graph: `PP_MAIL_TENANT_ID`, `PP_MAIL_CLIENT_ID`, `PP_MAIL_CLIENT_SECRET`, `PP_MAIL_BUZON_n`.
- Comprobado el 2026-10-09 con una sola lectura (`GET WhoAmI` → 200), sin imprimir el token.

## Cómo se prueba

`python3 herramientas/correr_pruebas.py` corre las tres carpetas (`construir/tests`, `codigo/tests`, `tests`), cada una en su proceso, y falla si alguna no ejecuta ninguna prueba. Ninguna toca el entorno ni la red: cliente simulado (`construir/tests/cliente_simulado.py`), archivos temporales y valores inventados. Reglas de `pruebas.md` aplicadas: en tabla (una fila por campo), toda prueba que espera `error` afirma una parte del `detalle`, el simulado lleva configuradas todas las rutas del camino feliz, y se comprobó por **mutación** en una copia fuera del repo (se rompe cada arreglo por separado y alguna prueba tiene que fallar).

Estado al 2026-10-09: **1136 pruebas** (`construir/tests` 1081, `codigo/tests` 38 con 1 omitida a propósito, `tests` 17), todas en verde. La omitida es «el `Choices.g.cs` del repo está al día»: se reactiva sola cuando existan playbooks de choice (si hay playbooks y falta el archivo, falla). Mutación: las del trabajo de tabla/`_comun`/`columnas_estandar`/`muestra_tabla` (79 mutantes) y las de cada módulo migrado se hicieron en copias fuera del repo; el único mutante vivo es la última línea de `muestra_tabla.main()` (el código de salida), que necesita un cliente real.

Dos límites que importan:

1. **El doble repite la suposición del código** (`pruebas.md` 2.2) en todo lo que la skill 0.2.0 marca como pendiente: la forma de la lectura de `statuscode`/`statecode` (`$expand=OptionSet($select=Options)`), la lectura de una fórmula, las respuestas de `InsertStatusValue`/`UpdateOptionValue`, las de `CanCreateAttributes`. En el primer ciclo real hay que validar esas formas contra una respuesta **real** exportada y, si difieren, **primero** se arregla el doble y la suite pasa a rojo.
2. **Las muestras de `construir/tests/muestras/`** (`tabla`, `relacion`, `clave`, `choice-global`, `rol`, `perfil-columnas`) son formas de exportación derivadas de las reales del proyecto anterior, con los nombres de este proyecto y una etiqueta 3082 gemela de cada 1033. **No son exportaciones de este proyecto**: cada carpeta lo dice en su `LEEME.txt`. Se reemplazan por la exportación real en el primer ciclo (`muestra_<tipo>.py --guardar`).

## Lo nuevo de la skill 0.2.0 y cómo se comporta

Regla general: **donde la skill marca un comportamiento como pendiente de verificar, la herramienta devuelve `bloqueado` y lo explica; nunca supone.** Lo no ensayado se declara en `tabla.SIN_ENSAYAR` / `relacion` (lista de características) y solo se corre con `--permitir-no-verificadas`, que **únicamente vale sobre nombres descartables** (`_tbl_zz…` para tablas y relaciones, `_zz…` en todas las columnas para `columnas-estandar`). Un ensayo se hace sobre una tabla descartable de una solución descartable; recién después de comprobar contra la plataforma se mueve la característica a verificadas.

| Novedad | Qué hace la herramienta | Pendiente de verificar (motivo del `bloqueado`) |
|---|---|---|
| Columna `decimal` (`precision`, `minimo`, `maximo`) | Crea con `DecimalAttributeMetadata`; compara exacto como `Decimal`, no como float; exige los tres datos, rango ±100.000.000.000, `minimo` ≤ `maximo` y 15 dígitos como máximo | Que `MinValue`/`MaxValue` vuelvan exactos; precisión 0 (flag aparte `decimal:precision0`: si el diseño pide cero decimales, es un `entero`) |
| Columna `formula` | **No la crea.** Crea el resto de la tabla; para la fórmula solo lee y verifica (`SourceType` 3, bits de `SourceTypeMask`, tipo y precisión del resultado, `IsValidForCreate/Update` falsos, requerida/protegida/auditada como el playbook, nombres por idioma, que existan las bases y que las fechas sean `UserLocal` si usa `Now()`). Si falta devuelve **`bloqueado`** con la instrucción literal del paso manual; después del paso manual, la segunda corrida da `ya_existia` | Cómo guarda `FormulaDefinition` el editor: **la expresión no se compara** y el detalle lo dice (comparar contra el export `Formulas/<tabla>-FormulaDefinitions.yaml`); con qué `IsAuditEnabled` nace; si deja las etiquetas de los demás idiomas |
| `motivos_estado` | `null` o ausente (playbooks 0.1.0) conserva los dos por defecto y solo comprueba que `statuscode` tiene exactamente el 1 (estado 0) y el 2 (estado 1). Con lista: renombra los por defecto (`UpdateOptionValue`) y agrega el resto (`InsertStatusValue`, valor explícito, comparado con `NewOptionValue`) en el orden del playbook, publica una vez y verifica valor, estado y etiqueta de **cada idioma**, y que `statecode` sean exactamente 0 y 1 | Que `UpdateOptionValue` acepte `statuscode` (un 4xx es `bloqueado` con el paso manual de renombre); que `InsertStatusValue` respete un `Value` explícito (otro valor asignado es `bloqueado`); la forma de la lectura; qué motivo toma un alta sin `statuscode` |
| `tipo: columnas-estandar` (por ejemplo `sanic_tiemporespuesta` sobre `email`) | Comprueba que la tabla existe, es estándar y `CanCreateAttributes` es `true` (si no, `bloqueado`), que la tabla esté auditada si una columna pide auditoría (si no, `bloqueado`: hueco del diseño), agrega las columnas con el mismo cuerpo que una tabla propia, publica y verifica con las mismas comprobaciones; «columnas de más» solo cuenta las del publisher | Pertenencia a la solución: se **observa y se informa** (filas de `solutioncomponents` de la tabla y de cada columna), no se da por verificada; si alguien marca `variante:pertenencia` como verificada, exige la tabla una vez con `rootcomponentbehavior` distinto de 0. Las banderas de reparación de `tabla.py` no aplican |
| `relacion.py`: `jerarquica` y relación autorreferida | `jerarquica` solo existe (y vale `false`) cuando padre e hija son la misma tabla; nunca `parental`; lookup no requerido; `IsHierarchical: false` explícito; verifica `IsHierarchical = false` | Que el alta acepte `IsHierarchical: false`; el error con `parental`; si la plataforma impide apuntarse a sí mismo o los ciclos |
| `relacion.py`: tabla hija estándar (REL-19, lookup en `email`) | No la crea: lee `IsCustomEntity` de la hija y devuelve `bloqueado` si no es propia | Pertenencia a la solución, parámetros de `CanBeReferencing`/`CanBeReferenced`, publicar una tabla estándar |

Además, en todas: `idiomas` reemplaza a `lcid` (un playbook viejo es un error, no se lee en silencio); la descripción es prosa y se escribe con el **mismo texto en cada idioma**; se verifica la etiqueta de cada idioma en los dos sentidos (una etiqueta en un idioma no declarado también es una diferencia); y el idioma base del entorno tiene que estar entre los `idiomas` (`bloqueado` si no) y todos tienen que estar provisionados.

## Qué se copió, qué se descartó y qué quedó sin adaptar

**Descartado** (no se copió):

- `parametro.py` y su prueba: versiona filas de la tabla de parámetros de M-PPP (`sanic_mppp_tbl_parametro`, su choice de tipos y su playbook de datos). Este proyecto no tiene esa tabla (su configuración vive en environment variables). No es el parámetro de una Custom API, que lo cubre `custom_api.py`.
- Todo lo de dominio del proyecto anterior en `especificar/`, `pruebas/`, `diagnostico/`, `generar_er.py`, `progreso.py` y `prueba_humo_validacion.py`: generadores de playbooks con las especificaciones de M-PPP dentro, los sets de aceptación contra su entorno, el histórico, los simuladores de su proceso y sus datos de prueba. No se pidieron y no aplican tal cual.

**Pendiente de adaptar** (genérico, pero no pedido; se hace cuando haga falta):

- `especificar/playbook_*.py` (generadores de playbooks desde una especificación compacta, recomendados por `tabla.md` §4 cuando hay varias tablas) y el corredor de aceptación de `pruebas/` (`correr.py`, `semaforo.py`, `marca_agua.py`): son genéricos pero cargan datos de M-PPP. Quien los adapte tiene que seguir las mismas reglas de este README.
- `diagnostico/` (flujos que fallan, entregas de webhook).

**Sin adaptar o con decisión abierta**:

- Las herramientas "heredadas" (tabla de arriba) siguen siendo una cadena por nombre visible en `formulario.py`/`vista.py`/`web_resource.py`/`variable_entorno.py`/`custom_api.py` (no hay formato de skill todavía); las etiquetas **embebidas en el XML** (sitemap, formulario, ribbon) sí van una por idioma de la Identidad.
- `custom_api.py`, `step.py` y `paquete_plugins.py` tienen un helper duplicado que convierte la forma mal tipada de un dato **del playbook** en `ErrorPlaybook` (heredaban `ErrorEntorno`, que sale como «devolvió 200…»). Conviene que `exigir_forma` acepte la clase de excepción y unificarlo en `_comun.py`. `paquete_plugins` solo exige el prefijo `sanic_` y `step` no valida el patrón `STEP - CARP - tabla - mensaje - etapa`.
- `datos.py` devuelve `bloqueado` («etiqueta ambigua») cuando un mismo texto es la etiqueta 1033 de una opción y la 3082 de otra (con dos idiomas la herramienta elegía una en silencio). `rol.py` conserva la clave opcional `sin_privilegios_propios` (lógica de plataforma) que `rol.md` no documenta.
- `formulario.py`: `pestana` y `titulo` de sección pasan a ser un objeto por idioma (las etiquetas embebidas del `formxml` van una por idioma de la Identidad; la de un campo sale del `DisplayName` de la columna, y si falta en un idioma es `bloqueado`). `app.py` e `importar_solucion.py` toman los idiomas de la Identidad (se quitó la clave `idiomas` duplicada del bloque del componente). Los títulos de sitemap cumplen BP-PP-197; los rótulos y tooltips de ribbon no se revisan (son texto de interfaz).
- El estado tras `--corregir-nombres`, `--agregar-columnas` y `--corregir-primaria` sigue siendo `ya_existia` con el detalle de lo que se corrigió (lo que piden la receta 2.6 y las órdenes de `agentes/`); el contrato general dice `actualizado` para «solo se corrigió algo que ya existía». Hay que decidir una de las dos.
- `construir/tests/muestras/*` (ver arriba) y los dobles de la lectura de estados/fórmulas: se validan en el primer ciclo.
- No hay herramienta de **muestra** para decimal, fórmula y motivos: `muestra_tabla.py` los nombra como «SIN COMPARAR» (y no sale `OK`) hasta que una exportación real fije cómo se ven en el XML.
- El repositorio todavía no está inicializado en esta carpeta (`.git`); `herramientas/.gitignore` ignora `__pycache__`.

## Trampas encontradas al adaptar

- `lcid` → `idiomas` no es un renombre: cambia la forma de todo nombre visible y la precondición de idioma (base del entorno ∈ `idiomas`, todos provisionados). Y el diccionario/convenciones exigen que las etiquetas existan en **ambos** idiomas: una herramienta de un solo idioma habría marcado «etiqueta en otro idioma» como diferencia en cada tabla.
- Una diferencia ahora es **una por idioma**: los reconocedores de «solo difieren nombres» (`--corregir-nombres`) tuvieron que deduplicar por columna u opción, o escribían la misma columna dos veces.
- Los decimales: `json.loads` los devolvía como `float` (999999999.99 no es exacto). El bloque ahora se lee con `parse_float=Decimal`, y la lectura de la plataforma se compara convirtiendo con `Decimal(str(valor))`.
- La primaria hereda la auditoría de la tabla: con una tabla auditada, el doble del entorno tiene que devolver la primaria auditada (si no, falla en silencio una prueba que parecía de otra cosa).
- La relación `parental` del diccionario (§6.1) está descrita como «Cascade All» (las siete en `Cascade`); la receta verificada de `construir` (2.3) y la herramienta fijan `Merge` y `RollupView` en `NoCascade`. **No se cambió**: la herramienta sigue la receta verificada y una prueba lo fija; hay que corregir el diccionario o la receta.
- El cliente de Dataverse sigue codificando la ruta (`urllib.parse.quote`); `graph_api.py` también. Cualquier cambio en uno se aplica al otro.
