# Playbook: web-resource · sanic_carp_wr_svg_arr_prestamo

> **Tipo con 0 ciclos** (`power-platform-especificar` no tiene referencia para web resource). Icono propio de entrada del sitemap: **Arreglos de prestamo** / *Loan agreements*. Reemplaza a los cuatro iconos compartidos de DA-10 (`diseno/07` §13, DA-22, 2026-10-28).

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

De dónde sale cada dato: `diseno/01-convenciones.md` §0, verificados contra Dev el 2026-10-09. El icono sale de la tabla de `diseno/07` §13.

Advertencias: el web resource tiene **un solo** `displayname` y una sola `description` (no son etiquetas por idioma); por eso la descripción lleva el texto en los dos idiomas. Lo que la persona ve en cada idioma es el título del sitemap o la etiqueta del botón, que sí van en 1033 y 3082. El `displayname` va sin tildes.

## 2. Qué se crea

```json
{
  "tipo": "web-resource",
  "nombre": "sanic_carp_wr_svg_arr_prestamo",
  "displayname": "WR - CARP - SVG - Arr Prestamo",
  "descripcion": "ES: Arreglos de prestamo / EN: Loan agreements. Edificio de banco: producto de prestamo. Trazo currentColor, 24x24, sin marcas.",
  "recurso": "SVG",
  "archivo": "webresources/svg/arr_prestamo.svg"
}
```

Diseño: trazo `stroke="currentColor"` sin colores fijos ni hojas de estilo, con `width`, `height` y `viewBox` (Learn, "Image web resources": la app controla el color y el contraste). Dibujo propio en el estilo de línea de Fluent UI System Icons (no copia ningún ícono; Fluent UI System Icons es MIT).

## 3. Precondiciones

| # | Qué tiene que cumplirse | Cómo se comprueba |
|---|---|---|
| 1 | El archivo existe en el repositorio, no está vacío y es un SVG válido | `webresources/svg/arr_prestamo.svg` (la herramienta devuelve `bloqueado` si falta; el XML se validó al generarlo) |
| 2 | La solución existe y no es managed | `comprobar_solucion_e_idioma` |

## 4. Cómo se construye

```
python3 herramientas/construir/web_resource.py playbooks/web-resource/sanic_carp_wr_svg_arr_prestamo.md
```

## 5. Verificación

```
python3 herramientas/construir/web_resource.py playbooks/web-resource/sanic_carp_wr_svg_arr_prestamo.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET webresourceset?$filter=name eq 'sanic_carp_wr_svg_arr_prestamo'` | una fila · `webresourcetype` = 11 (SVG) · `displayname` y `description` los de la sección 2 · `ismanaged` = false |
| 2 | Contenido | igual al archivo del repositorio |
| 3 | Pertenencia a la solución | figura en `sanic_carp_sol_controlarreglospago` |
| 4 | Segunda ejecución | `ya_existia` |

## 6. Si ya existe

Mismas comprobaciones que la sección 5. Si pasan: `ya_existia`; si no: `difiere` sin tocar nada (`--completar` corrige descripción y contenido y publica).

## 7. Reversa

`DELETE webresourceset(<webresourceid>)`. Imposible mientras entrada del sitemap lo referencie (dependencia de `$webresource:`).

## 8. Fuera de alcance

Quién lo usa: entrada del sitemap (`playbooks/app/controlarreglospago.md`, `playbooks/importacion/comandos.md` o `playbooks/icono-tabla/tablas.md`).
