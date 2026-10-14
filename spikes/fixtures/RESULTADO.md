# Fixtures de Dataverse para validar los dobles (lote 3, diseno/03 §4.5)

Fecha: 2026-10-14. Entorno: Dev (`https://org36e60d9d.crm.dynamics.com`, afirmado en el codigo; la organizacion de `WhoAmI` debe coincidir con la de la linea base o el programa aborta).
Origen de TODOS los fixtures: **ServiceClient 1.2.26 (`Microsoft.PowerPlatform.Dataverse.Client`), net8.0, cliente fuera del sandbox**. Dentro de un plugin la deserializacion puede diferir (sobre todo `DateTime.Kind`); no se capturo desde un plugin.
Los valores se leyeron por `IOrganizationService` (la interfaz que usa el codigo), sin "arreglar" nada de lo que llega.

**Estado: completo.** Los 7 fixtures de §4.5 y dos extra (AliasedValue y agregados; paginacion), con limpieza verificada y control de anonimizacion en cero.

## Piezas

| Pieza | Que hace |
|---|---|
| `src/Zzf.Exportador/` | Consola net8.0. Crea filas `ZZF-`, las registra en `estado.json` al nacer, exporta, y las borra en el `finally`. Anonimiza en memoria |
| `f00_linea_base.py` | Solo lee: residuales `ZZF-` (detiene si hay), steps no administrados del proyecto (detiene si hay), clave K-01 activa, concurrencia optimista, solucion (26 componentes), candidatos de correo |
| `f10_exportar.py` | Lanza el exportador; los secretos viajan por variables de entorno del proceso hijo, nunca por linea de comandos ni archivo |
| `f20_control_anonimizacion.py` | Control independiente (en memoria, solo conteos) |
| `f30_resumen.py` | Tabla de tipos observados -> `evidencia/30_tipos_observados.json` |
| `f90_limpieza.py` / `f91_verificar_limpieza.py` | Red de seguridad por Web API / verificacion releyendo cada GUID (debe dar 404) y buscando `ZZF-` residual -> `evidencia/91_limpieza_verificada.json` |
| `salida/*.json` | Los fixtures. Cada atributo: `{"tipo": "<tipo .NET completo>", "valor": ...}` (`DateTime` agrega `kind`) |

## Fixtures

| Archivo | Filas | Contenido |
|---|---|---|
| `01-email.json` | 2 entrantes + 2 salientes REALES anonimizadas (ya existian entre los buzones de prueba) + 1 entrante y 1 saliente SINTETICOS `ZZF-` con `regardingobjectid` a una conversacion (no enviados) | `Retrieve` con `ColumnSet(true)` y `RetrieveMultiple` con las columnas del diseno |
| `02-conversacion.json` | SINTETICAS: Abierta (0/1), Finalizada (1/2), Unida (1/159460001) | Todas las columnas |
| `03-arreglo.json` | SINTETICAS: Borrador congelado (0/1 con `sanic_fechacongelamiento`), Vigente (0/159460001) + cliente y version de plantilla | Todas las columnas |
| `04-teammembership-team-queue.json` | REALES anonimizadas | `teammembership` por `RetrieveMultiple` de la intersect (sin join) para dos usuarios y para un team; el team `isdefault` de la BU Arreglos de Pago; 2 colas con `emailaddress` |
| `05-environmentvariable.json` | SINTETICAS `sanic_zzf...` | No hay `sanic_carp_ev_*` en Dev (0); se creo una definicion de texto y su valor FUERA de la solucion |
| `06-whoami-systemuser.json` | REALES anonimizadas | `WhoAmIResponse`, SYSTEM, usuario de aplicacion conectado, usuario de aplicacion no administrador |
| `07-fallas.json` | SINTETICAS | K-01, RowVersion, fila inexistente, paginacion con `PageInfo.Count = 2` |
| `08-aliased-y-agregados.json` | SINTETICAS | `AliasedValue` de joins de `QueryExpression` y de FetchXML agregado |
| `00-incidencias-y-tipos.json` | | incidencias (0) y todos los tipos .NET vistos |

## Tipos observados (lo que el doble debe devolver)

