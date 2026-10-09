# Cambios de la plantilla F-NIC-0000052: v06 → v07 (borrador)

**Para:** Héctor Romero (dueño del formato). **Estado:** borrador para aprobación (condición C-01). No está publicado en ningún entorno.

Este documento permite aprobar la v07 sin abrir los dos archivos lado a lado. Lista cada celda que cambia y confirma que todo lo demás quedó igual.

| | Archivo | SHA-256 |
|---|---|---|
| Base (copia de `material/Formulario_de_Arreglos_de_Pagos.xlsx`, sin modificar el original) | `base/F-NIC-0000052_v06_copia.xlsx` | `062d85257aecc988ac53cfaff6b9190bd3a0fe9df96f4159e15f964aa26bfee2` |
| Borrador v07 | `F-NIC-0000052_v07_borrador.xlsx` | `8f6b4dd58f422ee55ba548f8bcbbf9432dbc59eb9564894d659c9a2367d4f63a` |

Comparación visual (LibreOffice 26.2, exportación a PDF): `comparacion/v06.pdf` frente a `comparacion/v07.pdf` y sus imágenes `v06-*.png`, `v07-*.png`.

Nota sobre nombres de hoja: la primera hoja se llama **` FORMATO ARP ELECTRONICO` con un espacio al principio** (así está en la v06 y se conserva). La segunda es `CONTROL DE PAGOS`.

## 1. Cambios, celda por celda

### Hoja ` FORMATO ARP ELECTRONICO`

| Celda | v06 | v07 | Formato de la celda |
|---|---|---|---|
| A4 | `F-NIC-0000052-Formato de Arreglo de Pagos Préstamos Versión 06` | `F-NIC-0000052-Formato de Arreglo de Pagos Préstamos Versión 07` | Sin cambio (combinada A4:F4) |
| B7 (Fecha Formalización) | fórmula `=TODAY()` (fecha de hoy, cambiaba al abrir) | **vacía**. La completa el generador con la fecha de formalización guardada | Sin cambio (`dd/mm/aaaa`) |
| F7 (Oficial de Arreglo de Pagos) | texto `Roger Moran` | **vacía**. La completa el generador | Sin cambio |
| A12 (etiqueta) | `Número de Préstamo:` | `Número de Préstamo / Tarjeta:` | Sin cambio |

### Hoja `CONTROL DE PAGOS`

| Celda(s) | v06 | v07 | Formato de la celda |
|---|---|---|---|
| E4 (Fecha) | fórmula `=TODAY()` | **vacía**. La completa el generador con la misma fecha fija | Sin cambio (fecha corta) |
| A7:A54 (N° cuota) | números 1 a 48 escritos a mano | **vacías**. El generador escribe el número solo en las cuotas que existen | Sin cambio |
| Filas 55 a 66 (cuotas 49 a 60) | no existían (la fila 55 era la de totales) | **filas nuevas** con calendario completo hasta 60 cuotas (filas 7 a 66). Columnas A a E con el mismo estilo de celda, la misma altura (14,1) y los mismos bordes que la fila 54 | Idéntico a la fila 54 |
| Fila de totales: antes 55, ahora **67** | `B55 = TOTALES`, `C55 = SUM(C7:C42)`, `E55 = SUM(E7:E42)` | `B67 = TOTALES`, `C67 = SUM(C7:C66)`, `E67 = SUM(E7:E66)` | Idéntico (doble subrayado, moneda) |
| Cláusula legal (antes A57:A60) | filas 57 a 60 (cuatro líneas combinadas A:E) | **desplazadas 12 filas**: ahora A69:A72, con el mismo texto y las mismas combinaciones (A69:E69, A70:E70, A71:E71, A72:E72) | Idéntico |
| Firmas (antes fila 64) | `A64 = Firma Cliente`, `D64 = Firma del Oficial de Arp` | `A76`, `D76`, mismo texto | Idéntico |
| Fila combinada vacía A66:D66 (antes) | fila 66 | ahora A78:D78 | Idéntico |

Hallazgo que corrige este cambio: en la v06 la fila de totales sumaba solo `C7:C42` y `E7:E42`, es decir 36 de las 48 filas de cuotas. En la v07 suma las 60 filas.

Nota: los totales siguen siendo fórmulas dentro de la plantilla. El generador no las usa: calcula los totales desde los datos y los escribe como valor (D-16). Quedan en el archivo para que el formato en blanco siga calculando si alguien lo llena a mano.

## 2. Qué NO cambió

Verificado con `tools/verificar_v07.py` (compara valor y estilo de cada celda de ambas hojas):

