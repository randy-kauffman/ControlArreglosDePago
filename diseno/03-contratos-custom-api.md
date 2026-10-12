# 03. Contratos de Custom API, plugins y máquinas de estado

Proyecto: 2026-003-control-arreglos-pago · Etapa 3 · Estado: **borrador del arquitecto para revisión del aprobador** (2026-10-09); decisiones abiertas OA-1 a OA-12 resueltas por el usuario el 2026-10-10 (sección 6); supuestos de plataforma V-1 a V-8 verificados en Dev el 2026-10-11 (0.9, `spikes/verificaciones/RESULTADO.md`); cuatro decisiones del usuario y la inconsistencia 16 del arquitecto, del 2026-10-11 (D-39 de `definicion.md`); a partir de V-8, cuatro decisiones más del 2026-10-11 (D-40: formato del código autonumérico del Arreglo en el Create, `statecode` de Mapeo y de Cliente solo de SYSTEM, catálogo que se retira a Histórica, Reabrir sin otros cambios; secciones 2.2, 2.3, 2.5, 2.6, 2.8 y 7)

Fuentes y forma de citar: `def.` = `definicion.md` (con línea `L###` o `ítem N` de §6 Lógica); `D-xx` = decisión de la definición; `dicc.` = `diseno/02-diccionario-datos.md` (con sección, `P-xx`, `R-xx`, `DD-xx`); `matriz` = `diseno/04-matriz-privilegios.md`; `inv.` = `diseno/06-inventario-componentes.md`; `conv.` = `diseno/01-convenciones.md`; `spike N` = `spikes/*/RESULTADO.md` (1 plantilla, 1b sandbox, 2 plataforma, 3 sincronización, 4 manual, roles). `M-PPP` = `/home/gmaker/projects/M-PPP/diseno/03-contratos-custom-api.md`, solo como modelo de forma.

Recetas respetadas: `power-platform-construir/references/dataverse/` `patrones.md` (Custom API, steps, elevación a SYSTEM), `seguridad.md`, `pruebas.md` (sin FakeXrmEasy, BP-PP-159) y `rendimiento.md` (sin N+1, sin `ColumnSet(true)`, sin `ExecuteMultiple` dentro de un plugin).

## Para qué sirve este documento

Un constructor escribe el C# con TDD estricto **sin tomar decisiones de diseño**. Por eso cada pieza trae: el contrato (qué entra, qué sale, qué escribe, con qué identidad), los errores con código y mensaje, las pruebas de aceptación numeradas (`PA-…`, Dado/Cuando/Entonces) y, en la sección 4, las firmas y la estructura de carpetas. El ciclo es el de `herramientas/agentes/orden_constructor_codigo.md`: el arquitecto convierte las pruebas `PA-…` en `src/Sanic.Carp.Plugins.Tests/Aceptacion/` y deja firmas que lanzan `NotImplementedException`; el constructor implementa hasta ponerlas en verde.

Lo que este documento **no** decide: reglas de negocio que las fuentes dejan abiertas. Esas fueron a la sección 6 ("Decisiones abiertas"), con su análisis y un comportamiento provisional marcado en el texto. **El 2026-10-10 el usuario decidió las doce (OA-1 a OA-12)**, aceptando todas las recomendaciones: ya no queda ninguna marca de decisión abierta ni comportamiento provisional, y cada decisión termina con su línea "Decidida el 2026-10-10" al pie de su análisis, que se conserva como justificación. Lo que sí fija este documento, y que las fuentes no traían, está en 0.8 ("Decisiones de diseño de este documento", `DC-nn`): son decisiones técnicas reversibles, cada una con su motivo.

## Mapa de piezas

| Inventario | Pieza | Sección |
|---|---|---|
| 7.2 a 7.7 | Dominio puro: estados, normalización, huella, montos, calendario, consecutivo, tiempos, validación de publicación | 2.0 y 3 |
| 7.8 | Generador Open XML (portado del spike 1) | 4.3 |
| 7.9 | Doble de `IOrganizationService` en memoria y apoyos de prueba | 4.5 |
| 7.10 | Datos (repositorios) | 4.2 |
| 7.11 | Steps de Email y entrada de correo | 2.9 a 2.11 |
| 7.12 | Steps de Conversación | 2.3 y 2.4 |
| 7.13 | Steps de Cliente | 2.2 |
| 7.14 | Steps de Arreglo | 2.5 y 2.6 |
| 7.15 | Steps de Cuota | 2.7 |
| 7.16 | Steps de Versión de plantilla y Mapeo | 2.8 |
| 7.17 | Propietario | 2.1 |
| 7.18 / 8.2 | Custom API `generarformato` | 1.1 |
| 7.19 / 8.3 | Custom API `reconciliarcorreos` | 1.2 |
| 7.20 / 8.4 | Custom API `tomarconversacion` | 1.3 |
| 7.21 / 8.5 | Custom API `unirconversaciones` | 1.4 |
| 7.22 / 8.6 | Custom API `iniciarconversacionnueva` | 1.5 |
| 8.7 | Lista cerrada de steps | 5 |

---

## 0. Reglas comunes

### 0.1 Todas las Custom API

| Propiedad | Valor | Fuente |
|---|---|---|
| Tipo | **Action** (`IsFunction = false`). Todas escriben algo o dependen de la identidad; una Function se puede cachear y su `IsFunction` es inmutable | def. L506; spike 1b §2; `patrones.md` |
| Binding | **Global** (unbound, `BindingType = 0`) | def. L145-150 |
| `AllowedCustomProcessingStepType` | `None` (nadie registra steps sobre ellas) | M-PPP cabecera |
| `IsPrivate` | **`false`**. Con `true` el conector de Dataverse no resuelve la acción y el flujo de salud (inv. 11.2) no se podría activar. No es una medida de seguridad: la seguridad es el privilegio de ejecución | M-PPP cabecera (corrección del 2026-09-22); `seguridad.md` §3 |
| Plugin de respaldo | una clase `IPlugin` por API, en el paquete `sanic_carp_pkg_plugins`, namespace `Sanic.Carp.Plugins.Api` | conv. §5 |
| Nombres | API: `sanic_carp_capi_<nombre>`, display `CAPI - CARP - <Nombre>`. Parámetro de entrada: `uniquename` corto, `name` = `sanic_carp_capiip_<api>_<uniquename>`, display `CAPI_IP - CARP - <Nombre>`. Parámetro de salida: ídem con `capiop`/`CAPI_OP` | conv. §2 |
| Tipos de parámetro | Guid (12), String (10), Integer (7), Boolean (0). Ningún parámetro es `EntityReference`: los ids viajan como Guid, para que el JS y el flujo los armen sin tipos de entidad | decisión DC-24 |
| Parámetro opcional | Una **Action** no recibe la clave en `InputParameters` si el llamador la omite (a diferencia de una Function, M-PPP §2). El código distingue "ausente" (`!Contains`) de "presente y vacío". Aun así, un Guid o un texto vacío se tratan como ausentes | `patrones.md` (Function vs Action) |
| Respuesta | Los parámetros de salida se escriben **todos** siempre que la llamada termina bien (con valor por defecto si no aplican) | M-PPP §1b |
| Errores | `InvalidPluginExecutionException` con mensaje `CARP-<ÁREA>-<NNN>: <texto en español>`; ver 0.5 | DC-25 |

**Privilegio de ejecución** (`ExecutePrivilegeName`; solo puede nombrar un privilegio que ya exista; D-17, def. L402):

| API | `ExecutePrivilegeName` | Quién lo tiene (matriz §2) |
|---|---|---|
| `generarformato` | `prvReadsanic_carp_tbl_generacionformato` | Oficial (L), Purga (L). Jefe y Servicio por el Oficial |
| `reconciliarcorreos` | `prvCreatesanic_carp_tbl_conversacion` | Servicio (L). El Oficial no lo tiene |
| `tomarconversacion`, `unirconversaciones`, `iniciarconversacionnueva` | `prvWritesanic_carp_tbl_conversacion` | Oficial (L); Jefe y Servicio por el Oficial |

La matriz asigna el rol Oficial al team del sistema de la BU, y las cuentas de servicio están en la BU. Por eso Servicio y Purga también ejecutan `generarformato`, `tomar…`, `unir…` e `iniciar…`. **Aceptado el 2026-10-10 (OA-12, Inconsistencias, punto 11):** las API no tienen sentido de negocio para esas cuentas, pero se acepta que puedan ejecutarlas porque la escritura queda atribuida.

### 0.2 Identidad y elevación (quién lee, quién escribe, quién es el actor)

Regla de la definición (def. L228, L241-247, D-26): el sistema hace solo lo que el oficial no puede; la lectura de datos de negocio y los gestos van con la identidad del llamador; cuando se eleva a SYSTEM, **el actor se captura antes de elevar y viaja explícito en el `Target`**, porque bajo SYSTEM `UserId` e `InitiatingUserId` valen SYSTEM en toda la cadena y las `SharedVariables` no llegan (`patrones.md`; spike roles; M-PPP §4).

| Pieza | Lee con | Escribe con | Actor y dónde queda |
|---|---|---|---|
| `generarformato` | llamador (arreglo, cuotas, cliente); SYSTEM (versión, mapeo, archivo) | SYSTEM (congelamiento, instantánea, número provisional, bitácora) | `InitiatingUserId` capturado al inicio → `sanic_generadoporid` |
| `reconciliarcorreos` | SYSTEM | SYSTEM | No hay actor humano. La cuenta de servicio que llama no se registra |
| `tomarconversacion` | llamador | **llamador** (sin elevar) | `InitiatingUserId` → `sanic_oficialasignadoid` |
| `unirconversaciones` | llamador (valida acceso) | SYSTEM | `InitiatingUserId` capturado → `sanic_finalizadoporid` del origen |
| `iniciarconversacionnueva` | llamador (valida acceso) | SYSTEM | No hay columna de actor (ver 1.5) |
| Steps de gestos (2.3 a 2.8) | `IOrganizationService` del llamador | el mismo `Target` (PreOperation) y SYSTEM para efectos laterales | `InitiatingUserId` del contexto, **nunca** un lookup de negocio como proxy (`patrones.md`, D-60 de M-PPP) |
| Steps asíncronos de Email (2.10, 2.11) | SYSTEM | SYSTEM | `sanic_respondidoporid` = `createdby` del Email (DC-12) |

### 0.3 `IIdentidadDeEjecucion`: cómo un step distingue "sistema" de "persona"

Supuesto de arquitectura de la def. (L523, L548: "las escrituras anidadas del sistema se distinguen por la identidad de ejecución", confianza media). Contrato único, usado por todos los steps:

- `ActorId`: `InitiatingUserId` del contexto. **Verificado el 2026-10-11 (V-4):** una escritura que la propia plataforma hace como SYSTEM por cuenta de una persona (por ejemplo, dejar `sanic_archivo` en nulo al reemplazar un archivo) trae `UserId` = SYSTEM pero conserva a la persona en `InitiatingUserId`; por eso el criterio es `InitiatingUserId` y nunca `UserId`.
- `EsSistema`: `ActorId` es el usuario SYSTEM. El id de SYSTEM se obtiene **una vez por ejecución** con `WhoAmIRequest` sobre un servicio creado con `CreateOrganizationService(null)`. No se cachea entre ejecuciones (los plugins son sin estado, `rendimiento.md`).
- `EsEscrituraDelSistema` = `EsSistema`. Se conserva el nombre porque lo usan todos los steps; ya no incluye a ninguna cuenta de servicio.
- ~~`EsCuentaDeServicio`: el `systemuser` de `ActorId` tiene `applicationid` igual al valor de la variable de entorno de Application ID de la cuenta de servicio (10.7).~~ **Retirada el 2026-10-10 (OA-12, opción C).** No se reconoce ninguna cuenta de servicio: solo SYSTEM escribe columnas solo-sistema, porque las Custom API elevan siempre y ningún flujo del diseño las escribe. La cuenta de servicio es un usuario licenciado (RNF-06, D-21) y no tiene Application ID, así que la variable nunca habría coincidido. Cualquier otra identidad, incluida la cuenta de servicio, se trata como persona.
- `EsJefatura()`: `ActorId` figura en `teammembership` del team de jefatura (nombre en `sanic_carp_ev_nombreteamjefatura`, dentro de la BU). Consulta perezosa, una vez por ejecución. Las tablas intersect devuelven `Guid` crudo, no `EntityReference` (`patrones.md` §3).
- **Perezosa**: un step primero revisa, en memoria, si el `Target` toca columnas vigiladas. Si no las toca, no consulta identidad (camino barato).

### 0.4 Configuración del área

Variables de entorno que los plugins leen (def. L418; `seguridad.md` §2). Los nombres *(prov.)* del inventario quedan **fijados** acá:

| Variable | Inventario | Uso |
|---|---|---|
| `sanic_carp_ev_nombrebu` | 10.1 | Nombre de la BU ARP |
| `sanic_carp_ev_nombreteamjefatura` | 10.2 | Nombre del team de jefatura, buscado dentro de la BU |
| `sanic_carp_ev_buzonarea` | 10.8 | Dirección del buzón del área; resuelve la queue remitente |

~~Variable de Application ID de la cuenta de servicio (inventario 10.7).~~ **Retirada el 2026-10-10 (OA-12):** los plugins no leen ninguna variable para reconocer a una cuenta de servicio (0.3). El inventario conserva la fila 10.7 marcada como retirada, sin renumerar.

`IConfiguracionDelArea` (una instancia por ejecución, todo con servicio SYSTEM y memoizado dentro de la ejecución):

1. Valor de cada variable: `environmentvariablevalue.value`, si no `environmentvariabledefinition.defaultvalue`, si no `CARP-CFG-006`.
2. BU: `businessunit` con `name` igual al valor, no deshabilitada. Debe haber exactamente una (`CARP-CFG-001`).
3. Team propietario: `team` con `businessunitid` = la BU e `isdefault = true`. Exactamente uno (`CARP-CFG-002`). **No se busca por nombre** (conv. §2).
4. Team de jefatura: `team` con `name` = valor y `businessunitid` = la BU (`CARP-CFG-003`).
5. Queue remitente: `queue` con `emailaddress` igual (sin distinguir mayúsculas) al buzón, no inactiva. Cero ⇒ `CARP-CFG-004`; más de una ⇒ `CARP-CFG-005` (el spike 3 vio dos queues sobre un buzón: indefine el propietario del Email).

Los mensajes `CARP-CFG-*` nombran la variable o el elemento que falta, nunca un valor.

### 0.5 Errores

- Formato: `CARP-<ÁREA>-<NNN>: <texto>`. El texto está en español con ortografía completa, sin datos sensibles (cédula, cuenta, cuerpo de correo) y sin traza de pila (def. L412; `seguridad.md`).
- `001` a `899`: fallo de negocio o de configuración. **No se reintenta**; se corrige la causa.
- `900` a `999`: transitorio (concurrencia, tiempo). **Quien llama puede reintentar**.
- Un error de la plataforma que el código no previó se relanza como `CARP-<ÁREA>-900` con el mensaje de la plataforma recortado a 300 caracteres; la traza completa va a `ITracingService` (sin datos sensibles).
- Dentro de un plugin una operación de datos que falla **revierte toda la transacción** aunque el código la atrape (`patrones.md` §3). Por eso: se valida en memoria antes de escribir, y el código no "sigue adelante" tras un fallo de escritura. Las excepciones esperables de negocio son resultados, no excepciones (patrón `Resultado<T>` de `patrones.md`).
- Excepción: la clave alterna K-01 en la creación de una conversación (`ErrorCode` de duplicado). Se evita preguntando primero; el reintento del job asíncrono es la red (ver 2.10).

### 0.6 Números, fechas y reloj

- Dinero: `decimal`, dos decimales, nunca `double` (spike 1, M01). Sin redondeos propios: las entradas ya tienen dos decimales y la suma, resta y producto por entero conservan dos.
- Minutos: entero, **truncado hacia abajo**, mínimo 0 (DC-11).
- `fecha DO` (vencimiento, pago, inicio, fin, formalización): el generador recibe la fecha tal cual está guardada, sin conversión de zona (spike 1 §6, última fila; dicc. DD-04).
- El dominio no lee el reloj. `IReloj.AhoraUtc` entra por parámetro; en producción es `DateTime.UtcNow`, en pruebas es fijo.
- Comparaciones de texto: `StringComparison.Ordinal`/`OrdinalIgnoreCase`; formato con `CultureInfo.InvariantCulture` (orden del constructor).

### 0.7 Constantes de choices y de plataforma

Los choices se usan por los `enum` de `Dominio/Choices.g.cs` (generado, no se edita): `ResultadoDeConversacion`, `MotivoDeSinRespuesta`, `ProductoCrediticio`, `TipoDeArreglo`, `Moneda`, `CanalDeOrigen`, `EstadoDeVersionDePlantilla`, `TipoDeMapeo`, `CampoMapeable`. Nunca el literal numérico (conv. §6).

`statecode`/`statuscode` (dicc. §3.2) se modelan en `Dominio/Estados`:

| Tabla | Estado | statecode | statuscode |
|---|---|---|---|
| Conversación | Abierta | 0 | 1 |
| Conversación | Finalizada | 1 | 2 |
| Conversación | Unida | 1 | 159460001 |
| Arreglo | Borrador | 0 | 1 |
| Arreglo | Vigente | 0 | 159460001 |
| Arreglo | Terminado | 1 | 2 |
| Arreglo | Anulado | 1 | 159460002 |
| Arreglo | Descartado | 1 | 159460003 |

Estado por defecto de un `statecode` sin `statuscode` en el `Target` (DC-06): Activo ⇒ 1, Inactivo ⇒ 2. La plataforma rechaza combinaciones inválidas con 400 (hecho verificado, dicc. §3.2). **Verificado el 2026-10-11 (V-5):** un PATCH con solo `statecode` llega al `Target` sin `statuscode` y la plataforma elige el motivo por defecto: en Arreglo `statecode = 0` deja Borrador (1), no Vigente; en Conversación `statecode = 0` deja Abierta (1) y `statecode = 1` deja Finalizada (2). Todo cambio de estado que escribe el código de este diseño (unir, paso a Terminado) lleva las dos columnas.

Email (tabla estándar): `directioncode` falso = entrante, verdadero = saliente; `statuscode` Received = 4, Sent = 3. **Confirmado el 2026-10-11 (V-3)** con los metadatos de Dev y la lectura de los 548 correos que ya había (`spikes/verificaciones/RESULTADO.md`): ambos están en `statecode` 1 (Completed), es decir, un correo entrante ya llega cerrado. Los fixtures de 4.5 siguen siendo la prueba de los dobles.

### 0.8 Decisiones de diseño de este documento (`DC-nn`)

No estaban en las fuentes. Son técnicas, reversibles y se pueden vetar sin tocar reglas de negocio. Las que rozan una regla de negocio se repiten en la sección 6 con opciones.

| ID | Decisión | Motivo |
|---|---|---|
| DC-01 | Una columna solo-sistema "cambió" si está en el `Target` **y** su valor difiere del de la pre-imagen. Estar presente con el mismo valor no es un cambio. Para las columnas con seguridad por columna, la pre-imagen puede venir sin ellas: ver DC-28 (2026-10-11) | Un cliente que reenvía la fila completa no debe fallar sin motivo. D-30 dice "cambio" |
| DC-02 | "Sistema" = SYSTEM (`WhoAmI` elevado). No se reconoce ninguna cuenta de servicio (0.3). ~~O la cuenta de servicio por Application ID~~ (retirado el 2026-10-10, OA-12) | Supuesto de def. L523. Las Custom API elevan siempre y ningún flujo del diseño escribe columnas solo-sistema; la cuenta de servicio es un usuario licenciado sin Application ID (RNF-06, D-21) |
| DC-03 | Si `EsEscrituraDelSistema`, el step deja pasar el `Target` sin revalidar reglas de gesto. Solo aplica normalización y el atajo de propietario | Riesgo residual aceptado en M-PPP §4: otro plugin SYSTEM del entorno. Mantiene baratas las 60 altas de cuotas |
| DC-04 | En Arreglo, un **gesto** es una transición de estado o una columna disparadora, y no admite otros cambios en el mismo `Update` | Evita mezclar "formalizar" con editar datos. Hace cada gesto verificable |
| DC-05 | **Desbloquear** = `Update` con `sanic_fechacongelamiento = null`. **Reabrir un Vigente congelado** = `Update` con `sanic_motivoreapertura` | El diccionario no da columnas de gesto para estos dos (inv. 12.24: "el mecanismo no está definido") |
| DC-06 | Estado efectivo = el del `Target`, y si falta `statuscode`, el por defecto del `statecode`. **Verificado el 2026-10-11 (V-5):** con solo `statecode = 0` el Arreglo queda en Borrador, no en Vigente; por eso una reapertura (Terminado → Vigente) solo es válida si el `Target` trae `statecode` **y** `statuscode`, y un `statecode = 0` solo sobre un Terminado es un Terminado → Borrador, que se rechaza | Los botones nativos de desactivar y activar mandan solo `statecode` |
| DC-07 | Remitente: se **reemplaza** por la queue si falta o es el usuario que crea; se **rechaza** cualquier otro | def. L270 pide "fijar" y "rechazar cualquier otro", pero el remitente por defecto del formulario es el usuario (spike 4) |
| DC-08 | Al registrar un correo entrante sin Regarding se busca primero por `In-Reply-To` el correo al que responde, y se usa su conversación | Plan B de def. L430 aplicado como segunda red; la correlación puede llegar en un `Update` posterior (spike 3) |
| DC-09 | Un correo correlacionado a una conversación **Unida** se redirige a la conversación de `sanic_unidaaid` | Si no, queda invisible y rompe el 100 % (criterio 1, RF-01). Las fuentes no lo cubren |
| DC-10 | Finalizar vacía `sanic_sinrespuestadesde`; el correo nuevo en finalizada la completa con su fecha (P-09); Reabrir **no** la vacía; Iniciar conversación nueva la vacía en la anterior | D-06: spam y automáticos no deben inflar pendientes. Ver Inconsistencias, punto 3 |
| DC-11 | Tiempo de respuesta = minutos truncados entre `senton` y `sanic_sinrespuestadesde`, mínimo 0. "Primera respuesta" = la primera respuesta con tiempo. Si el correo pendiente llegó **después** del envío, no se vacía nada | Diccionario: "primera respuesta enviada"; evita borrar un pendiente real |
| DC-12 | `sanic_respondidoporid` = `createdby` del Email saliente | Quien envía es quien creó el borrador: el rol solo da Write Basic sobre Email (matriz §3) |
| DC-13 | Huella: Message-ID sin espacios, sin `<` `>` de los extremos y en minúscula; SHA-256 de UTF-8 en hex minúscula. Sin Message-ID: `sin-messageid:` + id del Email | def. L168; spike 3 (dominio en mayúsculas en salientes). El respaldo da idempotencia a un correo raro |
| DC-14 | Vincular al cliente solo si **exactamente un** `Cliente` activo tiene ese correo | Dos clientes con el mismo correo no se desempatan en el servidor |
| DC-15 | `tomarconversacion` no recibe versión de fila del cliente: lee, valida y escribe con `IfRowVersionMatches` | La garantía de D-04 es la misma; menos parámetros |
| DC-16 | Unir: el destino se recalcula desde sus correos; se rechazan cadenas; tope de 1.000 correos | def. ítem 5 dice "recalcula" sin fórmula; M-PPP usa topes explícitos |
| DC-17 | Iniciar conversación nueva: mueve solo correos **entrantes** posteriores a la finalización; copia el cliente; no copia el oficial | def. ítem 6 dice "llegados después" |
| DC-18 | La vista previa no escribe nada (ni bitácora ni congelamiento). La generación normal escribe la bitácora siempre | def. ítem 14: "la vista previa… no congela" |
| DC-19 | Una versión nace siempre en Borrador (también por API) y se publica por el gesto normal. Al publicar se hace además una **generación de prueba** con datos sintéticos | Atrapa hoja inexistente (spike 1, hallazgo 1). Habilita la promoción Test→Prod (6, OA-4) |
| DC-20 | Las cuotas se crean con `Create` individual (no `ExecuteMultiple`) y con propietario explícito; el step de propietario deja pasar una escritura del sistema que ya trae team | `rendimiento.md`; evita 120 consultas por calendario |
| DC-21 | "Pago registrado" = `sanic_fechapago` y `sanic_montopagado` juntos: ambos o ninguno, monto mayor que cero y sin tope superior. Lo exige `ReglasDeCuota` con `CARP-CUO-003` (decidido el 2026-10-10, OA-8). ~~Antes: `sanic_fechapago` no nulo, sin más validaciones~~. "Cuota sin pago" para el paso a Terminado y para la vista de riesgo sigue siendo `fechapago` nulo | D-11 usa `fechapago` nulo (spike 2); con el par obligatorio, `fechapago` nulo equivale a no tener pago |
| DC-22 | La unicidad de la cédula se valida también al cambiarla, y la consulta corre con SYSTEM | Sin SYSTEM, un usuario sin perfil de columna ve `null` y la duplicada pasa en silencio (spike 4, tarea A) |
| DC-23 | Nombre de step: `STEP - CARP - <tabla> - <mensaje> - <etapa> - <clase>` | conv. §2 no distingue dos steps del mismo mensaje y etapa; el playbook rechaza nombres repetidos |
| DC-24 | Parámetros de Custom API siempre Guid, nunca `EntityReference` | JS y flujos más simples |
| DC-25 | Códigos `CARP-<ÁREA>-<NNN>`; 900+ reintentable | M-PPP §3 (códigos para que quien llama decida) |
| DC-26 | Las listas de columnas por categoría (libre, de gesto, solo-sistema, bloqueada por congelamiento) viven en `Dominio/Columnas` y una prueba las contrasta con el diccionario | Defecto D-63 de M-PPP (`patrones.md` §3) |
| DC-27 | Un step por (clase, mensaje, tabla); sin post-imágenes; filtro de atributos solo en `Update`; asíncrono solo en PostOperation | Restricciones de `herramientas/construir/step.py` |
| DC-28 | **Columnas con seguridad por columna en la detección de cambios** (decisión del usuario, 2026-10-11, "leerlas como SYSTEM"; V-6). Si una columna protegida (`sanic_cuentadebitar` e `sanic_instantaneacedula` en Arreglo; `sanic_cedula` en Cliente) está en el `Target` pero **falta** en la pre-imagen, el step la lee con el servicio de SYSTEM (una consulta por id, solo esas columnas) y compara con ese valor (DC-01). Si la pre-imagen la trae (llamador con el perfil), se compara tal cual y no hay consulta extra. Una columna ausente nunca se compara contra "nulo" | La pre-imagen se filtra con la seguridad por columna del usuario con el que corre el step: un llamador sin el perfil la recibe **sin** esas columnas (no en nulo; V-6), y reenviar el mismo valor se vería como un cambio que no existe. Alternativa descartada: registrar el step con `impersonatinguserid` de un usuario con el perfil (no se probó con una cuenta de servicio que solo tenga el perfil) |

### 0.9 Supuestos de plataforma que se verifican en Dev antes de dar la pieza por buena

No se deducen de las fuentes. Cada uno tiene su prueba de humo en Dev (no en las unitarias). Si alguno falla, se escala; no se adapta el código.

**Verificados en Dev el 2026-10-11** con un plugin de prueba fuera de la solución, ya retirado (evidencia y método en `spikes/verificaciones/RESULTADO.md`): V-1, V-2, V-3, V-5 y V-7 confirmados; V-4 refutado, con un sustituto probado; V-6 parcial. **V-8 se agregó y se verificó el mismo día, con la misma infraestructura: REFUTADO** (la plataforma completa el autonumérico del Arreglo antes de PreValidation). La columna "Veredicto" resume qué cambió en este documento; lo que quedó sin probar está en 8.1 (H-12 y H-13) y en las notas de cada veredicto.

| # | Supuesto | Qué rompe si es falso | Veredicto (2026-10-11, `spikes/verificaciones/RESULTADO.md`) |
|---|---|---|---|
| V-1 | Un `Update` con `statecode`/`statuscode` dispara los steps de `Update` con esos filtros, venga del formulario, de `Xrm.WebApi` o de `SetStateRequest` | Los gestos de estado (2.3, 2.5). ~~Plan: registrar además un step `SetState` por tabla~~ | **Confirmado.** Un PATCH (= `Xrm.WebApi` = formulario) dispara los steps de `Update` pre y post a profundidad 1. ~~Plan del step `SetState`~~: **eliminado**, un step de `SetState` nunca se dispara (0 trazas de `SetState` frente a 42 de `SetStateDynamicEntity` y 67 de `Update`) y el Web API no tiene la acción. Un `SetStateRequest` llega a los plugins como `SetStateDynamicEntity` y cae en un `Update` anidado, a **profundidad 2**, con `statecode` y `statuscode` en el `Target`: los steps de `Update` no asumen profundidad 1 (reglas comunes de 2). Sin probar: formulario real (se tomó el PATCH como equivalente) y `SetStateRequest` desde un cliente SDK externo |
| V-2 | El código (SYSTEM) puede hacer `Update` sobre una fila **inactiva** de Conversación y Arreglo (columnas que no son el estado) | RF-15: marcar el indicador sobre una Finalizada; limpiar el indicador; repuntar arreglos terminados al unir | **Confirmado, y más.** La plataforma **no bloquea** las escrituras en filas inactivas, ni para SYSTEM ni para una persona: un Oficial sin privilegios de administrador también escribe sobre una Finalizada o un Terminado, y puede reabrirlos con un PATCH. Toda regla de "solo lectura" de una fila cerrada la impone un plugin. Efectos: el doble queda con `RechazarEscriturasEnInactivas = false` (4.5); el repunte de arreglos al unir alcanza a **todos**, también a los inactivos (1.4, inconsistencia 15); las reglas de 2.3, 2.6, 2.7 y 2.8 son la única barrera sobre una fila inactiva o congelada |
| V-3 | Valores de `email.statuscode`: Received = 4, Sent = 3; `directioncode` falso = entrante | Entrada de correo (2.10) y respuesta enviada (2.11) | **Confirmado.** Metadatos de Dev: 4 Received, 3 Sent; `directioncode` falso = Incoming. Ambos llegan con `statecode` 1 (Completed). Sin cambios en el diseño |
| V-4 | ~~Existe `sdkmessagefilter` para `CommitFileBlocksUpload` sobre `sanic_carp_tbl_versionplantilla`~~ | El step S-27 (inmutabilidad por subida de archivo). La huella de cada generación es la barrera de respaldo (D-15, def. L536) | **REFUTADO, con sustituto probado.** `CommitFileBlocksUpload`, `InitializeFileBlocksUpload` y `UploadBlock` no admiten steps personalizados (400; su único filtro es de entidad `none` y `iscustomprocessingstepallowed = false`). Pero la subida (por bloques o con un PATCH a la columna) provoca un `Update` de la Versión con `Target` que trae `sanic_archivo`, mensaje padre `CommitFileBlocksUpload`. **S-27 se rediseñó** como step de `Update` con filtro `sanic_archivo` (2.8, 5). Sin probar: lanzar `InvalidPluginExecutionException` desde ese step y la subida por un usuario no administrador (8.1, H-12) |
| V-5 | El `statecode` Activo→Inactivo y la reapertura Terminado→Vigente son aceptados por la plataforma como un único `Update` con ambas columnas | Reabrir un Terminado (2.5) | **Confirmado**, con una condición: la reapertura **debe llevar las dos columnas**. Un PATCH con solo `statecode = 0` sobre un Terminado lo deja en Borrador (y en Conversación, solo `statecode` deja el motivo por defecto). DC-06, 2.6 y 3.2 lo reflejan; un Terminado → Borrador se rechaza |
| V-6 | Una pre-imagen incluye columnas con seguridad por columna (cuenta, cédula) con su valor real para el usuario del perfil | Detección de cambios en Arreglo (DC-01) | **Parcial.** Con el perfil (administrador o usuario con el perfil asociado directo), sí: valor real, y si el `Target` la trae, la pre-imagen guarda el anterior. **Sin el perfil**, las columnas protegidas **no están** en la pre-imagen (ausentes, no nulas). Decisión del usuario del 2026-10-11: el step las lee con SYSTEM antes de comparar (DC-28). Sin probar: asociar el perfil **por team** (solo se probó directo al usuario; 8.1, H-13), la pre-imagen en PostOperation para un llamador sin perfil, y `impersonatinguserid` con una cuenta que solo tenga el perfil |
| V-7 | `RetrievePrincipalAccess` responde `WriteAccess` para el llamador sobre una versión (def. L342) | Vista previa (1.1) | **Confirmado.** Administrador y Jefe (por el team Jefatura): `WriteAccess`, y un PATCH real da 204; Oficial solo: sin `WriteAccess`, y un PATCH real da 403. Un no administrador puede preguntar por sí mismo. Sin cambios en el diseño |
| V-8 | ~~La plataforma completa el autonumérico `sanic_nombre` del Arreglo **después** del PreOperation de Create, así que el `Target` solo trae `sanic_nombre` si lo envió el cliente (2.5, `ArregloCrearStep`, paso 2)~~ | El alta del Arreglo (2.5, paso 2): con `sanic_nombre` en la lista de solo-sistema, si la clave siempre está en el `Target` se rechazan todas las altas (el diseño ya decía "hay que escalarlo") | **REFUTADO (2026-10-11, `spikes/verificaciones/RESULTADO.md`, sección V-8).** El `Target` de un `Create` ya trae `sanic_nombre = ARR-00nnnn` en **PreValidation, PreOperation y PostOperation** aunque la persona no lo envíe (`null` y `''` equivalen a no enviarlo); el `Target` tiene la misma forma con y sin valor del llamador, y no hay marca de quién lo puso. Si la persona lo envía (`ZZV-NOMBRE`, también `ARR-777777`, también siendo no administrador) **la fila guarda el valor de la persona** y el autonumérico no se consume; la plataforma no valida el formato. El paso 2 tal como estaba **rechazaba todas las altas**. **Consecuencia (decisión del usuario, 2026-10-11, D-40):** en el `Create`, `ArregloCrearStep` ya no trata `sanic_nombre` como una columna solo-sistema por presencia, sino que **rechaza un valor que no cumpla el formato del autonumérico** `^ARR-[0-9]{6,16}$` con `CARP-ARR-009` (2.5, paso 2). **Riesgo conocido aceptado:** un `ARR-nnnnnn` falsificado por API pasa (en el `Create` es indistinguible del de la plataforma, B0 y N5 del informe); el formulario lo muestra de solo lectura y el número de negocio es `sanic_numeroarreglo`, protegido por K-02. En el `Update` sí se distingue (la plataforma nunca agrega `sanic_nombre` a un `Update`, caso A6): cualquier valor distinto del de la pre-imagen se rechaza (2.6, paso 0). Hallazgo lateral: un Oficial **sin** el perfil `CSP - CARP - Datos sensibles` recibe un 403 de la plataforma al crear un Arreglo que envía `sanic_cuentadebitar` o `sanic_instantaneacedula` (casos N1 y N2; ocurre después de PreOperation); es coherente con el perfil por columna y no requiere regla de plugin. Sin probar: qué pasa si la secuencia pasa de 999999 (por eso el patrón admite más dígitos, ver 2.5), un `Create` desde un plugin o un cliente SDK, y desde un formulario real |

---

## 1. Custom API

Cinco, las que nombran def. L145-150 e inv. 8.2 a 8.6. Los nombres *(prov.)* del inventario quedan fijados tal como están. Convenciones comunes en 0.1.

### 1.1 `sanic_carp_capi_generarformato` (inv. 8.2, 7.18)

Genera el F-NIC-0000052 en Excel para un arreglo, lo devuelve en Base64 y, si el arreglo no estaba congelado, lo congela. Quién la llama: el comando "Generar formato para firma" del web resource (def. L151, D-17) y la **vista previa** del jefe. Fuente: def. ítem 14 (L326-337), ítem 10 (L305-312), ítem 15 (L342), D-13, D-15, D-16, D-25, spikes 1 y 1b.

**Registro**: Action, global (unbound), `IsPrivate = false`. **Privilegio de ejecución**: `prvReadsanic_carp_tbl_generacionformato`. **Identidad**: lee con el llamador y escribe con SYSTEM, con el actor capturado (0.2).

| Parámetro | Dir. | Tipo | Req | Nota |
|---|---|---|---|---|
| `arregloid` | entrada | Guid | sí | Arreglo a imprimir |
| `versionpreviaid` | entrada | Guid | no | Si viene, es **vista previa** con esa versión (debe estar en Borrador). No congela y no escribe nada (DC-18) |
| `archivo` | salida | String | | El `.xlsx` en Base64 (43,7 mil caracteres con 60 cuotas; spike 1b) |
| `nombrearchivo` | salida | String | | `F-NIC-0000052-<primaria del arreglo>.xlsx`, por ejemplo `F-NIC-0000052-ARR-000123.xlsx`. No lleva datos del cliente |
| `numeroprovisional` | salida | Integer | | Número impreso para un Borrador; `0` si no aplica (Vigente, Terminado, etc.) |
| `congelado` | salida | Boolean | | `true` si, al terminar, el arreglo está congelado (por esta llamada o antes). En vista previa es el estado que ya tenía |
| `advertencias` | salida | String | | Avisos del generador (por ejemplo, fecha escrita en una celda con formato de moneda; spike 1, hallazgo 4), uno por línea. Vacío si no hay |

**Datos del generador** (clave de `CampoMapeable` ⇒ clave del generador ⇒ origen). El generador exige **todas** las claves simples, aunque el valor sea `null` (spike 1, hallazgo 3):

