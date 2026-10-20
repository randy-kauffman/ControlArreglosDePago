# 07. Diseño detallado de la app model-driven (grupo 12) y de los flujos de monitoreo (grupo 11)

Proyecto: 2026-003-control-arreglos-pago · Etapa 3 · Estado: **borrador del arquitecto (2026-10-20)**. Lo que las fuentes no cierran va como decisión abierta `DA-nn` (sección 10), con recomendación y tradeoff, **marcada para confirmar**. El usuario delegó: "procede con lo que recomendés". Hasta que se confirme, cada DA se construye con la recomendación.

Fuentes: `definicion.md` (§6 Componentes, Datos, Lógica, Seguridad, Operación; D-01, D-11, D-12, D-17, D-21, D-27, D-28, D-35, D-37 a D-41), `mockup/index.html` v3 (referencia funcional), `diseno/01` a `06`, el código de `src/Sanic.Carp.Plugins` (vía `03-contratos-custom-api.md`, que lo documenta fila por fila) y las herramientas de `herramientas/construir/` (`vista.py`, `formulario.py`, `app.py`, `web_resource.py`, `flujo.py`, `importar_solucion.py`), cuyos formatos de playbook consumen los datos de este documento.

Nada de este documento se verificó contra Dev (no hubo conexión). Lo que se apoya en una suposición de plataforma lleva la marca **[verificar en Dev]**.

## 0. Datos de la tanda 1 de pruebas de humo (2026-10-20) que este diseño incorpora

| # | Hallazgo | Dónde se aplica |
|---|---|---|
| 1 | Reenviar `sanic_fechacongelamiento = null` sobre un Borrador **no** congelado da `CARP-ARR-017` (se lee como Desbloquear). | Regla general de la sección 5: el web resource y los formularios mandan **solo las columnas que cambiaron**, nunca la fila completa. Desbloquear manda `null` solo si el borrador está congelado (regla de visibilidad). |
| 2 | El mapeo v07 tiene **28** filas, no 27 (`clienteNombre` y `fechaFormalizacion` aparecen dos veces). | 9.2 y la subgrilla "Mapeo de la version" (12.13): la subgrilla trae `registros_por_pagina` (G-F6) en **28**, y la de cuotas en **60**, para que se vea todo sin paginar (o el menor valor que admita la plataforma y sea mayor o igual: **[verificar en Dev]**). La vista en sí no pagina. |
| 3 | Dev redondea `AwayFromZero` con dos decimales. | Los montos se muestran con 2 decimales tal cual los guarda el servidor; ningún cálculo en el cliente. |
| 4 | Perder la carrera de "Tomar" da `CARP-TOM-900` (`0x80060882`). | Sección 5: se muestra el texto neutro del catálogo y se refresca, sin reintento automático (DA-07). |
| 5 | Un Create de Arreglo con `statecode = 1` nace Borrador igual. | Los formularios y el web resource **nunca mandan `statecode`/`statuscode` en un alta**. Solo los gestos de la sección 5 mandan estado. |

## 1. Resumen de decisiones de mecanismo

| Tema | Decisión | Por qué (una línea) |
|---|---|---|
| Botones de gesto | **Ribbon clásico con JavaScript** (`importar_solucion.py`, cambios `ribbon`) | Es lo que D-17 decidió y lo único con herramienta. Power Fx commanding no tiene herramienta, no captura entradas y no entrega archivos (sección 5.2). |
| Captura del motivo / resultado | **Campos del propio formulario + botón** (M1). El botón lee los campos y manda un solo `Update` con solo esas columnas | Los diálogos HTML no devuelven valores (`navigateTo` solo devuelve valor en páginas generativas y altas de registro, Learn) y `Xrm.*` no existe en un HTML web resource de diálogo. Cero componentes extra. |
| Autoguardado | El formulario de Conversación y el de Arreglo **cancelan el autoguardado** (`OnSave` con `getSaveMode() = 70`) | Un motivo escrito y guardado solo da `CARP-ARR-024`/`CARP-CNV-005` a los 30 s. |
| Aviso de "otra conversación abierta" | `OnLoad` del formulario + `setFormNotification` con una consulta de hasta 3 filas | Es lo más simple que funciona (sección 4.2). |
| Elegir la conversación o el arreglo en Unir y Vista previa | `Xrm.Utility.lookupObjects` con filtro | Sin HTML propio; el selector es el nativo. |
| Descarga del `.xlsx` | `Xrm.Navigation.openFile` con `openMode: 2`; `fileSize` va en **KB** | Learn, `openFile`. |
| "Quién ve cada cosa" | Las vistas no tienen seguridad: **la ve quien tiene la entrada del sitemap**. Entradas del jefe: `Privilege Create` sobre Versión de plantilla. Botones del jefe: regla JS por nombre de rol | Solo es UX; la barrera real son los plugins y los privilegios (cliente = feedback, §6 Lógica). |
| Dashboard y gráficos | **Diferidos** a después de la primera versión utilizable, construcción manual guiada | No hay herramienta (sección 6). |
| Flujos | 11.1 y 11.2 **después** de la primera versión utilizable y antes del pase a Test; 11.3 no se construye | La primera versión es de operación, no de monitoreo (sección 9). |

## 2. Brechas de las herramientas (lo que hay que cambiar para poder escribir los playbooks)

Los playbooks de vista, formulario, app, web resource, importación y flujo **no tienen referencia en `power-platform-especificar`** (el índice solo cubre 9 tipos). El formato de cada uno es el docstring de su herramienta. Cada tipo sería "0 ciclos". Las brechas siguientes son las que impiden expresar este diseño con las herramientas actuales:

| ID | Herramienta | Brecha | Qué pide este diseño | Cuándo |
|---|---|---|---|---|
| G-V1 | `vista.py` | El filtro no emite `isquickfindfields="1"` | Búsqueda rápida de Cliente y de Arreglo (12.10, 12.11): `filtro` con `tipo: or`, condiciones `like` con valor `{0}` y bandera `buscar: true` | Lote A |
| G-V2 | `vista.py` | `link-type` fijo en `inner`; sin `any`/`not any`; sin filtros `or` anidados; operadores solo hasta `like`, `begins-with`, `in`, `not-in` | 12.7 (riesgo) necesita `link-entity link-type="any"` con un `filter or` anidado y los operadores `olderthan-x-days` y `yesterday`; 12.6 necesita `olderthan-x-days`. Agregar `olderthan-x-days`, `yesterday`, `today`, `on-or-before` a `OPERADORES_*`, `tipo` por enlace y `filtros` anidados | Lote C |
| G-V3 | `vista.py` | `_otras_por_defecto` solo conoce las vistas `Publica` | La vista `Lookup` de unir (12.13) no es "por defecto": no hay problema. Solo cuidar que no se declare `pordefecto` | — |
| G-F1 | `formulario.py` | `solo_lectura` es un booleano de todo el formulario | Casi todos los campos de Conversación y Arreglo son solo-sistema: `campos` debe aceptar `"col"` o `{"nombre": "col", "solo_lectura": true}` | Lote A |
| G-F2 | `formulario.py` | Sin control Timeline | Formulario de Conversación (12.18): sección tipo `timeline`. El `classid` y los parámetros se **leen de un formulario real de Dev** (por ejemplo el Main de `contact` con `GET systemforms`), como se hizo con los demás `classid`; no se escriben de memoria | Lote A |
| G-F3 | `formulario.py` | Sin bibliotecas ni eventos | `bibliotecas: [nombre del web resource]` y `eventos: [{evento: onload\|onsave\|onchange, funcion, columna?}]` para `OnLoad` (campos "gesto" en modo `never`, habilitar el motivo de reapertura, aviso) y `OnSave` (autoguardado) en 12.18, 12.19, 12.21 y el de Cuota | **Lote A** (sube desde el B) |
| G-F4 | `formulario.py` | Una sola pestaña | `pestanas: [{titulo, secciones}]` para el Arreglo (General, Cuotas, Formatos, Cierre). **Opcional**: con una sola pestaña y secciones el formulario es utilizable | Lote C |
| G-F5 | `formulario.py` | Sin control para columnas `DecimalType` (`CONTROL_POR_TIPO` no lo trae) | Los montos del Arreglo y de la Cuota son `decimal(2)`; sin el control no se pueden poner en el formulario. Otro agente la construye | **Lote A** |
| G-F6 | `formulario.py` | La subgrilla fija `RecordsPerPage = 10` | Clave nueva `registros_por_pagina` en la subgrilla: cuotas **60**, mapeo **28** (o el menor valor admitido por la plataforma, mayor o igual) | **Lote A** |
| G-A1 | `app.py` | Sin `<Privilege>` por entrada del sitemap; sin entradas de dashboard (`Url`) | Entradas del jefe ocultas al oficial; entrada del dashboard 12.14 | Lote C (privilegio), lote E (dashboard) |
| G-I1 | `importar_solucion.py` | (a) `app_componentes` y `ribbon` exigen el prefijo `sanic_` en la tabla; (b) los botones reciben `SelectedControlSelectedItemIds` y `SelectedControl`, no `PrimaryControl` | (a) La tabla estándar `email` tiene que entrar a la app (timeline, vista 12.4). (b) En el formulario, `SelectedControl` es el contexto de **grilla**; el de formulario llega con `PrimaryControl` (Learn, *Pass data from a page as a parameter to ribbon actions*). Para `ubicaciones` que incluyan `Form`, el botón debe pasar `PrimaryControl`; para `HomepageGrid`, `SelectedControlSelectedItemIds`. **La `EnableRule` (`CustomRule`) también pasa `PrimaryControl`** en `Form`: hoy emite `SelectedControl` | Lote B |
| G-I2 | `importar_solucion.py` | Todo botón no-menú lleva la regla `Mscrm.SelectionCountAtLeastOne` también en `Form` | **[verificar en Dev]** que en el formulario el botón aparece; si no, no emitir esa regla para `Form` | Lote B |
| G-I3 | `importar_solucion.py` | `ocultar` genera `HideCustomAction`, que no se puede quitar después (Learn, *Define custom actions to modify the ribbon*) | Ocultar con un **`DisplayRule` que siempre dé falso** (`Mscrm.HideOnModern` + `Mscrm.ShowOnlyOnModern`) sobre el `CommandDefinition` del botón: es reversible. La herramienta debe emitir eso en vez de `HideCustomAction` (sección 5.5). Los comandos de plataforma son globales: se ocultan por el **Id del botón**, como ya hace la herramienta | Lote C |

Los iconos son web resources SVG obligatorios para cada entrada del sitemap y cada botón (`app.py` y `importar_solucion.py` los exigen). Para no hacer 25 playbooks de SVG: **cuatro SVG** compartidos (DA-10): `sanic_carp_wr_svg_app`, `sanic_carp_wr_svg_lista`, `sanic_carp_wr_svg_accion`, `sanic_carp_wr_svg_alerta`.

## 3. Vistas (12.1 a 12.13)

### 3.1 Reglas comunes

