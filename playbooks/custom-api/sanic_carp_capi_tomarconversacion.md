# Playbook: custom-api · sanic_carp_capi_tomarconversacion

> **Piloto de un tipo con 0 ciclos** (`power-platform-especificar`, `references/custom-api.md`). Casi todo lo que este playbook fija es **irreversible**: leer la tabla de decisiones y los huecos de diseño antes de construir.

## 1. Identidad

```json
{
  "tipo_playbook": "custom-api",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "8.4",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. La API está definida en `diseno/03-contratos-custom-api.md` §0.1 (reglas de todas las Custom API), §0.2 (identidad) y §1.3 (contrato), y figura en `diseno/06-inventario-componentes.md` §8, renglón 8.4.

Advertencias: en Dev hay otro publisher con el mismo prefijo `sanic` («Sanic Corp»): la herramienta se detiene si el publisher de la solución no es el de arriba. El nombre de la API dejó de ser provisional: lo fija `diseno/03` §1.3. `version_skill` es la de la skill al escribir este piloto; la referencia del tipo se agregó el 2026-10-20 sin subir la versión de la skill.

## 2. Qué se crea

```json
{
  "tipo": "custom-api",
  "nombre": "sanic_carp_capi_tomarconversacion",
  "displayname": "CAPI - CARP - Tomar conversacion",
  "descripcion": "Asigna la conversación abierta al oficial que llama, con control de concurrencia optimista. Devuelve el oficial asignado y si ya lo estaba.",
  "paquete": "sanic_carp_pkg_plugins",
  "plugintype": "Sanic.Carp.Plugins.Api.TomarConversacionApi",
  "esfuncion": false,
  "esprivada": false,
  "binding": "Global",
  "pasosdeterceros": "None",
  "habilitadaenflujos": false,
  "privilegio": "prvWritesanic_carp_tbl_conversacion",
  "entrada": [
    {
      "nombre": "conversacionid",
      "displayname": "CAPI_IP - CARP - Conversacion",
      "descripcion": "Identificador de la conversación que se quiere tomar.",
      "tipo": "Guid",
      "opcional": true
    }
  ],
  "salida": [
    {
      "nombre": "oficialasignadoid",
      "displayname": "CAPI_OP - CARP - Oficial asignado",
      "descripcion": "Oficial que quedó asignado a la conversación: el usuario que llama.",
      "tipo": "Guid"
    },
    {
      "nombre": "oficialnombre",
      "displayname": "CAPI_OP - CARP - Nombre del oficial",
      "descripcion": "Nombre completo del oficial asignado; vacío si no se pudo leer.",
      "tipo": "String"
    },
    {
      "nombre": "yaasignada",
      "displayname": "CAPI_OP - CARP - Ya asignada",
      "descripcion": "Verdadero si ya estaba asignada al llamador y no se cambió nada.",
      "tipo": "Boolean"
    }
  ]
}
```

Una Custom API **Action** (`IsFunction = false`: escribe algo y depende de la identidad), **global** (unbound), pública (`IsPrivate = false`: con `true` el conector de Dataverse de Power Automate no la resuelve y el flujo de salud, inventario 11.2, no podría activarse; no es una medida de seguridad, la seguridad es el privilegio), sin steps de terceros (`AllowedCustomProcessingStepType = None`: `diseno/03` §0.1). La API **no eleva**: lee y escribe con la identidad de quien llama, y el actor (`InitiatingUserId`) queda en `sanic_oficialasignadoid` (`diseno/03` §0.2 y §1.3, decisión 1). El nombre corto de cada parámetro es el que viaja en la llamada; el `name` largo (`sanic_carp_capiip_tomarconversacion_conversacionid`, `…capiop_tomarconversacion_…`) lo calcula la herramienta.

**Quién puede ejecutarla.** El privilegio `prvWritesanic_carp_tbl_conversacion` lo tiene el rol Oficial (alcance unidad); el Jefe y las cuentas de servicio y de purga lo reciben por el rol Oficial que el team del sistema de la BU les da. Aceptado el 2026-10-10 (OA-12, inconsistencia 11): las cuentas de servicio y de purga pueden ejecutarla aunque no tenga sentido de negocio para ellas.

Decisiones que este playbook toma y el diseño no traía:

| Decisión | Valor | Por qué |
|---|---|---|
| `habilitadaenflujos` | `false` | Decisión del usuario, 2026-10-20 (registrada en `diseno/03` §0.1); antes era un hueco de diseño. `false` es el valor por defecto de la plataforma; el diseño solo usa flujos de nube con el conector de Dataverse. **Irreversible** |
| `displayname` de la API | «CAPI - CARP - Tomar conversacion» | El patrón es `CAPI - CARP - <Nombre>` y el diseño da el ejemplo «CAPI - CARP - Generar formato»; el texto «Tomar conversacion» sale del nombre de la API, sin tilde (BP-PP-197) |
| `descripcion` de la API y de los parámetros, y `displayname` de los parámetros | Los de arriba | `diseno/03` §0.1 da el patrón de los nombres visibles pero no los textos; los de las salidas salen de las notas de la tabla de §1.3 («Quien quedó asignado (el llamador)», «Nombre completo de ese usuario», «`true` si ya estaba asignada al propio llamador»). Las descripciones de las salidas miden 100 caracteres o menos (tope de la columna) |
| `opcional` de `conversacionid` | `true` | Decisión del usuario, 2026-10-20: el plugin responde `CARP-TOM-001` («Falta la conversación que se quiere tomar») cuando el parámetro falta (`TomarConversacionLogica.cs:55`: `if (!pedido.ConversacionId.HasValue) throw CodigosDeError.TOM_001.Crear()`; `ContextoDeApi.LeerGuid`, `ContextoDeApi.cs:97-104`, trata «ausente», `null` y `Guid.Empty` como ausente). Con `opcional = false` la plataforma rechazaría una llamada sin `conversacionid` antes de ejecutar el plugin, con un error técnico en inglés y no con el código del catálogo (spike 1b del 2026-10-08, `spikes/sandbox/RESULTADO.md`: «el error lo produce la capa OData antes del plugin»). **Difiere de `diseno/03` §1.3** («Req: sí»), que describe lo que el usuario necesita pasar, no cómo se declara. `opcional` es inmutable. Cierra el hueco que tenía este piloto |

Huecos de diseño que este playbook no cierra:

- **Etiquetas por idioma.** `displayname` y `description` de la API y de cada parámetro son columnas localizables; la herramienta escribe una cadena única (en español, sin tildes en el nombre). La regla de etiquetas en 1033 y 3082 no se cumple para este tipo hasta que la herramienta lo soporte.

## 3. Precondiciones

| # | Qué tiene que cumplirse | Consulta | Esperado |
|---|---|---|---|
| 1 | La solución existe, no es managed, su publisher coincide en unique name, prefijo y prefijo de opciones; cada idioma está provisionado y el idioma base es uno de ellos | `solutions?$filter=uniquename eq '<solucion>'` con el publisher expandido; `organizations`; `RetrieveProvisionedLanguages` | los de la sección 1 |
| 2 | El tipo de plugin está registrado **dentro del paquete `paquete`**, una sola vez (un tipo con el mismo nombre en otro paquete no cuenta) | `pluginpackages` por `uniquename` → `pluginassemblies` por `_packageid_value` → `plugintypes` por `_pluginassemblyid_value` | una fila con `Sanic.Carp.Plugins.Api.TomarConversacionApi` (la crea el playbook del paquete, inventario 8.1) |
| 3 | El privilegio de ejecución existe. **A mano: la herramienta no lo comprueba** | `privileges?$select=name&$filter=name eq 'prvWritesanic_carp_tbl_conversacion'` | una fila (nace con la tabla `sanic_carp_tbl_conversacion`, inventario 3.2) |
| 4 | La API no existe con otro valor en un campo inmutable | `customapis?$filter=uniquename eq 'sanic_carp_capi_tomarconversacion'` | ninguna fila, o una igual (sección 6) |
| 5 | Nombre y nombres visibles sin tildes ni ñ; nombres de parámetros en minúsculas y dígitos | sin red | los de la sección 2 |
| 6 | **El dueño confirmó `habilitadaenflujos` y el `opcional` de la sección 2** (decisiones del 2026-10-20) | decisión escrita en `diseno/03` §0.1 | confirmación; mientras no esté, el constructor no corre |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/dataverse/patrones.md`, «Custom API — construirlo desde un playbook». Herramienta del proyecto: `herramientas/construir/custom_api.py`.