| Opción de `CampoMapeable` | Clave | Origen |
|---|---|---|
| FechaDeFormalizacion | `fechaFormalizacion` | `sanic_fechaformalizacion` (DateTime tal cual) |
| OficialDeArreglo | `oficial` | `fullname` del usuario `sanic_oficialid` |
| NombreDelCliente / CedulaDelCliente / CorreoDelCliente / CelularDelCliente | `clienteNombre` / `clienteCedula` / `clienteCorreo` / `clienteCelular` | Si el arreglo está **congelado**: `sanic_instantaneanombre` / `…cedula` / `…correo` / `…celular`. Si no: `sanic_nombrecompleto`, `sanic_cedula`, `sanic_correo`, `sanic_celular` del Cliente (def. ítem 14) |
| NumeroDePrestamoOTarjeta | `numeroProducto` | `sanic_numeroproducto` |
| TipoDeArreglo | `tipo` | texto formal con ortografía, decidido el 2026-10-10 (OA-7): "Temporal" / "Cancelación" |
| NumeroDeArreglo | `numeroArreglo` | Borrador: el número provisional (el guardado, o el calculado si aún no hay). Otros estados: `sanic_numeroarreglo` (def. ítem 14: "en un borrador, imprime el número provisional") |
| SaldoInicial, MontoAbonado, SaldoRemanente, CuotaMensual, TotalAPagar | `saldoInicial`, `montoAbonado`, `saldoRemanente`, `cuota`, `totalPagar` | columnas del arreglo (decimal) |
| PlazoEnCuotas | `plazo` | `sanic_plazo` (int) |
| FechaDeInicio, FechaDeFinalizacion | `fechaInicio`, `fechaFin` | columnas del arreglo (DateTime sin conversión) |
| CuentaADebitar | `cuentaDebitar` | `sanic_cuentadebitar` |
| Comentarios | `comentarios` | `sanic_comentarios` (`null` si vacío) |
| Moneda | `moneda` | texto formal con ortografía, decidido el 2026-10-10 (OA-7): "Córdobas" / "Dólares". Mientras la v07 no tenga una celda de moneda mapeada, la clave viaja en los datos y no se imprime. **El generador del spike 1 todavía no tiene esta clave**: se agrega (dicc. §3.1, opción 27) |
| Cuota: numero, fecha, monto, fecha de pago, monto pagado | `cuota.*` | una `CuotaDato` por cuota, orden por número |
| Total: montos de cuota, montos pagados | `total.*` | los calcula el generador |

La tabla "opción ⇒ clave" vive en `Formato/ClavesDelGenerador`, y ahí también la tabla de textos formales de Tipo y Moneda (OA-7), aparte de las etiquetas sin tildes del choice (BP-PP-197). Una prueba verifica que cubre las 27 opciones del enum `CampoMapeable` (si se agrega una opción y falta acá, la prueba falla). `TipoDeMapeo` ⇒ `TipoMapeo` del generador: ValorSimple ⇒ Simple, TotalCalculado ⇒ Total, ColumnaDeBloqueDeCuotas ⇒ Bloque.

**Comportamiento paso a paso**

1. Leer parámetros. `arregloid` ausente o vacío ⇒ `CARP-GEN-001`. Capturar `actor = InitiatingUserId` y `ahora`.
2. Crear dos servicios: `llamador` (`CreateOrganizationService(context.UserId)`) y `sistema` (`CreateOrganizationService(null)`).
3. Leer el arreglo con `llamador` (columnas nombradas, sin `ColumnSet(true)`). No existe **o** no se puede leer ⇒ `CARP-GEN-002`, el mismo mensaje en los dos casos (no revelar existencia).
4. Leer las cuotas del arreglo con `llamador`, orden `sanic_numero`. Deben ser exactamente `sanic_plazo`, numeradas 1..plazo ⇒ si no, `CARP-GEN-005` (un arreglo sin calendario no puede existir, def. ítem 8).
5. Datos del cliente: instantánea si `sanic_fechacongelamiento` no es nulo; si no, leer el Cliente con `llamador` (necesita el perfil de columna para la cédula; un usuario sin perfil vería `null`, spike 4).
6. Versión: con `versionpreviaid`, esa (debe estar en Borrador, `CARP-GEN-006`; el llamador debe tener escritura sobre la versión, comprobada con `RetrievePrincipalAccess` ⇒ `WriteAccess`, `CARP-GEN-007`; def. L342). Sin él, la `sanic_versionplantillaid` del arreglo (cualquier estado: una Histórica es válida para un arreglo antiguo).
7. Con `sistema`: leer la versión (`sanic_estado`, `sanic_huella`, `sanic_capacidad`) y su archivo con `InitializeFileBlocksDownloadRequest` + `DownloadBlockRequest` en bloques de 4 MB (spike 1b). Sin archivo ⇒ `CARP-GEN-003`. Leer las filas de mapeo activas de la versión.
8. `plazo` mayor que `sanic_capacidad` ⇒ `CARP-GEN-004`.
9. Armar `DatosArreglo` y la lista `MapeoCelda`; llamar a `IGeneradorDeFormato.Generar(plantilla, huellaEsperada, mapeo, datos)`. En vista previa la versión es Borrador y puede no tener huella: se pasa la huella calculada del propio archivo (sin verificar). Traducción de excepciones del generador (spike 1 §5, hallazgo 6): `HuellaNoCoincideException` ⇒ `CARP-GEN-008`; `PlantillaInvalidaException` ⇒ `CARP-GEN-009`; `MapeoInvalidoException` ⇒ `CARP-GEN-010`; `DatosInvalidosException` ⇒ `CARP-GEN-011`. Se traza `InnerException` (tipo y mensaje, sin datos) antes de convertir.
10. Si **no** es vista previa, y el arreglo está activo, escribir con `sistema`, en este orden:
    a. Si no está congelado: `Update` del arreglo con `sanic_fechacongelamiento = ahora`, las cuatro `sanic_instantanea*` copiadas del Cliente y, **solo si es Borrador**, `sanic_numeroprovisional` = `max+1` (2.0.5).
    b. `Create` de `sanic_carp_tbl_generacionformato`: `sanic_arregloid`, `sanic_versionplantillaid`, `sanic_generadoporid = actor` (explícito, 0.2), `sanic_fechageneracion = ahora`, `ownerid` = team del área.
    Arreglo inactivo (Terminado, Anulado, Descartado): no se congela ni se toca el arreglo; solo se escribe la bitácora (decidido el 2026-10-10, OA-9).
11. Escribir las salidas.

**Todo en una transacción** (la API corre dentro de la transacción de la plataforma): si algo falla, no queda congelamiento ni bitácora (def. ítem 14: "si falla, error sin archivo parcial y sin congelar"). El archivo se genera **antes** de escribir.

**Errores**

| Código | Mensaje | Reintentar |
|---|---|---|
| `CARP-GEN-001` | Falta el arreglo que se quiere imprimir. | no |
| `CARP-GEN-002` | No se encontró el arreglo o no tiene acceso a él. | no |
| `CARP-GEN-003` | La versión de plantilla no tiene archivo cargado. | no |
| `CARP-GEN-004` | El plazo del arreglo ({plazo}) supera la capacidad de la plantilla ({capacidad} cuotas). | no |
| `CARP-GEN-005` | El calendario del arreglo está incompleto: hay {n} cuotas y el plazo es {plazo}. | no |
| `CARP-GEN-006` | La vista previa solo admite una versión en estado Borrador. | no |
| `CARP-GEN-007` | No tiene permiso para previsualizar versiones de plantilla. | no |
| `CARP-GEN-008` | La plantilla no coincide con la huella guardada al publicar la versión. Fue alterada o no es la versión publicada. | no |
| `CARP-GEN-009` | El archivo de la plantilla no es un libro de Excel válido. | no |
| `CARP-GEN-010` | El mapeo de la versión es inválido: {detalle}. | no |
| `CARP-GEN-011` | Los datos del arreglo no sirven para esta plantilla: {detalle}. | no |
| `CARP-GEN-900` | No se pudo generar el formato. No se guardó ningún cambio. Intente de nuevo. | sí |

`{detalle}` lista los errores del generador (hoja, celda y campo); no incluye valores de datos del cliente. Los errores de forma de la llamada (parámetro de tipo equivocado) los devuelve la capa OData antes del plugin con texto técnico (spike 1b §6): el web resource no lo muestra tal cual.

**Concurrencia.** Dos generaciones simultáneas del mismo borrador no congelado calculan el mismo provisional y escriben los mismos valores; el resultado final es el mismo. Dos formalizaciones concurrentes las resuelve la clave K-02 (2.6).

**Rendimiento.** 72 ms en caliente, 0,5 a 1 s en frío (spike 1b). Tras publicar una versión del paquete, las 2 o 3 primeras llamadas tardan ~1,5 s: la pantalla muestra "generando…". Las consultas son un número fijo: arreglo, cuotas, cliente, usuario, versión, mapeo, bloques de archivo.

**Pruebas de aceptación** (`Aceptacion/GenerarFormatoAceptacion.cs`; generador sustituido por `GeneradorFalso`, salvo donde dice "real")

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-GEN-01 | Borrador sin congelar, plazo 6, sin otros arreglos del crédito; cliente con nombre, cédula, correo y celular | Un Oficial llama con `arregloid` | `archivo` no vacío, `congelado = true`, `numeroprovisional = 1`; el arreglo queda con `fechacongelamiento = ahora`, las cuatro instantáneas iguales al Cliente y `numeroprovisional = 1`; hay una fila de Generación con `arregloid`, `versionplantillaid`, `generadoporid = Oficial`, `fechageneracion = ahora` y `ownerid` = team; las escrituras salieron del servicio de SYSTEM y las lecturas del servicio del llamador |
| PA-GEN-02 | Borrador del crédito X; ya existen del mismo crédito un arreglo con número 1, otro con 2 (Terminado) y otro con 3 (Anulado), más un Borrador y un Descartado sin número | Se genera el formato | `numeroprovisional = 4` (los sin número no cuentan; los inactivos con número sí) |
| PA-GEN-03 | Borrador ya congelado (provisional 2), y el Cliente cambió de nombre después | Se genera de nuevo | El arreglo no se escribe; el generador recibió el nombre de la instantánea, no el nuevo; se crea una fila de Generación más; `numeroprovisional = 2` |
| PA-GEN-04 | Vigente congelado con `numeroarreglo = 3` | Se genera | No se escribe el arreglo; `numeroArreglo = 3` en los datos; `numeroprovisional = 0`; hay bitácora |
| PA-GEN-05 | Vigente reabierto (sin congelar, instantánea vacía) | Se genera | Se congela con la instantánea actual del Cliente; `sanic_numeroprovisional` queda vacío |
| PA-GEN-06 | Parámetro `arregloid` ausente | Se llama | `CARP-GEN-001`; ninguna escritura |
| PA-GEN-07 | `arregloid` de un arreglo que no existe | Se llama | `CARP-GEN-002` |
| PA-GEN-08 | El arreglo existe pero el doble niega la lectura al llamador | Se llama | `CARP-GEN-002` con el mismo texto que PA-GEN-07 |
| PA-GEN-09 | Plazo 6 y solo 5 cuotas | Se llama | `CARP-GEN-005`; sin escrituras |
| PA-GEN-10 | Cuotas numeradas 1,2,3,3,5,6 | Se llama | `CARP-GEN-005` |
| PA-GEN-11 | Plazo 12 y capacidad 6 | Se llama | `CARP-GEN-004` con 12 y 6 en el mensaje |
| PA-GEN-12 | La versión no tiene archivo | Se llama | `CARP-GEN-003` |
| PA-GEN-13 | El generador falso lanza `HuellaNoCoincideException` | Se llama | `CARP-GEN-008`; el arreglo no se congela y no hay bitácora |
| PA-GEN-14 | El generador falso lanza `PlantillaInvalidaException`, `MapeoInvalidoException` y `DatosInvalidosException` (tres casos) | Se llama | `CARP-GEN-009`, `-010` y `-011`; sin escrituras |
| PA-GEN-15 | Vista previa: `versionpreviaid` de una versión Borrador, llamador con `WriteAccess` | Se llama | Archivo generado con esa versión; **ninguna** escritura (ni arreglo ni Generación); `congelado` refleja el estado previo |
| PA-GEN-16 | `versionpreviaid` de una versión Vigente | Se llama | `CARP-GEN-006` |
| PA-GEN-17 | Vista previa y el llamador no tiene `WriteAccess` sobre la versión | Se llama | `CARP-GEN-007` |
| PA-GEN-18 | Vista previa de un Borrador sin congelar | Se llama | `numeroprovisional` = lo que se imprimió (`max+1`), sin guardarse |
| PA-GEN-19 | Un arreglo en dólares de tipo Cancelación y otro en córdobas de tipo Temporal | Se llama (dos casos) | `moneda` y `tipo` reciben los textos formales con ortografía (OA-7): "Dólares" y "Cancelación"; "Córdobas" y "Temporal" |
| PA-GEN-20 | Cliente sin correo ni celular; sin comentarios | Se llama | `Valores` contiene las claves `clienteCorreo`, `clienteCelular` y `comentarios` con valor `null` (no ausentes) |
| PA-GEN-21 | `fechainicio` guardada 2026-11-05 | Se llama | El generador recibe `DateTime` 2026-11-05 sin cambio de zona |
| PA-GEN-22 | Arreglo Terminado | Se llama | Se genera sin congelar ni tocar el arreglo y se escribe la bitácora (OA-9) |
| PA-GEN-23 | Una escritura falla a mitad (fallo inyectado al crear la Generación) | Se llama | `CARP-GEN-900`; al restaurar la transacción del doble, el arreglo no está congelado |
| PA-GEN-24 | El generador devuelve dos advertencias | Se llama | `advertencias` las trae separadas por salto de línea |
| PA-GEN-25 | Arreglo con cédula en el Cliente | Se llama | Ninguna línea de traza contiene la cédula, la cuenta ni el nombre |
| PA-GEN-26 | Test de cobertura | — | `ClavesDelGenerador` tiene una clave para cada una de las 27 opciones de `CampoMapeable` y el generador real acepta `moneda` |
| PA-GEN-27 (real) | Plantilla v07 (fixture) con 60 cuotas y el mapeo del diccionario más la celda de moneda | Se genera con el generador real | Salida idéntica byte a byte a la del spike 1b para 6, 30 y 60 cuotas (las 205 pruebas del spike se portan, 4.3) |

### 1.2 `sanic_carp_capi_reconciliarcorreos` (inv. 8.3, 7.19)

Red de seguridad de la entrada de correo (D-03, def. L262): registra los correos entrantes que quedaron sin conversación porque el plugin asíncrono falló. Reutiliza la misma lógica de 2.10. Quién la llama: el flujo de salud del buzón (inv. 11.2) con la cuenta de servicio. **Registro**: Action, global (unbound), `IsPrivate = false`. **Privilegio de ejecución**: `prvCreatesanic_carp_tbl_conversacion` (solo Servicio). **Identidad**: todo con SYSTEM; el llamador es la cuenta de servicio.

| Parámetro | Dir. | Tipo | Req | Nota |
|---|---|---|---|---|
| `emailid` | entrada | Guid | no | Si viene, reconcilia solo ese correo (reintento dirigido) |
| `maximo` | entrada | Integer | no | Cuántos correos como máximo. Ausente o `0` ⇒ 20; tope 100 (más se recorta, sin error); negativo ⇒ `CARP-REC-001` |
| `minutosminimos` | entrada | Integer | no | Solo correos creados hace al menos esos minutos (da tiempo al plugin asíncrono). Ausente ⇒ 10; `0` explícito ⇒ sin espera; negativo ⇒ `CARP-REC-002` |
| `encontrados` | salida | Integer | | Candidatos leídos en este lote |
| `registrados` | salida | Integer | | Los que quedaron con conversación (creada o existente) |
| `conerror` | salida | Integer | | Los que se omitieron con un código |
| `haymas` | salida | Boolean | | Hay más candidatos fuera del lote |
| `detalle` | salida | String | | JSON: `[{"emailid":"…","resultado":"creada\|unida\|yatenia\|omitido","conversacionid":"…","codigo":null}]`. Solo ids y códigos, nunca asunto ni remitente |

**Comportamiento**

1. Validar `maximo` y `minutosminimos`. Resolver `IConfiguracionDelArea` (0.4); cualquier `CARP-CFG-*` aborta la llamada.
2. Candidatos (servicio SYSTEM): `email` con `directioncode = falso`, `statuscode = Received`, `regardingobjectid` nulo, `owningbusinessunit` = la BU, `createdon <= ahora − minutosminimos`; orden `createdon` ascendente; `TopCount = maximo + 1`. Con `emailid`: solo ese correo, sin el filtro de antigüedad (`CARP-REC-003` si no existe o no es entrante).
3. Por cada candidato, `RegistradorDeCorreo.Registrar(emailId)` (2.10). Un resultado de negocio negativo (el correo no es entrante, etc.) se anota en `detalle` con su `CARP-ENT-*` y sigue. Un **fallo de la plataforma** (excepción de datos) no se atrapa: aborta la llamada con `CARP-REC-900` y revierte el lote entero. El flujo reintenta; el candidato "venenoso" repetido lo detecta el aviso del flujo.
4. `haymas = (candidatos > maximo)`; se procesan los primeros `maximo`.

Idempotente (BP-PP-055): un correo que ya tiene conversación no es candidato; dos correos con el mismo Message-ID comparten conversación (K-01).

**Concurrencia.** Si el plugin asíncrono y la reconciliación procesan el mismo correo a la vez, la clave K-01 rechaza la segunda creación; esa llamada falla con `CARP-REC-900` y la siguiente corrida ya lo encuentra registrado.

| Código | Mensaje | Reintentar |
|---|---|---|
| `CARP-REC-001` | El máximo de correos no puede ser negativo. | no |
| `CARP-REC-002` | Los minutos mínimos no pueden ser negativos. | no |
| `CARP-REC-003` | El correo indicado no existe o no es un correo entrante sin conversación. | no |
| `CARP-REC-900` | No se pudo completar la reconciliación. No se guardó ningún cambio del lote. | sí |

**Pruebas de aceptación** (`Aceptacion/ReconciliarCorreosAceptacion.cs`)

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-REC-01 | No hay candidatos | Se llama sin parámetros | Todos los contadores en 0, `haymas = false`, `detalle = []`; ninguna escritura |
| PA-REC-02 | Dos correos entrantes sin Regarding, con Message-ID distintos, creados hace 30 min, de la BU | Se llama | Dos conversaciones nuevas; cada Email con `regardingobjectid` y `ownerid` = team; `registrados = 2`; `detalle` con `resultado = creada` |
| PA-REC-03 | Un correo creado hace 3 minutos | Se llama con el valor por defecto (10) | No es candidato; `encontrados = 0` |
| PA-REC-04 | El mismo correo | Se llama con `minutosminimos = 0` | Se registra |
| PA-REC-05 | Un correo saliente sin Regarding, otro entrante con Regarding a una conversación, otro entrante de otra BU | Se llama | Ninguno es candidato |
| PA-REC-06 | Tres candidatos | `maximo = 1` | Procesa el más antiguo; `registrados = 1`, `haymas = true` |
| PA-REC-07 | — | `maximo = -1` y, en otra llamada, `minutosminimos = -5` | `CARP-REC-001` y `CARP-REC-002`, sin escrituras |
| PA-REC-08 | `maximo = 500` y 150 candidatos | Se llama | Procesa 100 (tope), `haymas = true` |
| PA-REC-09 | `emailid` de un correo que ya tiene conversación | Se llama | `resultado = yatenia`, sin escrituras |
| PA-REC-10 | `emailid` inexistente, y otro de un correo saliente | Se llama | `CARP-REC-003` en ambos |
| PA-REC-11 | Dos correos con el mismo Message-ID | Se llama dos veces seguidas | Una sola conversación; la segunda llamada encuentra 0 |
| PA-REC-12 | Un correo con `inreplyto` igual al `messageid` de un Email ya registrado en la conversación C | Se llama | `resultado = unida`; el correo queda con Regarding C; no se crea conversación (DC-08) |
| PA-REC-13 | La variable `sanic_carp_ev_nombrebu` no tiene valor | Se llama | `CARP-CFG-006`; sin escrituras |
| PA-REC-14 | Llamada hecha por la cuenta de servicio | Se llama | Lecturas y escrituras con el servicio de SYSTEM |
| PA-REC-15 | Fallo de datos inyectado al crear la segunda conversación | Se llama | `CARP-REC-900`; al restaurar, tampoco existe la primera |

### 1.3 `sanic_carp_capi_tomarconversacion` (inv. 8.4, 7.20)

Asigna la conversación al oficial que llama, con concurrencia optimista. Fuente: def. ítem 2 (L263-266), D-04, spike 2 (pregunta 3). **Registro**: Action, global (unbound), `IsPrivate = false`. **Privilegio de ejecución**: `prvWritesanic_carp_tbl_conversacion`. **Identidad**: lee y escribe con la identidad del llamador, sin elevar (def. L244).

| Parámetro | Dir. | Tipo | Req | Nota |
|---|---|---|---|---|
| `conversacionid` | entrada | Guid | sí | |
| `oficialasignadoid` | salida | Guid | | Quien quedó asignado (el llamador) |
| `oficialnombre` | salida | String | | Nombre completo de ese usuario |
| `yaasignada` | salida | Boolean | | `true` si ya estaba asignada al propio llamador y no se cambió nada |

**Comportamiento**

1. `conversacionid` ausente ⇒ `CARP-TOM-001`. `actor = InitiatingUserId`. Servicio del llamador.
2. Leer la conversación (`statecode`, `statuscode`, `sanic_oficialasignadoid`, `RowVersion`). No existe o sin acceso ⇒ `CARP-TOM-002` (mismo texto).
3. No está Abierta ⇒ `CARP-TOM-003`.
4. `sanic_oficialasignadoid` igual al actor ⇒ salida con `yaasignada = true`, sin escribir (idempotente).
5. Asignada a otro ⇒ `CARP-TOM-004` con el nombre de ese oficial (si no se puede leer, "otro oficial"). Sin escrituras.
6. `Update` de `sanic_oficialasignadoid = actor` con `RowVersion` leído y `ConcurrencyBehavior = IfRowVersionMatches`. Éxito ⇒ salidas.
7. Si la plataforma responde "versión de fila no coincide" (el Web API la muestra como 412, spike 2), **no se reintenta dentro del plugin** (la transacción ya no es confiable tras una falla de datos): se lanza `CARP-TOM-900`. El web resource vuelve a llamar; la segunda llamada ve la asignación del otro y responde `CARP-TOM-004` con su nombre. El **primero** gana (spike 2: "dos Tomar con el mismo ETag: 204 y 412").

El step de asignación (2.4) acepta este `Update` porque es de vacío a uno mismo (def. ítem 2).

| Código | Mensaje | Reintentar |
|---|---|---|
| `CARP-TOM-001` | Falta la conversación que se quiere tomar. | no |
| `CARP-TOM-002` | No se encontró la conversación o no tiene acceso a ella. | no |
| `CARP-TOM-003` | Solo se puede tomar una conversación abierta. | no |
| `CARP-TOM-004` | La conversación ya fue tomada por {oficial}. | no |
| `CARP-TOM-900` | La conversación cambió en este instante. Actualice la pantalla y vuelva a intentar. | sí |

**Pruebas de aceptación** (`Aceptacion/TomarConversacionAceptacion.cs`)

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-TOM-01 | Conversación Abierta sin oficial | El oficial A llama | `sanic_oficialasignadoid = A`; `yaasignada = false`; `oficialnombre` = nombre de A; el `Update` salió del servicio de A, con `IfRowVersionMatches`, y solo trae esa columna |
| PA-TOM-02 | Ya asignada a A | A llama otra vez | `yaasignada = true`; ninguna escritura |
| PA-TOM-03 | Asignada a B | A llama | `CARP-TOM-004` con el nombre de B; ninguna escritura |
| PA-TOM-04 | B tiene el nombre ilegible para A | A llama | El mensaje dice "otro oficial" |
| PA-TOM-05 | El doble lanza "versión de fila no coincide" en el `Update` | A llama | `CARP-TOM-900`; la conversación no cambió |
| PA-TOM-06 | Dos llamadas intercaladas: A y B leen; A escribe; B escribe con la versión vieja | Se ejecutan en ese orden | A queda asignado; B recibe `CARP-TOM-900`; una nueva llamada de B da `CARP-TOM-004` |
| PA-TOM-07 | Conversación Finalizada, y otra Unida | A llama | `CARP-TOM-003` en ambas |
| PA-TOM-08 | `conversacionid` ausente; luego inexistente; luego sin acceso | Se llama | `CARP-TOM-001`, `-002`, `-002` (mismo texto) |
| PA-TOM-09 | Cadena (pipeline simulado): llamador sin pertenencia a Jefatura | Se llama | El step `ConversacionAsignacionStep` deja pasar el `Update` (vacío → uno mismo) |

### 1.4 `sanic_carp_capi_unirconversaciones` (inv. 8.5, 7.21)

Mueve los correos de una conversación de origen a una de destino. Fuente: def. ítem 5 (L280-282), D-32, RF-14, spike 3 (REL-09 permite autorreferencia y ciclos). **Registro**: Action, global (unbound), `IsPrivate = false`. **Privilegio de ejecución**: `prvWritesanic_carp_tbl_conversacion`. **Identidad**: valida con el llamador y escribe como SYSTEM, con el actor capturado (def. L247).

| Parámetro | Dir. | Tipo | Req | Nota |
|---|---|---|---|---|
| `origenid` | entrada | Guid | sí | La que desaparece como conversación activa |
| `destinoid` | entrada | Guid | sí | La que recibe |
| `destinoid` | salida | Guid | | Eco del destino |
| `correosmovidos` | salida | Integer | | Correos cuyo Regarding pasó al destino |
| `arreglosrepunteados` | salida | Integer | | Arreglos que apuntan ahora al destino: **todos** los del origen, activos e inactivos (Terminado, Anulado, Descartado) desde el 2026-10-11. ~~Arreglos activos que apuntan ahora al destino~~ |

(El nombre `destinoid` en entrada y salida es legal: son tablas distintas, `patrones.md`.)

**Comportamiento**

1. Ids ausentes ⇒ `CARP-UNI-001`. Iguales ⇒ `CARP-UNI-002` (REL-09 permite que un registro se apunte a sí mismo; la API no).
2. Leer origen y destino con el servicio del llamador. Alguno inexistente o sin acceso ⇒ `CARP-UNI-003`.
3. Origen no Abierta ⇒ `CARP-UNI-004` (decidido el 2026-10-10, OA-11: un tema finalizado se continúa con Reabrir o Iniciar conversación nueva). Destino no Abierta ⇒ `CARP-UNI-005` (def. ítem 5). Origen y destino con cliente, y clientes distintos ⇒ `CARP-UNI-010` (decidido el 2026-10-10, OA-11: unir correos de dos clientes mezclaría datos de personas distintas, RNF-03). Si alguna de las dos no tiene cliente, o ninguna lo tiene, no hay nada que comparar y la unión sigue (confirmado por el usuario el 2026-10-10); el destino toma el cliente del origen solo si no tiene uno (paso 6c).
4. Cadenas y ciclos, **en este orden**:
   - Ciclo: seguir `sanic_unidaaid` desde el destino, hasta 10 saltos. Si se llega al origen, si se vuelve a una conversación ya visitada (incluido el destino apuntándose a sí mismo) o si no termina en 10 saltos ⇒ `CARP-UNI-007`.
   - Cadena: `origen.sanic_unidaaid` o `destino.sanic_unidaaid` no nulos (aunque el estado diga Abierta: dato corrupto) ⇒ `CARP-UNI-006`.
   - Alguna conversación tiene `sanic_unidaaid = origen` (el origen ya recibió uniones; sería una cadena) ⇒ `CARP-UNI-008`.
5. Contar los correos del origen con SYSTEM. Más de 1.000 ⇒ `CARP-UNI-009` (DC-16). Todavía no se escribió nada.
6. Con el servicio SYSTEM, en este orden:
   a. Cada Email del origen: `regardingobjectid = destino`. No se tocan dueño ni `parentactivityid`.
   b. Cada Arreglo con `sanic_conversacionid = origen`, **en cualquier estado** (Borrador, Vigente, Terminado, Anulado, Descartado), congelado o no: `sanic_conversacionid = destino`, y **solo esa columna** (el estado, el congelamiento y los motivos no se tocan). Decidido por el usuario el 2026-10-11 (inconsistencia 15), apoyado en V-2: la plataforma deja escribir sobre filas inactivas. ~~Solo los arreglos activos; los inactivos no se tocan.~~ El repunte lo hace SYSTEM dentro de esta API; **no habilita a ninguna persona** a editar un arreglo cerrado: un `Update` de una persona sobre `sanic_conversacionid` de un Terminado, Anulado o Descartado se rechaza con `CARP-ARR-022` (2.6, paso 0). `arreglosrepunteados` cuenta todos los actualizados.
   c. Destino: `sanic_clienteid` y `sanic_oficialasignadoid` del origen **solo si** el destino los tiene vacíos; y el recálculo (2.0.6): `sanic_fechaprimercorreo` y `sanic_sinrespuestadesde` = el mínimo de los no nulos; `sanic_cantidadrespuestas` = respuestas salientes procesadas entre los correos del destino (`sanic_respondidoporid` no nulo); `sanic_tiempoprimerarespuesta` = el tiempo de la respuesta procesada más antigua que tenga tiempo.
   d. Origen, con `RowVersion` leído en el paso 2 y `IfRowVersionMatches`: `statecode = 1`, `statuscode = 159460001` (Unida), `sanic_unidaaid = destino`, `sanic_fechafinalizacion = ahora` (P-08), `sanic_finalizadoporid = actor` (explícito: bajo SYSTEM la auditoría solo mostraría SYSTEM, def. L247), `sanic_sinrespuestadesde = null`.
7. Salidas.

**Concurrencia.** Dos uniones simultáneas sobre el mismo origen leen ambas "Abierta". La escritura del origen es condicional a la versión de fila: la perdedora recibe `CARP-UNI-900` y la plataforma revierte su transacción entera (los correos movidos vuelven). Toda la operación es una transacción (def. ítem 5: "todo en una transacción"). Los steps de Conversación (2.3) aceptan estas escrituras por ser del sistema (DC-03).

| Código | Mensaje | Reintentar |
|---|---|---|
| `CARP-UNI-001` | Faltan la conversación de origen o la de destino. | no |
| `CARP-UNI-002` | No se puede unir una conversación consigo misma. | no |
| `CARP-UNI-003` | No se encontró alguna de las conversaciones o no tiene acceso a ella. | no |
| `CARP-UNI-004` | La conversación de origen debe estar abierta. | no |
| `CARP-UNI-005` | La conversación de destino debe estar abierta. | no |
| `CARP-UNI-006` | Una de las conversaciones ya está unida a otra. No se permiten uniones encadenadas. | no |
| `CARP-UNI-007` | La unión formaría un ciclo entre conversaciones. | no |
| `CARP-UNI-008` | La conversación de origen ya recibió otras conversaciones. Una unión sobre otra unión no está permitida: una el destino sobre el origen o elija otro destino. | no |
| `CARP-UNI-009` | La conversación de origen tiene más de 1.000 correos y no se puede unir en una sola operación. | no |
| `CARP-UNI-010` | Las dos conversaciones son de clientes distintos. Solo se pueden unir conversaciones del mismo cliente. | no |
| `CARP-UNI-900` | No se pudo unir. No se guardó ningún cambio. Intente de nuevo. | sí |

**Pruebas de aceptación** (`Aceptacion/UnirConversacionesAceptacion.cs`)

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-UNI-01 | Origen A (3 correos, 1 arreglo Borrador) y destino B (2 correos), ambas Abiertas | El Oficial llama | Los 5 correos con Regarding B; el arreglo con `conversacionid` B; A en Unida (1/159460001) con `unidaaid` B, `fechafinalizacion` = ahora y `finalizadoporid` = Oficial; salidas 3 y 1; toda escritura con el servicio de SYSTEM |
| PA-UNI-02 | A tiene cliente y oficial; B no | Se llama | B recibe cliente y oficial de A |
| PA-UNI-03 | A y B tienen el mismo cliente y oficiales distintos | Se llama | B conserva su oficial y su cliente |
| PA-UNI-04 | A con `fechaprimercorreo` 10:00 y `sinrespuestadesde` 10:00; B con 09:00 y `sinrespuestadesde` vacío | Se llama | B queda con `fechaprimercorreo` 09:00 y `sinrespuestadesde` 10:00; A con `sinrespuestadesde` vacío |
| PA-UNI-05 | Correos del destino: 2 salientes procesados (tiempos 40 y 15, el de 40 enviado antes) y 1 del origen: 1 saliente procesado (tiempo 5) enviado después | Se llama | `cantidadrespuestas = 3`; `tiempoprimerarespuesta = 40` |
| PA-UNI-06 | `origenid = destinoid` | Se llama | `CARP-UNI-002` |
| PA-UNI-07 | Ids ausentes (dos casos) | Se llama | `CARP-UNI-001` |
| PA-UNI-08 | Una de las conversaciones no existe; otra que el llamador no puede leer | Se llama | `CARP-UNI-003` |
| PA-UNI-09 | Origen Finalizada; origen Unida (OA-11) | Se llama | `CARP-UNI-004` |
| PA-UNI-10 | Destino Finalizada; destino Unida | Se llama | `CARP-UNI-005` |
| PA-UNI-11 | Destino Abierta con `unidaaid` apuntando a una tercera conversación (cadena que termina); origen con `unidaaid` no nulo | Se llama (dos casos) | `CARP-UNI-006` |
| PA-UNI-12 | Destino con `unidaaid` = origen; destino apuntándose a sí mismo; cadena de 11 saltos que no termina | Se llama (tres casos) | `CARP-UNI-007` |
| PA-UNI-13 | La conversación C tiene `unidaaid` = origen | Se llama | `CARP-UNI-008`; sin escrituras |
| PA-UNI-14 | 1.001 correos en el origen | Se llama | `CARP-UNI-009`; sin escrituras |
| PA-UNI-15 | Un Arreglo Terminado apunta al origen | Se llama | Su `sanic_conversacionid` pasa al destino (escritura de SYSTEM) y `arreglosrepunteados` lo cuenta; el estado Terminado, el congelamiento y las demás columnas no cambian. ~~Antes: no se modifica y no se cuenta~~ (decidido el 2026-10-11) |
| PA-UNI-16 | Fallo inyectado al actualizar el origen (último paso) | Se llama | `CARP-UNI-900`; al restaurar, ningún correo cambió de Regarding |
| PA-UNI-17 | `UserId` del contexto distinto de `InitiatingUserId` | Se llama | `finalizadoporid = InitiatingUserId` |
| PA-UNI-18 | Los correos del origen tienen dueños y `parentactivityid` | Se llama | Siguen iguales tras el movimiento |
| PA-UNI-19 | Clientes distintos en origen y destino (OA-11) | Se llama | `CARP-UNI-010`; sin escrituras. ~~Antes: se une sin error~~ |
| PA-UNI-21 | Entre la lectura del origen y su escritura, otro proceso lo modificó (el doble sube la versión de fila) | Se llama | `CARP-UNI-900`; al restaurar, ningún correo cambió de Regarding |
| PA-UNI-20 | Cadena (pipeline simulado): se ejecuta la API y luego los steps de Conversación con identidad SYSTEM | Se llama | `ConversacionActualizarStep` acepta Unida, `unidaaid` y `finalizadoporid` (escritura del sistema); un Oficial que intente lo mismo por `Update` directo recibe `CARP-CNV-006` |
| PA-UNI-22 | Origen y destino del mismo cliente, ambas Abiertas (OA-11) | Se llama | Se une sin error; el destino conserva su cliente |
| PA-UNI-23 | Origen sin cliente y destino con cliente; y origen y destino sin cliente (dos casos) (OA-11) | Se llama | Se une sin error en los dos; el destino conserva su cliente o sigue sin él |
| PA-UNI-24 | Al origen apuntan un Borrador, un Vigente congelado, un Terminado, un Anulado y un Descartado (V-2, decisión del 2026-10-11) | Se llama | Los cinco quedan con `sanic_conversacionid` = destino; `arreglosrepunteados = 5`; en cada uno el `Update` trae solo `sanic_conversacionid`, salió del servicio de SYSTEM y el estado y el congelamiento siguen iguales (el doble con `RechazarEscriturasEnInactivas = false`) |
| PA-UNI-25 | Cadena (pipeline simulado): se ejecuta la API y luego `ArregloActualizarStep` (S-21) con identidad SYSTEM sobre un Terminado, un Anulado y un Descartado | Se llama | S-21 acepta el repunte por ser del sistema (DC-03); un Oficial que haga el mismo `Update` directo sobre ese Terminado recibe `CARP-ARR-022` (PA-ARR-51) |

### 1.5 `sanic_carp_capi_iniciarconversacionnueva` (inv. 8.6, 7.22)

Cuando llegó un correo a una conversación Finalizada, el oficial puede abrir una conversación nueva con él. Fuente: def. ítem 6 (L283-287), D-32, RF-15, dicc. P-09. **Registro**: Action, global (unbound), `IsPrivate = false`. **Privilegio de ejecución**: `prvWritesanic_carp_tbl_conversacion`. **Identidad**: valida con el llamador; escribe como SYSTEM.

| Parámetro | Dir. | Tipo | Req | Nota |
|---|---|---|---|---|
| `conversacionid` | entrada | Guid | sí | La Finalizada con el indicador |
| `nuevaconversacionid` | salida | Guid | | |
| `correosmovidos` | salida | Integer | | |

**Comportamiento**

1. Id ausente ⇒ `CARP-INI-001`. Leer la conversación con el llamador; no existe o sin acceso ⇒ `CARP-INI-002`.
2. Debe estar Finalizada (1/2) **y** con `sanic_correonuevoenfinalizada = sí` ⇒ si no, `CARP-INI-003`.
3. Con SYSTEM: correos de esa conversación con `directioncode = falso` y fecha de llegada (`actualend`, si es nula `createdon`) **posterior** a `sanic_fechafinalizacion`, orden ascendente por esa fecha (DC-17). Ninguno ⇒ `CARP-INI-004` (el indicador está inconsistente; no se limpia solo). Más de 1.000 ⇒ `CARP-INI-006`.
4. El primero (`E1`): huella de su Message-ID (DC-13). Si ya existe una conversación con esa huella ⇒ `CARP-INI-005`.
5. Escribir con SYSTEM:
   a. `Create` de la conversación nueva, con los mismos campos que la entrada de correo (2.10): huella, `sanic_fechaprimercorreo` = llegada de `E1`, `sanic_asunto` y `sanic_remitente` de `E1`, `sanic_sinrespuestadesde` = llegada de `E1`, `sanic_cantidadrespuestas = 0`, `sanic_correonuevoenfinalizada = no`, `sanic_clienteid` de la anterior, **sin** oficial, `ownerid` = team.
   b. Cada correo movido: `regardingobjectid = nueva`.
   c. La anterior: `sanic_correonuevoenfinalizada = no`, `sanic_sinrespuestadesde = null`. Sigue Finalizada.
