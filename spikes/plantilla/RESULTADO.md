# Resultado del spike C-01: relleno de la plantilla F-NIC-0000052 con Open XML

**Proyecto:** 2026-003 Control de Arreglos de Pago. **Condición:** C-01 (factibilidad técnica de RF-06/RF-08). **Decisiones que valida:** D-13, D-15, D-16.
**Alcance:** todo en local. No se tocó ningún entorno de Dataverse, ni `material/`, ni `definicion.md`.
**Estado:** el prototipo cumple el diseño en local. Quedan dos validaciones que este spike no puede dar (el sandbox de Dataverse y Excel de escritorio) y la aprobación de Héctor Romero sobre la v07.

## 1. Veredicto por punto de C-01

| Punto | Veredicto | Evidencia |
|---|---|---|
| **Fidelidad del relleno** (mapeo celda → campo, tipos correctos) | **Cumple** en local | 205 casos de prueba en verde. Un lector independiente (XML crudo, sin el SDK), `openpyxl` y LibreOffice leen el resultado con los mismos valores y tipos. Números como número, fechas como serial de Excel con el formato de la celda, textos como texto compartido (un texto `=1+1` queda como texto, nunca como fórmula). Un monto de 16 cifras (9876543219876543) queda exacto en el archivo. |
| **Hasta 60 cuotas** | **Cumple** | La v07 recibe 60 cuotas en las filas 7 a 66; el total sale de los datos (suma exacta en `decimal`). Plazo 61, plazo 0, y plazo mayor que la capacidad de la versión (48 en la v06) se rechazan con mensaje claro. |
| **Filas sobrantes** | **Cumple** | Con 6 cuotas, las filas 13 a 66 quedan sin valor ni fórmula y con su estilo y bordes. Se probó también contra la v06 real (números de cuota preimpresos 1 a 48) y contra una plantilla con fórmulas, texto y números sueltos en filas sobrantes. |
| **Recálculo** | **Cumple** | `=TODAY()` desaparece (B7 y E4). Los totales se escriben como valor. Si quedan fórmulas (por ejemplo totales sin mapear, o la v06 con TODAY sin mapear) se marca `fullCalcOnLoad`; si no quedan, no se marca. Se elimina `calcChain.xml`, porque con celdas que dejan de ser fórmula Excel pediría reparar el archivo. |
| **Conservación de formato** | **Cumple con una salvedad de impresión** | Logo (imagen y dibujo idénticos byte a byte), 14 y 8 celdas combinadas, área de impresión, configuración de página, anchos y `styles.xml` (idéntico byte a byte), y el StyleIndex de todas las celdas. Salvedad: con 60 filas la hoja CONTROL DE PAGOS se imprime en 3 hojas y la cláusula queda partida (observación 1 de `CAMBIOS-v07.md`); es un tema de la plantilla, no del generador. |
| **Open XML SDK dentro del sandbox de Dataverse** | **No validado** (fuera del alcance local) | Ver sección 6. |
| **Plantilla v07 aprobada por Héctor Romero** | **Pendiente** | Borrador y lista de cambios listos: `F-NIC-0000052_v07_borrador.xlsx`, `CAMBIOS-v07.md`. Hay 7 observaciones que él debe decidir. |

**Conclusión:** no apareció nada que obligue a activar el plan B de D-13 (generación en JavaScript en el cliente). La decisión de mantener Open XML SDK sigue en pie, condicionada a la validación en el sandbox.

## 2. Qué se entrega (todo bajo `spikes/plantilla/`)

