# Playbook: custom-api · sanic_carp_capi_generarformato

> **Tipo con 0 ciclos** (`power-platform-especificar`, `references/custom-api.md`). Casi todo lo que este playbook fija es **irreversible**: leer la tabla de decisiones y los huecos de diseño antes de construir. El molde es el piloto `sanic_carp_capi_tomarconversacion`.

## 1. Identidad

```json
{
  "tipo_playbook": "custom-api",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "8.2",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. La API está definida en `diseno/03-contratos-custom-api.md` §0.1 (reglas de todas las Custom API), §0.2 (identidad) y §1.1 (contrato), y figura en `diseno/06-inventario-componentes.md` §8, renglón 8.2. Los nombres de los parámetros son los que lee y escribe el código (`src/Sanic.Carp.Plugins/Api/GenerarFormatoApi.cs`, constantes `Parametro*` y `Salida*`).

Advertencias: en Dev hay otro publisher con el mismo prefijo `sanic` («Sanic Corp»): la herramienta se detiene si el publisher de la solución no es el de arriba.

## 2. Qué se crea

```json
{
  "tipo": "custom-api",
  "paquete": "sanic_carp_pkg_plugins",
  "nombre": "sanic_carp_capi_generarformato",
  "displayname": "CAPI - CARP - Generar formato",
  "descripcion": "Genera el formato F-NIC-0000052 en Excel de un arreglo y lo devuelve en Base64; si el arreglo no estaba congelado, lo congela. Con una versión previa solo hace la vista previa, sin escribir nada.",
  "plugintype": "Sanic.Carp.Plugins.Api.GenerarFormatoApi",
  "esfuncion": false,
  "esprivada": false,
  "binding": "Global",
  "pasosdeterceros": "None",
  "habilitadaenflujos": false,
  "privilegio": "prvReadsanic_carp_tbl_generacionformato",
  "entrada": [
    {
      "nombre": "arregloid",
      "displayname": "CAPI_IP - CARP - Arreglo",
      "descripcion": "Identificador del arreglo cuyo formato se quiere generar.",
      "tipo": "Guid",
      "opcional": true
    },
    {
      "nombre": "versionpreviaid",
      "displayname": "CAPI_IP - CARP - Version previa",
      "descripcion": "Versión de plantilla en Borrador con la que se hace una vista previa. Si falta, se genera el formato real.",
      "tipo": "Guid",
      "opcional": true
    }
  ],
  "salida": [
    {
      "nombre": "archivo",
      "displayname": "CAPI_OP - CARP - Archivo",
      "descripcion": "Libro de Excel (.xlsx) del formato, codificado en Base64.",
      "tipo": "String"
    },
    {
      "nombre": "nombrearchivo",
      "displayname": "CAPI_OP - CARP - Nombre del archivo",
      "descripcion": "Nombre del archivo: F-NIC-0000052 y el código del arreglo. No lleva datos del cliente.",
      "tipo": "String"
    },
    {
      "nombre": "numeroprovisional",
      "displayname": "CAPI_OP - CARP - Numero provisional",
      "descripcion": "Número impreso en un Borrador; 0 si no aplica.",
      "tipo": "Integer"
    },
    {
      "nombre": "congelado",
      "displayname": "CAPI_OP - CARP - Congelado",
      "descripcion": "Verdadero si, al terminar, el arreglo está congelado (por esta llamada o antes).",
      "tipo": "Boolean"
    },
    {
      "nombre": "advertencias",
      "displayname": "CAPI_OP - CARP - Advertencias",
      "descripcion": "Avisos del generador, uno por línea. Vacío si no hay.",
      "tipo": "String"
    }
  ]
}
```

Una Custom API **Action** (`IsFunction = false`: escribe algo y depende de la identidad), **global** (unbound), pública (`IsPrivate = false`: con `true` el conector de Dataverse de Power Automate no la resuelve y el flujo de salud, inventario 11.2, no podría activarse; no es una medida de seguridad, la seguridad es el privilegio), sin steps de terceros (`AllowedCustomProcessingStepType = None`: `diseno/03` §0.1). **Identidad** (`diseno/03` §0.2 y §1.1): lee con el llamador (arreglo, cuotas, cliente) y escribe con SYSTEM (congelamiento, instantánea, número provisional y bitácora); el actor (`InitiatingUserId`) se captura al inicio, antes de crear ningún servicio, y queda explícito en `sanic_generadoporid`. El nombre corto de cada parámetro es el que viaja en la llamada; el `name` largo (`sanic_carp_capiip_generarformato_<nombre>`, `…capiop_generarformato_<nombre>`) lo calcula la herramienta.

**Quién puede ejecutarla.** El privilegio `prvReadsanic_carp_tbl_generacionformato` lo tienen los roles Oficial y Purga (lectura de Generación de formato); el Jefe y las cuentas de servicio lo reciben por el rol Oficial que el team del sistema de la BU les da (`diseno/03` §0.1). Aceptado el 2026-10-10 (OA-12, inconsistencia 11): las cuentas de servicio y de purga pueden ejecutarla aunque no tenga sentido de negocio para ellas.

Decisiones que este playbook toma y el diseño no traía, o que el dueño fijó:

| Decisión | Valor | Por qué |
|---|---|---|
| `habilitadaenflujos` | `false` | Decisión del usuario, 2026-10-20 (registrada en `diseno/03` §0.1). Es el valor por defecto de la plataforma; el diseño solo usa flujos de nube con el conector de Dataverse. **Irreversible** |
| `opcional` de `arregloid` | `true` | Decisión del usuario, 2026-10-20: el plugin responde `CARP-GEN-001` («Falta el arreglo que se quiere imprimir») cuando el parámetro falta (`GenerarFormatoLogica.cs:140`: `if (!pedido.ArregloId.HasValue) throw CodigosDeError.GEN_001.Crear()`; `ContextoDeApi.LeerGuid`, `ContextoDeApi.cs:97-104`, trata «ausente», `null` y `Guid.Empty` como ausente). Con `opcional = false` la plataforma rechazaría la llamada antes del plugin con un error técnico en inglés y el código `-001` solo se alcanzaría con un Guid vacío (spike 1b, `spikes/sandbox/RESULTADO.md`). **Difiere de `diseno/03` §1.1** («Req: sí»), que describe lo que el usuario necesita pasar, no cómo se declara. Irreversible |
| `opcional` de `versionpreviaid` | `true` | `diseno/03` §1.1 («Req: no»): su ausencia significa «no es vista previa» (`GenerarFormatoApi.cs`, `LeerGuid(ParametroVersionPreviaId)`) |
| `displayname` de la API | «CAPI - CARP - Generar formato» | El patrón es `CAPI - CARP - <Nombre>` y `diseno/03` §0.1 da justo este ejemplo; sin tilde (BP-PP-197) |
| `descripcion` de la API y de los parámetros, y `displayname` de los parámetros | Los de arriba | `diseno/03` §0.1 da el patrón de los nombres visibles pero no los textos; los de las salidas salen de las notas de la tabla de §1.1. Las descripciones de las salidas miden 100 caracteres o menos (tope de la columna) |
| Tipos | `arregloid` y `versionpreviaid` Guid; `archivo`, `nombrearchivo` y `advertencias` String; `numeroprovisional` Integer; `congelado` Boolean | `diseno/03` §1.1 y los tipos que escribe `GenerarFormatoApi.Ejecutar` (`string`, `string`, `int`, `bool`, `string`); ningún id es `EntityReference` (DC-24) |

Discrepancias entre el diseño y el código: ninguna en nombres ni tipos de parámetros. Las cinco salidas se escriben siempre (`GenerarFormatoApi.cs`, `EscribirSalida`); si la lógica lanza, no se escribe ninguna.

Huecos de diseño que este playbook no cierra:

- **Etiquetas por idioma.** `displayname` y `description` de la API y de cada parámetro son columnas localizables; la herramienta escribe una cadena única (en español, sin tildes en el nombre). La regla de etiquetas en 1033 y 3082 no se cumple para este tipo hasta que la herramienta lo soporte.
- **Quién ejecuta la API según el inventario.** El renglón 8.2 dice «Oficial y Jefe»; `diseno/03` §0.1 dice Oficial y Purga por rol, y Jefe y Servicio por el Oficial. El playbook sigue a `diseno/03`; el inventario conviene alinearlo.
- **Mensaje técnico ante un tipo equivocado.** Un parámetro de tipo equivocado lo rechaza la capa OData con texto técnico antes del plugin (spike 1b); el web resource no debe mostrarlo tal cual.

## 3. Precondiciones

| # | Qué tiene que cumplirse | Consulta | Esperado |
|---|---|---|---|
| 1 | La solución existe, no es managed, su publisher coincide en unique name, prefijo y prefijo de opciones; cada idioma está provisionado y el idioma base es uno de ellos | `solutions?$filter=uniquename eq '<solucion>'` con el publisher expandido; `organizations`; `RetrieveProvisionedLanguages` | los de la sección 1 |
| 2 | El tipo de plugin está registrado, una sola vez | `plugintypes?$filter=typename eq 'Sanic.Carp.Plugins.Api.GenerarFormatoApi'` | una fila (la crea el playbook del paquete, inventario 8.1) |
| 3 | El privilegio de ejecución existe. **A mano: la herramienta no lo comprueba** | `privileges?$select=name&$filter=name eq 'prvReadsanic_carp_tbl_generacionformato'` | una fila (nace con la tabla `sanic_carp_tbl_generacionformato`, inventario 3.7) |
| 4 | La API no existe con otro valor en un campo inmutable | `customapis?$filter=uniquename eq 'sanic_carp_capi_generarformato'` | ninguna fila, o una igual (sección 6) |
| 5 | Nombre y nombres visibles sin tildes ni ñ; nombres de parámetros en minúsculas y dígitos; descripciones de salida de 100 caracteres o menos | sin red | los de la sección 2 |
| 6 | **El dueño confirmó `habilitadaenflujos` y los `opcional` de la sección 2** (decisiones del 2026-10-20) | decisión escrita en `diseno/03` §0.1 | confirmación; mientras no esté, el constructor no corre |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/dataverse/patrones.md`, «Custom API — construirlo desde un playbook». Herramienta del proyecto: `herramientas/construir/custom_api.py`.