| Atributo | Tipo observado | Detalle |
|---|---|---|
| `teammembership.systemuserid` / `teamid` / `teammembershipid` | `System.Guid` | crudo, NO `EntityReference` (el defecto del proyecto anterior) |
| `email.statuscode` / `statecode` | `OptionSetValue` | entrante 4 / 1; saliente 3 / 1 |
| `email.directioncode` | `System.Boolean` | entrante `false`, saliente `true` |
| `email.messageid` / `inreplyto` | `System.String` | con `<>`; `inreplyto` solo si hay |
| `email.actualend` / `senton` | `DateTime` Kind=**Utc** | `senton` ausente en los entrantes |
| `email.createdby` / `ownerid` | `EntityReference` | `systemuser`, con `Name` |
| `email.regardingobjectid` | `EntityReference` | ninguno de los correos reales lo tiene; en los sinteticos apunta a `sanic_carp_tbl_conversacion` |
| `email.from` / `to` / `cc` / `bcc` / `related` | `EntityCollection` de `activityparty` | solo con `ColumnSet(true)` |
| conversacion `statecode` / `statuscode` | `OptionSetValue` | Abierta 0/1, Finalizada 1/2, Unida 1/159460001 |
| conversacion `sanic_fechaprimercorreo` (fechahora UL) | `DateTime` Kind=**Utc** | |
| conversacion `sanic_clienteid`, `sanic_unidaaid`, `sanic_finalizadoporid`, `ownerid` | `EntityReference` | `ownerid` -> `team` |
| conversacion `sanic_resultado` | `OptionSetValue` | |
| arreglo `sanic_fechainicio` / `sanic_fechafin` / `sanic_fechaformalizacion` (fecha DO) | `DateTime` Kind=**Unspecified** | `2026-11-01T00:00:00`, sin `Z` |
| arreglo `sanic_fechacongelamiento` / `sanic_fechaformalizado` (UL) | `DateTime` Kind=**Utc** | |
| arreglo montos (`sanic_saldoinicial`...) | `System.Decimal` | NO `Money`; llega con escala 10 (`"1000.5000000000"` para un `1000.50m` enviado) |
| arreglo `sanic_numeroarreglo`, `sanic_plazo` | `Int32` | `sanic_numeroarreglo` ausente en el Borrador |
| `team.isdefault` | `Boolean` | |
| `queue.emailaddress` | `String` | 2 de 281 colas tienen direccion |
| `environmentvariabledefinition.type` | `OptionSetValue` | 100000000 = Texto |
| `WhoAmIResponse.Results` | `Guid` x3 | `UserId`, `BusinessUnitId`, `OrganizationId`; `ResponseName` = `WhoAmI` |
| SYSTEM | `fullname` "SYSTEM", `isdisabled` **true**, `accessmode` 0 | `businessunitid` `EntityReference` |
| usuario de aplicacion | `isdisabled` false, `accessmode` 4 | |
| `AliasedValue.Value` | segun la columna origen | `OptionSetValue`, `EntityReference`, `DateTime` Utc, `Int32`, `String` |
| `Entity.RowVersion` | `System.String` | digitos (`"13059426"`) |

## Sorpresas respecto de §4.5

1. **Version de fila**: el codigo es `0x80060882` (`-2147088254`), y es un `FaultException<OrganizationServiceFault>` directo (sin envoltura). §4.5 lo tenia "a confirmar".
2. **`ServiceClient` 1.2.26 no envuelve la falla**: la cadena es de largo 1 y la excepcion exterior YA es `FaultException<OrganizationServiceFault>`. Todas las fallas llevan `Detail.InnerFault` y 12 claves `Api*` en `ErrorDetails` (`ApiStepKey` es `Guid`, `ApiDepthKey` `Int32`). La de K-01 suma `DuplicateAttributes`, `DuplicateEntity` y las claves `"0"`, `"1"` (todas `String`).
3. **K-01** = `0x80060892` (`-2147088238`). **Fila inexistente** = `0x80040217`.
4. **`DateTime.Kind` no es uniforme**: fechahora UL llega `Utc`; fecha DO llega `Unspecified` a las 00:00:00. El doble debe distinguirlas (hoy el diseno solo dice "UTC y solo fecha").
5. **Decimales con escala 10**: `decimal(2)` vuelve como `1000.5000000000`. Comparar por valor, no por texto.
6. **Una columna nula no viene en `Attributes`** (ausente, no `null`): `senton` en un entrante, `sanic_numeroarreglo` en un Borrador, `sanic_unidaaid` en una Abierta, etc. Coincide con V-6 para columnas ocultas.
7. **`EntityReference.Name` no siempre viene**: viene en `ownerid`, `createdby`, `modifiedby` y los lookups; llega `null` en `owningteam`, `owninguser` y en referencias a `activitypointer`/`mailbox`. El doble no debe asumir `Name`.
8. **`email` + `ColumnSet(true)` trae cinco `EntityCollection` de `activityparty`** (`from`, `to`, `cc`, `bcc`, `related`); un `RetrieveMultiple` con columnas explicitas devuelve solo `activityid` mas lo pedido. En `activityparty`, `activityid` es un `EntityReference` a `activitypointer`, no a `email`.
9. **Update de estado de un `email` funciona con `Update`** de `statecode`+`statuscode` (no hizo falta `SetStateRequest`). La plataforma pisa `actualend` al completarlo y pone `senton` al pasar a Sent. `inreplyto` NO se conservo al crear (el sintetico entrante lo perdio): parece columna que fija la sincronizacion.
10. **Paginacion**: `PagingCookie` es un XML `<cookie page="n"><createdon .../><pk last first/></cookie>` que incluye la columna del `OrderExpression`. Sin `ReturnTotalRecordCount` el total es `-1`; con el, `TotalRecordCount` = 4 y `TotalRecordCountLimitExceeded` = false. Hay cookie incluso sin paginar y con `MoreRecords` en false (salvo con `TopCount`, donde viene vacia). `MinActiveRowVersion` = `"-1"`.
11. **`AliasedValue`**: un `count` agregado es `Int32`, un `max` de fecha es `DateTime` Utc, un `groupby` de `statuscode` es `OptionSetValue`. Un lookup en un join trae un alias extra `<alias>.<col>_<col>yominame` de tipo `String`.
12. **Variable de entorno**: la plataforma fuerza el `schemaname` del valor al de su definicion (se envio `..._valor`, quedo igual al de la definicion).
13. **SYSTEM esta deshabilitado** (`isdisabled` true, `accessmode` 0): una regla "usuario habilitado" no puede aplicarse a SYSTEM.
14. `statecode` por `Update` directo funciono en Conversacion, Arreglo y Email sin `SetStateRequest`; ningun step del proyecto esta registrado (linea base: 0).