6. Salidas.

**Concurrencia.** Dos llamadas simultáneas sobre la misma conversación crean la misma huella: K-01 rechaza la segunda, que falla con `CARP-INI-900`. La primera deja el indicador limpio, y un reintento ve `CARP-INI-003`.

No hay columna de actor para esta acción; la fila nueva y los movimientos quedan atribuidos al sistema. Las respuestas salientes posteriores a la finalización se quedan en la anterior.

| Código | Mensaje | Reintentar |
|---|---|---|
| `CARP-INI-001` | Falta la conversación. | no |
| `CARP-INI-002` | No se encontró la conversación o no tiene acceso a ella. | no |
| `CARP-INI-003` | Solo una conversación finalizada con correos nuevos puede iniciar una conversación nueva. | no |
| `CARP-INI-004` | La conversación está marcada con correos nuevos, pero no se encontró ninguno posterior a la finalización. Avise al equipo de soporte. | no |
| `CARP-INI-005` | Ya existe una conversación para ese correo. | no |
| `CARP-INI-006` | Hay más de 1.000 correos nuevos y no se pueden mover en una sola operación. | no |
| `CARP-INI-900` | No se pudo iniciar la conversación nueva. No se guardó ningún cambio. Intente de nuevo. | sí |

**Pruebas de aceptación** (`Aceptacion/IniciarConversacionNuevaAceptacion.cs`)

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-INI-01 | Finalizada en T con indicador; entrantes `e1` (antes de T), `e2` (T+1 h), `e3` (T+2 h); saliente `s1` (T+30 min); cliente C, oficial O | Se llama | Nueva conversación Abierta con huella de `e2`, `fechaprimercorreo` y `sinrespuestadesde` = llegada de `e2`, asunto y remitente de `e2`, cliente C, **sin** oficial, `ownerid` = team, `cantidadrespuestas = 0`; `e2` y `e3` con Regarding nueva; `e1` y `s1` siguen en la anterior; la anterior con indicador `no` y `sinrespuestadesde` vacío; salidas `correosmovidos = 2` |
| PA-INI-02 | Conversación Abierta; Finalizada sin indicador; Unida | Se llama | `CARP-INI-003` en los tres |
| PA-INI-03 | Indicador `sí` pero sin entrantes posteriores | Se llama | `CARP-INI-004`; sin escrituras |
| PA-INI-04 | Ya existe una conversación con la huella de `e2` | Se llama | `CARP-INI-005` |
| PA-INI-05 | 1.001 entrantes posteriores | Se llama | `CARP-INI-006` |
| PA-INI-06 | Id ausente; inexistente; sin acceso | Se llama | `CARP-INI-001`, `-002`, `-002` |
| PA-INI-07 | `e2` sin `actualend` | Se llama | Usa `createdon` para ordenar y como fecha |
| PA-INI-08 | `e2` sin `messageid` | Se llama | Huella de respaldo `sin-messageid:<id del Email>` (DC-13) |
| PA-INI-09 | Fallo inyectado al mover `e3` | Se llama | `CARP-INI-900`; al restaurar, no existe la conversación nueva |
| PA-INI-10 | Anterior sin cliente | Se llama | La nueva queda sin cliente |
| PA-INI-11 | Se llama dos veces seguidas | Segunda llamada | `CARP-INI-003` (el indicador ya se limpió) |
| PA-INI-12 | Escrituras | Se llama | Todas con el servicio de SYSTEM; lecturas de la conversación con el del llamador |
| PA-INI-13 | La anterior está inactiva (Finalizada) | Se llama | El `Update` de sus dos columnas se hace (V-2); el estado sigue Finalizada |
| PA-INI-15 | El doble lanza la falla de clave duplicada al crear la conversación nueva | Se llama | `CARP-INI-900`; sin cambios |
| PA-INI-14 | Cadena (pipeline simulado) | Se llama | Los steps de propietario y de Conversación aceptan la creación y la actualización como escrituras del sistema, sin consultar la BU por cada una (contar llamadas del doble) |

---

## 2. Plugins y steps

### Reglas comunes de todo step

- Cada step es una clase `IPlugin` con **constructor sin parámetros** (el que usa Dataverse) y un constructor `internal` que recibe los puertos (servicio, reloj, configuración, identidad) para las pruebas (`patrones.md`, "Refactor seguro"). Nunca se renombra la clase ni el namespace tras el primer registro (conv. §5).
- La clase del step solo arma el contexto y delega. La regla vive en `Dominio` (puro) o en una clase de `Steps/` con puertos. Un step no cachea nada en campos de instancia entre ejecuciones.
- Los steps de gestos y de columnas solo-sistema siguen el esquema: (1) calcular en memoria las columnas **vigiladas que cambiaron** (DC-01); (2) si no hay ninguna, terminar; (3) recién entonces consultar identidad; (4) si es escritura del sistema, terminar (DC-03); (5) evaluar; (6) escribir en el `Target` las columnas que el sistema fija.
- `ColumnSet` siempre con nombres. Nunca `ColumnSet(true)`.
- Errores: `InvalidPluginExecutionException("CARP-…")` para negocio; cualquier otra excepción se traza y se relanza como `CARP-<ÁREA>-900` (0.5).
- No se usa `Depth` para saltarse reglas. ~~Sí se usa para la protección contra recursión donde un step escribe sobre su propia tabla (2.7).~~ **Corregido el 2026-10-11:** 2.7 no usa `Depth`, y ningún otro step tampoco: no hay protección contra recursión por profundidad porque ningún step de este documento vuelve a dispararse a sí mismo (`CuotaTerminarArregloStep`, el único que escribe sobre otra tabla con steps de Update, escribe sobre Arreglo como SYSTEM: `ArregloActualizarStep` termina sin reglas por DC-03 y `ArregloCalendarioStep` filtra columnas que esa escritura no trae). `Depth` solo aparece como dato del contexto simulado (4.5).
- **Los steps de `Update` no asumen profundidad 1 (V-1, 2026-10-11).** Un `SetStateRequest` llega a los plugins como `SetStateDynamicEntity` y de ahí cae en un `Update` anidado, a profundidad 2, con `statecode` y `statuscode` en el `Target`; el gesto de estado debe evaluarse igual que en un PATCH (profundidad 1). Tampoco se mira el mensaje padre: un `Update` se evalúa por lo que trae el `Target`. No se registra ningún step de `SetState` (nunca se dispara); el `Update` anidado ya pasa por los steps de `Update`. Ningún step de este documento filtra por `Depth` (revisado el 2026-10-11).
- **Una fila inactiva o congelada solo la protegen los plugins (V-2, 2026-10-11).** La plataforma deja escribir sobre filas inactivas a SYSTEM y a las personas. Cada tabla con estados cerrados tiene su regla en 2.3 (Conversación), 2.6 (Arreglo), 2.7 (Cuota, a través del estado del Arreglo) y 2.8 (Versión y Mapeo); un cambio de estado hecho por una persona con un PATCH directo pasa por la misma máquina de estados que el gesto (sección 3). **Decisión del usuario del 2026-10-11:** en una Conversación Finalizada o Unida una persona no cambia ninguna columna sin reabrir (`CARP-CNV-009`, 2.3), y el `statecode` nativo de Cuota y de Versión de plantilla solo lo cambia SYSTEM (2.7, 2.8); en Arreglo, `sanic_nombre` es solo-sistema en todos los estados (2.0.8, 2.6). **Decisiones del usuario del 2026-10-11 (D-40):** el `statecode` y el `statuscode` nativos de Mapeo de celda (2.8) y de Cliente (2.2) también los cambia solo SYSTEM; y el gesto Reabrir, de Conversación (2.3) y de Arreglo (2.6), no admite ningún otro cambio real en el mismo guardado.
- Trazas: prefijo `[NombreDelStep]`, entrada y salida, solo ids. Nunca cédula, cuenta, nombre ni cuerpo.

### 2.0 Piezas de dominio puro (inv. 7.2 a 7.7)

Sin `Microsoft.Xrm.Sdk`. Se prueban con pruebas unitarias puras. Firmas en 4.4.

#### 2.0.1 Normalización (P-06; inv. 7.3)

| Dato | Regla |
|---|---|
| Número de producto | Sin ningún espacio en blanco, mayúscula. **Los guiones se conservan** |
| Cédula | Igual que el número de producto |
| Correo | Sin espacios en blanco, minúscula |
| Nombre completo | Recortado; las secuencias de espacios internos pasan a un solo espacio. Conserva tildes |
| Celular | Recortado |
| Cualquier texto | `null` se queda `null`; una cadena solo de espacios pasa a `null`; normalizar dos veces da lo mismo que una. Siempre con cultura invariante |

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-NOR-01 | `"5412 7500 1234 8890"` | Normalizar número de producto | `"5412750012348890"` |
| PA-NOR-02 | `" 10-2033-4471 "` y `"ab-12 c"` | Normalizar número de producto | `"10-2033-4471"` y `"AB-12C"` |
| PA-NOR-03 | `" 001 -000000- 0001a"` | Normalizar cédula | `"001-000000-0001A"` |
| PA-NOR-04 | `"  Ana.Perez@Banco.COM "` | Normalizar correo | `"ana.perez@banco.com"` |
| PA-NOR-05 | `"  Ana   María\tPérez "` | Normalizar nombre | `"Ana María Pérez"` |
| PA-NOR-06 | `null`, `""`, `"   "` | Normalizar cualquiera | `null` |
| PA-NOR-07 | Cultura del hilo `tr-TR` | Normalizar `"id-1"` | `"ID-1"` (no `"İD-1"`) |
| PA-NOR-08 | Cualquier valor ya normalizado | Normalizar de nuevo | Mismo valor |

#### 2.0.2 Huella del Message-ID (D-03, DC-13; inv. 7.3)

Normalizar: quitar todo espacio en blanco, quitar **un** `<` inicial y **un** `>` final, pasar a minúscula. Huella = SHA-256 de los bytes UTF-8 en hexadecimal minúscula (64 caracteres). Si el Message-ID es nulo, vacío o queda vacío tras normalizar: huella de `"sin-messageid:" + id del Email en formato "D" minúscula`.

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-HUE-01 | `"<CAF+abc123@mail.example.com>"` | Calcular huella | `093f09291642088a9522c5833b491aa367f7d28c10544c960eff9d3a8dd05d1d` |
| PA-HUE-02 | `" <caf+ABC123@Mail.Example.com> "` y `"caf+abc123@mail.example.com"` | Calcular huella | La misma del caso anterior |
| PA-HUE-03 | `"<ab c@x>"` | Normalizar | `"abc@x"` |
| PA-HUE-04 | Message-ID `null`, `""`, `"<>"` y el Email `11111111-1111-1111-1111-111111111111` | Calcular huella | `bdc7ce0b320a83847a51efaa9e521460ec45b9a52d14d77914a562c933b99340` en los tres casos |
| PA-HUE-05 | Cualquier entrada | Calcular huella | 64 caracteres, solo `[0-9a-f]` |

#### 2.0.3 Montos del arreglo (D-30; dicc. DD-12, R-04; def. ítem 7; inv. 7.4)

`ReglasDeMontos.Calcular(saldoInicial, montoAbonado, cuotaMensual, plazo)`. Todo en `decimal`. Orden de validación:

1. `cuotaMensual <= 0` ⇒ error `CARP-ARR-007`.
2. `montoAbonado > saldoInicial` ⇒ `CARP-ARR-004`.
3. `saldoRemanente = saldoInicial − montoAbonado`.
4. Si `cuotaMensual × (plazo − 1) >= saldoRemanente` ⇒ `CARP-ARR-005`: con una cuota menos ya se cubre el saldo. El mensaje indica con cuántas cuotas se cubre: `N = ceil(saldoRemanente / cuotaMensual)`. Si `saldoRemanente = 0`, el mensaje dice que no hay saldo por pagar.
5. Si `cuotaMensual × plazo > saldoRemanente`: `totalPagar = saldoRemanente`; `montoUltimaCuota = saldoRemanente − cuotaMensual × (plazo − 1)` (siempre mayor que cero por la regla 4).
6. Si no (alivio temporal): `totalPagar = cuotaMensual × plazo`; `montoUltimaCuota = cuotaMensual`.

Resultado: `SaldoRemanente`, `TotalPagar`, `MontoUltimaCuota`, o el error con su código y texto.

| ID | Dado (inicial, abonado, cuota, plazo) | Cuando | Entonces |
|---|---|---|---|
| PA-MON-01 | 10000, 2000, 1000, 8 | Calcular | remanente 8000, total 8000, última 1000 |
| PA-MON-02 | 10000, 2000, 1000, 9 | Calcular | `CARP-ARR-005`, "con 8 cuotas" |
| PA-MON-03 | 1000, 0, 300, 4 | Calcular | remanente 1000, total 1000, última 100 |
| PA-MON-04 | 10000, 0, 500, 6 | Calcular | total 3000 (alivio temporal), última 500 |
| PA-MON-05 | 1000, 1500, 100, 5 | Calcular | `CARP-ARR-004` |
| PA-MON-06 | 1000, 1000, 100, 5 | Calcular | `CARP-ARR-005`, sin saldo por pagar |
| PA-MON-07 | 1000, 0, 0 y 1000, 0, −5, 3 | Calcular | `CARP-ARR-007` en ambos |
| PA-MON-08 | 500, 0, 800, 1 | Calcular | total 500, última 500 |
| PA-MON-09 | 1000, 0, 200, 1 | Calcular | total 200, última 200 |
| PA-MON-10 | 1000.10, 0.05, 333.35, 3 | Calcular | remanente 1000.05, total 1000.05, última 333.35 |
| PA-MON-11 | 0.30, 0.10, 0.10, 2 | Calcular | remanente 0.20 exacto, total 0.20 (la prueba compara con `decimal`; detecta una versión basada en `double`) |
| PA-MON-12 | 999999999.99, 0.01, 16666666.67, 60 | Calcular | remanente 999999999.98, total 999999999.98, última 16666666.45 |
| PA-MON-13 | 1000, 0, 1000, 2 | Calcular | `CARP-ARR-005` con el texto en singular: "con 1 cuota" |
| PA-MON-14 | Cualquier caso válido | Listar los montos de las cuotas | `plazo − 1` cuotas de `cuotaMensual` y la última igual a `MontoUltimaCuota`; la suma de las cuotas es `totalPagar` |

#### 2.0.4 Calendario de cuotas (def. ítem 8; inv. 7.4)

`GeneradorDeCalendario.Generar(fechaInicio, plazo, cuotaMensual, montoUltimaCuota)` devuelve `plazo` cuotas con `Numero` (1..plazo), `FechaVencimiento` y `Monto`. **Regla de fechas decidida el 2026-10-10 (OA-5, opción A)**: la cuota `n` vence en `fechaInicio.Date.AddMonths(n − 1)`, es decir, el mismo día de cada mes contado siempre desde la fecha de inicio (nunca encadenando mes a mes), y si el mes no tiene ese día, el último del mes. La regla de negocio puede consultarse con Héctor Romero, pero el código ya la aplica. La cuota 1 vence en `fechaInicio` (dicc. §4.3: "fecha de la primera cuota"). `fechaFin` = vencimiento de la última cuota; el plugin la calcula y la sobrescribe (OA-5, opción (i)). Toda la regla queda en este único método (def. ítem 8: "centralizada en el plugin").

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-FEC-01 | Inicio 2026-11-05, plazo 3 | Generar | 2026-11-05, 2026-12-05, 2027-01-05 |
| PA-FEC-02 | Inicio 2026-01-31, plazo 4 | Generar | 01-31, 02-28, 03-31, 04-30 (no encadenado: marzo vuelve a 31) |
| PA-FEC-03 | Inicio 2027-12-31, plazo 3 | Generar | 2027-12-31, 2028-01-31, 2028-02-29 |
| PA-FEC-04 | Plazo 60 | Generar | 60 cuotas, números 1 a 60, fechas estrictamente crecientes |
| PA-FEC-05 | Cuota 300, última 100, plazo 4 | Generar | montos 300, 300, 300, 100 |
| PA-FEC-06 | Plazo 0 y plazo 61 | Generar | `ArgumentOutOfRangeException` (el plugin ya rechazó antes con `CARP-ARR-003`) |
| PA-FEC-07 | Inicio con hora 2026-11-05 14:30 | Generar | Usa solo la fecha |

#### 2.0.5 Consecutivo por crédito (D-09, RF-07; inv. 7.5)

`ConsecutivoDeCredito.Siguiente(numerosExistentes)`: máximo de los no nulos más uno; `1` si no hay ninguno. Los números existentes son los de **todos** los arreglos del mismo (producto, número de producto) con `sanic_numeroarreglo` no nulo, en cualquier estado (un Anulado conserva su número y cuenta; un Borrador o Descartado no tiene). Si el siguiente supera 9999 (rango del dicc. §11.3) ⇒ `CARP-ARR-023`.

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-CON-01 | Sin números | Siguiente | 1 |
| PA-CON-02 | 1, 2, 3 | Siguiente | 4 |
| PA-CON-03 | `null`, 2 | Siguiente | 3 |
| PA-CON-04 | 1, 3 (hueco en 2) | Siguiente | 4 (no se rellenan huecos) |
| PA-CON-05 | 9999 | Siguiente | `CARP-ARR-023` |

#### 2.0.6 Tiempos y recálculo de la conversación (RF-03, RF-04; dicc. DD-05; inv. 7.6)

- `MinutosEntre(desde, hasta)`: parte entera de los minutos (truncada hacia abajo); si `hasta < desde`, 0 (DC-11).
- `AplicarRespuestaEnviada(estado, sinRespuestaDesde, cantidad, primeraRespuesta, enviadoEn)` devuelve: `TiempoDeEstaRespuesta` (o nulo), `NuevoSinRespuestaDesde`, `NuevaCantidad`, `NuevaPrimeraRespuesta`.
  - Conversación Abierta y `sinRespuestaDesde` no nulo y `sinRespuestaDesde <= enviadoEn`: tiempo = minutos entre ambos; `NuevoSinRespuestaDesde = null`; si `primeraRespuesta` es nula, pasa a ser ese tiempo.
  - Abierta y `sinRespuestaDesde` nulo: sin tiempo; nada más cambia.
  - Abierta y `sinRespuestaDesde > enviadoEn` (el pendiente llegó después): sin tiempo; `sinRespuestaDesde` se mantiene.
  - Finalizada (decidido el 2026-10-10, OA-10): sin tiempo y sin cambios en `sinRespuestaDesde`; la respuesta solo suma al contador. Para marcar respondido, el oficial reabre.
  - En todos los casos `NuevaCantidad = cantidad + 1`.
- `Recalcular` para la unión: ver PA-TIE-10 y 1.4 paso 6c.

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-TIE-01 | 10:00 y 10:45 | Minutos | 45 |
| PA-TIE-02 | 10:00:00 y 10:45:59 | Minutos | 45 |
| PA-TIE-03 | 10:45 y 10:00 | Minutos | 0 |
| PA-TIE-04 | 10:00 de un día y 10:00 del siguiente | Minutos | 1440 |
| PA-TIE-05 | Abierta, pendiente 10:00, enviado 10:45, cantidad 0, primera nula | Aplicar | tiempo 45, pendiente nulo, cantidad 1, primera 45 |
| PA-TIE-06 | Igual pero primera ya 30 y cantidad 3 | Aplicar | primera 30, cantidad 4 |
| PA-TIE-07 | Abierta, pendiente nulo | Aplicar | tiempo nulo, cantidad +1, primera sin cambio |
| PA-TIE-08 | Abierta, pendiente 10:50, enviado 10:45 | Aplicar | tiempo nulo, pendiente 10:50, cantidad +1 |
| PA-TIE-09 | Finalizada, pendiente 10:00, enviado 10:45 | Aplicar | tiempo nulo, pendiente 10:00, cantidad +1 |
| PA-TIE-10 | Dos conversaciones con `fechaprimercorreo` 10:00 y 09:00, pendientes 10:00 y nulo, y los correos salientes procesados con (enviado, tiempo) = (11:00, 40), (12:00, 15), (13:00, 5) | Recalcular | `fechaprimercorreo` 09:00, pendiente 10:00, cantidad 3, `primera` 40 |

#### 2.0.7 Validación de publicación del mapeo (D-15, ítem 15; inv. 7.7)

`ValidadorDePublicacion.Validar(capacidad, filasDeMapeo)` devuelve **todos** los errores, no solo el primero (spike 1, `ValidarMapeo`). Cada error trae código y texto. Reglas:

1. El mapeo no está vacío ⇒ `CARP-VER-007`.
2. Las reglas del generador (`ValidarMapeo` portado del spike 1): celda A1 bien formada (A1 a XFD1048576), sin dos campos en la misma celda de la misma hoja, bloque de cuotas completo (las cinco columnas), campo coherente con el tipo ⇒ `CARP-VER-007` con la lista.
3. Tabla "tipo de mapeo que admite" de dicc. §3.1 (columna 4): las 19 primeras opciones y `Moneda` solo con ValorSimple; `Cuota: …` (5) solo con ColumnaDeBloqueDeCuotas; `Total: …` (2) solo con TotalCalculado ⇒ `CARP-VER-007`.
4. Todas las columnas del bloque tienen `sanic_filasmaximas` igual a `capacidad` ⇒ si no, `CARP-VER-008`.

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-PUB-01 | El mapeo v07 completo (27 filas del fixture) con capacidad 60 | Validar | Sin errores |
| PA-PUB-02 | Mapeo vacío | Validar | `CARP-VER-007` |
| PA-PUB-03 | Celdas `B0`, `7B`, `XFE1` | Validar | Un error por celda, los tres en la misma respuesta |
| PA-PUB-04 | `B7` usada por dos campos de la misma hoja; y la misma celda en dos hojas distintas | Validar | Error solo en el primer caso |
| PA-PUB-05 | Bloque sin `Cuota: monto pagado` | Validar | `CARP-VER-007` |
| PA-PUB-06 | `Cuota: monto` con ValorSimple; `Numero de arreglo` con Bloque; `Total: montos de cuota` con Bloque; `Moneda` con Total | Validar | Un error por cada uno |
| PA-PUB-07 | Capacidad 60, bloque con filas máximas 59 en una columna | Validar | `CARP-VER-008` |
| PA-PUB-08 | Capacidad 48, bloque con 60 | Validar | `CARP-VER-008` |
| PA-PUB-09 | Hoja `" FORMATO ARP ELECTRONICO"` (espacio inicial) | Validar | Sin error; el nombre se conserva tal cual (spike 1, hallazgo 1) |

#### 2.0.8 Columnas por categoría (DC-26)

Viven en `Dominio/Columnas`. La prueba `CatalogoDeColumnasAceptacion` lee `diseno/02-diccionario-datos.md` (busca el archivo subiendo desde el directorio de la prueba; falla si no lo encuentra) y exige que **cada columna de cada tabla propia** esté en exactamente una categoría de su tabla, y que cada categoría solo nombre columnas que existen. Así una columna nueva no queda fuera de la regla en silencio (`patrones.md` §3, D-63 de M-PPP). Además toma de ese documento las columnas con *Sis* = Sí y verifica que son exactamente la unión de las categorías "solo-sistema".

| Tabla | Solo-sistema | De gesto | Datos del acuerdo (se bloquean al congelar) | Libres |
|---|---|---|---|---|
| Conversación | `sanic_huellamessageid`, `sanic_fechaprimercorreo`, `sanic_asunto`, `sanic_remitente`, `sanic_sinrespuestadesde`, `sanic_correonuevoenfinalizada`, `sanic_fechafinalizacion`, `sanic_tiempoprimerarespuesta`, `sanic_tiempototal`, `sanic_cantidadrespuestas`, `sanic_unidaaid`, `sanic_finalizadoporid` | `sanic_resultado`, `sanic_motivosinrespuesta`, `sanic_detallesinrespuesta` | — | `sanic_clienteid`, `sanic_nombre` (autonumérica). Aparte: `sanic_oficialasignadoid` (2.4), `ownerid` (2.1), `statecode`/`statuscode` (3.1), `sanic_antiguedad` (fórmula, no escribible). **Desde el 2026-10-11, en una Conversación Finalizada o Unida cuentan como vigiladas también `sanic_clienteid`, `sanic_nombre`, `sanic_oficialasignadoid` y `ownerid`** (2.3, `CARP-CNV-009`): en una Abierta siguen sin vigilancia en este step |
| Arreglo | `sanic_numeroarreglo`, `sanic_numeroprovisional`, `sanic_saldoremanente`, `sanic_totalpagar`, `sanic_fechacongelamiento` (salvo el gesto Desbloquear), `sanic_instantaneanombre`, `sanic_instantaneacedula`, `sanic_instantaneacorreo`, `sanic_instantaneacelular`, `sanic_fechaformalizado`, `sanic_formalizadoporid`, `sanic_fechadescarte`, `sanic_descartadoporid`, `sanic_fechaanulacion`, `sanic_anuladoporid`, `sanic_fechareapertura`, `sanic_reabiertoporid`, `sanic_cantidadreaperturas`, `sanic_fechatermino`, `sanic_versionplantillaid`, `sanic_nombre` (autonumérica; solo-sistema en todos los estados desde el 2026-10-11. **En el Create la columna siempre está en el `Target` (V-8):** no se rechaza por presencia sino solo si su valor no cumple `^ARR-[0-9]{6,16}$`, 2.5 paso 2) | `sanic_motivodescarte`, `sanic_motivoanulacion`, `sanic_motivoreapertura` | `sanic_producto`, `sanic_numeroproducto`, `sanic_tipo`, `sanic_moneda`, `sanic_canalorigen`, `sanic_saldoinicial`, `sanic_montoabonado`, `sanic_plazo`, `sanic_cuotamensual`, `sanic_fechainicio`, `sanic_fechafin`, `sanic_cuentadebitar`, `sanic_fechaformalizacion`, `sanic_comentarios`, `sanic_clienteid`, `sanic_oficialid` | `sanic_conversacionid` ~~, `sanic_nombre`~~ (movida a solo-sistema el 2026-10-11). Aparte: `ownerid`, `statecode`/`statuscode` |
| Cuota | `sanic_nombre`, `sanic_numero`, `sanic_fechavencimiento`, `sanic_monto`, `sanic_arregloid` | — | — | de pago: `sanic_fechapago`, `sanic_montopagado`. Aparte: `statecode`/`statuscode` (solo SYSTEM desde el 2026-10-11, 2.7) |
| Versión de plantilla | `sanic_huella`, `sanic_fechaaprobacion`, `sanic_aprobadoporid` | `sanic_estado` (gesto Publicar) | — | `sanic_nombre`, `sanic_archivo`, `sanic_capacidad` (solo en Borrador). Aparte: `statecode`/`statuscode` (solo SYSTEM desde el 2026-10-11, 2.8) |
| Mapeo de celda | `sanic_nombre` (calculada), `sanic_versionplantillaid` | — | — | `sanic_hoja`, `sanic_celda`, `sanic_tipo`, `sanic_campo`, `sanic_filasmaximas` (solo en versión Borrador). Aparte: `statecode`/`statuscode` (solo SYSTEM desde el 2026-10-11, D-40, 2.8) |
| Email (columnas propias) | `sanic_tiemporespuesta`, `sanic_respondidoporid` | — | — | — |

"Datos del acuerdo" incluye `sanic_clienteid` y `sanic_oficialid` porque la instantánea y el formato los imprimen (DC-26 los deriva de dicc. §4.3 y def. ítem 10). `sanic_conversacionid` no se imprime: queda libre. Cliente, Generación de formato y la columna `sanic_nombre` de Cliente no tienen categorías: Cliente se rige por 2.2 (su `statecode`/`statuscode` nativos solo los cambia SYSTEM desde el 2026-10-11, D-40); Generación no la escribe ningún humano.

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-COL-01 | El diccionario de datos | Contrastar | Cada columna de Conversación, Arreglo, Cuota, Versión, Mapeo y Email está en exactamente una categoría; ninguna categoría nombra una columna inexistente |
| PA-COL-02 | Las columnas con Sis = Sí del diccionario | Contrastar | Coinciden con la unión de "solo-sistema" de cada tabla (más `sanic_finalizadoporid` y `sanic_unidaaid`, que el diccionario marca en la nota de lookups). **2026-10-11 (inconsistencia 19):** esta prueba fallaba contra el diccionario, que marcaba Sis = No en las cinco columnas de Cuota y en las tres de pago de Versión que esta tabla declara solo-sistema; se alineó el diccionario (es coherente con `CARP-CUO-001` y `CARP-VER-002`), no esta tabla. Mapeo: `sanic_nombre` sigue sin alinear (inconsistencia 20) |

### 2.1 Propietario (inv. 7.17; D-31, P-11)

Un plugin pre-create fija el team del área como propietario de **las siete tablas propias**; un pre-update rechaza otro propietario. No hace falta Assign: crear con el team como dueño no lo exige, pero el team debe tener Read sobre la tabla (spike roles, matriz §1.2). Ningún rol humano tiene Assign.

**`PropietarioAlCrearStep`** — Create · PreOperation · síncrono · orden 5 · sin filtro · sin imagen, en cada una de las 7 tablas.
1. Si `EsEscrituraDelSistema` y el `Target` ya trae `ownerid` de tipo `team`: terminar (atajo, DC-20; el calendario crea hasta 60 cuotas y no debe consultar la BU 60 veces).
2. Si no: resolver el team del área (0.4) y poner `Target["ownerid"] = EntityReference("team", teamId)`, **reemplazando** cualquier valor (el creador, otro team).

**`PropietarioAlActualizarStep`** — Update · PreOperation · síncrono · orden 5 · filtro `ownerid` · sin imagen, en las 7 tablas.
1. Si `EsEscrituraDelSistema`: terminar.
2. Si `Target["ownerid"]` no es el team del área ⇒ `CARP-PRO-001`.

| Código | Mensaje |
|---|---|
| `CARP-PRO-001` | Los registros de esta solución pertenecen siempre al equipo del área. No se puede cambiar el propietario. |

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-PRO-01 | Alta de Cliente por un Oficial sin `ownerid` | Create | `Target.ownerid` = team del área (EntityReference `team`) |
| PA-PRO-02 | Alta con `ownerid` = el propio Oficial; y con `ownerid` = otro team | Create | Se reemplaza por el team del área |
| PA-PRO-03 | Las 7 tablas (una prueba parametrizada) | Create | Todas quedan con el team del área |
| PA-PRO-04 | Alta por el sistema con `ownerid` = team | Create | No se consulta BU ni team (el doble cuenta 0 consultas de BU); `ownerid` igual |
| PA-PRO-05 | Alta por el sistema sin `ownerid` | Create | Se fija el team |
| PA-PRO-06 | Update con `ownerid` = el team | Update | Pasa |
| PA-PRO-07 | Update de un Oficial con `ownerid` = un usuario o un team ajeno | Update | `CARP-PRO-001` |
| PA-PRO-08 | Update del sistema con `ownerid` ajeno | Update | Pasa (DC-03) |
| PA-PRO-09 | La BU no existe, o hay dos | Create | `CARP-CFG-001`; el alta falla |
| PA-PRO-10 | Dos teams `isdefault` en la BU | Create | `CARP-CFG-002` |
| PA-PRO-11 | La BU tiene un team llamado igual que el team de jefatura de otra BU | Create | El team del área es el `isdefault` de **la BU configurada** |

### 2.2 Cliente (inv. 7.13; D-07, P-05, P-06)

**`ClienteStep`** — registrado dos veces: Create · PreOperation · síncrono · orden 10 · sin imagen; Update · PreOperation · síncrono · orden 10 · filtro `sanic_nombrecompleto,sanic_cedula,sanic_correo,sanic_celular,statecode,statuscode` · pre-imagen `PreImagen` (`statecode`, `statuscode`). ~~Update: filtro `sanic_nombrecompleto,sanic_cedula,sanic_correo,sanic_celular`, sin imagen.~~ **Modificado el 2026-10-11 (D-40, decisión del usuario):** el filtro suma `statecode` y `statuscode` y el Update lleva pre-imagen con esas dos columnas, para aplicar la regla del estado nativo (paso 0 del Update). No hace falta un step nuevo: es la misma clase y el mismo registro S-16 (siguen 35 registros y 20 clases). La pre-imagen no lleva la cédula: sigue sin pre-imagen de columnas protegidas y la cédula guardada se lee con SYSTEM (paso 2 del Update, DC-28). `statecode` y `statuscode` no tienen seguridad por columna, así que la pre-imagen los trae completos para cualquier llamador.

Create:
1. Normalizar nombre completo, cédula, correo, celular (2.0.1) y escribir los valores normalizados en el `Target`.
2. Nombre completo vacío ⇒ `CARP-CLI-001`. Cédula vacía ⇒ `CARP-CLI-002` (la unicidad necesita que exista, P-05).
3. `sanic_nombre` = nombre completo normalizado (primaria calculada, dicc. §11.2).
4. Unicidad de la cédula con servicio **SYSTEM** (DC-22): `Cliente` con esa cédula, **activo o inactivo**, ⇒ `CARP-CLI-003`. La consulta con el servicio del llamador no sirve: sin el perfil de columna la cédula llega vacía y el duplicado pasaría en silencio (spike 4, tarea A).

Update (solo las columnas presentes en el `Target`):
0. **Estado nativo (decisión del usuario, 2026-10-11, D-40; misma regla que Cuota, Versión y Mapeo).** Si el `Target` trae `statecode` o `statuscode` con un valor distinto del de la pre-imagen (DC-01; reenviar el mismo valor no es un cambio) y el actor no es del sistema ⇒ `CARP-CLI-004`. Es lo primero que se evalúa: gana sobre las demás reglas del Update y rechaza el guardado completo, aunque traiga también otras columnas. Se consulta la identidad solo si hubo un cambio de estado (camino barato). Para SYSTEM (`EsEscrituraDelSistema`, DC-03) no hay regla de estado. Ningún proceso de este diseño cambia el estado de un Cliente: la purga elimina clientes con `Delete` (BulkDelete) y no pasa por este step (no hay step de Delete sobre Cliente), de modo que no se ve afectada.
1. Normalizar las presentes. Si trae nombre completo: vacío ⇒ `CARP-CLI-001`; si cambió, recalcular `sanic_nombre`.
2. Si trae cédula: vacía ⇒ `CARP-CLI-002`; unicidad igual que en Create **excluyendo el propio registro** ⇒ `CARP-CLI-003`. Si la cédula normalizada no cambió, no se consulta. Para saber si cambió, el step compara con la cédula guardada, que **lee con el servicio de SYSTEM** (la pre-imagen del step no incluye la cédula y, sin el perfil de columna, el llamador la recibiría ausente o vacía: V-6, DC-28, decisión del 2026-10-11).

Vincular conversaciones al cliente **no** lo hace este step: lo hace la entrada de correo al crear la conversación (2.10) y el oficial a mano (def. L163). Ver Inconsistencias, punto 4.

| Código | Mensaje |
|---|---|
| `CARP-CLI-001` | El nombre completo del cliente es obligatorio. |
| `CARP-CLI-002` | La cédula del cliente es obligatoria. |
| `CARP-CLI-003` | Ya existe un cliente con esa cédula. |
| `CARP-CLI-004` | Un cliente no se activa ni se desactiva a mano: lo gestiona el sistema. |

`CARP-CLI-004` es nuevo (decisión del usuario, 2026-10-11). Ningún mensaje existente encaja exacto: `CARP-VER-014` y `CARP-CUO-001` nombran a otra tabla.

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-CLI-01 | Alta con `" Ana  Pérez "`, cédula `" 001-000000-0001a"`, correo `"ANA@X.COM "`, celular `" 8888-0000"` | Create | Target: nombre `"Ana Pérez"`, cédula `"001-000000-0001A"`, correo `"ana@x.com"`, celular `"8888-0000"`, `sanic_nombre = "Ana Pérez"` |
| PA-CLI-02 | Nombre vacío; cédula vacía | Create | `CARP-CLI-001` y `CARP-CLI-002` |
| PA-CLI-03 | Existe un cliente con cédula `001-000000-0001A` | Alta con `001-000000-0001a ` | `CARP-CLI-003`; el mensaje no nombra al otro cliente |
| PA-CLI-04 | El duplicado está inactivo | Alta | `CARP-CLI-003` |
| PA-CLI-05 | El doble simula seguridad por columna: para el llamador la cédula del existente llega `null`, para SYSTEM no | Alta con la misma cédula | `CARP-CLI-003` (la consulta usó el servicio de SYSTEM) |
| PA-CLI-06 | Alta sin correo ni celular | Create | Pasa; esas columnas quedan sin valor |
| PA-CLI-07 | Update de la cédula a la de otro cliente | Update | `CARP-CLI-003` |
| PA-CLI-08 | Update que reescribe la propia cédula con otro formato (`…0001a` vs `…0001A`) | Update | Pasa sin consulta de unicidad (no cambió) |
| PA-CLI-09 | Update del nombre completo | Update | Se recalcula `sanic_nombre` |
| PA-CLI-10 | Update del celular | Update | No se consulta la unicidad |
| PA-CLI-11 | Update con cédula vacía | Update | `CARP-CLI-002` |
| PA-CLI-12 | Llamador **sin** el perfil de columna (el doble oculta la cédula al llamador, no a SYSTEM); el cliente tiene la cédula `001-000000-0001A` | Update que reenvía `001-000000-0001a` | No es un cambio (DC-28): pasa sin consulta de unicidad; la cédula guardada se leyó con el servicio de SYSTEM |
| PA-CLI-13 | Mismo caso sin perfil, pero el Update trae la cédula de **otro** cliente | Update | Es un cambio: unicidad con SYSTEM ⇒ `CARP-CLI-003` |
| PA-CLI-14 | Cliente activo (V-2: la plataforma no bloquea la desactivación; decisión del 2026-10-11) | Un Oficial, por PATCH directo, lo desactiva con **solo** `statecode = 1` (botón nativo) y, en otra llamada, con `statecode = 1` y `statuscode = 2` | `CARP-CLI-004` en ambos; el cliente sigue activo |
| PA-CLI-15 | Cliente inactivo | Un Oficial lo reactiva (`statecode = 0`, `statuscode = 1`) | `CARP-CLI-004` |
| PA-CLI-16 | Cliente activo | Un Oficial desactiva y cambia el celular en el mismo guardado | `CARP-CLI-004` (gana el paso 0; el celular no se normaliza ni se escribe) |
| PA-CLI-17 | Cliente activo y otro inactivo | El sistema (`InitiatingUserId` = SYSTEM) cambia `statecode` y `statuscode` | Pasa sin modificar el `Target` |
| PA-CLI-18 | Cliente activo | Un Oficial reenvía `statecode = 0` y `statuscode = 1` con los mismos valores de la pre-imagen, junto con un cambio de celular | Pasa: el estado no es un cambio (DC-01); 0 llamadas a `WhoAmI` |
| PA-CLI-19 | Cadena (pipeline simulado) con los steps de Cliente y la cuenta de purga | `Delete` de un Cliente | Pasa: no corre ningún step; no hay step de Delete sobre Cliente (la purga no se ve afectada) |

