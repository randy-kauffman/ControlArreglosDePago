# Playbook: vista · Arreglos del cliente

> **HUECO (sin construir).** El diseño pide ordenar por `createdon` descendente y su lista de columnas (§3.4) no incluye `createdon`. `vista.py` solo ordena por una columna visible (regla de validación: «se ordena por … que la vista no muestra»). Este playbook queda tal como lo dice el diseño y por eso NO valida. Para construirlo hay que decidir una de dos cosas: agregar la columna `createdon` a la vista, u ordenar por una columna ya visible (por ejemplo `sanic_fechainicio`, que es nula en los borradores). Decisión del arquitecto; no se inventa acá.

## 1. Identidad

```json
{
  "tipo_playbook": "vista",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.12",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. La vista figura en `diseno/06-inventario-componentes.md` grupo 12, fila 12.12, y su definición completa está en `diseno/07-diseno-app-y-flujos.md` §3.4. Los nombres lógicos de tabla y columnas se comprobaron contra Dev (`EntityDefinitions`) el 2026-10-20.

Advertencias: `idiomas` solo cumple el formato común; una vista tiene un único `name`. El nombre va sin tildes (BP-PP-197); la descripción es prosa y lleva ortografía completa.

## 2. Qué se crea

```json
{
  "tipo": "vista",
  "nombre": "Arreglos del cliente",
  "tabla": "sanic_carp_tbl_arreglo",
  "clase": "Publica",
  "descripcion": "Arreglos de un cliente en todos los estados, para las subgrillas de los formularios de Cliente y de Conversación.",
  "pordefecto": false,
  "columnas": [
    {
      "nombre": "sanic_nombre",
      "ancho": 110
    },
    {
      "nombre": "sanic_producto",
      "ancho": 100
    },
    {
      "nombre": "sanic_numeroproducto",
      "ancho": 140
    },
    {
      "nombre": "sanic_numeroarreglo",
      "ancho": 90
    },
    {
      "nombre": "statuscode",
      "ancho": 110
    },
    {
      "nombre": "sanic_tipo",
      "ancho": 100
    },
    {
      "nombre": "sanic_moneda",
      "ancho": 90
    },
    {
      "nombre": "sanic_cuotamensual",
      "ancho": 110
    },
    {
      "nombre": "sanic_plazo",
      "ancho": 70
    },
    {
      "nombre": "sanic_fechainicio",
      "ancho": 120
    },
    {
      "nombre": "sanic_oficialid",
      "ancho": 160
    }
  ],
  "orden": [
    {
      "columna": "createdon",
      "sentido": "desc"
    }
  ],
  "filtro": {
    "tipo": "and",
    "condiciones": [],
    "enlaces": []
  }
}
```

El `fetchxml` y el `layoutxml` no se escriben acá: los genera `vista.py` desde las columnas, el orden y el filtro. La columna primaria va siempre al `fetch` y la primera de `columnas` es el vínculo que abre el registro.

Viene del diseño (`diseno/07` §3.4), no lo decide este playbook:

- Tabla: `sanic_carp_tbl_arreglo`. Clase: `Publica`. Perfil que la encuentra: Ninguno (subgrilla de 12.17 y 12.18; DA-19) (lo decide el sitemap; la vista no tiene seguridad propia).
- Filtro: ninguno (todos los estados). Orden: `createdon` descendente.
- Anchos en píxeles y orden de columnas: los de la tabla de columnas de §3.2 a §3.5.

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Descripción | «Arreglos de un cliente en todos los estados, para las subgrillas de los formularios de Cliente y de Conversación.» | `vista.py` exige una descripción; el diseño trae el nombre, el propósito y el perfil, y el texto lo fija el playbook |

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
python3 herramientas/construir/vista.py playbooks/vista/arreglos_del_cliente.md
```

## 5. Verificación

```
python3 herramientas/construir/vista.py playbooks/vista/arreglos_del_cliente.md --solo-verificar
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