```
python3 herramientas/construir/custom_api.py playbooks/custom-api/sanic_carp_capi_generarformato.md
```

## 5. Verificación

```
python3 herramientas/construir/custom_api.py playbooks/custom-api/sanic_carp_capi_generarformato.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET customapis` por `uniquename` | una fila · `ismanaged = false` · `name` y `uniquename` `sanic_carp_capi_generarformato` · `displayname` y `description` de la sección 2 · `bindingtype = 0` (Global) · `isfunction = false` · `isprivate = false` · `allowedcustomprocessingsteptype = 0` (None) · `workflowsdkstepenabled = false` · `executeprivilegename = prvReadsanic_carp_tbl_generacionformato` · plugin `Sanic.Carp.Plugins.Api.GenerarFormatoApi` |
| 2 | Parámetros de entrada (`customapirequestparameters`) | **exactamente dos**: `arregloid` y `versionpreviaid`, ambos Guid (12) con `isoptional = true`, cada uno con su `name` `sanic_carp_capiip_generarformato_<nombre>`, `displayname` y `description` de la sección 2 |
| 3 | Propiedades de salida (`customapiresponseproperties`) | **exactamente cinco**: `archivo` String (10), `nombrearchivo` String (10), `numeroprovisional` Integer (7), `congelado` Boolean (0) y `advertencias` String (10), cada una con su `name` `sanic_carp_capiop_generarformato_<nombre>`, `displayname` y `description`. Una de más o una de menos es `difiere` |
| 4 | Pertenencia a la solución | la API figura en `solutioncomponents` de la solución; **se informa el `componenttype`** que puso la plataforma |
| 5 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**. Si lo que difiere es un campo inmutable (`uniquename`, `bindingtype`, `isfunction`, `allowedcustomprocessingsteptype`, `workflowsdkstepenabled`; tipo u opcionalidad de un parámetro), la herramienta lo dice y no hay corrección posible: **hay que borrar la API entera y rehacerla**, decisión de quien dirige. Con `--completar` la herramienta solo corrige lo mutable (nombres visibles, descripción, privilegio, `isprivate`, plugin) y agrega parámetros que falten; nunca borra ni pisa un campo inmutable. Un parámetro que sobra se denuncia con su nombre.

## 7. Reversa

`DELETE customapis(<customapiid>)`, con sus parámetros y propiedades. **Irreversible desde la primera llamada**: `uniquename`, `bindingtype`, `isfunction`, `allowedcustomprocessingsteptype`, `workflowsdkstepenabled`, y de cada parámetro `uniquename`, `type`, `isoptional`. Borrarla rompe todo lo que la llame (el web resource de comandos, inventario 12.22, y la vista previa del jefe).

## 8. Fuera de alcance

- Registrar el paquete y el tipo `GenerarFormatoApi`: inventario 8.1 (`playbooks/paquete-plugins/Sanic.Carp.Plugins.md`).
- Dar el privilegio `prvReadsanic_carp_tbl_generacionformato` a los roles: inventario 6.2 a 6.5 (Oficial y Purga; el Jefe y las cuentas de servicio lo reciben por el Oficial).
- La tabla de bitácora `sanic_carp_tbl_generacionformato` (inventario 3.7) y los steps de Arreglo y Generación que aceptan sus escrituras del sistema (inventario 8.7).
- El web resource que la invoca y dispara la descarga: inventario 12.22.
- Las otras cuatro Custom API: inventario 8.3 a 8.6.