- Un playbook de vista = una vista (`vista.py`). Bloque del componente: `tipo: vista`, `nombre`, `tabla`, `clase` (`Publica`, `BusquedaRapida`, `Lookup`), `descripcion`, `pordefecto`, `columnas` (`nombre`, `ancho`, `enlace?`), `orden`, `filtro` (`tipo`, `condiciones`, `enlaces`).
- Nombre sin tildes (BP-PP-197), sin prefijo. La columna primaria va siempre al fetch; **debe ir primera en `columnas`** porque la primera columna es el vínculo que abre el registro. El `orden` solo puede nombrar una columna visible.
- Valores de choice y estado en el filtro: el valor numérico (`159460001`…). Booleanos: `1`/`0`. Los operadores usados son los de `vista.py` salvo los marcados G-V2.
- **Una sola vista por defecto por tabla** (la herramienta desmarca las demás). Por defecto: Conversación "Mis conversaciones pendientes", Arreglo "Arreglos vigentes", Cliente "Clientes", Versión de plantilla "Versiones de plantilla". Las demás tablas no declaran.
- **Quién la ve.** La vista pública la puede abrir cualquiera con Read sobre la tabla (los dos roles); lo que decide quién la *encuentra* es el sitemap (sección 7). La columna "Perfil" de abajo es esa intención.
- `sanic_antiguedad` (horas, fórmula) se muestra, pero **no se ordena por ella**: se ordena por `sanic_sinrespuestadesde`.
- Las columnas con seguridad por columna (`sanic_cedula`, `sanic_cuentadebitar`, `sanic_instantaneacedula`) se muestran como bloqueadas a quien no tiene el perfil; `sanic_instantaneacedula` **nunca** va a una vista (D-19). `sanic_cuentadebitar` tampoco (no se necesita en listas).
- Anchos en píxeles (rango 25 a 800 de la herramienta). Orden de las columnas = el de las tablas.
- Ordenar ascendente pone los **nulos primero** en Dataverse: por eso las vistas de "pendientes" filtran `not-null` antes de ordenar por `sanic_sinrespuestadesde`.

### 3.2 Vistas de Conversación (`sanic_carp_tbl_conversacion`)

| Inv. | Nombre | Clase | Perfil | Filtro | Orden |
|---|---|---|---|---|---|
| 12.1 | **Correos sin respuesta** | Publica | Jefe (Supervisión) | `sanic_sinrespuestadesde` `not-null` | `sanic_sinrespuestadesde` asc |
| 12.2 | **Correos llegados a conversaciones finalizadas** | Publica | Oficial y Jefe | `sanic_correonuevoenfinalizada` `eq` `1` | `sanic_sinrespuestadesde` asc |
| 12.3 | **Finalizadas sin respuesta** | Publica | Jefe | `sanic_resultado` `eq` `159460003` | `sanic_fechafinalizacion` desc |
| 12.13 | **Mis conversaciones pendientes** (por defecto) | Publica | Oficial | `statecode` `eq` `0`; `sanic_oficialasignadoid` `eq-userid`; `sanic_sinrespuestadesde` `not-null` | `sanic_sinrespuestadesde` asc |
| 12.13 | **Conversaciones sin asignar** | Publica | Oficial y Jefe | `statecode` `eq` `0`; `sanic_oficialasignadoid` `null` | `sanic_fechaprimercorreo` asc |
| 12.13 | **Conversaciones abiertas** | Publica | Oficial y Jefe | `statecode` `eq` `0` | `sanic_fechaprimercorreo` desc |
| 12.13 | **Conversaciones finalizadas** | Publica | Oficial y Jefe | `statecode` `eq` `1`; `statuscode` `eq` `2` | `sanic_fechafinalizacion` desc |
| 12.13 | **Conversaciones del cliente** (subgrid de 12.17) | Publica | — (subgrid) | ninguno (la relación `sanic_clienteid` filtra) | `sanic_fechaprimercorreo` desc |
| 12.13 | **Conversaciones para unir** (selector de Unir) | Lookup | — (selector) | `statecode` `eq` `0` | `sanic_fechaprimercorreo` desc |

Columnas (nombre lógico, ancho). El primer nombre es la primaria `sanic_nombre` ("Numero").

| Vista | Columnas en orden |
|---|---|
| Correos sin respuesta | `sanic_nombre` 110 · `sanic_asunto` 280 · `sanic_remitente` 200 · `sanic_clienteid` 180 · `sanic_oficialasignadoid` 160 · `statuscode` 110 · `sanic_sinrespuestadesde` 150 · `sanic_antiguedad` 100 · `sanic_correonuevoenfinalizada` 90 |
| Correos llegados a conversaciones finalizadas | `sanic_nombre` 110 · `sanic_asunto` 280 · `sanic_remitente` 200 · `sanic_clienteid` 180 · `sanic_oficialasignadoid` 160 · `sanic_resultado` 150 · `sanic_fechafinalizacion` 150 · `sanic_sinrespuestadesde` 150 · `sanic_antiguedad` 100 |
| Finalizadas sin respuesta | `sanic_nombre` 110 · `sanic_asunto` 260 · `sanic_remitente` 200 · `sanic_motivosinrespuesta` 190 · `sanic_detallesinrespuesta` 300 · `sanic_finalizadoporid` 160 · `sanic_fechafinalizacion` 150 |
| Mis conversaciones pendientes | `sanic_nombre` 110 · `sanic_asunto` 280 · `sanic_remitente` 200 · `sanic_clienteid` 180 · `sanic_sinrespuestadesde` 150 · `sanic_antiguedad` 100 · `sanic_correonuevoenfinalizada` 90 |
| Conversaciones sin asignar | `sanic_nombre` 110 · `sanic_asunto` 280 · `sanic_remitente` 200 · `sanic_clienteid` 180 · `sanic_fechaprimercorreo` 150 · `sanic_sinrespuestadesde` 150 · `sanic_antiguedad` 100 |
| Conversaciones abiertas | `sanic_nombre` 110 · `sanic_asunto` 280 · `sanic_remitente` 200 · `sanic_clienteid` 180 · `sanic_oficialasignadoid` 160 · `sanic_fechaprimercorreo` 150 · `sanic_sinrespuestadesde` 150 · `sanic_cantidadrespuestas` 90 |
| Conversaciones finalizadas | `sanic_nombre` 110 · `sanic_asunto` 260 · `sanic_clienteid` 180 · `sanic_oficialasignadoid` 160 · `sanic_resultado` 150 · `sanic_fechafinalizacion` 150 · `sanic_tiempoprimerarespuesta` 120 · `sanic_tiempototal` 110 |
| Conversaciones del cliente | `sanic_nombre` 110 · `sanic_asunto` 260 · `statuscode` 110 · `sanic_oficialasignadoid` 160 · `sanic_fechaprimercorreo` 150 · `sanic_resultado` 150 |
| Conversaciones para unir | `sanic_nombre` 110 · `sanic_asunto` 260 · `sanic_remitente` 200 · `sanic_clienteid` 180 · `sanic_fechaprimercorreo` 150 |

Notas: `sanic_tiempoprimerarespuesta` y `sanic_tiempototal` están en **minutos** (display "(minutos)"); el promedio por oficial **no** sale de vistas (D-27), sale de gráficos. "Esperando al cliente" del mockup no tiene vista propia: es "Conversaciones abiertas" ordenada.

### 3.3 Vista de Email

| Inv. | Nombre | Clase | Perfil | Filtro (`and`) | Orden |
|---|---|---|---|---|---|
| 12.4 | **Respuestas con error de envio** | Publica | Jefe (Supervisión) | `directioncode` `eq` `1`; `statuscode` `eq` `8`; `regardingobjecttypecode` `eq` `sanic_carp_tbl_conversacion` **[verificar en Dev]** | `createdon` desc |

| 12.13 | **Respuestas enviadas** | Publica | Jefe (Supervisión) | `directioncode` `eq` `1`; `sanic_tiemporespuesta` `not-null` | `senton` desc |

Columnas de "Respuestas enviadas": `subject` 300 (primaria) · `regardingobjectid` 140 · `sanic_respondidoporid` 180 · `sanic_tiemporespuesta` 120 (minutos) · `senton` 150. Cubre RF-03 (qué se respondió, quién y cuándo) y el criterio de éxito 2 de la definición; no agrega promedios (D-27).

Columnas de "Respuestas con error de envio": `subject` 300 (primaria) · `regardingobjectid` 140 · `torecipients` 220 · `createdby` 160 · `createdon` 150 · `statuscode` 110. `statuscode` 8 = Failed (`03` §0.7, §2.9). Si `regardingobjecttypecode` no acepta el literal en un filtro de vista, la alternativa es `enlaces` hacia `sanic_carp_tbl_conversacion` por `regardingobjectid` → `sanic_carp_tbl_conversacionid` (un salto). La tabla `email` debe estar en la app (G-I1a).

### 3.4 Vistas de Arreglo (`sanic_carp_tbl_arreglo`)

| Inv. | Nombre | Clase | Perfil | Filtro | Orden |
|---|---|---|---|---|---|
| 12.5 | **Borradores** | Publica | Oficial y Jefe | `statecode` `eq` `0`; `statuscode` `eq` `1` | `modifiedon` desc |
| 12.6 | **Borradores sin movimiento** | Publica | Jefe | los de Borradores + `modifiedon` `olderthan-x-days` `30` (G-V2) | `modifiedon` asc |
| 12.7 | **Arreglos en riesgo** | Publica | Oficial y Jefe | `statecode` `eq` `0`; `statuscode` `eq` `159460001`; **enlace `any`** a cuota (abajo) | `sanic_fechainicio` asc |
| 12.8 | **Arreglos de prestamo** | Publica | Oficial y Jefe | `sanic_producto` `eq` `159460001` | `modifiedon` desc |
| 12.9 | **Arreglos de tarjeta** | Publica | Oficial y Jefe | `sanic_producto` `eq` `159460002` | `modifiedon` desc |
| 12.11 | **Busqueda rapida de Arreglo** | BusquedaRapida | Oficial y Jefe | `or` de `sanic_numeroproducto` `like` `{0}` (G-V1) | — |
| 12.12 | **Arreglos del cliente** (subgrid de 12.17 y de 12.18) | Publica | — (subgrid) | ninguno (todos los estados) | `createdon` desc |
| 12.13 | **Arreglos vigentes** (por defecto) | Publica | Oficial y Jefe | `statecode` `eq` `0`; `statuscode` `eq` `159460001` | `modifiedon` desc |
| 12.13 | **Arreglos cerrados** | Publica | Oficial y Jefe | `statecode` `eq` `1` (Terminado, Anulado, Descartado) | `sanic_fechatermino` desc |
| 12.13 | **Todos los arreglos** | Publica | Oficial y Jefe | ninguno | `modifiedon` desc |

Filtro de 12.7 como lo probó el spike 2 (`spikes/plataforma/RESULTADO.md`; D-11): ningún operador da "fecha anterior a hoy" por sí solo sobre una columna `DateOnly`; la condición es `olderthan-x-days 1` **o** `yesterday`:

```xml
<filter type="and">
  <condition attribute="statecode" operator="eq" value="0" />
  <condition attribute="statuscode" operator="eq" value="159460001" />
  <link-entity name="sanic_carp_tbl_cuota" from="sanic_arregloid" to="sanic_carp_tbl_arregloid" link-type="any">
    <filter type="and">
      <filter type="or">
        <condition attribute="sanic_fechavencimiento" operator="olderthan-x-days" value="1" />
        <condition attribute="sanic_fechavencimiento" operator="yesterday" />
      </filter>
      <condition attribute="sanic_fechapago" operator="null" />
    </filter>
  </link-entity>
</filter>
```

(En FetchXML, el `link-entity` de tipo `any`/`not any` **va dentro de un `<filter>`**, no como hijo directo de `<entity>`.)

(`link-type="any"` evita filas repetidas cuando un arreglo tiene varias cuotas vencidas; el spike lo probó en una vista guardada.) El atributo `to` es la primaria de Arreglo (`sanic_carp_tbl_arregloid`) y el `from` es el lookup `sanic_arregloid` de la cuota. Riesgo residual del spike: zona horaria del servidor.

Columnas de las vistas de Arreglo (todas llevan la primaria `sanic_nombre` "Codigo interno" primera):

