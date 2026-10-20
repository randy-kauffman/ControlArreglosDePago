# Playbook: rol · SR - CARP - Oficial

## 1. Identidad

```json
{
  "tipo_playbook": "rol",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "6.2",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. El rol está definido en `diseno/04-matriz-privilegios.md` (columna Ofi, secciones 2 y 3) y figura en `diseno/06-inventario-componentes.md` §6, renglón 6.2 (en la definición: "ARP Oficial").

Advertencias: un rol no lleva etiquetas por idioma (es un texto único), así que `idiomas` solo cumple el formato común. El nombre va sin tildes (BP-PP-197); la descripción es prosa y lleva ortografía completa.

## 2. Qué se crea

```json
{
  "tipo": "rol",
  "nombre": "SR - CARP - Oficial",
  "descripcion": "Oficial de arreglos de pago: gestiona conversaciones, clientes y arreglos del área, y responde correos con la queue del área como remitente. No borra nada.",
  "base": "App Opener",
  "tablas": {
    "sanic_carp_tbl_cliente": {
      "crear": "unidad",
      "leer": "unidad",
      "escribir": "unidad",
      "anexar": "unidad",
      "anexar_a": "unidad"
    },
    "sanic_carp_tbl_conversacion": {
      "leer": "unidad",
      "escribir": "unidad",
      "anexar": "unidad",
      "anexar_a": "unidad"
    },
    "sanic_carp_tbl_arreglo": {
      "crear": "unidad",
      "leer": "unidad",
      "escribir": "unidad",
      "anexar": "unidad",
      "anexar_a": "unidad"
    },
    "sanic_carp_tbl_cuota": {
      "leer": "unidad",
      "escribir": "unidad"
    },
    "sanic_carp_tbl_versionplantilla": {
      "leer": "unidad",
      "anexar_a": "unidad"
    },
    "sanic_carp_tbl_generacionformato": {
      "leer": "unidad"
    },
    "sanic_carp_tbl_mapeocelda": {
      "leer": "unidad"
    }
  },
  "otros_privilegios": {
    "prvCreateActivity": "usuario",
    "prvWriteActivity": "usuario",
    "prvAppendActivity": "usuario",
    "prvReadActivity": "unidad",
    "prvAppendToActivity": "unidad",
    "prvReadQueue": "unidad",
    "prvAppendToQueue": "unidad",
    "prvAppendToUser": "unidad",
    "prvReadRecordAuditHistory": "organizacion"
  }
}
```

Un security role en la unidad de negocio raíz, con los privilegios del rol `App Opener` (los que tenga el entorno en el momento de construir) más los 29 de este playbook (20 sobre tablas de la solución y 9 estándar: 5 de Email, 2 de Queue, `prvAppendToUser` y `prvReadRecordAuditHistory`). Alcances: `usuario` = Basic (solo lo propio) · `unidad` = Local (la BU `Arreglos de Pago`) · `unidad_e_hijas` = Deep · `organizacion` = Global.

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| Descripción del rol | «Oficial de arreglos de pago: gestiona conversaciones, clientes y arreglos del área, y responde correos con la queue del área como remitente. No borra nada.» | La matriz trae el nombre y los privilegios; el texto lo fija el playbook |
| Rol base | `App Opener` | `rol.py` exige un rol base y la convención del estudio es `App Opener` (`rol.md` §1). Duda D-05 de la matriz |

Viene del diseño, no lo decide este playbook:

- Privilegios sobre tablas: `04` §2, columna Ofi (definición líneas 383 a 391, "Profundidad BU", línea 381).
- Privilegios de Email y Queue: `04` §3. Son los del rol `SPK Oficial Correo` probado en el spike 4 con un usuario **no administrador** (9 privilegios: los 7 estándar de acá más Read y AppendTo de Conversación, que acá son los de `sanic_carp_tbl_conversacion`). Leídos de Dev el 2026-10-09 (`RetrieveRolePrivilegesRole`). Email: Create, Write y Append en `usuario`; Read y AppendTo en `unidad` (definición línea 387).
- **No existe un privilegio "Send"**: `SendEmail` exige `prvWriteActivity`. **No hace falta "Send Email as Another User"** (spike 4, D-28). Los privilegios de Email son los de `Activity`, compartidos por todas las actividades (spike 4, hallazgo 2).
- `prvAppendToQueue` lo exige la plataforma para poner la queue como remitente (spike 4, D-28).
- **Adjuntos de correo**: la definición pide "crear y leer" y deja los privilegios para la construcción (línea 388). Leído de Dev el 2026-10-09: `activitymimeattachment` y `attachment` **no tienen privilegios propios**; los cubren `prvCreateActivity` y `prvReadActivity`. No se otorga nada sobre `msdyn_richtextfile` (el editor no la usa, spike 4). Duda D-03 de la matriz: no se probó con un adjunto real.
- Queue Item no tiene privilegios propios (spike 4, hallazgo 3); lo cubre `prvReadQueue`.
- **Sin `asignar`** (prueba de roles del 2026-10-09, `spikes/roles/RESULTADO.md`): crear con el team como dueño no lo necesita; la plataforma exige en cambio que el team dueño lea la tabla. Por eso este rol lee Mapeo (opción A del usuario) aunque no lo edite, y lleva `prvAppendToUser` para los lookups a usuario.
- No tiene `crear` sobre Conversación (las crea el sistema, línea 384; y es el privilegio de ejecución de la reconciliación, solo de Servicio), ni `crear` ni `borrar` sobre Cuota (las altas y bajas las hace el sistema, D-08), ni acceso a Mapeo de celda ni a Mailbox (líneas 390 y 392).
- Nadie humano borra (D-18): ninguna tabla lleva `borrar` y no hay `prvDeleteActivity`.
- Tiene `escribir` sobre Conversación, Arreglo y Cuota, pero qué columnas puede tocar lo limitan los plugins (definición §6 Lógica), no este rol.
- `anexar` y `anexar_a` de los lookups: Arreglo a Cliente, Conversación y Versión de plantilla; Conversación a Cliente; Email a Conversación. La matriz trae los dos lados de cada uno.
- Las Custom API "Tomar", unir e iniciar conversación nueva se restringen con `escribir` de Conversación, y generar formato con `leer` de Generación de formato (línea 400); este rol ya los tiene.
- **View Audit History** (`prvReadRecordAuditHistory`, Global): agregado el 2026-10-20 por decisión del usuario (M7, opción A; `diseno/04-matriz-privilegios.md` §3 y §8). Sin él, el Oficial no admin recibía 403 en `RetrieveRecordChangeHistory` sobre un arreglo que sí lee (`pruebas-humo/tanda2b/RESULTADO.md`). **Solo admite Global** (Dev: `canbeglobal = true`, los otros tres alcances `false`); qué registros ve lo sigue limitando el Read de cada tabla. No se agregan `prvReadAuditSummary` ni `prvReadAuditPartitions` (la vista de resumen de auditoría de la organización no es parte de la app).
- Se parte de `App Opener`, no de `Basic User` (receta de seguridad, §2.1).
- Este rol va al team del sistema de la BU y también lo reciben las cuentas de servicio que están en ella (decisión del 2026-10-09, línea 393).

## 3. Precondiciones

Las de la receta (`seguridad/patrones.md` §2.1), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide en unique name, prefijo y prefijo de opciones; cada idioma está provisionado y el idioma base es uno de ellos | los de la sección 1 |
| 2 | Hay exactamente una unidad de negocio raíz | una |
| 3 | El rol base existe una vez en la unidad raíz | `App Opener` |
| 4 | Cada tabla nombrada existe, y cada privilegio admite el alcance pedido | `sanic_carp_tbl_cliente` · `sanic_carp_tbl_conversacion` · `sanic_carp_tbl_arreglo` · `sanic_carp_tbl_cuota` · `sanic_carp_tbl_versionplantilla` · `sanic_carp_tbl_generacionformato` |
| 5 | Cada privilegio de `otros_privilegios` existe una vez y admite el alcance pedido | `prvCreateActivity` · `prvWriteActivity` · `prvAppendActivity` · `prvReadActivity` · `prvAppendToActivity` · `prvReadQueue` · `prvAppendToQueue` · `prvAppendToUser` · `prvReadRecordAuditHistory` (solo Global) |
| 6 | El nombre no lleva tildes ni ñ (BP-PP-197) y cumple `SR - CARP - Nombre` | el de la sección 2 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/seguridad/patrones.md` §2.1 "Security role". Herramienta del proyecto: `herramientas/construir/rol.py`.

