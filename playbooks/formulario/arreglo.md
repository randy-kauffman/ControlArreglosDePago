# Playbook: formulario · FRM - CARP - Arreglo - Principal

> **Tipo con 0 ciclos** (`power-platform-especificar` no tiene referencia para formulario). El formato sale de la Identidad común y de lo que lee `herramientas/construir/formulario.py` (formato ampliado de `herramientas/README.md`).

## 1. Identidad

```json
{
  "tipo_playbook": "formulario",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.19",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0, verificados contra Dev el 2026-10-09. El formulario es la fila 12.19 de `diseno/06-inventario-componentes.md` §12.3; su diseño está en `diseno/07-diseno-app-y-flujos.md` §4.1 y §4.4. Solo-sistema / gesto / datos del acuerdo salen de `src/Sanic.Carp.Plugins/Dominio/Columnas.cs`.

Advertencias: etiquetas de pestaña y sección en los dos LCID, en español y sin tildes (BP-PP-197); las de los campos salen del `DisplayName`. `formulario.py` actualiza el Main existente.

## 2. Qué se crea

```json
{
  "tipo": "formulario",
  "nombre": "FRM - CARP - Arreglo - Principal",
  "tabla": "sanic_carp_tbl_arreglo",
  "descripcion": "Formulario principal de Arreglo (cuatro pestañas: General, Cuotas, Formatos y Cierre): datos del acuerdo, montos, cuotas, formatos generados y los campos de motivo de descarte, anulación y reapertura, que envían los botones del lote B. Los campos calculados por el sistema son de solo lectura.",
  "solo_lectura": false,
  "encabezado": [
    {"nombre": "statuscode", "solo_lectura": true},
    {"nombre": "sanic_numeroarreglo", "solo_lectura": true},
    {"nombre": "sanic_fechacongelamiento", "solo_lectura": true}
  ],
  "pestanas": [
   {
    "titulo": {"1033": "General", "3082": "General"},
    "secciones": [
    {
      "titulo": {
        "1033": "Acuerdo",
        "3082": "Acuerdo"
      },
      "campos": [
        {"nombre": "sanic_nombre", "solo_lectura": true},
        {"nombre": "sanic_clienteid", "solo_lectura": false},
        {"nombre": "sanic_conversacionid", "solo_lectura": false},
        {"nombre": "sanic_producto", "solo_lectura": false},
        {"nombre": "sanic_numeroproducto", "solo_lectura": false},
        {"nombre": "sanic_tipo", "solo_lectura": false},
        {"nombre": "sanic_moneda", "solo_lectura": false},
        {"nombre": "sanic_canalorigen", "solo_lectura": false},
        {"nombre": "sanic_oficialid", "solo_lectura": false},
        {"nombre": "sanic_cuentadebitar", "solo_lectura": false},
        {"nombre": "sanic_fechaformalizacion", "solo_lectura": false},
        {"nombre": "sanic_comentarios", "solo_lectura": false}
      ]
    },
    {
      "titulo": {
        "1033": "Montos y plazo",
        "3082": "Montos y plazo"
      },
      "campos": [
        {"nombre": "sanic_saldoinicial", "solo_lectura": false},
        {"nombre": "sanic_montoabonado", "solo_lectura": false},
        {"nombre": "sanic_saldoremanente", "solo_lectura": true},
        {"nombre": "sanic_plazo", "solo_lectura": false},
        {"nombre": "sanic_cuotamensual", "solo_lectura": false},
        {"nombre": "sanic_totalpagar", "solo_lectura": true},
        {"nombre": "sanic_fechainicio", "solo_lectura": false},
        {"nombre": "sanic_fechafin", "solo_lectura": true}
      ]
    },
    {
      "titulo": {
        "1033": "Plantilla y numeracion",
        "3082": "Plantilla y numeracion"
      },
      "campos": [
        {"nombre": "sanic_versionplantillaid", "solo_lectura": true},
        {"nombre": "sanic_numeroprovisional", "solo_lectura": true}
      ]
    },
    {
      "titulo": {
        "1033": "Cliente al congelar",
        "3082": "Cliente al congelar"
      },
      "campos": [
        {"nombre": "sanic_instantaneanombre", "solo_lectura": true},
        {"nombre": "sanic_instantaneacedula", "solo_lectura": true},
        {"nombre": "sanic_instantaneacorreo", "solo_lectura": true},
        {"nombre": "sanic_instantaneacelular", "solo_lectura": true}
      ]
    }
    ]
   },
   {
    "titulo": {"1033": "Cuotas", "3082": "Cuotas"},
    "secciones": [
    {
      "titulo": {
        "1033": "Cuotas",
        "3082": "Cuotas"
      },
      "subgrilla": {
        "tabla": "sanic_carp_tbl_cuota",
        "lookup": "sanic_arregloid",
        "vista": "Cuotas del arreglo",
        "registros_por_pagina": 60
      }
    }
    ]
   },
   {
    "titulo": {"1033": "Formatos", "3082": "Formatos"},
    "secciones": [
    {
      "titulo": {
        "1033": "Formatos generados",
        "3082": "Formatos generados"
      },
      "subgrilla": {
        "tabla": "sanic_carp_tbl_generacionformato",
        "lookup": "sanic_arregloid",
        "vista": "Formatos generados del arreglo"
      }
    }
    ]
   },
   {
    "titulo": {"1033": "Cierre", "3082": "Cierre"},
    "secciones": [
    {
      "titulo": {
        "1033": "Formalizacion",
        "3082": "Formalizacion"
      },
      "campos": [
        {"nombre": "sanic_fechaformalizado", "solo_lectura": true},
        {"nombre": "sanic_formalizadoporid", "solo_lectura": true},
        {"nombre": "sanic_fechatermino", "solo_lectura": true}
      ]
    },
    {
      "titulo": {
        "1033": "Descarte",
        "3082": "Descarte"
      },
      "campos": [
        {"nombre": "sanic_motivodescarte", "solo_lectura": false},
        {"nombre": "sanic_fechadescarte", "solo_lectura": true},
        {"nombre": "sanic_descartadoporid", "solo_lectura": true}
      ]
    },
    {
      "titulo": {
        "1033": "Anulacion",
        "3082": "Anulacion"
      },
      "campos": [
        {"nombre": "sanic_motivoanulacion", "solo_lectura": false},
        {"nombre": "sanic_fechaanulacion", "solo_lectura": true},
        {"nombre": "sanic_anuladoporid", "solo_lectura": true}
      ]
    },
    {
      "titulo": {
        "1033": "Reapertura",
        "3082": "Reapertura"
      },
      "campos": [
        {"nombre": "sanic_motivoreapertura", "solo_lectura": false},
        {"nombre": "sanic_fechareapertura", "solo_lectura": true},
        {"nombre": "sanic_reabiertoporid", "solo_lectura": true},
        {"nombre": "sanic_cantidadreaperturas", "solo_lectura": true}
      ]
    }
    ]
   }
  ],
  "bibliotecas": [
    "sanic_carp_wr_js_comandos"
  ],
  "eventos": [
    {"evento": "onload", "biblioteca": "sanic_carp_wr_js_comandos", "funcion": "SanicCarp.Arreglo.alCargar", "pasar_contexto": true},
    {"evento": "onsave", "biblioteca": "sanic_carp_wr_js_comandos", "funcion": "SanicCarp.Arreglo.alGuardar", "pasar_contexto": true}
  ]
}
```

Decisiones y huecos:

- **Cuatro pestañas** (lote C, G-F4; `diseno/07` §4.4): General (Acuerdo, Montos y plazo, Plantilla y numeracion, Cliente al congelar), Cuotas, Formatos y Cierre (Formalizacion, Descarte, Anulacion, Reapertura). Las 10 secciones y su orden no cambian respecto del lote A (que tenía una sola pestaña "General"); solo se reemplazó `pestana` + `secciones` por `pestanas`. Los títulos de pestaña "Cuotas" y "Formatos" son los de `diseno/07` §4.4.
- **Subgrilla de cuotas con `registros_por_pagina` 60** (G-F6; el plazo máximo es 60). El 60 está dentro del rango 2 a 250 que valida la herramienta; que la plataforma lo acepte en Dev es `diseno/07` §12 punto 15. La de formatos generados queda en el 10 por omisión: **hueco**, `diseno/07` no fija un valor.
- Los montos (`sanic_saldoinicial`, `sanic_montoabonado`, `sanic_saldoremanente`, `sanic_totalpagar`, `sanic_cuotamensual`) son `DecimalType` o Money según la tabla; el classid de decimal se agregó a `formulario.py` (G-F5). `sanic_fechafin` y `sanic_saldoremanente` los calcula el plugin: solo lectura (un requerido deshabilitado no bloquea el guardado).
- **Campos gesto**: `sanic_motivodescarte`, `sanic_motivoanulacion`, `sanic_motivoreapertura` (editables, sin `setSubmitMode`: Learn dice que con `never` la columna no se puede editar). El `OnSave` (`alGuardar`) cancela el guardado en todos los modos mientras alguno esté *dirty* y avisa "Use el botón…", salvo que lo dispare un botón de gesto (bandera `SanicCarp.Comun.levantarBandera()`); los envían los botones del lote B.
- `alCargar` habilita `sanic_motivoreapertura` para el jefe en un Terminado o Vigente congelado (`diseno/07` §4.4 punto 2; `diseno/07` §12 punto 12 a verificar en Dev). El respaldo del alta desde una conversación (heredar cliente y oficial) **no** está en el JS: primera opción, el mapeo de columnas de REL-03, paso manual del maker; `diseno/07` §12 puntos 13.
- Los datos del acuerdo siguen editables con el arreglo congelado: el servidor responde `CARP-ARR-011`. `sanic_cuentadebitar` y `sanic_instantaneacedula` se ven solo con el perfil de columna.
- `sanic_saldoremanente`, `sanic_totalpagar` y `sanic_fechafin` están en `Columnas.cs` como solo-sistema o datos del acuerdo: se muestran en solo lectura por decisión del diseño.

## 3. Precondiciones

| # | Qué tiene que existir | Cómo se comprueba |
|---|---|---|
| 1 | La tabla y las 42 columnas del formulario (3 del encabezado y 39 en secciones), con etiqueta en 1033 y 3082, y con control conocido para cada tipo (incluido el decimal) | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_arreglo')/Attributes` |
| 2 | La tabla figura en la solución con `rootcomponentbehavior` 0 | `GET solutioncomponents` (`componenttype` 1) |
| 3 | Vistas **Cuotas del arreglo** (sobre `sanic_carp_tbl_cuota`) y **Formatos generados del arreglo** (sobre `sanic_carp_tbl_generacionformato`), una cada una | `GET savedqueries?$filter=name eq '...' and returnedtypecode eq '...'` |
| 4 | Relaciones `sanic_carp_arreglo_cuota` y `sanic_carp_arreglo_generacionformato` (lookup `sanic_arregloid`) | `GET .../OneToManyRelationships` |
| 5 | El web resource `sanic_carp_wr_js_comandos` existe y es JavaScript | `GET webresourceset?...` |

