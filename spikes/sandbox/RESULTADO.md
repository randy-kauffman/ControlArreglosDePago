# Resultado del spike 1b: generador del F-NIC-0000052 dentro del sandbox de Dataverse

**Proyecto:** 2026-003 Control de Arreglos de Pago. **Condición:** C-01 (la parte que el spike 1 dejó pendiente). **Decisiones que toca:** D-13, D-15 y el supuesto "Open XML SDK funciona dentro del sandbox".
**Fecha:** 2026-10-08. **Entorno:** `https://org36e60d9d.crm.dynamics.com/` (Dev, confirmado con `pac org who`). Todo lo creado está en la solución descartable `SpikesArreglosPago` (publisher `spk`).
**No se tocó:** `definicion.md`, `spikes/plantilla/`, otras soluciones ni configuración de la organización (el trace de plugins de la organización ya estaba en "All"; se leyó, no se cambió).

## 1. Veredicto

El generador del spike 1, compilado para `net462` y publicado como paquete de plugins con `DocumentFormat.OpenXml` 2.20.0 como ensamblado dependiente, **carga y corre en el sandbox de Dataverse sin ningún error de carga de ensamblado**, y devuelve archivos **idénticos byte a byte por parte del paquete** a los que genera la misma biblioteca en .NET 8 local. El plan B de D-13 (JavaScript en el cliente) no se activa.

| # | Punto | Veredicto | Evidencia resumida |
|---|---|---|---|
| 1 | El paquete de plugins con `DocumentFormat.OpenXml` (6,1 MB) como ensamblado dependiente se registra y carga en el sandbox | **CONFIRMADO** | Paquete de 1.647.521 bytes; ensamblado `Spk.Plugins` registrado con `isolationmode = 2` (sandbox) y `sourcetype = 4` (paquete). Dentro del proceso del sandbox se listaron cargados `DocumentFormat.OpenXml 2.20.0.0`, `ArreglosPago.Formato 1.0.0.0`, `WindowsBase 4.0.0.0`, `System.IO.Compression 4.0.0.0` (sección 4) |
| 2 | `System.IO.Packaging` / `WindowsBase` y `System.IO.Compression` funcionan bajo el aislamiento (sin tocar disco) | **CONFIRMADO** | 131 llamadas medidas, más las de verificación y de error, ninguna con excepción de carga ni de acceso a archivos. El generador trabaja solo con `MemoryStream` |
| 3 | Corre en .NET Framework (net462) | **CONFIRMADO** | CLR `4.0.30319.42000`, 7 procesadores, 64 bits. El binario es el `net462` de la biblioteca |
| 4 | Pruebas unitarias del spike 1 (205 casos) en net462 | **NO PROBADO** | No hay .NET Framework en esta máquina (Linux). Lo que sí se probó es la salida: 24 de 24 partes iguales byte a byte entre sandbox (net462) y local (net8.0) en los 3 plazos |
| 5 | Descarga de la plantilla desde la columna File **dentro del plugin** (`InitializeFileBlocksDownload` + `DownloadBlock`) | **CONFIRMADO** | Plantilla de 32.236 bytes en un bloque; además una de 4,5 MB (2 bloques de 4 MB) sin errores. `IsChunkingSupported = true`. Descarga en caliente: mediana 50 ms |
| 6 | Validación de la huella SHA-256 dentro del plugin | **CONFIRMADO** | La huella guardada coincide con la del archivo releído; con una huella alterada la API rechaza (sección 6) |
| 7 | Fidelidad del contenido (valores y tipos, N cuotas, filas sobrantes limpias, imagen, celdas combinadas, área de impresión) | **CONFIRMADO** | Plazo 6, 30 y 60: 24/24 partes del paquete iguales al esperado local; oráculo independiente en Python (`openpyxl` y `zipfile`, sin el SDK) con 0 fallos; control negativo (darle al oráculo el archivo de otro plazo) falla como debe; PDF por LibreOffice con logo y datos correctos (sección 5) |
| 8 | Tiempo dentro del plugin y límite de 2 minutos | **CONFIRMADO** | Frío: 515 a 984 ms. Caliente: mediana 72 ms. Margen frente a 120.000 ms: más de 120 veces en el peor frío (sección 3) |
| 9 | Tamaño del archivo y del Base64 | **CONFIRMADO** | 32.813 bytes y 43.752 caracteres (60 cuotas); coincide con el local |
| 10 | Error de negocio claro con huella distinta | **CONFIRMADO** | HTTP 400 con `InvalidPluginExecutionException` y mensaje en español, sin traza de pila (sección 6). Matiz: los errores de forma de la llamada (parámetro ausente o de otro tipo) los devuelve la capa OData antes del plugin, con texto técnico |
| 11 | Algún límite de tamaño del parámetro de salida string que se acerque | **CONFIRMADO que no se acerca** | La documentación de Microsoft Learn no publica un tope para una propiedad de respuesta String. Los topes documentados cercanos son de 116,85 MB (contexto hacia el sandbox) y 128 MB por solicitud. Prueba empírica: una salida de 6.045.740 caracteres (6 MB) funcionó (sección 7) |
| 12 | Redespliegue verificado por contenido (versión y `modifiedon`) | **CONFIRMADO** | Tres redespliegues con el `AssemblyVersion` subiendo 1.0.1.0 a 1.0.4.0; en cada uno la versión y `modifiedon` del ensamblado avanzaron y el binario nuevo fue el que corrió (sección 2) |
| 13 | Script de limpieza | **CONFIRMADO** (ensayada la secuencia con un conjunto de prueba; no se ejecuta sobre el conjunto real porque el pedido es dejarlo para inspección) | Sección 8 |
| 14 | Llamador con permisos mínimos (no administrador) y lectura de la plantilla "como sistema" | **NO PROBADO** | Aquí el llamador es el usuario de aplicación del estudio, con rol de administrador. El diseño real lee la plantilla como sistema |
| 15 | Carga concurrente y varios usuarios a la vez | **NO PROBADO** | Solo llamadas secuenciales. Se observó que el sandbox usa varios procesos trabajadores (sección 3) |
| 16 | Apertura con Excel de escritorio sin "reparar" | **NO PROBADO** | Sigue pendiente, como en el spike 1 |