| Vista | Columnas en orden |
|---|---|
| Borradores / Borradores sin movimiento | `sanic_nombre` 110 · `sanic_clienteid` 180 · `sanic_producto` 100 · `sanic_numeroproducto` 140 · `sanic_tipo` 100 · `sanic_moneda` 90 · `sanic_saldoremanente` 120 · `sanic_cuotamensual` 110 · `sanic_plazo` 70 · `sanic_fechacongelamiento` 150 · `sanic_oficialid` 160 · `modifiedon` 150 |
| Arreglos en riesgo | `sanic_nombre` 110 · `sanic_clienteid` 180 · `sanic_producto` 100 · `sanic_numeroproducto` 140 · `sanic_numeroarreglo` 90 · `sanic_tipo` 100 · `sanic_moneda` 90 · `sanic_cuotamensual` 110 · `sanic_plazo` 70 · `sanic_fechainicio` 120 · `sanic_oficialid` 160 |
| Arreglos de prestamo / de tarjeta / vigentes / cerrados / Todos | `sanic_nombre` 110 · `sanic_clienteid` 180 · `sanic_producto` 100 · `sanic_numeroproducto` 140 · `sanic_numeroarreglo` 90 · `statuscode` 110 · `sanic_tipo` 100 · `sanic_moneda` 90 · `sanic_cuotamensual` 110 · `sanic_plazo` 70 · `sanic_oficialid` 160 · `modifiedon` 150 (en "cerrados" se agrega `sanic_fechatermino` 150 antes de `modifiedon`) |
| Arreglos del cliente | `sanic_nombre` 110 · `sanic_producto` 100 · `sanic_numeroproducto` 140 · `sanic_numeroarreglo` 90 · `statuscode` 110 · `sanic_tipo` 100 · `sanic_moneda` 90 · `sanic_cuotamensual` 110 · `sanic_plazo` 70 · `sanic_fechainicio` 120 · `sanic_oficialid` 160 |
| Busqueda rapida de Arreglo | `sanic_nombre` 110 · `sanic_numeroproducto` 150 · `sanic_clienteid` 180 · `sanic_producto` 100 · `sanic_numeroarreglo` 90 · `statuscode` 110 |

Lo que el mockup muestra en "En riesgo" y **una vista no puede dar**: cuotas vencidas, días de atraso, monto vencido (son agregados del detalle). Quedan en el tab/sección de cuotas del formulario (la vista "Cuotas del arreglo" ordenada por número). Si negocio los pide en la lista, hace falta una columna rollup o un gráfico: **no se agrega** (DA-11).

### 3.5 Resto de vistas (Cliente, Cuota, Generación, Versión, Mapeo)

| Inv. | Nombre | Tabla | Clase | Perfil | Filtro | Orden | Columnas |
|---|---|---|---|---|---|---|---|
| 12.10 | **Busqueda rapida de Cliente** | `sanic_carp_tbl_cliente` | BusquedaRapida | Oficial y Jefe | `or` de `like` `{0}` sobre `sanic_cedula`, `sanic_nombrecompleto`, `sanic_correo`, `sanic_celular` (G-V1) | — | `sanic_nombre` 200 · `sanic_cedula` 140 · `sanic_correo` 220 · `sanic_celular` 120 |
| 12.13 | **Clientes** (por defecto) | `sanic_carp_tbl_cliente` | Publica | Oficial y Jefe | `statecode` `eq` `0` | `sanic_nombre` asc | `sanic_nombre` 220 · `sanic_cedula` 140 · `sanic_correo` 220 · `sanic_celular` 120 |
| 12.13 | **Cuotas del arreglo** (subgrid de 12.19) | `sanic_carp_tbl_cuota` | Publica | — (subgrid) | ninguno | `sanic_numero` asc | `sanic_nombre` 110 · `sanic_numero` 70 · `sanic_fechavencimiento` 130 · `sanic_monto` 110 · `sanic_fechapago` 130 · `sanic_montopagado` 110 |
| 12.13 | **Formatos generados del arreglo** (subgrid) | `sanic_carp_tbl_generacionformato` | Publica | — (subgrid) | ninguno | `sanic_fechageneracion` desc | `sanic_nombre` 110 · `sanic_fechageneracion` 150 · `sanic_generadoporid` 180 · `sanic_versionplantillaid` 120 |
| 12.13 | **Versiones de plantilla** (por defecto) | `sanic_carp_tbl_versionplantilla` | Publica | Jefe | ninguno | `sanic_nombre` desc | `sanic_nombre` 110 · `sanic_estado` 110 · `sanic_capacidad` 90 · `sanic_fechaaprobacion` 150 · `sanic_aprobadoporid` 180 |
| 12.13 | **Mapeo de la version** (subgrid de 12.20) | `sanic_carp_tbl_mapeocelda` | Publica | Jefe | ninguno | `sanic_hoja` asc, `sanic_celda` asc | `sanic_nombre` 140 · `sanic_hoja` 170 · `sanic_celda` 80 · `sanic_tipo` 190 · `sanic_campo` 200 · `sanic_filasmaximas` 90 |

Notas: la vista de cuotas **no** tiene la columna "Estado" del mockup (Pendiente, Vencida, Pagada): sería una columna de fórmula nueva (DA-11). Quick find de cédula: la bloqueó el perfil de columna para quien no lo tiene (spike 4: devuelve 0 filas sin error). La vista de búsqueda rápida existente de cada tabla tiene un nombre de fábrica ("Quick Find Active …"): **el playbook toma el nombre real leído en Dev**, porque `vista.py` reconoce la vista por `name` + tabla y una segunda vista de búsqueda rápida no se usa.

### 3.6 Cierre de 12.13

12.13 queda cerrada como **15 vistas** (6 de Conversación, 1 de Email "Respuestas enviadas", 3 de Arreglo, y Clientes, Cuotas del arreglo, Formatos generados del arreglo, Versiones de plantilla, Mapeo de la version). Cada una tiene su playbook `playbooks/vista/<nombre sin espacios>.md`; el inventario las cuenta como la fila 12.13. Total de vistas del diseño: **27** (12 de 12.1 a 12.12 y 15 de 12.13).

Reparto por perfil (entradas del sitemap, sección 7):

| Perfil | Vistas que encuentra |
|---|---|
| Oficial | Mis conversaciones pendientes, Conversaciones sin asignar, Conversaciones abiertas, Correos llegados a conversaciones finalizadas, Conversaciones finalizadas, Clientes, Borradores, Arreglos vigentes, Arreglos en riesgo, Arreglos de prestamo, Arreglos de tarjeta, Arreglos cerrados, Todos los arreglos |
| Jefe (además) | Correos sin respuesta, Finalizadas sin respuesta, Respuestas con error de envio, Respuestas enviadas, Borradores sin movimiento, Versiones de plantilla (y el panel, lote E) |
| Ninguno (solo subgrid o selector) | Conversaciones del cliente, Conversaciones para unir, Arreglos del cliente, Cuotas del arreglo, Formatos generados del arreglo, Mapeo de la version |
| Búsqueda | Busqueda rapida de Cliente y de Arreglo (barra de búsqueda de la grilla) |

## 4. Formularios (12.17 a 12.21, y el de Cuota)

### 4.1 Reglas comunes

- `formulario.py` **actualiza el Main existente** de la tabla (cambia nombre y `formxml`); no crea otro.
- Etiquetas de pestaña y de sección: un texto por idioma (`{"1033": "...", "3082": "..."}`, sin tildes); las de los campos salen del `DisplayName` de la columna.
- Clave de campo: **ro** = solo lectura (G-F1); **ed** = editable; **gesto** = editable *solo* para que el botón lo lea (su valor no se guarda con Guardar; ver 5.3).
- La columna primaria `sanic_nombre` de **Cliente** y **Mapeo** (calculada por plugin, "requerida") **no va en el formulario**: la validación de requerido de la interfaz solo mira lo que está en el formulario, y el plugin la completa. En Conversación, Arreglo y Generación es autonumérica (no requerida): va ro en Conversación y Arreglo.
- **Campos requeridos y calculados por el plugin** (`sanic_saldoremanente`, `sanic_totalpagar`, `sanic_fechafin` en Arreglo): van ro en el formulario. Un requerido con el control deshabilitado no bloquea el guardado (Learn), así que no hace falta ningún ajuste.
- **Campos "gesto" y envío.** En `OnLoad`, todos los campos "gesto" (resultado, motivo y detalle de Conversación; motivos de descarte, anulación y reapertura de Arreglo) llevan `setSubmitMode("never")`: ni el autoguardado ni la navegación los mandan solos; **el botón los manda de forma explícita** en su `updateRecord`. Así Guardar nunca produce `CARP-ARR-024` ni `CARP-CNV-005` por un motivo suelto.
- **Autoguardado y botón Guardar.** Con el autoguardado activo, el botón **Guardar** solo aparece en el alta; en un registro existente se guarda con el icono del pie de la página o con Ctrl+S. En Conversación y Arreglo se cancela el autoguardado (`OnSave`, `getSaveMode() === 70`); en **Cuota** y **Mapeo de celda** también, para que no rebote `CARP-CUO-003` (fecha sin monto, a medio escribir) ni `CARP-MAP-003`/`-004` (campo y tipo a medio elegir).
- Nunca se manda `statecode`/`statuscode` en un alta (humo 5), y nunca se reenvía una fila completa (humo 1). Un formulario manda solo lo que cambió: es el comportamiento nativo.

### 4.2 12.18 `FRM - CARP - Conversacion - Principal` (tabla `sanic_carp_tbl_conversacion`)

Encabezado (3): `statuscode` (ro) · `sanic_oficialasignadoid` (ed) · `sanic_sinrespuestadesde` (ro).

Pestaña única "General" (G-F4 opcional; las secciones van en este orden):

| # | Sección | Contenido en orden | Modo |
|---|---|---|---|
| 1 | Conversacion | `sanic_nombre` ro · `sanic_asunto` ro · `sanic_remitente` ro · `sanic_clienteid` **ed** · `sanic_fechaprimercorreo` ro · `sanic_antiguedad` ro · `sanic_correonuevoenfinalizada` ro | — |
| 2 | Finalizacion | `sanic_resultado` **gesto** · `sanic_motivosinrespuesta` **gesto** · `sanic_detallesinrespuesta` **gesto** · `sanic_fechafinalizacion` ro · `sanic_finalizadoporid` ro · `sanic_unidaaid` ro | — |
| 3 | Tiempos | `sanic_tiempoprimerarespuesta` ro · `sanic_cantidadrespuestas` ro · `sanic_tiempototal` ro | — |
| 4 | Correos | **Timeline** (G-F2): solo actividades de correo; sin notas | — |
| 5 | Arreglos de la conversacion | subgrilla: tabla `sanic_carp_tbl_arreglo`, lookup `sanic_conversacionid`, vista "Arreglos del cliente" | — |

