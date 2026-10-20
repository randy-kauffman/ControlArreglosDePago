# Playbook: vista · Arreglos de prestamo

## 1. Identidad

```json
{
  "tipo_playbook": "vista",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.8",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. La vista figura en `diseno/06-inventario-componentes.md` grupo 12, fila 12.8, y su definición completa está en `diseno/07-diseno-app-y-flujos.md` §3.4 (con DA-18: todos los estados). Los nombres lógicos de tabla y columnas se comprobaron contra Dev (`EntityDefinitions`) el 2026-10-20 en el lote A (mismas columnas que "Arreglos vigentes").

Advertencias: `idiomas` solo cumple el formato común; una vista tiene un único `name`. El nombre va sin tildes (BP-PP-197); la descripción es prosa y lleva ortografía completa.

## 2. Qué se crea

```json
{
  "tipo": "vista",
  "nombre": "Arreglos de prestamo",
  "tabla": "sanic_carp_tbl_arreglo",
  "clase": "Publica",
  "descripcion": "Arreglos cuyo producto es préstamo, en cualquier estado. El filtro de la columna Estado permite acotar.",
  "pordefecto": false,
  "columnas": [
    {"nombre": "sanic_nombre", "ancho": 110},
    {"nombre": "sanic_clienteid", "ancho": 180},
    {"nombre": "sanic_producto", "ancho": 100},
    {"nombre": "sanic_numeroproducto", "ancho": 140},
    {"nombre": "sanic_numeroarreglo", "ancho": 90},
    {"nombre": "statuscode", "ancho": 110},
    {"nombre": "sanic_tipo", "ancho": 100},
    {"nombre": "sanic_moneda", "ancho": 90},
    {"nombre": "sanic_cuotamensual", "ancho": 110},
    {"nombre": "sanic_plazo", "ancho": 70},
    {"nombre": "sanic_oficialid", "ancho": 160},
    {"nombre": "modifiedon", "ancho": 150}
  ],
  "orden": [
    {"columna": "modifiedon", "sentido": "desc"}
  ],
  "filtro": {
    "tipo": "and",
    "condiciones": [
      {"columna": "sanic_producto", "operador": "eq", "valores": [159460001]}
    ],
    "enlaces": []
  }
}
```

El `fetchxml` y el `layoutxml` no se escriben acá: los genera `vista.py` desde las columnas, el orden y el filtro. La columna primaria va siempre al `fetch` y la primera de `columnas` es el vínculo que abre el registro.

Viene del diseño (`diseno/07` §3.4, DA-18), no lo decide este playbook:

- Tabla: `sanic_carp_tbl_arreglo`. Clase: `Publica`. Perfil que la encuentra: Oficial y Jefe (lo decide el sitemap).
- Filtro: `sanic_producto` = 159460001 (préstamo), todos los estados. Orden: `modifiedon` descendente. Columnas: las de la fila "Arreglos de prestamo / de tarjeta / vigentes / cerrados / Todos".

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Descripción | «Arreglos cuyo producto es préstamo, en cualquier estado. El filtro de la columna Estado permite acotar.» | `vista.py` exige una descripción; el texto lo fija el playbook |

## 3. Precondiciones

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, su publisher coincide y cada idioma está provisionado | los de la sección 1 |
| 2 | La tabla existe y figura en la solución con `rootcomponentbehavior` 0 (la vista viaja dentro de ella) | `sanic_carp_tbl_arreglo` |
| 3 | Cada columna del playbook existe en la tabla | las de la sección 2 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, vistas (`savedquery`). Herramienta del proyecto: `herramientas/construir/vista.py`.

```
python3 herramientas/construir/vista.py playbooks/vista/arreglos_de_prestamo.md
```

## 5. Verificación

```
python3 herramientas/construir/vista.py playbooks/vista/arreglos_de_prestamo.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET savedqueries` por `name` + `returnedtypecode` | una vista · `querytype` 0 · `isdefault` = false · la descripción de la sección 2 |
| 2 | `fetchxml` y `layoutxml` contra los que genera el playbook | iguales (se ignora el `savedqueryid` que la plataforma inyecta) |
| 3 | Pertenencia a la solución | la tabla figura en la solución con `rootcomponentbehavior` 0 |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |
| 5 | `GET <conjunto de entidades de la tabla>?savedQuery=<savedqueryid>` (solo lectura) | responde 200 sin error; con datos de humo, la cantidad de filas queda anotada en el informe del lote |
| 6 | Las otras vistas públicas de la tabla | ninguna otra con `isdefault` = true |

No existe `muestra_vista.py`: la comprobación independiente de este tipo es la del punto 5 y la relectura de `savedqueries`.

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, sin tocar nada. `--completar` corrige `name`, `description`, `isdefault`, `fetchxml` y `layoutxml` y publica la tabla; el `querytype` no se corrige.

## 7. Reversa

`DELETE savedqueries(<savedqueryid>)`. No lo usa ningún dato; la entrada del sitemap que la nombre deja de encontrarla.

## 8. Fuera de alcance

- Que la vista aparezca en el sitemap de la app: `diseno/07` §7 (inventario 12.23 y 12.25).
- Promedios, días de atraso y estado de la cuota: no se muestran en vistas (D-27, DA-11).
