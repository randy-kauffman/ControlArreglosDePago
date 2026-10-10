# Playbook: rol · SR - CARP - Purga

## 1. Identidad

```json
{
  "tipo_playbook": "rol",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "6.5",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. El rol está definido en `diseno/04-matriz-privilegios.md` (columna Pur, secciones 2 y 3) y figura en `diseno/06-inventario-componentes.md` §6, renglón 6.5 (en la definición: "ARP Purga").

Advertencias: un rol no lleva etiquetas por idioma (es un texto único), así que `idiomas` solo cumple el formato común. El nombre va sin tildes (BP-PP-197); la descripción es prosa y lleva ortografía completa.

## 2. Qué se crea

```json
{
  "tipo": "rol",
  "nombre": "SR - CARP - Purga",
  "descripcion": "Cuenta de servicio de los trabajos de retención: lee y borra arreglos, cuotas, generaciones de formato, conversaciones, correos con sus adjuntos y clientes, y ejecuta eliminaciones masivas. Es el único rol que borra el agregado.",
  "base": "App Opener",
  "tablas": {
    "sanic_carp_tbl_arreglo": {
      "leer": "unidad",
      "borrar": "unidad"
    },
    "sanic_carp_tbl_cuota": {
      "leer": "unidad",
      "borrar": "unidad"
    },
    "sanic_carp_tbl_generacionformato": {
      "leer": "unidad",
      "borrar": "unidad"
    },
    "sanic_carp_tbl_conversacion": {
      "leer": "unidad",
      "borrar": "unidad"
    },
    "sanic_carp_tbl_cliente": {
      "leer": "unidad",
      "borrar": "unidad"
    }
  },
  "otros_privilegios": {
    "prvReadActivity": "unidad",
    "prvDeleteActivity": "unidad",
    "prvBulkDelete": "organizacion"
  }
}
```

Un security role en la unidad de negocio raíz, con los privilegios del rol `App Opener` (los que tenga el entorno en el momento de construir) más los 13 de este playbook (10 sobre tablas de la solución y 3 estándar). Alcances: `usuario` = Basic (solo lo propio) · `unidad` = Local (la BU `Arreglos de Pago`) · `unidad_e_hijas` = Deep · `organizacion` = Global.

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Descripción del rol | «Cuenta de servicio de los trabajos de retención: lee y borra arreglos, cuotas, generaciones de formato, conversaciones, correos con sus adjuntos y clientes, y ejecuta eliminaciones masivas. Es el único rol que borra el agregado.» | La matriz trae el nombre y los privilegios; el texto lo fija el playbook |
| Rol base | `App Opener` | `rol.py` exige un rol base y la convención del estudio es `App Opener` (`rol.md` §1). Duda D-05 de la matriz |

Viene del diseño, no lo decide este playbook:

- Privilegios: `04` §2 y §3, columna Pur. Definición línea 396: "Read, Delete y BulkDelete sobre Arreglo, Cuota, Generación, Conversación, Email con sus adjuntos y Queue Item y Cliente. Ningún rol humano tiene Delete sobre el agregado".
- **`prvBulkDelete` solo admite `organizacion`** (Dev, 2026-10-09: `canbeglobal = true` y los otros tres en `false`). Es la única celda de la matriz que no es `unidad`. El borrado sigue acotado a la BU porque el Delete de cada tabla es `unidad`: BulkDelete es el permiso de usar la función, no el alcance de los datos.
- **Email con sus adjuntos**: el Email se borra con `prvDeleteActivity`. `activitymimeattachment` y `attachment` no tienen privilegios propios (Dev, 2026-10-09), y el spike 3 mostró que borrar el Email limpia el adjunto y sus Queue Items (definición línea 346). `prvDeleteActivity` es el privilegio de Delete de **toda** actividad: en `unidad` alcanza a cualquier actividad de la BU, no solo a los correos (spike 4, hallazgo 2).
- **Queue Item**: no tiene privilegios propios (Dev, 2026-10-09; spike 4, hallazgo 3), así que no hay nada que asignar. Duda D-04 de la matriz: no consta que el borrado en cascada del Queue Item se haya probado con este rol y no con un administrador.
- Se lee cada tabla porque un BulkDelete por filtro necesita ver las filas; `prvReadActivity` por la misma razón para el Email.
- Orden de purga (definición línea 346): primero los Emails y después la conversación, porque borrar la conversación deja huérfano el archivo del adjunto. Lo respeta el trabajo (inventario 13.12), no el rol.
- No lleva `anexar`, `anexar_a`, `escribir`, `crear` ni `asignar` sobre ninguna tabla: la purga no escribe nada.
- No hay un trabajo de borrado de `msdyn_richtextfile` y el rol no tiene privilegios sobre esa tabla: las imágenes pegadas viven dentro del cuerpo del Email (spike 4, D-22 y D-34).
- Este rol lo reciben las cuentas de servicio que están en la BU, que además tienen `SR - CARP - Oficial` (definición línea 393). La purga corre en Prod con la identidad de la cuenta de servicio y en Test para validarla (definición, "Trabajos de eliminación masiva").

## 3. Precondiciones

Las de la receta (`seguridad/patrones.md` §2.1), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide en unique name, prefijo y prefijo de opciones; cada idioma está provisionado y el idioma base es uno de ellos | los de la sección 1 |
| 2 | Hay exactamente una unidad de negocio raíz | una |
| 3 | El rol base existe una vez en la unidad raíz | `App Opener` |
| 4 | Cada tabla nombrada existe, y cada privilegio admite el alcance pedido | `sanic_carp_tbl_arreglo` · `sanic_carp_tbl_cuota` · `sanic_carp_tbl_generacionformato` · `sanic_carp_tbl_conversacion` · `sanic_carp_tbl_cliente` |
| 5 | Cada privilegio de `otros_privilegios` existe una vez y admite el alcance pedido | `prvReadActivity` · `prvDeleteActivity` · `prvBulkDelete` (solo admite `organizacion`) |
| 6 | El nombre no lleva tildes ni ñ (BP-PP-197) y cumple `SR - CARP - Nombre` | el de la sección 2 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/seguridad/patrones.md` §2.1 "Security role". Herramienta del proyecto: `herramientas/construir/rol.py`.