## 2. Qué se construyó y cómo se verificó el despliegue

Todo con el prefijo `spk_`, en la solución `SpikesArreglosPago`:

| Componente | Detalle | Pertenencia a la solución (releída) |
|---|---|---|
| Tabla `spk_versionplantilla` | Columna File `spk_archivo` (`MaxSizeInKB` 5120), texto `spk_huella` (64) y nombre. 5 filas de prueba: v07 válida; v07 con huella alterada; texto plano que no es .xlsx (con su huella correcta); sin archivo; plantilla grande de 4,5 MB | componente tipo 1 |
| Paquete de plugins `spk_ArreglosPagoSandbox` | Versión de paquete 1.0.0 (no se puede cambiar en el servidor). Contiene `Spk.Plugins.dll`, `ArreglosPago.Formato.dll` y `DocumentFormat.OpenXml.dll` en `lib/net462/`. Único ensamblado registrado: `Spk.Plugins` (tipo `Spk.Plugins.GenerarFormatoSpikePlugin`). Versión final desplegada del ensamblado: 1.0.4.0 | componente tipo 10039 |
| Custom API `spk_GenerarFormatoSpike` | **Acción** global (no función). Entrada: `VersionId` (Guid), `Plazo` (Integer). Salida: `ArchivoBase64` (String), `Milisegundos` (Integer), `TamanoBytes` (Integer) y `Detalle` (String, diagnóstico del spike; no estaba en el pedido) | API tipo 10036, parámetros tipo 10037, propiedades tipo 10038 |
| Step | El spike no registra ninguno. La plataforma crea uno interno por Custom API (`CustomApi 'spk_GenerarFormatoSpike' implementation`) y lo borra con la API | no aplica |