```
python3 herramientas/construir/custom_api.py playbooks/custom-api/sanic_carp_capi_tomarconversacion.md
```

## 5. Verificación

```
python3 herramientas/construir/custom_api.py playbooks/custom-api/sanic_carp_capi_tomarconversacion.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET customapis` por `uniquename` | una fila · `ismanaged = false` · `name` y `uniquename` `sanic_carp_capi_tomarconversacion` · `displayname` y `description` de la sección 2 · `bindingtype = 0` (Global) · `isfunction = false` · `isprivate = false` · `allowedcustomprocessingsteptype = 0` (None) · `workflowsdkstepenabled = false` · `executeprivilegename = prvWritesanic_carp_tbl_conversacion` · plugin `Sanic.Carp.Plugins.Api.TomarConversacionApi` |
| 2 | Parámetros de entrada (`customapirequestparameters`) | **exactamente uno**: `conversacionid`, Guid (12), `isoptional = true`, `name` `sanic_carp_capiip_tomarconversacion_conversacionid`, y `displayname` y `description` de la sección 2 |
| 3 | Propiedades de salida (`customapiresponseproperties`) | **exactamente tres**: `oficialasignadoid` Guid (12), `oficialnombre` String (10), `yaasignada` Boolean (0), cada una con su `name` `sanic_carp_capiop_tomarconversacion_<nombre>`, `displayname` y `description`. Una de más o una de menos es `difiere` |
| 4 | Pertenencia a la solución | la API figura en `solutioncomponents` de la solución; **se informa el `componenttype`** que puso la plataforma |
| 5 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**. Si lo que difiere es un campo inmutable (`uniquename`, `bindingtype`, `isfunction`, `allowedcustomprocessingsteptype`, `workflowsdkstepenabled`; tipo u opcionalidad de un parámetro), la herramienta lo dice y no hay corrección posible: **hay que borrar la API entera y rehacerla**, decisión de quien dirige. Con `--completar` la herramienta solo corrige lo mutable (nombres visibles, descripción, privilegio, `isprivate`, plugin) y agrega parámetros que falten; nunca borra ni pisa un campo inmutable. Un parámetro que sobra se denuncia con su nombre.

## 7. Reversa

`DELETE customapis(<customapiid>)`, con sus parámetros y propiedades. **Irreversible desde la primera llamada**: `uniquename`, `bindingtype`, `isfunction`, `allowedcustomprocessingsteptype`, `workflowsdkstepenabled`, y de cada parámetro `uniquename`, `type`, `isoptional`. Borrarla rompe todo lo que la llame (el web resource de comandos, inventario 12.22, y el flujo que se escriba después).

## 8. Fuera de alcance

- Registrar el paquete y el tipo `TomarConversacionApi`: inventario 8.1 (`playbooks/paquete-plugins/Sanic.Carp.Plugins.md`).
- Dar el privilegio `prvWritesanic_carp_tbl_conversacion` a los roles: inventario 6.2 (Oficial); el Jefe y las cuentas de servicio y purga lo reciben por el Oficial (6.3 a 6.5).
- El step que acepta el `Update` de esta API (de vacío a uno mismo): `ConversacionAsignacionStep`, inventario 8.7.
- El web resource que la invoca: inventario 12.22.
- Las otras cuatro Custom API: inventario 8.2, 8.3, 8.5 y 8.6.