```
python3 herramientas/construir/rol.py playbooks/rol/sr_carp_purga.md
```

## 5. Verificación

```
python3 herramientas/construir/rol.py playbooks/rol/sr_carp_purga.md --solo-verificar
python3 herramientas/construir/muestra_rol.py playbooks/rol/sr_carp_purga.md --guardar
```

Privilegios sobre las tablas de la solución. **Exactamente estos**: uno de menos, uno de más u otro alcance es `difiere`.

| Tabla | Privilegios esperados |
|---|---|
| `sanic_carp_tbl_arreglo` | `prvReadsanic_carp_tbl_arreglo` = Local · `prvDeletesanic_carp_tbl_arreglo` = Local |
| `sanic_carp_tbl_cuota` | `prvReadsanic_carp_tbl_cuota` = Local · `prvDeletesanic_carp_tbl_cuota` = Local |
| `sanic_carp_tbl_generacionformato` | `prvReadsanic_carp_tbl_generacionformato` = Local · `prvDeletesanic_carp_tbl_generacionformato` = Local |
| `sanic_carp_tbl_conversacion` | `prvReadsanic_carp_tbl_conversacion` = Local · `prvDeletesanic_carp_tbl_conversacion` = Local |
| `sanic_carp_tbl_cliente` | `prvReadsanic_carp_tbl_cliente` = Local · `prvDeletesanic_carp_tbl_cliente` = Local |

Otros privilegios:

| Privilegio | Alcance |
|---|---|
| `prvReadActivity` | Local |
| `prvDeleteActivity` | Local |
| `prvBulkDelete` | Global |

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET roles` por nombre en la unidad raíz | un rol · `ismanaged = false` · la descripción de la sección 2 |
| 2 | `RetrieveRolePrivilegesRole` | los privilegios de `App Opener` más los 13 de arriba; **ninguno más** (en particular, ningún Create, Write, Append ni Assign, y ningún privilegio sobre Versión de plantilla ni Mapeo de celda) |
| 3 | Pertenencia a la solución | el rol figura una vez en la solución de la sección 1 (`solutioncomponents`, tipo 20) |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |
| 5 | Comprobación independiente contra el XML exportado (`muestra_rol.py`) | `OK`, y la muestra queda en `playbooks/rol/muestras/` |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**. La herramienta nunca quita un privilegio ni cambia un alcance. Si lo único que pasa es que **faltan** privilegios (una corrida cortada, o una actualización de Microsoft que amplió `App Opener`), se completa con `--completar`; eso lo decide quien dirige la construcción, no el constructor.

## 7. Reversa

`DELETE roles(<roleid>)`. No es posible mientras el rol esté asignado a un usuario o a un team. Nada es irreversible: el nombre, la descripción y los privilegios se pueden cambiar después; pero **cambiar un rol ya asignado cambia lo que esas personas pueden hacer en el momento**. Un usuario no ve los privilegios agregados a un rol ya asignado hasta que se le quita y se le vuelve a asociar el rol (spike 4, hallazgo 1).

## 8. Fuera de alcance

- Asignar el rol a la cuenta de servicio y licenciarla: tarea de administración 13.6, inventario 13. La cuenta recibe además `SR - CARP - Oficial` por estar en la BU (definición línea 393).
- Crear los trabajos recurrentes de BulkDelete y fijar su orden y filtros: inventario 13.12 (diseño detallado pendiente; no son componentes de solución). Qué otros privilegios necesita la cuenta para crearlos o verlos en System Jobs no lo dice la definición; se confirma al crear el primer trabajo en Test, y si falta uno vuelve primero a la matriz.
- Dar al rol acceso a la app model-driven: no corresponde, la cuenta de servicio no abre la app.
- El perfil de seguridad de columna: la cuenta no necesita leer las columnas protegidas (inventario 6.1 y 13.8).
- Los privilegios de ejecución de las Custom API: inventario 8.2 a 8.6 (diseño detallado pendiente). Ninguna API se apoya en este rol.
- La duda D-04 de `diseno/04-matriz-privilegios.md` §5 (borrado en cascada de Queue Item con este rol) se cierra con la prueba de retención en Test.
