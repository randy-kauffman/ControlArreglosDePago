# Playbook: app · MDA - CARP - Control de Arreglos de Pago

> **Tipo con 0 ciclos** (`power-platform-especificar` no tiene referencia para app model-driven). El formato sale de la Identidad común y de lo que lee `herramientas/construir/app.py` (formato ampliado de `herramientas/README.md`: `privilegios` por entrada).

## 1. Identidad

```json
{
  "tipo_playbook": "app",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.23+12.25",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0, verificados contra Dev el 2026-10-09. El sitemap y la app son las filas 12.23 y 12.25 de `diseno/06-inventario-componentes.md` §12.4; su diseño está en `diseno/07-diseno-app-y-flujos.md` §7.1 y §7.2, con DA-04 (privilegios) y G-A1.

Advertencias: los títulos de sitemap van una vez por idioma de la Identidad y sin tildes (BP-PP-197). Desde el 2026-10-28 (DA-22, `diseno/07` §13) 1033 lleva el título en inglés y 3082 en español; antes llevaban el mismo texto en español. Cada entrada tiene su icono SVG propio (`diseno/07` §13), no los cuatro compartidos de DA-10. Cada entrada con vista se abre por `Url` con el `viewid` (la herramienta resuelve el id por nombre y tabla).

## 2. Qué se crea

```json
{
  "tipo": "app",
  "nombre": "MDA - CARP - Control de Arreglos de Pago",
  "uniquename": "sanic_carp_mda_controlarreglospago",
  "descripcion": "Una sola app para el oficial y el jefe del área de arreglos de pago. Las áreas de supervisión y de administración del formato las ve solo quien puede crear versiones de plantilla (el jefe); es una ayuda de interfaz, la seguridad real son los plugins y los privilegios.",
  "icono": "sanic_carp_wr_svg_app",
  "sitemap": "sanic_carp_sm_controlarreglospago",
  "roles": [
    "SR - CARP - Oficial",
    "SR - CARP - Jefe"
  ],
  "tablas": [
    "sanic_carp_tbl_cliente",
    "sanic_carp_tbl_conversacion",
    "sanic_carp_tbl_arreglo",
    "sanic_carp_tbl_cuota",
    "sanic_carp_tbl_generacionformato",
    "sanic_carp_tbl_versionplantilla",
    "sanic_carp_tbl_mapeocelda",
    "email"
  ],
  "areas": [
    {
      "titulo": {"1033": "Conversations", "3082": "Conversaciones"},
      "grupos": [
        {
          "titulo": {"1033": "Inbox", "3082": "Bandeja"},
          "entradas": [
            {"id": "mispendientes", "titulo": {"1033": "My pending conversations", "3082": "Mis conversaciones pendientes"}, "tabla": "sanic_carp_tbl_conversacion", "vista": "Mis conversaciones pendientes", "icono": "sanic_carp_wr_svg_conv_mias"},
            {"id": "sinasignar", "titulo": {"1033": "Unassigned conversations", "3082": "Conversaciones sin asignar"}, "tabla": "sanic_carp_tbl_conversacion", "vista": "Conversaciones sin asignar", "icono": "sanic_carp_wr_svg_conv_sinasignar"},
            {"id": "abiertas", "titulo": {"1033": "Open conversations", "3082": "Conversaciones abiertas"}, "tabla": "sanic_carp_tbl_conversacion", "vista": "Conversaciones abiertas", "icono": "sanic_carp_wr_svg_conv_abiertas"},
            {"id": "llegadasafinalizadas", "titulo": {"1033": "New emails in closed conversations", "3082": "Correos llegados a finalizadas"}, "tabla": "sanic_carp_tbl_conversacion", "vista": "Correos llegados a conversaciones finalizadas", "icono": "sanic_carp_wr_svg_conv_correonuevo"},
            {"id": "finalizadas", "titulo": {"1033": "Closed conversations", "3082": "Conversaciones finalizadas"}, "tabla": "sanic_carp_tbl_conversacion", "vista": "Conversaciones finalizadas", "icono": "sanic_carp_wr_svg_conv_finalizadas"}
          ]
        }
      ]
    },
    {
      "titulo": {"1033": "Customers and agreements", "3082": "Clientes y arreglos"},
      "grupos": [
        {
          "titulo": {"1033": "Customers", "3082": "Clientes"},
          "entradas": [
            {"id": "clientes", "titulo": {"1033": "Customers", "3082": "Clientes"}, "tabla": "sanic_carp_tbl_cliente", "vista": "Clientes", "icono": "sanic_carp_wr_svg_clientes"}
          ]
        },
        {
          "titulo": {"1033": "Agreements", "3082": "Arreglos"},
          "entradas": [
            {"id": "borradores", "titulo": {"1033": "Drafts", "3082": "Borradores"}, "tabla": "sanic_carp_tbl_arreglo", "vista": "Borradores", "icono": "sanic_carp_wr_svg_arr_borradores"},
            {"id": "vigentes", "titulo": {"1033": "Active agreements", "3082": "Arreglos vigentes"}, "tabla": "sanic_carp_tbl_arreglo", "vista": "Arreglos vigentes", "icono": "sanic_carp_wr_svg_arr_vigentes"},
            {"id": "enriesgo", "titulo": {"1033": "At-risk agreements", "3082": "Arreglos en riesgo"}, "tabla": "sanic_carp_tbl_arreglo", "vista": "Arreglos en riesgo", "icono": "sanic_carp_wr_svg_arr_enriesgo"},
            {"id": "prestamo", "titulo": {"1033": "Loan agreements", "3082": "Arreglos de prestamo"}, "tabla": "sanic_carp_tbl_arreglo", "vista": "Arreglos de prestamo", "icono": "sanic_carp_wr_svg_arr_prestamo"},
            {"id": "tarjeta", "titulo": {"1033": "Card agreements", "3082": "Arreglos de tarjeta"}, "tabla": "sanic_carp_tbl_arreglo", "vista": "Arreglos de tarjeta", "icono": "sanic_carp_wr_svg_arr_tarjeta"},
            {"id": "cerrados", "titulo": {"1033": "Closed agreements", "3082": "Arreglos cerrados"}, "tabla": "sanic_carp_tbl_arreglo", "vista": "Arreglos cerrados", "icono": "sanic_carp_wr_svg_arr_cerrados"},
            {"id": "todos", "titulo": {"1033": "All agreements", "3082": "Todos los arreglos"}, "tabla": "sanic_carp_tbl_arreglo", "vista": "Todos los arreglos", "icono": "sanic_carp_wr_svg_arr_todos"}
          ]
        }
      ]
    },
    {
      "titulo": {"1033": "Supervision", "3082": "Supervision"},
      "grupos": [
        {
          "titulo": {"1033": "Follow-up", "3082": "Seguimiento"},
          "entradas": [
            {"id": "sinrespuesta", "titulo": {"1033": "Unanswered emails", "3082": "Correos sin respuesta"}, "tabla": "sanic_carp_tbl_conversacion", "vista": "Correos sin respuesta", "icono": "sanic_carp_wr_svg_sup_sinrespuesta", "privilegios": [{"tabla": "sanic_carp_tbl_versionplantilla", "privilegio": "Create"}]},
            {"id": "cierres", "titulo": {"1033": "Closed without reply", "3082": "Finalizadas sin respuesta"}, "tabla": "sanic_carp_tbl_conversacion", "vista": "Finalizadas sin respuesta", "icono": "sanic_carp_wr_svg_sup_finsinrespuesta", "privilegios": [{"tabla": "sanic_carp_tbl_versionplantilla", "privilegio": "Create"}]},
            {"id": "errores", "titulo": {"1033": "Replies with send errors", "3082": "Respuestas con error de envio"}, "tabla": "email", "vista": "Respuestas con error de envio", "icono": "sanic_carp_wr_svg_sup_errorenvio", "privilegios": [{"tabla": "sanic_carp_tbl_versionplantilla", "privilegio": "Create"}]},
            {"id": "respenviadas", "titulo": {"1033": "Sent replies", "3082": "Respuestas enviadas"}, "tabla": "email", "vista": "Respuestas enviadas", "icono": "sanic_carp_wr_svg_sup_enviadas", "privilegios": [{"tabla": "sanic_carp_tbl_versionplantilla", "privilegio": "Create"}]},
            {"id": "borradoressinmov", "titulo": {"1033": "Stale drafts", "3082": "Borradores sin movimiento"}, "tabla": "sanic_carp_tbl_arreglo", "vista": "Borradores sin movimiento", "icono": "sanic_carp_wr_svg_sup_borradorquieto", "privilegios": [{"tabla": "sanic_carp_tbl_versionplantilla", "privilegio": "Create"}]},
            {"id": "panel", "titulo": {"1033": "Supervision dashboard", "3082": "Panel de supervision"}, "dashboard": "DASH - CARP - Supervision", "icono": "sanic_carp_wr_svg_sup_panel", "privilegios": [{"tabla": "sanic_carp_tbl_versionplantilla", "privilegio": "Create"}]}
          ]
        }
      ]
    },
    {
      "titulo": {"1033": "Form administration", "3082": "Administracion del formato"},
      "grupos": [
        {
          "titulo": {"1033": "Template", "3082": "Plantilla"},
          "entradas": [
            {"id": "plantillas", "titulo": {"1033": "Template versions", "3082": "Versiones de plantilla"}, "tabla": "sanic_carp_tbl_versionplantilla", "vista": "Versiones de plantilla", "icono": "sanic_carp_wr_svg_plantillas", "privilegios": [{"tabla": "sanic_carp_tbl_versionplantilla", "privilegio": "Create"}]}
          ]
        }
      ]
    }
  ]
}
```

### Privilegios por entrada (DA-04, G-A1)

Las siete entradas del jefe (lote A: `sinrespuesta`, `respenviadas`, `plantillas`; lote C: `cierres`, `errores`, `borradoressinmov`) llevan `Privilege Entity="sanic_carp_tbl_versionplantilla" Privilege="Create"`: solo `SR - CARP - Jefe` tiene Create sobre Versión de plantilla (`diseno/04-matriz-privilegios.md`), así que el Oficial no las ve. Las del Oficial no llevan privilegio (las ven los dos roles). Es solo interfaz: un Oficial que abra la URL de una vista del jefe igual la ve si tiene Read sobre la tabla; la barrera real son los plugins y los privilegios.

### Entradas diferidas

Las seis entradas del lote C (`enriesgo`, `prestamo`, `tarjeta`, `cierres`, `errores`, `borradoressinmov`) ya están en el JSON (sus vistas se construyeron en el lote C). La del lote E (`panel`) se agregó el 2026-10-20, cuando se construyó el dashboard; `app.py` también lo agrega como componente de la app (tipo 60). Ya no queda ninguna diferida:

| Lote | Área › grupo | `id` | Tabla | Vista o dashboard | Ícono | Privilegio |
|---|---|---|---|---|---|---|
| E | Supervision › Seguimiento | `panel` | (dashboard) | Panel de supervision (`dashboard`, sin `tabla`) | lista | Create versión |

Decisión de este playbook: `app.py` bloquea si una entrada nombra una vista que no existe, así que cada lote suma sus entradas cuando construye sus vistas (`diseno/07` §9.3). El orden de entradas dentro de cada grupo es el de la tabla de `diseno/07` §7.2. Los títulos de las entradas `llegadasafinalizadas` y `sinrespuesta` se acortaron o repiten el nombre de la vista; **hueco**: `diseno/07` no fija los títulos de entrada, solo la vista que abren. Se usó el nombre de la vista salvo `llegadasafinalizadas` ("Correos llegados a finalizadas", por largo).

## 3. Precondiciones

| # | Qué tiene que existir | Cómo se comprueba |
|---|---|---|
| 1 | Los cuatro iconos `sanic_carp_wr_svg_app`, `_lista`, `_alerta` (y `_accion`, aunque aún no se use) | `GET webresourceset?$filter=name eq '...'` |
| 2 | Cada vista nombrada en una entrada, exactamente una por tabla: Mis conversaciones pendientes, Conversaciones sin asignar, Conversaciones abiertas, Correos llegados a conversaciones finalizadas, Conversaciones finalizadas, Correos sin respuesta (todas sobre `sanic_carp_tbl_conversacion`); Clientes (`sanic_carp_tbl_cliente`); Borradores, Arreglos vigentes, Arreglos en riesgo, Arreglos de prestamo, Arreglos de tarjeta, Arreglos cerrados, Todos los arreglos, Borradores sin movimiento (`sanic_carp_tbl_arreglo`); Finalizadas sin respuesta (`sanic_carp_tbl_conversacion`); Respuestas enviadas y Respuestas con error de envio (`email`); Versiones de plantilla (`sanic_carp_tbl_versionplantilla`) | `GET savedqueries?$filter=name eq '...' and returnedtypecode eq '...'` |
| 3 | Los roles `SR - CARP - Oficial` y `SR - CARP - Jefe` | `GET roles?$filter=name eq '...'` |
| 4 | Las 8 tablas existen en el entorno (7 propias y `email`) | `GET EntityDefinitions(LogicalName='...')` |
| 5 | Idiomas 1033 y 3082 provisionados y solución no managed | `comprobar_solucion_e_idioma` |
| 6 | Los formularios de las 7 tablas ya construidos (para que la app abra el formulario nuevo) | `playbooks/formulario/*.md` |

Si alguna falla: `bloqueado`, no se escribe nada.

## 4. Cómo se construye

Herramienta del proyecto: `herramientas/construir/app.py`. Hace la secuencia sitemap → app → `AddAppComponents` (sitemap y cada tabla, **incluida `email`**) → roles → `PublishXml` → `ValidateApp`.

```
python3 herramientas/construir/app.py playbooks/app/controlarreglospago.md
```

Orden completo de `diseno/07` §7.1: `app.py` → botones del ribbon (lote B, `importar_solucion.py`) → regenerar la metadata del ribbon (`&ribbondebug=true`, Command Checker) → `app.py --solo-verificar`. La importación `app_componentes` de `importar_solucion.py` ya no hace falta para agregar las tablas: `app.py` las agrega (la fila 12.28 de `diseno/07` §11 queda cubierta por esta sección).

## 5. Verificación

```
python3 herramientas/construir/app.py playbooks/app/controlarreglospago.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `RetrieveUnpublishedMultiple` de `appmodules` por `uniquename` | una app · nombre y descripción los de la sección 2 · `clienttype` 4 · no managed |
| 2 | `GET sitemaps?$filter=sitemapnameunique eq 'sanic_carp_sm_controlarreglospago'` | un sitemap · `isappaware` true · `sitemapxml` igual al generado, con un título por idioma y los `Privilege` de las entradas del jefe |
| 3 | Tablas de la app | las 8 del playbook figuran como componentes de la app (la herramienta cuenta lo que quedó, no lo que pidió) |
| 4 | Roles | `SR - CARP - Oficial` y `SR - CARP - Jefe` asociados |
| 5 | `ValidateApp` | sin issues de tipo Error |
| 6 | Segunda ejecución | `ya_existia` |

Fuera de la herramienta, en Dev con un usuario no administrador de cada rol (`diseno/07` §12 punto 6): el Oficial no ve las áreas Supervisión ni Administración del formato; el Jefe las ve. El patrón de etiquetas por idioma del sitemap no se pudo verificar contra un sitemap real: mirarlo una vez en la app.

## 6. Si ya existe

Mismas comprobaciones que la sección 5. Si pasan: `ya_existia`; si no: `difiere` con la diferencia exacta y sin tocar nada fuera de lo que `app.py` corrige.

## 7. Reversa

Se despublica y se elimina la app (`DELETE appmodules(<id>)`) y su sitemap. Sin datos asociados: es reversible mientras no llegue a Test o Prod.

## 8. Fuera de alcance

Los botones del ribbon (lote B, `playbooks/importacion/comandos.md`), el dashboard y sus gráficos (lote E), las entradas diferidas de la tabla de arriba, las vistas, los formularios, el mapeo de columnas de REL-03 (paso manual).
