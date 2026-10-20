# Playbook: formulario · FRM - CARP - Version de plantilla - Principal

> **Tipo con 0 ciclos** (`power-platform-especificar` no tiene referencia para formulario). El formato sale de la Identidad común y de lo que lee `herramientas/construir/formulario.py` (formato ampliado de `herramientas/README.md`).

## 1. Identidad

```json
{
  "tipo_playbook": "formulario",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.20",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0, verificados contra Dev el 2026-10-09. El formulario es la fila 12.20 de `diseno/06-inventario-componentes.md` §12.3; su diseño está en `diseno/07-diseno-app-y-flujos.md` §4.1 y §4.5, y la regla del mapeo de 28 filas en §0 punto 2.

Advertencias: etiquetas en los dos LCID, en español y sin tildes (BP-PP-197). `formulario.py` actualiza el Main existente.

## 2. Qué se crea

```json
{
  "tipo": "formulario",
  "nombre": "FRM - CARP - Version de plantilla - Principal",
  "tabla": "sanic_carp_tbl_versionplantilla",
  "descripcion": "Formulario principal de Versión de plantilla: número de versión, archivo .xlsx, capacidad y el mapeo de celdas. El estado es de solo lectura: Publicar es un botón del lote B que envía solo ese campo.",
  "solo_lectura": false,
  "pestana": {
    "1033": "General",
    "3082": "General"
  },
  "encabezado": [
    {"nombre": "sanic_estado", "solo_lectura": true},
    {"nombre": "sanic_capacidad", "solo_lectura": false},
    {"nombre": "sanic_fechaaprobacion", "solo_lectura": true}
  ],
  "secciones": [
    {
      "titulo": {
        "1033": "Version",
        "3082": "Version"
      },
      "campos": [
        {"nombre": "sanic_nombre", "solo_lectura": false},
        {"nombre": "sanic_archivo", "solo_lectura": false},
        {"nombre": "sanic_huella", "solo_lectura": true},
        {"nombre": "sanic_aprobadoporid", "solo_lectura": true}
      ]
    },
    {
      "titulo": {
        "1033": "Mapeo de la version",
        "3082": "Mapeo de la version"
      },
      "subgrilla": {
        "tabla": "sanic_carp_tbl_mapeocelda",
        "lookup": "sanic_versionplantillaid",
        "vista": "Mapeo de la version",
        "registros_por_pagina": 28
      }
    }
  ]
}
```

Decisiones y huecos:

- `sanic_nombre` es editable (patrón `^v[0-9]{2,3}$`, `CARP-VER-012`); la valida el plugin.
- `sanic_archivo` es una columna de archivo (5 MB): usa el control de archivo (`FileType`), ya conocido por la herramienta.
- **Subgrilla del mapeo con `registros_por_pagina` 28**: el mapeo v07 tiene 28 filas (`diseno/07` §0 punto 2). Si el mapeo de otra versión es mayor, se verá paginado: **hueco**, el diseño fija 28 solo para v07.
- Sin `bibliotecas` ni `eventos`: `diseno/07` §4.5 no pide `OnSave` en este formulario. Publicar y Vista previa son del lote B.
- Cargar el mapeo con la importación nativa desde Excel es un procedimiento, no un componente (DA-13, `diseno/07` §12 punto 14).

## 3. Precondiciones

| # | Qué tiene que existir | Cómo se comprueba |
|---|---|---|
| 1 | La tabla y las 7 columnas del formulario (3 del encabezado y 4 en la sección 1), con etiqueta en 1033 y 3082 | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_versionplantilla')/Attributes` |
| 2 | La tabla figura en la solución con `rootcomponentbehavior` 0 | `GET solutioncomponents` |
| 3 | La vista **Mapeo de la version** sobre `sanic_carp_tbl_mapeocelda`, exactamente una | `GET savedqueries?$filter=name eq 'Mapeo de la version' and returnedtypecode eq 'sanic_carp_tbl_mapeocelda'` |
| 4 | La relación `sanic_carp_versionplantilla_mapeocelda` (lookup `sanic_versionplantillaid`) | `GET .../OneToManyRelationships` |

## 4. Cómo se construye

```
python3 herramientas/construir/formulario.py playbooks/formulario/versionplantilla.md
```

## 5. Verificación

```
python3 herramientas/construir/formulario.py playbooks/formulario/versionplantilla.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET systemforms?$filter=objecttypecode eq 'sanic_carp_tbl_versionplantilla' and type eq 2` | un solo Main · `name` y `description` los de la sección 2 · activo · no managed |
| 2 | `formxml` | igual al generado, etiquetas en los dos idiomas |
| 3 | Estructura | 1 pestaña, 2 secciones, 4 campos en secciones, 1 subgrilla con `RecordsPerPage` 28, 3 en el encabezado, 0 bibliotecas, 0 manejadores |
| 4 | Pertenencia | la tabla figura en la solución con `rootcomponentbehavior` 0 |
| 5 | Segunda ejecución | `ya_existia` |

## 6. Si ya existe

Mismas comprobaciones que la sección 5. Si pasan: `ya_existia`; si no: `difiere` (corrige con `PATCH` y publica; un Main managed no se toca).

## 7. Reversa

Se restituye el `formxml` anterior desde la solución exportada. Reversible mientras no llegue a Test o Prod.

## 8. Fuera de alcance

Botones Publicar y Vista previa (lote B), el formulario de Mapeo de celda (`playbooks/formulario/mapeocelda.md`), la vista del mapeo.
