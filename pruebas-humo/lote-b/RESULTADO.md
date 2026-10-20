# Lote B de la app: gestos en Dev (2026-10-20)

URL de la app: https://org36e60d9d.crm.dynamics.com/main.aspx?appid=f13895ff-9bcc-f111-a05b-00224808ffcd

## Construcción

| Paso | Ciclo | Resultado |
|---|---|---|
| Web resource `sanic_carp_wr_js_comandos` (14 gestos) | `--solo-verificar` dio `difiere` (descripción y contenido); `--completar` dio `actualizado` (publicó); `--solo-verificar` dio `ya_existia` | JS de 35193 bytes publicado. |
| Ribbon, `playbooks/importacion/comandos.md` | `--solo-verificar` dio `difiere` (faltaban los 13 botones); sin bandera dio `creado`; `--solo-verificar` dio `ya_existia` | 3 ribbon con 13 botones propios. El import apagó 37 steps y la herramienta los volvió a encender todos. |
| Lectura del `RibbonDiffXml` exportado | `ExportSolution` de solo lectura | OK, ver abajo. |

### Ribbon exportado (las 3 tablas)

14 elementos `Button` = 13 botones (Tomar va en Form y en la grilla). Cada uno tiene su `CommandDefinition` con `JavaScriptFunction` de la biblioteca `sanic_carp_wr_js_comandos` y su `EnableRule` con `CustomRule`.

| Tabla | Botones | Contexto |
|---|---|---|
| Conversación | Tomar (Form y HomepageGrid), Finalizar, Unir, Reabrir, Iniciar nueva | Form: `PrimaryControl` en comando y regla. Tomar en grilla: comando con `SelectedControlSelectedItemIds` + `SelectedControl`, regla con `SelectedControl` y además `Mscrm.SelectionCountAtLeastOne` |
| Arreglo | Generar, Desbloquear, Formalizar, Descartar, Anular, Reabrir | Form, `PrimaryControl` en comando y regla |
| Versión de plantilla | Publicar, Vista previa | Form, `PrimaryControl` en comando y regla |

Las 26 funciones distintas referenciadas (13 gestos y 13 reglas) existen como función en el JS publicado (descargado de `webresourceset` y evaluado con `vm` de node). Los iconos son los `$webresource:` de los SVG de acción y alerta.

## Prueba funcional por Web API (admin, datos HUMO)

Payload exacto del JS, respuesta del servidor. Evidencia: `evidencia/10_gestos.json` (veredictos), `evidencia/10_gestos_log.json` (payload y respuesta de cada llamada) y 8 `.xlsx` generados. 28 de 30 comprobaciones en OK.

| Gesto | Payload (JS) | Respuesta |
|---|---|---|
| 1 Tomar | POST `sanic_carp_capi_tomarconversacion` `{conversacionid}` | 200 `oficialasignadoid`=yo, `yaasignada` false. Repetido: `yaasignada` true |
| 2 Finalizar | PATCH `{statecode:1, statuscode:2, sanic_resultado:159460003, sanic_motivosinrespuesta:159460002, sanic_detallesinrespuesta}` | 204, Finalizada, finalizadopor=yo, con fecha |
| 2 Finalizar (solo resultado) | PATCH `{statecode:1, statuscode:2, sanic_resultado:159460001}` | 204 |
| 2 Finalizar sin resultado | PATCH `{..., sanic_resultado:null}` | 400 CARP-CNV-003 |
| 3 Reabrir conversación | PATCH `{statecode:0, statuscode:1}` | 204, Abierta, resultado, fecha y finalizadopor nulos |
| 4 Unir | POST `sanic_carp_capi_unirconversaciones` `{origenid, destinoid}` | 200 `correosmovidos` 0, `arreglosrepunteados` 0; origen Unida con `unidaaid`=destino |
| 5 Iniciar nueva | POST `sanic_carp_capi_iniciarconversacionnueva` `{conversacionid}` | **Camino feliz NO probado.** 400 CARP-INI-003 (Finalizada sin correos nuevos). El indicador `sanic_correonuevoenfinalizada` solo lo prende el plugin al llegar un correo (sembrarlo da 400 CARP-CNV-001) y no se crean correos |
| 6 Generar | POST `sanic_carp_capi_generarformato` `{arregloid}` | 200, `.xlsx` válido (zip, 32 KB), `congelado` true, `numeroprovisional` 1. Probado en Borrador, tras desbloquear, Vigente congelado y Anulado. Tras publicar v95 también |
| 7 Desbloquear | PATCH `{sanic_fechacongelamiento:null}` | 204, instantáneas y provisional nulos. Repetido: 400 CARP-ARR-017 |
| 8 Formalizar | PATCH `{statecode:0, statuscode:159460001}` | 204, Vigente, `numeroarreglo` 1, sigue congelado |
| 9 Descartar | PATCH `{statecode:1, statuscode:159460003, sanic_motivodescarte}` | 204 con motivo. Motivo null: 400 CARP-ARR-013 |
| 10 Anular | PATCH `{statecode:1, statuscode:159460002, sanic_motivoanulacion}` | 204 con motivo. Motivo null: 400 CARP-ARR-014 |
| 11 Reabrir arreglo | PATCH `{sanic_motivoreapertura}` (Vigente congelado) y `{sanic_motivoreapertura, statecode:0, statuscode:159460001}` (Terminado) | **400 CARP-ARR-016** con el administrador (solo jefatura). Ver "Pendiente" |
| 12 Registrar pago | PATCH de la cuota `{sanic_fechapago, sanic_montopagado}` | 204 en las dos cuotas; al pagar la última el arreglo pasó a Terminado |
| 13 Publicar | PATCH `{sanic_estado:159460002}` sobre v95 | 204, v95 Vigente con huella y aprobador, v91 pasó a Histórica |
| 14 Vista previa | POST generarformato `{arregloid, versionpreviaid}` con v95 Borrador | 200, `.xlsx`, el arreglo no cambió |