### 2.3 Conversación: columnas solo-sistema y gestos Finalizar y Reabrir (inv. 7.12; D-30; def. ítem 6)

**`ConversacionActualizarStep`** — Update · PreOperation · síncrono · orden 10 · **sin filtro** (a propósito: filtrar dejaría pasar justo las columnas que nadie declaró, M-PPP §5.1) · pre-imagen `PreImagen` con: `statecode`, `statuscode`, `sanic_huellamessageid`, `sanic_fechaprimercorreo`, `sanic_asunto`, `sanic_remitente`, `sanic_sinrespuestadesde`, `sanic_correonuevoenfinalizada`, `sanic_resultado`, `sanic_motivosinrespuesta`, `sanic_detallesinrespuesta`, `sanic_fechafinalizacion`, `sanic_tiempoprimerarespuesta`, `sanic_tiempototal`, `sanic_cantidadrespuestas`, `sanic_unidaaid`, `sanic_finalizadoporid`, y desde el 2026-10-11 `sanic_clienteid`, `sanic_nombre`, `sanic_oficialasignadoid` y `ownerid` (decisión del usuario, 2026-10-11: sin ellas DC-01 no puede decir si una columna libre cambió en una conversación inactiva). Son 21 columnas.

Algoritmo (esquema común de los steps de gestos):
1. Calcular las columnas vigiladas que cambiaron (solo-sistema, de gesto, `statecode`, `statuscode`), con DC-01 y el estado efectivo de DC-06. **Si la pre-imagen es Finalizada o Unida, cuentan además `sanic_clienteid`, `sanic_nombre`, `sanic_oficialasignadoid` y `ownerid`** (2026-10-11). Ninguna ⇒ terminar. En una conversación Abierta esas cuatro siguen sin vigilancia aquí (camino barato, PA-CNV-15).
2. `EsEscrituraDelSistema` ⇒ terminar (DC-03). Así pasan las escrituras del sistema sobre una Finalizada o una Unida (el indicador de RF-15, iniciar conversación nueva, el traslado de cliente y oficial al unir).
3. **Conversación inactiva (decisión del usuario, 2026-10-11; mismo criterio que el paso 1 de 2.6: gana el primer paso que corresponde).** Si la pre-imagen es Finalizada o Unida y el `Target` cambió cualquier columna distinta de `statecode` y `statuscode` ⇒ `CARP-CNV-009`, salvo que sea el gesto **Reabrir** (pre-imagen Finalizada y estado efectivo Abierta), que pasa al paso 3 bis. ~~Reabrir seguía al paso 4 con las reglas de siempre (el gesto no gana restricciones nuevas): un cambio de cliente, de nombre, de oficial o de propietario dentro del mismo guardado pasaba.~~ Reemplazado el 2026-10-11 por el paso 3 bis (D-40). Este paso va antes que `CARP-CNV-001` y `CARP-CNV-005`: un cambio de columnas en una Finalizada o Unida da `CARP-CNV-009` aunque la columna sea solo-sistema o de gesto. Un cambio de estado sin otras columnas no cae aquí: lo resuelve el paso 5, con sus códigos de siempre (`CARP-CNV-002`, `CARP-CNV-006`).
3 bis. **Reabrir es puro (decisión del usuario, 2026-10-11, D-40).** En el gesto Reabrir el `Target` admite solo el cambio de estado (`statecode`, `statuscode`); el gesto no tiene columnas de motivo (el resultado, el motivo y el detalle de la finalización los limpia el sistema, paso 6) y las columnas que completa el sistema (`sanic_resultado`, `sanic_motivosinrespuesta`, `sanic_detallesinrespuesta`, `sanic_fechafinalizacion`, `sanic_finalizadoporid`, `sanic_tiempototal`, `sanic_correonuevoenfinalizada`) las escribe el sistema en el `Target` después de evaluar, no la persona. Si el `Target` trae además un cambio real (DC-01: presente y distinto del valor de la pre-imagen) de `sanic_clienteid`, `sanic_nombre`, `sanic_oficialasignadoid` u `ownerid` ⇒ `CARP-CNV-010` (nombra las columnas). Reenviar el mismo valor no es un cambio. Las demás columnas siguen con sus códigos: una solo-sistema, `CARP-CNV-001` (paso 4); el resultado, el motivo o el detalle, `CARP-CNV-005` (paso 5). Gana el primer paso: si vienen a la vez un cambio de cliente y uno de `sanic_resultado`, responde `CARP-CNV-010`. Si el actor es SYSTEM el step ya terminó en el paso 2.
4. Si cambió alguna solo-sistema ⇒ `CARP-CNV-001` (nombra las columnas). Un usuario no puede fijar `finalizadoporid`, `fechafinalizacion` ni los tiempos ni siquiera dentro de un gesto.
5. Estado: sea `de` el de la pre-imagen y `a` el efectivo.
   - `Abierta → Finalizada`: **Finalizar**. Exige `sanic_resultado` ⇒ `CARP-CNV-003`. Si es `SinRespuesta`, exige motivo y detalle no vacíos (recortados) ⇒ `CARP-CNV-004`. Si es otro resultado, se ponen en `null` motivo y detalle del `Target`.
   - `Finalizada → Abierta`: **Reabrir**. Si el `Target` trae resultado, motivo o detalle con valor distinto del actual ⇒ `CARP-CNV-005`.
   - `* → Unida` ⇒ `CARP-CNV-006` (solo la API de unir).
   - `Unida → *` ⇒ `CARP-CNV-002`.
   - Cualquier otra combinación con cambio de estado ⇒ `CARP-CNV-002`.
   - Sin cambio de estado pero con resultado, motivo o detalle cambiados ⇒ `CARP-CNV-005` (en una Abierta; en una Finalizada o Unida ya lo capturó el paso 3).
6. Efectos escritos en el `Target`:
   - **Finalizar**: `sanic_finalizadoporid = InitiatingUserId`; `sanic_fechafinalizacion = ahora`; `sanic_tiempototal = MinutosEntre(sanic_fechaprimercorreo, ahora)` (si la fecha del primer correo es nula, no se escribe); `sanic_sinrespuestadesde = null` (DC-10); `sanic_correonuevoenfinalizada` queda como está.
   - **Reabrir**: `sanic_resultado`, `sanic_motivosinrespuesta`, `sanic_detallesinrespuesta`, `sanic_fechafinalizacion`, `sanic_finalizadoporid`, `sanic_tiempototal` = `null`; `sanic_correonuevoenfinalizada = no`. **No** se toca `sanic_sinrespuestadesde`: si había un correo nuevo sin responder, sigue pendiente en la conversación ya abierta (DC-10; Inconsistencias, punto 3). `sanic_tiempoprimerarespuesta` y `sanic_cantidadrespuestas` no cambian.

Quién: cualquier usuario del área con Write sobre Conversación (Oficial; el Jefe también). El step no pide jefatura. En una conversación Finalizada o Unida no hay ninguna otra excepción para la jefatura: lo único que una persona puede hacer es Reabrir una Finalizada, y solo eso, sin otros cambios en el mismo guardado (`CARP-CNV-010`, paso 3 bis) (V-2: la plataforma no bloquea esas escrituras, así que estos pasos son la única barrera). Una Unida no se puede reabrir ni modificar.

| Código | Mensaje |
|---|---|
| `CARP-CNV-001` | Estas columnas las gestiona el sistema y no se pueden modificar: {columnas}. |
| `CARP-CNV-002` | Esa transición de estado no está permitida. |
| `CARP-CNV-003` | Para finalizar la conversación hay que indicar el resultado. |
| `CARP-CNV-004` | Si el resultado es "Sin respuesta", el motivo y el detalle son obligatorios. |
| `CARP-CNV-005` | El resultado, el motivo y el detalle solo se registran al finalizar la conversación. |
| `CARP-CNV-006` | Una conversación solo pasa a Unida con la acción "Unir conversaciones". |
| `CARP-CNV-008` | Solo la jefatura puede reasignar una conversación o dejarla sin asignar. (Ver 2.4.) |
| `CARP-CNV-009` | Una conversación finalizada o unida no se puede modificar. Para cambiar una finalizada, reábrala primero. |
| `CARP-CNV-010` | Al reabrir una conversación no se puede cambiar nada más en el mismo guardado: {columnas}. Reabra la conversación, guarde, y haga el otro cambio después. |

`CARP-CNV-009` es nuevo (decisión del usuario, 2026-10-11). El número 007 no está definido en ninguna fuente y no se reutiliza. El texto no ofrece reabrir una Unida porque una Unida no se reabre (`CARP-CNV-002`). `CARP-CNV-010` es nuevo (decisión del usuario, 2026-10-11, D-40): ningún mensaje de Conversación encaja exacto (`CARP-CNV-009` dice que no se puede modificar y manda a reabrir, justo lo que la persona está haciendo; `CARP-CNV-005` es del resultado, el motivo y el detalle). `CARP-ARR-012` sí encaja exacto para Arreglo (2.6).

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-CNV-01 | Abierta, `fechaprimercorreo` 10:00; `ahora` 12:30; actor Oficial | `statecode=1, statuscode=2, resultado=ConArregloDePago` | Target recibe `finalizadoporid = Oficial`, `fechafinalizacion = 12:30`, `tiempototal = 150`, `sinrespuestadesde = null`; motivo y detalle `null` |
| PA-CNV-02 | Igual, solo con `statecode=1` | Update | Se trata como Finalizada (DC-06) con los mismos efectos |
| PA-CNV-03 | Finalizar sin resultado | Update | `CARP-CNV-003` |
| PA-CNV-04 | Resultado Sin respuesta sin motivo; con motivo pero detalle `"   "` | Update | `CARP-CNV-004` en ambos |
| PA-CNV-05 | Resultado Sin respuesta, motivo Spam, detalle "correo masivo" | Update | Pasa; conserva motivo y detalle |
| PA-CNV-06 | Resultado SinArregloDePago con motivo Spam presente | Update | Target con motivo y detalle `null` |
| PA-CNV-07 | Abierta con `sinrespuestadesde` 09:00 | Finalizar | `sinrespuestadesde = null` |
| PA-CNV-08 | Finalizada con resultado, motivo, detalle, fecha, finalizador, tiempo y `sinrespuestadesde` 11:00 | `statecode=0, statuscode=1` | Esas seis columnas pasan a `null` y el indicador a `no`; `sinrespuestadesde` queda 11:00 |
| PA-CNV-09 | Reabrir con `resultado` distinto en el Target | Update | `CARP-CNV-005` |
| PA-CNV-10 | Un Oficial manda `statecode=1, statuscode=159460001` | Update | `CARP-CNV-006` |
| PA-CNV-11 | Unida → Abierta; Unida → Finalizada | Update | `CARP-CNV-002` |
| PA-CNV-12 | Abierta sin cambio de estado, `resultado=ConArregloDePago` | Update | `CARP-CNV-005` |
| PA-CNV-13 | Conversación **Abierta** (aclarado el 2026-10-11: en una Finalizada o Unida gana `CARP-CNV-009`); una prueba parametrizada por cada columna solo-sistema (12) con valor distinto | Update de un Oficial | `CARP-CNV-001` nombrando esa columna |
| PA-CNV-14 | El Target trae `sanic_cantidadrespuestas` con el mismo valor de la pre-imagen | Update | Pasa (no es un cambio, DC-01) |
| PA-CNV-15 | Conversación **Abierta** (aclarado el 2026-10-11) | Update solo de `sanic_clienteid` | Pasa y **no** se consulta identidad (0 llamadas a `WhoAmI`) |
| PA-CNV-16 | Update del sistema (`InitiatingUserId` = SYSTEM) que cambia columnas solo-sistema y pasa a Unida | Update | Pasa sin modificar el `Target` |
| PA-CNV-17 | Conversación **Abierta** (aclarado el 2026-10-11); update de la cuenta de servicio (un usuario licenciado que no es SYSTEM) que cambia `sinrespuestadesde` (OA-12) | Update | `CARP-CNV-001`: no se reconoce ninguna cuenta de servicio |
| PA-CNV-18 | Conversación **Abierta** (aclarado el 2026-10-11); update de otro usuario que no es SYSTEM (por ejemplo, un usuario de aplicación) que cambia `sinrespuestadesde` (OA-12) | Update | Se trata como persona: `CARP-CNV-001`. Solo SYSTEM escribe columnas solo-sistema (DC-02) |
| PA-CNV-19 | Finalizar con `fechaprimercorreo` nula | Update | No se escribe `tiempototal`; el resto sí |
| PA-CNV-20 | Finalizar y Reabrir hechos por un Oficial y por un Jefe | Update | Ambos pasan |
| PA-CNV-21 | `UserId` ≠ `InitiatingUserId` | Finalizar | `finalizadoporid = InitiatingUserId` |
| PA-CNV-22 | Un Oficial intenta escribir `finalizadoporid` y `fechafinalizacion` dentro de Finalizar | Update | `CARP-CNV-001` |
| PA-CNV-23 | Finalizar | Update | Como máximo 1 `WhoAmI` y ninguna consulta de BU |
| PA-CNV-24 | Finalizada con resultado, fecha, finalizador y tiempo (V-5, PATCH directo de un Oficial) | Update con **solo** `statecode = 0` (sin `statuscode`) | Estado efectivo Abierta (DC-06): es Reabrir y tiene los mismos efectos que PA-CNV-08 |
| PA-CNV-25 | Conversación **Finalizada** (fila inactiva; V-2: la plataforma no la bloquea) | Un Oficial, por PATCH directo, cambia `sanic_resultado` sin cambio de estado; en otro caso, `sanic_tiempototal` | ~~`CARP-CNV-005`; `CARP-CNV-001`~~ `CARP-CNV-009` en ambos casos (modificada el 2026-10-11: en una conversación inactiva gana el paso 3 de 2.3, antes que `-005` y `-001`) |
| PA-CNV-26 | Abierta, y otra Finalizada (V-1) | Un `SetStateRequest` de una persona llega como `Update` anidado, `Depth = 2`, con `statecode = 1`, `statuscode = 2` y `sanic_resultado` (primer caso); con `statecode = 0` y `statuscode = 1` (segundo) | Se evalúa igual que a profundidad 1: Finalizar y Reabrir con sus efectos; el resultado no depende de `Depth` ni del mensaje padre |
| PA-CNV-27 | Conversación **Finalizada**, y otra **Unida** (V-2: la plataforma no las bloquea; decisión del 2026-10-11) | Un Oficial, por PATCH directo, cambia `sanic_clienteid` a otro cliente (y, en otro caso, lo vacía); y cambia `sanic_nombre` | `CARP-CNV-009` en los seis casos; el `Target` no se modifica |
| PA-CNV-28 | Conversación **Unida**, y otra **Finalizada** (pipeline simulado: S-17 y S-18) | Un Oficial se asigna a sí mismo desde vacío; asigna a otro; la deja vacía; y un Jefe la reasigna a B | `CARP-CNV-009` en los ocho casos; S-18 no llega a evaluar la jefatura (ni `CARP-CNV-008`) |
| PA-CNV-29 | Conversación **Finalizada** con resultado, fecha, finalizador y tiempo | Un Oficial y un Jefe la reabren por el gesto: `statecode = 0` y `statuscode = 1`; y, en otro caso, solo `statecode = 0` (V-5) | Pasa: el paso 3 de 2.3 no se activa y se aplican los efectos de PA-CNV-08. Es el único cambio que una persona puede hacer sobre una Finalizada |
| PA-CNV-30 | Conversación **Finalizada**, y otra **Unida** (RF-15, unir, iniciar conversación nueva) | El sistema (`InitiatingUserId` = SYSTEM) pone `sanic_correonuevoenfinalizada = sí` y `sanic_sinrespuestadesde`; limpia ambos (iniciar nueva); y cambia `sanic_clienteid` y `sanic_oficialasignadoid` | Pasa sin modificar el `Target` (DC-03); no se consulta jefatura |
| PA-CNV-31 | Conversación **Finalizada**, y otra **Unida** | Un Oficial reenvía el mismo valor de `sanic_clienteid`, `sanic_nombre`, `sanic_oficialasignadoid` y `ownerid` (el team) que tiene la pre-imagen | Pasa: no es un cambio (DC-01); 0 llamadas a `WhoAmI` |
| PA-CNV-32 | Conversación **Finalizada** con cliente A (D-40, 2026-10-11) | Un Oficial reabre (`statecode = 0`, `statuscode = 1`) y cambia `sanic_clienteid` a B en el mismo guardado; en otro caso, lo vacía | `CARP-CNV-010` nombrando `sanic_clienteid` en ambos; el `Target` no se modifica y la conversación sigue Finalizada |
| PA-CNV-33 | Conversación **Finalizada** con oficial A (D-40) | Un Oficial reabre y cambia `sanic_oficialasignadoid` a sí mismo; y un Jefe reabre y la reasigna a B | `CARP-CNV-010` nombrando `sanic_oficialasignadoid` en ambos; `ConversacionAsignacionStep` no llega a evaluar la jefatura (ni `CARP-CNV-008`) |
| PA-CNV-34 | Conversación **Finalizada** (D-40) | Un Oficial reabre y cambia `sanic_nombre`; en otro caso, reabre y cambia `ownerid` a un team ajeno | `CARP-CNV-010` nombrando la columna en el primero; en el segundo responde `CARP-PRO-001` (el step de propietario, orden 5, corre antes) |
| PA-CNV-35 | Conversación **Finalizada** con resultado, fecha, finalizador y tiempo (D-40) | Un Oficial reabre solo con el cambio de estado (`statecode = 0`, `statuscode = 1`) | Pasa con los efectos de PA-CNV-08. El gesto no tiene columnas de motivo |
| PA-CNV-36 | Conversación **Finalizada** con cliente A, oficial B y `ownerid` el team (D-40) | Un Oficial reabre y reenvía el mismo `sanic_clienteid`, `sanic_oficialasignadoid`, `sanic_nombre` y `ownerid` | Pasa: no es un cambio (DC-01); los efectos de PA-CNV-08 |
| PA-CNV-37 | Conversación **Finalizada** (D-40) | Un Oficial reabre y, a la vez, cambia `sanic_clienteid` y `sanic_resultado` | `CARP-CNV-010` (gana el primer paso: el 3 bis va antes que el 5, `CARP-CNV-005`) |
| PA-CNV-38 | Conversación **Finalizada** (D-40) | El sistema (`InitiatingUserId` = SYSTEM) reabre y cambia `sanic_clienteid` en el mismo guardado | Pasa sin modificar el `Target` (DC-03) |

### 2.4 Conversación: asignación (inv. 7.12; D-04; def. ítem 2)

**`ConversacionAsignacionStep`** — Update · PreOperation · síncrono · orden 20 · filtro `sanic_oficialasignadoid` · pre-imagen `PreImagen` con `sanic_oficialasignadoid`.

1. Valor nuevo igual al actual ⇒ terminar.
2. `EsEscrituraDelSistema` ⇒ terminar (la unión traslada el oficial al destino, 1.4).
3. Actual vacío y nuevo = `InitiatingUserId` ("Tomar") ⇒ pasa, sin consultar jefatura.
4. Cualquier otro cambio (de uno a otro, a un tercero, a vacío) exige `EsJefatura()` ⇒ si no, `CARP-CNV-008`.

En una conversación Finalizada o Unida este step no llega a evaluar el cambio de una persona: `ConversacionActualizarStep` (orden 10) lo rechaza antes con `CARP-CNV-009` (2.3, paso 3; decisión del 2026-10-11). Tampoco la jefatura puede reasignar una inactiva. ~~Si el mismo `Update` es el gesto Reabrir, la asignación se evalúa con las reglas de arriba.~~ **Modificado el 2026-10-11 (D-40, Reabrir es puro):** si el mismo `Update` es el gesto Reabrir y cambia el oficial asignado, `ConversacionActualizarStep` lo rechaza antes con `CARP-CNV-010` (2.3, paso 3 bis); este step solo evalúa el cambio de oficial de una persona cuando la conversación está Abierta.

La pertenencia al team de jefatura se consulta en `teammembership` (dicc. de supuestos: confianza media; plan B team de pertenencia manual, def. L502).

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-ASG-01 | Sin oficial | El Oficial A se asigna a sí mismo | Pasa; 0 consultas de jefatura |
| PA-ASG-02 | Sin oficial | El Oficial A asigna a B | `CARP-CNV-008` |
| PA-ASG-03 | Sin oficial | Un Jefe (miembro de Jefatura) asigna a B | Pasa |
| PA-ASG-04 | Asignada a A | B (Oficial) se asigna; y el Jefe reasigna a B | `CARP-CNV-008` y pasa, respectivamente |
| PA-ASG-05 | Asignada a A | Un Oficial la deja vacía; un Jefe la deja vacía | `CARP-CNV-008` y pasa |
| PA-ASG-06 | Asignada a A | Update con A otra vez | Pasa |
| PA-ASG-07 | Unir (sistema) asigna el oficial del origen al destino vacío | Update | Pasa |
| PA-ASG-08 | Un usuario está en un team llamado como el de jefatura pero de otra BU | Reasignar | `CARP-CNV-008` (el team se busca dentro de la BU configurada) |
| PA-ASG-09 | Dos cambios en la misma ejecución que consultan jefatura | Evaluar | Una sola consulta de pertenencia |
| PA-ASG-10 | El team de jefatura no existe en la BU | Reasignar | `CARP-CFG-003` |

### 2.5 Arreglo: alta y calendario de cuotas (inv. 7.14; def. ítems 7 y 8; D-08, D-33, D-36)

#### `ArregloCrearStep` — Create · PreOperation · síncrono · orden 10 · sin filtro · sin imagen

Un `EsEscrituraDelSistema` termina el step sin reglas (DC-03). Para una persona:

1. Estado: si el `Target` trae `statecode`/`statuscode` y el efectivo no es Borrador ⇒ `CARP-ARR-008`. El alta sin `statuscode` toma el motivo 1 = Borrador (hecho verificado, dicc. §3.2).
2. Si el `Target` trae alguna columna solo-sistema del Arreglo (2.0.8) **distinta de `sanic_nombre`** ⇒ `CARP-ARR-009` (nombra las columnas). El formulario no debe someter esas columnas; las que muestre calculadas son solo de pantalla. **`sanic_nombre` (el código autonumérico) se trata aparte** (V-8 refutó el supuesto anterior; decisión del usuario, 2026-10-11, D-40): la plataforma lo completa antes de PreValidation, así que en el `Create` la clave **siempre** está en el `Target`, con el autonumérico (`ARR-001002`) si la persona no lo envió (o envió `null` o vacío) y con el valor de la persona si lo envió, y ese valor se guarda tal cual. Por eso la presencia no distingue nada. La regla es: si el `Target` trae `sanic_nombre` con un valor que **no cumple** `^ARR-[0-9]{6,16}$` (comparación ordinal, sensible a mayúsculas) ⇒ `CARP-ARR-009` nombrando `sanic_nombre`; si lo trae con el formato, o no lo trae, o lo trae nulo, el alta sigue. El patrón es el del formato `ARR-{SEQNUM:6}` del playbook (`playbooks/tabla/sanic_carp_tbl_arreglo.md`): el número `6` es el mínimo de dígitos, rellenados con ceros, y el largo de la columna es 20, de modo que el patrón admite de 6 a 16 dígitos para cuando la secuencia pase de 999999 (el informe de V-8 no probó ese desborde: **por verificar en Dev**). ~~Supuesto sin verificar en Dev de que el `Target` solo trae `sanic_nombre` si lo envió el cliente; si lo trajera siempre, este paso rechazaría todas las altas y hay que escalarlo.~~ **Riesgo conocido aceptado por el usuario:** un `ARR-nnnnnn` falsificado por API pasa, porque en el `Create` es indistinguible del autonumérico de la plataforma (casos B0 y N5 de V-8); en el formulario la columna es de solo lectura y el número de negocio es `sanic_numeroarreglo`, protegido por la clave K-02. La decisión se toma en PreOperation, donde el valor está en el `Target`; no se quita ni se reemplaza el valor (hacerlo en PreOperation dejaría la primaria vacía, caso B2 de V-8). En el `Update` rige otra regla: cualquier `sanic_nombre` distinto del de la pre-imagen se rechaza, sin mirar el formato (2.6, paso 0). **Hallazgo lateral (V-8, N1 y N2):** un Oficial sin el perfil `CSP - CARP - Datos sensibles` recibe un 403 de la plataforma, después de PreOperation, al crear un Arreglo que envía `sanic_cuentadebitar` o `sanic_instantaneacedula`; es coherente con el perfil por columna y no requiere regla de plugin. (Con este step, `sanic_instantaneacedula` en un alta de una persona ya recibe `CARP-ARR-009` antes del 403; el 403 queda para `sanic_cuentadebitar`, que el Oficial sí envía al crear el borrador y que por eso debe estar en su perfil con permiso de creación.)
3. Obligatorias para poder calcular: `sanic_numeroproducto` (tras normalizar), `sanic_saldoinicial`, `sanic_montoabonado`, `sanic_cuotamensual`, `sanic_plazo`, `sanic_fechainicio` ⇒ si falta alguna, `CARP-ARR-006` con la lista. (Que el resto de las columnas "requeridas" las exige la app, no el servicio, dicc. §0.)
4. Normalizar `sanic_numeroproducto` (2.0.1) y escribirlo en el `Target`.
5. Versión: buscar con SYSTEM las versiones con `sanic_estado = Vigente` y `statecode = 0`. Ninguna ⇒ `CARP-ARR-001`. Más de una ⇒ `CARP-ARR-002`. Escribir `sanic_versionplantillaid` en el `Target`.
6. Plazo: debe estar entre 1 y 60 **y** no superar `sanic_capacidad` de esa versión ⇒ `CARP-ARR-003`.
7. Canal de origen: si `sanic_conversacionid` viene informado y `sanic_canalorigen` está vacío ⇒ poner `Correo` (DD-13). Sin conversación no se completa nada.
8. Montos: `ReglasDeMontos.Calcular` ⇒ errores `CARP-ARR-004/005/007`; si va bien, escribir `sanic_saldoremanente` y `sanic_totalpagar`.
9. `sanic_fechafin` = vencimiento de la última cuota, calculado con `GeneradorDeCalendario`. El plugin la **sobrescribe** aunque el `Target` traiga otra (OA-5, decidido el 2026-10-10).
10. `sanic_cantidadreaperturas = 0`.

No se valida si ya hay otro Borrador del mismo crédito: se permiten varios (decidido el 2026-10-10, OA-6, opción A); la clave K-02 admite varios con número vacío (spike 2).

#### `ArregloCalendarioStep` — Create · PostOperation · **síncrono** · orden 10 · sin filtro; y Update · PostOperation · síncrono · orden 10 · filtro `sanic_plazo,sanic_cuotamensual,sanic_fechainicio,sanic_saldoinicial,sanic_montoabonado`

Una sola clase, dos registros. Corre como SYSTEM (el Oficial no tiene Create ni Delete sobre Cuota, D-26).

1. Leer el arreglo (con SYSTEM): `sanic_nombre`, `statecode`, `statuscode`, `sanic_fechacongelamiento`, `sanic_plazo`, `sanic_cuotamensual`, `sanic_fechainicio`, `sanic_saldoinicial`, `sanic_montoabonado`, `ownerid`.
2. En **Update**: si el arreglo está inactivo o congelado ⇒ terminar (el pre-update ya lo rechazó si lo cambiaron un usuario). Si alguna cuota tiene `sanic_fechapago` ⇒ `CARP-ARR-021` (defensa; el pre-update ya lo valida).
3. Recalcular montos con `ReglasDeMontos` y el calendario con `GeneradorDeCalendario`.
4. En **Update**: borrar las cuotas existentes (consulta con `ColumnSet(false)`, un `Delete` por cuota).
5. Crear las cuotas con `Create` **individual** (DC-20, `rendimiento.md`): `sanic_nombre = <sanic_nombre del arreglo>-C<NN>` (NN de dos dígitos, `ARR-000123-C07`), `sanic_numero`, `sanic_fechavencimiento`, `sanic_monto`, `sanic_arregloid`, `ownerid` = el propietario del arreglo. `sanic_fechapago` y `sanic_montopagado` vacíos.
6. Cualquier fallo revierte la transacción, incluido el arreglo (def. ítem 8: "un arreglo sin calendario no puede existir").

El tiempo de 60 altas individuales dentro de la misma transacción se mide en Dev antes de dar el step por bueno (límite de 2 minutos, BP-PP-053).

| Código | Mensaje |
|---|---|
| `CARP-ARR-001` | No hay una versión vigente de la plantilla. Publique una versión antes de registrar arreglos. |
| `CARP-ARR-002` | Hay más de una versión vigente de la plantilla. Avise al equipo de soporte. |
| `CARP-ARR-003` | El plazo debe estar entre 1 y {máximo} cuotas (la plantilla vigente admite {capacidad}). |
| `CARP-ARR-004` | El monto abonado no puede ser mayor que el saldo inicial. |
| `CARP-ARR-005` | Con una cuota menos ya se cubre el saldo remanente. El saldo se cubre con {n} cuotas. (Si no hay saldo: "El saldo remanente es cero; no hay cuotas que pagar.") |
| `CARP-ARR-006` | Faltan datos para calcular el arreglo: {columnas}. |
| `CARP-ARR-007` | La cuota mensual debe ser mayor que cero. |
| `CARP-ARR-008` | Un arreglo siempre se registra en estado Borrador. |
| `CARP-ARR-009` | Estas columnas las gestiona el sistema y no se pueden modificar: {columnas}. |

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-ARR-01 | Versión Vigente con capacidad 60; alta de un Oficial con conversación, sin canal, número `" 5412 7500 "`, inicial 1000, abonado 0, cuota 300, plazo 4, inicio 2026-11-05 | Create | Target: `versionplantillaid` = la Vigente, canal Correo, número `"54127500"`, remanente 1000, total 1000, `fechafin` 2027-02-05, `cantidadreaperturas` 0 |
| PA-ARR-02 | Igual, sin conversación y sin canal | Create | `canalorigen` no se completa |
| PA-ARR-03 | Con conversación y canal Telefono | Create | Conserva Telefono |
| PA-ARR-04 | Ninguna versión Vigente; luego dos | Create | `CARP-ARR-001`; `CARP-ARR-002` |
| PA-ARR-05 | Plazo 0, 61, y 13 con capacidad 12 | Create | `CARP-ARR-003` en los tres |
| PA-ARR-06 | Abonado 1500 con inicial 1000; cuota 0; cuota que cubre con una menos | Create | `CARP-ARR-004`; `-007`; `-005` con el N correcto |
| PA-ARR-07 | Falta el saldo inicial y la fecha de inicio | Create | `CARP-ARR-006` nombrando ambos |
| PA-ARR-08 | `statuscode = Vigente`; `statuscode = Borrador`; sin estado | Create | `CARP-ARR-008`; pasa; pasa |
| PA-ARR-09 | Una prueba parametrizada con cada columna solo-sistema **salvo `sanic_nombre`** (20; ~~21: se sumaba `sanic_nombre`, 2026-10-11~~ vuelve a 20 el mismo día por V-8: `sanic_nombre` tiene su propia regla de formato, `PA-ARR-62` a `-64`) con valor | Create de un Oficial | `CARP-ARR-009` nombrándola |
| PA-ARR-10 | Ya existe un Borrador del mismo crédito | Create | Pasa (OA-6: se permiten varios borradores) |
| PA-ARR-11 | Alta del sistema con columnas solo-sistema (incluido un `sanic_nombre` de cualquier formato) | Create | Pasa sin cambios en el `Target` |
| PA-CAL-01 | Arreglo recién creado `ARR-000123`, plazo 3, cuota 300, remanente 900, inicio 2026-11-05 | Post-create | 3 cuotas `ARR-000123-C01…C03`; números 1 a 3; fechas 11-05, 12-05, 01-05; montos 300; `arregloid`; `ownerid` = el del arreglo; pagos vacíos |
| PA-CAL-02 | Remanente 1000, cuota 300, plazo 4 | Post-create | Montos 300, 300, 300, 100 |
| PA-CAL-03 | Plazo 60 | Post-create | 60 cuotas, últimas `…-C59`, `…-C60`; 60 `Create` individuales y 0 `ExecuteMultiple`/`ExecuteTransaction` en el registro del doble |
| PA-CAL-04 | Borrador sin congelar con 6 cuotas; cambia el plazo a 4 | Post-update | Las 6 se borran y se crean 4 |
| PA-CAL-05 | Borrador con una cuota con `fechapago` | Post-update con cambio de cuota | `CARP-ARR-021` |
| PA-CAL-06 | Arreglo congelado | Post-update | No toca cuotas |
| PA-CAL-07 | Fallo inyectado al crear la cuota 5 | Post-create | La excepción sube; al restaurar la transacción del doble no queda el arreglo ni cuotas |
| PA-CAL-08 | Post-update cuyo `Target` no trae ninguna columna del filtro | Ejecutar | No hace nada |
| PA-CAL-09 | Escrituras | Post-create | Todas con el servicio de SYSTEM |

### 2.6 Arreglo: edición, congelamiento y gestos (inv. 7.14; D-10, D-25, D-30; def. ítems 9, 10, 12)

#### `ArregloActualizarStep` — Update · PreOperation · síncrono · orden 10 · **sin filtro** · pre-imagen `PreImagen`

Columnas de la pre-imagen (43; `sanic_nombre` se agregó el 2026-10-11, decisión del usuario): `statecode`, `statuscode`, `sanic_nombre`, `sanic_producto`, `sanic_numeroproducto`, `sanic_numeroarreglo`, `sanic_numeroprovisional`, `sanic_tipo`, `sanic_moneda`, `sanic_canalorigen`, `sanic_saldoinicial`, `sanic_montoabonado`, `sanic_saldoremanente`, `sanic_plazo`, `sanic_cuotamensual`, `sanic_totalpagar`, `sanic_fechainicio`, `sanic_fechafin`, `sanic_cuentadebitar`, `sanic_fechaformalizacion`, `sanic_comentarios`, `sanic_fechacongelamiento`, `sanic_instantaneanombre`, `sanic_instantaneacedula`, `sanic_instantaneacorreo`, `sanic_instantaneacelular`, `sanic_fechaformalizado`, `sanic_formalizadoporid`, `sanic_motivodescarte`, `sanic_fechadescarte`, `sanic_descartadoporid`, `sanic_motivoanulacion`, `sanic_fechaanulacion`, `sanic_anuladoporid`, `sanic_motivoreapertura`, `sanic_fechareapertura`, `sanic_reabiertoporid`, `sanic_cantidadreaperturas`, `sanic_fechatermino`, `sanic_versionplantillaid`, `sanic_clienteid`, `sanic_conversacionid`, `sanic_oficialid`. ~~(V-6: la pre-imagen trae también las columnas protegidas)~~ **V-6 (2026-10-11): solo las trae si el usuario con el que corre el step tiene el perfil de columna**; sin el perfil, `sanic_cuentadebitar` e `sanic_instantaneacedula` llegan ausentes (no nulas) y el step las lee con SYSTEM cuando el `Target` las trae (DC-28).

La lógica vive en `ReglasDeArreglo.EvaluarActualizacion(pedido)` (pura; los puertos `IConsecutivos` y `IJefatura` los implementa `Steps/`), que devuelve permitido/rechazado y las columnas a escribir.

**Paso 0.** Calcular las columnas cambiadas entre todas las categorías excepto "libres" (DC-01). `sanic_nombre` (el código interno autonumérico `ARR-000123`) es solo-sistema en todos los estados desde el 2026-10-11: su cambio por una persona se rechaza con `CARP-ARR-009` en Borrador y en Vigente (paso 4), y con `CARP-ARR-022` en un Terminado, Anulado o Descartado (paso 1); reenviar el mismo valor no es un cambio. **En el `Update` no hay ambigüedad (V-8, caso A6, 2026-10-11):** la plataforma nunca agrega `sanic_nombre` a un `Update`, así que si viene en el `Target` con un valor distinto de la pre-imagen lo mandó una persona; se rechaza sea cual sea su formato (un `ARR-777777` con la forma del autonumérico tampoco pasa), a diferencia del `Create` (2.5, paso 2), donde solo se rechaza el valor sin el formato. Si una columna con seguridad por columna (`sanic_cuentadebitar`, `sanic_instantaneacedula`) está en el `Target` y falta en la pre-imagen, se lee con el servicio de SYSTEM antes de comparar (DC-28, 2026-10-11); si la pre-imagen la trae, no hay consulta extra. **En un arreglo Terminado, Anulado o Descartado cuenta además como cambio la columna libre `sanic_conversacionid`** (V-2, 2026-10-11: la plataforma no bloquea las filas inactivas; el repunte al unir lo hace SYSTEM, 1.4). Ninguna ⇒ terminar. `EsEscrituraDelSistema` ⇒ terminar (DC-03; así pasan la generación del formato, el paso a Terminado y la unión, incluido su repunte de arreglos cerrados).