| Archivo | Para qué |
|---|---|
| `F-NIC-0000052_v07_borrador.xlsx` | Plantilla v07 borrador, generada desde una copia de la v06 (`base/F-NIC-0000052_v06_copia.xlsx`). |
| `CAMBIOS-v07.md` | Cambios celda por celda contra la v06, y siete observaciones para Héctor. |
| `comparacion/` | PDF e imágenes de la v06 y la v07 exportados con LibreOffice 26.2 (sí está instalado), y de los ejemplos. |
| `src/ArreglosPago.Formato/` | Biblioteca `net462;net8.0`. Sin dependencias de Dataverse. API: `GeneradorFormato.Generar(byte[] plantilla, string huellaEsperada, IReadOnlyList<MapeoCelda> mapeo, DatosArreglo datos)`, más `GenerarConInforme` (devuelve además advertencias y si marcó recálculo), `CalcularHuella` y `ValidarMapeo` (validación pura del mapeo, pensada para el plugin que publica la versión, ítem 15). |
| `tests/ArreglosPago.Formato.Tests/` | xUnit en `net8.0`: 88 métodos, 205 casos. |
| `salida-ejemplo-60-cuotas.xlsx/.pdf` y `salida-ejemplo-6-cuotas.xlsx/.pdf` | Ejemplos con datos ficticios (el de 60 trae 12 pagos registrados). |
| `tools/crear_v07.py`, `tools/verificar_v07.py` | Cómo se hizo la v07 y cómo se comprobó que solo cambió lo pedido. |
| `tools/mutaciones.py` | Prueba por mutación (sección 4). |
| `tools/Medicion/` | Genera los ejemplos y mide tiempo, tamaño y memoria. |

## 3. Números medidos

Máquina de desarrollo (8 CPU, Linux, .NET 8.0.30, compilación Release), plantilla v07, 60 cuotas con 12 pagos registrados, 100 corridas en caliente:

| Medida | 60 cuotas | 6 cuotas |
|---|---|---|
| Tiempo de la primera llamada (en frío: carga de ensamblados y JIT) | 0,5 a 0,7 s (4 corridas: 532, 711, 496 y 509 ms) | no aplica |
| Tiempo en caliente, mediana / p95 / máximo | **48 ms** / 71 ms / 114 ms | 13 ms / 22 ms / 26 ms |
| Tamaño del .xlsx resultante | **32.817 bytes (32,0 KB)** | 32.280 bytes (31,5 KB) |
| Tamaño en Base64 (lo que devuelve la Custom API) | **43.756 caracteres (42,7 KB)** | 43.040 caracteres (42,0 KB) |
| Memoria asignada por llamada | 2,2 MB | 2,1 MB |
| Pico de memoria del proceso de medición | 86 MB (incluye el runtime y el SDK) | |

Otras medidas: plantilla v07 = 32.236 bytes; `ArreglosPago.Formato.dll` = 44 KB; `DocumentFormat.OpenXml.dll` (net46) = 6,1 MB, sin otras dependencias.

Lectura: el trabajo es lo bastante liviano como para que el límite de 2 minutos del plugin síncrono no sea el riesgo (hay unas 2.500 veces de margen en la medición en caliente y unas 200 veces en frío). Es una medición en .NET 8; en .NET Framework 4.6.2 y en el sandbox compartido hay que repetirla (sección 6).

## 4. Cómo se verificó

**TDD estricto, en cuatro ciclos.** Cada ciclo empezó con pruebas en rojo y se implementó después:

1. Huella y validación del mapeo: 63 pruebas rojas (todas contra una implementación que lanzaba `NotImplementedException`), luego 68 de 69 en verde. La restante necesitaba la generación.
2. Relleno, bloque de cuotas, conservación y recálculo: 87 rojas. Tras la primera implementación quedaron 5 rojas: 4 eran errores de las pruebas (un serial de fecha mal calculado, dos comparaciones de XML por texto y una prueba que miraba la hoja equivocada) y 1 mostró un hallazgo real (el SDK reescribía `styles.xml`).
3. Contenido corrupto dentro de un contenedor ZIP válido: 29 rojas. Mostraron siete excepciones crudas distintas (ver hallazgo 6). Se resolvió con `TryParse` en cada atributo que se lee del archivo, lectura de `styles.xml` sin pasar por el modelo del SDK, y una frontera de error controlado.
4. Celdas combinadas y sistema de fechas 1904: 5 rojas, luego verdes. Después, fórmulas compartidas o matriciales: 2 rojas, luego verdes.

