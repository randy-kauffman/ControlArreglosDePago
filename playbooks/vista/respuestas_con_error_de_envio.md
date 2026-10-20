# Playbook: vista · Respuestas con error de envio

> **Nota de verificación para `email`** (ver `respuestas_enviadas.md`): si `vista.py` da `difiere` solo por «la tabla `email` está en la solución con `rootcomponentbehavior` 1», la vista igual viaja como componente propio (`solutioncomponents` tipo 26). El resultado de la construcción se anota en `pruebas-humo/lote-c/RESULTADO.md`.

## 1. Identidad

```json
{
  "tipo_playbook": "vista",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.4",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. La vista figura en `diseno/06-inventario-componentes.md` grupo 12, fila 12.4, y su definición completa está en `diseno/07-diseno-app-y-flujos.md` §3.3. `statuscode` 8 = Failed (`03` §0.7, §2.9).

Advertencias: `idiomas` solo cumple el formato común; una vista tiene un único `name`. El nombre va sin tildes (BP-PP-197); la descripción es prosa y lleva ortografía completa.

## 2. Qué se crea

```json
{
  "tipo": "vista",
  "nombre": "Respuestas con error de envio",
  "tabla": "email",
  "clase": "Publica",
  "descripcion": "Correos de respuesta que el sistema no pudo enviar, con la conversación a la que pertenecen. Para supervisión.",
  "pordefecto": false,
  "columnas": [
    {"nombre": "subject", "ancho": 300},
    {"nombre": "regardingobjectid", "ancho": 140},
    {"nombre": "torecipients", "ancho": 220},
    {"nombre": "createdby", "ancho": 160},
    {"nombre": "createdon", "ancho": 150},
    {"nombre": "statuscode", "ancho": 110}
  ],
  "orden": [
    {"columna": "createdon", "sentido": "desc"}
  ],
  "filtro": {
    "tipo": "and",
    "condiciones": [
      {"columna": "directioncode", "operador": "eq", "valores": [1]},
      {"columna": "statuscode", "operador": "eq", "valores": [8]}
    ],
    "enlaces": [
      {
        "tipo": "inner",
        "tabla": "sanic_carp_tbl_conversacion",
        "de": "sanic_carp_tbl_conversacionid",
        "a": "regardingobjectid",
        "alias": "cnv"
      }
    ]
  }
}
```

El `fetchxml` y el `layoutxml` no se escriben acá: los genera `vista.py` desde las columnas, el orden y el filtro. La columna primaria va siempre al `fetch` y la primera de `columnas` es el vínculo que abre el registro.

Viene del diseño (`diseno/07` §3.3), no lo decide este playbook:

- Tabla: `email`. Clase: `Publica`. Perfil: Jefe (Supervisión; lo decide el sitemap).
- Filtro: `directioncode` = 1; `statuscode` = 8; y que el correo sea de una conversación. Orden: `createdon` descendente.
- **Verificado en Dev (2026-10-20, lote C):** el literal `regardingobjecttypecode = 'sanic_carp_tbl_conversacion'` da HTTP 400 (el atributo espera un Int32, el ObjectTypeCode, que en una tabla personalizada cambia de entorno a entorno). Se usa el respaldo del diseño: `enlace` inner hacia `sanic_carp_tbl_conversacion` por `regardingobjectid` → `sanic_carp_tbl_conversacionid` (un salto, sin columnas).

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Descripción | «Correos de respuesta que el sistema no pudo enviar, con la conversación a la que pertenecen. Para supervisión.» | `vista.py` exige una descripción; el texto lo fija el playbook |

## 3. Precondiciones

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, su publisher coincide y cada idioma está provisionado | los de la sección 1 |
| 2 | La tabla existe y figura en la solución | `email` |
| 3 | Cada columna del playbook existe en la tabla | las de la sección 2 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, vistas (`savedquery`). Herramienta del proyecto: `herramientas/construir/vista.py`.

```
python3 herramientas/construir/vista.py playbooks/vista/respuestas_con_error_de_envio.md
```

## 5. Verificación

```
python3 herramientas/construir/vista.py playbooks/vista/respuestas_con_error_de_envio.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET savedqueries` por `name` + `returnedtypecode` | una vista · `querytype` 0 · `isdefault` = false · la descripción de la sección 2 |
| 2 | `fetchxml` y `layoutxml` contra los que genera el playbook | iguales (se ignora el `savedqueryid` que la plataforma inyecta) |
| 3 | Pertenencia a la solución | ver la nota del inicio (tabla estándar) |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |
| 5 | `GET emails?savedQuery=<savedqueryid>` (solo lectura) | responde 200 sin error (prueba que el enlace por `regardingobjectid` lo acepta el servidor); la cantidad de filas queda anotada |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, sin tocar nada. `--completar` corrige `name`, `description`, `isdefault`, `fetchxml` y `layoutxml` y publica la tabla; el `querytype` no se corrige.

## 7. Reversa

`DELETE savedqueries(<savedqueryid>)`. No lo usa ningún dato; la entrada del sitemap que la nombre deja de encontrarla.

## 8. Fuera de alcance

- Que la vista aparezca en el sitemap de la app: `diseno/07` §7 (inventario 12.23 y 12.25).
- El gráfico y el panel (lote E).