**Paso 1: estados inactivos.** Si el arreglo es Anulado o Descartado, cualquier cambio ⇒ `CARP-ARR-022` ("no se reactiva"; incluye un PATCH a Vigente o Borrador, que la plataforma aceptaría: V-2, V-5). Si es Terminado, solo se acepta el gesto Reabrir; todo lo demás ⇒ `CARP-ARR-022`. **Los pasos se evalúan en orden y gana el primero que corresponde** (inconsistencia 16, resuelta el 2026-10-11 por el arquitecto): para un Terminado, todo lo que no sea el gesto Reabrir da `CARP-ARR-022` aquí, también Terminado → Borrador y Terminado → Anulado; esas transiciones no llegan al paso 2.

**Paso 2: identificar el gesto** (DC-04, DC-05). Hay a lo sumo uno. Si hay dos ⇒ `CARP-ARR-012`.

| Gesto | Cómo se reconoce |
|---|---|
| Formalizar | estado efectivo `Vigente` y pre-estado `Borrador` |
| Descartar | estado efectivo `Descartado` y pre-estado `Borrador` |
| Anular | estado efectivo `Anulado` y pre-estado `Vigente` |
| Reabrir un Terminado | estado efectivo `Vigente` y pre-estado `Terminado`. El `Target` **debe traer `statecode = 0` y `statuscode = Vigente`** (V-5, 2026-10-11): con solo `statecode = 0` el estado efectivo es Borrador (DC-06) y es un Terminado → Borrador, no el gesto |
| Reabrir un Vigente congelado | `sanic_motivoreapertura` cambió y el estado no cambió |
| Desbloquear | `sanic_fechacongelamiento` pasa a `null` y el estado no cambió |
| Edición | ningún cambio de estado, ni motivos, ni desbloqueo |

Cualquier otro cambio de estado (por ejemplo Vigente→Terminado, Vigente→Borrador, Borrador→Anulado, Borrador→Terminado) ⇒ `CARP-ARR-010`. ~~Terminado→Anulado~~ (retirado de la lista el 2026-10-11, inconsistencia 16): las transiciones que salen de un Terminado, un Anulado o un Descartado ya las capturó el paso 1 con `CARP-ARR-022`, salvo el gesto Reabrir; a este paso solo llegan los estados de origen Borrador y Vigente (y el Terminado que reabre).

**Paso 3: reglas de cada gesto.** Cada gesto admite **solo** sus columnas (`statecode`, `statuscode` y su motivo); otra columna cambiada ⇒ `CARP-ARR-012`. **Reabrir es puro (decisión del usuario, 2026-10-11, D-40; vale para los dos gestos Reabrir).** Admite solo `statecode`, `statuscode` y `sanic_motivoreapertura`, más lo que el propio gesto completa el sistema (última columna de la tabla); cualquier otro cambio real (DC-01: presente y distinto) ⇒ `CARP-ARR-012` ("Esta acción no admite otros cambios en el mismo guardado: {columnas}"), que encaja exacto, así que no hay mensaje nuevo para Arreglo. Reabrir más un cambio de `sanic_clienteid` o de `sanic_oficialid` ya daba `CARP-ARR-012` (son datos del acuerdo y el paso 0 los cuenta); la brecha era `sanic_conversacionid`: es una columna libre y el paso 0 no la cuenta en un Vigente congelado, de modo que un Reabrir de Vigente congelado que la cambiaba pasaba. **Desde el 2026-10-11 en los dos gestos Reabrir `sanic_conversacionid` cuenta como cualquier otra columna** (en un Terminado ya contaba por el paso 0). Reenviar el mismo valor (por ejemplo el mismo cliente) no es un cambio y pasa. El actor es `InitiatingUserId`; `ahora` del reloj.

| Gesto | Precondiciones | Columnas que escribe el sistema en el `Target` |
|---|---|---|
| Formalizar | Congelado y con `numeroprovisional` ⇒ si no, `CARP-ARR-019`. Se recalcula `Siguiente(números del crédito)` (consulta con SYSTEM, todos los estados). Si es distinto del provisional ⇒ `CARP-ARR-020`. Si supera 9999 ⇒ `CARP-ARR-023` | `sanic_numeroarreglo = numeroprovisional`; `sanic_formalizadoporid = actor`; `sanic_fechaformalizado = ahora`. `sanic_numeroprovisional` se conserva (evidencia de lo impreso); `sanic_fechaformalizacion` (la impresa) no se toca (P-07) |
| Descartar | `sanic_motivodescarte` no vacío (recortado) ⇒ si no, `CARP-ARR-013`. Vale congelado o no | `sanic_descartadoporid`, `sanic_fechadescarte`, `sanic_fechatermino = ahora` |
| Anular | `sanic_motivoanulacion` no vacío ⇒ `CARP-ARR-014` | `sanic_anuladoporid`, `sanic_fechaanulacion`, `sanic_fechatermino = ahora` |
| Reabrir un Terminado | `sanic_motivoreapertura` no vacío ⇒ `CARP-ARR-015`; `EsJefatura()` ⇒ si no, `CARP-ARR-016` | ~~Estado a Vigente (si el `Target` no lo trae, `statecode = 0` y `statuscode = Vigente`)~~ (retirado el 2026-10-11, V-5: el estado Vigente lo trae el `Target`; el sistema no lo completa); `sanic_reabiertoporid = actor`; `sanic_fechareapertura = ahora`; `sanic_cantidadreaperturas = anterior + 1`; `sanic_fechatermino`, `sanic_fechacongelamiento`, las cuatro `sanic_instantanea*` y `sanic_numeroprovisional` = `null` |
| Reabrir un Vigente congelado | Pre-estado Vigente **congelado** ⇒ si no está congelado, `CARP-ARR-017`; motivo no vacío (`-015`); jefatura (`-016`) | Lo mismo, sin cambio de estado |
| Desbloquear | Pre-estado Borrador **congelado**. Vigente congelado ⇒ `CARP-ARR-018` (solo la jefatura reabre). Borrador sin congelar ⇒ `CARP-ARR-017`. Cualquier usuario del área | Las cuatro `sanic_instantanea*` y `sanic_numeroprovisional` = `null` (la fecha ya viene `null` del `Target`) |

La reapertura de un arreglo Terminado sobre un registro inactivo dependía de V-5: **confirmada el 2026-10-11**, con la condición de que el `Target` traiga `statecode` y `statuscode` (ver el reconocimiento del gesto).

**Paso 4: edición (sin gesto).**
1. Si cambió alguna columna solo-sistema ⇒ `CARP-ARR-009`. Si cambió un motivo (descarte, anulación, reapertura) ⇒ `CARP-ARR-024`.
2. Si el arreglo está congelado y cambió alguna columna de "datos del acuerdo" ⇒ `CARP-ARR-011` (nombra la columna). Un valor igual al de la pre-imagen no cuenta.
3. Normalizar `sanic_numeroproducto` si viene.
4. Con los valores efectivos (pre-imagen + `Target`): si cambió `saldoinicial`, `montoabonado`, `cuotamensual` o `plazo` ⇒ validar el plazo (`CARP-ARR-003`, la capacidad se lee de la versión del arreglo con SYSTEM) y recalcular montos (`CARP-ARR-004/005/007`), escribiendo `sanic_saldoremanente` y `sanic_totalpagar`.
5. Si cambió `plazo`, `fechainicio` o `fechafin`, recalcular y **sobrescribir** `sanic_fechafin` con el vencimiento de la última cuota (OA-5, decidido el 2026-10-10: el oficial no puede dejar una fecha de fin que contradiga el calendario).
6. Si cambió algo que altera el calendario (`plazo`, `cuotamensual`, `fechainicio`, `saldoinicial`, `montoabonado`) y existe una cuota con `sanic_fechapago` ⇒ `CARP-ARR-021` (def. ítem 8: solo se reemplaza "mientras no esté congelado ni tenga pagos").

| Código | Mensaje |
|---|---|
| `CARP-ARR-010` | Esa transición de estado no está permitida. |
| `CARP-ARR-011` | El arreglo está congelado: no se puede modificar {columna}. Si el formato no se ha firmado, desbloquéelo; si ya está vigente, solo la jefatura puede reabrirlo. |
| `CARP-ARR-012` | Esta acción no admite otros cambios en el mismo guardado: {columnas}. |
| `CARP-ARR-013` | Para descartar el borrador hay que indicar el motivo. |
| `CARP-ARR-014` | Para anular el arreglo hay que indicar el motivo. |
| `CARP-ARR-015` | Para reabrir el arreglo hay que indicar el motivo. |
| `CARP-ARR-016` | Solo la jefatura puede reabrir un arreglo. |
| `CARP-ARR-017` | El arreglo no está congelado: no hay nada que desbloquear o reabrir. |
| `CARP-ARR-018` | Un arreglo vigente no se desbloquea. Solo la jefatura puede reabrirlo. |
| `CARP-ARR-019` | Para formalizar, primero hay que generar el formato para firma. |
| `CARP-ARR-020` | Otro arreglo del mismo crédito se formalizó después de imprimir este formato. Desbloquee el borrador y vuelva a generar el formato. |
| `CARP-ARR-021` | No se puede cambiar el calendario porque el arreglo ya tiene pagos registrados. |
| `CARP-ARR-022` | Un arreglo terminado, anulado o descartado no se puede modificar. Un terminado solo se reabre por la jefatura. |
| `CARP-ARR-023` | Este crédito alcanzó el máximo de 9.999 arreglos. |
| `CARP-ARR-024` | El motivo solo se registra junto con su acción (descartar, anular o reabrir). |

La clave K-02 es la última barrera ante dos formalizaciones simultáneas: la plataforma rechaza la segunda (412 en el Web API, spike 2) y el oficial reintenta. Esa rama no se prueba en unitarias sino en Dev (V-1 a V-7 más la prueba de humo de formalización concurrente).

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-ARR-12 | Borrador sin congelar | Update de `sanic_comentarios` | Pasa; el `Target` no cambia (sin recálculos) |
| PA-ARR-13 | Borrador (inicial 1000, abonado 0, cuota 300, plazo 4) | Update de la cuota a 250 | Target con remanente 1000, total 1000, `fechafin` recalculada |
| PA-ARR-14 | Borrador | Update de abonado a 2000 | `CARP-ARR-004` |
| PA-ARR-15 | Borrador | Update de `numeroproducto` a `" ab 12"` | Target `"AB12"` |
| PA-ARR-16 | Borrador congelado | Update de cada columna de datos del acuerdo (16, parametrizada) con valor distinto | `CARP-ARR-011` nombrando la columna |
| PA-ARR-17 | Borrador congelado | Update de `sanic_conversacionid` | Pasa |
| PA-ARR-18 | Borrador congelado | Update con los mismos valores de la pre-imagen | Pasa |
| PA-ARR-19 | Borrador | Update del plazo a 61, y a 13 con capacidad 12 | `CARP-ARR-003` |
| PA-ARR-20 | Vigente reabierto con una cuota pagada | Update de `sanic_cuotamensual` | `CARP-ARR-021` |
| PA-ARR-21 | Borrador | Update de `numeroarreglo` y de `saldoremanente` (dos casos) | `CARP-ARR-009` |
| PA-ARR-22 | Borrador | Update de `sanic_motivoanulacion` sin cambio de estado | `CARP-ARR-024` |
| PA-ARR-23 | Borrador congelado, provisional 3, existentes 1 y 2 | Formalizar (`statuscode = Vigente`) | Target con `numeroarreglo = 3`, `formalizadoporid = actor`, `fechaformalizado = ahora`; `numeroprovisional` sigue en 3 |
| PA-ARR-24 | Borrador sin congelar | Formalizar | `CARP-ARR-019` |
| PA-ARR-25 | Borrador congelado, provisional 3, y entretanto otro arreglo del crédito se formalizó con 3 | Formalizar | `CARP-ARR-020` |
| PA-ARR-26 | Formalizar con `sanic_comentarios` en el mismo guardado | Update | `CARP-ARR-012` |
| PA-ARR-27 | Borrador (congelado y sin congelar) | Descartar con motivo; luego con motivo `"  "` | Pasa con `descartadoporid`, `fechadescarte`, `fechatermino`; y `CARP-ARR-013` |
| PA-ARR-28 | Vigente | Anular con motivo; sin motivo | Pasa con los tres campos; `CARP-ARR-014` |
| PA-ARR-29 | Borrador, Terminado | Anular | `CARP-ARR-010` y `CARP-ARR-022` |
| PA-ARR-30 | Borrador congelado | Desbloquear | Target con las cuatro instantáneas y `numeroprovisional` en `null` |
| PA-ARR-31 | Vigente congelado; Borrador sin congelar | Desbloquear | `CARP-ARR-018`; `CARP-ARR-017` |
| PA-ARR-32 | Terminado con congelamiento e instantánea | Jefatura reabre con motivo, con `statecode = 0` **y** `statuscode = Vigente` en el `Target` (V-5) | Estado Vigente; `reabiertoporid`, `fechareapertura`, contador +1; `fechatermino`, congelamiento, instantáneas y provisional en `null` |
| PA-ARR-33 | Terminado | Un Oficial (no Jefatura) reabre; reabre sin motivo | `CARP-ARR-016`; `CARP-ARR-015` |
| PA-ARR-34 | Vigente congelado | Jefatura escribe solo `motivoreapertura` | Mismo efecto sin cambio de estado; contador +1 |
| PA-ARR-35 | Vigente sin congelar | Reabrir | `CARP-ARR-017` |
| PA-ARR-36 | Reabrir con `sanic_comentarios` en el mismo guardado | Update | `CARP-ARR-012` |
| PA-ARR-37 | Anulado y Descartado | Cualquier cambio de datos | `CARP-ARR-022` |
| PA-ARR-38 | Terminado | Editar datos | `CARP-ARR-022` |
| PA-ARR-39 | Vigente | Un usuario escribe `statecode = 1, statuscode = Terminado`; Vigente → Borrador | `CARP-ARR-010` |
| PA-ARR-40 | Borrador congelado | `statuscode = Vigente` y `fechacongelamiento = null` a la vez | `CARP-ARR-012` |
| PA-ARR-41 | `UserId` ≠ `InitiatingUserId` | Anular | `anuladoporid = InitiatingUserId` |
| PA-ARR-42 | El sistema congela (escribe `fechacongelamiento` e instantánea) y marca Terminado | Update | Pasa sin tocar el `Target` |
| PA-ARR-43 | El crédito ya tiene 9999 | Formalizar | `CARP-ARR-023` |
| PA-ARR-44 | Reabrir con jefatura | Update | Una sola consulta de pertenencia a jefatura |
| PA-ARR-45 | Update que solo cambia `sanic_conversacionid` ~~y `sanic_nombre`~~ (modificada el 2026-10-11: `sanic_nombre` pasó a solo-sistema y ahora sí consulta identidad, PA-ARR-59) | Update | No consulta identidad |
| PA-ARR-46 | Alta de un Oficial con `fechafin` distinta del vencimiento de la última cuota (OA-5) | Create | Target con `fechafin` = la calculada |
| PA-ARR-47 | Borrador sin congelar | Update de un Oficial que cambia solo `fechafin` a una fecha distinta de la del calendario (OA-5) | Target con `fechafin` = la calculada |
| PA-ARR-48 | Borrador sin congelar; llamador **sin** el perfil de columna (la pre-imagen no trae `sanic_cuentadebitar` ni `sanic_instantaneacedula`, el doble las omite, V-6) | Update que reenvía `sanic_cuentadebitar` con el valor guardado | No es un cambio (DC-28): pasa; el valor guardado se leyó con el servicio de SYSTEM, una consulta con solo esas columnas |
| PA-ARR-49 | Borrador congelado; llamador sin el perfil | Update con `sanic_cuentadebitar` de valor distinto | Es un cambio: `CARP-ARR-011` nombrando la columna |
| PA-ARR-50 | Borrador sin congelar; llamador **con** el perfil (la pre-imagen trae `sanic_cuentadebitar`) | Update que reenvía el mismo valor, y otro con un valor distinto | Primer caso: no es un cambio; segundo: sí. En ambos, 0 consultas extra de las columnas protegidas (comparación normal, DC-01) |
| PA-ARR-51 | Arreglo Terminado, Anulado y Descartado (parametrizada; V-2: la plataforma no los bloquea) | Un Oficial, por PATCH directo, cambia solo `sanic_conversacionid` | `CARP-ARR-022` en los tres |
| PA-ARR-52 | Los mismos tres estados | El sistema (`InitiatingUserId` = SYSTEM) cambia `sanic_conversacionid` | Pasa sin tocar el `Target` (DC-03); es el repunte de 1.4 |
| PA-ARR-53 | Vigente congelado | Un Oficial, por PATCH directo, cambia cada columna de datos del acuerdo (parametrizada) | `CARP-ARR-011` nombrando la columna; el arreglo no se reabre solo |
| PA-ARR-54 | Terminado (V-5) | PATCH directo con **solo** `statecode = 0`, hecho por un Oficial y por la jefatura con motivo | Estado efectivo Borrador (DC-06): Terminado → Borrador, rechazo `CARP-ARR-022` en ambos (paso 1: gana el primer paso; ~~`CARP-ARR-010`~~, inconsistencia 16 resuelta el 2026-10-11); no se reabre |
| PA-ARR-55 | Terminado | PATCH directo con `statecode = 0`, `statuscode = Borrador` | Rechazo `CARP-ARR-022` (~~`CARP-ARR-010`~~, 2026-10-11), misma nota que PA-ARR-54 |
| PA-ARR-56 | Anulado y Descartado (V-2, V-5) | Un Oficial, y luego la jefatura, por PATCH directo: Anulado → Vigente (`0` / Vigente) y Descartado → Borrador (`0` / `1`) | `CARP-ARR-022` en los cuatro casos: no se reactivan |
| PA-ARR-57 | Borrador, y Vigente | PATCH directo con **solo** `statecode = 1` (botón nativo de desactivar) | Estado efectivo Terminado (DC-06): `CARP-ARR-010`; no queda ni Descartado ni Anulado |
| PA-ARR-58 | Borrador congelado, y Vigente (V-1) | Un `SetStateRequest` de una persona llega como `Update` anidado, `Depth = 2`, con `statecode = 0` y `statuscode = Vigente` (Formalizar, primer caso) o con `statecode = 1` y `statuscode = Terminado` (segundo caso) | Se evalúa igual que a profundidad 1: Formalizar con sus efectos; `CARP-ARR-010` |
| PA-ARR-59 | Arreglo Borrador (sin congelar y congelado) y Vigente congelado; y en otro grupo Terminado, Anulado y Descartado (parametrizada; decisión del 2026-10-11) | Un Oficial, por PATCH directo, cambia `sanic_nombre` a otro valor | `CARP-ARR-009` nombrando `sanic_nombre` en los tres primeros; `CARP-ARR-022` en los tres inactivos (paso 1); el `Target` no se modifica |
| PA-ARR-60 | Arreglo Borrador, Vigente y Terminado | El sistema (`InitiatingUserId` = SYSTEM) cambia `sanic_nombre` | Pasa sin tocar el `Target` (DC-03) |
| PA-ARR-61 | Arreglo Borrador | Un Oficial reenvía `sanic_nombre` con el mismo valor de la pre-imagen | Pasa: no es un cambio (DC-01); 0 llamadas a `WhoAmI` |
| PA-ARR-62 | Alta de un Oficial cuyo `Target` ya trae `sanic_nombre = "ARR-001002"` (lo que la plataforma deja siempre en el `Target` cuando la persona no lo envía, V-8, casos A1, A4 y A5) | Create | Pasa: cumple el formato; el `Target` conserva `ARR-001002` y no se modifica por este paso |
| PA-ARR-63 | Alta de un Oficial con `sanic_nombre` = `"ZZV-NOMBRE"`; `"ARR-12345"` (cinco dígitos); `"arr-001002"` (minúscula); `"ARR-001002 "` (espacio al final) (parametrizada; V-8, D-40) | Create | `CARP-ARR-009` nombrando `sanic_nombre`; el alta no sigue |
| PA-ARR-64 | Alta de un Oficial con `sanic_nombre` = `"ARR-777777"`; y otra con `"ARR-1000000"` (siete dígitos, el formato admite más dígitos al crecer) (V-8, B0 y N5) | Create | Pasa en ambas. **Riesgo conocido aceptado por el usuario (2026-10-11):** un `ARR-nnnnnn` falsificado por API pasa, porque en el `Create` es indistinguible del autonumérico; en el formulario la columna es de solo lectura y el número de negocio es `sanic_numeroarreglo`, protegido por K-02 |
| PA-ARR-65 | Arreglo Borrador (y otro Vigente congelado) | Un Oficial, por PATCH directo, cambia `sanic_nombre` a `"ARR-777777"` (con la forma del autonumérico) (V-8, caso A6) | `CARP-ARR-009` nombrando `sanic_nombre`: en el `Update` se rechaza todo valor distinto de la pre-imagen, sin mirar el formato |
| PA-ARR-66 | Arreglo Terminado con congelamiento e instantánea (D-40, 2026-10-11) | La jefatura reabre con motivo, con `statecode = 0` y `statuscode = Vigente`, y cambia `sanic_clienteid` a otro cliente en el mismo guardado; en otro caso, `sanic_oficialid` | `CARP-ARR-012` nombrando la columna en ambos; el arreglo sigue Terminado y el `Target` no se modifica |
| PA-ARR-67 | Arreglo Vigente congelado (D-40) | La jefatura escribe `sanic_motivoreapertura` y cambia `sanic_clienteid` (primer caso), `sanic_oficialid` (segundo) o `sanic_conversacionid` (tercero, la columna libre) en el mismo guardado | `CARP-ARR-012` nombrando la columna en los tres |
| PA-ARR-68 | Arreglo Terminado, y otro Vigente congelado (D-40) | La jefatura reabre solo con el motivo y el cambio de estado (primer caso: `statecode = 0` y `statuscode = Vigente`; segundo: solo `sanic_motivoreapertura`) | Pasa con los efectos de `PA-ARR-32` y `PA-ARR-34` |
| PA-ARR-69 | Arreglo Terminado, y otro Vigente congelado (D-40) | La jefatura reabre con motivo y reenvía los mismos valores de `sanic_clienteid`, `sanic_oficialid` y `sanic_conversacionid` que tiene la pre-imagen | Pasa: no son cambios (DC-01); los efectos de `PA-ARR-32` y `PA-ARR-34` |

### 2.7 Cuota (inv. 7.15; def. ítem 11; D-08, D-10; dicc. R-01, R-02)

#### `CuotaActualizarStep` — Update · PreOperation · síncrono · orden 10 · sin filtro · pre-imagen `PreImagen` (`sanic_nombre`, `sanic_numero`, `sanic_fechavencimiento`, `sanic_monto`, `sanic_fechapago`, `sanic_montopagado`, `sanic_arregloid`, `statecode`, `statuscode`; las dos últimas se agregaron el 2026-10-11, decisión del usuario)

1. Columnas cambiadas (DC-01), incluidos `statecode` y `statuscode`. Ninguna ⇒ terminar. `EsEscrituraDelSistema` ⇒ terminar.
2. Si cambió alguna columna distinta de `sanic_fechapago` y `sanic_montopagado` ⇒ `CARP-CUO-001` (nombra la columna). El oficial solo registra pagos. Esto incluye el `statecode` y el `statuscode` nativos (**decisión del usuario, 2026-10-11**): una persona no activa ni desactiva una cuota, solo SYSTEM lo hace; si cambian los dos, el mensaje nombra ambos. Ningún proceso de este diseño cambia el estado de una cuota: la purga la elimina con `Delete` (BulkDelete), el calendario la borra y la crea, y el paso a Terminado escribe sobre el Arreglo, no sobre la cuota.
3. Leer el arreglo (`statecode`, `statuscode`). Si no está **Vigente** ⇒ `CARP-CUO-002`. Esto incluye al Borrador: la solo-lectura nativa del registro inactivo no se hereda a la hija (def. ítem 11). V-2 (2026-10-11) va más lejos: la plataforma tampoco bloquea la escritura sobre el propio arreglo inactivo, así que esta regla (y las de 2.6) son la única barrera sobre las cuotas de un arreglo cerrado o congelado.
4. Pago registrado (DC-21, decidido el 2026-10-10, OA-8, opción B): con los valores efectivos de `sanic_fechapago` y `sanic_montopagado` (pre-imagen más `Target`), ambos o ninguno ⇒ si solo uno tiene valor, `CARP-CUO-003`; si `sanic_montopagado` tiene valor y no es mayor que cero ⇒ `CARP-CUO-003`. **Sin tope superior**: un monto mayor que el de la cuota pasa (puede ser un adelanto). Se evalúa solo si cambió alguna de las dos columnas (paso 1) y después de `CARP-CUO-001` y `CARP-CUO-002`.

#### `CuotaTerminarArregloStep` — Update · PostOperation · síncrono · orden 20 · filtro `sanic_fechapago,sanic_montopagado` · sin imagen

1. Leer el arreglo con SYSTEM. Si no está Vigente ⇒ terminar.
2. Buscar si queda alguna cuota del arreglo **sin pago registrado** (`sanic_fechapago` nulo, DC-21): consulta con `ColumnSet(false)` y `TopCount = 1`.
3. Si no queda ninguna: `Update` del arreglo como SYSTEM con `statecode = 1`, `statuscode = Terminado` y `sanic_fechatermino = ahora` (def. ítem 11). Un pago parcial cuenta (R-02). Pasa por `ArregloActualizarStep` como escritura del sistema.

Borrar un pago (vaciar `sanic_fechapago` y `sanic_montopagado` juntos) en un Vigente se permite y no revierte nada; vaciar solo una de las dos es `CARP-CUO-003`. Corregir un pago en un Terminado exige que la jefatura reabra el arreglo (2.6).

| Código | Mensaje |
|---|---|
| `CARP-CUO-001` | En una cuota solo se puede registrar la fecha y el monto de pago. Columna no permitida: {columna}. |
| `CARP-CUO-002` | Solo se pueden registrar pagos en un arreglo vigente. |
| `CARP-CUO-003` | El pago de una cuota se registra con su fecha y su monto juntos, y el monto debe ser mayor que cero. |

Códigos que estuvieron reservados para decisiones abiertas: `CARP-CUO-003` (OA-8, opción B) y `CARP-UNI-010` (OA-11) **se emiten desde el 2026-10-10** (tablas de errores de 2.7 y 1.4). ~~`CARP-GEN-012` (OA-9, opción C) y `CARP-EML-004` (OA-10, opción C)~~ quedan descartados: se decidió la opción A en ambos casos y el código no los emite.

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-CUO-01 | Arreglo Vigente | Un Oficial cambia `fechapago` y `montopagado` (monto mayor que cero) | Pasa |
| PA-CUO-02 | Arreglo Vigente | Un Oficial cambia `sanic_monto`, `sanic_fechavencimiento`, `sanic_numero` o `sanic_arregloid` (parametrizada) | `CARP-CUO-001` nombrando la columna |
| PA-CUO-03 | Arreglo Borrador, Terminado, Anulado, Descartado (parametrizada) | Un Oficial registra un pago | `CARP-CUO-002` |
| PA-CUO-04 | Update con los mismos valores de la pre-imagen | Update | Pasa; no se lee el arreglo |
| PA-CUO-05 | El sistema modifica `sanic_monto` | Update | Pasa |
| PA-CUO-06 | Arreglo Vigente con 3 cuotas; las 2 primeras con pago | Se registra el pago de la 3.ª | El arreglo recibe (SYSTEM) `statecode = 1`, `statuscode = Terminado`, `fechatermino = ahora` |
| PA-CUO-07 | Queda una cuota sin pago | Se registra otro pago | El arreglo no se escribe |
| PA-CUO-08 | La última cuota se paga con `montopagado` menor que `monto` | Post-update | Termina igual (R-02) |
| PA-CUO-09 | Vigente reabierto (sin congelar) con todas las cuotas pagadas | Post-update | Termina |
| PA-CUO-10 | Se vacían `fechapago` y `montopagado` de una cuota con pago en un Vigente | Update | Pasa; el arreglo sigue Vigente |
| PA-CUO-11 | Una cuota con `montopagado` y sin `fechapago` escrita por el sistema (un Oficial no puede producirla, PA-CUO-16), las demás pagadas | Post-update | El arreglo no termina: la búsqueda de pendientes mira `fechapago` nulo (DC-21) |
| PA-CUO-12 | El arreglo ya no está Vigente cuando corre el post | Post-update | No hace nada |
| PA-CUO-13 | Arreglo de 60 cuotas | Post-update | La búsqueda de pendientes es una sola consulta con `TopCount = 1` y sin columnas |
| PA-CUO-14 | Cadena (pipeline simulado) | Se paga la última cuota | `ArregloActualizarStep` acepta la transición a Terminado por ser del sistema; un Oficial que intente el mismo `Update` recibe `CARP-ARR-010` |
| PA-CUO-15 | Arreglo Vigente, cuota sin pago | Un Oficial escribe `fechapago` sin `montopagado` (OA-8) | `CARP-CUO-003` |
| PA-CUO-16 | Arreglo Vigente, cuota sin pago | Un Oficial escribe `montopagado` sin `fechapago` (OA-8) | `CARP-CUO-003` |
| PA-CUO-17 | Arreglo Vigente, cuota sin pago | Un Oficial escribe `fechapago` con `montopagado` en 0 y, en otra llamada, negativo (OA-8) | `CARP-CUO-003` en ambos |
| PA-CUO-18 | Arreglo Vigente, cuota de 300 sin pago | Un Oficial registra fecha y `montopagado` de 500 (OA-8, sin tope) | Pasa |
| PA-CUO-19 | Arreglo Vigente, cuota sin pago | Un Oficial envía `fechapago` y `montopagado` ambos vacíos (OA-8) | Pasa; no hay cambio (DC-01) y ambos o ninguno se cumple |
| PA-CUO-20 | Arreglo Vigente, cuota con pago | Un Oficial vacía solo `fechapago`, y en otra llamada solo `montopagado` (OA-8) | `CARP-CUO-003` en ambos |
| PA-CUO-21 | Arreglo Vigente, cuota activa sin pago (V-2: la plataforma no bloquea la desactivación; decisión del 2026-10-11) | Un Oficial, por PATCH directo, desactiva la cuota con **solo** `statecode = 1` (botón nativo) y, en otra llamada, con `statecode = 1` y `statuscode = 2` | `CARP-CUO-001` en ambos, nombrando `statecode` (y `statuscode` en el segundo); la cuota sigue activa |
| PA-CUO-22 | Cuota activa, y otra inactiva | El sistema (`InitiatingUserId` = SYSTEM) cambia `statecode` y `statuscode` | Pasa sin tocar el `Target` (DC-03) |
| PA-CUO-23 | Cuota activa | Un Oficial reenvía `statecode = 0` y `statuscode = 1` con los mismos valores de la pre-imagen, junto con el pago de fecha y monto | Pasa: el estado no es un cambio (DC-01); solo cuenta el pago |

### 2.8 Versión de plantilla y Mapeo de celda (inv. 7.16; def. ítem 15; D-14, D-15, D-16)

#### `VersionPlantillaCrearStep` — Create · PreOperation · síncrono · orden 10

Para una persona (el sistema pasa): (1) `sanic_estado` ausente o `Borrador`; otro ⇒ `CARP-VER-001`. Una versión **siempre nace en Borrador**, también por API (DC-19). (2) `sanic_huella`, `sanic_fechaaprobacion` o `sanic_aprobadoporid` presentes ⇒ `CARP-VER-002`. (3) `sanic_nombre` debe cumplir `^v[0-9]{2,3}$` ⇒ `CARP-VER-012` (la plataforma no valida el patrón; la unicidad la da K-03).

#### `VersionPlantillaActualizarStep` — Update · PreOperation · síncrono · orden 10 · sin filtro · pre-imagen `PreImagen` (`statecode`, `statuscode`, `sanic_nombre`, `sanic_estado`, `sanic_capacidad`, `sanic_huella`, `sanic_fechaaprobacion`, `sanic_aprobadoporid`; `statuscode` se agregó el 2026-10-11)

Columnas cambiadas (DC-01), incluidos `statecode` y `statuscode`; ninguna ⇒ terminar. `EsEscrituraDelSistema` ⇒ terminar (así pasa el paso Vigente → Histórica, que escribe `sanic_estado`, no el `statecode`). **Decisión del usuario, 2026-10-11: el `statecode` y el `statuscode` nativos de la versión solo los cambia SYSTEM.** Para una persona, según el estado anterior:

- **Borrador**:
  - Cambió `statecode` o `statuscode` ⇒ `CARP-VER-014`. Se evalúa primero: ~~se puede desactivar (`statecode`) el borrador~~ (retirado el 2026-10-11). Con esto una versión Borrador desactivada ya no puede existir por la acción de una persona (inconsistencia 17).
  - Cambió `sanic_huella`, `sanic_fechaaprobacion` o `sanic_aprobadoporid` ⇒ `CARP-VER-002`.
  - Cambió `sanic_estado`: solo `Borrador → Vigente` es válido (**Publicar**); cualquier otro ⇒ `CARP-VER-004`. Publicar no admite otras columnas en el mismo guardado ⇒ `CARP-VER-013`.
  - Si no cambió el estado: edición libre de `sanic_nombre` (con el patrón, `-012`), `sanic_capacidad` y el archivo. ~~Se puede desactivar (`statecode`) el borrador.~~
- **Vigente**: cualquier cambio ⇒ `CARP-VER-003` (inmutable, también desactivarla).
- **Histórica**: cualquier cambio ⇒ `CARP-VER-003`. ~~Solo se acepta cambiar `statecode` (desactivar o reactivar el catálogo).~~ Retirado el 2026-10-11: el `statecode` solo lo cambia SYSTEM, también en una Histórica.

**Publicar** (con SYSTEM para lo que sigue):
1. Descargar el archivo (`InitializeFileBlocksDownload` + `DownloadBlock`). Sin archivo ⇒ `CARP-VER-005`.
2. Leer las filas de mapeo activas de la versión. `ValidadorDePublicacion.Validar(capacidad, filas)` ⇒ si hay errores, `CARP-VER-007`/`-008` con **todos** (2.0.7).
3. **Generación de prueba** (DC-19): `IGeneradorDeFormato.Generar` con el archivo, su huella recién calculada, el mapeo y un `DatosArreglo` sintético (`DatosDePrueba`, plazo = capacidad, todas las claves con valores inventados). `PlantillaInvalidaException` ⇒ `CARP-VER-006`; `MapeoInvalidoException` o `DatosInvalidosException` ⇒ `CARP-VER-009` (incluye hoja inexistente: el generador responde con las hojas reales, spike 1 hallazgo 1). No escribe nada.
4. Escribir en el `Target`: `sanic_huella` (SHA-256 del archivo), `sanic_fechaaprobacion = ahora`, `sanic_aprobadoporid = InitiatingUserId` (es el contexto del propio usuario: no hay elevación). El `sanic_estado = Vigente` ya viene del gesto.
5. Con SYSTEM: cada otra versión `Vigente` pasa a `Histórica`. Después, comprobar que no queda ninguna otra Vigente; si queda ⇒ `CARP-VER-010` (revierte todo).

#### `ArchivoDePlantillaStep` — Update · PreOperation · síncrono · orden 20 · filtro `sanic_archivo` · sin imagen · tabla `sanic_carp_tbl_versionplantilla`

~~Antes: mensaje `CommitFileBlocksUpload` · PreOperation · síncrono · orden 10 · registro condicionado a V-4.~~ **Rediseñado el 2026-10-11 (V-4 refutado).** `CommitFileBlocksUpload`, `InitializeFileBlocksUpload` y `UploadBlock` no admiten steps personalizados. Lo que sí ocurre: la subida de un archivo (por bloques o con un PATCH a la columna) provoca un `Update` de la Versión con `MessageName = "Update"`, mensaje padre `CommitFileBlocksUpload` y `Target` con el id de la versión y `sanic_archivo` (el id del archivo). Un PATCH de otra columna (por ejemplo `sanic_huella`) no lo dispara.

1. Lee el id del `Target`. Si el actor (`InitiatingUserId`, 0.3) no es del sistema, lee con SYSTEM el `sanic_estado` de la versión (una consulta por id); si no es Borrador ⇒ `CARP-VER-003`. No mira el mensaje padre ni `Depth` (V-1: no asume profundidad 1).
2. **Reemplazo con PATCH directo, dos pasadas.** Al reemplazar un archivo, la plataforma hace primero un `Update` como SYSTEM (`UserId` = SYSTEM, `InitiatingUserId` = el llamador) que deja `sanic_archivo` en nulo, y después otro `Update` del llamador con el id nuevo. El step trata las dos con `InitiatingUserId`, no con `UserId`: en una versión no Borrador ambas son de una persona y ya la primera se rechaza (la transacción se revierte y el archivo no queda en nulo); en un Borrador ambas pasan. Si `InitiatingUserId` fuera SYSTEM de verdad (código elevado), el step deja pasar (DC-03).
3. Si la plataforma dejara de disparar el `Update` oculto, la barrera de respaldo sigue siendo la huella validada en cada generación (D-15, `CARP-GEN-008`).

**Relación con `VersionPlantillaActualizarStep` (S-26).** S-26 va sin filtro y también recibe este `Update`, pero su pre-imagen no incluye `sanic_archivo`, de modo que DC-01 no puede decir si el archivo "cambió". Se **deja separado** (decisión de este documento, 2026-10-11): una regla explícita sobre el archivo, sin pre-imagen del archivo, que no depende de DC-01. Va en orden 20, después de S-26 (orden 10), para que no haya dos steps con el mismo orden sobre (`Update`, tabla, etapa) (BP-PP-060). En una versión no Borrador, S-26 normalmente rechaza antes con el mismo `CARP-VER-003`; S-27 queda como barrera propia del archivo. Sigue sin probarse en Dev que un `InvalidPluginExecutionException` lanzado desde este step aborte la subida, ni la subida por un usuario no administrador (8.1, H-12).

