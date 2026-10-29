# Playbook: icono-tabla · tablas propias

> **Tipo con 0 ciclos** (`power-platform-especificar` no tiene referencia para el ícono de tabla). Pone el ícono SVG propio de las 7 tablas del proyecto (`diseno/07` §13, DA-22, 2026-10-28). Lo usan la navegación, los lookups, los formularios y las vistas de cada tabla.

## 1. Identidad

```json
{
  "tipo_playbook": "icono-tabla",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.27",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0, verificados contra Dev el 2026-10-09. La tabla → ícono sale de `diseno/07` §13.

Advertencias: el ícono es `EntityMetadata.IconVectorName`, el nombre de un web resource SVG (Learn, "Image web resources": se usa en lugar de `IconSmallName`, `IconMediumName` e `IconLargeName`). No tiene idioma: los nombres visibles de cada tabla ya están en 1033 y 3082 desde su playbook de tabla.

## 2. Qué se crea

```json
{
  "tipo": "icono-tabla",
  "tablas": [
    {"tabla": "sanic_carp_tbl_cliente", "icono": "sanic_carp_wr_svg_tbl_cliente"},
    {"tabla": "sanic_carp_tbl_conversacion", "icono": "sanic_carp_wr_svg_tbl_conversacion"},
    {"tabla": "sanic_carp_tbl_arreglo", "icono": "sanic_carp_wr_svg_tbl_arreglo"},
    {"tabla": "sanic_carp_tbl_cuota", "icono": "sanic_carp_wr_svg_tbl_cuota"},
    {"tabla": "sanic_carp_tbl_generacionformato", "icono": "sanic_carp_wr_svg_tbl_generacionformato"},
    {"tabla": "sanic_carp_tbl_versionplantilla", "icono": "sanic_carp_wr_svg_tbl_versionplantilla"},
    {"tabla": "sanic_carp_tbl_mapeocelda", "icono": "sanic_carp_wr_svg_tbl_mapeocelda"}
  ]
}
```

## 3. Precondiciones

| # | Qué tiene que cumplirse | Cómo se comprueba |
|---|---|---|
| 1 | La solución existe y no es managed; los idiomas están provisionados | `comprobar_solucion_e_idioma` |
| 2 | Los 7 web resources `sanic_carp_wr_svg_tbl_*` existen | `GET webresourceset?$filter=name eq '...'` (si falta uno: `bloqueado` sin escribir nada) |
| 3 | Las 7 tablas existen y no son managed | `GET EntityDefinitions(LogicalName='...')` |

## 4. Cómo se construye

```
python3 herramientas/construir/icono_tabla.py playbooks/icono-tabla/tablas.md
```

Por cada tabla cuyo `IconVectorName` difiere: PUT de la definición COMPLETA que devuelve el entorno (PUT reemplaza todo) con el ícono cambiado, `MSCRM.MergeLabels: true` y `MSCRM.SolutionUniqueName`; después `PublishXml` de esa tabla y relectura.

## 5. Verificación

```
python3 herramientas/construir/icono_tabla.py playbooks/icono-tabla/tablas.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET EntityDefinitions(LogicalName='<tabla>')` de cada una | `IconVectorName` = el del playbook |
| 2 | Segunda ejecución | `ya_existia` |

## 6. Si ya existe

Una tabla que ya tiene el ícono no se toca. `--solo-verificar` informa `difiere` con el ícono actual de cada tabla que no coincide.

## 7. Reversa

El mismo PUT con el `IconVectorName` anterior (o vacío para volver al ícono por defecto). No borra nada.

## 8. Fuera de alcance

Los web resources (`playbooks/web-resource/sanic_carp_wr_svg_tbl_*.md`) y los nombres visibles de las tablas (sus playbooks de tabla).
