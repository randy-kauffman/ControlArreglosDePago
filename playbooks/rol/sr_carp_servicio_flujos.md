# Playbook: rol · SR - CARP - Servicio flujos

## 1. Identidad

```json
{
  "tipo_playbook": "rol",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "6.6",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. El rol sale de `diseno/08-alternativa-correo-por-flujos.md` §5 (fila "Rol `SR - CARP - Servicio`") y §11 (notas de F3), y de leer qué tablas tocan los flujos `playbooks/flujo/carp-ent.json` y `carp-snd.json`. Figura en `diseno/06-inventario-componentes.md` §6, renglón 6.6.

## 2. Qué se crea

```json
{
  "tipo": "rol",
  "nombre": "SR - CARP - Servicio flujos",
  "descripcion": "Solo para el modo flujos del correo (plan B): se suma a SR - CARP - Servicio en la cuenta dueña de las conexiones de los flujos ENT y SND. Crea, escribe y anexa correos en la unidad de negocio, los anexa a conversaciones y los pone en la cola del area. No borra ni asigna nada.",
  "base": "App Opener",
  "tablas": {
    "sanic_carp_tbl_conversacion": {
      "anexar_a": "unidad"
    }
  },
  "otros_privilegios": {
    "prvCreateActivity": "unidad",
    "prvWriteActivity": "unidad",
    "prvAppendActivity": "unidad",
    "prvAppendToQueue": "unidad"
  }
}
```

Un security role en la unidad de negocio raíz, con los privilegios del rol `App Opener` más los 5 de este playbook (1 sobre una tabla de la solución y 4 estándar). Alcances: `usuario` = Basic · `unidad` = Local (la BU `Arreglos de Pago`) · `unidad_e_hijas` = Deep · `organizacion` = Global.

**Es un rol aditivo y temporal**: se asigna **además** de `SR - CARP - Servicio`, solo a la cuenta dueña de las conexiones de ENT y SND, y solo en un entorno en modo flujos. No se asigna a ningún team ni a ningún usuario humano. Decisión del usuario (2026-11-03): no se modifica `SR - CARP - Servicio`.

### Cómo se derivó (qué tocan los flujos)

| Qué hacen los flujos | Tabla | Privilegio | Lo tiene Servicio | Este rol |
|---|---|---|---|---|
| ENT crea el email entrante con dueño = team del área, partes `from` y `to` (cola) y lo cierra Received; SND lo actualiza (`statecode`, `statuscode`, `messageid`) | Email (Activity) | Create, Write | No | Create y Write, Local |
| El email referencia a la cola (parte `to`) y a la conversación (`regardingobjectid`); ENT crea los adjuntos con `objectid_activitypointer` | Email (Activity) | Append | No | Append, Local |
| ENT pone el email en la cola del área (`queueitems` con `objectid_email`) y la cola es parte del email | Queue | AppendTo | No | AppendTo, Local |
| ENT enlaza el email a la conversación de la que viene el hilo | Conversación | AppendTo | No (Create y Read) | AppendTo, Local |
| ENT y SND leen emails, partes, adjuntos, conversaciones, colas, teams y BU | Activity, Conversación, Queue, Team, BU | Read | Sí (Servicio y `App Opener`) | nada |
| `activitymimeattachment`, `queueitem` | — | no tienen privilegios propios (`diseno/04` R-01 y R-02) | — | nada |
| Variables de entorno y connection references | — | las resuelve la plataforma al ejecutar el flujo, no el usuario | — | nada |

Decisiones que este playbook toma y `diseno/08` §5 no traía igual:

| Decisión | Valor | Por qué |
|---|---|---|
| `prvAssign` de Email | **No se otorga** | `diseno/08` §5 lo listaba "para dar el email al team". La prueba de roles con un no administrador (`spikes/roles/RESULTADO.md`, C1) probó que crear un registro con el team como dueño **no necesita Assign**. ENT crea el email ya con `ownerid` = team (`item/ownerid_email@odata.bind`) y no reasigna nunca. Si en F4 diera 403 por falta de Assign, vuelve a este playbook |
| `prvAppendToActivity` | No se otorga | Solo hace falta cuando se pone `parentactivityid`; ni ENT ni SND lo hacen (el padre se resuelve por `messageid`, no por lookup) |
| `prvAppendToUser` | No se otorga | El dueño es un team y las partes llevan dirección o cola, nunca un usuario. Es el privilegio que el Oficial necesita para sus lookups a usuario (`diseno/04` §3), no el de los flujos |
| Profundidad | Local (BU) para los 5 | Son emails del team del área, no de la cuenta. Con el rol asignado directo a un usuario, Basic solo alcanza registros propios de ese usuario; el Oficial lo tiene en Basic porque llega por el team. Local no excede la BU. No hace falta Deep ni Global |
| Nombre | `SR - CARP - Servicio flujos` | Sigue `SR - CARP - Nombre` (la herramienta lo valida), sin tildes (BP-PP-197). Dice de qué rol cuelga |
| Rol base | `App Opener` | Igual que Servicio (`sr_carp_servicio.md` §2). Trae `prvFlow` y `prvWorkflowExecution` |

Hallazgo para quien instala: una cuenta de servicio dentro de la BU ya recibe `SR - CARP - Oficial` por el team por defecto (`diseno/04` §1, punto 5), y el Oficial ya trae Create, Write y Append de Email (Basic), AppendTo de Queue (Local) y AppendTo de Conversación (Local). Es probable que Servicio + Oficial ya alcancen para los flujos y este rol sea redundante. Se crea igual, explícito, porque el comportamiento con un usuario no administrador **no se probó** y porque la decisión del usuario es no depender de que el Oficial llegue por el team. Es un subconjunto de lo que la cuenta ya tendría (Local frente al Basic del Oficial en Create, Write y Append): no le da nada nuevo sobre otras tablas. Si F4 confirma que sin este rol los flujos corren, no se asigna.

## 3. Precondiciones

Las de la receta (`seguridad/patrones.md` §2.1), con estos valores esperados:

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, y su publisher coincide en unique name, prefijo y prefijo de opciones; cada idioma está provisionado y el idioma base es uno de ellos | los de la sección 1 |
| 2 | Hay exactamente una unidad de negocio raíz | una |
| 3 | El rol base existe una vez en la unidad raíz | `App Opener` |
| 4 | La tabla nombrada existe, y su privilegio admite el alcance pedido | `sanic_carp_tbl_conversacion` |
| 5 | Cada privilegio de `otros_privilegios` existe una vez y admite el alcance pedido | `prvCreateActivity` · `prvWriteActivity` · `prvAppendActivity` · `prvAppendToQueue` |
| 6 | El nombre no lleva tildes ni ñ (BP-PP-197) y cumple `SR - CARP - Nombre` | el de la sección 2 |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/seguridad/patrones.md` §2.1 "Security role". Herramienta del proyecto: `herramientas/construir/rol.py`.