Si alguna falla: `bloqueado`, no se escribe nada.

## 4. Cómo se construye

```
python3 herramientas/construir/formulario.py playbooks/formulario/arreglo.md
```

Orden: después del web resource y de las dos vistas.

## 5. Verificación

```
python3 herramientas/construir/formulario.py playbooks/formulario/arreglo.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET systemforms?$filter=objecttypecode eq 'sanic_carp_tbl_arreglo' and type eq 2` | un solo Main · `name` y `description` los de la sección 2 · activo · no managed |
| 2 | `formxml` | igual al generado, etiquetas en los dos idiomas |
| 3 | Estructura | 4 pestañas, 10 secciones, 39 campos en secciones, 2 subgrillas (la de cuotas con `RecordsPerPage` 60), 3 en el encabezado, 1 biblioteca, 2 manejadores |
| 4 | Scripts | `onload` → `SanicCarp.Arreglo.alCargar`, `onsave` → `SanicCarp.Arreglo.alGuardar`, con `passExecutionContext` true |
| 5 | Pertenencia | la tabla figura en la solución con `rootcomponentbehavior` 0 |
| 6 | Segunda ejecución | `ya_existia` |

Fuera de la herramienta, en Dev con un no administrador: las 60 cuotas se ven sin paginar; escribir un motivo y guardar no guarda y muestra el aviso (sin `CARP-ARR-024`); el jefe puede escribir el motivo de reapertura en un Terminado.

## 6. Si ya existe

Mismas comprobaciones que la sección 5. Si pasan: `ya_existia`; si no: `difiere` (corrige con `PATCH` y publica; un Main managed no se toca).

## 7. Reversa

Se restituye el `formxml` anterior desde la solución exportada. Reversible mientras no llegue a Test o Prod.

## 8. Fuera de alcance

Los botones de gesto (lote B), el respaldo de herencia de cliente y oficial en el alta (solo si el mapeo de REL-03 falla), la historia de auditoría (DA-21), las vistas y el JS.
