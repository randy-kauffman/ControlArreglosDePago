# Playbook: formulario · FRM - CARP - Conversacion - Principal

> **Tipo con 0 ciclos** (`power-platform-especificar` no tiene referencia para formulario). El formato sale de la Identidad común y de lo que lee `herramientas/construir/formulario.py` (formato ampliado de `herramientas/README.md`).

## 1. Identidad

```json
{
  "tipo_playbook": "formulario",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.18",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0, verificados contra Dev el 2026-10-09. El formulario es la fila 12.18 de `diseno/06-inventario-componentes.md` §12.3; su diseño está en `diseno/07-diseno-app-y-flujos.md` §4.1 y §4.2. La clasificación solo-sistema / de gesto / libre sale de `src/Sanic.Carp.Plugins/Dominio/Columnas.cs`.

Advertencias: etiquetas de pestaña y sección en los dos LCID, en español y sin tildes (BP-PP-197); las de los campos salen del `DisplayName`. `formulario.py` actualiza el Main existente.

## 2. Qué se crea

```json
{
  "tipo": "formulario",
  "nombre": "FRM - CARP - Conversacion - Principal",
  "tabla": "sanic_carp_tbl_conversacion",
  "descripcion": "Formulario principal de Conversación: casi todo es de solo lectura (lo escribe el sistema); el oficial vincula el cliente, el jefe reasigna, y el resultado, el motivo y el detalle se escriben aquí pero los envía el botón Finalizar (lote B). Incluye el timeline de correos y los arreglos de la conversación.",
  "solo_lectura": false,
  "pestana": {
    "1033": "General",
    "3082": "General"
  },
  "encabezado": [
    {"nombre": "statuscode", "solo_lectura": true},
    {"nombre": "sanic_oficialasignadoid", "solo_lectura": false},
    {"nombre": "sanic_sinrespuestadesde", "solo_lectura": true}
  ],
  "secciones": [
    {
      "titulo": {
        "1033": "Conversacion",
        "3082": "Conversacion"
      },
      "campos": [
        {"nombre": "sanic_nombre", "solo_lectura": true},
        {"nombre": "sanic_asunto", "solo_lectura": true},
        {"nombre": "sanic_remitente", "solo_lectura": true},
        {"nombre": "sanic_clienteid", "solo_lectura": false},
        {"nombre": "sanic_fechaprimercorreo", "solo_lectura": true},
        {"nombre": "sanic_antiguedad", "solo_lectura": true},
        {"nombre": "sanic_correonuevoenfinalizada", "solo_lectura": true}
      ]
    },
    {
      "titulo": {
        "1033": "Finalizacion",
        "3082": "Finalizacion"
      },
      "campos": [
        {"nombre": "sanic_resultado", "solo_lectura": false},
        {"nombre": "sanic_motivosinrespuesta", "solo_lectura": false},
        {"nombre": "sanic_detallesinrespuesta", "solo_lectura": false},
        {"nombre": "sanic_fechafinalizacion", "solo_lectura": true},
        {"nombre": "sanic_finalizadoporid", "solo_lectura": true},
        {"nombre": "sanic_unidaaid", "solo_lectura": true}
      ]
    },
    {
      "titulo": {
        "1033": "Tiempos",
        "3082": "Tiempos"
      },
      "campos": [
        {"nombre": "sanic_tiempoprimerarespuesta", "solo_lectura": true},
        {"nombre": "sanic_cantidadrespuestas", "solo_lectura": true},
        {"nombre": "sanic_tiempototal", "solo_lectura": true}
      ]
    },
    {
      "titulo": {
        "1033": "Correos",
        "3082": "Correos"
      },
      "timeline": {
        "alto": 12
      }
    },
    {
      "titulo": {
        "1033": "Arreglos de la conversacion",
        "3082": "Arreglos de la conversacion"
      },
      "subgrilla": {
        "tabla": "sanic_carp_tbl_arreglo",
        "lookup": "sanic_conversacionid",
        "vista": "Arreglos del cliente"
      }
    }
  ],
  "bibliotecas": [
    "sanic_carp_wr_js_comandos"
  ],
  "eventos": [
    {"evento": "onload", "biblioteca": "sanic_carp_wr_js_comandos", "funcion": "SanicCarp.Conversacion.alCargar", "pasar_contexto": true},
    {"evento": "onsave", "biblioteca": "sanic_carp_wr_js_comandos", "funcion": "SanicCarp.Conversacion.alGuardar", "pasar_contexto": true}
  ]
}
```

Decisiones y huecos:

- **Campos "gesto"** (`sanic_resultado`, `sanic_motivosinrespuesta`, `sanic_detallesinrespuesta`): editables para que el botón Finalizar (lote B) los lea. **No** se usa `setSubmitMode("never")` (Learn: con `never` la columna no se puede editar). El `OnSave` (`alGuardar`) cancela el guardado en todos los modos mientras alguno esté *dirty* y avise "Use el botón Finalizar…", salvo que lo dispare el botón (bandera `SanicCarp.Comun.levantarBandera()`).- **Reasignar** = editar `sanic_oficialasignadoid` (encabezado, editable) y guardar; **Vincular cliente** = editar `sanic_clienteid`. Sin botón (DA-08).
- **Timeline** (G-F2): `alto` 12 filas es una elección de este playbook (**hueco**: `diseno/07` no fija la altura). "Solo correos, sin notas": la tabla no tiene Notas (`notas: false`), así que no hay notas; limitar las actividades a correo **no está en el XML** del control y se configura a mano en el diseñador (pendiente, `herramientas/README.md`). Responder con adjuntos es el botón nativo del timeline.
- **Subgrilla de arreglos**: reutiliza la vista "Arreglos del cliente" (DA-19) por el lookup `sanic_conversacionid` (REL-03). `registros_por_pagina` por omisión (10): `diseno/07` no pide otro valor.
- **Aviso de otra conversación abierta (RF-14, DA-06; lote C).** `OnLoad` → `SanicCarp.Conversacion.alCargar`: si la conversación está Abierta, consulta hasta 3 conversaciones Abiertas, con otro id, del mismo cliente o del mismo remitente (`Xrm.WebApi.retrieveMultipleRecords`, solo lectura) y muestra `setFormNotification` INFO (id `sanic_carp_otra_conversacion_abierta`) con el número y el asunto, y la sugerencia de usar Unir a otra conversación. Si la consulta falla no avisa y el formulario abre igual. No se oculta nada por estado.
- `sanic_nombre` va ro (autonumérica, no requerida).

## 3. Precondiciones

| # | Qué tiene que existir | Cómo se comprueba |
|---|---|---|
| 1 | La tabla y las 19 columnas del formulario (16 en secciones y 3 en el encabezado, incluida `statuscode`), con etiqueta en 1033 y 3082 | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_conversacion')/Attributes` |
| 2 | La tabla tiene `HasActivities` = true | `GET EntityDefinitions(...)?$select=HasActivities` |
| 3 | La tabla figura en la solución con `rootcomponentbehavior` 0 | `GET solutioncomponents` (`componenttype` 1) |
| 4 | La vista **Arreglos del cliente** sobre `sanic_carp_tbl_arreglo`, exactamente una | `GET savedqueries?$filter=name eq 'Arreglos del cliente' and returnedtypecode eq 'sanic_carp_tbl_arreglo'` |
| 5 | La relación con lookup `sanic_conversacionid` en Arreglo (REL-03) | `GET .../OneToManyRelationships` |
| 6 | El web resource `sanic_carp_wr_js_comandos` existe y es JavaScript (tipo 3) | `GET webresourceset?$filter=name eq 'sanic_carp_wr_js_comandos'` |