v95: Borrador HUMO nueva con archivo y 28 mapeos, publicada (queda Vigente; v91 queda Histórica).

### Pendiente

- **Reabrir arreglo (gesto 11)**: con el administrador da CARP-ARR-016, igual que en las tandas 2 y 2b. No se repitió con SPIKE4 en el team Jefatura porque eso habilita un usuario y cambia membresías, y no estaba autorizado en este encargo. Los mismos dos payloads ya pasaron con jefatura en `../tanda2b/RESULTADO.md` (204 en ambos), así que el contrato está cubierto por esa evidencia.
- **Iniciar nueva (gesto 5)**: el camino feliz necesita un correo real posterior a la finalización.
- El contenido interno de los `.xlsx` no se reinspeccionó (no hay `openpyxl`); solo que son zips OOXML válidos con dos hojas.

### Filas HUMO

Registradas en `estado.json`: conversaciones `lb-conv-*` (la primera pasada, interrumpida por un campo largo, dejó 6 y la segunda otras 6), arreglos `lb-arr-1` a `lb-arr-4` (1 Vigente descongelable, 2 Descartado, 3 Anulado, 4 Terminado) con sus 2 cuotas, v95 con 28 mapeos. Modificada de otra tanda: `version-v91` (Vigente a Histórica).

## Defectos y gotchas

1. `importar_solucion.py` apaga 37 steps en el import y los vuelve a encender (lo hace sola y lo informa).
2. Cambiar la `description` del playbook del web resource lo deja `difiere` hasta `--completar`.
3. `sanic_cuentadebitar` admite 20 caracteres (mi primer dato de humo era más largo).
4. No se pudo sembrar el indicador de correo nuevo (CARP-CNV-001).
5. El administrador no es jefatura (CARP-ARR-016).

## Prueba manual en el navegador

Antes de empezar: abrir la app con `&ribbondebug=true` en la URL, Command Checker, "Regenerate ribbon metadata" (rol System Administrator o System Customizer), y recargar con Ctrl+F5. Sin eso los botones nuevos pueden no aparecer. Usar un Oficial y un Jefe, y solo datos HUMO.

1. **Tomar** (Conversación, formulario y grilla): en una conversación abierta sin oficial, el botón aparece en el formulario y en la grilla con una fila seleccionada; Tomar asigna al usuario y avisa. En una ya tomada, el botón no aparece en el formulario.
2. **Aparición sin `SelectionCountAtLeastOne`** (§12 punto 4): en la grilla con ninguna fila seleccionada Tomar no se ve; al seleccionar una aparece. En el formulario aparece sin selección y `PrimaryControl` llega bien.
3. **Finalizar**: elegir resultado, motivo y detalle en el formulario y pulsar Finalizar; confirma, finaliza y refresca. Sin resultado, aparece el error CARP-CNV-003.
4. **Reabrir** (conversación finalizada): vuelve a Abierta y limpia el resultado.
5. **Unir** (§12 punto 9): abre el selector con la vista "Conversaciones para unir" y `filters` (solo abiertas y no la actual); confirmar; el formulario navega a la destino sin cancelar el guardado por el guardia de OnSave.
6. **Iniciar conversación nueva**: en una finalizada con correo nuevo posterior, crea la nueva y navega a ella.
7. **Generar formato** (§12 punto 8): con cambios sin guardar avisa que se guarde; guardado, confirma, muestra "Generando…" y descarga el `.xlsx` con `openFile` (`openMode: 2`, tamaño en KB). El `.xlsx` abre en Excel. En un Borrador congela.
8. **Desbloquear / Formalizar** (Borrador congelado): Desbloquear vacía el congelamiento; Formalizar pasa a Vigente con número y avisa "N.º".
9. **Descartar / Anular**: con el motivo vacío, error CARP-ARR-013 o 014; con motivo, confirma y cambia el estado. El campo de motivo solo cambia con el botón (guardia de OnSave).
10. **Reabrir arreglo** (Jefe, §12 punto 12): en un Terminado el motivo es editable; el botón reabre. Para un Oficial el botón no aparece.
11. **Publicar versión** (Jefe, Versión en Borrador): confirma, pasa a Vigente, la anterior a Histórica. **Vista previa**: selector de arreglo (cualquier estado), descarga el `.xlsx`, no cambia nada.
12. **Guardia de OnSave al navegar** (§12 punto 17): escribir un motivo sin pulsar el botón, intentar navegar o "Guardar y nuevo": avisa y no rompe la navegación. Durante un gesto, el guardado no se cancela.
13. Consola (F12): sin errores de `sanic_carp_wr_js_comandos`; tooltips en español con el usuario en 3082.