- **Reasignar** (solo el jefe): el jefe edita `sanic_oficialasignadoid` y guarda. El servidor exige jefatura (`CARP-CNV-008`) y que el cambio sea el único (los pasos de `03` §2.4). No hay botón: ahorra un comando y el servidor ya decide. El mockup tiene el botón; esto es una diferencia de interfaz (DA-08).
- **Vincular cliente**: editar `sanic_clienteid` y guardar (válido en una Abierta; en Finalizada/Unida da `CARP-CNV-009`). **Crear cliente**: "+ Nuevo" del propio lookup (abre el formulario 12.17). No hay comando propio.
- **Responder con adjuntos e imágenes**: el botón Responder del timeline (RF-03). No hay código. El remitente lo fija el plugin (`CARP-EML-001` si se intenta otro).
- **Aviso de otra conversación abierta (RF-14)**: `OnLoad` → `SanicCarp.Conversacion.alCargar`. Si la conversación está Abierta, consulta hasta 3 conversaciones Abiertas, con otro id, del mismo cliente **o** del mismo remitente, y muestra `setFormNotification` nivel INFO: "Este cliente tiene otra conversación abierta: CONV-000123 «asunto». Si es el mismo tema, use Unir a otra conversación." Alternativas: (a) columna rollup en Cliente: se recalcula con retraso (no sirve para un aviso), (b) subgrid en el formulario: la relación es de Cliente a Conversación y no de Conversación a Conversación, (c) no avisar y confiar en el subgrid del Cliente: incumple RF-14. Recomendada: `OnLoad` (DA-06). Es una consulta de solo lectura, sin lógica de negocio.
- **Autoguardado**: `OnSave` → `SanicCarp.Conversacion.alGuardar` cancela el guardado automático (`getSaveMode() === 70`); el guardado manual (icono del pie o Ctrl+S) sigue funcionando. `OnLoad` → `alCargar` pone `setSubmitMode("never")` en los tres campos de la sección Finalizacion.
- Visibilidad por estado: **no se oculta nada** en el formulario (los campos siguen ahí y el servidor rechaza lo que no corresponde). Los avisos del mockup ("Llegó un correo nuevo a esta conversación finalizada…") se resuelven con `sanic_correonuevoenfinalizada` visible en la sección 1 y con la vista 12.2.

### 4.3 12.17 `FRM - CARP - Cliente - Principal` (tabla `sanic_carp_tbl_cliente`)

Sin encabezado. Pestaña "General":

| # | Sección | Contenido en orden | Modo |
|---|---|---|---|
| 1 | Datos del cliente | `sanic_nombrecompleto` · `sanic_cedula` · `sanic_correo` · `sanic_celular` | ed |
| 2 | Arreglos del cliente | subgrilla `sanic_carp_tbl_arreglo` por `sanic_clienteid`, vista "Arreglos del cliente" | — |
| 3 | Conversaciones del cliente | subgrilla `sanic_carp_tbl_conversacion` por `sanic_clienteid`, vista "Conversaciones del cliente" | — |

Un Cliente nuevo se crea con el "+ Nuevo" de la grilla, del lookup de Conversación o del de Arreglo. "Registrar arreglo" desde el cliente es el "+ Nuevo" de la subgrilla de arreglos (hereda el cliente). Sin comandos, sin JS.

### 4.4 12.19 `FRM - CARP - Arreglo - Principal` (tabla `sanic_carp_tbl_arreglo`)

Encabezado (3): `statuscode` (ro) · `sanic_numeroarreglo` (ro) · `sanic_fechacongelamiento` (ro).

Objetivo (G-F4): pestañas **General**, **Cuotas**, **Formatos**, **Cierre**. Mínimo utilizable: una pestaña con las secciones en el mismo orden.

| Pestaña | # | Sección | Contenido en orden | Modo |
|---|---|---|---|---|
| General | 1 | Acuerdo | `sanic_nombre` ro · `sanic_clienteid` ed · `sanic_conversacionid` ed · `sanic_producto` ed · `sanic_numeroproducto` ed · `sanic_tipo` ed · `sanic_moneda` ed · `sanic_canalorigen` ed · `sanic_oficialid` ed · `sanic_cuentadebitar` ed · `sanic_fechaformalizacion` ed · `sanic_comentarios` ed | — |
| General | 2 | Montos y plazo | `sanic_saldoinicial` ed · `sanic_montoabonado` ed · `sanic_saldoremanente` ro · `sanic_plazo` ed · `sanic_cuotamensual` ed · `sanic_totalpagar` ro · `sanic_fechainicio` ed · `sanic_fechafin` ro | — |
| General | 3 | Plantilla y numeracion | `sanic_versionplantillaid` ro · `sanic_numeroprovisional` ro | — |
| General | 4 | Cliente al congelar | `sanic_instantaneanombre` ro · `sanic_instantaneacedula` ro · `sanic_instantaneacorreo` ro · `sanic_instantaneacelular` ro | — |
| Cuotas | 5 | Cuotas | subgrilla `sanic_carp_tbl_cuota` por `sanic_arregloid`, vista "Cuotas del arreglo" | — |
| Formatos | 6 | Formatos generados | subgrilla `sanic_carp_tbl_generacionformato` por `sanic_arregloid`, vista "Formatos generados del arreglo" | — |
| Cierre | 7 | Formalizacion | `sanic_fechaformalizado` ro · `sanic_formalizadoporid` ro · `sanic_fechatermino` ro | — |
| Cierre | 8 | Descarte | `sanic_motivodescarte` **gesto** · `sanic_fechadescarte` ro · `sanic_descartadoporid` ro | — |
| Cierre | 9 | Anulacion | `sanic_motivoanulacion` **gesto** · `sanic_fechaanulacion` ro · `sanic_anuladoporid` ro | — |
| Cierre | 10 | Reapertura | `sanic_motivoreapertura` **gesto** · `sanic_fechareapertura` ro · `sanic_reabiertoporid` ro · `sanic_cantidadreaperturas` ro | — |

- Un campo no puede repetirse en el formulario (la herramienta lo rechaza): por eso el estado vive solo en el encabezado.
- La auditoría del mockup ("Auditoría") es la **historia de auditoría nativa** de Dataverse: no es un componente. Los datos económicos y de identificación no se auditan (D-20). Ver la historia exige el privilegio **View Audit History**, que la matriz `04` no asigna a Oficial ni a Jefe: **no se modifican los roles**; queda como decisión pendiente del usuario y como verificación en Dev con un no administrador (sección 12).
- `sanic_cuentadebitar` se ve solo con el perfil de columna; sin él aparece bloqueado y el alta de arreglos falla de forma visible. 13.8 es previo (sección 9).
- Los campos de **datos del acuerdo** quedan editables aunque el arreglo esté congelado: el servidor responde `CARP-ARR-011` con el nombre de la columna. No hay lógica de bloqueo en el cliente (regla de la definición: el cliente solo da feedback).
- Alta: el formulario manda solo los campos que el usuario llenó (sin `statecode`, humo 5). El plugin pone la versión Vigente, el canal, los montos y el calendario de cuotas, y el usuario ve las cuotas en la sección 5 al guardar.
- `OnSave` → `SanicCarp.Arreglo.alGuardar`: cancela el autoguardado (igual que en Conversación). `OnLoad` → `SanicCarp.Arreglo.alCargar`: (1) `setSubmitMode("never")` en `sanic_motivodescarte`, `sanic_motivoanulacion` y `sanic_motivoreapertura`; (2) **habilita el motivo de reapertura**: el formulario de un registro inactivo (Terminado, Anulado, Descartado) deshabilita todos los controles, así que si el arreglo está **Terminado** (o **Vigente congelado**, por si la plataforma también lo deshabilita) y el usuario es de jefatura (`esJefe()`), hace `setDisabled(false)` sobre `sanic_motivoreapertura`. **[verificar en Dev]** que el control habilitado por script se puede escribir en un registro inactivo y que el botón lo envía. **Respaldo si no**: el diálogo propio de DA-01 (página generativa o HTML con `parent.Xrm`) solo para Reabrir; (3) en el **alta** desde la subgrilla de una conversación, hereda el cliente y el oficial (más abajo).
- **Alta desde una conversación (B7).** El "+ Nuevo" de la subgrilla de arreglos de la conversación llena `sanic_conversacionid`. Para heredar además `sanic_clienteid` (el cliente de la conversación) y `sanic_oficialid` (el oficial asignado de la conversación): **primera opción**, el **mapeo de columnas de la relación** REL-03 (`sanic_carp_conversacion_arreglo`): `sanic_clienteid` → `sanic_clienteid` y `sanic_oficialasignadoid` → `sanic_oficialid` (lookups con el mismo destino), sin código; es un paso manual del maker porque ninguna herramienta lo hace. **[verificar en Dev]** que el mapeo existe para esa relación y copia al crear. **Respaldo** (si no): `SanicCarp.Arreglo.alCargar` en el alta lee la conversación padre (`Xrm.WebApi.retrieveRecord`, dos columnas) y llena los dos lookups si están vacíos. Desde el Cliente, el "+ Nuevo" ya hereda el cliente.
- **Registrar un pago** no pasa por este formulario: se abre la cuota desde la subgrilla (4.7).

### 4.5 12.20 `FRM - CARP - Version de plantilla - Principal` (tabla `sanic_carp_tbl_versionplantilla`)

Encabezado (3): `sanic_estado` (ro) · `sanic_capacidad` (ed) · `sanic_fechaaprobacion` (ro). Pestaña "General":

| # | Sección | Contenido | Modo |
|---|---|---|---|
| 1 | Version | `sanic_nombre` ed (patrón `^v[0-9]{2,3}$`, `CARP-VER-012`) · `sanic_archivo` ed (control de archivo; 5 MB) · `sanic_huella` ro · `sanic_aprobadoporid` ro | — |
| 2 | Mapeo de la version | subgrilla `sanic_carp_tbl_mapeocelda` por `sanic_versionplantillaid`, vista "Mapeo de la version" | — |

`sanic_estado` va ro: **Publicar es un botón** que manda solo `sanic_estado` (el servidor rechaza cualquier otra columna en ese guardado, `CARP-VER-013`). Una versión nueva se crea con el "+ Nuevo" de la grilla (el plugin la fuerza a Borrador). Para "clonar y corregir" en la primera versión: nuevo número, mismo archivo, y el mapeo se carga con la **importación nativa desde Excel** (plantilla de importación de la tabla Mapeo de celda), que pasa por los plugins y por los privilegios del usuario, en vez de crear 28 filas a mano **[verificar en Dev]**. Sin botón "Clonar" (DA-13).

### 4.6 12.21 `FRM - CARP - Mapeo de celda - Principal` (tabla `sanic_carp_tbl_mapeocelda`)

Sin encabezado. Una sección "Celda del formato": `sanic_versionplantillaid` ro (se hereda de la subgrilla; moverlo da `CARP-MAP-006`) · `sanic_hoja` · `sanic_celda` · `sanic_tipo` · `sanic_campo` · `sanic_filasmaximas` (todos ed). Sin `sanic_nombre` (se calcula). Solo se edita con la versión en Borrador (`CARP-MAP-001`). `OnSave` cancela el autoguardado (`getSaveMode() === 70`) para que `CARP-MAP-003`/`-004` no rebote con el tipo y el campo a medio elegir.

### 4.7 Propuesta: `FRM - CARP - Cuota - Principal` (no está en el inventario; DA-09)

El registro de pagos (RF-10) necesita un formulario mínimo de Cuota: sin él, el Main de fábrica muestra las cinco columnas solo-sistema como editables. Sin encabezado, una sección "Pago": `sanic_nombre` ro · `sanic_numero` ro · `sanic_fechavencimiento` ro · `sanic_monto` ro · `sanic_arregloid` ro · **`sanic_fechapago` ed · `sanic_montopagado` ed**. Se abre al hacer clic en una fila de la subgrilla de cuotas (comportamiento nativo). El pago se guarda con Guardar: fecha y monto **juntos** (si falta uno, `CARP-CUO-003`). Un pago solo se acepta con el arreglo Vigente (`CARP-CUO-002`). `OnSave` cancela el autoguardado: sin eso, escribir la fecha y esperar daría `CARP-CUO-003`. **Alternativas**: (A) grilla editable en la subgrilla (control de "grilla editable"; `formulario.py` genera la subgrilla de solo lectura y no la configura), (B) este formulario (la recomendada: una tabla, un playbook, nada de código). Propuesta de fila 12.26 (sección 11).

## 5. Web resource y comandos (12.22 y 12.24)

### 5.1 Qué se construye