#### Mapeo de celda: `MapeoCeldaCrearStep`, `MapeoCeldaActualizarStep`, `MapeoCeldaEliminarStep`

- **Create** · PreOperation · síncrono · orden 10. **Update** · PreOperation · síncrono · orden 10 · sin filtro · pre-imagen `PreImagen` (`sanic_versionplantillaid`, `sanic_hoja`, `sanic_celda`, `sanic_tipo`, `sanic_campo`, `sanic_filasmaximas`, `statecode`, `statuscode`; las dos últimas se agregaron el 2026-10-11, D-40). **Delete** · PreOperation · síncrono · orden 10 · pre-imagen `PreImagen` (`sanic_versionplantillaid`).
- El sistema pasa. Para una persona: la versión padre debe estar en **Borrador** ⇒ si no, `CARP-MAP-001` (la versión padre se lee de la referencia del `Target` en Create y de la pre-imagen en Update y Delete). El Jefe puede borrar un mapeo solo en una versión Borrador (matriz §2).
- **Estado nativo (decisión del usuario, 2026-10-11, D-40; misma regla que Cuota, Versión y Cliente).** El `statecode` y el `statuscode` nativos del mapeo solo los cambia SYSTEM. En Update, si el `Target` trae `statecode` o `statuscode` con un valor distinto del de la pre-imagen (DC-01) y el actor no es del sistema ⇒ `CARP-MAP-007`, tanto para desactivar como para reactivar. Se evalúa justo después de la comprobación de la versión padre (`CARP-MAP-001`: en una versión no Borrador gana ese código, porque todo cambio se rechaza) y antes de validar valores, de modo que un guardado que solo trae el estado no valida nada más. La identidad se consulta solo si hubo un cambio de estado (camino barato). Create y Delete no tienen regla de estado (el Delete lo gobierna la versión padre). El filtro de lectura de la publicación (2.8, Publicar, paso 2: "filas de mapeo activas") no cambia: ninguna persona deja un mapeo inactivo.
- Validar con los valores efectivos: celda A1 bien formada ⇒ `CARP-MAP-002`; (tipo, campo) admitido por la tabla de dicc. §3.1 ⇒ `CARP-MAP-003`; `sanic_filasmaximas` entre 1 y 60 si el tipo es ColumnaDeBloqueDeCuotas ⇒ `CARP-MAP-004`; hoja no vacía ⇒ `CARP-MAP-005`. La hoja **no se recorta**: el nombre real empieza con un espacio (spike 1, hallazgo 1). Una hoja de solo espacios es vacía.
- Normalizar la celda a mayúscula y escribir `sanic_nombre = "<hoja>!<celda>"` en Create y en Update si cambió la hoja o la celda.
- Update que cambia `sanic_versionplantillaid` ⇒ `CARP-MAP-006`.
- Las reglas entre filas (dos campos en la misma celda, bloque completo, capacidad) se validan al publicar (2.0.7).

| Código | Mensaje |
|---|---|
| `CARP-VER-001` | Una versión de plantilla siempre se crea en estado Borrador. |
| `CARP-VER-002` | La huella, la fecha y el aprobador los fija el sistema al publicar. |
| `CARP-VER-003` | Solo una versión en Borrador se puede modificar. Para cambiar el formato cree una versión nueva. |
| `CARP-VER-004` | Esa transición de estado de la versión no está permitida. Solo se puede publicar un borrador. |
| `CARP-VER-005` | La versión no tiene archivo de plantilla cargado. |
| `CARP-VER-006` | El archivo de la plantilla no es un libro de Excel válido. |
| `CARP-VER-007` | El mapeo no se puede publicar: {errores}. |
| `CARP-VER-008` | La capacidad de la versión ({capacidad}) no coincide con las filas máximas del bloque de cuotas. |
| `CARP-VER-009` | La plantilla y el mapeo no producen un formato válido: {detalle}. |
| `CARP-VER-010` | Quedó más de una versión vigente. Avise al equipo de soporte. |
| `CARP-VER-012` | El número de versión debe tener el formato v07 (la letra v y 2 o 3 dígitos). |
| `CARP-VER-013` | Publicar no admite otros cambios en el mismo guardado. |
| `CARP-VER-014` | Una versión de plantilla no se activa ni se desactiva a mano: lo gestiona el sistema. |
| `CARP-MAP-001` | Solo se puede modificar el mapeo de una versión en Borrador. |
| `CARP-MAP-002` | La celda "{celda}" no está bien escrita. Use notación como B7. |
| `CARP-MAP-003` | El campo "{campo}" no es compatible con el tipo de mapeo elegido. |
| `CARP-MAP-004` | Las columnas del bloque de cuotas necesitan filas máximas entre 1 y 60. |
| `CARP-MAP-005` | La hoja es obligatoria. |
| `CARP-MAP-006` | Un mapeo no se puede mover a otra versión. |
| `CARP-MAP-007` | Un mapeo de celda no se activa ni se desactiva a mano: lo gestiona el sistema. |

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-VER-01 | Alta por el Jefe de `v08` | Create | Pasa |
| PA-VER-02 | Alta con `nombre = "7"`; con estado Vigente; con huella | Create | `CARP-VER-012`; `-001`; `-002` |
| PA-VER-03 | v08 en Borrador con el archivo v07 y el mapeo del diccionario; existe una v07 Vigente | Publicar (`sanic_estado = Vigente`) | Target con `huella` = SHA-256 del archivo, `fechaaprobacion = ahora`, `aprobadoporid = actor`; la v07 pasa a Histórica (escritura de SYSTEM); queda una sola Vigente |
| PA-VER-04 | Primera publicación, sin Vigente previa | Publicar | Pasa |
| PA-VER-05 | Sin archivo; archivo que no es zip | Publicar | `CARP-VER-005`; `CARP-VER-006` |
| PA-VER-06 | Mapeo vacío; mapeo con dos campos en una celda y una celda mal formada | Publicar | `CARP-VER-007` con todos los errores |
| PA-VER-07 | Capacidad 48 y bloque de 60 | Publicar | `CARP-VER-008` |
| PA-VER-08 | Mapeo apuntando a la hoja `"FORMATO"` (sin el espacio inicial) | Publicar | `CARP-VER-009`; el mensaje lista las hojas reales entre comillas |
| PA-VER-09 | Publicar y cambiar `sanic_capacidad` a la vez | Update | `CARP-VER-013` |
| PA-VER-10 | Versión Vigente | Update de cualquier columna; desactivarla | `CARP-VER-003` |
| PA-VER-11 | Versión Histórica | Editar la capacidad; desactivarla | `CARP-VER-003`; ~~pasa~~ `CARP-VER-003` (modificada el 2026-10-11: el `statecode` solo lo cambia SYSTEM) |
| PA-VER-12 | Borrador | Editar la capacidad; escribir `huella`; pasar a Histórica; pasar a Borrador desde Vigente | pasa; `-002`; `-004`; `-003` |
| PA-VER-13 | Una escritura del sistema pasa Vigente → Histórica | Update | Pasa sin validar |
| PA-VER-14 | Tras publicar queda otra Vigente (dato inconsistente) | Publicar | `CARP-VER-010`; al restaurar, la versión sigue en Borrador |
| PA-VER-15 | ~~`CommitFileBlocksUpload` sobre una versión Vigente; sobre una Borrador~~ `Update` con `sanic_archivo` en el `Target` (mensaje padre `CommitFileBlocksUpload`, V-4) de una persona sobre una versión Vigente; sobre una Histórica; sobre una Borrador | Ejecutar `ArchivoDePlantillaStep` | `CARP-VER-003`; `CARP-VER-003`; pasa |
| PA-VER-16 | Publicar | Generación de prueba | Usa solo datos sintéticos y no hay escrituras fuera de las de publicación (el registro del doble lo demuestra) |
| PA-VER-17 | Cualquier fallo de publicación | Trazas | Sin nombres de personas ni datos de arreglos |
| PA-VER-18 | Reemplazo de archivo con PATCH directo en una versión Borrador (V-4) | Dos `Update` seguidos: (1) `UserId` = SYSTEM, `InitiatingUserId` = el Jefe, `sanic_archivo = null`; (2) el Jefe con el id nuevo | Ambos pasan |
| PA-VER-19 | Reemplazo de archivo en una versión Vigente (V-4) | La misma primera pasada: `UserId` = SYSTEM, `InitiatingUserId` = el Jefe, `sanic_archivo = null` | `CARP-VER-003`: se evalúa con `InitiatingUserId`, no con `UserId`; la transacción se revierte |
| PA-VER-20 | Versión Vigente; `Update` con `sanic_archivo` y `InitiatingUserId` = SYSTEM | Ejecutar `ArchivoDePlantillaStep` | Pasa (DC-03) |
| PA-VER-21 | Los casos de PA-VER-15 y PA-VER-19 | Ejecutar con `Depth = 1` y con `Depth = 2`, y con otro mensaje padre | Mismo resultado: el step no filtra por profundidad ni por mensaje padre (V-1) |
| PA-VER-22 | Versión Histórica | Un Jefe escribe `sanic_estado = Vigente`; y otro, `sanic_estado = Borrador` (PATCH directo) | `CARP-VER-003` en ambos |
| PA-VER-23 | Versión Vigente | Un Jefe escribe `sanic_estado = Histórica` (PATCH directo) | `CARP-VER-003` (esa transición es solo del sistema) |
| PA-VER-24 | Versión Borrador (V-2: la plataforma no bloquea la desactivación; decisión del 2026-10-11) | Un Jefe, por PATCH directo, la desactiva con **solo** `statecode = 1`; en otra llamada, con `statecode = 1` y `statuscode = 2`; y publica con `statecode = 1` en el mismo guardado | `CARP-VER-014` en los tres casos (el primer paso gana sobre `CARP-VER-013`); la versión sigue activa y en Borrador |
| PA-VER-25 | Versión Histórica | Un Jefe la reactiva (de inactiva a activa), y otro la desactiva | `CARP-VER-003` en ambos |
| PA-VER-26 | Versión Borrador, Vigente e Histórica | El sistema (`InitiatingUserId` = SYSTEM) cambia `statecode` y `statuscode` | Pasa sin validar (DC-03) en los tres estados |
| PA-MAP-01 | Versión Borrador | Create de `hoja = " FORMATO ARP ELECTRONICO"`, `celda = "b7"` | `sanic_nombre = " FORMATO ARP ELECTRONICO!B7"`; hoja sin recortar; celda `"B7"` |
| PA-MAP-02 | Versión Vigente o Histórica | Create, Update, Delete | `CARP-MAP-001` |
| PA-MAP-03 | Celdas `B0`, `7B`, `XFE1`, `""` | Create | `CARP-MAP-002` |
| PA-MAP-04 | `Cuota: monto` + ValorSimple; `Numero de arreglo` + Bloque; `Total: montos de cuota` + TotalCalculado; `Moneda` + ValorSimple | Create | `CARP-MAP-003`; `-003`; pasa; pasa |
| PA-MAP-05 | Bloque con filas máximas vacías, 0 y 61 | Create | `CARP-MAP-004` |
| PA-MAP-06 | Hoja vacía; solo espacios; `" X"` | Create | `CARP-MAP-005`; `-005`; pasa |
| PA-MAP-07 | Mapeo en Borrador | Update de la celda | Recalcula `sanic_nombre` |
| PA-MAP-08 | Update que cambia la versión padre | Update | `CARP-MAP-006` |
| PA-MAP-09 | Delete en Borrador; en Vigente | Delete | pasa; `CARP-MAP-001` |
| PA-MAP-10 | Escrituras del sistema | Create/Update/Delete | Pasan |
| PA-MAP-11 | Mapeo activo de una versión Borrador (V-2: la plataforma no bloquea la desactivación; decisión del 2026-10-11, D-40) | Un Jefe, por PATCH directo, lo desactiva con **solo** `statecode = 1` (botón nativo) y, en otra llamada, con `statecode = 1` y `statuscode = 2` | `CARP-MAP-007` en ambos (`CARP-MAP-007` es nuevo: ningún mensaje existente encaja, `CARP-MAP-001` habla de la versión y `CARP-VER-014` de otra tabla); el mapeo sigue activo |
| PA-MAP-12 | Mapeo inactivo de una versión Borrador | Un Jefe lo reactiva (`statecode = 0`, `statuscode = 1`) | `CARP-MAP-007` |
| PA-MAP-13 | Mapeo activo, y otro inactivo | El sistema (`InitiatingUserId` = SYSTEM) cambia `statecode` y `statuscode` | Pasa sin modificar el `Target` (DC-03) |
| PA-MAP-14 | Mapeo activo de una versión Borrador | Un Jefe reenvía `statecode = 0` y `statuscode = 1` con los mismos valores de la pre-imagen y cambia la celda | Pasa: el estado no es un cambio (DC-01); 0 llamadas a `WhoAmI`; recalcula `sanic_nombre` |
| PA-MAP-15 | Mapeo activo de una versión Vigente, y otro de una Histórica | Un Jefe lo desactiva con `statecode = 1` | `CARP-MAP-001` (gana la comprobación de la versión padre) |

### 2.9 Email: remitente del área y columnas solo-sistema (inv. 7.11; D-28; def. ítem 3, L270)

#### `RemitenteDelAreaStep` — Create · PreOperation · síncrono · orden 10 · sin filtro · sin imagen · tabla `email`

Aplica solo a un correo **saliente** (`directioncode` verdadero) cuyo `regardingobjectid` es una `sanic_carp_tbl_conversacion`. Cualquier otro correo: terminar sin consultar nada (0 consultas).

1. Resolver la queue del área (0.4, paso 5): `CARP-CFG-004`/`-005` si no hay una sola.
2. Leer `Target["from"]` (colección de `activityparty`):
   - Vacía o ausente ⇒ poner una sola parte: la queue.
   - Solo la queue ⇒ pasa.
   - Solo el usuario que crea (`InitiatingUserId`) ⇒ **reemplazar** por la queue (DC-07: es el remitente por defecto del formulario, spike 4).
   - Cualquier otra combinación (otro usuario, otra queue, la queue más otra parte) ⇒ `CARP-EML-001`.
3. No se fija `parentactivityid`: el botón Responder ya lo completa (spike 4, def. L269).

#### `EmailSoloSistemaStep` — Create · PreOperation · orden 10; y Update · PreOperation · orden 10 · filtro `sanic_tiemporespuesta,sanic_respondidoporid` · tabla `email`

Si el `Target` trae esas columnas y el actor no es del sistema ⇒ `CARP-EML-003`. (Dicc. §5 las marca solo-sistema; D-30 de la definición no las nombra, Inconsistencias punto 9.) El oficial tiene Write sobre Email y podría falsear los tiempos.

| Código | Mensaje |
|---|---|
| `CARP-EML-001` | Las respuestas de una conversación salen siempre desde el buzón del área. |
| `CARP-EML-003` | Estas columnas las gestiona el sistema y no se pueden modificar. |

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-EML-01 | Respuesta saliente con Regarding a una Conversación y `from = [usuario]` | Create | `from = [queue]` |
| PA-EML-02 | Igual con `from` ausente | Create | `from = [queue]` |
| PA-EML-03 | `from = [queue]` | Create | Igual |
| PA-EML-04 | `from = [otro usuario]`; `from = [queue, usuario]`; `from = [otra queue]` | Create | `CARP-EML-001` |
| PA-EML-05 | Correo entrante | Create | No toca nada; 0 consultas |
| PA-EML-06 | Saliente sin Regarding; saliente con Regarding a una cuenta | Create | No toca nada; 0 consultas |
| PA-EML-07 | Cero queues con la dirección configurada; dos queues | Create | `CARP-CFG-004`; `CARP-CFG-005` |
| PA-EML-08 | La variable trae `Area@Banco.com`; la queue tiene `area@banco.com` | Create | Se resuelve (sin distinguir mayúsculas) |
| PA-ESI-01 | Un Oficial crea un Email con `sanic_tiemporespuesta` | Create | `CARP-EML-003` |
| PA-ESI-02 | Un Oficial actualiza `sanic_respondidoporid` | Update | `CARP-EML-003` |
| PA-ESI-03 | El sistema escribe ambas | Create/Update | Pasa |
| PA-ESI-04 | Update con las columnas pero valor `null` y la fila ya las tiene `null` | Update | Pasa (sin cambio) |

### 2.10 Entrada de correo en conversaciones (inv. 7.11, 7.19; RF-01; D-03, D-32; def. ítem 1)

#### `RegistrarCorreoEntranteStep` — Create · **PostOperation · asíncrono** · orden 10 · sin filtro · sin imagen · tabla `email`

El step solo toma `PrimaryEntityId` y llama a `RegistradorDeCorreo.Registrar(emailId)`. La API de reconciliación (1.2) llama a la misma clase. Corre con SYSTEM. Si falla, el correo igual queda guardado (D-03) y la reconciliación lo recoge.

**`RegistradorDeCorreo.Registrar(emailId)`** devuelve `Creada`, `Unida`, `YaTenia` u `Omitido(código)`.

1. **Releer el Email por id** con SYSTEM (`directioncode`, `regardingobjectid`, `messageid`, `inreplyto`, `subject`, `sender`, `actualend`, `createdon`, `ownerid`). No se confía en el `Target`: el Regarding de la correlación puede escribirse en un `Update` posterior al `Create` (spike 3, "Qué columnas usar"). No existe ⇒ `Omitido(CARP-ENT-002)`. Es saliente ⇒ `Omitido(CARP-ENT-001)`.
2. Fecha de llegada = `actualend`; si es nula, `createdon` (DC-13 y spike 3: `createdon` suma demora de sincronización, por eso no es la primera opción).
3. **Determinar la conversación**:
   - **A. Ya correlacionado**: el Regarding es una Conversación. Se lee.
   - **B. Segunda red** (DC-08): sin Regarding (o con Regarding a una entidad que no es Conversación: se ignora y se traza) y con `inreplyto` informado: buscar un Email cuyo `messageid` coincida con `inreplyto` (se prueban tres variantes: tal cual, con `<` `>` y sin ellos; la comparación de texto de Dataverse no distingue mayúsculas, necesario porque los salientes de Dataverse llevan el dominio en mayúsculas, spike 3) y que tenga Regarding a una Conversación. Si se encuentra, esa es la conversación.
   - **C. Nueva**: calcular la huella (2.0.2). Buscar por la clave K-01; si existe, usar esa (idempotencia, D-03). Si no existe, **crear** la conversación (paso 5).
4. **Conversación Unida** (DC-09): si la encontrada está en Unida, seguir `sanic_unidaaid` (hasta 3 saltos) hasta una que no lo esté. Si no termina ⇒ `Omitido(CARP-ENT-003)`.
5. **Crear** (ruta C, con SYSTEM): `sanic_huellamessageid`; `sanic_fechaprimercorreo` = llegada; `sanic_asunto` = `subject` recortado a 400; `sanic_remitente` = `sender` recortado, sin espacios y en minúscula, a 320; `sanic_sinrespuestadesde` = llegada; `sanic_cantidadrespuestas = 0`; `sanic_correonuevoenfinalizada = no`; `ownerid` = team del área (en el mismo create, def. D-31); `sanic_clienteid` = el `Cliente` activo cuyo `sanic_correo` es igual al remitente **si hay exactamente uno** (DC-14); estado Abierta por defecto. Un duplicado concurrente lo rechaza K-01; el job asíncrono falla y la reconciliación lo repara. No se atrapa.
6. **Actualizar la conversación existente** (rutas A, B, o C encontrada por huella), con SYSTEM y solo si el valor cambia:
   - **Abierta**: `sanic_sinrespuestadesde` = el menor entre el actual (si lo hay) y la llegada.
   - **Finalizada**: `sanic_correonuevoenfinalizada = sí` y `sanic_sinrespuestadesde` = el menor entre el actual y la llegada (P-09, DC-10). No se reabre (RF-15).
7. **Email**: si su Regarding no es la conversación final, `Update` de `regardingobjectid`. Si su `ownerid` no es el team del área, `AssignRequest` al team (con SYSTEM). Si ya es el team, no se asigna.
8. Resultado: `Creada` (ruta C creó), `Unida` (se agregó a una existente por A, B o por huella ya existente cuando el correo no tenía Regarding), `YaTenia` (el correo ya estaba en la conversación correcta y no hubo cambios).

Columnas de la conversación que **no** se tocan acá: cantidad de respuestas y tiempos (2.11).

Residual conocido: dos correos que llegan casi a la vez a la misma conversación calculan el mínimo cada uno sin bloqueo; el resultado final puede quedar con la fecha del segundo. La vista de pendientes y la reconciliación semanal lo hacen visible; no se pone concurrencia optimista porque una falla de datos dentro del plugin revierte el trabajo (`patrones.md` §3).

| Código | Mensaje (va al registro del job asíncrono y al `detalle` de 1.2) |
|---|---|
| `CARP-ENT-001` | El correo no es entrante. |
| `CARP-ENT-002` | No se encontró el correo. |
| `CARP-ENT-003` | La cadena de conversaciones unidas no termina en una conversación activa. |

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-ENT-01 | Email entrante sin Regarding ni `inreplyto`, `messageid` `<CAF+abc123@mail.example.com>`, asunto "Arreglo", remitente `Ana@X.com`, `actualend` 10:00 | Registrar | Conversación con huella `093f0929…05d1d`, `fechaprimercorreo` 10:00, asunto, remitente `ana@x.com`, `sinrespuestadesde` 10:00, cantidad 0, indicador no, `ownerid` = team; el Email con Regarding a ella y `ownerid` = team (por Assign); resultado `Creada` |
| PA-ENT-02 | El mismo Email procesado dos veces | Registrar | Una sola conversación; la segunda vez `YaTenia` y sin escrituras |
| PA-ENT-03 | Dos Emails con el mismo Message-ID (copia por otra queue) | Registrar ambos | Una sola conversación; ambos con su Regarding; el segundo `Unida` |
| PA-ENT-04 | Email ya con Regarding a una conversación Abierta con `sinrespuestadesde` vacío | Registrar | `sinrespuestadesde` = llegada; no se crea conversación |
| PA-ENT-05 | Abierta con `sinrespuestadesde` 09:00; llega correo de 10:00 | Registrar | Queda 09:00 y no se escribe la conversación |
| PA-ENT-06 | Abierta con `sinrespuestadesde` 11:00; llega un correo con llegada 10:00 | Registrar | Queda 10:00 |
| PA-ENT-07 | Conversación Finalizada | Llega un correo correlacionado de 10:00 | Indicador `sí`, `sinrespuestadesde` 10:00, sigue Finalizada |
| PA-ENT-08 | Finalizada con indicador `sí` y `sinrespuestadesde` 09:00; llega otro de 10:00 | Registrar | Queda 09:00 |
| PA-ENT-09 | El Regarding es una conversación Unida cuyo destino está Abierto | Registrar | El Email pasa a la conversación destino y esta se actualiza; la Unida no se toca |
| PA-ENT-10 | Unida → Unida → Unida → Unida (4 saltos) | Registrar | `Omitido(CARP-ENT-003)`; sin escrituras |
| PA-ENT-11 | Sin Regarding; `inreplyto` = `<ID@MAIL.EXAMPLE.COM>` y existe un Email con `messageid` `<id@mail.example.com>` con Regarding a la conversación C | Registrar | El correo se une a C; no se crea conversación (DC-08) |
| PA-ENT-12 | Sin Regarding; `inreplyto` desconocido | Registrar | Se crea conversación nueva |
| PA-ENT-13 | Email saliente; Email inexistente | Registrar | `Omitido(CARP-ENT-001)`; `Omitido(CARP-ENT-002)`; sin escrituras |
| PA-ENT-14 | El Email ya tiene `ownerid` = team | Registrar | No hay `Assign` |
| PA-ENT-15 | Un `Cliente` activo con el correo del remitente; ninguno; dos | Registrar | `clienteid` = ese; vacío; vacío |
| PA-ENT-16 | `actualend` nulo | Registrar | Usa `createdon` |
| PA-ENT-17 | Asunto de 500 caracteres y remitente de 400 | Registrar | Recortados a 400 y 320 |
| PA-ENT-18 | Sin `messageid`, Email `11111111-…` | Registrar | Huella `bdc7ce0b…99340` |
| PA-ENT-19 | Regarding a una cuenta | Registrar | Se trata como sin Regarding; se crea la conversación y el Email se re-apunta |
| PA-ENT-20 | La BU no se resuelve | Registrar | Excepción `CARP-CFG-001`; sin escrituras |
| PA-ENT-21 | Cualquier correo | Registrar | Ninguna traza contiene asunto, remitente ni cuerpo |
| PA-ENT-22 | El step recibe un `Create` | Ejecutar | Llama a `Registrar` con el id del `Target`, no con datos del `Target` |
| PA-ENT-23 | Cualquier correo | Registrar | Todas las escrituras con el servicio de SYSTEM |

### 2.11 Respuesta enviada (inv. 7.11; RF-03, RF-04; D-05; def. ítem 3)

#### `RespuestaEnviadaStep` — Update · **PostOperation · asíncrono** · orden 10 · filtro `statuscode` · sin imagen · tabla `email`

Con SYSTEM:

1. Releer el Email: `directioncode`, `statuscode`, `regardingobjectid`, `senton`, `actualend`, `createdby`, `sanic_respondidoporid`. Continuar solo si es **saliente**, `statuscode = Sent`, Regarding a una Conversación y `sanic_respondidoporid` **vacío** (esa columna es la marca de "ya procesado": idempotente ante reintentos). Si no, terminar. Un envío fallido (Failed), en cola o borrador no hace nada: `sanic_sinrespuestadesde` no se vacía (def. ítem 3).
2. Instante del envío = `senton`; si es nulo, `actualend`.
3. Leer la conversación. Si está Unida, seguirla hasta una activa (como en 2.10) y re-apuntar el Email.
4. `TiemposDeConversacion.AplicarRespuestaEnviada` (2.0.6).
5. Escribir el Email: `sanic_respondidoporid = createdby` (DC-12; siempre, aunque no haya tiempo) y `sanic_tiemporespuesta` (si hay).
6. Escribir la conversación (solo si hay cambios): `sanic_cantidadrespuestas`, `sanic_sinrespuestadesde`, `sanic_tiempoprimerarespuesta`.

Una respuesta enviada a una conversación Finalizada (por ejemplo, una cortesía) suma al contador y no toca los pendientes ni el indicador (decidido el 2026-10-10, OA-10, opción A). Para marcar un correo como respondido, el oficial reabre la conversación. Una conversación Unida se redirige al destino (paso 3).

| ID | Dado | Cuando | Entonces |
|---|---|---|---|
| PA-RES-01 | Abierta, `sinrespuestadesde` 10:00, cantidad 0, primera vacía; respuesta Sent con `senton` 10:45 creada por el Oficial O | Procesar | Email: `tiemporespuesta = 45`, `respondidoporid = O`; conversación: `sinrespuestadesde` vacío, cantidad 1, primera 45 |
| PA-RES-02 | Segunda respuesta: pendiente 11:00, `senton` 11:20, primera 45, cantidad 1 | Procesar | Email 20; conversación cantidad 2, primera 45, pendiente vacío |
| PA-RES-03 | Pendiente vacío (un recordatorio) | Procesar | Email sin tiempo pero con `respondidoporid`; cantidad +1; primera sin cambio |
| PA-RES-04 | La primera respuesta fue sin pendiente (primera vacía) y la segunda tiene pendiente | Procesar ambas | La primera se fija con la segunda (DC-11) |
| PA-RES-05 | Pendiente 10:50, `senton` 10:45 | Procesar | Sin tiempo; pendiente 10:50; cantidad +1 |
| PA-RES-06 | `statuscode` Failed, Pending Send, Sending, Draft | Procesar | No hace nada |
| PA-RES-07 | `sanic_respondidoporid` ya informado | Procesar | No hace nada |
| PA-RES-08 | Correo entrante; correo sin Regarding; Regarding a una cuenta | Procesar | No hace nada |
| PA-RES-09 | Conversación Finalizada con pendiente 10:00 | Procesar | Cantidad +1; Email con `respondidoporid` y sin tiempo; pendiente 10:00; indicador sin cambio (OA-10) |
| PA-RES-10 | Regarding a una Unida con destino Abierto | Procesar | Se aplica al destino y el Email se re-apunta |
| PA-RES-11 | `senton` nulo, `actualend` 10:45 | Procesar | Usa `actualend` |
| PA-RES-12 | `senton` 10:45:59 y pendiente 10:00:00 | Procesar | 45 minutos |
| PA-RES-13 | `createdby` = O y el job corre como SYSTEM | Procesar | `respondidoporid = O` |
| PA-RES-14 | Escrituras | Procesar | Con el servicio de SYSTEM; el Email se escribe antes que la conversación |

---

## 3. Máquinas de estado

Los estados son `statecode`/`statuscode` del diccionario §3.2 (0.7). Las transiciones las impone el plugin (dicc. §3.2, D-30), no la plataforma. "Gesto" es lo que el usuario hace y cómo lo reconoce el servidor (DC-04, DC-05, DC-06).

### 3.1 Conversación (D-03, D-06, RF-14, RF-15)

```
   (entrada de correo, sistema)
              │
              ▼
        ┌──────────┐  Finalizar (resultado; motivo y detalle si Sin respuesta)  ┌────────────┐
        │ Abierta  │ ─────────────────────────────────────────────────────────► │ Finalizada │
        │  0 / 1   │ ◄───────────────────────────────────────────────────────── │   1 / 2    │
        └──────────┘                   Reabrir                                   └────────────┘
              │                                                                       │
              │ Unir conversaciones (API, sistema)                                    │ Iniciar conversación nueva (API):
              ▼                                                                       │ la conversación sigue Finalizada
        ┌──────────┐                                                                  ▼
        │  Unida   │  (terminal; sin retorno)                                  (no cambia de estado)
        │ 1/159460001
        └──────────┘
```

| De | A | Quién | Gesto y cómo se reconoce | Columnas que escribe el usuario | Columnas que escribe el sistema | Error si no |
|---|---|---|---|---|---|---|
| ∅ | Abierta | Sistema (2.10, 1.5) | Alta | — | huella, fechaprimercorreo, asunto, remitente, sinrespuestadesde, cantidad 0, indicador no, cliente (si hay uno), propietario | — |
| Abierta | Finalizada | Oficial o Jefe | **Finalizar**: `statecode = 1` (con o sin `statuscode = 2`) | `sanic_resultado`; si es Sin respuesta, `sanic_motivosinrespuesta` y `sanic_detallesinrespuesta` | `finalizadoporid`, `fechafinalizacion`, `tiempototal`, `sinrespuestadesde = null`; motivo y detalle `null` si el resultado no es Sin respuesta | `CNV-003`, `CNV-004` |
| Finalizada | Abierta | Oficial o Jefe | **Reabrir**: `statecode = 0` (con o sin `statuscode = 1`: sin él, el motivo por defecto es Abierta, V-5). **Sin otros cambios en el mismo guardado** (`CNV-010`, 2026-10-11, D-40) | — | `resultado`, `motivo`, `detalle`, `fechafinalizacion`, `finalizadoporid`, `tiempototal` = null; indicador no; `sinrespuestadesde` **se conserva** | `CNV-005`, `CNV-010` |
| Abierta | Unida | Solo la API de unir (sistema) | `unirconversaciones` | — | `unidaaid`, `fechafinalizacion`, `finalizadoporid` (actor), `sinrespuestadesde = null` | `CNV-006` para un usuario |
| Finalizada | Finalizada | API iniciar nueva (sistema) | `iniciarconversacionnueva` | — | indicador `no`, `sinrespuestadesde = null` (no es cambio de estado) | `CNV-001` para un usuario |
| Unida | cualquiera | nadie | — | — | — | `CNV-002` |
| Abierta | Abierta | cualquiera | edición libre | `sanic_clienteid`; asignación (2.4) | — | `CNV-005` si trae resultado, motivo o detalle |
| Finalizada, Unida | la misma | nadie (solo SYSTEM) | cualquier cambio de columnas de una persona, incluidos `sanic_clienteid`, `sanic_nombre` y `sanic_oficialasignadoid` (decisión del 2026-10-11); el único gesto es Reabrir una Finalizada | — | — | `CNV-009` |

Asignación (columna `sanic_oficialasignadoid`, no cambia el estado): vacío → uno mismo lo hace cualquier usuario (API Tomar); todo otro cambio, solo Jefatura (2.4).

### 3.2 Arreglo (D-10, D-25, RF-12, RF-16)

```
                    Generar formato (API: congela)                     Formalizar
   ∅ ──alta──► Borrador ◄──────────────── Borrador ───────────────────────────────► Vigente
               (1, sin congelar)  Desbloquear  (1, congelado)   (solo desde congelado)  (159460001, congelado)
                    │                                                │                 │   │
                    │ Descartar (motivo)                             │ Descartar       │   │ Anular (motivo)
                    ▼                                                ▼                 │   ▼
               Descartado (1/159460003)  ◄───────────────────────────┘          Anulado (1/159460002)
                                                                                       │
        Todas las cuotas con pago (sistema) ──► Terminado (1/2) ◄─────────────── Vigente┘
                                                  │
                                      Reabrir (Jefatura, motivo)
                                                  ▼
                                        Vigente (sin congelar) ──► Generar formato ──► congelado otra vez

        Vigente congelado ──Reabrir (Jefatura, motivo)──► Vigente sin congelar
```

Anulado y Descartado son finales: no se reabren ni se reactivan (D-10). **Desde el 2026-10-11 (V-2 y V-5)** esto lo garantiza solo el plugin: la plataforma acepta un PATCH de una persona que reactive un Anulado o un Descartado, o que reabra un Terminado, así que toda transición de estado hecha por una persona con un PATCH directo (sin el gesto ni la Custom API) pasa por esta máquina de estados en el step (S-17, S-21, S-26) y se rechaza si no está en la tabla. Un `SetStateRequest` llega como `Update` anidado de profundidad 2 y recibe el mismo trato (V-1).

| De | A | Quién | Gesto y cómo se reconoce | Columnas que escribe el usuario | Columnas que escribe el sistema | Error si no |
|---|---|---|---|---|---|---|
| ∅ | Borrador | Oficial | Alta (siempre Borrador) | datos del acuerdo | versión Vigente, canal (si aplica), remanente, total, fechafin, reaperturas 0, propietario; cuotas | `ARR-001` a `-009` |
| Borrador (sin congelar) | Borrador (congelado) | Oficial o Jefe | **Generar formato** (API) | — | `fechacongelamiento`, 4 instantáneas, `numeroprovisional` | `GEN-*` |
| Borrador (congelado) | Borrador (sin congelar) | Oficial | **Desbloquear**: `fechacongelamiento = null` | `fechacongelamiento` solo a nulo | 4 instantáneas y `numeroprovisional` = null | `ARR-017`, `ARR-018` |
| Borrador (congelado) | Vigente | Oficial | **Formalizar**: estado efectivo Vigente | — | `numeroarreglo`, `formalizadoporid`, `fechaformalizado` | `ARR-019`, `ARR-020`, `ARR-023` |
| Borrador | Descartado | Oficial | **Descartar** | `sanic_motivodescarte` | `descartadoporid`, `fechadescarte`, `fechatermino` | `ARR-013` |
| Vigente | Anulado | Oficial | **Anular** | `sanic_motivoanulacion` | `anuladoporid`, `fechaanulacion`, `fechatermino` | `ARR-014` |
| Vigente | Terminado | Sistema (2.7) | Todas las cuotas con `fechapago` | — | `statecode/statuscode`, `fechatermino` | `ARR-010` para un usuario |
| Terminado | Vigente (sin congelar) | **Jefatura** | **Reabrir**: estado efectivo Vigente; el `Target` debe traer `statecode = 0` **y** `statuscode = Vigente` (V-5; con solo `statecode = 0` sería un Terminado → Borrador) | `sanic_motivoreapertura` | `reabiertoporid`, `fechareapertura`, contador +1, `fechatermino`, congelamiento, instantáneas y provisional = null | `ARR-015`, `ARR-016`; `ARR-012` si trae otro cambio real (D-40, 2026-10-11) |
| Vigente (congelado) | Vigente (sin congelar) | **Jefatura** | **Reabrir**: `sanic_motivoreapertura` | `sanic_motivoreapertura` | Igual, sin cambio de estado | `ARR-015`, `ARR-016`, `ARR-017`; `ARR-012` si trae otro cambio real, también `sanic_conversacionid` (D-40, 2026-10-11) |
| Vigente | Borrador | nadie | — | — | — | `ARR-010` |
| Terminado, Anulado, Descartado | cualquiera (salvo Reabrir un Terminado) | nadie | — | — | — | `ARR-022` (el paso 1 de 2.6 gana; inconsistencia 16, resuelta el 2026-10-11; ~~Vigente, Terminado → Borrador: `ARR-010`~~) |
Qué se puede editar según el estado:

| Estado | Datos del acuerdo | Cuotas (monto, fechas) | Pagos de cuota |
|---|---|---|---|
| Borrador sin congelar | sí (recalcula montos y calendario) | solo las reemplaza el sistema | no (`CUO-002`) |
| Borrador congelado | no (`ARR-011`) | no | no |
| Vigente congelado | no | no | sí |
| Vigente sin congelar (reabierto) | sí; calendario solo si no hay pagos (`ARR-021`) | solo las reemplaza el sistema | sí |
| Terminado, Anulado, Descartado | no (`ARR-022`) | no | no (`CUO-002`) |

### 3.3 Versión de plantilla (D-14, D-15; dicc. DD-03)

```
 ∅ ──alta──► Borrador ──Publicar (Jefe: valida mapeo, calcula huella)──► Vigente ──(otra se publica; sistema)──► Histórica
              (editable)                                                (inmutable)                             (inmutable)
```

