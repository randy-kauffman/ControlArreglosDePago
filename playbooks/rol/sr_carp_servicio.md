# Playbook: rol · SR - CARP - Servicio

## 1. Identidad

```json
{
  "tipo_playbook": "rol",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "6.4",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. El rol está definido en `diseno/04-matriz-privilegios.md` (columna Ser, secciones 2 y 3) y figura en `diseno/06-inventario-componentes.md` §6, renglón 6.4 (en la definición: "ARP Servicio").

Advertencias: un rol no lleva etiquetas por idioma (es un texto único), así que `idiomas` solo cumple el formato común. El nombre va sin tildes (BP-PP-197); la descripción es prosa y lleva ortografía completa.

## 2. Qué se crea

```json
{
  "tipo": "rol",
  "nombre": "SR - CARP - Servicio",
  "descripcion": "Cuenta de servicio de los flujos y de la reconciliación: lee el buzón, los correos, las conversaciones y los arreglos, y crea conversaciones (privilegio de ejecución de la Custom API de reconciliación). No escribe cuotas ni borra nada.",
  "base": "App Opener",
  "tablas": {
    "sanic_carp_tbl_conversacion": {
      "crear": "unidad",
      "leer": "unidad"
    },
    "sanic_carp_tbl_arreglo": {
      "leer": "unidad"
    }
  },
  "otros_privilegios": {
    "prvReadMailbox": "unidad",
    "prvReadActivity": "unidad",
    "prvReadQueue": "unidad"
  }
}
```

Un security role en la unidad de negocio raíz, con los privilegios del rol `App Opener` (los que tenga el entorno en el momento de construir) más los 6 de este playbook (3 sobre tablas de la solución y 3 estándar). Alcances: `usuario` = Basic (solo lo propio) · `unidad` = Local (la BU `Arreglos de Pago`) · `unidad_e_hijas` = Deep · `organizacion` = Global.

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Descripción del rol | «Cuenta de servicio de los flujos y de la reconciliación: lee el buzón, los correos, las conversaciones y los arreglos, y crea conversaciones (privilegio de ejecución de la Custom API de reconciliación). No escribe cuotas ni borra nada.» | La matriz trae el nombre y los privilegios; el texto lo fija el playbook |
| Rol base | `App Opener` | `rol.py` exige un rol base y la convención del estudio es `App Opener` (`rol.md` §1). Ya trae `prvFlow` y `prvWorkflowExecution` (ejecutar flujos), que la definición no repite. Duda D-05 de la matriz |
| `prvReadQueue` como "lectura de Queue Item" | Local | Queue Item no tiene privilegios propios (Dev, 2026-10-09); con `prvReadQueue` se ven sus Queue Items (spike 4, hallazgo 3) |

Viene del diseño, no lo decide este playbook:

- Privilegios: `04` §2 y §3, columna Ser. Definición línea 395: "lectura sobre Mailbox, Email, Queue Item, Conversación y Arreglo; Create sobre Conversación, que es el privilegio de ejecución de la Custom API de reconciliación; escritura sobre Cuota solo si se activa el respaldo de D-11".
- **No lleva escritura sobre Cuota**: el respaldo de D-11 no se activa (el spike 2 mostró que el operador de fecha relativa sirve; inventario 11.3, condicional y no se construye). Si se activa, se agrega `escribir` de Cuota a la matriz y después acá.
- `prvReadActivity` (Email), `prvReadQueue` y la lectura de Conversación y Arreglo **ya las trae el rol Oficial**, que las cuentas de servicio reciben por estar en la BU (definición línea 393). Se repiten acá porque la definición las lista en este rol y porque este rol debe poder ser comprendido solo. No suman privilegio.
- `prvReadMailbox`: el Oficial no tiene acceso al Mailbox; Servicio lo lee para el flujo que avisa el estado del buzón (inventario 11.1 y 11.2).
- `crear` de Conversación es el privilegio de ejecución de la Custom API de reconciliación (D-17, línea 400): solo este rol lo tiene. La conversación la crea el sistema con el plugin; el rol no escribe el propietario.
- No tiene `anexar`, `anexar_a` ni `asignar` sobre ninguna tabla: la definición no se los da.
- Nadie borra en este rol (D-18): el borrado es del rol Purga.
- Las columnas solo-sistema las protege el plugin por la identidad de la cuenta de servicio (variable de entorno del Application ID, inventario 10.7), no este rol.

## 3. Precondiciones

Las de la receta (`seguridad/patrones.md` §2.1), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide en unique name, prefijo y prefijo de opciones; cada idioma está provisionado y el idioma base es uno de ellos | los de la sección 1 |
| 2 | Hay exactamente una unidad de negocio raíz | una |
| 3 | El rol base existe una vez en la unidad raíz | `App Opener` |
| 4 | Cada tabla nombrada existe, y cada privilegio admite el alcance pedido | `sanic_carp_tbl_conversacion` · `sanic_carp_tbl_arreglo` |
| 5 | Cada privilegio de `otros_privilegios` existe una vez y admite el alcance pedido | `prvReadMailbox` · `prvReadActivity` · `prvReadQueue` |
| 6 | El nombre no lleva tildes ni ñ (BP-PP-197) y cumple `SR - CARP - Nombre` | el de la sección 2 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/seguridad/patrones.md` §2.1 "Security role". Herramienta del proyecto: `herramientas/construir/rol.py`.