Algunas pruebas de caracterización (estructura de hoja, filas sobrantes "sucias") se agregaron cuando el comportamiento ya existía y pasaron al primer intento; se cubren con la prueba por mutación de abajo.

**Contenido corrupto (regla de `dataverse/pruebas.md`).** 27 casos de archivo corrupto (25 con ZIP válido y contenido interno roto: sharedStrings vacío, con XML sin cerrar, con basura binaria o con otra raíz; `styles.xml` roto o con `numFmtId` no numérico o desbordado; hoja vacía; fila o celda con índice inválido, negativo o desbordado; estilo no numérico, fuera de rango o desbordado; combinadas inválidas; ZIP sin workbook; y 2 que ni siquiera son un ZIP: texto plano y archivo vacío) más 6 casos de corrupción en partes que el generador no interpreta. En todos el resultado es `PlantillaInvalidaException` o una `FormatoException`; ninguna excepción cruda. También hay límites para un ZIP con demasiadas partes (más de 500) o que descomprime a más de 50 MB.

**Prueba por mutación.** Se rompió a propósito la implementación, una cosa por vez, en una copia fuera del repositorio (`tools/mutaciones.py`), y se comprobó que alguna prueba falle:

| Id | Defecto introducido | Resultado | Pruebas que fallan |
|---|---|---|---|
| M01 | decimal escrito via double (pierde precision) | Detectada | 3 |
| M02 | total acumulado en double | Detectada | 2 |
| M03 | no limpiar las filas sobrantes del bloque | Detectada | 5 |
| M04 | no reemplazar la formula de la celda | Detectada | 5 |
| M05 | perder el StyleIndex de la celda escrita | Detectada | 31 |
| M06 | no verificar la huella | Detectada | 8 |
| M07 | no controlar el plazo contra la capacidad | **Sobrevive** | 0 |
| M07b | no controlar el plazo contra la capacidad de la version | Detectada | 1 |
| M07c | no controlar el plazo (ni maximo general ni capacidad de la version, ambos) | Detectada | 4 |
| M08 | Parse en vez de TryParse en el indice de fila | Detectada | 3 |
| M09 | sin frontera de error controlado | Detectada | 7 |
| M10 | no eliminar calcChain | Detectada | 2 |
| M11 | no marcar recalculo al abrir | Detectada | 2 |
| M12 | no detectar dos campos en la misma celda | Detectada | 2 |
| M13 | no detectar celda que se pisa con el bloque | Detectada | 4 |
| M14 | no detectar bloque incompleto | Detectada | 6 |
| M15 | ignorar el sistema de fechas 1904 | Detectada | 2 |
| M16 | no acotar tamaño del contenedor | Detectada | 1 |
| M17 | reemplazar una sharedStrings vacia por una nueva | Detectada | 2 |
| M18 | toda celda cuenta como de fecha (sin advertencia) | Detectada | 1 |
| M19 | no validar caracteres de control en textos | Detectada | 1 |
| M20 | aceptar double como valor | Detectada | 1 |
| M21 | no validar numFmtId con TryParse (usa Parse) | Detectada | 2 |
| M22 | estilo de celda con Parse | Detectada | 2 |
| M23 | escribir texto como numero (sin sharedStrings) | Detectada | 15 |
| M24 | capacidad de bloque ignorada: limpiar solo hasta el plazo | Detectada | 5 |
| M25 | no avisar de celdas en la parte oculta de una combinacion | Detectada | 2 |
| M26 | no validar el rango de fechas del sistema 1904 | Detectada | 1 |
| M27 | rango de combinada con Split sin validar (usa Parse) | Detectada | 1 |
| M28 | reemplazar la maestra de una formula compartida/matricial | Detectada | 2 |

