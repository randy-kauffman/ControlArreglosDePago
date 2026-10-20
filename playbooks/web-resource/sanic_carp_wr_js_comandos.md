# Playbook: web-resource · sanic_carp_wr_js_comandos

> **Tipo con 0 ciclos** (`power-platform-especificar` no tiene referencia para web resource). El formato sale de la Identidad común y de lo que lee `herramientas/construir/web_resource.py`.

## 1. Identidad

```json
{
  "tipo_playbook": "web-resource",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.22",
  "fase": 1,
  "entorno_url": "https://org36e60d9d.crm.dynamics.com/",
  "solucion": "sanic_carp_sol_controlarreglospago",
  "publisher": "Sistemas_Abiertos_Nicaragua",
  "prefijo": "sanic",
  "abrev": "carp",
  "prefijo_opciones": 15946,
  "idiomas": [
    1033,
    3082
  ]
}
```

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. El recurso es la fila 12.22 de `diseno/06-inventario-componentes.md` §12.4; su diseño completo está en `diseno/07-diseno-app-y-flujos.md` §5.1 a §5.3 y §4 (eventos de formulario).

Advertencias: `web_resource.py` maneja `displayname` y `descripcion` como una sola cadena (no hay etiquetas por idioma); `idiomas` solo cumple el formato común. El `displayname` va sin tildes (BP-PP-197). La ruta del archivo la fija `diseno/07` §5.1 (`webresources/js/comandos.js`, en la raíz del repositorio, no en `src/`).

## 2. Qué se crea

```json
{
  "tipo": "web-resource",
  "nombre": "sanic_carp_wr_js_comandos",
  "displayname": "WR - CARP - JS - Comandos",
  "descripcion": "JavaScript de la app: eventos de formulario (OnLoad y OnSave) de Conversacion, Arreglo, Cuota y Mapeo de celda, y las funciones de los gestos del command bar (lote B). Sin logica de negocio.",
  "recurso": "JS",
  "archivo": "webresources/js/comandos.js"
}
```

Contenido (lotes A y B; el archivo ya existe en el repositorio, la herramienta lo sube y el playbook no lo repite). Un solo archivo, namespace `SanicCarp`, un namespace por tabla (`diseno/07` §5.3 regla 8): `Comun`, `Conversacion`, `Arreglo`, `Cuota`, `Plantilla`. Los manejadores de evento se registran con **pasar el contexto de ejecución como primer parámetro** (`pasar_contexto: true` en el formulario). Los comandos del ribbon los registra `playbooks/importacion/comandos.md`.

**Eventos de formulario (lote A)**

| Función | Formulario | Evento | Qué hace |
|---|---|---|---|
| `SanicCarp.Conversacion.alGuardar` | Conversación (12.18) | OnSave | si `sanic_resultado`, `sanic_motivosinrespuesta` o `sanic_detallesinrespuesta` están *dirty* y la bandera está baja: `preventDefault()` en **todos** los modos (70, 2, 1, 59) y `setFormNotification` INFO "Use el botón Finalizar para aplicar el resultado y el motivo."; si no hay gesto pendiente, limpia el aviso y no toca el guardado |
| `SanicCarp.Arreglo.alCargar` | Arreglo (12.19) | OnLoad | si el arreglo es Terminado (`statuscode` 2) o Vigente congelado (`statuscode` 159460001 y `sanic_fechacongelamiento` con valor) y `esJefe()`, `setDisabled(false)` sobre `sanic_motivoreapertura` (`diseno/07` §4.4 punto 2) |
| `SanicCarp.Arreglo.alGuardar` | Arreglo (12.19) | OnSave | igual que Conversación, sobre `sanic_motivodescarte`, `sanic_motivoanulacion`, `sanic_motivoreapertura` y el aviso "Use el botón Descartar, Anular o Reabrir arreglo para aplicar el motivo." |
| `SanicCarp.Cuota.alGuardar` | Cuota (12.26) | OnSave | si `getSaveMode() === 70`, `preventDefault()` (solo el autoguardado) |
| `SanicCarp.Plantilla.alGuardarMapeo` | Mapeo de celda (12.21) | OnSave | igual que Cuota |

**Gestos del command bar (lote B; `diseno/07` §5.4). Todos corren dentro de `SanicCarp.Comun.conBandera` (try/finally: la bandera siempre baja)**

