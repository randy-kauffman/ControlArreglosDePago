# Playbook: custom-api · sanic_carp_capi_reconciliarcorreos

> **Tipo con 0 ciclos** (`power-platform-especificar`, `references/custom-api.md`). Casi todo lo que este playbook fija es **irreversible**: leer la tabla de decisiones y los huecos de diseño antes de construir. El molde es el piloto `sanic_carp_capi_tomarconversacion`.

## 1. Identidad

```json
{
  "tipo_playbook": "custom-api",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "8.3",
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

De dónde sale cada dato: `diseno/01-convenciones.md` §0 (publisher, prefijo, abreviatura, solución e idiomas), verificados contra Dev el 2026-10-09. La API está definida en `diseno/03-contratos-custom-api.md` §0.1 (reglas de todas las Custom API), §0.2 (identidad) y §1.2 (contrato), y figura en `diseno/06-inventario-componentes.md` §8, renglón 8.3. Los nombres de los parámetros son los que lee y escribe el código (`src/Sanic.Carp.Plugins/Api/ReconciliarCorreosApi.cs`, constantes `Parametro*` y `Salida*`).

Advertencias: en Dev hay otro publisher con el mismo prefijo `sanic` («Sanic Corp»): la herramienta se detiene si el publisher de la solución no es el de arriba.

## 2. Qué se crea

```json
{
  "tipo": "custom-api",
  "paquete": "sanic_carp_pkg_plugins",
  "nombre": "sanic_carp_capi_reconciliarcorreos",
  "displayname": "CAPI - CARP - Reconciliar correos",
  "descripcion": "Red de seguridad de la entrada de correo: registra en una conversación los correos entrantes del área que quedaron sin ella porque el plugin asíncrono falló. Devuelve cuántos encontró, registró y omitió.",
  "plugintype": "Sanic.Carp.Plugins.Api.ReconciliarCorreosApi",
  "esfuncion": false,
  "esprivada": false,
  "binding": "Global",
  "pasosdeterceros": "None",
  "habilitadaenflujos": false,
  "privilegio": "prvCreatesanic_carp_tbl_conversacion",
  "entrada": [
    {
      "nombre": "emailid",
      "displayname": "CAPI_IP - CARP - Correo",
      "descripcion": "Correo entrante que se quiere reconciliar. Si falta, se reconcilian los candidatos de un lote.",
      "tipo": "Guid",
      "opcional": true
    },
    {
      "nombre": "maximo",
      "displayname": "CAPI_IP - CARP - Maximo de correos",
      "descripcion": "Cantidad máxima de correos del lote. Ausente o 0 vale 20; más de 100 se recorta a 100; un negativo se rechaza.",
      "tipo": "Integer",
      "opcional": true
    },
    {
      "nombre": "minutosminimos",
      "displayname": "CAPI_IP - CARP - Minutos minimos",
      "descripcion": "Antigüedad mínima del correo, en minutos. Ausente vale 10; 0 explícito es sin espera; un negativo se rechaza.",
      "tipo": "Integer",
      "opcional": true
    }
  ],
  "salida": [
    {
      "nombre": "encontrados",
      "displayname": "CAPI_OP - CARP - Encontrados",
      "descripcion": "Correos procesados en este lote.",
      "tipo": "Integer"
    },
    {
      "nombre": "registrados",
      "displayname": "CAPI_OP - CARP - Registrados",
      "descripcion": "Correos que quedaron con conversación (creada, unida o que ya la tenían).",
      "tipo": "Integer"
    },
    {
      "nombre": "conerror",
      "displayname": "CAPI_OP - CARP - Con error",
      "descripcion": "Correos que se omitieron con un código de negocio.",
      "tipo": "Integer"
    },
    {
      "nombre": "haymas",
      "displayname": "CAPI_OP - CARP - Hay mas",
      "descripcion": "Verdadero si quedan candidatos fuera de este lote.",
      "tipo": "Boolean"
    },
    {
      "nombre": "detalle",
      "displayname": "CAPI_OP - CARP - Detalle",
      "descripcion": "JSON con un renglón por correo: emailid, resultado, conversacionid y codigo. Sin datos del correo.",
      "tipo": "String"
    }
  ]
}
```

Una Custom API **Action** (`IsFunction = false`: escribe algo), **global** (unbound), pública (`IsPrivate = false`: con `true` el conector de Dataverse de Power Automate no la resuelve y el flujo de salud, inventario 11.2, que es quien la invoca, no podría activarse; no es una medida de seguridad, la seguridad es el privilegio), sin steps de terceros (`AllowedCustomProcessingStepType = None`: `diseno/03` §0.1). **Identidad** (`diseno/03` §0.2 y §1.2, decisión 1): lee y escribe todo con SYSTEM; no hay actor humano y la cuenta de servicio que llama no se registra (el código crea un solo servicio, el de SYSTEM). El nombre corto de cada parámetro es el que viaja en la llamada; el `name` largo (`sanic_carp_capiip_reconciliarcorreos_<nombre>`, `…capiop_reconciliarcorreos_<nombre>`) lo calcula la herramienta.

**Quién puede ejecutarla.** El privilegio `prvCreatesanic_carp_tbl_conversacion` lo tiene solo el rol Servicio (el Oficial no lo tiene; `diseno/03` §0.1). Es la única de las cinco API que no ejecuta el Oficial.

Decisiones que este playbook toma y el diseño no traía, o que el dueño fijó:

| Decisión | Valor | Por qué |
|---|---|---|
| `habilitadaenflujos` | `false` | Decisión del usuario, 2026-10-20 (registrada en `diseno/03` §0.1). Es el valor por defecto de la plataforma; el diseño solo usa flujos de nube con el conector de Dataverse. **Irreversible** |
| `opcional` de `emailid`, `maximo` y `minutosminimos` | `true` los tres | `diseno/03` §1.2 («Req: no»). La decisión del 2026-10-20 (opcional cuando el plugin responde con un código propio al faltar el parámetro) **no cambia nada acá**: ningún parámetro tiene un código para el caso «falta». `CARP-REC-001` y `CARP-REC-002` son para un valor **negativo** presente (`ReconciliarCorreosLogica.cs:77-78`); un `maximo` o unos `minutosminimos` ausentes toman el valor por defecto (20 y 10). Irreversible |
| `displayname` de la API | «CAPI - CARP - Reconciliar correos» | El patrón es `CAPI - CARP - <Nombre>`; el texto sale del nombre de la API, sin tilde (BP-PP-197) |
| `descripcion` de la API y de los parámetros, y `displayname` de los parámetros | Los de arriba | `diseno/03` §0.1 da el patrón de los nombres visibles pero no los textos; los de las salidas salen de las notas de la tabla de §1.2. Las descripciones de las salidas miden 100 caracteres o menos (tope de la columna) |
| Tipos | `emailid` Guid; `maximo` y `minutosminimos` Integer; `encontrados`, `registrados` y `conerror` Integer; `haymas` Boolean; `detalle` String | `diseno/03` §1.2 y los tipos que lee (`LeerGuid`, `LeerEntero`) y escribe (`int`, `bool`, `string`) `ReconciliarCorreosApi.Ejecutar`; ningún id es `EntityReference` (DC-24) |

Discrepancias entre el diseño y el código:

- `encontrados`: la tabla de §1.2 dice «Candidatos leídos en este lote»; el código y la decisión 3 del mismo documento (lote 6) lo definen como los correos **procesados**, sin contar el candidato extra que sirve para `haymas`, y con `emailid` vale 1. La descripción del playbook sigue al código.
- Ninguna en nombres, tipos ni opcionalidad de los parámetros.

Huecos de diseño que este playbook no cierra:

- **Etiquetas por idioma.** `displayname` y `description` de la API y de cada parámetro son columnas localizables; la herramienta escribe una cadena única (en español, sin tildes en el nombre). La regla de etiquetas en 1033 y 3082 no se cumple para este tipo hasta que la herramienta lo soporte.
- **Riesgo H-26** (`diseno/03` §1.2, decisión 6): si el correo que llega por sincronización no queda en la BU configurada, la reconciliación lo muestra como `omitido` con `CARP-ENT-004` y el área no registra sus propios correos. No se dio por medido.

## 3. Precondiciones

| # | Qué tiene que cumplirse | Consulta | Esperado |
|---|---|---|---|
| 1 | La solución existe, no es managed, su publisher coincide en unique name, prefijo y prefijo de opciones; cada idioma está provisionado y el idioma base es uno de ellos | `solutions?$filter=uniquename eq '<solucion>'` con el publisher expandido; `organizations`; `RetrieveProvisionedLanguages` | los de la sección 1 |
| 2 | El tipo de plugin está registrado, una sola vez | `plugintypes?$filter=typename eq 'Sanic.Carp.Plugins.Api.ReconciliarCorreosApi'` | una fila (la crea el playbook del paquete, inventario 8.1) |
| 3 | El privilegio de ejecución existe. **A mano: la herramienta no lo comprueba** | `privileges?$select=name&$filter=name eq 'prvCreatesanic_carp_tbl_conversacion'` | una fila (nace con la tabla `sanic_carp_tbl_conversacion`, inventario 3.2) |
| 4 | La API no existe con otro valor en un campo inmutable | `customapis?$filter=uniquename eq 'sanic_carp_capi_reconciliarcorreos'` | ninguna fila, o una igual (sección 6) |
| 5 | Nombre y nombres visibles sin tildes ni ñ; nombres de parámetros en minúsculas y dígitos; descripciones de salida de 100 caracteres o menos | sin red | los de la sección 2 |
| 6 | **El dueño confirmó `habilitadaenflujos`** (decisión del 2026-10-20) | decisión escrita en `diseno/03` §0.1 | confirmación; mientras no esté, el constructor no corre |

Si alguna falla: estado `bloqueado`, no se crea nada.

## 4. Cómo se construye

Receta: `power-platform-construir`, `references/dataverse/patrones.md`, «Custom API — construirlo desde un playbook». Herramienta del proyecto: `herramientas/construir/custom_api.py`.

```
python3 herramientas/construir/custom_api.py playbooks/custom-api/sanic_carp_capi_reconciliarcorreos.md
```

## 5. Verificación

```
python3 herramientas/construir/custom_api.py playbooks/custom-api/sanic_carp_capi_reconciliarcorreos.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET customapis` por `uniquename` | una fila · `ismanaged = false` · `name` y `uniquename` `sanic_carp_capi_reconciliarcorreos` · `displayname` y `description` de la sección 2 · `bindingtype = 0` (Global) · `isfunction = false` · `isprivate = false` · `allowedcustomprocessingsteptype = 0` (None) · `workflowsdkstepenabled = false` · `executeprivilegename = prvCreatesanic_carp_tbl_conversacion` · plugin `Sanic.Carp.Plugins.Api.ReconciliarCorreosApi` |
| 2 | Parámetros de entrada (`customapirequestparameters`) | **exactamente tres**: `emailid` Guid (12), `maximo` Integer (7) y `minutosminimos` Integer (7), los tres con `isoptional = true`, cada uno con su `name` `sanic_carp_capiip_reconciliarcorreos_<nombre>`, `displayname` y `description` de la sección 2 |
| 3 | Propiedades de salida (`customapiresponseproperties`) | **exactamente cinco**: `encontrados`, `registrados` y `conerror` Integer (7), `haymas` Boolean (0) y `detalle` String (10), cada una con su `name` `sanic_carp_capiop_reconciliarcorreos_<nombre>`, `displayname` y `description`. Una de más o una de menos es `difiere` |
| 4 | Pertenencia a la solución | la API figura en `solutioncomponents` de la solución; **se informa el `componenttype`** que puso la plataforma |
| 5 | Segunda ejecución sin `--solo-verificar` | estado `ya_existia`, código de salida 0 |

## 6. Si ya existe

Se hacen todas las comprobaciones de la sección 5. Si todas pasan: `ya_existia`. Si alguna no: `difiere`, con la diferencia exacta, **sin tocar nada**. Si lo que difiere es un campo inmutable (`uniquename`, `bindingtype`, `isfunction`, `allowedcustomprocessingsteptype`, `workflowsdkstepenabled`; tipo u opcionalidad de un parámetro), la herramienta lo dice y no hay corrección posible: **hay que borrar la API entera y rehacerla**, decisión de quien dirige. Con `--completar` la herramienta solo corrige lo mutable (nombres visibles, descripción, privilegio, `isprivate`, plugin) y agrega parámetros que falten; nunca borra ni pisa un campo inmutable. Un parámetro que sobra se denuncia con su nombre.

## 7. Reversa

`DELETE customapis(<customapiid>)`, con sus parámetros y propiedades. **Irreversible desde la primera llamada**: `uniquename`, `bindingtype`, `isfunction`, `allowedcustomprocessingsteptype`, `workflowsdkstepenabled`, y de cada parámetro `uniquename`, `type`, `isoptional`. Borrarla rompe todo lo que la llame: el flujo de salud del buzón (inventario 11.2) y, con él, la red de seguridad de la entrada de correo (D-03).

## 8. Fuera de alcance

- Registrar el paquete y el tipo `ReconciliarCorreosApi`: inventario 8.1 (`playbooks/paquete-plugins/Sanic.Carp.Plugins.md`).
- Dar el privilegio `prvCreatesanic_carp_tbl_conversacion` al rol Servicio: inventario 6.4.
- El flujo de salud del buzón que la invoca con la cuenta de servicio: inventario 11.2.
- El step asíncrono `RegistrarCorreoEntranteStep` (S-34), cuya lógica reutiliza esta API: inventario 8.7.
- Las otras cuatro Custom API: inventario 8.2, 8.4, 8.5 y 8.6.
