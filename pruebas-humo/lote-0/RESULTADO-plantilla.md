# Lote 0 · Plantilla real v07 en Dev (inventario 9.1 y 9.2)

Fecha: 2026-10-20. Entorno: Dev (`org36e60d9d`). Caller: administrador de Dev. Todo dato de prueba es HUMO-.

El usuario confirmó el 2026-10-20 que `spikes/plantilla/F-NIC-0000052_v07_borrador.xlsx` es la versión que aprobó Héctor Romero, y que el formato generado en H-11 abre en Excel de escritorio sin pedir reparación.

## Qué se hizo

| Paso | Cómo | Resultado |
|---|---|---|
| Versión v07 en Borrador con el archivo | `datos.py playbooks/datos/versionplantilla.md` | `creado`; el archivo se sube como `F-NIC-0000052_v07.xlsx` y se verifica bajándolo (SHA-256) |
| 28 mapeos de MAP_V07 | `datos.py playbooks/datos/mapeocelda.md` | `creado` (28 de 28) |
| Publicar v07 | gesto Publicar (PATCH `sanic_estado` = Vigente), `scripts/l10_plantilla_v07.py` | 204; v07 Vigente con huella = SHA-256 del archivo aprobado, aprobador y fecha; **v95 (HUMO) pasó a Histórica**; queda una sola Vigente |
| Re-verificación después de publicar | `datos.py ... --solo-verificar` (los dos playbooks) | `ya_existia` (1 y 28) |
| Formato en Borrador (PA-GEN-01) | `generarformato` sobre un arreglo HUMO de plazo 3 | las 26 celdas que no son de pago tienen dato |
| Formato del arreglo Vigente con la cuota 1 pagada (PA-GEN-04) | formalizar, pagar la cuota 1, `generarformato` | **28 de 28 celdas con dato** |

Evidencia: `evidencia/10_plantilla_v07.json` (veredictos de la segunda corrida; la primera, que publicó, dejó `vigentes_antes = ['v95']`), `evidencia/10_F-NIC-0000052-v07-HUMO-borrador.xlsx` y `evidencia/10_F-NIC-0000052-v07-HUMO-vigente.xlsx`.

## GUIDs

| Fila | GUID |
|---|---|
| Versión v07 (Vigente) | `7d5465f9-adcc-f111-a05b-00224808ffcd` |
| Arreglo HUMO `arreglo-l0-v07` (primera corrida, con `cliente-01` de la tanda 1) | `fa2f6257-aecc-f111-a05b-00224808ffcd` |
| Cliente HUMO `cliente-l0-v07` (con correo `@example.com` y celular) | `15a4476b-aecc-f111-a05b-00224808ffcd` |
| Arreglo HUMO `arreglo-l0-v07b` (el de la verificación de 28 de 28) | `0d68be71-aecc-f111-a05b-00224808ffcd` |

Los GUID de las filas HUMO están en `estado-plantilla.json` (separado del `estado.json` del resto del lote 0).

## Hallazgos

1. **Primera corrida, 26 de 28:** B9 (correo) y F9 (celular) salieron vacías porque `cliente-01` de la tanda 1 no tiene correo ni celular. No es defecto: el formato imprime lo que hay en la instantánea del cliente. Se repitió con un cliente HUMO completo y dio 28 de 28. El arreglo de la primera corrida queda Vigente con la cuota 1 pagada (no se borra nada).
2. Las columnas de pago del bloque (D7 y E7) solo tienen dato con una cuota pagada; por eso la segunda generación es sobre el arreglo ya Vigente.
3. En Dev publicó el administrador. En Test y Prod publica el jefe con el mismo gesto, y se compara la huella con la de Test (OA-4). La huella de v07 es la SHA-256 del archivo aprobado.

## Herramienta

`herramientas/construir/datos.py` ahora admite columnas de archivo (`{"archivo": "<ruta relativa al playbook>", "nombre": "<nombre en Dataverse>"}`): el archivo no viaja en el alta, se sube aparte y se verifica bajándolo y comparando la SHA-256; con `--completar` se vuelve a subir si difiere (sobre una Vigente el plugin lo rechaza con CARP-VER-003 y la herramienta informa `error`). Pruebas nuevas en `tests/test_datos_archivo.py` (14); la suite de `herramientas/construir` da 1492 OK.