```
python3 herramientas/construir/rol.py playbooks/rol/sr_carp_oficial.md
```

## 5. Verificación

```
python3 herramientas/construir/rol.py playbooks/rol/sr_carp_oficial.md --solo-verificar
python3 herramientas/construir/muestra_rol.py playbooks/rol/sr_carp_oficial.md --guardar
```

Privilegios sobre las tablas de la solución. **Exactamente estos**: uno de menos, uno de más u otro alcance es `difiere`.

| Tabla | Privilegios esperados |
|---|---|
| `sanic_carp_tbl_cliente` | `prvCreatesanic_carp_tbl_cliente` = Local · `prvReadsanic_carp_tbl_cliente` = Local · `prvWritesanic_carp_tbl_cliente` = Local · `prvAppendsanic_carp_tbl_cliente` = Local · `prvAppendTosanic_carp_tbl_cliente` = Local |
| `sanic_carp_tbl_conversacion` | `prvReadsanic_carp_tbl_conversacion` = Local · `prvWritesanic_carp_tbl_conversacion` = Local · `prvAppendsanic_carp_tbl_conversacion` = Local · `prvAppendTosanic_carp_tbl_conversacion` = Local |
| `sanic_carp_tbl_arreglo` | `prvCreatesanic_carp_tbl_arreglo` = Local · `prvReadsanic_carp_tbl_arreglo` = Local · `prvWritesanic_carp_tbl_arreglo` = Local · `prvAppendsanic_carp_tbl_arreglo` = Local · `prvAppendTosanic_carp_tbl_arreglo` = Local |
| `sanic_carp_tbl_cuota` | `prvReadsanic_carp_tbl_cuota` = Local · `prvWritesanic_carp_tbl_cuota` = Local |
| `sanic_carp_tbl_versionplantilla` | `prvReadsanic_carp_tbl_versionplantilla` = Local · `prvAppendTosanic_carp_tbl_versionplantilla` = Local |
| `sanic_carp_tbl_generacionformato` | `prvReadsanic_carp_tbl_generacionformato` = Local |
| `sanic_carp_tbl_mapeocelda` | `prvReadsanic_carp_tbl_mapeocelda` = Local |