Resultado: 29 de 30 mutaciones detectadas. **M07 sobrevive y es equivalente**: el máximo general de 60 y la capacidad de la versión protegen la misma condición con la v07 (capacidad 60), y `ValidarMapeo` ya impide capacidades mayores que 60. Es defensa en profundidad redundante; quitando las dos a la vez (M07c) fallan 4 pruebas, y quitando solo la de la versión (M07b) falla la de la v06 con 49 cuotas. Las mutaciones de precisión (M01, M02) se diseñaron con montos que un `double` no conserva: 9876543219876543 (mayor que 2^53) y 3 × 9876543219876,543.

**Lectores independientes.** Además del lector XML crudo de las pruebas: `openpyxl` (valores y tipos de los ejemplos), LibreOffice (PDF de los ejemplos, revisado a ojo) y el `OpenXmlValidator` del SDK (la salida no suma errores de esquema respecto de la plantilla).

## 5. Hallazgos

Los que cambian algo del diseño o del mapeo van primero.

1. **El nombre de la hoja FORMATO empieza con un espacio: `" FORMATO ARP ELECTRONICO"`.** El mockup (`HOJAS.FORMATO`, `MAP_V07`) lo escribe sin el espacio, y contra el archivo real esa fila de mapeo no encontraría la hoja. El generador compara el nombre exacto y, si no coincide, responde con las hojas reales entre comillas. Recomendación para el plugin del ítem 15: que el jefe elija la hoja de una lista leída del archivo (no que la escriba), o que la v07 definitiva renombre la hoja (un cambio más que Héctor debe aprobar).
2. **Los datos del bloque se interpretan por tipo de campo, no por nombre libre.** `cuota.*` y `total.*` están reservados; un bloque completo exige las cinco columnas (número, fecha, monto, fecha de pago, monto pagado). Es una decisión del prototipo para cerrar la ambigüedad de "bloque incompleto" del ítem 15; si negocio quiere una plantilla sin columnas de pago, hay que cambiarla.
3. **Valor `null` y clave ausente se distinguen.** `null` deja la celda vacía; una clave ausente para un campo mapeado es un error que nombra el campo y la celda (atrapa un campo mal escrito en el mapeo). Conviene que el plugin llene todos los campos del choice.
4. **La columna "Fecha pago" tiene formato de moneda** (heredado de la v06). Una fecha escrita ahí se ve como `$46,335.00`. El generador lo detecta y lo informa en `Advertencias`, sin cambiar el estilo (el diseño exige conservar el StyleIndex). Hay que decidirlo con Héctor (observación 3 de `CAMBIOS-v07.md`) antes de generar un formato con pagos registrados.
5. **Excel guarda 15 cifras significativas.** El archivo conserva exactos los 16 dígitos, pero al abrirlo Excel los redondea a 15. Los números de cuenta o de préstamo deben viajar como texto (como ya hace el formato: F15 y B12 son de formato texto). Las pruebas lo cubren con un monto de 16 cifras, que es un caso de laboratorio.
6. **El SDK deja escapar siete excepciones crudas distintas ante contenido corrupto:** `XmlException`, `InvalidDataException`, `FormatException`, `OverflowException`, `NullReferenceException`, `ArgumentOutOfRangeException` y `FileFormatException`. La frontera de error controlado es obligatoria. El plugin debe registrar en el trace la excepción interna (`ex.InnerException`) antes de convertirla en `InvalidPluginExecutionException`.
7. **Hay que eliminar `calcChain.xml`** al reemplazar fórmulas por valores, o Excel ofrece "reparar" el archivo. El generador lo hace siempre.
8. **Conviene no leer `styles.xml` con el modelo del SDK:** abrirlo hace que el SDK lo reescriba al guardar (por ejemplo, la declaración XML pasa de `UTF-8` a `utf-8`). Se lee con `XmlReader` y queda idéntico byte a byte.
9. **El SDK reescribe las hojas con el prefijo `x:`** (`<x:worksheet>`), con los mismos nombres calificados, `mc:Ignorable` y espacios de nombres declarados. Es XML equivalente y válido (lo confirman el validador del SDK, LibreOffice y `openpyxl`), pero **no se abrió con Excel de escritorio**: es una de las dos validaciones que faltan.
10. **Metadatos de personas en la plantilla** (autor `hromero`, última modificación por `Roger Moran Navarrete`, ruta `C:\Users\roger.moran\...`): cada formato generado los hereda (observación 7 de `CAMBIOS-v07.md`).
11. **Dependencia de `DocumentFormat.OpenXml`:** se eligió la 2.20.0 porque su destino net46 no trae dependencias (usa `System.IO.Packaging` de `WindowsBase`). La 3.x agrega `DocumentFormat.OpenXml.Framework` y `System.IO.Packaging` 8 como ensamblados dependientes. La 2.x está en mantenimiento; revisar en la etapa de construcción si conviene la 3.x.
12. **`openpyxl` no sirve para esta tarea:** conserva la imagen (con Pillow instalado) pero descarta `printerSettings`, `customXml` y `calcChain` y reescribe todas las partes. Por eso la v07 se hizo editando el XML del paquete.
13. **Hay 3 hojas impresas con 60 filas** (observación 1 de `CAMBIOS-v07.md`) y la etiqueta nueva de A12 no entra en su columna (observación 2).

