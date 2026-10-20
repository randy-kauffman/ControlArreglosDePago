# Playbook: web-resource · sanic_carp_wr_svg_app

> **Tipo con 0 ciclos** (`power-platform-especificar` no tiene referencia para web resource). Uno de los cuatro iconos SVG compartidos (DA-10, propuesta 12.27 de `diseno/07` §11).

## 1. Identidad

```json
{
  "tipo_playbook": "web-resource",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0, verificados contra Dev el 2026-10-09. Los cuatro iconos salen de `diseno/07` §2 (final) y §5.1; la fila 12.27 es una **propuesta** de `diseno/07` §11 que no suma al inventario hasta que se confirme.

Advertencias: `web_resource.py` maneja `displayname` y `descripcion` como una sola cadena; `idiomas` solo cumple el formato común. El `displayname` va sin tildes.

## 2. Qué se crea

```json
{
  "tipo": "web-resource",
  "nombre": "sanic_carp_wr_svg_app",
  "displayname": "WR - CARP - SVG - App",
  "descripcion": "Icono de la app model-driven MDA - CARP - Control de Arreglos de Pago: una hoja de documento. Monocromo, sin marcas.",
  "recurso": "SVG",
  "archivo": "webresources/svg/app.svg"
}
```

## 3. Precondiciones

| # | Qué tiene que cumplirse | Cómo se comprueba |
|---|---|---|
| 1 | El archivo existe en el repositorio, no está vacío y es un SVG válido | `webresources/svg/app.svg` (la herramienta devuelve `bloqueado` si falta; que sea XML válido se revisó a mano) |
| 2 | La solución existe y no es managed | `comprobar_solucion_e_idioma` |

## 4. Cómo se construye

```
python3 herramientas/construir/web_resource.py playbooks/web-resource/sanic_carp_wr_svg_app.md
```

## 5. Verificación

```
python3 herramientas/construir/web_resource.py playbooks/web-resource/sanic_carp_wr_svg_app.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET webresourceset?$filter=name eq 'sanic_carp_wr_svg_app'` | una fila · `webresourcetype` = 11 (SVG) · `displayname` y `description` los de la sección 2 · `ismanaged` = false |
| 2 | Contenido | igual al archivo del repositorio |
| 3 | Pertenencia a la solución | figura en `sanic_carp_sol_controlarreglospago` |
| 4 | Segunda ejecución | `ya_existia` |

## 6. Si ya existe

Mismas comprobaciones que la sección 5. Si pasan: `ya_existia`; si no: `difiere` sin tocar nada (`--completar` corrige descripción y contenido y publica).

## 7. Reversa

`DELETE webresourceset(<webresourceid>)`. Imposible si la app o una entrada del sitemap lo referencia: primero se cambia el icono de la app.

## 8. Fuera de alcance

La app que lo usa: `playbooks/app/controlarreglospago.md`. Los otros tres iconos: `sanic_carp_wr_svg_lista`, `_accion`, `_alerta`.