| # | Gesto | Función del comando | Regla (`EnableRule`) | Qué hace | Manda |
|---|---|---|---|---|---|
| 1 | Tomar | `SanicCarp.Conversacion.tomar` | `puedeTomar` | Custom API `sanic_carp_capi_tomarconversacion`; `CARP-TOM-900`: texto neutro del catálogo + refresco, sin reintento; en grilla exige una sola fila | `conversacionid` |
| 2 | Finalizar | `SanicCarp.Conversacion.finalizar` | `puedeFinalizar` | confirma y `updateRecord` | `statecode: 1`, `statuscode: 2`, `sanic_resultado` y, si tienen valor, motivo y detalle |
| 3 | Reabrir conversación | `SanicCarp.Conversacion.reabrir` | `puedeReabrir` | confirma y `updateRecord` | solo `statecode: 0`, `statuscode: 1` |
| 4 | Unir | `SanicCarp.Conversacion.unir` | `puedeUnir` | `lookupObjects` (abiertas, distinta de la actual, vista "Conversaciones para unir" por nombre), confirma con los dos números, Custom API `unirconversaciones`, abre el destino | `origenid`, `destinoid` |
| 5 | Iniciar conversación nueva | `SanicCarp.Conversacion.iniciarNueva` | `puedeIniciarNueva` | confirma, Custom API `iniciarconversacionnueva`, abre la nueva | `conversacionid` |
| 6 | Generar formato | `SanicCarp.Arreglo.generar` | `puedeGenerar` | exige formulario sin cambios; confirmación según estado; "Generando…"; Custom API `generarformato`; descarga con `openFile` (`fileSize` en KB, `openMode: 2`); muestra `advertencias`; refresca | `arregloid` |
| 7 | Desbloquear | `SanicCarp.Arreglo.desbloquear` | `puedeDesbloquear` | confirma y `updateRecord` | solo `sanic_fechacongelamiento: null` |
| 8 | Formalizar | `SanicCarp.Arreglo.formalizar` | `puedeFormalizar` | confirma y `updateRecord`; el toast relee `sanic_numeroarreglo` tras el refresco | solo `statecode: 0`, `statuscode: 159460001` |
| 9 | Descartar | `SanicCarp.Arreglo.descartar` | `puedeDescartar` | confirma y `updateRecord` | `statecode: 1`, `statuscode: 159460003`, `sanic_motivodescarte` |
| 10 | Anular | `SanicCarp.Arreglo.anular` | `puedeAnular` | confirma y `updateRecord` | `statecode: 1`, `statuscode: 159460002`, `sanic_motivoanulacion` |
| 11 | Reabrir arreglo | `SanicCarp.Arreglo.reabrir` | `puedeReabrir` (+ `esJefe()`) | confirma y `updateRecord` | de un Terminado: `statecode: 0`, `statuscode: 159460001`, `sanic_motivoreapertura`; de un Vigente congelado: solo `sanic_motivoreapertura` |
| 12 | Registrar pago | (sin botón) | — | Guardar nativo del formulario de Cuota | — |
| 13 | Publicar versión | `SanicCarp.Plantilla.publicar` | `puedePublicar` (+ `esJefe()`) | exige formulario sin cambios; confirma y `updateRecord` | solo `sanic_estado: 159460002` |
| 14 | Vista previa | `SanicCarp.Plantilla.vistaPrevia` | `puedeVistaPrevia` (+ `esJefe()`) | confirma, `lookupObjects` de un arreglo, Custom API `generarformato`, descarga; no escribe ni refresca | `arregloid`, `versionpreviaid` |

**Utilidades `SanicCarp.Comun`**: `levantarBandera` / `bajarBandera` / `banderaLevantada` / `conBandera`, `formContext`, `hayGestoPendiente`, `guardarSinGestoSuelto`, `cancelarAutoguardado`, `esJefe` (rol `SR - CARP - Jefe` por nombre en `userSettings.roles`, DA-14); y, del lote B, `contexto(primero, segundo)` (unifica `PrimaryControl` y `SelectedControlSelectedItemIds` + `SelectedControl`), `ejecutarApi` (`Xrm.WebApi.online.execute`, `Edm.Guid`), `escribirGesto`, `textoDeError` / `mostrarError` (regex `/CARP-[A-Z]{3,4}-\d{3}:[\s\S]*/`: del código al final, todas las líneas; si no hay código, texto genérico y el técnico en `details`), `avisar` (toast), `alerta`, `confirmar`, `conProgreso`, `descargarXlsx`, `generarYDescargar`.

Sintaxis: `async`/`await` (ES2017) en el lote B; el lote A conserva su forma.

Decisiones del playbook (el diseño no trae el nombre): `diseno/07` §4.1, §4.6 y §4.7 dicen que Cuota y Mapeo cancelan el autoguardado en `OnSave` pero no nombran la función. Se eligió `SanicCarp.Cuota.alGuardar` (el namespace de la regla 8 solo lista Conversacion, Arreglo y Plantilla; se agrega `Cuota`) y `SanicCarp.Plantilla.alGuardarMapeo` (el mapeo es parte de la versión de plantilla). **Si el usuario prefiere otros nombres, se cambian aquí, en el JS y en los dos playbooks de formulario a la vez.**