```
python3 herramientas/construir/rol.py playbooks/rol/sr_carp_servicio.md
```

## 5. Verificación

```
python3 herramientas/construir/rol.py playbooks/rol/sr_carp_servicio.md --solo-verificar
python3 herramientas/construir/muestra_rol.py playbooks/rol/sr_carp_servicio.md --guardar
```

Privilegios sobre las tablas de la solución. **Exactamente estos**: uno de menos, uno de más u otro alcance es `difiere`.

| Tabla | Privilegios esperados |
|---|---|
| `sanic_carp_tbl_conversacion` | `prvCreatesanic_carp_tbl_conversacion` = Local · `prvReadsanic_carp_tbl_conversacion` = Local |
| `sanic_carp_tbl_arreglo` | `prvReadsanic_carp_tbl_arreglo` = Local |

Otros privilegios:

| Privilegio | Alcance |
|---|---|
| `prvReadMailbox` | Local |
| `prvReadActivity` | Local |
| `prvReadQueue` | Local |

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET roles` por nombre en la unidad raíz | un rol · `ismanaged = false` · la descripción de la sección 2 |
| 2 | `RetrieveRolePrivilegesRole` | los privilegios de `App Opener` más los 6 de arriba; **ninguno más** (en particular, ningún privilegio de escritura sobre Cuota, ningún Delete, ningún privilegio de Cliente) |
| 3 | Pertenencia a la solución | el rol figura una vez en la solución de la sección 1 (`solutioncomponents`, tipo 20) |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |
| 5 | Comprobación independiente contra el XML exportado (`muestra_rol.py`) | `OK`, y la muestra queda en `playbooks/rol/muestras/` |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**. La herramienta nunca quita un privilegio ni cambia un alcance. Si lo único que pasa es que **faltan** privilegios (una corrida cortada, o una actualización de Microsoft que amplió `App Opener`), se completa con `--completar`; eso lo decide quien dirige la construcción, no el constructor.

## 7. Reversa

`DELETE roles(<roleid>)`. No es posible mientras el rol esté asignado a un usuario o a un team. Nada es irreversible: el nombre, la descripción y los privilegios se pueden cambiar después; pero **cambiar un rol ya asignado cambia lo que esas personas pueden hacer en el momento**. Un usuario no ve los privilegios agregados a un rol ya asignado hasta que se le quita y se le vuelve a asociar el rol (spike 4, hallazgo 1).

## 8. Fuera de alcance

- Asignar el rol a la cuenta de servicio y licenciarla: tareas de administración 13.6 (y 13.7), inventario 13. La cuenta recibe además `SR - CARP - Oficial` por estar en la BU (definición línea 393).
- Dar al rol acceso a la app model-driven: no corresponde, la cuenta de servicio no abre la app.
- El perfil de seguridad de columna: la cuenta de servicio no necesita leer las columnas protegidas (inventario 6.1 y 13.8).
- Los privilegios de ejecución de las Custom API: inventario 8.2 a 8.6 (diseño detallado pendiente). La definición apoya la API de reconciliación (8.3) en `crear` de Conversación, que este rol ya tiene; el registro de la API con su `ExecutePrivilegeName` es del grupo 8.
- Ser dueña de los flujos y ejecutarlos (11.1 y 11.2): `prvFlow` y `prvWorkflowExecution` vienen de `App Opener`. Qué más necesitan esos flujos para correr (por ejemplo leer environment variables o connection references) no lo dice la definición; se confirma cuando existan los flujos, y si falta un privilegio vuelve primero a la matriz.