- **Logo BAC:** la imagen, el dibujo y su ancla (`xl/media/image1.png`, `xl/drawings/drawing1.xml`) son idénticos byte a byte. Sigue en la hoja FORMATO.
- **Celdas combinadas:** 14 en FORMATO y 8 en CONTROL, las mismas que en la v06 (las 5 de la zona de cláusulas y firmas se desplazaron con ellas).
- **Estilos:** `xl/styles.xml` idéntico byte a byte. Ninguna celda cambia de estilo; las celdas nuevas reutilizan los estilos de la fila 54.
- **Anchos de columna, tema, configuración de impresora, propiedades del documento:** idénticos byte a byte.
- **Área de impresión:** `' FORMATO ARP ELECTRONICO'!$A$1:$F$58`, igual que la v06. La hoja CONTROL DE PAGOS **no tenía área de impresión definida en la v06**, así que no se agregó ninguna (ver observación 1).
- **Configuración de página:** FORMATO a 58 % ajustado a una página de ancho; CONTROL a 69 %, vertical. Sin cambios.
- **Textos:** todos los demás, incluidas las cláusulas legales de las dos hojas, son idénticos (la comparación celda por celda no encontró otra diferencia que las de la sección 1).
- **Cómo se hizo:** edición directa del XML del paquete. No se usó una librería que reescriba el archivo, porque `openpyxl` descarta de la v06 las partes `printerSettings`, `customXml` y `calcChain` y reescribe todas las demás.

Detalle técnico (para quien lo revise): partes modificadas, solo cuatro: `xl/worksheets/sheet1.xml`, `xl/worksheets/sheet2.xml`, `xl/sharedStrings.xml` (el texto `Roger Moran` se eliminó del archivo, no solo de la celda) y `xl/calcChain.xml` (se actualizó para que no apunte a las fórmulas que ya no existen).

## 3. Observaciones que NO se aplicaron (decisión de Héctor)

Se encontraron al revisar el PDF. Ninguna estaba en el encargo y la regla fue no hacer más cambios que los cinco pedidos. Cada una tiene una propuesta; si se aprueba, se aplica en una v07 definitiva.

| N° | Observación | Evidencia | Propuesta |
|---|---|---|---|
| 1 | **La hoja CONTROL DE PAGOS con 60 filas ya no cabe en una página.** Se imprime en 3 hojas y la cláusula legal queda cortada entre la página 2 y la 3 (las firmas quedan solas en la 3). La v06 con 48 filas cabía en una. | `comparacion/v07-2.png` y `v07-3.png` | Bajar la escala de impresión de CONTROL de 69 % a 60 %: en LibreOffice el documento completo pasa a 2 hojas (una por pestaña). Probado con 60, 58 y 56 %; con 62 % todavía cabe y con 64 % no. Habría que confirmarlo imprimiendo desde Excel, porque la paginación puede diferir. Otra opción: salto de página manual antes de la cláusula. |
| 2 | **La etiqueta nueva de A12 no entra en su celda** cuando B12 trae el número: se ve `Número de Préstamo / Ta`. La columna A mide 24,4 y la etiqueta tiene 29 caracteres. | `comparacion/ej60-1.png` | Ensanchar la columna A a unos 31, o acortar la etiqueta (por ejemplo `N° Préstamo / Tarjeta:`). |
| 3 | **La columna "Fecha pago" (D7:D66) tiene formato de moneda**, igual que en la v06. Una fecha de pago escrita ahí se ve como `$46,335.00`. Hoy está vacía y el defecto no se nota, pero aparecerá en cuanto se regenere un formato con pagos registrados (el diseño lo prevé, definicion.md ítem 10). El generador lo detecta y devuelve una advertencia. | `comparacion/ej60-2.png` | Cambiar el formato de D7:D66 al de fecha de la columna B (estilo 36). Es un cambio de formato de 60 celdas. |
| 4 | **Dos celdas de la columna "Monto pagado" tienen otro borde.** E15 y E24 (cuotas 9 y 18) no tienen borde superior ni inferior, a diferencia del resto. Viene así de la v06. | Estilo 39 frente a 38 en `xl/styles.xml` | Igualar con el estilo 38. |
| 5 | **El texto "Este formato consta de una hoja adjunta." (FORMATO!A58)** puede quedar desactualizado si el calendario se imprime en más de una hoja. Se conservó porque el encargo pide dejar todo texto idéntico. | | Revisar con la observación 1. |
| 6 | **El comentario (FORMATO!A18:F18) ocupa una sola línea.** Las filas 19 a 23 están combinadas y con borde, pero el mapeo propuesto solo escribe en A18, y esa celda no ajusta el texto (estilo 54 sin ajuste de texto). Un texto más largo que el ancho de A18:F18 se corta. | Prueba con 170 caracteres: entra en la línea. No se probó el límite exacto | Definir el largo máximo del comentario o mapear también A19 a A23. |
| 7 | **Metadatos del archivo con nombres de personas.** Aunque se quitó "Roger Moran" de F7, el paquete conserva en `docProps/core.xml` el autor `hromero` y "última modificación por" `Roger Moran Navarrete`, y en `xl/workbook.xml` la ruta `C:\Users\roger.moran\Documents\Formatos ARP\`. Cada formato generado para un cliente heredaría esos metadatos (no se ven en la hoja, sí en Propiedades del archivo). | `docProps/core.xml`, `xl/workbook.xml` | Limpiar autor, última modificación y ruta antes de publicar la v07 (se hace en una v07 definitiva; el generador no los toca). |