**Por qué Acción y no Función:** el diseño final escribe bitácora y congela el arreglo (ítem 14 de la sección 6), es decir, tiene efectos secundarios. Una función se puede cachear y no debe tenerlos.

**Verificación por contenido después de cada despliegue** (`s10_paquete.py`; un 2xx no se toma como prueba):

- El tipo declarado está dentro del `.dll` empaquetado antes de subir (descarta el caso de `dotnet pack` que no reescribe el `.nupkg`).
- Se relee `pluginassemblies` (versión y `modifiedon`) y se compara contra el `AssemblyVersion` del `.csproj`.
- Se comparan en los dos sentidos ensamblados y tipos declarados contra los registrados: 0 faltan, 0 sobran.
- Se descarga el paquete guardado y se compara su SHA-256 con el local: igual.
- Se verifica la pertenencia a la solución (`solutioncomponents`).

| Despliegue | Ensamblado antes | Ensamblado después | `modifiedon` del ensamblado | Resultado |
|---|---|---|---|---|
| Alta | (nuevo) | 1.0.1.0 | 20:25:02Z | verificado |
| Cambio 1 | 1.0.1.0 | 1.0.2.0 | 20:25:02Z a 20:49:07Z (avanzó) | verificado; el siguiente llamado ejecutó `build=1.0.2.0` |
| Cambio 2 | 1.0.2.0 | 1.0.3.0 | 20:49:07Z a 20:49:47Z (avanzó) | verificado |
| Cambio 3 | 1.0.3.0 | 1.0.4.0 | 20:49:47Z a 20:50:15Z (avanzó) | verificado; es la versión que queda |

Una actualización del paquete tarda unos 2 a 6 segundos en reflejarse en `pluginassemblies`. La versión del paquete (1.0.0) no cambió en ningún redespliegue y la plataforma lo aceptó: lo que cambia es la del ensamblado.

## 3. Números medidos

Cliente HTTP en esta máquina (Linux, fuera del centro de datos), llamadas secuenciales con la misma plantilla v07 y los mismos datos ficticios que `spikes/plantilla/tools/Medicion` (60 cuotas con 12 pagos; 6 cuotas sin pagos; 30 cuotas con 12 pagos). La medición dentro del plugin usa `Stopwatch` desde el inicio de `Execute` hasta antes de devolver los parámetros (incluye la descarga de la plantilla).

**Primera llamada de un proceso del sandbox ("frío")**, es decir, `llamadaEnElProceso = 1`:

| Plazo | Plugin (ms) | de eso, generación (ms) | HTTP total (ms) | Muestras |
|---|---|---|---|---|
| 6 | 719, 944, 972 | 668, 871, 857 | 1.523 a 1.566 | 3 |
| 30 | 515, 670 | 464, 604 | 1.091 y 1.381 | 2 |
| 60 | 530, 614, (984) | 479, 545, (702) | 1.185, 1.472, (1.777) | 2 (+1) |

El valor entre paréntesis es la primera llamada tras crear el paquete (llamada de humo, no registrada en el archivo de mediciones).

**Llamadas siguientes en el mismo proceso ("caliente")**, `llamadaEnElProceso >= 2`, 39 a 40 muestras por plazo:

| Plazo | Plugin: mediana / p95 / máx (ms) | Generación: mediana / p95 (ms) | Descarga de plantilla: mediana (ms) | HTTP total: mediana / p95 / máx (ms) |
|---|---|---|---|---|
| 6 | 72 / 153 / 188 | 19 / 50 | 50 | 406 / 573 / 962 |
| 30 | 68 / 152 / 161 | 18 / 25 | 50 | 398 / 533 / 648 |
| 60 | 73 / 107 / 124 | 19 / 28 | 52 | 406 / 527 / 672 |

**Tamaños (iguales en sandbox y en local):**

| Plazo | Archivo | Base64 |
|---|---|---|
| 6 | 32.276 bytes | 43.036 caracteres |
| 30 | 32.585 bytes | 43.448 caracteres |
| 60 | 32.813 bytes | 43.752 caracteres (42,7 KB) |

