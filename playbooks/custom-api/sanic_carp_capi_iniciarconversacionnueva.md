# Playbook: custom-api · sanic_carp_capi_iniciarconversacionnueva

> **Tipo con 0 ciclos** (`power-platform-especificar`, `references/custom-api.md`). Casi todo lo que este playbook fija es **irreversible**: leer la tabla de decisiones y los huecos de diseño antes de construir. El molde es el piloto `sanic_carp_capi_tomarconversacion`.

## 1. Identidad

```json
{
  "tipo_playbook": "custom-api",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "8.6",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. La API está definida en `diseno/03-contratos-custom-api.md` §0.1 (reglas de todas las Custom API), §0.2 (identidad) y §1.5 (contrato), y figura en `diseno/06-inventario-componentes.md` §8, renglón 8.6. Los nombres de los parámetros son los que lee y escribe el código (`src/Sanic.Carp.Plugins/Api/IniciarConversacionNuevaApi.cs`, constantes `Parametro*` y `Salida*`).

Advertencias: en Dev hay otro publisher con el mismo prefijo `sanic` («Sanic Corp»): la herramienta se detiene si el publisher de la solución no es el de arriba.

## 2. Qué se crea

```json
{
  "tipo": "custom-api",
  "paquete": "sanic_carp_pkg_plugins",
  "nombre": "sanic_carp_capi_iniciarconversacionnueva",
  "displayname": "CAPI - CARP - Iniciar conversacion nueva",
  "descripcion": "Cuando llegaron correos nuevos a una conversación finalizada, abre una conversación nueva con el primero de ellos y le mueve los correos posteriores. Devuelve la conversación nueva y cuántos correos movió.",
  "plugintype": "Sanic.Carp.Plugins.Api.IniciarConversacionNuevaApi",
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
      "descripcion": "Identificador de la conversación finalizada que tiene el indicador de correos nuevos.",
      "tipo": "Guid",
      "opcional": true
    }
  ],
  "salida": [
    {
      "nombre": "nuevaconversacionid",
      "displayname": "CAPI_OP - CARP - Nueva conversacion",
      "descripcion": "Identificador de la conversación nueva que se creó.",
      "tipo": "Guid"
    },
    {
      "nombre": "correosmovidos",
      "displayname": "CAPI_OP - CARP - Correos movidos",
      "descripcion": "Correos que pasaron de la conversación finalizada a la nueva.",
      "tipo": "Integer"
    }
  ]
}
```

Una Custom API **Action** (`IsFunction = false`: escribe algo y depende de la identidad), **global** (unbound), pública (`IsPrivate = false`: con `true` el conector de Dataverse de Power Automate no la resuelve y el flujo de salud, inventario 11.2, no podría activarse; no es una medida de seguridad, la seguridad es el privilegio), sin steps de terceros (`AllowedCustomProcessingStepType = None`: `diseno/03` §0.1). **Identidad** (`diseno/03` §0.2 y §1.5, decisión 1): valida el acceso a la conversación con el llamador y lee y escribe todo lo demás con SYSTEM. **No hay actor**: no existe una columna de actor para esta acción y el código no lee `InitiatingUserId`; la conversación nueva y los movimientos quedan atribuidos al sistema. El nombre corto de cada parámetro es el que viaja en la llamada; el `name` largo (`sanic_carp_capiip_iniciarconversacionnueva_conversacionid`, `…capiop_iniciarconversacionnueva_<nombre>`) lo calcula la herramienta.

**Quién puede ejecutarla.** El privilegio `prvWritesanic_carp_tbl_conversacion` lo tiene el rol Oficial (alcance unidad); el Jefe y las cuentas de servicio y de purga lo reciben por el rol Oficial que el team del sistema de la BU les da. Aceptado el 2026-10-10 (OA-12, inconsistencia 11): las cuentas de servicio y de purga pueden ejecutarla aunque no tenga sentido de negocio para ellas.

Decisiones que este playbook toma y el diseño no traía, o que el dueño fijó:

| Decisión | Valor | Por qué |
|---|---|---|
| `habilitadaenflujos` | `false` | Decisión del usuario, 2026-10-20 (registrada en `diseno/03` §0.1). Es el valor por defecto de la plataforma; el diseño solo usa flujos de nube con el conector de Dataverse. **Irreversible** |
| `opcional` de `conversacionid` | `true` | Decisión del usuario, 2026-10-20: el plugin responde `CARP-INI-001` («Falta la conversación») cuando el parámetro falta (`IniciarConversacionNuevaLogica.cs:75`: `if (!pedido.ConversacionId.HasValue) throw CodigosDeError.INI_001.Crear()`; `ContextoDeApi.LeerGuid`, `ContextoDeApi.cs:97-104`, trata «ausente», `null` y `Guid.Empty` como ausente). Con `opcional = false` la plataforma rechazaría la llamada antes del plugin con un error técnico en inglés. **Difiere de `diseno/03` §1.5** («Req: sí»). Irreversible |
| `displayname` de la API | «CAPI - CARP - Iniciar conversacion nueva» | El patrón es `CAPI - CARP - <Nombre>`; el texto sale del nombre de la API, sin tilde (BP-PP-197) |
| `descripcion` de la API y de los parámetros, y `displayname` de los parámetros | Los de arriba | `diseno/03` §0.1 da el patrón de los nombres visibles pero no los textos; la nota de §1.5 de las salidas está vacía y se redactó de su nombre. Las descripciones de las salidas miden 100 caracteres o menos (tope de la columna) |
| Tipos | `conversacionid` y `nuevaconversacionid` Guid; `correosmovidos` Integer | `diseno/03` §1.5 y los tipos que lee (`LeerGuid`) y escribe (`Guid`, `int`) `IniciarConversacionNuevaApi.Ejecutar`; ningún id es `EntityReference` (DC-24) |

Discrepancias entre el diseño y el código: ninguna en nombres ni tipos de parámetros. Las dos salidas se escriben siempre (`IniciarConversacionNuevaApi.cs`, `EscribirSalida`). El nombre `correosmovidos` se repite en `unirconversaciones`, pero son API distintas.

Huecos de diseño que este playbook no cierra:

- **Etiquetas por idioma.** `displayname` y `description` de la API y de cada parámetro son columnas localizables; la herramienta escribe una cadena única (en español, sin tildes en el nombre). La regla de etiquetas en 1033 y 3082 no se cumple para este tipo hasta que la herramienta lo soporte.
- **Riesgo sin medir en Dev** (`diseno/03` §1.5, decisión 4): H-19 (a), los steps `ConversacionCrearStep` y `ConversacionActualizarStep` aceptan las escrituras porque ven SYSTEM.

## 3. Precondiciones

| # | Qué tiene que cumplirse | Consulta | Esperado |
|---|---|---|---|
| 1 | La solución existe, no es managed, su publisher coincide en unique name, prefijo y prefijo de opciones; cada idioma está provisionado y el idioma base es uno de ellos | `solutions?$filter=uniquename eq '<solucion>'` con el publisher expandido; `organizations`; `RetrieveProvisionedLanguages` | los de la sección 1 |
| 2 | El tipo de plugin está registrado, una sola vez | `plugintypes?$filter=typename eq 'Sanic.Carp.Plugins.Api.IniciarConversacionNuevaApi'` | una fila (la crea el playbook del paquete, inventario 8.1) |
| 3 | El privilegio de ejecución existe. **A mano: la herramienta no lo comprueba** | `privileges?$select=name&$filter=name eq 'prvWritesanic_carp_tbl_conversacion'` | una fila (nace con la tabla `sanic_carp_tbl_conversacion`, inventario 3.2) |
| 4 | La API no existe con otro valor en un campo inmutable | `customapis?$filter=uniquename eq 'sanic_carp_capi_iniciarconversacionnueva'` | ninguna fila, o una igual (sección 6) |
| 5 | Nombre y nombres visibles sin tildes ni ñ; nombres de parámetros en minúsculas y dígitos; descripciones de salida de 100 caracteres o menos | sin red | los de la sección 2 |
| 6 | **El dueño confirmó `habilitadaenflujos` y el `opcional` de la sección 2** (decisiones del 2026-10-20) | decisión escrita en `diseno/03` §0.1 | confirmación; mientras no esté, el constructor no corre |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/dataverse/patrones.md`, «Custom API — construirlo desde un playbook». Herramienta del proyecto: `herramientas/construir/custom_api.py`.