## Fallas (exterior, ErrorCode)

| Caso | Tipo exterior | ErrorCode |
|---|---|---|
| K-01: dos conversaciones con la misma `sanic_huellamessageid` | `System.ServiceModel.FaultException<OrganizationServiceFault>` | `0x80060892` (-2147088238) |
| `UpdateRequest` + `IfRowVersionMatches` con RowVersion viejo | idem | `0x80060882` (-2147088254) |
| `Retrieve` de GUID inexistente (extra) | idem | `0x80040217` (-2147220969) |

Control: el mismo `UpdateRequest` con la version vigente devuelve `UpdateResponse` (`Results` vacio).

## Limpieza

- 3 corridas con filas: 10 + 10 + 12 = **32 creadas, 32 borradas** (`finally` del exportador). Las dos primeras fueron para depurar (la primera aborto antes de crear nada por una comprobacion de nombre de organizacion mal planteada; la segunda creo y borro 10 filas y fallo solo al escribir el JSON).
- Verificacion (`f91`) tras cada corrida, releyendo cada GUID: todas dan HTTP 404; 0 filas `ZZF-` / `sanic_zzf` residuales; las tablas del proyecto siguen en 0; la solucion sigue con 26 componentes (0 nuevos, 0 que faltan); los correos siguen en 560.
- Evidencia: `evidencia/91_limpieza_verificada.json` (corrida final, 12 GUID) y `evidencia/91_limpieza_verificada_corrida1_sin_fixtures.json`, `..._corrida2.json` (las anteriores).
- Nada se agrego a la solucion; no se registraron plugins ni steps; no se envio ni conecto ningun correo (los dos correos sinteticos son actividades sin enviar, borradas).

## Control de anonimizacion (`evidencia/20_control_anonimizacion.json`)

0 hallazgos: correos 40 (todos `@example.com`), URL de la organizacion 0, `PP_MAIL_BUZON_1..4` y dominio del tenant 0, ids de tenant/aplicaciones 0, GUID reales (usuarios, teams, BU, colas, organizacion, correos) 0, nombres reales comparados (350 de usuario, 1473 de team, 2 de BU, 281 de cola, 178 correos/UPN) 0 coincidencias, secretos en archivos del spike 0 (41 archivos de texto). Las comparaciones usan valores JSON (no claves) y excluyen solo las etiquetas de plataforma "Microsoft" y "for Outlook" (usuarios integrados). `ZZF-`, SYSTEM y los autonumericos `CONV-`/`ARR-` se conservan a proposito (solo existen en filas sinteticas).

Esquema de anonimizacion: GUID de cualquier fila -> `0fff000C-0000-4000-8000-nnnnnnnnnnnn` (C = categoria: 1 usuario, 2 team, 3 BU, 4 cola, 5 organizacion, 9 otro); correos -> `zzf-buzonN@example.com`; `messageid` -> `<zzf-msgN@example.com>`; nombres -> `ZZF Usuario N`, `ZZF Team N`, `ZZF BU N`, `ZZF Cola N`; asunto -> `ZZF-asunto N`; cuerpo -> `ZZF-cuerpo N`; cualquier otro texto no `ZZF-` -> `ZZF-texto-<columna> N`. El mapeo vive solo en memoria.

## Reproducir

```
cd spikes/fixtures
dotnet build src/Zzf.Exportador -c Release     # solo este proyecto
python3 f00_linea_base.py
python3 f10_exportar.py                        # crea ZZF-, exporta, borra
python3 f91_verificar_limpieza.py              # debe dar limpieza_verificada_OK
python3 f20_control_anonimizacion.py           # debe dar control_OK
python3 f30_resumen.py
```

`f90_limpieza.py` solo hace falta si una corrida muere sin pasar por su `finally`.