**Comparación con el local.** Spike 1 (informe): primera llamada 496 a 711 ms; caliente 48 ms de mediana (60 cuotas) y 13 ms (6 cuotas); 32.817 bytes. Repetición hoy en esta máquina con el mismo código net8.0 y 300 corridas (`salida/local-medidas.txt`): primera llamada 514 ms; caliente 12 a 14 ms de mediana en los tres plazos. La diferencia entre los 48 ms del spike 1 y los 12 ms de hoy no está explicada (probablemente la carga de la máquina en aquel momento); no cambia las conclusiones.

| Medida | Local net8.0 | Sandbox net462 | Razón |
|---|---|---|---|
| Primera llamada (generación) | 514 a 754 ms | 464 a 871 ms | casi igual |
| Caliente (generación, 60 cuotas) | 12 ms | 19 ms (mediana) | 1,5 veces |
| Caliente (plugin completo con descarga) | no aplica | 72 ms | la descarga de la plantilla pesa 50 ms de esos 72 |
| HTTP completo | no aplica | 400 ms en caliente, 1,1 a 1,8 s en frío | dominado por la red y la puerta de entrada |

La duración que registra la propia plataforma en `plugintracelogs` (`performanceexecutionduration`) es de 78 a 110 ms en caliente y 1.203 a 1.249 ms en las dos primeras llamadas: unos 20 a 40 ms mayor que la del `Stopwatch` interno (serialización del contexto hacia el sandbox).

**Comportamiento del sandbox que conviene conocer:**

- **Hay varios procesos trabajadores.** Tras cada redespliegue aparecieron entre 2 y 3 llamadas con `llamadaEnElProceso = 1` seguidas (cada una con su arranque en frío de 500 a 950 ms), y después las llamadas se reparten entre procesos con contadores independientes. Para el usuario: tras publicar una versión nueva del paquete, las primeras 2 o 3 generaciones tardan unos 1,5 s en vez de 0,4 s.
- **Un proceso sobrevivió 6 minutos de inactividad** (contador 54 a 55 a 59 a 57 sin reiniciar, en tres pausas de 6 minutos). No se midió cuánto tarda en reciclarse; los únicos arranques en frío observados fueron justo después de crear o actualizar el paquete.
- **El límite de 2 minutos no es un riesgo:** el peor tiempo total dentro del plugin fue 1.068 ms (con la plantilla de 4,5 MB, dominado por la descarga).
- **Memoria:** `GC.GetTotalMemory` pasó de 27 a 34 MB por llamada con la plantilla real, y de 58 a 99 MB con la de 4,5 MB. Lejos de cualquier tope observado; no se midió el tope del proceso.

## 4. Qué cargó el proceso del sandbox (campo `Detalle` de la respuesta)

```
build=1.0.2.0; llamadaEnElProceso=1; ...; clr=4.0.30319.42000; procesadores=7; so64=True;
ensamblados=ArreglosPago.Formato 1.0.0.0 | DocumentFormat.OpenXml 2.20.0.0 | Spk.Plugins 1.0.x.0 |
            System.IO.Compression 4.0.0.0 | System.IO.Compression.FileSystem 4.0.0.0 | WindowsBase 4.0.0.0
```

`WindowsBase` y `System.IO.Compression` son los del propio .NET Framework del sandbox (no viajan en el paquete). `DocumentFormat.OpenXml` y la biblioteca sí viajan, en `lib/net462/` del paquete.

## 5. Cómo se verificó el contenido

`s30_verificar.py` llama a la API (plazo 6, 30 y 60), decodifica el Base64 (con `validate=True`) y comprueba:

1. **Contra el esperado local** (mismo código fuente de mapeo y datos, generado en net8.0 por `tools/LocalEsperado`): mismos nombres de partes y **bytes idénticos en las 24 partes** del paquete, en los tres plazos.
2. **Con un oráculo independiente** escrito aparte (Python, `openpyxl` y `zipfile`, `Decimal` para los montos, calendario propio; no usa el SDK ni el código del generador):
   - 23 de 23 celdas simples y totales con valor y tipo correctos (fechas como fecha, números como número, textos como texto).
   - Las 6, 30 y 60 cuotas con número, fecha, monto, fecha de pago y monto pagado correctos.
   - Filas sobrantes (54 y 30 de 60) sin valor ni fórmula y con el estilo y los bordes de la plantilla.
   - Celdas combinadas, área de impresión y anchos de columna iguales a los de la plantilla en las dos hojas.
   - Logo (imagen y dibujo) idéntico byte a byte a la plantilla.
   - Sin `TODAY()`, sin `calcChain.xml`, 0 fórmulas restantes, sin `fullCalcOnLoad` (los totales se escriben como valor).
3. **Control negativo:** dándole al oráculo el archivo de otro plazo, falla en cada plazo (celdas de parámetros y totales, cuotas, comentario y partes con bytes distintos). No es una verificación vacía.
4. **LibreOffice a PDF** (26.2): 3 páginas en los tres plazos, con nombre del cliente, oficial y comentario; revisada a ojo la de 60 cuotas (logo, datos, 60 filas, totales). Se ve el problema ya conocido de la columna "Fecha pago" con formato de moneda (hallazgo 4 del spike 1): el oráculo lee ahí el número de serie, y eso es de la plantilla, no del generador.

Los archivos devueltos por el sandbox quedan en `salida/sandbox-6.xlsx`, `sandbox-30.xlsx` y `sandbox-60.xlsx` (y el esperado local en `local-*.xlsx`). El de 60 coincide en tamaño con el esperado: 32.813 bytes. (El ejemplo del spike 1 pesa 32.817 porque su comentario tenía 5 caracteres más: "C-01".)

## 6. Errores controlados

`s40_errores.py`. Cada caso devuelve HTTP 400 con el mensaje exacto del `InvalidPluginExecutionException`, sin traza de pila y sin archivo parcial:

| Caso | Mensaje (resumido) |
|---|---|
| Huella distinta | `No se pudo generar el formato (HuellaNoCoincideException): La huella SHA-256 de la plantilla no coincide con la guardada al publicar la versión (esperada 0f6b..., calculada 8f6b...). La plantilla fue alterada o no es la versión publicada.` |
| Archivo que no es .xlsx (huella correcta) | `No se pudo generar el formato (PlantillaInvalidaException): La plantilla no es un archivo .xlsx (contenedor ZIP) válido.` |
| Versión sin archivo | `La versión de plantilla no tiene archivo cargado.` |
| Plazo 0, 61 y -3 | `El plazo debe estar entre 1 y 60 cuotas; se recibió N.` |
| Versión inexistente | `No se encontró la versión de plantilla <id> (o no hay permiso para leerla).` |

**Matiz.** Si la llamada llega mal formada (falta `Plazo` o `VersionId`, o `Plazo` es texto), el error lo produce la capa OData **antes** del plugin: HTTP 400, código `0x80048d19`, con texto técnico en inglés y fragmentos de traza. No se puede controlar desde el plugin. En el diseño real la llamada la arma la aplicación, no una persona; no es un riesgo, pero conviene que la aplicación no muestre ese texto tal cual.

Los errores también quedan en `plugintracelogs` con la excepción y la traza del plugin (la organización tiene el registro de trace en "All", así que también quedan las ejecuciones correctas). Se ve que el sandbox ejecuta el plugin por gRPC (`SandboxFabricGrpcClient`).

## 7. Límite de tamaño de la salida

**Documentación (Microsoft Learn):**