```
python3 herramientas/construir/custom_api.py playbooks/custom-api/sanic_carp_capi_iniciarconversacionnueva.md
```

## 5. Verificación

```
python3 herramientas/construir/custom_api.py playbooks/custom-api/sanic_carp_capi_iniciarconversacionnueva.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET customapis` por `uniquename` | una fila · `ismanaged = false` · `name` y `uniquename` `sanic_carp_capi_iniciarconversacionnueva` · `displayname` y `description` de la sección 2 · `bindingtype = 0` (Global) · `isfunction = false` · `isprivate = false` · `allowedcustomprocessingsteptype = 0` (None) · `workflowsdkstepenabled = false` · `executeprivilegename = prvWritesanic_carp_tbl_conversacion` · plugin `Sanic.Carp.Plugins.Api.IniciarConversacionNuevaApi` |
| 2 | Parámetros de entrada (`customapirequestparameters`) | **exactamente uno**: `conversacionid`, Guid (12), `isoptional = true`, `name` `sanic_carp_capiip_iniciarconversacionnueva_conversacionid`, y `displayname` y `description` de la sección 2 |
| 3 | Propiedades de salida (`customapiresponseproperties`) | **exactamente dos**: `nuevaconversacionid` Guid (12) y `correosmovidos` Integer (7), cada una con su `name` `sanic_carp_capiop_iniciarconversacionnueva_<nombre>`, `displayname` y `description`. Una de más o una de menos es `difiere` |
| 4 | Pertenencia a la solución | la API figura en `solutioncomponents` de la solución; **se informa el `componenttype`** que puso la plataforma |
| 5 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**. Si lo que difiere es un campo inmutable (`uniquename`, `bindingtype`, `isfunction`, `allowedcustomprocessingsteptype`, `workflowsdkstepenabled`; tipo u opcionalidad de un parámetro), la herramienta lo dice y no hay corrección posible: **hay que borrar la API entera y rehacerla**, decisión de quien dirige. Con `--completar` la herramienta solo corrige lo mutable (nombres visibles, descripción, privilegio, `isprivate`, plugin) y agrega parámetros que falten; nunca borra ni pisa un campo inmutable. Un parámetro que sobra se denuncia con su nombre.

## 7. Reversa

`DELETE customapis(<customapiid>)`, con sus parámetros y propiedades. **Irreversible desde la primera llamada**: `uniquename`, `bindingtype`, `isfunction`, `allowedcustomprocessingsteptype`, `workflowsdkstepenabled`, y de cada parámetro `uniquename`, `type`, `isoptional`. Borrarla rompe todo lo que la llame (el web resource de comandos, inventario 12.22).

## 8. Fuera de alcance

- Registrar el paquete y el tipo `IniciarConversacionNuevaApi`: inventario 8.1 (`playbooks/paquete-plugins/Sanic.Carp.Plugins.md`).
- Dar el privilegio `prvWritesanic_carp_tbl_conversacion` a los roles: inventario 6.2 (Oficial); el Jefe y las cuentas de servicio y purga lo reciben por el Oficial (6.3 a 6.5).
- Los steps de propietario y de Conversación (`ConversacionCrearStep` y `ConversacionActualizarStep`) que aceptan las escrituras del sistema de esta API: inventario 8.7.
- El web resource que la invoca: inventario 12.22.
- Las otras cuatro Custom API: inventario 8.2 a 8.5.
