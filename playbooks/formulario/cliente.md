# Playbook: formulario · FRM - CARP - Cliente - Principal

> **Tipo con 0 ciclos** (`power-platform-especificar` no tiene referencia para formulario). El formato sale de la Identidad común y de lo que lee `herramientas/construir/formulario.py` (formato ampliado de `herramientas/README.md`).

## 1. Identidad

```json
{
  "tipo_playbook": "formulario",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.17",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0, verificados contra Dev el 2026-10-09. El formulario es la fila 12.17 de `diseno/06-inventario-componentes.md` §12.3; su diseño está en `diseno/07-diseno-app-y-flujos.md` §4.1 y §4.3.

Advertencias: las etiquetas de pestaña y sección van en los dos LCID, en español y sin tildes (BP-PP-197); las de los campos salen del `DisplayName` de cada columna. `formulario.py` **actualiza el Main existente** de la tabla, no crea otro. `sanic_nombre` (primaria, calculada por el plugin y "requerida") **no va en el formulario** (`diseno/07` §4.1): la validación de requerido de la interfaz solo mira lo que está en el formulario y el plugin la completa.

## 2. Qué se crea

```json
{
  "tipo": "formulario",
  "nombre": "FRM - CARP - Cliente - Principal",
  "tabla": "sanic_carp_tbl_cliente",
  "descripcion": "Formulario principal de Cliente: datos de contacto y, debajo, sus arreglos y sus conversaciones. Sin comandos y sin JavaScript.",
  "solo_lectura": false,
  "pestana": {
    "1033": "General",
    "3082": "General"
  },
  "encabezado": [],
  "secciones": [
    {
      "titulo": {
        "1033": "Datos del cliente",
        "3082": "Datos del cliente"
      },
      "campos": [
        "sanic_nombrecompleto",
        "sanic_cedula",
        "sanic_correo",
        "sanic_celular"
      ]
    },
    {
      "titulo": {
        "1033": "Arreglos del cliente",
        "3082": "Arreglos del cliente"
      },
      "subgrilla": {
        "tabla": "sanic_carp_tbl_arreglo",
        "lookup": "sanic_clienteid",
        "vista": "Arreglos del cliente"
      }
    },
    {
      "titulo": {
        "1033": "Conversaciones del cliente",
        "3082": "Conversaciones del cliente"
      },
      "subgrilla": {
        "tabla": "sanic_carp_tbl_conversacion",
        "lookup": "sanic_clienteid",
        "vista": "Conversaciones del cliente"
      }
    }
  ]
}
```

Decisiones y huecos:

- Todos los campos son editables (`diseno/07` §4.3, modo `ed`). `sanic_cedula` está protegida por seguridad de columna: sin el perfil `CSP - CARP - Datos sensibles` aparece bloqueada (no es un defecto del formulario).
- Las subgrillas no declaran `registros_por_pagina`: `diseno/07` no pide un valor para ellas, así que queda el 10 por omisión de la herramienta. **Hueco**: si negocio quiere ver más de 10 arreglos o conversaciones por cliente sin paginar, hay que fijar un valor.
- Sin `bibliotecas` ni `eventos` (`diseno/07` §4.3: "Sin comandos, sin JS").
- "Registrar arreglo desde el cliente" es el "+ Nuevo" de la subgrilla de arreglos (hereda el cliente); no es un componente.

## 3. Precondiciones

| # | Qué tiene que existir | Cómo se comprueba |
|---|---|---|
| 1 | La tabla `sanic_carp_tbl_cliente` y sus 4 columnas, con la etiqueta en 1033 y 3082 | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_cliente')/Attributes` |
| 2 | La tabla figura en la solución con `rootcomponentbehavior` 0 (el formulario viaja con ella) | `GET solutioncomponents` (`componenttype` 1) |
| 3 | La vista **Arreglos del cliente** sobre `sanic_carp_tbl_arreglo` (12.12 / 12.13), exactamente una | `GET savedqueries?$filter=name eq 'Arreglos del cliente' and returnedtypecode eq 'sanic_carp_tbl_arreglo'` |
| 4 | La vista **Conversaciones del cliente** sobre `sanic_carp_tbl_conversacion`, exactamente una | ídem con su nombre |
| 5 | Las relaciones `sanic_carp_cliente_arreglo` (lookup `sanic_clienteid`) y `sanic_carp_cliente_conversacion` (lookup `sanic_clienteid`) | `GET EntityDefinitions(LogicalName='sanic_carp_tbl_cliente')/OneToManyRelationships` |

Si alguna falla: `bloqueado`, no se escribe nada.

## 4. Cómo se construye

No hay referencia de `power-platform-construir` para formulario en el índice; la herramienta del proyecto es `herramientas/construir/formulario.py`.

```
python3 herramientas/construir/formulario.py playbooks/formulario/cliente.md
```

Orden: después de las vistas de las subgrillas (precondiciones 3 y 4).

## 5. Verificación

```
python3 herramientas/construir/formulario.py playbooks/formulario/cliente.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET systemforms?$filter=objecttypecode eq 'sanic_carp_tbl_cliente' and type eq 2` | un solo Main · `name` y `description` los de la sección 2 · `formactivationstate` 1 · `ismanaged` false |
| 2 | `formxml` | igual al que genera el playbook, con una etiqueta por idioma (1033 y 3082) en pestaña, secciones y campos, en los dos sentidos |
| 3 | Estructura | 1 pestaña, 3 secciones, 4 campos, 2 subgrillas, 0 en el encabezado, 0 bibliotecas, 0 manejadores |
| 4 | Pertenencia | la tabla figura en la solución con `rootcomponentbehavior` 0 |
| 5 | Segunda ejecución | `ya_existia` |

Fuera de la herramienta: abrir un cliente en la app y ver las dos subgrillas con las vistas pedidas.

## 6. Si ya existe

Mismas comprobaciones que la sección 5. Si pasan: `ya_existia`. Si no: `difiere` con la diferencia exacta; la herramienta corrige `name`, `description`, `formxml` y activación con `PATCH` y publica la tabla. Un Main managed no se toca.

## 7. Reversa

Se restituye el `formxml` del Main anterior (queda en el historial de la solución exportada, no en la herramienta). No hay datos asociados: es reversible mientras no haya llegado a Test o Prod.

## 8. Fuera de alcance

Las vistas de las subgrillas (`playbooks/vista/`), la app y su sitemap (`playbooks/app/controlarreglospago.md`), el formulario de creación rápida (no existe en el diseño).
