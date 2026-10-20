# Playbook: vista · Correos sin respuesta

## 1. Identidad

```json
{
  "tipo_playbook": "vista",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.1",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. La vista figura en `diseno/06-inventario-componentes.md` grupo 12, fila 12.1, y su definición completa está en `diseno/07-diseno-app-y-flujos.md` §3.2. Los nombres lógicos de tabla y columnas se comprobaron contra Dev (`EntityDefinitions`) el 2026-10-20.

Advertencias: `idiomas` solo cumple el formato común; una vista tiene un único `name`. El nombre va sin tildes (BP-PP-197); la descripción es prosa y lleva ortografía completa.

## 2. Qué se crea

```json
{
  "tipo": "vista",
  "nombre": "Correos sin respuesta",
  "tabla": "sanic_carp_tbl_conversacion",
  "clase": "Publica",
  "descripcion": "Conversaciones con un correo del cliente sin responder, incluidas las finalizadas a las que llegó un correo nuevo. Supervisión del jefe.",
  "pordefecto": false,
  "columnas": [
    {
      "nombre": "sanic_nombre",
      "ancho": 110
    },
    {
      "nombre": "sanic_asunto",
      "ancho": 280
    },
    {
      "nombre": "sanic_remitente",
      "ancho": 200
    },
    {
      "nombre": "sanic_clienteid",
      "ancho": 180
    },
    {
      "nombre": "sanic_oficialasignadoid",
      "ancho": 160
    },
    {
      "nombre": "statuscode",
      "ancho": 110
    },
    {
      "nombre": "sanic_sinrespuestadesde",
      "ancho": 150
    },
    {
      "nombre": "sanic_antiguedad",
      "ancho": 100
    },
    {
      "nombre": "sanic_correonuevoenfinalizada",
      "ancho": 90
    }
  ],
  "orden": [
    {
      "columna": "sanic_sinrespuestadesde",
      "sentido": "asc"
    }
  ],
  "filtro": {
    "tipo": "and",
    "condiciones": [
      {
        "columna": "sanic_sinrespuestadesde",
        "operador": "not-null"
      }
    ],
    "enlaces": []
  }
}
```

El `fetchxml` y el `layoutxml` no se escriben acá: los genera `vista.py` desde las columnas, el orden y el filtro. La columna primaria va siempre al `fetch` y la primera de `columnas` es el vínculo que abre el registro.

Viene del diseño (`diseno/07` §3.2), no lo decide este playbook:

- Tabla: `sanic_carp_tbl_conversacion`. Clase: `Publica`. Perfil que la encuentra: Jefe (Supervisión) (lo decide el sitemap; la vista no tiene seguridad propia).
- Filtro: `sanic_sinrespuestadesde` no vacío. Orden: `sanic_sinrespuestadesde` ascendente (se ordena por la fecha y no por `sanic_antiguedad`, que es fórmula).
- Anchos en píxeles y orden de columnas: los de la tabla de columnas de §3.2 a §3.5.

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Descripción | «Conversaciones con un correo del cliente sin responder, incluidas las finalizadas a las que llegó un correo nuevo. Supervisión del jefe.» | `vista.py` exige una descripción; el diseño trae el nombre, el propósito y el perfil, y el texto lo fija el playbook |

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
python3 herramientas/construir/vista.py playbooks/vista/correos_sin_respuesta.md
```

## 5. Verificación

```
python3 herramientas/construir/vista.py playbooks/vista/correos_sin_respuesta.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET savedqueries` por `name` + `returnedtypecode` | una vista · `querytype` 0 · `isdefault` = false · la descripción de la sección 2 |
| 2 | `fetchxml` y `layoutxml` contra los que genera el playbook | iguales (se ignora el `savedqueryid` que la plataforma inyecta) |
| 3 | Pertenencia a la solución | la tabla figura en la solución con `rootcomponentbehavior` 0 |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |
| 5 | `GET <conjunto de entidades de la tabla>?savedQuery=<savedqueryid>` (solo lectura) | responde 200 sin error; con datos de humo, la cantidad de filas queda anotada en el informe del lote |

No existe `muestra_vista.py`: la comprobación independiente de este tipo es la del punto 5 y la relectura de `savedqueries`.

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, sin tocar nada. `--completar` corrige `name`, `description`, `isdefault`, `fetchxml` y `layoutxml` y publica la tabla; el `querytype` no se corrige (conviene borrar la vista y rehacerla), y eso lo decide quien dirige la construcción.

## 7. Reversa

`DELETE savedqueries(<savedqueryid>)`. No lo usa ningún dato; las subgrillas y selectores que la nombren dejan de encontrarla.

## 8. Fuera de alcance

- Que la vista aparezca en el sitemap de la app: `diseno/07` §7 (inventario 12.23 y 12.25).
- Subgrillas y selectores que usan la vista: formularios 12.17 a 12.21, `diseno/07` §4.
- Promedios, días de atraso y estado de la cuota: no se muestran en vistas (D-27, DA-11).