- No hay un tope publicado para una propiedad de respuesta de tipo String de una Custom API (`custom-api-tables`, `custom-api`).
- Las solicitudes a Dataverse admiten hasta 128 MB de payload (413 si se excede) y las respuestas hasta 1 GB (`work-with-data`).
- El contexto hacia el sandbox admite hasta 116,85 MB (error `Message size exceeded when sending context to Sandbox`, `dataverse-plug-ins-errors`).
- Cada proceso trabajador del sandbox tiene memoria finita (mismo documento): el riesgo documentado es de memoria, no de tamaño de parámetro.
- Un ensamblado no puede superar 16 MB (`build-and-package`): `DocumentFormat.OpenXml.dll` pesa 6,1 MB.
- El archivo de la columna File está limitado por `MaxSizeInKB` de la columna (aquí 5 MB), no por `maxuploadfilesize` de la organización.

**Prueba empírica:** con una plantilla de 4,5 MB (la v07 más una parte sin referenciar de 4,5 MB incompresibles), 5 llamadas con plazo 60: el archivo devuelto fue de 4.534.304 bytes y el Base64 de **6.045.740 caracteres**, sin error. Tiempos: plugin 426 a 1.068 ms (la descarga, en 2 bloques de 4 MB, es casi todo: 339 a 1.018 ms), generación 41 a 79 ms, HTTP 1,6 a 2,4 s. El local con la misma plantilla genera en 60 a 70 ms.

**Lectura:** el Base64 del formato real (43,7 mil caracteres) está unas 140 veces por debajo de lo que se probó con éxito y unas 2.700 veces por debajo de 116,85 MB. No hay nada que se acerque.

## 8. Limpieza (`s99_limpieza.py`)

Ensayo en seco por defecto; `--si-borrar` borra, en el orden que exige la plataforma. Solo toca objetos con el nombre exacto del spike y que pertenezcan a la solución (si existe uno que no pertenece, aborta). **No borra la solución ni el publisher**: los comparte con el spike 2, cuya limpieza está en `spikes/plataforma/s99_limpieza.py`.

1. Steps propios de los tipos del paquete (el spike no crea ninguno; el step interno de la Custom API lo borra la plataforma con la API).
2. Propiedades de respuesta y parámetros de entrada de la Custom API.
3. La Custom API.
4. El paquete de plugins (se lleva ensamblados y tipos).
5. La tabla `spk_versionplantilla` (se lleva columnas y filas; unos 40 s, con relectura hasta 404).

**Ensayo real de la secuencia (pasos 1 a 4)** con un conjunto de prueba aparte, `--conjunto prueba-limpieza` (paquete mínimo `spk_TmpLimpiezaPkg` más Custom API `spk_TmpLimpiezaApi`, creados con `s98_prueba_limpieza.py`): los 4 DELETE devolvieron 204 y cada relectura dio 404; el step interno desapareció con la API; 0 ensamblados y 0 tipos quedaron del paquete; la solución pasó de 25 a 20 componentes. El paso 5 (tabla) no se ensayó aquí; es el mismo `DELETE EntityDefinitions` que el spike 2 ya usó. No se probó qué responde la plataforma si se intenta borrar el paquete antes que la Custom API (se evitó a propósito para no borrar el conjunto real). El conjunto real **no se borró**: queda en el entorno para inspección. Inventario final en `salida/inventario-final.txt`: 20 componentes en la solución; 0 restos de la prueba de limpieza.

## 9. Problemas encontrados y cómo se resolvieron