| De | A | Quién | Gesto | Error si no |
|---|---|---|---|---|
| ∅ | Borrador | Jefe | Alta (siempre Borrador) | `VER-001`, `VER-002`, `VER-012` |
| Borrador | Vigente | Jefe | **Publicar**: `sanic_estado = Vigente` | `VER-005` a `VER-010`, `VER-013` |
| Vigente | Histórica | Sistema, al publicar otra | — | `VER-003` para un usuario |
| ~~Borrador, Histórica~~ | ~~desactivar (`statecode`)~~ | ~~Jefe~~ | ~~Desactivar el catálogo~~ | ~~Histórica sí; Vigente no (`VER-003`)~~ |
| cualquiera | desactivar o reactivar (`statecode`/`statuscode`) | solo SYSTEM (decisión del 2026-10-11, reemplaza la fila tachada) | — | `VER-014` en Borrador; `VER-003` en Vigente e Histórica |
| cualquiera | cualquiera otra | nadie | — | `VER-003`, `VER-004` |

**El catálogo se retira pasando a Histórica, no desactivándose (decisión del arquitecto, 2026-10-11, D-40; consecuencia de D-39).** Como el `statecode` nativo de la versión y el de sus mapeos solo lo cambia SYSTEM y ningún proceso del diseño lo cambia, una versión (con sus mapeos) deja de usarse porque otra se publica y ella pasa a Histórica (`sanic_estado`, paso del sistema, fila "Vigente → Histórica"). Nunca se purga (REL-07 es restringida). `definicion.md` §6 Datos y el diccionario §4.5 dicen lo mismo desde esa fecha. Con esto queda sin pendiente la falta de actor para desactivar el catálogo (inconsistencia 17).

---

## 4. Estructura del código (inv. 7.1, 7.9, 7.10; conv. §5; M-PPP §8)

### 4.1 Solución y proyectos

| Proyecto | Destino | Qué es |
|---|---|---|
| `src/Sanic.Carp.sln` | — | Solución (conv. §5) |
| `src/Sanic.Carp.Plugins/` | `net462;net8.0` | Ensamblado y paquete `sanic_carp_pkg_plugins`. Sin firmar. Dependencia: `DocumentFormat.OpenXml` 2.20.0 (spike 1 hallazgo 11; la 3.x queda para revisar). `TreatWarningsAsErrors` |
| `src/Sanic.Carp.Plugins.Tests/` | `net8.0` | xUnit. Sin FakeXrmEasy (BP-PP-159). Corre en el puesto Linux con `dotnet test src/Sanic.Carp.Plugins.Tests` |

```
src/Sanic.Carp.Plugins/
├── Dominio/                    PURO: ningún `using Microsoft.Xrm.Sdk`
│   ├── Choices.g.cs            generado por herramientas/codigo/generar_enums.py (no se edita)
│   ├── Estados.cs              EstadoDeConversacion, EstadoDeArreglo, EstadoEfectivo (DC-06)
│   ├── Columnas.cs             categorías de columnas por tabla (2.0.8)
│   ├── Cambios.cs              "cambió" = presente y distinto de la pre-imagen (DC-01)
│   ├── Normalizacion.cs        NormalizadorDeTexto (2.0.1)
│   ├── HuellaDeMessageId.cs    (2.0.2)
│   ├── Montos/ReglasDeMontos.cs                (2.0.3)
│   ├── Calendario/GeneradorDeCalendario.cs     (2.0.4)
│   ├── ConsecutivoDeCredito.cs                 (2.0.5)
│   ├── Tiempos/TiemposDeConversacion.cs, RecalculoDeConversacion.cs   (2.0.6)
│   ├── Conversacion/ReglasDeConversacion.cs    Finalizar, Reabrir, asignación (2.3, 2.4)
│   ├── Arreglo/ReglasDeArreglo.cs, PedidoDeActualizacion.cs, ResultadoDeEvaluacion.cs   (2.6)
│   ├── Cuota/ReglasDeCuota.cs                  (2.7)
│   ├── Plantilla/ValidadorDePublicacion.cs, TablaDeCompatibilidad.cs   (2.0.7)
│   ├── Errores/ErrorDeNegocio.cs, CodigosDeError.cs   código + texto de cada CARP-*
│   └── Puertos/                interfaces (4.2)
├── Formato/                    PURO (usa OpenXml, no Xrm): generador del F-NIC-0000052 (4.3)
├── Datos/                      Xrm: implementa los puertos sobre IOrganizationService
├── Infraestructura/            Xrm: IdentidadDeEjecucion, ConfiguracionDelArea, ContextoDeStep, BaseDeStep, Reloj
├── Api/                        las 5 Custom API (plugin de respaldo + clase de lógica cada una)
└── Steps/                      los steps de la sección 5

src/Sanic.Carp.Plugins.Tests/
├── Aceptacion/                 una clase por pieza, con los PA-* de este documento (las escribe el arquitecto)
│   └── Formato/                las pruebas del spike 1, portadas (4.3)
├── Unitarias/<Pieza>/          pruebas del constructor para lo que la aceptación no cubre
├── Dobles/                     OrganizationServiceEnMemoria, PipelineSimulado, … (4.5)
├── Apoyo/                      constructores de datos, contexto simulado, lector de fixtures
└── Fixtures/                   plantilla v07 y respuestas reales exportadas de Dev (4.5)
```

Dependencias permitidas: `Steps`, `Api` → `Infraestructura`, `Datos`, `Dominio`, `Formato`. `Datos` → `Dominio`. `Dominio` → `Formato` solo para `ValidadorDePublicacion`. Nada del dominio ni de `Formato` conoce Dataverse. Solo `Api`, `Steps`, `Datos` e `Infraestructura` tocan `IOrganizationService`.

Reglas de código de la orden del constructor, que este diseño asume: comparaciones ordinales, cultura invariante, la hora entra por parámetro, sin APIs que no existan en net462, textos de usuario con ortografía completa, comentarios que explican el porqué con la cita.

Regla de datos: **sin joins** (`link-entity`) en las consultas de los repositorios. El doble de pruebas no los simula y una consulta con join pasa en la prueba y falla en el entorno (M-PPP §1 paso 6). La variable de entorno se lee en dos consultas (definición y valor), no con el `link` de `seguridad.md`.

### 4.2 Puertos (en `Dominio/Puertos`) y su implementación

| Puerto | Métodos (nombre y semántica) | Implementa |
|---|---|---|
| `IReloj` | `AhoraUtc` | `Infraestructura/Reloj` |
| `IIdentidadDeEjecucion` | `ActorId`, `EsSistema`, `EsEscrituraDelSistema` (= `EsSistema`), `EsJefatura()` (0.3) | `Infraestructura/IdentidadDeEjecucion` |
| `IConfiguracionDelArea` | `BuId`, `TeamPropietarioId`, `TeamJefaturaId`, `QueueRemitenteId`. Perezosos; lanzan `ErrorDeNegocio` `CARP-CFG-*` | `Infraestructura/ConfiguracionDelArea` |
| `IConversaciones` | `Leer(id)`, `BuscarPorHuella(huella)`, `Crear(nueva)`, `Actualizar(id, cambios, conVersion)`, `HayUnidasA(id)`, `SeguirUnida(id, saltos)` | `Datos/ConversacionesDataverse` |
| `ICorreos` | `Leer(id)`, `DeConversacion(conversacionId)`, `EntrantesSinConversacion(buId, antesDe, tope)`, `BuscarPorMessageId(variantes)`, `ApuntarA(emailId, conversacionId)`, `AsignarAlTeam(emailId, teamId)`, `EscribirRespuesta(emailId, tiempo, respondidoPor)` | `Datos/CorreosDataverse` |
| `IArreglos` | `Leer(id)`, `NumerosDelCredito(producto, numeroProducto)`, `VersionesVigentes()`, `Actualizar(id, cambios)`, `CongelarYBitacora(...)` | `Datos/ArreglosDataverse` |
| `ICuotas` | `Listar(arregloId)`, `Crear(cuota)`, `Borrar(id)`, `HayPagos(arregloId)`, `QuedaAlgunaSinPago(arregloId)` | `Datos/CuotasDataverse` |
| `IClientes` | `BuscarPorCedula(cedulaNormalizada)` (siempre con SYSTEM, incluye inactivos), `BuscarActivosPorCorreo(correo, tope)`, `Leer(id)` | `Datos/ClientesDataverse` |
| `IVersiones` | `Leer(id)`, `OtrasVigentes(exceptoId)`, `Actualizar`, `FilasDeMapeo(versionId)`, `DescargarArchivo(versionId)` | `Datos/VersionesDataverse` |
| `IUsuarios` | `NombreCompleto(id)` | `Datos/UsuariosDataverse` |
| `IGeneradorDeFormato` | `Generar(plantilla, huellaEsperada, mapeo, datos)` → `ResultadoGeneracion`; `CalcularHuella(bytes)` | `Formato/GeneradorOpenXml` |

Los métodos de `Datos` son delgados: una consulta o una escritura, con `ColumnSet` nombrado. Los repositorios que necesitan "como sistema" o "como llamador" reciben el servicio por constructor; `Api` y `Steps` deciden cuál (0.2). Cuanto más fina sea esta capa, más barato es el TDD (M-PPP §8).

### 4.3 Generador del formato (inv. 7.8)

Se **porta** `spikes/plantilla/src/ArreglosPago.Formato/` a `src/Sanic.Carp.Plugins/Formato/`, namespace `Sanic.Carp.Plugins.Formato`: `GeneradorFormato`, `HojaEditable`, `EstilosInfo`, `ReferenciaCelda`, `ValidadorMapeo`, `Campos`, `DatosArreglo`, `MapeoCelda`, `ResultadoGeneracion`, `Excepciones`. Cambios respecto del spike, y solo esos:

1. Agregar la clave `moneda` (opción 27 de `CampoMapeable`, simple) a la lista de campos conocidos si el generador la valida.
2. Namespace y visibilidad: lo que el spike marca `public` y el paquete no necesita fuera del ensamblado pasa a `internal`, salvo lo que usan las pruebas (`InternalsVisibleTo`).
3. `ClavesDelGenerador` (opción ⇒ clave, 1.1) y `DatosDePrueba` (datos sintéticos de la publicación, 2.8) son nuevos.
4. `GeneradorOpenXml : IGeneradorDeFormato` es el adaptador. Registra `InnerException` antes de convertir (spike 1 hallazgo 6).

Las 88 pruebas y 205 casos del spike se portan a `Aceptacion/Formato/`, con las cinco pruebas de la plantilla v07 como fixture (`Fixtures/F-NIC-0000052_v07.xlsx`, copia de `spikes/plantilla/F-NIC-0000052_v07_borrador.xlsx`). Más: una prueba de la clave `moneda` y la mutación equivalente a M07 se descarta por equivalente (spike 1 §4). Quedan pendientes, como en el spike, correrlas en `net462` en Windows y abrir un archivo generado en Excel de escritorio (spike 1b, filas 4 y 16).

### 4.4 Firmas públicas de las piezas puras (lo que escribe el arquitecto como `NotImplementedException`)

| Clase | Miembros |
|---|---|
| `NormalizadorDeTexto` (estática) | `NumeroDeProducto(string)`, `Cedula(string)`, `Correo(string)`, `NombreCompleto(string)`, `Celular(string)` → `string` |
| `HuellaDeMessageId` (estática) | `Normalizar(string)` → `string`; `Calcular(string messageId, Guid emailId)` → `string` |
| `ReglasDeMontos` (estática) | `Calcular(decimal saldoInicial, decimal montoAbonado, decimal cuotaMensual, int plazo)` → `ResultadoDeMontos { bool Ok; ErrorDeNegocio Error; decimal SaldoRemanente; decimal TotalPagar; decimal MontoUltimaCuota; IReadOnlyList<decimal> MontosDeCuotas() }` |
| `GeneradorDeCalendario` (estática) | `Generar(DateTime fechaInicio, int plazo, decimal cuotaMensual, decimal montoUltimaCuota)` → `IReadOnlyList<CuotaDeCalendario { int Numero; DateTime FechaVencimiento; decimal Monto }>`; `FechaFin(...)` |
| `ConsecutivoDeCredito` (estática) | `Siguiente(IEnumerable<int?> numeros)` → `int` o `ErrorDeNegocio` |
| `TiemposDeConversacion` (estática) | `MinutosEntre(DateTime desde, DateTime hasta)` → `int`; `AplicarRespuestaEnviada(...)` → `ResultadoDeRespuesta`; `RecalcularTrasUnion(...)` |
| `ReglasDeConversacion` (estática) | `Evaluar(PedidoDeConversacion)` → `ResultadoDeEvaluacion { bool Permitida; ErrorDeNegocio Error; IReadOnlyDictionary<string, object> Escribir }` |
| `ReglasDeArreglo` (estática) | `EvaluarAlta(PedidoDeAlta, IReadOnlyList<VersionVigente>)`, `EvaluarActualizacion(PedidoDeActualizacion, IConsecutivos, IJefatura)` → `ResultadoDeEvaluacion` |
| `ReglasDeCuota` (estática) | `Evaluar(PedidoDeCuota)` → `ResultadoDeEvaluacion` |
| `ValidadorDePublicacion` (estática) | `Validar(int capacidad, IReadOnlyList<FilaDeMapeo>)` → `IReadOnlyList<ErrorDeNegocio>` |
| `Cambios` (estática) | `Distinto(Entity o equivalente neutro)`: se define sobre un `IReadOnlyDictionary<string, object>` para no depender del SDK; compara `null`, `OptionSetValue`→int, `EntityReference`→Guid, `DateTime`, `decimal`, texto ordinal |
| `ErrorDeNegocio` | `Codigo` (`"CARP-ARR-004"`), `Mensaje`, `ToString()` = `"CARP-ARR-004: …"` |
| `CodigosDeError` | una constante con código y texto por cada fila de error de este documento |

El dominio no usa `Entity`. Los steps convierten `Entity` ⇄ diccionario neutro en `Infraestructura` (`ConversorDeEntidades`).

### 4.5 Dobles de prueba (inv. 7.9; `pruebas.md`; M-PPP §8)

Todos en `Sanic.Carp.Plugins.Tests/Dobles` y `Apoyo`. Los datos de prueba nunca son datos reales (def. L411).

| Doble | Qué hace |
|---|---|
| `OrganizationServiceEnMemoria : IOrganizationService` | Guarda `Entity` en un diccionario por tabla. Implementa `Create`, `Retrieve`, `Update`, `Delete`, `Associate`/`Disassociate` (no usados), `RetrieveMultiple` con `QueryExpression` y `FetchExpression` mínimo (no usados), y `Execute` para: `UpdateRequest` (con `ConcurrencyBehavior`), `AssignRequest`, `WhoAmIRequest`, `RetrievePrincipalAccessRequest` (tabla de accesos configurable), `InitializeFileBlocksDownloadRequest` y `DownloadBlockRequest` (bloques de 4 MB sobre un diccionario de archivos). Condiciones: `Equal`, `NotEqual`, `In`, `Null`, `NotNull`, `GreaterThan`, `GreaterEqual`, `LessThan`, `LessEqual`; `OrderExpression`; `TopCount`; `PageInfo` con cookie. Sin joins |
| Versión de fila | Cada `Create`/`Update` sube un `RowVersion`. `UpdateRequest` con `IfRowVersionMatches` y versión vieja lanza `FaultException<OrganizationServiceFault>` con el código de "versión de fila no coincide" (a confirmar con una muestra real, spike 2 pregunta 3) |
| Identidad por servicio | `FabricaDeServiciosSimulada : IOrganizationServiceFactory` entrega vistas del mismo almacén marcadas con la identidad (`CreateOrganizationService(userId)`; `null` = SYSTEM). Cada escritura queda en `Escrituras` con (operación, tabla, id, columnas, identidad, instante). Así se prueba "escribe con SYSTEM" y "lee con el llamador" |
| Seguridad simulada | `NegarLectura(userId, tabla, id)`; `OcultarColumna(tabla, columna, paraUsuario)` devuelve `null` al usuario y el valor real a SYSTEM (cédula sin perfil de columna, spike 4). **V-6 (2026-10-11):** en la **pre-imagen** de un step que corre con ese usuario la columna oculta debe estar **ausente** (no en nulo), como en la plataforma |
| Fallas | `FallarEn(operacion, tabla, ordinal, codigoDeError)` lanza una `FaultException` en la n-ésima llamada |
| Transacción | `Instantanea()` y `Restaurar()`. El ayudante `EjecutarComoPlataforma(plugin, contexto)` toma la instantánea, ejecuta y, si hay excepción, restaura: es la transacción de la plataforma |
| Registro de consultas | `Consultas`: tabla, condiciones, columnas pedidas, `TopCount`. Permite afirmar "0 consultas de BU", "una sola consulta de jefatura", "sin `ColumnSet(true)`" |
| Registros inactivos | Bandera `RechazarEscriturasEnInactivas`. **Verificado el 2026-10-11 (V-2): el comportamiento real de la plataforma es `false`** (no bloquea escrituras en filas inactivas, ni de SYSTEM ni de una persona) y el doble debe quedar así por defecto; ninguna prueba lo pone en `true`. La bandera existe solo para que una prueba declare que depende de V-2 |
| `ContextoDePluginSimulado : IPluginExecutionContext` | `MessageName`, `Stage`, `Mode`, `Depth`, `UserId`, `InitiatingUserId`, `PrimaryEntityName`, `PrimaryEntityId`, `InputParameters` (con `Target`), `OutputParameters`, `PreEntityImages`, `OperationCreatedOn`, `ParentContext` |
| `ProveedorDeServiciosSimulado : IServiceProvider` | Entrega el contexto, la fábrica y un `ITracingService` que guarda las líneas |
| `PipelineSimulado` | Registra instancias de los steps (clase, mensaje, tabla, etapa, orden, filtro, pre-imagen) y, cuando un servicio escribe, ejecuta los steps que corresponden **con el contexto que la plataforma daría**: `UserId` e `InitiatingUserId` = SYSTEM si la escritura salió del servicio de SYSTEM, `Depth + 1`. Respeta el filtro de atributos. Sirve para las pruebas de cadena (la trampa A-23) y para comprobar que las escrituras del sistema no son rechazadas por nuestros propios steps |
| `RelojFijo : IReloj` | Hora configurable |
| `IdentidadSimulada` | Para pruebas del dominio y de steps: `ActorId`, banderas, `EsJefatura` configurable, contador de consultas |
| `ConfiguracionSimulada` | Variables de entorno en un diccionario; BU, teams y queue sembrados; contadores de consultas |
| `GeneradorFalso : IGeneradorDeFormato` | Devuelve bytes fijos o lanza la excepción pedida; guarda los datos y el mapeo recibidos |
| `ConstructorDeDatos` | Constructores fluidos: `UnaConversacion()`, `UnArreglo()`, `UnaCuota()`, `UnCorreoEntrante()`, `UnaVersion()`, `UnMapeo()` con los valores por defecto del diccionario |

**Validación contra respuestas reales** (`pruebas.md` §1; defecto D-59 de M-PPP: 1104 pruebas en verde con un doble que repetía el mismo error del código). Antes de dar por buenos los dobles, se exportan de Dev estos fixtures JSON y una prueba (`DobleContraRespuestasRealesAceptacion`) comprueba que el doble devuelve **los mismos tipos**: `OptionSetValue`, `EntityReference`, `DateTime` (UTC y solo fecha), `decimal`, `Guid` crudo en intersect (`teammembership`), `AliasedValue`:

1. `email` entrante (Received) y `email` saliente (Sent) con `statuscode`, `directioncode`, `messageid` con `<>`, `inreplyto`, `actualend`, `senton`, `createdby`, `regardingobjectid`, `ownerid`.
2. `sanic_carp_tbl_conversacion` en Abierta, Finalizada y Unida.
3. `sanic_carp_tbl_arreglo` en Borrador congelado y Vigente.
4. `teammembership` de un usuario; `team` con `isdefault`; `queue` con `emailaddress`.
5. `environmentvariabledefinition` y `environmentvariablevalue`.
6. El usuario SYSTEM (`WhoAmI` elevado) y un `systemuser` ordinario (`WhoAmI` del llamador).
7. El `FaultException` de una violación de K-01 y el de "versión de fila no coincide".

---

## 5. Lista cerrada de steps a registrar (inv. 8.7)

**35 registros** (los 35 firmes: ~~34 firmes y 1 condicionado a V-4~~; el 2026-10-11 V-4 se refutó y S-27 se rediseñó como `Update` con filtro `sanic_archivo`, que sí se puede registrar), sobre **20 clases** de step, todos en el paquete `sanic_carp_pkg_plugins`, tipo `Sanic.Carp.Plugins.Steps.<Clase>`. Las cinco Custom API no figuran: la plataforma crea su propio step interno al registrarlas (`Sanic.Carp.Plugins.Api.<Clase>Api`). Restricciones de la herramienta `step.py` respetadas (DC-27): un step por (clase, mensaje, tabla); filtro de atributos solo en `Update` y nunca la clave primaria; pre-imagen solo en `Update` y `Delete`; sin post-imágenes; asíncrono solo en PostOperation; etapas PreOperation y PostOperation.

Nombre del step: `STEP - CARP - <tabla> - <mensaje> - <etapa> - <clase>` (DC-23). Todos sincrónicos salvo los marcados. La pre-imagen siempre se llama `PreImagen`. `sanic_carp_tbl_` se abrevia `t_` en la columna Tabla de esta tabla; el playbook usa el nombre completo.

| # | Clase (`Steps.*`) | Mensaje | Tabla | Etapa | Orden | Filtro de atributos | Pre-imagen (sección) |
|---|---|---|---|---|---|---|---|
| S-01 | `PropietarioAlCrearStep` | Create | `t_cliente` | PreOperation | 5 | — | — |
| S-02 | `PropietarioAlCrearStep` | Create | `t_conversacion` | PreOperation | 5 | — | — |
| S-03 | `PropietarioAlCrearStep` | Create | `t_arreglo` | PreOperation | 5 | — | — |
| S-04 | `PropietarioAlCrearStep` | Create | `t_cuota` | PreOperation | 5 | — | — |
| S-05 | `PropietarioAlCrearStep` | Create | `t_versionplantilla` | PreOperation | 5 | — | — |
| S-06 | `PropietarioAlCrearStep` | Create | `t_mapeocelda` | PreOperation | 5 | — | — |
| S-07 | `PropietarioAlCrearStep` | Create | `t_generacionformato` | PreOperation | 5 | — | — |
| S-08 | `PropietarioAlActualizarStep` | Update | `t_cliente` | PreOperation | 5 | `ownerid` | — |
| S-09 | `PropietarioAlActualizarStep` | Update | `t_conversacion` | PreOperation | 5 | `ownerid` | — |
| S-10 | `PropietarioAlActualizarStep` | Update | `t_arreglo` | PreOperation | 5 | `ownerid` | — |
| S-11 | `PropietarioAlActualizarStep` | Update | `t_cuota` | PreOperation | 5 | `ownerid` | — |
| S-12 | `PropietarioAlActualizarStep` | Update | `t_versionplantilla` | PreOperation | 5 | `ownerid` | — |
| S-13 | `PropietarioAlActualizarStep` | Update | `t_mapeocelda` | PreOperation | 5 | `ownerid` | — |
| S-14 | `PropietarioAlActualizarStep` | Update | `t_generacionformato` | PreOperation | 5 | `ownerid` | — |
| S-15 | `ClienteStep` | Create | `t_cliente` | PreOperation | 10 | — | — |
| S-16 | `ClienteStep` | Update | `t_cliente` | PreOperation | 10 | `sanic_nombrecompleto,sanic_cedula,sanic_correo,sanic_celular,statecode,statuscode` (~~sin `statecode,statuscode`~~, 2026-10-11, D-40) | 2.2 (~~—~~) |
| S-17 | `ConversacionActualizarStep` | Update | `t_conversacion` | PreOperation | 10 | — (a propósito) | 2.3 |
| S-18 | `ConversacionAsignacionStep` | Update | `t_conversacion` | PreOperation | 20 | `sanic_oficialasignadoid` | `sanic_oficialasignadoid` |
| S-19 | `ArregloCrearStep` | Create | `t_arreglo` | PreOperation | 10 | — | — |
| S-20 | `ArregloCalendarioStep` | Create | `t_arreglo` | **PostOperation** | 10 | — | — |
| S-21 | `ArregloActualizarStep` | Update | `t_arreglo` | PreOperation | 10 | — (a propósito) | 2.6 |
| S-22 | `ArregloCalendarioStep` | Update | `t_arreglo` | **PostOperation** | 10 | `sanic_plazo,sanic_cuotamensual,sanic_fechainicio,sanic_saldoinicial,sanic_montoabonado` | — |
| S-23 | `CuotaActualizarStep` | Update | `t_cuota` | PreOperation | 10 | — (a propósito) | 2.7 |
| S-24 | `CuotaTerminarArregloStep` | Update | `t_cuota` | **PostOperation** | 20 | `sanic_fechapago,sanic_montopagado` | — |
| S-25 | `VersionPlantillaCrearStep` | Create | `t_versionplantilla` | PreOperation | 10 | — | — |
| S-26 | `VersionPlantillaActualizarStep` | Update | `t_versionplantilla` | PreOperation | 10 | — (a propósito) | 2.8 |
| S-27 | `ArchivoDePlantillaStep` | Update (~~`CommitFileBlocksUpload`~~, V-4) | `t_versionplantilla` | PreOperation | 20 (~~10~~) | `sanic_archivo` | — (~~condicionado a V-4~~, firme desde el 2026-10-11) |
| S-28 | `MapeoCeldaCrearStep` | Create | `t_mapeocelda` | PreOperation | 10 | — | — |
| S-29 | `MapeoCeldaActualizarStep` | Update | `t_mapeocelda` | PreOperation | 10 | — (a propósito) | 2.8 (con `statecode`, `statuscode` desde el 2026-10-11) |
| S-30 | `MapeoCeldaEliminarStep` | Delete | `t_mapeocelda` | PreOperation | 10 | — | `sanic_versionplantillaid` |
| S-31 | `RemitenteDelAreaStep` | Create | `email` | PreOperation | 10 | — | — |
| S-32 | `EmailSoloSistemaStep` | Create | `email` | PreOperation | 20 | — | — |
| S-33 | `EmailSoloSistemaStep` | Update | `email` | PreOperation | 10 | `sanic_tiemporespuesta,sanic_respondidoporid` | — |
| S-34 | `RegistrarCorreoEntranteStep` | Create | `email` | **PostOperation, asíncrono** | 10 | — | — |
| S-35 | `RespuestaEnviadaStep` | Update | `email` | **PostOperation, asíncrono** | 10 | `statuscode` | — |

Pre-imágenes con sus columnas, resumidas (el playbook las copia de 2.2, 2.3, 2.6, 2.7 y 2.8):

| Step | Columnas de `PreImagen` |
|---|---|
| S-17 | `statecode`, `statuscode`, `sanic_huellamessageid`, `sanic_fechaprimercorreo`, `sanic_asunto`, `sanic_remitente`, `sanic_sinrespuestadesde`, `sanic_correonuevoenfinalizada`, `sanic_resultado`, `sanic_motivosinrespuesta`, `sanic_detallesinrespuesta`, `sanic_fechafinalizacion`, `sanic_tiempoprimerarespuesta`, `sanic_tiempototal`, `sanic_cantidadrespuestas`, `sanic_unidaaid`, `sanic_finalizadoporid`, y desde el 2026-10-11 `sanic_clienteid`, `sanic_nombre`, `sanic_oficialasignadoid`, `ownerid` (21 en total) |
| S-16 | `statecode`, `statuscode` (agregada el 2026-10-11, D-40) |
| S-18 | `sanic_oficialasignadoid` |
| S-21 | las 43 de 2.6 (~~42~~; se suma `sanic_nombre`, 2026-10-11) |
| S-23 | `sanic_nombre`, `sanic_numero`, `sanic_fechavencimiento`, `sanic_monto`, `sanic_fechapago`, `sanic_montopagado`, `sanic_arregloid`, y desde el 2026-10-11 `statecode`, `statuscode` |
| S-26 | `statecode`, `statuscode` (agregada el 2026-10-11), `sanic_nombre`, `sanic_estado`, `sanic_capacidad`, `sanic_huella`, `sanic_fechaaprobacion`, `sanic_aprobadoporid` |
| S-29 | `sanic_versionplantillaid`, `sanic_hoja`, `sanic_celda`, `sanic_tipo`, `sanic_campo`, `sanic_filasmaximas`, y desde el 2026-10-11 `statecode`, `statuscode` (D-40) |
| S-30 | `sanic_versionplantillaid` |

Orden de ejecución por (mensaje, tabla), para verificar BP-PP-060: `t_conversacion` Update PreOperation: 5, 10, 20. `t_arreglo` Create Pre: 5, 10; Post: 10. `t_arreglo` Update Pre: 5, 10; Post: 10. `t_cuota` Update Pre: 5, 10; Post: 20. `t_cliente` Create Pre: 5, 10; Update Pre: 5, 10. `t_versionplantilla` Create Pre: 5, 10; Update Pre: 5, 10, 20 (el 20 es S-27, 2026-10-11). `t_mapeocelda` Create Pre: 5, 10; Update Pre: 5, 10. `email` Create Pre: 10, 20; Create Post async: 10; Update Pre: 10; Update Post async: 10. El de propietario (5) corre antes que el de reglas (10) para que ninguna regla vea un propietario ajeno.

Los pre-update de Arreglo y Cuota **no** llevan filtro para que nada pase sin evaluar (misma razón que M-PPP §5.1, "la lista blanca va sin filtro de atributos a propósito"). El costo es una ejecución barata por guardado: sin cambios en columnas vigiladas el step termina antes de consultar nada.

`Upsert`, `UpdateMultiple` y `CreateMultiple` no evitan estos controles: la plataforma funde los pipelines (M-PPP §5.1, cerrado con cita de Learn). Aplica a tablas estándar. **Si alguna tabla propia se convirtiera en elástica, las reglas se caen en silencio**; el diccionario las deja estándar (DD-09).

---

## 6. Decisiones abiertas

Solo lo que no se pudo deducir de las fuentes. **Estado al 2026-10-10: las doce están decididas** (el usuario aceptó todas las recomendaciones; ver D-37 en `definicion.md`). Cada una conserva su análisis como justificación y termina con su línea "Decidida el 2026-10-10". Donde la opción decidida cambió el comportamiento que el código tenía como provisional (OA-8, OA-11 y OA-12), el texto provisional queda tachado. Héctor Romero sigue siendo quien puede revisar las reglas de negocio del formato y de las cuotas (def. L639).

### OA-1. ¿Formalizar y desbloquear piden motivo?

**Qué dicen las fuentes.** def. L143 llama "gestos con motivo" a finalizar, formalizar, desbloquear, descartar, anular y reabrir. El diccionario solo tiene columnas de motivo para descartar, anular y reabrir (dicc. L389-393) y para finalizar con "Sin respuesta" (resultado, motivo, detalle). def. ítem 9 (L299-303) no pide motivo para formalizar ni desbloquear.

| Opción | Qué implica | Costo |
|---|---|---|
| A. Ni formalizar ni desbloquear piden motivo | Es lo que describe el ítem 9 y lo que modela el diccionario. Quién y cuándo formalizó queda en `sanic_formalizadoporid` y `sanic_fechaformalizado`; quién y cuándo desbloqueó queda en la auditoría nativa de `sanic_fechacongelamiento` (la columna está auditada, dicc. §4.3) | Cero. Se corrige el texto de L143 |
| B. Desbloquear pide motivo | Una columna nueva (por ejemplo `sanic_motivodesbloqueo`, memo 500, auditada), un cambio al gesto (DC-05) y una regla más en `ReglasDeArreglo`. Sirve para saber por qué se reimprime un formato | Columna nueva e irreversible, más fricción para el oficial en un paso previo a la firma, sin documento firmado en juego |
| C. Los dos piden motivo | B más una columna para formalizar | Lo mismo, sin beneficio claro: formalizar es un paso positivo que ya deja usuario y fecha |

**Recomendación: A.** El desbloqueo ocurre antes de firmar; el rastro de quién y cuándo ya existe. Cambiar a B después es barato (una columna y una regla) mientras no haya datos.

**Decidida el 2026-10-10: A.** Ni formalizar ni desbloquear piden motivo. Se corrige `definicion.md` (ítem de plugins, antes L143): los gestos con motivo son finalizar, descartar, anular y reabrir. Inconsistencia 1 de la sección 7 resuelta.

### OA-2. Vistas de "sin respuesta", de "llegados a finalizadas" y de borradores

**Qué dicen las fuentes.** def. L122 lista como vistas separadas "correos sin respuesta" y "correos llegados a conversaciones finalizadas"; def. ítem 4 (L276) describe una sola que reúne ambos casos. Además L122 lista "borradores" y L450 describe "borradores sin cambios en más de 30 días": no queda claro si es una vista o dos.

El servidor ya resuelve la mitad: por P-09 (dicc. L661), un correo que llega a una finalizada completa `sanic_sinrespuestadesde`, así que **"sinrespuestadesde no vacío" ya incluye los dos casos**.

| Opción | Detalle | Costo |
|---|---|---|
| A. Una sola vista | "Correos sin respuesta" = `sinrespuestadesde` no vacío, con el indicador visible como columna | El oficial no puede ir directo a las finalizadas con correo nuevo |
| B. Una unida para supervisión y una operativa | "Correos sin respuesta" (todo `sinrespuestadesde` no vacío, para el criterio de éxito 4 del jefe) más "Correos llegados a conversaciones finalizadas" (indicador = sí, para el oficial que decide reabrir o iniciar una nueva) | Dos vistas, una consulta cada una; sin código |
| C. Solo las dos separadas | La unión se hace a ojo | El jefe tendría que mirar dos vistas para comprobar el criterio 4 |

Borradores: (i) una vista "Borradores" y otra "Borradores sin movimiento" (más de 30 días sin cambios, para formalizar o descartar), o (ii) solo la segunda. **Recomendación: B para las conversaciones y (i) para los borradores**, más "Finalizadas sin respuesta" (resultado = Sin respuesta) como pide D-06. No hay impacto en el servidor.

**Decidida el 2026-10-10: B para las conversaciones y (i) para los borradores.** Cinco vistas: "Correos sin respuesta" (todo `sanic_sinrespuestadesde` no vacío, para el jefe), "Correos llegados a conversaciones finalizadas" (indicador = sí, para el oficial), "Borradores", "Borradores sin movimiento" (más de 30 días sin cambios) y "Finalizadas sin respuesta" (D-06). `definicion.md` y el inventario (12.1 a 12.6) quedan alineados. Inconsistencia 2 de la sección 7 resuelta.

### OA-3. Aviso cuando falla la purga

**Qué dicen las fuentes.** def. L453 dice "si falla, el flujo avisa", pero ningún flujo lo cubre (def. L152-155, L446-449; inv. 11 y su inconsistencia 5).

| Opción | Qué implica | Costo |
|---|---|---|
| A. Ampliar el flujo recurrente de salud (inv. 11.2) | Una cuarta comprobación: trabajos de eliminación masiva fallidos en las últimas 24 h. Hay que confirmar que `SR - CARP - Servicio` puede leerlos (la matriz no le da ese privilegio) | Un privilegio y una acción en un flujo que ya existe |
| B. Un flujo nuevo | Trigger sobre el trabajo del sistema | Un flujo más para mantener y una connection reference más |
| C. Revisión manual | El jefe o el equipo de automatización miran System Jobs una vez por semana (def. L453) | Cero código; el fallo se detecta tarde, y una purga fallida afecta RNF-07 y RNF-10 |
| D. Una sexta Custom API | `estadodepurgas` que lee los trabajos con SYSTEM y devuelve conteos | Contradice la lista de cinco (def. L145-150); innecesaria si A funciona |

**Recomendación: A**, y C como respaldo hasta que el flujo exista. Es una decisión de los flujos (inv. 11), no del código de este documento.

**Decidida el 2026-10-10: A, con C como respaldo.** El flujo recurrente de salud (inv. 11.2) suma la comprobación de trabajos de eliminación masiva fallidos en las últimas 24 h; hasta que el flujo exista, revisión manual semanal de System Jobs a cargo del equipo de automatización (decisión del usuario del 2026-10-10). Consecuencia pendiente: `SR - CARP - Servicio` probablemente necesite leer los trabajos de eliminación masiva; el privilegio **no está aplicado** y queda como duda D-09 de `04-matriz-privilegios.md`, a verificar en Dev al construir 11.2.

### OA-4. Cómo se promueve la plantilla de Test a Prod

**Qué dicen las fuentes.** def. L427: "con la herramienta de migración de configuración o una recarga controlada por el jefe (se decide en etapa 3)"; inv. 9 e inconsistencia 15.

Dato de este diseño que condiciona la elección: una versión **solo puede nacer en Borrador** (`CARP-VER-001`, DC-19) y solo pasa a Vigente por el gesto Publicar, que valida el mapeo, genera una prueba, calcula la huella y deja aprobador y fecha. La herramienta de migración de configuración escribiría la fila ya Vigente con su huella copiada: el step la rechaza (y con razón: saltaría la validación).

| Opción | Qué implica | Costo |
|---|---|---|
| A. Migración de configuración | Copia fila, archivo y mapeo | Choca con `CARP-VER-001`/`-002`. Habría que dejar entrar al sistema en Vigente sin validar, o aflojar el step. Pierde la prueba de publicación en Prod |
| B. Recarga controlada: script versionado | Un script del proyecto (`playbooks/datos/versionplantilla.md` y `mapeocelda.md`) crea la versión en Borrador en Prod, sube el archivo, carga las filas de mapeo y **el jefe publica** con el gesto normal. Se compara la huella resultante con la de Test: deben ser iguales (mismo archivo) | Un script y un paso manual del jefe; el mismo camino que usa el negocio, con todas las validaciones |
| C. Todo manual por el jefe | Sube el archivo y escribe 27 filas de mapeo a mano | Error humano en el mapeo de un documento firmado (riesgo "baja/alto", def. L625) |

**Recomendación: B.** Cumple el diseño sin excepciones, y la comparación de huellas prueba que el archivo es el mismo. El código ya seguía B (no tiene otro camino).