| Pieza | Nombre | Tipo | Archivo en el repo | Notas |
|---|---|---|---|---|
| JS | `sanic_carp_wr_js_comandos` (`WR - CARP - JS - Comandos`) | JS | `webresources/js/comandos.js` | Un solo archivo. Sirve a Conversación, Arreglo y Versión de plantilla |
| Iconos | `sanic_carp_wr_svg_app`, `_lista`, `_accion`, `_alerta` | SVG | `webresources/svg/<nombre>.svg` | Cuatro, compartidos (DA-10) |

Playbooks: `playbooks/web-resource/<nombre lógico>.md` (uno por recurso). La carpeta `webresources/` se crea en la raíz del repo; el límite de la herramienta es 5 MB.

### 5.2 Ribbon JS frente a Power Fx commanding

| Criterio | Ribbon clásico con JS (recomendado) | Power Fx (command designer) |
|---|---|---|
| Herramienta | Existe: `importar_solucion.py` (cambio `ribbon`) | No existe; se haría a mano en el diseñador, o por la tabla `appaction` sin ensayo |
| Llamar una Custom API | `Xrm.WebApi.online.execute` | Posible por la fuente `Environment` de Dataverse **[verificar]** |
| Entregar un archivo | `Xrm.Navigation.openFile` | No hay forma directa |
| Capturar una entrada | Los campos del formulario (M1) | Tampoco |
| Visibilidad por estado | Regla JS (`EnableRule`) | Fórmula `Self.Selected.Item.<col>` (más cómoda) |
| Revisión y Git | El `.js` y el playbook | Vive en la solución exportada, sin diff legible |
| Costo de mantenimiento | Un `.js` | Cero JS, pero cada gesto se rehace a mano |
| Riesgo | El ribbon clásico es la forma "heredada"; Microsoft empuja la moderna | Es preview/limitada para varios casos |

Se recomienda el ribbon JS (D-17 ya lo decidió y el costo de Power Fx es mayor que el de un `.js`). Tradeoff: se paga el paso manual de **regenerar la metadata del ribbon** (`&ribbondebug=true` → Command Checker) tras cada importación, que `importar_solucion.py` ya avisa.

### 5.3 Reglas del JavaScript

1. **Sin lógica de negocio.** Las reglas están en los plugins. El JS: (a) lee los campos del formulario, (b) pide confirmación, (c) llama a la API o hace **un** `Xrm.WebApi.updateRecord`, (d) muestra el resultado o el mensaje `CARP-*`, (e) refresca. Las "reglas de visibilidad" repiten el diagrama de estados para no mostrar un botón sin sentido; si se desvían, el servidor sigue siendo la barrera.
2. **Solo las columnas del gesto.** Cada `updateRecord` manda únicamente las columnas del gesto (tabla 5.4), nunca la fila completa ni un `statecode` en un alta (humo 1 y 5).
3. **Mensaje del error.** `mostrarError(err)`: busca `/CARP-[A-Z]{3,4}-\d{3}:[\s\S]*/` en `err.message`, es decir **desde el código hasta el final del mensaje, con todas las líneas** (los errores de publicación vienen unidos con `\n`). Si lo encuentra, `Xrm.Navigation.openErrorDialog({message: <ese texto>})` (el texto ya está en español y sin datos sensibles, `03` §0.5). Si no (error de la capa OData antes del plugin, texto técnico en inglés), el mensaje es "No se pudo completar la acción. Intente de nuevo; si persiste, avise al equipo de soporte." y el texto técnico va a `details`. Nunca se escribe en consola ni en trazas el contenido de campos.
4. **Los `-900` no se reintentan solos.** El texto del catálogo ya dice "Intente de nuevo". Excepción única: `CARP-TOM-900` (sección 5.4, Tomar).
5. **Refresco.** Tras un gesto en formulario: `formContext.data.refresh(false)`. Tras un gesto en grilla: `gridContext.refresh()`.
6. **Parámetros del ribbon.** Por G-I1b: en `Form`, `PrimaryControl` (el formContext); en `HomepageGrid`, `SelectedControlSelectedItemIds`. Una función de comando acepta ambos con una utilidad `contexto(arg)`.
7. **Enable rules** (`regla` del playbook): funciones `puede<Gesto>(PrimaryControl)` (en `Form`; en `HomepageGrid`, el contexto de grilla) que devuelven `true` ante la duda. La `EnableRule` recibe `PrimaryControl` igual que el comando (G-I1b). `esJefe()` consulta `Xrm.Utility.getGlobalContext().userSettings.roles` por el nombre `SR - CARP - Jefe`; `userSettings.roles` **incluye los roles que el usuario recibe por los teams** (cerrado, DA-14). Es solo UX: el servidor decide.
8. **Namespacing**: `SanicCarp.Conversacion.*`, `SanicCarp.Arreglo.*`, `SanicCarp.Plantilla.*`. El nombre de la función del ribbon es el nombre completo (`SanicCarp.Arreglo.formalizar`).
9. **Llamada a una Custom API**: `Xrm.WebApi.online.execute` con `getMetadata()` → `operationType: 0` (Action), `boundParameter: null`, `operationName` = nombre lógico de la API, `parameterTypes: {<param>: {typeName: "Edm.Guid", structuralProperty: 1}}`. Los nombres de parámetro son los cortos de `03` §1.

### 5.4 Los gestos uno por uno (contenido de 12.24)

Leyenda: **F** = formulario, **G** = grilla (`HomepageGrid`). "Visible si" es la `regla` JS. "Manda" es todo lo que escribe el JS. Todos los textos de confirmación van en 1033 y 3082 (los botones; los diálogos usan el idioma de la interfaz de la persona con texto en español).