## 6. Qué falta validar dentro del sandbox de Dataverse

Nada de esto se puede comprobar en local; es el siguiente spike, en el entorno de desarrollo.

| Qué | Por qué importa | Qué se sabe |
|---|---|---|
| **Compatibilidad net462 del paquete** | Las pruebas corrieron solo en `net8.0`. La biblioteca compila para `net462` sin errores ni advertencias, pero no se ejecutó en .NET Framework. En net462 el SDK usa el `System.IO.Packaging` de `WindowsBase`; en net8.0 usa el paquete NuGet, que no es la misma implementación. | Falta correr las 205 pruebas con `-f net462` en Windows (o en un entorno con Framework) antes del sandbox. |
| **Que el sandbox cargue `DocumentFormat.OpenXml.dll` (6,1 MB) como ensamblado dependiente** de un plugin package | La documentación de Microsoft dice que el límite es de 16 MB por ensamblado, que los paquetes de plugin admiten ensamblados dependientes (sin código no administrado) y que no es necesario firmar dentro de un paquete. | No se probó el registro ni la carga. |
| **`System.IO.Packaging` y `System.IO.Compression` bajo el aislamiento del sandbox** | El generador trabaja solo con `MemoryStream` (no usa disco), pero hay que confirmar que `WindowsBase` y `ZipArchive` funcionan en el proceso aislado y que no intentan escribir archivos temporales. | No verificado. |
| **Límite de 2 minutos** | Un plugin síncrono se corta a los 2 minutos. | Medido: 0,5 a 0,7 s en frío y 48 ms en caliente en la máquina de desarrollo. Hay que repetirlo en el sandbox (CPU compartida, .NET Framework). |
| **Límite de memoria y tamaño de salida** | La Custom API devuelve el archivo como texto Base64 de 42,7 KB. El generador asigna unos 2,2 MB por llamada. | No se encontró en la documentación revisada un tope para el parámetro de salida de tipo texto; se sabe que el contexto hacia el sandbox tiene un tope de unos 117 MB y que cada proceso trabajador tiene memoria finita. Confirmar en el entorno. |
| **Apertura con Excel de escritorio** y con el visor que usarán los oficiales | Es la validación de fidelidad que aquí no se pudo hacer (solo LibreOffice, `openpyxl` y el validador del SDK). | Incluirla en la aprobación de la v07 por Héctor: abrir `salida-ejemplo-60-cuotas.xlsx` y confirmar que Excel no ofrece reparar el archivo. |
| **Integración con Dataverse** | La biblioteca no conoce Dataverse. El plugin debe construir `DatosArreglo` (instantánea del cliente si está congelado) y traducir `FormatoException` a `InvalidPluginExecutionException`. | Por construir. Ojo con las fechas: la biblioteca escribe la parte de fecha tal cual llega; Dataverse guarda fechas y horas en UTC, así que el plugin debe entregar la fecha local de Nicaragua (UTC-6) o usar columnas de solo fecha. |