**Decidida el 2026-10-10: B.** Recarga controlada con un script versionado (`playbooks/datos/versionplantilla.md` y `mapeocelda.md`): crea la versión en Borrador en Prod, sube el archivo, carga el mapeo, y el jefe publica con el gesto normal; se compara la huella con la de Test. Se ajustan `definicion.md` (Datos de configuración) y el inventario 9; inconsistencia 15 del inventario resuelta.

### OA-5. Regla de fechas de vencimiento (R-03) y fecha de fin

**Qué dicen las fuentes.** def. ítem 8 (L298) y dicc. R-03: "mismo día de cada mes, tratamiento de fin de mes", a confirmar con Héctor; "centralizada en el plugin". `sanic_fechafin` es "fecha de la última cuota (el mockup la calcula con el plazo)" y no es solo-sistema.

| Opción | Detalle | Costo |
|---|---|---|
| A. Mismo día de cada mes; si el mes no tiene ese día, el último día del mes, calculado siempre desde la fecha de inicio | 31-ene, 28-feb, 31-mar, 30-abr. Es `AddMonths` desde el origen | Es la regla del cálculo financiero habitual; sin saltos |
| B. Igual, pero encadenando mes a mes | 31-ene, 28-feb, 28-mar… (el día se "pega" abajo) | El cliente ve fechas que se mueven; casi nunca es lo deseado |
| C. Día fijo 1 a 28 | Se obliga a elegir un día que existe en todos los meses | Cambia la fecha de inicio que el cliente acordó |
| D. Otra regla de negocio (feriados, días hábiles) | Requiere calendario | Más datos y reglas |

Sobre `fechafin`: (i) el plugin la **calcula y la sobrescribe** con el vencimiento de la última cuota, o (ii) el plugin **valida** que coincida y rechaza si no. **Recomendación: A y (i)** (un solo cálculo, sin que el oficial pueda dejar una fecha de fin que contradiga el calendario). Todo vive en `GeneradorDeCalendario`.

**Decidida el 2026-10-10: A y (i).** Mismo día de cada mes, contado siempre desde la fecha de inicio (`AddMonths` desde el origen); si el mes no tiene ese día, el último del mes. El plugin calcula y sobrescribe `sanic_fechafin` con el vencimiento de la última cuota. R-03 del diccionario queda decidida; la regla de negocio puede consultarse con Héctor Romero.

### OA-6. Más de un borrador del mismo crédito (R-07)

**Qué dicen las fuentes.** El spike 2 confirmó que la clave K-02 admite varios borradores con número vacío; la definición no dice si se permiten (dicc. R-07).

| Opción | Detalle | Costo |
|---|---|---|
| A. Se permiten | Sin regla. Dos propuestas distintas (por ejemplo temporal y cancelación) pueden convivir hasta que una se formalice | Un borrador olvidado queda vivo (lo cubre la vista de borradores sin movimiento, OA-2) |
| B. Se rechaza el segundo borrador | Una consulta en `ArregloCrearStep` por (producto, número de producto) con Borrador activo | Impide comparar dos propuestas; hay que descartar una |
| C. Se permite con aviso | Solo en el formulario | Sin garantía del servidor |

**Recomendación: A.** El formalizar ya protege el consecutivo (K-02 y el número provisional).

**Decidida el 2026-10-10: A.** Se permiten varios borradores del mismo crédito. R-07 del diccionario cerrada.

### OA-7. Cómo se imprimen la moneda, el tipo de arreglo y su celda

**Qué dicen las fuentes.** dicc. DD-11 y la opción 27 `Moneda` existen "para que el formato la imprima", pero (a) la v07 aprobada el 2026-10-09 (def. L68) no tiene una celda de moneda mapeada, (b) los formatos de monto de la plantilla muestran el símbolo C$ (dicc. L748 fila "Moneda"; spike 1), y (c) no hay texto fijado para imprimir: las etiquetas del choice van sin tildes (`Cordobas`, `Cancelacion`; BP-PP-197).

| Opción | Detalle | Costo |
|---|---|---|
| A. Texto formal con ortografía: "Córdobas" / "Dólares" y "Temporal" / "Cancelación" | Tabla de textos en `ClavesDelGenerador`, aparte de las etiquetas del choice | Un lugar más que mantener |
| B. Las etiquetas del choice tal cual (`Cordobas`, `Cancelacion`) | Cero código | Se imprime sin tilde en un documento firmado |
| C. Símbolo (`C$`, `US$`) | Hay que cambiar los formatos de las celdas de monto en la plantilla, no solo agregar una celda | Cambia la plantilla v07 (otra aprobación de Héctor) |

**Recomendación: A** para el texto, y que Héctor decida en qué celda va y si los formatos de monto cambian con la moneda (eso es un cambio de plantilla, no de código). Mientras no haya celda mapeada, la clave `moneda` viaja en los datos y no se imprime.

**Decidida el 2026-10-10: A.** Textos formales con ortografía en `ClavesDelGenerador`: "Córdobas" / "Dólares" y "Temporal" / "Cancelación". **Pendiente de Héctor Romero (no es una decisión abierta del código):** en qué celda se imprime la moneda y si cambian los formatos de monto; es un cambio de plantilla, no de código.

### OA-8. Qué es un "pago registrado"

**Qué dicen las fuentes.** def. ítem 11: el oficial registra "fecha y monto"; "todas sus cuotas tienen pago registrado" pasa el arreglo a Terminado; un pago parcial cuenta (R-02) y saca la cuota de la lista de riesgo (RF-11). D-11 y el spike 2 evalúan el riesgo con `fechapago` nulo. No dice si se exige el par ni si se valida el monto.

| Opción | Detalle | Costo |
|---|---|---|
| A. Pago registrado = `sanic_fechapago` no nulo; sin más validaciones | Coincide con la vista de riesgo (D-11) | Una fecha sin monto cuenta como pago |
| B. Fecha y monto juntos, ambos o ninguno, monto mayor que cero | Una regla en `ReglasDeCuota`; mensaje `CARP-CUO-003` | El oficial no puede guardar la fecha primero y el monto después |
| C. B más tope (monto pagado no mayor que el monto de la cuota) | Contradice el pago parcial solo si es *menor*, no mayor; un pago mayor podría ser un adelanto | Puede bloquear pagos reales |

**Recomendación: B** (consistente con "un par fecha y monto por cuota", def. L200), **sin** tope superior hasta que Héctor lo pida. La vista de riesgo debe seguir mirando `fechapago`. ~~Provisional: A (DC-21), porque coincide con la vista de riesgo ya validada.~~

**Decidida el 2026-10-10: B, sin tope.** Pago registrado = `sanic_fechapago` y `sanic_montopagado` juntos: ambos o ninguno, monto mayor que cero, sin tope superior. Regla en `ReglasDeCuota`, mensaje `CARP-CUO-003`. Cambian DC-21 (que era A), 2.7 y las pruebas `PA-CUO-…`; la vista de riesgo sigue mirando `fechapago` nulo.

### OA-9. ¿Se genera el formato de un arreglo Terminado, Anulado o Descartado?

**Qué dicen las fuentes.** def. ítem 14 y RF-08: el documento "se regenera desde los datos". No dice para qué estados. Congelar un registro inactivo depende de V-2.

| Opción | Detalle | Costo |
|---|---|---|
| A. Sí, sin congelar ni tocar el arreglo; se escribe la bitácora | Sirve para reimprimir un terminado | La bitácora registra impresiones de arreglos cerrados |
| B. Sí, y sin bitácora | Tratado como vista previa | No queda rastro de quién imprimió un arreglo cerrado |
| C. No: solo Borrador y Vigente | Un `CARP-GEN-012` más | El área no puede reimprimir el historial del cliente (RF-13) |

**Recomendación: A.**

**Decidida el 2026-10-10: A.** Se genera el formato de un arreglo Terminado, Anulado o Descartado sin congelar ni tocar el arreglo, y se escribe la bitácora (PA-GEN-22). `CARP-GEN-012` queda sin uso.

### OA-10. Respuesta enviada en una conversación Finalizada o Unida

**Qué dicen las fuentes.** def. ítem 3 solo cubre "una respuesta enviada sin nada pendiente (por ejemplo, un recordatorio)". Una respuesta de cortesía en una finalizada no está contemplada; tampoco una respuesta cuyo borrador se creó antes de que la conversación se uniera.

| Opción | Detalle | Costo |
|---|---|---|
| A. Finalizada: solo suma al contador; no toca pendientes ni indicador. Unida: se redirige al destino | Respeta "el oficial decide reabrir" (RF-15) | Una respuesta a un correo pendiente de una finalizada no lo marca como respondido; el oficial tiene que reabrir |
| B. Finalizada: la respuesta limpia el pendiente y el indicador | Más natural para el oficial | Sortea la decisión de reabrir o iniciar una nueva, y la conversación queda Finalizada con correos respondidos |
| C. Bloquear en el servidor responder desde una no Abierta | Un `CARP-EML-004` en `RemitenteDelAreaStep` | Impide también la cortesía sin reabrir |

**Recomendación: A.**

**Decidida el 2026-10-10: A.** Finalizada: la respuesta solo suma al contador y no toca pendientes ni indicador; para marcar respondido, el oficial reabre. Unida: se redirige al destino (PA-RES-09, PA-RES-10). `CARP-EML-004` queda sin uso.

### OA-11. Unir: ¿se admite un origen Finalizado? ¿y clientes distintos?

**Qué dicen las fuentes.** def. ítem 5: el destino "debe estar Abierta"; no dice nada del estado del origen ni de qué pasa si los dos tienen cliente distinto. "Un cliente puede tener a la vez un tema de tarjeta y otro de préstamo" (L281), pero ambos del mismo cliente.

| Pregunta | Opciones | Recomendación | Previsto en el código hasta el 2026-10-10 |
|---|---|---|---|
| Origen Finalizado | (a) rechazar (`CARP-UNI-004`); (b) permitir y dejar el origen Unida | Rechazar: para continuar un tema finalizado ya existen Reabrir e Iniciar conversación nueva | (a) |
| Clientes distintos | (a) no validar; (b) rechazar con `CARP-UNI-010`; (c) rechazar salvo que lo haga la jefatura | (b): unir correos de dos clientes mezcla datos de personas distintas (RNF-03) y repunta arreglos del otro cliente | ~~(a), porque es lo que dicen las fuentes~~ |

**Decidida el 2026-10-10:** origen Finalizado, (a) rechazar con `CARP-UNI-004` (ya era el comportamiento previsto); clientes distintos, (b) rechazar con `CARP-UNI-010` (el comportamiento previsto era (a), no validar, y cambió). Si alguna de las dos conversaciones no tiene cliente no hay nada que comparar y la unión sigue (paso 6c y PA-UNI-02, PA-UNI-23). Se agregan la regla, el mensaje y `PA-UNI-19`, `PA-UNI-22` y `PA-UNI-23`.

### OA-12. Cómo reconoce el plugin a la cuenta de servicio, y quién ejecuta las API

**Qué dicen las fuentes.** def. L418 y inv. 10.7 fijan una variable con el **Application ID** de la cuenta de servicio. def. L101 y D-21 (L482) dicen que la cuenta es **licenciada** y descartan un service principal como dueño de flujos. Un usuario licenciado es una persona con UPN y **no tiene `applicationid`**. En M-PPP esto obligó a reconocer a la cuenta por UPN (`servicio.cuenta`). Además la matriz (§1.2, L27) asigna Oficial a todo usuario de la BU, incluidas las cuentas de servicio, por lo que Servicio y Purga pasan el privilegio de ejecución de `generarformato`, `tomar…`, `unir…` e `iniciar…`.

| Opción | Detalle | Costo |
|---|---|---|
| A. Mantener el Application ID | Funciona solo si la cuenta que escribe es un usuario de aplicación | Si la cuenta es un usuario licenciado, la variable nunca coincide: sus escrituras se tratan como de una persona (inocuo para los flujos del diseño, porque pasan por las API, que elevan; relevante si algún día un flujo escribe columnas solo-sistema) |
| B. Cambiar la variable a UPN o `domainname` | Misma lógica, otro dato | Una variable con otro nombre y otro significado (inv. 10.7) |
| C. No reconocer cuenta alguna: solo SYSTEM | Las API elevan siempre; los flujos nunca escriben solo-sistema | Más simple; ningún flujo del diseño lo necesita |

Para las API: rechazar en `tomar…`, `unir…` e `iniciar…` a quien tenga `applicationid`, o aceptar que la cuenta de servicio también puede ejecutarlas.

**Recomendación: confirmar con el administrador qué tipo de usuario es la cuenta de servicio; si es una persona licenciada, C** (y retirar 10.7), y aceptar que las API puedan ser ejecutadas por Servicio y Purga (no tienen sentido de negocio para ellas y la escritura queda atribuida). ~~Provisional: A como está en las fuentes (`EsCuentaDeServicio`, 0.3), sin efecto sobre el resto.~~

**Decidida el 2026-10-10: C.** No se reconoce ninguna cuenta de servicio: solo SYSTEM escribe columnas solo-sistema (las Custom API elevan siempre y ningún flujo del diseño las escribe). La cuenta de servicio es un usuario licenciado (RNF-06, D-21) y no tiene Application ID, por eso la variable era un error. Se acepta que Servicio y Purga puedan ejecutar las API: reciben el rol Oficial por el team del sistema y la escritura queda atribuida. Se retiran `EsCuentaDeServicio`, la variable 10.7 (inventario, sin renumerar) y `Aplicacion(id)` de `IUsuarios`; se reescriben DC-02 y PA-CNV-17 y PA-CNV-18. Inconsistencias 6 y 11 de la sección 7 resueltas.

### Sin decisión pendiente para el servidor

- "Con arreglo de pago" sin arreglo formalizado vinculado (R-06, def. L284): "hoy solo avisa". El aviso es de formulario; el servidor no hace nada. Si se vuelve obligatorio, lo valida `ReglasDeConversacion` (una regla y un código).
- Promedio de tiempo total sin las "Sin respuesta" (def. L277 y L517): afecta el gráfico (inv. 12.16), no el servidor.
- Regla de pertenencia a un team de grupo de Entra consultable por plugin (def. L502, confianza media): se verifica en Dev (H-8 de la lista de humo, 8.1).
- Celda de moneda en la plantilla y formatos de monto por moneda (OA-7): pendiente de Héctor Romero; es un cambio de plantilla, no de código.

---

## 7. Inconsistencias encontradas

No se resolvieron en silencio. Para cada una, qué sigue este documento y, desde el 2026-10-10, su estado.

| # | Dónde | Qué choca | Qué sigue este documento | Estado |
|---|---|---|---|---|
| 1 | `definicion.md:143` frente a `02-diccionario-datos.md:389-393` y `definicion.md:299-303` | La def. llama "gestos con motivo" a finalizar, formalizar, desbloquear, descartar, anular y reabrir. El diccionario solo modela motivo para descartar, anular y reabrir (y resultado/motivo/detalle al finalizar). Formalizar y desbloquear no lo piden | El diccionario (OA-1). Decidido: ni formalizar ni desbloquear piden motivo | **Resuelta por OA-1 (2026-10-10).** `definicion.md` corregida |
| 2 | `definicion.md:122` frente a `:276` y `:450` | Dos vistas separadas ("sin respuesta", "llegados a finalizadas") frente a una sola que reúne ambos; "borradores" frente a "borradores sin cambios en más de 30 días" | Nada en el servidor (OA-2). Decidido: cinco vistas (ver OA-2) | **Resuelta por OA-2 (2026-10-10).** `definicion.md` e inventario 12.1 a 12.6 alineados |
| 3 | `02-diccionario-datos.md:661` (P-09) frente a `definicion.md:285-286` (ítem 6) y `:82`, `:276` | P-09 dice que el plugin "vacíe" `sanic_sinrespuestadesde` "al decidir" (reabrir o iniciar nueva). Si se vacía al reabrir, el correo nuevo queda sin respuesta en una conversación Abierta que ya no lo muestra, y falla el criterio de éxito 4 | Reabrir **conserva** `sinrespuestadesde`; Iniciar conversación nueva la vacía en la anterior (DC-10) | **Resuelta el 2026-10-10**: el usuario confirmó que Reabrir conserva `sinrespuestadesde`; P-09 del diccionario quedó corregida |
| 4 | `06-inventario-componentes.md:166` frente a `definicion.md:163`, `:260` | El inventario pone la "vinculación por correo" entre los steps de Cliente (7.13); la definición la pone en el plugin de entrada de correo | En la entrada de correo (2.10, paso 5); `ClienteStep` no vincula | Decidida en este documento (2.10). El inventario 7.13 todavía lista la vinculación entre los steps de Cliente; sin alinear |
| 5 | `01-convenciones.md:44` frente a `06-inventario-componentes.md:223` | El ejemplo de la convención nombra la variable de la BU `sanic_carp_ev_businessunitarea`; el inventario la llama `sanic_carp_ev_nombrebu` *(prov.)* | `sanic_carp_ev_nombrebu` (0.4); la convención debería actualizar su ejemplo | **Resuelta el 2026-10-10**: el usuario eligió `sanic_carp_ev_nombrebu`; la convención actualizó su ejemplo |
| 6 | `definicion.md:418` e `06-inventario-componentes.md:227` frente a `definicion.md:101` y `:482` (D-21) | Variable con el Application ID de la cuenta de servicio, pero la cuenta es un usuario licenciado (sin `applicationid`) | ~~Se implementa como dice (DC-02)~~. Decidido por OA-12: sin variable ni reconocimiento de cuenta de servicio; DC-02 reescrita | **Resuelta por OA-12 (2026-10-10).** Variable 10.7 retirada |
| 7 | `spikes/manual/RESULTADO.md:3` y `:128-135` frente a `definicion.md:181`, `:256`, `:269` y D-34 (`:495`) | La def. cita el spike 4 como confirmado (botón Responder completa `parentactivityid`, imagen pegada en Base64, respuesta con asunto cambiado desde Outlook web), pero `RESULTADO.md` dice que esas pruebas manuales están "preparadas, no ejecutadas" y que los veredictos "no están en este documento". La evidencia no está en el repositorio | Se toman como hechos verificados. ~~Falta registrar la evidencia~~: ya está registrada | **Resuelta.** Los veredictos de las cuatro pruebas manuales están en `spikes/manual/RESULTADO.md` (sección "Tarea D. Veredictos", agregada el 2026-10-09). Quedan desactualizados el encabezado (L3) y la fila D de la tabla de veredictos (L18) del mismo archivo |
| 8 | `definicion.md:270` | El plugin "fija el remitente en la queue y rechaza cualquier otro", pero la misma línea dice que el remitente por defecto del formulario es el usuario. Si se rechaza todo lo que no sea la queue, la respuesta por defecto sería siempre rechazada | Reemplazar el usuario creador por la queue; rechazar cualquier otro (DC-07) | Decidida en este documento (DC-07) |
| 9 | `definicion.md:249-251` y `:491` (D-30) frente a `02-diccionario-datos.md:507-508` | D-30 lista las columnas solo-sistema de Conversación y Arreglo; el diccionario marca también `sanic_tiemporespuesta` y `sanic_respondidoporid` de Email como solo-sistema, sin plugin que las proteja en la def. | Se agrega `EmailSoloSistemaStep` (2.9) | Decidida en este documento (2.9) |
| 10 | `01-convenciones.md:57` | El nombre de step `STEP - CARP - tabla - mensaje - etapa` no distingue dos steps del mismo mensaje y etapa (por ejemplo Conversación/Update/PreOperation tiene tres); el playbook rechaza nombres repetidos | Se agrega el nombre de la clase (DC-23) | Decidida en este documento (DC-23). `01-convenciones.md:57` todavía muestra el nombre sin la clase |
| 11 | `04-matriz-privilegios.md:27` y `:30` frente a `definicion.md:402` (D-17) | Cada API se restringe con un privilegio "presente solo en los roles que corresponden", pero Servicio y Purga reciben también el rol Oficial por el team del sistema, y con él `Write` de Conversación y `Read` de Generación (Purga lo tiene además por su cuenta, `04:71`) | Sin cambio de roles. Decidido por OA-12: se acepta | **Resuelta por OA-12 (2026-10-10).** Se acepta que Servicio y Purga ejecuten las API |
| 12 | `04-matriz-privilegios.md:74` frente a `:67` y `definicion.md:391` | El texto de la matriz dice que el Oficial "no tiene ningún privilegio" sobre Mapeo y que el Jefe tampoco lee; la tabla (§2) y la def. dan `Read` de Mapeo al Oficial (opción A del 2026-10-09). Texto desactualizado | Rige la tabla. Texto de la matriz corregido | **Resuelta (2026-10-10).** Texto de `04-matriz-privilegios.md` corregido: el Oficial lee Mapeo por la opción A del 2026-10-09; el Jefe no tiene Read propio, le llega por el Oficial |
| 13 | `06-inventario-componentes.md:150` | Dice que el directorio `src/` no existe; ya existe `src/Sanic.Carp.Plugins/Dominio/Choices.g.cs` | Se parte de lo que hay | Decidida en este documento. El inventario (L150) todavía dice que `src/` no existe |
| 14 | `definicion.md:292` y `02-diccionario-datos.md:47` (DD-12) frente a `definicion.md:311` | Los montos se calculan "al crear el arreglo y al modificar el borrador". Un Vigente reabierto (descongelado) también admite modificar datos (ítem 10) y quedaría con montos sin recalcular | Se recalculan en todo arreglo editable, no solo en Borrador (2.6, paso 4) | Decidida en este documento (2.6, paso 4) |
| 15 | `definicion.md:282` frente a `definicion.md:471` (D-10) y `02-diccionario-datos.md:275` | "Repunta los arreglos vinculados", pero un arreglo Terminado, Anulado o Descartado es inactivo y de solo lectura | ~~Se repuntan los activos; los inactivos no (V-2)~~ Se repuntan **todos** los arreglos del origen, activos e inactivos (Terminado, Anulado, Descartado); lo hace SYSTEM dentro de la API de unir y no habilita a ninguna persona a editar un arreglo cerrado (1.4 paso 6b, 2.6 paso 0) | **Resuelta el 2026-10-11 por decisión del usuario**, apoyada en V-2 (la plataforma deja escribir sobre filas inactivas). `definicion.md` (ítem 5) alineada; D-38 |
| 16 | `03-contratos-custom-api.md` 2.6 (paso 1) frente a 2.6 (paso 2, lista de otros cambios de estado) y 3.2 | Para un **Terminado**, el paso 1 dice que todo lo que no sea Reabrir da `CARP-ARR-022`; la lista del paso 2 pone Terminado → Anulado en `CARP-ARR-010` y la tabla de 3.2 pone Vigente/Terminado → Borrador en `CARP-ARR-010`. PA-ARR-29 espera `-022` para Terminado → Anular y PA-ARR-54 y PA-ARR-55 (V-5) esperan `-010` para Terminado → Borrador | ~~Se escribió cada PA con el código que ya traía su documento y no se eligió~~ Gana el primer paso: para un Terminado, todo lo que no sea el gesto Reabrir da `CARP-ARR-022` (2.6, paso 1). Se corrigieron la lista del paso 2, la tabla de 3.2, `PA-ARR-54` y `PA-ARR-55`; `PA-ARR-29` queda como estaba (`-010` para un Borrador, `-022` para un Terminado) | ~~**Abierta (2026-10-11).** Falta decidir qué código gana para un Terminado que cambia de estado sin ser el gesto Reabrir~~ **Resuelta el 2026-10-11 por el arquitecto: "gana el primer paso".** |
| 17 | Revisión del 2026-10-11, tras V-2 | "Una versión de plantilla Borrador desactivada sigue editable y publicable": la plataforma deja a una persona desactivar una versión (y una cuota) con un PATCH, y los steps de Versión lo permitían a propósito para Borrador e Histórica | El `statecode` y el `statuscode` nativos de Cuota y de Versión de plantilla solo los cambia SYSTEM (2.7, 2.8; `CARP-CUO-001`, `CARP-VER-014`, `CARP-VER-003`) | **Resuelta el 2026-10-11 por decisión del usuario (D-39).** Ningún proceso del diseño cambia ese `statecode`: la purga hace `Delete`, publicar y pasar a Histórica escriben `sanic_estado`. ~~Queda sin actor la desactivación del catálogo "se desactivan, nunca se purgan" (`definicion.md` §6 Datos, `02-diccionario-datos.md` §4.5): hoy solo la haría SYSTEM (un administrador también queda rechazado, porque el step solo reconoce a SYSTEM, 0.3) y no hay gesto previsto; no se resolvió~~ **Resuelta del todo el 2026-10-11 (decisión del arquitecto, D-40):** el catálogo no se desactiva, se retira pasando a Histórica (`sanic_estado`) y nunca se purga; se corrigieron `definicion.md` §6 Datos (relaciones no parentales y purga) y `02-diccionario-datos.md` §4.5 (descripción y nota de estado) y DD-03 |
| 18 | `03-contratos-custom-api.md` 2.5 (`ArregloCrearStep`, paso 2, supuesto sin verificar) frente a `spikes/verificaciones/RESULTADO.md`, V-8 | El paso 2 suponía que el `Target` de un `Create` solo trae `sanic_nombre` si lo envió el cliente; la plataforma lo completa antes de PreValidation, así que la clave **siempre** está y el paso rechazaba todas las altas. Un valor enviado por la persona se guarda tal cual (también `ARR-777777`) y no hay marca de quién lo puso | En el Create se rechaza un `sanic_nombre` que no cumpla `^ARR-[0-9]{6,16}$` con `CARP-ARR-009` (2.5, paso 2); en el Update se rechaza todo valor distinto de la pre-imagen (2.6, paso 0). Riesgo aceptado: un `ARR-nnnnnn` falsificado por API pasa | **Resuelta el 2026-10-11 por decisión del usuario (D-40, V-8 REFUTADO).** Queda por verificar en Dev que el formato `{SEQNUM:6}` crece más allá de seis dígitos (el patrón ya lo admite) |
| 19 | `02-diccionario-datos.md` §4.4 y §4.5 frente a 2.0.8 y `PA-COL-02` | El diccionario marcaba Sis = No en las cinco columnas de Cuota (`sanic_nombre`, `sanic_numero`, `sanic_fechavencimiento`, `sanic_monto`, lookup `sanic_arregloid`) y en `sanic_huella`, `sanic_fechaaprobacion` y `sanic_aprobadoporid` de Versión, que 2.0.8 declara solo-sistema; `PA-COL-02` (unión de Sis = Sí = unión de solo-sistema) fallaría | Rige 2.0.8, porque es coherente con los steps: `CuotaActualizarStep` rechaza con `CARP-CUO-001` cualquier cambio fuera del pago, y `VersionPlantillaCrearStep` y `VersionPlantillaActualizarStep` rechazan esas tres con `CARP-VER-002` (Borrador) o `CARP-VER-003` (resto). Era una diferencia de anotación, no de fondo | **Resuelta el 2026-10-11:** se corrigió el diccionario (Cuota 5, Versión 3, ver 02 §4.4 y §4.5). Quedan fuera de esta corrección `sanic_nombre` y el lookup de Mapeo (inconsistencia 20) |
| 20 | 2.0.8 (Mapeo, solo-sistema) frente a 2.8 (`MapeoCeldaActualizarStep`) y `02-diccionario-datos.md` §4.6 | 2.0.8 pone `sanic_nombre` de Mapeo (calculada por plugin) entre las solo-sistema, y el diccionario le marca Sis = No. El step la escribe en el Create y en el Update si cambió la hoja o la celda, pero **no rechaza** que una persona la cambie sola (un Update que solo trae `sanic_nombre` pasa mientras la versión sea Borrador) y no tiene la columna en la pre-imagen. Es una diferencia de fondo | Sin cambios en 2.0.8, en 2.8 ni en el diccionario (se reporta, no se resuelve). `sanic_versionplantillaid` de Mapeo sí es coherente (`CARP-MAP-006` lo rechaza) | **Abierta (2026-10-11).** Falta decidir: o el step rechaza el cambio directo de `sanic_nombre` (un mensaje nuevo y la columna en la pre-imagen) y el diccionario pasa a Sis = Sí, o 2.0.8 la mueve a las libres. Mientras tanto `PA-COL-02` falla para esa columna |
| 21 | `spikes/verificaciones/RESULTADO.md` V-8 frente a 2.0.8 (Conversación, `sanic_nombre` libre) | V-8 solo probó el autonumérico del Arreglo, pero es un comportamiento de la plataforma para toda primaria autonumérica: una persona puede enviar su propio `sanic_nombre` (`CONV-…`, `GEN-…`) y se guarda tal cual. En Conversación el nombre es una columna libre (editable en una Abierta; bloqueada en una Finalizada o Unida por `CARP-CNV-009`); Generación no la escribe ningún humano | Sin cambios: la decisión del 2026-10-11 es solo sobre el Arreglo | **Abierta (2026-10-11), sin decisión del usuario.** Conviene decidir si Conversación recibe una regla análoga; no se probó en Dev (solo se infiere de V-8) |
| 22 | 2.8 y 3.3 (Versión) tras D-39 y D-40 | Una versión que se queda en Borrador y nunca se publica no puede retirarse: la persona no desactiva (`CARP-VER-014`), el paso a Histórica solo ocurre al publicar otra y la purga no toca el catálogo | Sin cambios; la versión Borrador sigue editable y no estorba (no es Vigente) | **Abierta (2026-10-11).** No hay actor ni gesto para retirar un Borrador abandonado; hoy lo haría solo SYSTEM (por ejemplo, un script de soporte) |
| 23 | `playbooks/tabla/sanic_carp_tbl_versionplantilla.md:43` (y su muestra `.solucion.xml`) frente a D-40 y `02-diccionario-datos.md` §4.5 | La descripción de la tabla ya creada en Dev dice "Es catálogo: se desactiva, nunca se purga"; el diccionario y la definición dicen ahora que se retira pasando a Histórica | Sin cambios en `playbooks/` (fuera del alcance de esta edición) | **Abierta (2026-10-11).** Alinear el texto del playbook y de la descripción en Dev cuando se vuelva a tocar la tabla |

---

## 8. Resumen y pruebas de humo

### 8.1 Pruebas de humo en Dev (no automatizadas)

Lo que el doble no puede probar (pipeline real, transacción, seguridad) se prueba en Dev, no se simula (M-PPP §8). Cada pieza se da por buena solo después de su prueba de humo:

| # | Qué | Pieza |
|---|---|---|
| H-1 | V-1 a V-8 de 0.9. **Hecha el 2026-10-11** (`spikes/verificaciones/RESULTADO.md`): V-1, V-2, V-3, V-5 y V-7 confirmados, V-4 y V-8 refutados (V-4 rediseñado con sustituto; V-8 con la regla de formato del Create), V-6 parcial; lo que quedó sin probar está en H-12 y H-13 | todas |
| H-2 | Formalizar dos arreglos del mismo crédito a la vez: la segunda recibe el rechazo de K-02 | 2.6 |
| H-3 | 60 altas de cuota dentro de la transacción: tiempo bajo el límite | 2.5 |
| H-4 | Generar un formato con un Oficial **no** administrador con el rol real y el perfil de columna | 1.1 |
| H-5 | Responder desde el timeline: sale por la queue, `parentactivityid` completo, el cliente responde y entra a la misma conversación | 2.9, 2.10 |
| H-6 | Una respuesta enviada: se llenan `sanic_tiemporespuesta` y `sanic_respondidoporid` y se recalcula la conversación | 2.11 |
| H-7 | Reconciliación llamada por la cuenta de servicio desde un flujo | 1.2 |
| H-8 | La pertenencia al team de jefatura (grupo de Entra) se ve desde el plugin | 0.3 |
| H-9 | Los 7 fixtures de 4.5 exportados y comparados con el doble | 4.5 |
| H-10 | Publicar una versión: la generación de prueba, la huella y el paso a Histórica | 2.8 |
| H-11 | Abrir un archivo generado en Excel de escritorio sin que ofrezca reparar | 1.1 |
| H-12 | Con S-27 registrado (`Update` con filtro `sanic_archivo`, V-4): (a) que un `InvalidPluginExecutionException` lanzado desde ese step aborte la subida y, en un reemplazo con PATCH directo, deje `sanic_archivo` como estaba; (b) la subida y el reemplazo del archivo por un usuario **no administrador** (Jefe con el rol real), sobre un Borrador (pasa, las dos pasadas) y sobre una Vigente (`CARP-VER-003`). Pendiente de V-4: no se probó ninguna de las dos | 2.8 |
| H-13 | Asociar el perfil `CSP - CARP - Datos sensibles` **por team** (el team del sistema de la BU y Jefatura), no al usuario: que un Oficial del team reciba `sanic_cuentadebitar` e `sanic_instantaneacedula` en la pre-imagen de S-21, y uno fuera del perfil no (DC-28 las lee con SYSTEM). Pendiente de V-6: solo se probó la asociación directa al usuario | 2.6, 2.2 |

### 8.2 Conteos

| Concepto | Cantidad |
|---|---|
| Custom API | 5 (`generarformato`, `reconciliarcorreos`, `tomarconversacion`, `unirconversaciones`, `iniciarconversacionnueva`) |
| Registros de step | 35 (los 35 firmes desde el 2026-10-11; ~~34 firmes y 1 condicionado a V-4~~: S-27 pasó a `Update` con filtro `sanic_archivo`). El 2026-10-11 (D-40) S-16 suma `statecode,statuscode` al filtro y una pre-imagen, y S-29 suma esas dos columnas a su pre-imagen: no se agregó ningún step |
| Clases de step | 20 (sin cambios el 2026-10-11) |
| Máquinas de estado | 3 (Conversación, Arreglo, Versión de plantilla) |
| Piezas de dominio puro | 8 (2.0.1 a 2.0.8) |
| Pruebas de aceptación `PA-…` | ver el recuento al final de esta sección |
| Decisiones de diseño `DC-nn` | 28 (DC-02 y DC-21 reescritas el 2026-10-10; el 2026-10-11 se agregó DC-28 y se anotaron DC-01 y DC-06 con V-5 y V-6; ninguna retirada; las decisiones del 2026-10-11 de D-39 no agregan ninguna `DC`, siguen siendo 28) |
| Supuestos de plataforma `V-n` | 8, todos verificados el 2026-10-11: 5 confirmados (V-1, V-2, V-3, V-5, V-7), 2 refutados (V-4, con sustituto; V-8, con la regla de formato del Create), 1 parcial (V-6) (~~7~~: se sumó V-8) |
| Decisiones abiertas `OA-n` | 0 (las 12 decididas el 2026-10-10) |
| Mensajes de error nuevos del 2026-10-11 (D-40) | 3: `CARP-CNV-010`, `CARP-CLI-004`, `CARP-MAP-007`. Sin mensaje nuevo para Arreglo (`CARP-ARR-009` y `CARP-ARR-012` encajan) |
| Inconsistencias | 23 (la 15, la 16 y la 17 resueltas el 2026-10-11; la 16 por el arquitecto: "gana el primer paso"; la 17 por decisión del usuario, D-39, y resuelta del todo por D-40; se agregaron la 18 y la 19, resueltas el mismo día, y la 20 a la 23, abiertas: 20 de fondo en Mapeo, 21 Conversación frente a V-8, 22 Borrador abandonado, 23 texto del playbook) (~~17~~) |

Pruebas de aceptación numeradas `PA-…`: **420** en 26 áreas (ARR 69, ASG 10, CAL 9, CLI 19, CNV 38, COL 2, CON 5, CUO 23, EML 8, ENT 23, ESI 4, FEC 7, GEN 27, HUE 5, INI 15, MAP 15, MON 14, NOR 8, PRO 11, PUB 9, REC 15, RES 14, TIE 10, TOM 9, UNI 25, VER 26). Eran 394 antes de las decisiones del usuario del 2026-10-11 posteriores a V-8 (D-40): ese día se agregaron 26, ARR 8 (`PA-ARR-62` a `-69`), CLI 6 (`PA-CLI-14` a `-19`), CNV 7 (`PA-CNV-32` a `-38`) y MAP 5 (`PA-MAP-11` a `-15`); se modificaron `PA-ARR-09` (vuelve a 20 columnas solo-sistema: `sanic_nombre` pasa a la regla de formato, `PA-ARR-62` a `-64`), `PA-ARR-11` (el alta del sistema admite cualquier `sanic_nombre`) y `PA-COL-02` (nota sobre el diccionario alineado); `PA-ARR-32`, `-34`, `-36` y `PA-CNV-08`, `-29` quedan como estaban (los cubren los casos nuevos de Reabrir). Eran 380 antes de las decisiones del usuario del 2026-10-11 posteriores a la revisión (D-39): ese día se agregaron 14, ARR 3 (`PA-ARR-59` a `-61`), CNV 5 (`PA-CNV-27` a `-31`), CUO 3 (`PA-CUO-21` a `-23`) y VER 3 (`PA-VER-24` a `-26`); se modificaron `PA-ARR-09` (21 columnas solo-sistema), `PA-ARR-45`, `PA-ARR-54`, `PA-ARR-55` (ahora `CARP-ARR-022`), `PA-CNV-25` (ahora `CARP-CNV-009`), `PA-VER-11` (la desactivación da `CARP-VER-003`) y se aclaró que `PA-CNV-13`, `-15`, `-17` y `-18` son sobre una Abierta. Eran 356 el 2026-10-10: el 2026-10-11 (V-1 a V-7 y decisiones del usuario) se agregaron 24: ARR 11 (`PA-ARR-48` a `-58`), CLI 2 (`PA-CLI-12` y `-13`), CNV 3 (`PA-CNV-24` a `-26`), UNI 2 (`PA-UNI-24` y `-25`) y VER 6 (`PA-VER-18` a `-23`); se modificaron `PA-ARR-32`, `PA-UNI-15` y `PA-VER-15`. Antes, eran 346 el 2026-10-09: el 2026-10-10 se agregaron 2 de ARR (`PA-ARR-46` y `-47`, OA-5), 6 de CUO (`PA-CUO-15` a `-20`, OA-8) y 2 de UNI (`PA-UNI-22` y `-23`, OA-11); se modificaron `PA-CNV-17`, `PA-CNV-18`, `PA-CUO-01`, `-10`, `-11`, `PA-GEN-19`, `PA-UNI-03`, `-09` y `-19`.