| # | Gesto | Dónde | Visible si | Confirmación | Captura | Qué hace | Manda | Salida |
|---|---|---|---|---|---|---|---|---|
| 1 | **Tomar** | F y G | Abierta y `sanic_oficialasignadoid` vacío | No | — | Custom API `sanic_carp_capi_tomarconversacion` | `conversacionid` | Toast: "Conversación tomada." (usa `oficialnombre`). `yaasignada = true` → "Ya era suya." Una sola fila seleccionada en G; si hay más de una: "Seleccione una sola conversación." |
| 2 | **Finalizar** | F | Abierta | Sí: "Se registra el resultado, quién finaliza y cuándo." | Campos **gesto** de la sección Finalizacion: resultado, motivo, detalle | `updateRecord(sanic_carp_tbl_conversacion, id, …)` | `statecode: 1`, `statuscode: 2`, `sanic_resultado` y, si tienen valor, `sanic_motivosinrespuesta` y `sanic_detallesinrespuesta`. Los vacíos **no** se mandan | Toast "Conversación finalizada." Los faltantes los reclama el servidor: `CARP-CNV-003`/`-004` |
| 3 | **Reabrir conversación** | F | Finalizada | Sí: "Vuelve a Abierta; se limpian el resultado y la finalización." | — | `updateRecord` | **solo** `statecode: 0`, `statuscode: 1` (cualquier otra columna da `CARP-CNV-010`) | Toast "Conversación reabierta." |
| 4 | **Unir a otra conversación** | F | Abierta | Sí, con los dos números: "Los correos de CONV-A pasan a CONV-B. No se puede deshacer." | Selector `Xrm.Utility.lookupObjects` sobre `sanic_carp_tbl_conversacion`, un solo valor, filtro `statecode = 0` y `id ≠ origen`, vista "Conversaciones para unir" (resuelta por nombre con una lectura de `savedquery`; si no la halla, usa la vista de búsqueda por defecto) | Custom API `sanic_carp_capi_unirconversaciones` | `origenid`, `destinoid` | Toast con `correosmovidos` y `arreglosrepunteados`; navega al destino (`Xrm.Navigation.openForm`). Errores `CARP-UNI-001` a `-010` tal cual |
| 5 | **Iniciar conversacion nueva** | F | Finalizada y `sanic_correonuevoenfinalizada = sí` | Sí: "El correo llegado después de la finalización se mueve a una conversación nueva; esta sigue finalizada." | — | Custom API `sanic_carp_capi_iniciarconversacionnueva` | `conversacionid` | Navega a `nuevaconversacionid`. Errores `CARP-INI-*` |
| 6 | **Generar formato** | F | Siempre (también Terminado, Anulado y Descartado: se genera sin congelar, OA-9) | Sí y **depende del estado**: Borrador sin congelar → "Al generar se bloquean los datos del acuerdo; podrá desbloquear antes de la firma."; Vigente sin congelar (reabierto) → "Se vuelve a congelar con los datos actuales del cliente."; el resto → "Se genera el formato; el arreglo no cambia." Si el formulario tiene cambios sin guardar → "Guarde los cambios antes de generar el formato." y no llama | — | Custom API `sanic_carp_capi_generarformato` | `arregloid` | Descarga el `.xlsx` (`archivo` en Base64 → `openFile`, `fileName = nombrearchivo`, `fileSize` en KB = `ceil(bytes/1024)`, MIME `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, `openMode: 2`). Si `advertencias` no está vacío, las muestra. Refresca. **Descargas y OneDrive (13.17, RNF-03):** si TI confirma que la carpeta Descargas está redirigida a OneDrive (Known Folder Move), el formato (con cédula y cuenta del cliente) llegaría a OneDrive, lo que RNF-03 prohíbe; en ese caso este gesto **no se habilita** hasta resolver la redirección (excepción de carpeta o descarga a otra ruta). 13.17 es previo a la primera versión con datos reales. Tarda ~1,5 s las primeras llamadas tras publicar el paquete: mostrar "Generando…" (`Xrm.Utility.showProgressIndicator`). Errores `CARP-GEN-*` |
| 7 | **Desbloquear** | F | Borrador **y** `sanic_fechacongelamiento` con valor | Sí: "El formato ya impreso deja de valer; hay que reimprimirlo." | — | `updateRecord` | **solo** `sanic_fechacongelamiento: null` (humo 1: sobre un borrador no congelado daría `CARP-ARR-017`; por eso la regla de visibilidad exige que esté congelado) | Toast "Borrador desbloqueado." |
| 8 | **Formalizar** | F | Borrador congelado | Sí: "Confirme que el cliente firmó el formato impreso." | — | `updateRecord` | **solo** `statecode: 0`, `statuscode: 159460001` | Toast "Arreglo formalizado con el N.º <`sanic_numeroarreglo` releído>". Errores `CARP-ARR-019`/`-020`/`-023` |
| 9 | **Descartar** | F | Borrador | Sí: "Queda como descartado sin consumir número. No se puede recuperar." | Campo **gesto** `sanic_motivodescarte` | `updateRecord` | `statecode: 1`, `statuscode: 159460003`, `sanic_motivodescarte` | Toast "Borrador descartado." Falta de motivo: `CARP-ARR-013` |
| 10 | **Anular** | F | Vigente | Sí: "La anulación no se puede revertir. Si el cliente vuelve a negociar, se registra un arreglo nuevo." | Campo **gesto** `sanic_motivoanulacion` | `updateRecord` | `statecode: 1`, `statuscode: 159460002`, `sanic_motivoanulacion` | Toast "Arreglo anulado." `CARP-ARR-014` |
| 11 | **Reabrir arreglo** | F | (Terminado) o (Vigente **congelado**), y `esJefe()` | Sí: "Descongela los datos para corregirlos. El próximo formato congela de nuevo con los datos actuales." | Campo **gesto** `sanic_motivoreapertura` (habilitado por `OnLoad` en un arreglo inactivo, ver 4.4; respaldo: diálogo de DA-01) | `updateRecord` | De un Terminado: `statecode: 0`, `statuscode: 159460001`, `sanic_motivoreapertura`. De un Vigente congelado: **solo** `sanic_motivoreapertura` | Toast "Arreglo reabierto." `CARP-ARR-015`/`-016`/`-017` |
| 12 | **Registrar pago** | Sin botón | — | — | Campos `sanic_fechapago` y `sanic_montopagado` del formulario de Cuota (4.7) | Guardar nativo | Lo que el usuario cambió | `CARP-CUO-001`/`-002`/`-003` en el diálogo de error nativo |
| 13 | **Publicar versión** | F | `sanic_estado` Borrador y `esJefe()` | Sí: "La versión pasa a Vigente, la actual pasa a Histórica y no se podrá modificar." Si hay cambios sin guardar → "Guarde los cambios antes de publicar." | — | `updateRecord` | **solo** `sanic_estado: 159460002` | Toast "Versión publicada." Errores `CARP-VER-005` a `-010`, `-013` |
| 14 | **Vista previa** | F | `sanic_estado` Borrador y `esJefe()` | Sí: "Se genera con un arreglo existente; no congela ni guarda nada." | Selector `lookupObjects` de un arreglo (cualquier estado) | Custom API `generarformato` | `arregloid`, `versionpreviaid` (id de la versión abierta) | Descarga igual que (6). `CARP-GEN-006`/`-007` |

Detalles que no admiten duda:

- **Tomar y la carrera (humo 4).** Si el error contiene `CARP-TOM-900`, el JS muestra el texto neutro del catálogo (**"La conversación cambió en este instante. Actualice la pantalla y vuelva a intentar."**) y **además refresca** el formulario o la grilla, para que se vea quién la tiene. Sin reintento automático. El contrato `03` §1.3 paso 7 decía "el web resource vuelve a llamar" y la segunda llamada mostraría `CARP-TOM-004` con el nombre; se prefiere no reintentar (DA-07) porque el refresco ya muestra quién la tiene.
- **Los campos "gesto" y el guardado.** Si el usuario escribe un motivo y guarda con Guardar sin el botón, el servidor responde `CARP-ARR-024` (arreglo) o `CARP-CNV-005` (conversación). Es correcto y alcanza; el texto de la sección dice "Escriba el motivo y use el botón".
- **Orden de las llamadas.** Un gesto = una llamada. Nunca "guardar y luego gesto".
- **Quién lo hace.** Oficial y Jefe, salvo lo marcado `esJefe()` (que además el servidor exige por `EsJefatura`). Tomar, unir e iniciar conversación nueva exigen Write sobre Conversación (privilegio de ejecución); Generar formato, Read sobre Generación.
- **Reasignar** y **Responder** no son comandos (sección 4.2).

### 5.5 Botones estándar a ocultar (12.24, lote C)

Son opcionales. Se ocultan con un **`DisplayRule` que siempre da falso** (reversible; G-I3), no con `HideCustomAction`. Candidatos por tabla, a confirmar con `RetrieveEntityRibbon` en Dev (la herramienta ya ofrece solo los presentes): Conversación y Arreglo y Cuota y Versión y Mapeo y Cliente: `.Deactivate`, `.Activate`, `.Delete`, `.Assign`, `.Share`, `.NewRecord` (solo en Conversación y Cuota, que no se crean a mano). El servidor ya rechaza todo (`CARP-CNV-003`, `CARP-ARR-010`, `CARP-CLI-004`, …) y los privilegios que no existen ya deshabilitan Eliminar, Asignar y Compartir. **Recomendación**: no ocultar nada en la primera versión; ocultar Activar/Desactivar en el lote C (DA-15).

### 5.6 Botones por tabla (contenido de los cambios `ribbon`)

Un cambio `ribbon` reemplaza el `RibbonDiffXml` entero de **su tabla**: una entrada por tabla, con **todos** sus botones.

| Tabla | Botones (`id`, `ubicaciones`, `secuencia`, `funcion`, `regla`, icono) |
|---|---|
| `sanic_carp_tbl_conversacion` | `tomar` (Form, HomepageGrid, 10, `SanicCarp.Conversacion.tomar`, `puedeTomar`, accion) · `finalizar` (Form, 20, …, accion) · `unir` (Form, 30, …, accion) · `reabrir` (Form, 40, accion) · `iniciarnueva` (Form, 50, accion) |
| `sanic_carp_tbl_arreglo` | `generar` (Form, 10, accion) · `desbloquear` (Form, 20, accion) · `formalizar` (Form, 30, accion) · `descartar` (Form, 40, alerta) · `anular` (Form, 50, alerta) · `reabrir` (Form, 60, alerta) |
| `sanic_carp_tbl_versionplantilla` | `publicar` (Form, 10, accion) · `vistaprevia` (Form, 20, accion) |

Cada botón lleva `etiqueta` y `tooltip` en 1033 y 3082 (texto de interfaz, con tildes permitidas; los rótulos del ribbon no pasan por BP-PP-197). Etiquetas: Tomar, Finalizar, Unir a otra conversación, Reabrir, Iniciar conversación nueva; Generar formato, Desbloquear, Formalizar, Descartar, Anular, Reabrir arreglo; Publicar versión, Vista previa. Id completo: `sanic.carp.<tabla corta>.<id>` (lo arma la herramienta).

## 6. Dashboard y gráficos (12.14 a 12.16)

**Sin herramienta**: `herramientas/` no tiene tipo de playbook para gráficos ni dashboards. Los gráficos del sistema son filas de `savedqueryvisualization` (XML de datos y de presentación) y el dashboard una fila `systemform` de tipo dashboard. La entrada del dashboard en el sitemap tampoco la genera `app.py` (G-A1).

**Recomendación (DA-05): diferir los tres a después de la primera versión utilizable y construirlos a mano, con una guía y una verificación por API.** Costo: tres componentes manuales frente a una herramienta nueva que se usaría tres veces. Alternativa: una herramienta `grafico.py` pequeña (crear con `POST savedqueryvisualizations`); solo conviene si se prevén más gráficos.

Qué ve el jefe mientras tanto: las vistas 12.1, 12.3, 12.4 y las columnas `sanic_tiempoprimerarespuesta` y `sanic_tiempototal` de "Conversaciones finalizadas" (la exportación a Excel da el promedio). No hay promedio por oficial hasta el lote E.

| Inv. | Componente | Definición |
|---|---|---|
| 12.15 | `CHT - CARP - Promedio por respuesta` | Tabla `email`. Columna (categoría): `sanic_respondidoporid`. Medida: **promedio** de `sanic_tiemporespuesta`. Filtro: `sanic_tiemporespuesta` no nulo. Tipo: barras. Título "Promedio de minutos por respuesta, por oficial". Precondición: `email` en la app y el gráfico agregado a la solución (el componente `email` se incluyó sin subcomponentes: el gráfico se agrega aparte) |
| 12.16 | `CHT - CARP - Tiempo total` | Tabla `sanic_carp_tbl_conversacion`. Categoría: `sanic_oficialasignadoid`. Medida: **promedio** de `sanic_tiempototal`. Filtro: `statecode = 1`, `statuscode = 2`, `sanic_resultado ≠ 159460003` (sin las "Sin respuesta", a confirmar con negocio, D-27), `sanic_tiempototal` no nulo. Tipo: barras |
| 12.14 | `DASH - CARP - Supervision` | Dos gráficos arriba (12.15 y 12.16) y dos listas abajo: "Correos sin respuesta" (12.1) y "Respuestas con error de envio" (12.4). Se agrega a la app y se enlaza desde el sitemap con la entrada "Panel de supervision" (Url al dashboard) |

Guía manual (lote E): (1) crear cada gráfico desde el editor de gráficos de la tabla, con el nombre de la tabla; (2) exportar la solución y confirmar que el gráfico viaja; (3) armar el dashboard con 4 componentes; (4) agregarlo a la app y al sitemap; (5) verificar por `GET savedqueryvisualizations?$filter=name eq '…'` y `GET systemforms?$filter=type eq 0 and name eq '…'` que existen y que el dashboard pertenece a la solución. Los gráficos de Email dependen de que `sanic_respondidoporid` y `sanic_tiemporespuesta` ya se llenen: lo hace S-35, que corre con la plataforma real.

## 7. Sitemap (12.23) y app (12.25)

### 7.1 App

| Dato | Valor |
|---|---|
| `nombre` | `MDA - CARP - Control de Arreglos de Pago` |
| `uniquename` | `sanic_carp_mda_controlarreglospago` |
| `sitemap` | `sanic_carp_sm_controlarreglospago` |
| `icono` | `sanic_carp_wr_svg_app` |
| `roles` | `SR - CARP - Oficial`, `SR - CARP - Jefe` (Servicio y Purga no abren la app) |
| `tablas` | las siete propias y `email` (G-I1a) |

Orden de construcción: `app.py` (crea la app y el sitemap) → `importar_solucion.py` (`app_componentes`: agrega las tablas; `AddAppComponents` no agrega tablas) → botones del ribbon (12.24) → regenerar la metadata del ribbon → `app.py --solo-verificar`. El playbook `playbooks/app/controlarreglospago.md` lleva 12.23 y 12.25.

### 7.2 Sitemap

Títulos de área, grupo y entrada: texto por idioma, sin tildes. Las vistas se nombran como están en la sección 3. "Privilegio" es la condición de visibilidad (G-A1): `Create` sobre `sanic_carp_tbl_versionplantilla`, que solo tiene el Jefe. Sin G-A1, las áreas del jefe las ve todo el mundo y no se pierde seguridad (DA-04).

| Área | Grupo | Entrada (`id`) | Tabla | Vista | Ícono | Ve |
|---|---|---|---|---|---|---|
| **Conversaciones** | Bandeja | `mispendientes` | Conversación | Mis conversaciones pendientes | lista | Oficial, Jefe |
| | | `sinasignar` | Conversación | Conversaciones sin asignar | lista | Oficial, Jefe |
| | | `abiertas` | Conversación | Conversaciones abiertas | lista | Oficial, Jefe |
| | | `llegadasafinalizadas` | Conversación | Correos llegados a conversaciones finalizadas | alerta | Oficial, Jefe |
| | | `finalizadas` | Conversación | Conversaciones finalizadas | lista | Oficial, Jefe |
| **Clientes y arreglos** | Clientes | `clientes` | Cliente | Clientes | lista | Oficial, Jefe |
| | Arreglos | `borradores` | Arreglo | Borradores | lista | Oficial, Jefe |
| | | `vigentes` | Arreglo | Arreglos vigentes | lista | Oficial, Jefe |
| | | `enriesgo` | Arreglo | Arreglos en riesgo | alerta | Oficial, Jefe |
| | | `prestamo` | Arreglo | Arreglos de prestamo | lista | Oficial, Jefe |
| | | `tarjeta` | Arreglo | Arreglos de tarjeta | lista | Oficial, Jefe |
| | | `cerrados` | Arreglo | Arreglos cerrados | lista | Oficial, Jefe |
| | | `todos` | Arreglo | Todos los arreglos | lista | Oficial, Jefe |
| **Supervision** | Seguimiento | `sinrespuesta` | Conversación | Correos sin respuesta | alerta | **Jefe** (privilegio) |
| | | `cierres` | Conversación | Finalizadas sin respuesta | lista | **Jefe** |
| | | `errores` | Email | Respuestas con error de envio | alerta | **Jefe** |
| | | `respenviadas` | Email | Respuestas enviadas | lista | **Jefe** |
| | | `borradoressinmov` | Arreglo | Borradores sin movimiento | alerta | **Jefe** |
| | | `panel` (lote E) | (dashboard) | Panel de supervision | lista | **Jefe** |
| **Administracion del formato** | Plantilla | `plantillas` | Versión de plantilla | Versiones de plantilla | lista | **Jefe** |

Notas: (1) "Buscar historial" del mockup es la búsqueda rápida de la grilla de Clientes y de Arreglos (RF-13); no es una entrada. (2) El mapeo y las cuotas se abren desde su formulario padre, sin entrada. (3) "Formatos generados" se ve en el arreglo. (4) Una entrada con vista usa `Url` con `viewid`; el orden de las entradas es el de la tabla. (5) El área "Validación del mockup / Preguntas para negocio" no se construye. (6) El oficial ve "Correos sin respuesta" en su bandeja a través de "Mis conversaciones pendientes" y de "Correos llegados a conversaciones finalizadas"; el jefe ve el panorama completo en Supervisión (D-27).

## 8. Flujos (11.1 a 11.3)

### 8.1 Reglas comunes

- Formato: `flujo.py`. Playbook con `nombre` (`Cloud Flow - CARP - COD - Nombre`, sin tildes), `archivo` (un `.json` de definición Logic Apps en `playbooks/flujo/<cod>.json`, **sin GUID**: usa `@@conr:<clave>@@`, `@@ev:<schemaname>@@`), `conexiones`, `variables`, `hijos`, `activar`.
- Disparador y acciones de Dataverse: conector Dataverse con `CONR - CARP - Dataverse`; avisos: conector Office 365 Outlook con `CONR - CARP - Outlook` (10.6 y 10.5). Ambos conectores en el mismo grupo de DLP.
- **Dueño**: la cuenta de servicio licenciada (13.6, rol `SR - CARP - Servicio`). `flujo.py` crea el flujo con la identidad de la herramienta, no con la de la cuenta de servicio. La asignación del dueño y la creación de las conexiones (que solo las crea la propia cuenta, iniciando sesión) son **pasos manuales** del procedimiento 13.13 y 13.15 (DA-16). En Dev, hasta que exista 13.6, el flujo puede ser del desarrollador; en Test y Prod, no.
- **Secure inputs y secure outputs** en toda acción sobre tablas de ARP (`runtimeConfiguration.secureData.properties = [inputs, outputs]`). Los flujos **no leen** cuerpos ni adjuntos, y los avisos llevan solo conteos y nombres de cola/buzón (nada de asunto, remitente, cliente o cédula).
- Variables de entorno (valor por entorno): `sanic_carp_ev_destinatariosalertas` (10.3: lista de correos separados por `;`), `sanic_carp_ev_umbralesmonitoreo` (10.4: ver abajo) y `sanic_carp_ev_buzonarea` (10.8).

### 8.2 10.3 y 10.4: contenido (cierra lo que faltaba)

| Variable | Tipo | Contenido | Uso |
|---|---|---|---|
| `sanic_carp_ev_destinatariosalertas` | Texto | `jefe@…;equipo-automatizacion@…` | Campo "Para" de todos los avisos |
| `sanic_carp_ev_umbralesmonitoreo` | JSON | `{"horasSinEntrada":4,"ventanaErroresMin":35,"horasPurga":24,"minutosReconciliar":10}` | 11.2 (una sola variable; el JSON evita cuatro variables) |

`horasSinEntrada` es un valor por defecto de este diseño: **a confirmar con el jefe** (DA-03). El umbral de 30 días de los borradores sin movimiento **no** es una variable: es la vista 12.6.

### 8.3 11.1 `Cloud Flow - CARP - EST - Avisar estado del mailbox`

| Dato | Valor |
|---|---|
| ID | `CARP-EST` (el `COD` de tres letras queda `EST`) |
| Disparador | Dataverse, "Cuando se agrega, modifica o elimina una fila": cambio = **Modificada**, tabla **Buzones** (`mailbox`), **ámbito = Unidad de negocio** (`Servicio` tiene `prvReadMailbox` en alcance local), columnas seleccionadas `incomingemailstatus,outgoingemailstatus`, filtro de filas `incomingemailstatus eq 2 or outgoingemailstatus eq 2` |
| Acción 1 | Condición: `emailaddress` del buzón igual al valor de `sanic_carp_ev_buzonarea`. Si no, termina (el entorno compartido tiene otros buzones; con alcance de unidad de negocio solo llegan los de la BU, pero se confirma) |
| Acción 2 | Enviar correo (V2) a `sanic_carp_ev_destinatariosalertas`: asunto "CARP: falla la sincronización del buzón del área"; cuerpo con la dirección del buzón (no es dato sensible), cuál de las dos direcciones falló (entrada / salida) y la fecha, y "Revise el buzón en Configuración de correo electrónico". Sin datos de clientes |
| Estados | `mailbox_incomingemailstatus` y `mailbox_outgoingemailstatus`: 0 No ejecutado, 1 Éxito, **2 Falla** (Learn, tabla Mailbox) |
| Conexiones | Dataverse y Outlook, de la cuenta de servicio |
| Inventario | 11.1 |

Antirruido: la plataforma modifica la fila del buzón a menudo; el filtro de filas hace que el flujo solo corra al pasar a falla. Un aviso por cada modificación mientras siga en falla es aceptable (el cambio de otra columna con la falla vigente también dispara); si molesta, se agrega una condición sobre el valor anterior (`DA-17`).

### 8.4 11.2 `Cloud Flow - CARP - SAL - Salud del buzon`

| Dato | Valor |
|---|---|
| ID | `CARP-SAL` |
| Disparador | Recurrencia cada 30 minutos; zona horaria de Nicaragua (UTC−6, sin horario de verano); "horario hábil" de lunes a viernes 07:00 a 19:00 (**a confirmar**, DA-03). Se implementa con una condición al inicio sobre `dayOfWeek` y la hora local (la recurrencia por horas/días del conector también sirve) |
| Comprobación 1: entrada reciente | `List rows` sobre `emails` con `directioncode eq false and createdon ge <utcNow − horasSinEntrada>`, `$top = 1`, `$select = activityid`, sin cuerpo. Si no hay filas → conteo 0 → aviso "No ingresan correos al buzón desde hace N horas". Solo en horario hábil. `Servicio` lee Email en alcance local (BU) |
| Comprobación 2: correos sin conversación | **Llama directamente** a la Custom API `sanic_carp_capi_reconciliarcorreos` (acción sin enlazar de Dataverse) con `maximo = 20` y `minutosminimos = minutosReconciliar`. No hay consulta previa: sin candidatos la API no escribe nada (PA-REC-01). Aviso si `conerror > 0` o `haymas = true`, con los conteos (`encontrados`, `registrados`, `conerror`) y **sin** el `detalle` |
| Comprobación 3: errores de envío | `List rows` sobre `emails` con `directioncode eq true and statuscode eq 8 and modifiedon ge <inicio de la corrida − ventanaErroresMin minutos>`, solo el conteo (`$count`, `$select = activityid`). Aviso si > 0 con el conteo y "Vea la vista Respuestas con error de envio". La ventana de 35 min contra 30 min de recurrencia da 5 min de superposición: puede avisar dos veces el mismo error, nunca ninguna |
| Comprobación 4: purga fallida | **Después** de que existan los trabajos de purga (13.12). `List rows` sobre los trabajos de eliminación masiva fallidos en las últimas `horasPurga` horas (`bulkdeleteoperation` con `statuscode` de falla, o `asyncoperation` de tipo eliminación masiva con estado fallido: **a verificar en Dev**). El privilegio de `SR - CARP - Servicio` para leerlos es **PENDIENTE** (`04` duda D-09): si no alcanza, se agrega a `SR - CARP - Servicio` (cambio de rol) o se vuelve a la revisión manual semanal de System Jobs. Hasta entonces la rama no se activa |
| Aviso | Un único correo por corrida con todas las comprobaciones que fallaron (asunto "CARP: alerta de salud del buzón del área"), a `sanic_carp_ev_destinatariosalertas`. Nada si todo está bien |
| Secure data | Sí, en las 3 acciones sobre `emails` y en la llamada a la API |
| Conexiones | Dataverse y Outlook, de la cuenta de servicio |
| Privilegios | `SR - CARP - Servicio` (+ Oficial por el team del sistema). Ejecutar `reconciliarcorreos` exige `prvCreatesanic_carp_tbl_conversacion` (lo tiene Servicio) |
| `habilitadaenflujos` | La API se llama con el conector de Dataverse (acción sin enlazar), que no usa `habilitadaenflujos` (decidido `false`); `IsPrivate = false` es lo que la deja visible (`03` §0.1) |

Hasta que 11.2 exista, nadie llama a la reconciliación. En Dev es aceptable; antes del pase a Test es **obligatorio** (red de seguridad de D-03).

### 8.5 11.3

No se construye (D-11, spike 2: el operador de fecha relativa sirve). Si se activara, necesitaría una sexta Custom API y escritura de Servicio sobre Cuota. Queda en el inventario como condicional.

### 8.6 Qué flujos son de la primera versión utilizable

**Ninguno.** 11.1 y 11.2 son del lote D (sección 9) y bloquean el pase a Test, no el uso en Dev.

## 9. Corte de la primera versión utilizable y lotes de construcción

### 9.1 Qué significa "utilizable"

En Dev, un **oficial** puede: recibir un correo en el buzón de prueba y verlo como conversación; tomarla; responder con adjunto desde el timeline; vincular o crear el cliente; registrar un arreglo (borrador con cuotas); generar el formato (congela); formalizar; registrar pagos. Un **jefe** puede: publicar una plantilla (con vista previa) y supervisar los correos sin respuesta y los que llegan a conversaciones finalizadas. Se acepta que falten: monitoreo, gráficos, aviso de otra conversación abierta, vistas por producto, lista de riesgo, borradores sin movimiento, errores de envío, ocultar botones estándar y pestañas múltiples.

### 9.2 Requisitos de otros grupos (lote 0)

| Grupo | Pieza | Por qué es necesaria |
|---|---|---|
| 13 | 13.4 queue `Arreglos de Pago`; 13.5 buzón compartido de prueba y su Mailbox (Dev) | Sin ellos no entran correos ni sale ninguna respuesta (D-28, `CARP-CFG-004`) |
| 10 | 10.8 `sanic_carp_ev_buzonarea` con la dirección del buzón de prueba (definición + valor) | La resuelve `RemitenteDelAreaStep` y la entrada de correo |
| 13 | 13.7 usuarios de prueba (un oficial y un jefe) en la BU, con roles y team Jefatura | Sin usuarios no hay "oficial" ni "jefe" |
| 13 | 13.8 asociar el team del sistema y el de Jefatura al perfil `CSP - CARP - Datos sensibles` | Sin el perfil la cédula y la cuenta llegan ausentes y **falla cerrado**: `CARP-GEN-012` impide generar el formato |
| 9 | 9.1 y 9.2 versión v07 y su mapeo (**28 filas**), publicada | Sin una versión Vigente se rechaza el alta de arreglos |
| 8 | Hecho (paquete, 5 API, 37 steps) | — |
| 6 | Hecho (roles, perfil) | — |
| 10 | 10.5, 10.6, 10.3, 10.4 | **No** son de la primera versión |
| 13 | 13.6 cuenta de servicio, 13.9 a 13.17 | **No** son de la primera versión (13.9 y 13.10 sí antes del pase a Test) |

### 9.3 Lotes

| Lote | Contenido | Resultado | Tamaño relativo |
|---|---|---|---|
| **0. Prerrequisitos** | 13.4, 13.5 (Dev), 10.8 (valor), 13.7, 13.8, 9.1, 9.2 | El entorno tiene datos y gente para probar | **M** (6 tareas, 2 de datos con script; casi todo administración) |
| **A. Base navegable (sin gestos)** | Extensiones G-V1, G-F1, G-F2, **G-F3 (eventos), G-F5 (decimal), G-F6 (registros por página)** · iconos (4 SVG) · vistas: 12.1, 12.2, 12.5, 12.10, 12.11, 12.12 y las de 12.13 de la primera versión (Mis conversaciones pendientes, Sin asignar, Abiertas, **Finalizadas**, del cliente, para unir, **Respuestas enviadas**, Arreglos vigentes, **Arreglos cerrados, Todos los arreglos**, Clientes, Cuotas del arreglo, Formatos generados del arreglo, Versiones de plantilla, Mapeo de la version) · formularios 12.17, 12.18 (con `OnLoad`/`OnSave`), 12.19 (una pestaña, con eventos), 12.20, 12.21 y el de Cuota (propuesto) · `app.py` + sitemap (12.23, 12.25) · `app_componentes` (con G-I1a) · mapeo de columnas de REL-03 (manual) | El oficial lee conversaciones, responde con el timeline, edita clientes y arreglos y registra pagos; el jefe edita versiones y mapeo (importación nativa desde Excel). No hay gestos de estado | **XL** (≈21 vistas + 6 formularios + 4 SVG + app = ~33 playbooks; 6 extensiones de herramienta; el timeline exige leer un formulario real) |
| **B. Gestos** | G-I1b (PrimaryControl, también en la `EnableRule`) · G-I2 · JS `comandos.js` (14 gestos, más `alCargar`/`alGuardar` de los formularios) · 12.22 · 12.24 (3 cambios `ribbon`) · regenerar el ribbon | **Primera versión utilizable de punta a punta** | **L** (1 JS de ~450 líneas, 1 playbook de importación con 13 botones, 1 extensión, prueba manual de 14 gestos con los dos perfiles) |
| **C. Completar las pantallas de la fase 1** | Vistas 12.3, 12.4, 12.6, 12.7, 12.8, 12.9 · G-V2 · G-F4 (pestañas) · G-A1 (`Privilege`) · aviso `alCargar` (12.18) · ocultar botones estándar (DA-15, G-I3) | RF-04, RF-11, RF-14, RF-17 completos | **M** (6 vistas, una extensión importante de `vista.py`, entradas de sitemap nuevas) |
| **D. Monitoreo** | 10.3, 10.4, 10.5, 10.6 · 13.6 (cuenta de servicio) · conexiones · 11.1 · 11.2 (comprobaciones 1 a 3) · 13.13 | Captura de correos vigilada; la red de seguridad (reconciliación) en marcha | **M** (2 flujos, 4 componentes de configuración, pasos manuales de la cuenta de servicio) |
| **E. Supervisión visual** | 12.15, 12.16, 12.14 y la entrada del sitemap (G-A1) | Promedios por oficial (criterio de éxito del jefe) | **S** (3 componentes manuales con guía) |
| **F. Después** | Comprobación 4 de 11.2 (cuando exista 13.12) · clonar versión (DA-13) · diálogo propio de motivos (DA-01) | — | — |

**Plazo (RNF-09, entrega 2026-10-31, sujeta a C-03).** Al 2026-10-20 quedan 11 días. El orden de los lotes es el del camino crítico: 0 → A → B es lo mínimo para entregar algo utilizable; A y B comparten tres extensiones de `formulario.py` (G-F1 a G-F3, G-F5, G-F6) que son el cuello de botella y conviene empezar por ahí, en paralelo con el lote 0 (administración). Si el plazo aprieta, el orden de recorte es: E primero (no hay promedios por oficial), luego C (sin lista de riesgo ni vistas por producto), luego D (sin monitoreo, aceptable solo en Dev). D es obligatorio antes del pase a Test y C antes del pase a Prod; ni uno ni otro está en el camino de la primera versión. Este documento no estima horas: el tamaño es relativo.

Orden de dependencias: 0 → A → B (primera versión) → C → D → E. D puede ir en paralelo a C. Nada de C, D o E bloquea que el oficial y el jefe trabajen; D bloquea el pase a Test (reconciliación) y C bloquea el pase a Prod (RF completos).

Pruebas del lote B (humo de interfaz, ya con dos usuarios no administradores): los 14 gestos en el orden del flujo de trabajo (tomar → responder → registrar arreglo → generar → desbloquear → generar → formalizar → pago → anular; descartar; finalizar y reabrir; unir; iniciar nueva; publicar; vista previa), cada uno con un caso de error (`CARP-*` en español, no técnico) y la carrera de Tomar con dos navegadores.

## 10. Decisiones abiertas (marcadas para confirmar)

DA-12 se retiró (un requerido con el control deshabilitado no bloquea el guardado); el número no se reutiliza.

| ID | Decisión | Recomendación | Tradeoff / alternativa |
|---|---|---|---|
| DA-01 | Captura del motivo y del resultado de los gestos | **M1: campos del formulario + botón** | Alternativas: diálogo HTML (`navigateTo` no devuelve valor y `Xrm` no existe en el HTML; habría que llamar por `parent.Xrm`, no soportado fuera de un formulario), página personalizada (no devuelve valor tampoco; es lienzo, sin herramienta), página generativa (devuelve valor; nueva y sin ensayo). M1 cuesta cero componentes pero la experiencia es menos guiada |
| DA-02 | Ribbon JS frente a Power Fx | **Ribbon JS** (sección 5.2) | Power Fx sería más cómodo para la visibilidad, pero no entrega el archivo ni captura entradas |
| DA-03 | Valores de monitoreo: horario hábil (lun a vie 07:00 a 19:00), `horasSinEntrada = 4`, ventana de errores 35 min, `minutosReconciliar = 10` | Los de la sección 8 | Afectan el ruido de los avisos; los fija el jefe |
| DA-04 | Ocultar al oficial las áreas del jefe | **Sí, con `Privilege Create` sobre Versión de plantilla** (G-A1, lote C); mientras tanto visibles | Es UX, no seguridad. Sin la extensión se ve todo y se ahorra una extensión de `app.py` |
| DA-05 | Dashboard y gráficos | **Diferir (lote E) y construir a mano con guía** | Herramienta `grafico.py`: más trabajo ahora, útil solo si hay más gráficos |
| DA-06 | Aviso de otra conversación abierta | **`OnLoad` + `setFormNotification`** (consulta de ≤ 3 filas, mismo cliente o mismo remitente) | Sin JS no hay forma de avisar (rollup con retraso; subgrid no aplica) |
| DA-07 | Perder la carrera de Tomar | **Texto amable + refresco, sin reintento** (humo 4) | El contrato `03` §1.3 paso 7 dice "vuelve a llamar" para mostrar `CARP-TOM-004` con el nombre; el refresco ya lo muestra. Si se prefiere el reintento, es una línea |
| DA-08 | Reasignar y vincular cliente sin botón (edición del lookup y Guardar) | **Sin botón** | El mockup tiene el botón Reasignar; el servidor ya decide (`CARP-CNV-008`). Un botón costaría una función y un playbook |
| DA-09 | Agregar el formulario de Cuota (fila 12.26) | **Sí** (sección 4.7) | Alternativa: grilla editable en la subgrilla (la herramienta no la configura) |
| DA-10 | Iconos | **4 SVG compartidos** | Un SVG por entrada y por botón son ~25 playbooks; o extender las herramientas para permitir "sin icono" |
| DA-11 | Estado de la cuota (Pendiente, Vencida, Pagada) y "días de atraso" en las listas | **No agregar columnas ni rollups** en la fase 1 | Una columna de fórmula en Cuota cambia el modelo de datos (tabla 3.4 ya construida) |
| DA-13 | Botón "Clonar versión" | **No en la primera versión**; el jefe crea la versión nueva y carga el mapeo con la **importación nativa desde Excel** (plantilla de Mapeo de celda: respeta plugins y privilegios) **[verificar en Dev]**; el script 9.2 queda para la carga inicial | La Custom API de clonar (mapeo + archivo desde la interfaz) queda para después; no está entre las cinco |
| DA-14 | `esJefe()` en el cliente | **Por nombre de rol en `userSettings.roles`**, que incluye los roles que se reciben por team (cerrado) | Es solo UX; el servidor rechaza igual (`CARP-ARR-016`, Write sobre Versión) |
| DA-15 | Ocultar botones estándar | **Solo Activar/Desactivar, en el lote C, con un `DisplayRule` que siempre da falso** (reversible, G-I3) | `HideCustomAction` no se puede quitar sin una solución nueva |
| DA-16 | Dueño de los flujos | **Cuenta de servicio, asignado a mano** en cada entorno (13.13) | `flujo.py` no asigna dueño; verificar en Test cómo queda tras importar la solución managed |
| DA-17 | Antirruido de 11.1 | Filtro de filas por falla; un aviso por modificación mientras siga en falla | Una condición sobre el valor anterior evita repeticiones |
| DA-18 | Vista "Arreglos de prestamo/de tarjeta": todos los estados | **Todos los estados** (el filtro de la columna Estado hace el resto, D-35) | Solo activos sería más corto pero esconde el historial |
| DA-19 | Subgrid de arreglos de la conversación | **Reusa** la vista "Arreglos del cliente" | Una vista propia es un playbook más |
| DA-20 | Clientes inactivos | La vista "Clientes" muestra solo activos; **ningún proceso desactiva clientes** (D-40) | Sin efecto práctico |
| DA-21 | Ver la auditoría nativa (View Audit History) | **No se tocan los roles**: queda como decisión pendiente del usuario sobre la matriz `04`; verificar en Dev con un no administrador si lo necesitan | Sin el privilegio, la pestaña de auditoría del mockup no se ve; el dato igual se audita |

## 11. Componentes que el diseño agrega y el inventario no tiene (propuesta; no suman filas hasta confirmar)

| Propuesta | Playbook | Depende de |
|---|---|---|
| 12.26 `FRM - CARP - Cuota - Principal` | `playbooks/formulario/cuota.md` | 3.4, 12.13 |
| 12.27 Iconos SVG (4 web resources) | `playbooks/web-resource/sanic_carp_wr_svg_<nombre>.md` ×4 | 1.1 |
| 12.28 Importación de las tablas de la app (`app_componentes`) | parte de `playbooks/app/controlarreglospago.md` o `playbooks/importacion/app_tablas.md` | 12.25, G-I1a |

Si se confirman, el grupo 12 pasaría de 25 a 28 filas y el total de 127 a 130.

## 12. Verificaciones que quedan para Dev (lista corta)

1. `regardingobjecttypecode` en el filtro de la vista de Email (3.3).
2. (retirada)
3. Parámetros y `classid` del control Timeline (G-F2).
4. `PrimaryControl` en `Form` y la regla `SelectionCountAtLeastOne` (G-I1b, G-I2).
5. (cerrada) `userSettings.roles` incluye los roles de team.
6. `Privilege` del sitemap oculta la entrada al Oficial (G-A1).
7. `getSaveMode() === 70` en el `OnSave` cancela el autoguardado.
8. `Xrm.Navigation.openFile` con `openMode: 2` descarga el `.xlsx` de ~44 mil caracteres en Base64 y `fileSize` en KB (5.4, gesto 6).
9. `lookupObjects` con `filters` y vista por nombre.
10. Nombre real de la vista de búsqueda rápida de fábrica de Cliente y de Arreglo.
11. Tipo y estados de los trabajos de eliminación masiva y el privilegio de lectura (8.4, comprobación 4).
12. `setDisabled(false)` sobre `sanic_motivoreapertura` en un arreglo Terminado se puede escribir y se envía (4.4); si no, diálogo de DA-01.
13. Mapeo de columnas de REL-03 (cliente y oficial) disponible y efectivo; si no, el `alCargar` del alta (4.4).
14. Importación nativa desde Excel de Mapeo de celda: respeta plugins y privilegios y carga las 28 filas (4.5, DA-13).
15. `registros_por_pagina` 60 y 28 aceptados por la plataforma (G-F6).
16. View Audit History con un no administrador (4.4, DA-21).
17. `setSubmitMode("never")` evita que los campos "gesto" viajen en el autoguardado y en la navegación (4.1).