| # | Problema | Causa | Solución |
|---|---|---|---|
| 1 | `POST pluginpackages` devolvió 400 `The nuget file name does not contain a solution prefix ... named: 0` | Con `name` en texto libre ("Spikes Arreglos Pago Sandbox") la plataforma no encuentra el prefijo | El `name` del paquete es el mismo `uniquename` con el prefijo del publisher: `spk_ArreglosPagoSandbox` |
| 2 | `POST customapis` con `plugintypeid@odata.bind` devolvió 400 `undeclared property 'plugintypeid'` | La propiedad de navegación distingue mayúsculas: `PluginTypeId@odata.bind`; en parámetros y propiedades, `CustomAPIId@odata.bind` | Se leyó el nombre real en `ManyToOneRelationships` de la metadata y se usó tal cual |
| 3 | Consulta de `pluginpackages` con `_solutionid_value` falló | En `pluginpackage` `solutionid` es un Guid interno, no un lookup, y no indica pertenencia | La pertenencia se lee de `solutioncomponents`: el paquete es tipo **10039**, la Custom API **10036**, el parámetro **10037** y la propiedad de respuesta **10038** |
| 4 | El oráculo marcó fallas en la columna "Fecha pago" | `openpyxl` lee un número de serie porque la celda tiene formato de moneda en la plantilla (hallazgo 4 del spike 1) | Se corrigió el oráculo (compara el serial), no el generador. Es un problema de la plantilla, ya conocido |
| 5 | La limpieza habría intentado borrar a mano el step `CustomApi '...' implementation` | La plataforma crea un step interno por cada Custom API | `s99` lo reconoce por nombre, no lo borra a mano y verifica que desaparezca con la API |
| 6 | Un solo arranque en frío por plazo no se podía medir | La primera llamada del script de medición no siempre era la primera del proceso, y el sandbox usa varios procesos | Se mide `llamadaEnElProceso` dentro del plugin: "frío" es la llamada nro 1 de un proceso; y se forzaron arranques en frío redesplegando el paquete |
| 7 | `sd` no estaba instalado | herramienta | Se usó Python y `Edit` para las sustituciones de texto |

Lo que **no** fue un problema y era un riesgo del spike 1: la biblioteca **no necesitó ningún cambio** para net462. `src/ArreglosPago.Formato/` de este spike es una copia idéntica a la de `spikes/plantilla/` (comprobado con `diff -r`); se copió solo para no generar `bin/` ni `obj/` dentro de `spikes/plantilla/`. Tampoco dio problemas que el `.nuspec` del paquete declare como dependencia NuGet a `ArreglosPago.Formato` (que no existe en ningún repositorio): la plataforma tomó los ensamblados de `lib/net462/` y no intentó resolverla. El paquete se compila en Linux con `dotnet build` (el paquete NuGet `Microsoft.PowerApps.MSBuild.Plugin` genera el `.nupkg`); no hace falta Visual Studio.

## 10. Impacto en D-13 y en el supuesto del sandbox

**Supuesto "Open XML SDK funciona dentro del sandbox de Dataverse"** (`definicion.md`, sección de supuestos): pasa de "falta probarlo dentro del sandbox y en net462" a **confirmado en el entorno de desarrollo**, para las tres cosas que nombraba:

- *Compatibilidad de `System.IO.Packaging`:* confirmada (`WindowsBase` del Framework, sin errores de carga ni escritura a disco).
- *Límite de 16 MB:* sobrado (el ensamblado mayor pesa 6,1 MB).
- *2 minutos:* medido 0,5 a 1 s en frío y 70 ms en caliente.

**D-13** (Custom API con Open XML SDK como ensamblado dependiente, respaldo en JavaScript) **se sostiene y el respaldo queda como plan de contingencia, no como camino probable**. La confianza "media" de D-13 puede subir en lo técnico del sandbox; lo que sigue abierto no es el sandbox sino lo que este spike no cubrió:

- Excel de escritorio sobre el archivo generado (el SDK reescribe las hojas con el prefijo `x:`), y la aprobación de la v07 por Héctor Romero.
- Las 205 pruebas del spike 1 en net462 (hoy solo se sabe que la salida es idéntica en tres plazos).
- La Custom API real: lee arreglo y cuotas con la identidad del llamador y la plantilla y el mapeo **como sistema**; aquí el llamador era administrador.
- Fechas: el plugin debe entregar la fecha local de Nicaragua (UTC-6) a la biblioteca, que escribe la parte de fecha tal cual llega.
- Comportamiento con varios usuarios simultáneos.

**D-15 (huella):** la validación de huella dentro del plugin funciona y falla de forma limpia (error de negocio con las dos huellas en el mensaje). No se probó que la subida por bloques dispare un step de Update (eso es del plugin de inmutabilidad, fuera del alcance de este spike).

