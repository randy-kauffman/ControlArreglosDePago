# Lote A de la app: resultado de la construcción en Dev (2026-10-20)

## Estado de los componentes

| Componente | Estado | Nota |
|---|---|---|
| `sanic_carp_wr_js_comandos` y 4 SVG (accion, alerta, app, lista) | `ya_existia` (verificado) | Hecho. |
| Formulario Mapeo de celda, Cuota, Version de plantilla, Arreglo, Conversacion, Cliente | `ya_existia` (verificado con `--solo-verificar`) | Las etiquetas 3082 están guardadas: `GET systemforms` devuelve solo las del idioma del usuario (1033). `formulario.py` las verifica contra la solución exportada. |
| Sitemap `sanic_carp_sm_controlarreglospago` | `ya_existia`, 13 entradas, `isappaware` true | Coincide con el generado. |
| App `sanic_carp_mda_controlarreglospago` | `ya_existia` (verificado con `--solo-verificar`) | Las 8 tablas están en la app (agregadas por `ImportSolution`); `ValidateApp` sin errores. |

Defectos de las herramientas (corregidos el 2026-10-20):

1. `formulario.py`: la verificación exigía la etiqueta 3082 dentro del `formxml` de `systemforms`, que solo trae el idioma del usuario. Ahora lee los idiomas que la lectura no devuelve de la solución exportada, compara etiqueta por etiqueta (ignora las vacías de las celdas de relleno) y el alta también publica.
2. `app.py`: `AddAppComponents` con `Microsoft.Dynamics.CRM.entity` ignora el GUID y agrega siempre la tabla de metadatos `entity`. Las tablas entran ahora por `ImportSolution` (`importar_solucion.importar_tablas_de_la_app`). La verificación con `RetrieveAppComponents` se mantiene.

## Prueba manual en el navegador

URL de la app: https://org36e60d9d.crm.dynamics.com/main.aspx?appid=f13895ff-9bcc-f111-a05b-00224808ffcd

Las 8 tablas ya están en la app y está publicada: no hace falta ningún paso manual en el diseñador.

1. Abrir la app "MDA - CARP - Control de Arreglos de Pago" con un usuario Oficial: debe cargar sin error y mostrar el icono de la app.
2. Navegar las 4 áreas y abrir cada una de las 13 entradas: cada vista carga y no da error. Con el Oficial, no deben verse Supervisión ni Administración del formato (§12 punto 6); con el Jefe, sí.
3. Conversaciones: abrir una; el timeline de correos se ve (§12 punto 3) y la subgrilla "Arreglos del cliente" lista solo los de esa conversación.
4. Abrir el arreglo HUMO en Borrador. Comprobar las secciones, los campos de solo lectura y las subgrillas de cuotas y formatos.
5. En el arreglo HUMO, escribir un texto en el campo de motivo (descarte, anulación o reapertura) sin usar un botón y guardar: debe aparecer el aviso del guardia de OnSave y el guardado no debe romper la navegación ni "Guardar y nuevo" (§12 punto 17).
6. Autoguardado: dejar un campo modificado y salir del registro; el guardia de OnSave debe cancelar el autoguardado (§12 punto 7).
7. Abrir una cuota desde la subgrilla del arreglo y registrar un pago: fecha de pago y monto pagado juntos; guardar. Probar guardar solo uno de los dos: debe avisar. Las demás columnas son de solo lectura.
8. En un arreglo Terminado, intentar escribir en el motivo de reapertura (§12 punto 12).
9. Versión de plantilla: abrir una; el estado es de solo lectura y la subgrilla de mapeo carga con sus filas (§12 punto 15). Abrir un Mapeo de celda y guardar.
10. Cliente: abrir uno; ver sus arreglos y conversaciones; buscar por búsqueda rápida (§12 punto 10).
11. Consola del navegador (F12): sin errores de carga de `sanic_carp_wr_js_comandos`.