## 7. Limitaciones del prototipo

- Un solo bloque de cuotas por libro (una sola hoja), con las cinco columnas.
- No crea estilos ni fórmulas; solo escribe valores en celdas existentes (si la celda no existe la crea con el estilo de su columna).
- No escribe en celdas que sean la parte oculta de una celda combinada: lo rechaza como error de mapeo.
- No reemplaza la celda maestra de una fórmula compartida o matricial (daría un archivo que Excel pide reparar): lanza error. Si la fórmula compartida está toda dentro del bloque tampoco se acepta; es conservador.
- Rechaza filas sin atributo `r` o con índices desordenados (Excel siempre los escribe).
- Fechas anteriores a 1900-03-01 se rechazan (bug del año bisiesto 1900 de Excel); se respeta el sistema de fechas 1904 si el libro lo usa.
- Los límites de ZIP se calculan con los tamaños declarados en el contenedor; un ZIP que mienta sobre su tamaño no queda cubierto (la plantilla viene de Dataverse y además está protegida por la huella).
- El código del generador tiene un `catch` general que convierte cualquier otro error del SDK en `PlantillaInvalidaException`. Conserva la excepción original como interna, pero puede ocultar un defecto propio con el mismo rótulo; por eso se pide registrar `InnerException` en el trace.

## 8. Riesgos

| Riesgo | Prob. | Impacto | Mitigación |
|---|---|---|---|
| El sandbox no carga el SDK, o `WindowsBase` falla bajo aislamiento | media | alto | Spike inmediato en desarrollo; plan B de D-13 (JavaScript en el cliente). |
| Excel de escritorio ofrece reparar el archivo generado | baja | alto | Probar `salida-ejemplo-60-cuotas.xlsx` antes de aprobar la v07; el validador del SDK, LibreOffice y `openpyxl` no encontraron problemas. |
| Un mapeo con el nombre de hoja mal escrito (espacio inicial) | media | medio | Lista de hojas leída del archivo al publicar; el generador falla con mensaje que lista las hojas reales. |
| Fechas de pago mostradas como moneda | alta cuando haya pagos | medio | Decisión de Héctor sobre el formato de D7:D66 (observación 3). |
| Impresión en 3 hojas con cláusula partida | alta | medio | Observación 1 de `CAMBIOS-v07.md` (escala 60 %). |
| Metadatos de personas en cada formato entregado | alta | bajo a medio | Limpieza en la v07 definitiva (observación 7). |
| Dependencia de una versión en mantenimiento del SDK | baja | bajo | Revisar 3.x en la construcción. |
| Plantilla con fórmulas compartidas o columna combinada en futuras versiones | baja | medio | El generador rechaza esos casos con mensaje claro y la validación al publicar (`ValidarMapeo` + generación de vista previa) los atrapa. |

## 9. Cómo reproducir

```bash
cd spikes/plantilla
python3 tools/crear_v07.py base/F-NIC-0000052_v06_copia.xlsx F-NIC-0000052_v07_borrador.xlsx
python3 tools/verificar_v07.py base/F-NIC-0000052_v06_copia.xlsx F-NIC-0000052_v07_borrador.xlsx   # diferencias celda por celda
dotnet test tests/ArreglosPago.Formato.Tests                                                       # 205 casos
dotnet build src/ArreglosPago.Formato                                                              # net462 y net8.0
dotnet run --project tools/Medicion -c Release -- F-NIC-0000052_v07_borrador.xlsx . 100            # ejemplos y mediciones
python3 tools/mutaciones.py <carpeta-temporal-fuera-del-repo>                                      # prueba por mutación (unos 40 minutos en serie)
```