**Efectos de diseño que salen de este spike:**

1. El generador debe ser **Acción** (bitácora y congelamiento son efectos secundarios).
2. **Latencia en frío:** tras publicar una versión del paquete, las 2 o 3 primeras generaciones tardan unos 1,5 s (medido desde fuera). En caliente, 0,4 s. Conviene que la pantalla muestre "generando..." y no considerar ese tiempo un error.
3. La descarga de la plantilla es la mitad del tiempo en caliente (50 de 72 ms): si algún día importa, se puede cachear la plantilla por huella dentro del proceso, pero hoy no hace falta.
4. Un paquete se actualiza por contenido y su versión (1.0.0) no cambia: el control de despliegue se hace con la versión del **ensamblado** (`AssemblyVersion`, tercer número) y su `modifiedon`, como dice `alm.md`.
5. El mensaje de error de huella incluye las dos huellas completas; es útil para soporte y no es dato sensible. Si se prefiere no mostrarlas al usuario, se recortan en el plugin.

## 11. Archivos (`spikes/sandbox/`)

| Archivo | Para qué |
|---|---|
| `src/ArreglosPago.Formato/` | Copia idéntica de la biblioteca del spike 1 (sin cambios) |
| `src/Spk.Plugins/` | Proyecto net462 del paquete: `GenerarFormatoSpikePlugin.cs`, `DatosFicticios.cs` (mapeo y datos fijos) y `Spk.Plugins.csproj` |
| `tools/LocalEsperado/` | Genera en net8.0 el esperado local con el mismo `DatosFicticios.cs` y mide tiempos |
| `tools/PruebaLimpieza/` | Paquete mínimo para ensayar la limpieza |
| `empaquetar.sh` | Borra `obj/` y `bin/` de los dos proyectos y empaqueta |
| `s00_estado.py` | Estado de base del entorno (solo lectura) |
| `s01_tabla.py`, `s02_plantilla_grande.py` | Tabla, columna File y filas de prueba |
| `s10_paquete.py` | Sube o actualiza el paquete y lo verifica por contenido |
| `s11_customapi.py` | Custom API, parámetros y propiedades; verificación en los dos sentidos |
| `s19_humo.py`, `s20_medir.py`, `s21_frio.sh`, `s22_resumen.py` | Llamada de humo, mediciones, arranques en frío y resumen |
| `s30_verificar.py` | Verificación del contenido (esperado local, oráculo independiente, LibreOffice) |
| `s40_errores.py` | Casos de error controlado |
| `s97_inventario.py`, `s98_prueba_limpieza.py`, `s99_limpieza.py` | Inventario, conjunto de prueba de limpieza y limpieza |
| `salida/` | Evidencia: archivos devueltos por el sandbox, esperados locales, `mediciones.jsonl` (131 llamadas), `frio.log`, `local-medidas.txt`, `inventario-final.txt` |
| `estado_sandbox.json` | Identificadores (sin secretos) de las filas, el paquete y la Custom API |

## 12. Cómo reproducir

```bash
cd spikes/sandbox
python3 s01_tabla.py && python3 s02_plantilla_grande.py        # tabla y filas
./empaquetar.sh && python3 s10_paquete.py                      # paquete, con verificación
python3 s11_customapi.py                                       # Custom API
python3 s30_verificar.py                                       # contenido (usa soffice y pdftotext)
python3 s40_errores.py                                         # errores controlados
python3 s20_medir.py --n 30 && python3 s22_resumen.py          # tiempos
(cd tools/LocalEsperado && dotnet run -c Release -- ../../../plantilla/F-NIC-0000052_v07_borrador.xlsx ../../salida 300)
python3 s99_limpieza.py                                        # ensayo en seco; --si-borrar para borrar
```

Para redesplegar: subir el tercer número de `<AssemblyVersion>` y `<FileVersion>` en `Spk.Plugins.csproj`, `./empaquetar.sh` y `python3 s10_paquete.py`.
