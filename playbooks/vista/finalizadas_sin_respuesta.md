# Playbook: vista · Finalizadas sin respuesta

## 1. Identidad

```json
{
  "tipo_playbook": "vista",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.3",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. La vista figura en `diseno/06-inventario-componentes.md` grupo 12, fila 12.3, y su definición completa está en `diseno/07-diseno-app-y-flujos.md` §3.2. El valor 159460003 del resultado ("Sin respuesta") es el de `diseno/07` §3.2 y §6 (12.16).

Advertencias: `idiomas` solo cumple el formato común; una vista tiene un único `name`. El nombre va sin tildes (BP-PP-197); la descripción es prosa y lleva ortografía completa.

## 2. Qué se crea

```json
{
  "tipo": "vista",
  "nombre": "Finalizadas sin respuesta",
  "tabla": "sanic_carp_tbl_conversacion",
  "clase": "Publica",
  "descripcion": "Conversaciones cerradas sin responder, con el motivo y el detalle que dejó quien las finalizó. Para supervisión.",
  "pordefecto": false,
  "columnas": [
    {"nombre": "sanic_nombre", "ancho": 110},
    {"nombre": "sanic_asunto", "ancho": 260},
    {"nombre": "sanic_remitente", "ancho": 200},
    {"nombre": "sanic_motivosinrespuesta", "ancho": 190},
    {"nombre": "sanic_detallesinrespuesta", "ancho": 300},
    {"nombre": "sanic_finalizadoporid", "ancho": 160},
    {"nombre": "sanic_fechafinalizacion", "ancho": 150}
  ],
  "orden": [
    {"columna": "sanic_fechafinalizacion", "sentido": "desc"}
  ],
  "filtro": {
    "tipo": "and",
    "condiciones": [
      {"columna": "sanic_resultado", "operador": "eq", "valores": [159460003]}
    ],
    "enlaces": []
  }
}
```

El `fetchxml` y el `layoutxml` no se escriben acá: los genera `vista.py` desde las columnas, el orden y el filtro. La columna primaria va siempre al `fetch` y la primera de `columnas` es el vínculo que abre el registro.

Viene del diseño (`diseno/07` §3.2), no lo decide este playbook:

- Tabla: `sanic_carp_tbl_conversacion`. Clase: `Publica`. Perfil: Jefe (lo decide el sitemap).
- Filtro: `sanic_resultado` = 159460003. Orden: `sanic_fechafinalizacion` descendente.

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Descripción | «Conversaciones cerradas sin responder, con el motivo y el detalle que dejó quien las finalizó. Para supervisión.» | `vista.py` exige una descripción; el texto lo fija el playbook |

## 3. Precondiciones

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, su publisher coincide y cada idioma está provisionado | los de la sección 1 |
| 2 | La tabla existe y figura en la solución con `rootcomponentbehavior` 0 (la vista viaja dentro de ella) | `sanic_carp_tbl_conversacion` |
| 3 | Cada columna del playbook existe en la tabla | las de la sección 2 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, vistas (`savedquery`). Herramienta del proyecto: `herramientas/construir/vista.py`.

```
python3 herramientas/construir/vista.py playbooks/vista/finalizadas_sin_respuesta.md
```

## 5. Verificación

```
python3 herramientas/construir/vista.py playbooks/vista/finalizadas_sin_respuesta.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET savedqueries` por `name` + `returnedtypecode` | una vista · `querytype` 0 · `isdefault` = false · la descripción de la sección 2 |
| 2 | `fetchxml` y `layoutxml` contra los que genera el playbook | iguales (se ignora el `savedqueryid` que la plataforma inyecta) |
| 3 | Pertenencia a la solución | la tabla figura en la solución con `rootcomponentbehavior` 0 |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |
| 5 | `GET <conjunto de entidades de la tabla>?savedQuery=<savedqueryid>` (solo lectura) | responde 200 sin error; la cantidad de filas queda anotada en el informe del lote |
| 6 | Las otras vistas públicas de la tabla | ninguna otra con `isdefault` = true |

No existe `muestra_vista.py`: la comprobación independiente de este tipo es la del punto 5 y la relectura de `savedqueries`.

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, sin tocar nada. `--completar` corrige `name`, `description`, `isdefault`, `fetchxml` y `layoutxml` y publica la tabla; el `querytype` no se corrige.

## 7. Reversa

`DELETE savedqueries(<savedqueryid>)`. No lo usa ningún dato; la entrada del sitemap que la nombre deja de encontrarla.

## 8. Fuera de alcance

- Que la vista aparezca en el sitemap de la app: `diseno/07` §7 (inventario 12.23 y 12.25).