**No incluido** en el archivo: el aviso de otra conversación abierta (`alCargar` de Conversación, lote C, `diseno/07` §4.2) y el respaldo de herencia de cliente y oficial en el alta del Arreglo (`diseno/07` §4.4 punto 3, solo si el mapeo de columnas de REL-03 no funciona).

## 3. Precondiciones

| # | Qué tiene que cumplirse | Cómo se comprueba |
|---|---|---|
| 1 | El archivo existe, no está vacío y pesa menos de 5 MB | `webresources/js/comandos.js` en el repositorio (la herramienta devuelve `bloqueado` si falta) |
| 2 | La solución existe y no es managed; los idiomas están provisionados | `comprobar_solucion_e_idioma` de la herramienta |

## 4. Cómo se construye

No hay referencia de `power-platform-construir` para web resource en el índice; la herramienta del proyecto es `herramientas/construir/web_resource.py`.

```
python3 herramientas/construir/web_resource.py playbooks/web-resource/sanic_carp_wr_js_comandos.md
```

Orden de construcción: **antes** que los formularios 12.18, 12.19, 12.21 y 12.26 (`formulario.py` bloquea si la biblioteca no existe o no es JavaScript).

## 5. Verificación

```
python3 herramientas/construir/web_resource.py playbooks/web-resource/sanic_carp_wr_js_comandos.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET webresourceset?$filter=name eq 'sanic_carp_wr_js_comandos'` | una fila · `webresourcetype` = 3 (Script) · `displayname` y `description` los de la sección 2 · `ismanaged` = false |
| 2 | Contenido | el `content` (base64) es el del archivo del repositorio |
| 3 | Pertenencia a la solución | figura como componente de `sanic_carp_sol_controlarreglospago` |
| 4 | Segunda ejecución sin `--solo-verificar` | `ya_existia` |

Fuera de la herramienta (lote A, en Dev con un usuario no administrador): `diseno/07` §12 puntos 7 (`getSaveMode() === 70` cancela el autoguardado), 12 (`setDisabled(false)` en un Terminado) y 17 (campos gesto escribibles y `OnSave` sin romper la navegación; ver la sección 8). La prueba de las funciones con mocks de `Xrm` se hizo fuera del repositorio; el proyecto no tiene un runner de JavaScript (ni `package.json`) y no se instaló ninguno.

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si pasan: `ya_existia`. Si no: `difiere` con la diferencia exacta, sin tocar nada. Con `--completar` la herramienta corrige `displayname`, `description` y `content` con `PATCH` y **publica** (Learn: un web resource se publica al actualizarlo, no al crearlo). El tipo es inmutable.

## 7. Reversa

`DELETE webresourceset(<webresourceid>)`. Imposible mientras un formulario lo referencia como biblioteca: primero se quita de los formularios. Una vez la solución llegó a Test o Prod, el borrado se hace con una versión nueva de la solución. Actualizar el contenido es reversible (se vuelve a subir el archivo anterior de Git).

## 8. Fuera de alcance

- Los botones del ribbon que llaman a estas funciones: `playbooks/importacion/comandos.md` (12.24).
- Los registros de los manejadores en cada formulario: `playbooks/formulario/{conversacion,arreglo,cuota,mapeocelda}.md`.
- Los iconos: `playbooks/web-resource/sanic_carp_wr_svg_*.md`.
- **Por qué no `setSubmitMode("never")`** (Learn, `setSubmitMode`: "When this value is used, the column(s) in the form for this column cannot be edited"): los campos gesto quedan en `dirty` y el `OnSave` cancela el guardado mientras haya uno sin aplicar (mecanismo de `diseno/07` §4.1). **A verificar en Dev** (`diseno/07` §12 punto 17): que `preventDefault()` en el modo 2 (al navegar) y en "Guardar y nuevo" no deje al usuario atrapado; si pasa, el aviso lo orienta a usar el botón.
- Costo conocido: con un gesto sin aplicar no se guarda ningún otro cambio del formulario.
- **Lote B**: cada gesto que escribe corre dentro de `SanicCarp.Comun.conBandera` (levanta la bandera, `finally` la baja). Los gestos escriben con `Xrm.WebApi.updateRecord` (no con `data.save`), así que el `OnSave` no se dispara por el gesto; la bandera protege el refresco y cualquier guardado que la plataforma encadene.
- Un gesto en el formulario refresca con `data.refresh(false)`: descarta los cambios sin guardar de otros campos (solo Generar y Publicar los exigen guardados antes).
- Al navegar fuera del formulario con datos sin guardar, la plataforma guarda con modo 2 (Save and Close), que en **Cuota** y **Mapeo** el diseño no cancela: una fecha de pago sin monto daría `CARP-CUO-003` al salir. Es feedback del servidor, no se corrige en el cliente.