```
python3 herramientas/construir/rol.py playbooks/rol/sr_carp_servicio_flujos.md
```

## 5. Verificación

```
python3 herramientas/construir/rol.py playbooks/rol/sr_carp_servicio_flujos.md --solo-verificar
python3 herramientas/construir/muestra_rol.py playbooks/rol/sr_carp_servicio_flujos.md --guardar
```

Privilegios sobre las tablas de la solución. **Exactamente estos**: uno de menos, uno de más u otro alcance es `difiere`.

| Tabla | Privilegios esperados |
|---|---|
| `sanic_carp_tbl_conversacion` | `prvAppendTosanic_carp_tbl_conversacion` = Local |

Otros privilegios:

| Privilegio | Alcance |
|---|---|
| `prvCreateActivity` | Local |
| `prvWriteActivity` | Local |
| `prvAppendActivity` | Local |
| `prvAppendToQueue` | Local |

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET roles` por nombre en la unidad raíz | un rol · `ismanaged = false` · la descripción de la sección 2 |
| 2 | `RetrieveRolePrivilegesRole` | los privilegios de `App Opener` más los 5 de arriba; **ninguno más** (en particular, ningún Delete, ningún Assign y ningún privilegio de Cliente) |
| 3 | Pertenencia a la solución | el rol figura una vez en la solución de la sección 1 (`solutioncomponents`, tipo 20) |
| 4 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |
| 5 | Comprobación independiente contra el XML exportado (`muestra_rol.py`) | `OK`, y la muestra queda en `playbooks/rol/muestras/` |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**. La herramienta nunca quita un privilegio ni cambia un alcance. Si lo único que pasa es que **faltan** privilegios, se completa con `--completar`; eso lo decide quien dirige la construcción.

## 7. Reversa

`DELETE roles(<roleid>)`. No es posible mientras el rol esté asignado a un usuario o a un team. Para volver al modo SSS basta quitar la asignación; el rol puede quedar en la solución sin efecto. Un usuario no ve los privilegios agregados a un rol ya asignado hasta que se le quita y se le vuelve a asociar el rol (spike 4, hallazgo 1).

## 8. Fuera de alcance

- **Asignar el rol** a la cuenta dueña de las conexiones de ENT y SND: es un paso de instalación del modo flujos (`manual/procedimiento-instalacion.md`, sección "Modo flujos"), lo hace el administrador y solo en un entorno en modo flujos. Este playbook no asigna a nadie.
- Los privilegios de Callback Registration para el disparador de Dataverse de SND (`diseno/04` D-10, pendiente): no los cubre este rol; se confirman en F4 con la cuenta real.
- Cambios a `SR - CARP - Servicio`: ninguno.