Si alguna falla: `bloqueado`, no se escribe nada.

## 4. Cómo se construye

```
python3 herramientas/construir/formulario.py playbooks/formulario/conversacion.md
```

Orden: después del web resource y de la vista de la subgrilla.

## 5. Verificación

```
python3 herramientas/construir/formulario.py playbooks/formulario/conversacion.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET systemforms?$filter=objecttypecode eq 'sanic_carp_tbl_conversacion' and type eq 2` | un solo Main · `name` y `description` los de la sección 2 · activo · no managed |
| 2 | `formxml` | igual al generado, etiquetas en los dos idiomas |
| 3 | Estructura | 1 pestaña, 5 secciones, 16 campos en secciones, 1 subgrilla, 3 en el encabezado, 1 biblioteca, 2 manejadores |
| 4 | Scripts | `onload` → `SanicCarp.Conversacion.alCargar` y `onsave` → `SanicCarp.Conversacion.alGuardar`, ambos con `passExecutionContext` true |
| 5 | Pertenencia | la tabla figura en la solución con `rootcomponentbehavior` 0 |
| 6 | Segunda ejecución | `ya_existia` |

Fuera de la herramienta, en Dev con un usuario no administrador (`diseno/07` §12 puntos 3, 7 y 17): el timeline aparece y muestra correos; escribir un resultado o motivo y guardar (manual, autoguardado y al navegar) no guarda y muestra el aviso; los campos gesto se pueden escribir.

## 6. Si ya existe

Mismas comprobaciones que la sección 5. Si pasan: `ya_existia`; si no: `difiere` (la herramienta corrige con `PATCH` y publica la tabla; un Main managed no se toca).

## 7. Reversa

Se restituye el `formxml` anterior desde la solución exportada. Reversible mientras no llegue a Test o Prod.

## 8. Fuera de alcance

Botones de gesto (lote B), aviso de otra conversación abierta (lote C), configuración fina del timeline (manual), vistas (`playbooks/vista/`), el JS (`playbooks/web-resource/sanic_carp_wr_js_comandos.md`).