Otros privilegios:

| Privilegio | Alcance |
|---|---|
| `prvCreateActivity` | Basic |
| `prvWriteActivity` | Basic |
| `prvAppendActivity` | Basic |
| `prvReadActivity` | Local |
| `prvAppendToUser` | Local |
| `prvAppendToActivity` | Local |
| `prvReadQueue` | Local |
| `prvAppendToQueue` | Local |
| `prvReadRecordAuditHistory` | Global (2026-10-20, M7) |

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET roles` por nombre en la unidad raíz | un rol · `ismanaged = false` · la descripción de la sección 2 |
| 2 | `RetrieveRolePrivilegesRole` | los privilegios de `App Opener` más los de las tablas y los otros de arriba; **ninguno más** (en particular, ningún Delete y ningún privilegio sobre Mapeo de celda ni sobre Mailbox) |
| 3 | Pertenencia a la solución | el rol figura una vez en la solución de la sección 1 (`solutioncomponents`, tipo 20) |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |
| 5 | Comprobación independiente contra el XML exportado (`muestra_rol.py`) | `OK`, y la muestra queda en `playbooks/rol/muestras/` |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**. La herramienta nunca quita un privilegio ni cambia un alcance. Si lo único que pasa es que **faltan** privilegios (una corrida cortada, o una actualización de Microsoft que amplió `App Opener`), se completa con `--completar`; eso lo decide quien dirige la construcción, no el constructor.

## 7. Reversa

`DELETE roles(<roleid>)`. No es posible mientras el rol esté asignado a un usuario o a un team. Nada es irreversible: el nombre, la descripción y los privilegios se pueden cambiar después; pero **cambiar un rol ya asignado cambia lo que esas personas pueden hacer en el momento**. Un usuario no ve los privilegios agregados a un rol ya asignado hasta que se le quita y se le vuelve a asociar el rol (spike 4, hallazgo 1).

## 8. Fuera de alcance

- Asignar el rol al team del sistema de la BU `Arreglos de Pago` (y, por estar en la BU, a las cuentas de servicio): tarea de administración 13.7 (y 13.6), inventario 13. La copia del rol en la BU hija la crea la plataforma sola.
- Dar al rol acceso a la app model-driven: va con la app, inventario 12.25.
- El perfil de seguridad de columna de los datos sensibles y su asociación al team: inventarios 6.1 y 13.8.
- Los privilegios de ejecución de las Custom API: inventario 8.2 a 8.6 (diseño detallado pendiente). La definición los apoya en privilegios de tabla que este rol ya tiene (Write de Conversación, Read de Generación de formato); si el diseño detallado pide otro, vuelve primero a la matriz.
- Las dudas D-02, D-03, D-06, D-07 y D-08 de `diseno/04-matriz-privilegios.md` §5 se cierran con pruebas de comportamiento después de construir, con un usuario no administrador.
