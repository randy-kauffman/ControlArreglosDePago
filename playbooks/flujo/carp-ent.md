# Playbook: flujo · Cloud Flow - CARP - ENT - Capturar correo entrante

> **Tipo con 0 ciclos en este proyecto** (`power-platform-especificar`). El formato es el que lee `herramientas/construir/flujo.py`, igual que `carp-est.md`. La definición vive en `playbooks/flujo/carp-ent.json`, sin GUID. **El JSON no se edita a mano: lo genera `spikes/flujos-correo/generar_flujos.py`** (función `flujo_ent`, que comparte con el spike V4 el cuerpo `_entrada`), para que producción y spike no se separen.

## 1. Identidad

```json
{
  "tipo_playbook": "flujo",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "11.4",
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

Diseño: `diseno/08-alternativa-correo-por-flujos.md` §3 (entrada) y §10 (modo por entorno). Spike que lo valida: `spikes/flujos-correo/RESULTADO.md` (V4 r10, 17 de 17 en Dev, adjuntos idénticos a los de SSS). Inventario 11.4.

## 2. Qué se crea

```json
{
  "tipo": "flujo",
  "nombre": "Cloud Flow - CARP - ENT - Capturar correo entrante",
  "descripcion": "Modo flujos: captura el correo nuevo del buzon del area y crea el email en Dataverse con dueno el team del area (idempotencia, correlacion por In-Reply-To, adjuntos, cola, categoria). Solo trabaja si sanic_carp_ev_modocorreo vale flujos.",
  "archivo": "playbooks/flujo/carp-ent.json",
  "conexiones": {
    "dataverse": {
      "referencia": "sanic_carp_conr_dataverse",
      "api": "shared_commondataserviceforapps"
    },
    "outlook": {
      "referencia": "sanic_carp_conr_outlook",
      "api": "shared_office365"
    }
  },
  "variables": [
    "sanic_carp_ev_buzonarea",
    "sanic_carp_ev_modocorreo",
    "sanic_carp_ev_nombrebu"
  ],
  "hijos": [],
  "activar": false
}
```

**`activar` es `false` a propósito.** El flujo se crea APAGADO y se enciende solo en modo `flujos` (fases F3/F4 de `diseno/08` §8). En Dev el buzón del área (buzón 1) usa sincronización del lado del servidor; la regla es **nunca SSS y flujos sobre el mismo buzón** porque duplican la entrada. Además, el modo vale `sss`, así que aunque alguien lo encendiera, el primer paso termina sin hacer nada.

### 2.1 Qué hace

Disparador Office 365 Outlook «Cuando llega un correo nuevo a un buzón compartido (V2)» (`SharedMailboxOnNewEmailV2`) sobre `sanic_carp_ev_buzonarea`, carpeta Inbox, **sin filtro de asunto**, con adjuntos incluidos y salidas ocultas. Pasos, en orden:

1. **Compuerta de modo (`Verificar_modo`).** Si `sanic_carp_ev_modocorreo` no vale `flujos` (vacío o cualquier otro texto, sin distinguir mayúsculas), `Terminate` en Succeeded y nada más.
2. **Team y cola del área** (`Intento`): la BU por `sanic_carp_ev_nombrebu`, su team por defecto (será el dueño del email, lo exige S-34) y la queue cuyo `emailaddress` es el buzón que dispara. Como hace el plugin; ninguna variable guarda GUID.
3. **Encabezados** por Graph (`Send an HTTP request` del conector de Outlook): `internetMessageHeaders`. El In-Reply-To sale en tres variantes (tal cual, sin `<>`, con `<>`).
4. **Adjuntos** por Graph (`/attachments`, sin `$select`).
5. **Idempotencia:** si ya hay un email entrante con ese `messageid`, no crea nada (reenvíos y reintentos no duplican).
6. **Correlación:** busca el email más antiguo con Regarding cuyo `messageid` sea una de las tres variantes y toma su conversación (ruta A de S-34); si no hay, crea sin Regarding (ruta C).
7. **Crea el email** (`directioncode` false, dueño = team del área, partes remitente y queue, `actualend`), lo **cierra como Received** (1/4), crea un `activitymimeattachment` por archivo (con `attachmentcontentid` si es imagen en línea) y el `queueitem`.
8. **Categoría** `CARP-registrado` en el mensaje de Outlook. Los pasos 7-final (cola y categoría) van en un `Scope` aparte para que su falla no oculte el resto.
9. **Si `Intento` falla** (Failed o TimedOut), `Terminate` en Failed con código `CARP-ENT-FALLO`: la corrida queda Failed y el monitoreo la ve.

Diferencias con el spike V4 (r10), todas por diseño: sin filtro `SPK-FC-`; sin la fila `SPK-DIAG` ni `Estado_de_las_acciones`, `Armar_diagnostico`, `Resumen_adjuntos`, `Resumen_opcionales`, `varModo` y `varOpc`; con compuerta de modo; con cierre en Failed; con secure data. La lógica de correo es la misma generada por la misma función.

### 2.2 Trampas del conector (cada una costó una vuelta de activación; NO cambiar)

| Qué | Cómo tiene que ir |
|---|---|
| Búsquedas (lookups) | por **nombre de la propiedad de navegación**, no de la columna: `item/ownerid_email@odata.bind`, `item/regardingobjectid_sanic_carp_tbl_conversacion_email@odata.bind` |
| Partes del email | `item/activitypointer_activity_parties` (no `email_activity_parties`) |
| `queueitem` | `item/objectid_email@odata.bind` (con `activitypointer` falla con «This object type cannot be queued») |
| Adjuntos | `item/objectid_activitypointer@odata.bind` + `item/objecttypecode: email` + `item/attachmentid@odata.bind: @json('null')` (el conector exige el bind; la Web API acepta null) |
| Variables | sin autorreferencia en `SetVariable`; se usa `AppendToStringVariable` |
| Graph | solo por `HttpRequest` del conector de Office 365 Outlook; NO el conector «HTTP con Microsoft Entra ID» (descartado por el usuario). Dos conexiones en total: Outlook y Dataverse |
| `/attachments` | sin `$select` (`contentBytes` no existe en el tipo base) |

### 2.3 Secure inputs y outputs (`diseno/07` §8.1)

`runtimeConfiguration.secureData.properties = [inputs, outputs]` en toda acción de `Intento` que lee o escribe correo o filas de Dataverse (conectores, `Compose`, `Query`, `Select`, `SetVariable`), y `[outputs]` en el disparador (trae el correo entero). **Desviación de lectura de §8.1:** esa regla dice que los flujos «no leen cuerpos ni adjuntos»; ENT sí los lee (es su trabajo), por eso el secure data es obligatorio acá y no hay ningún aviso por correo.

## 3. Precondiciones

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, publisher e idiomas | los de la sección 1 |
| 2 | Las connection references existen | `sanic_carp_conr_dataverse` y `sanic_carp_conr_outlook` (10.5, 10.6). Sin conexión, el flujo se crea APAGADO |
| 3 | Las variables de entorno existen | `sanic_carp_ev_buzonarea`, `sanic_carp_ev_nombrebu`, `sanic_carp_ev_modocorreo` (10.8, 10.1, 10.9) |

## 4. Cómo se construye

Como la cuenta de servicio (queda a su nombre, 13.6):

```
python3 spikes/flujos-correo/generar_flujos.py        # regenera playbooks/flujo/carp-ent.json (y el spike); no cambia nada si ya está al día
python3 pruebas-humo/lote-d/scripts/como_servicio.py flujo playbooks/flujo/carp-ent.md
```

En Test y Prod el flujo llega con la solución managed; el dueño y las conexiones son pasos del procedimiento 13.13 y 13.15.

## 5. Verificación

```
python3 pruebas-humo/lote-d/scripts/como_servicio.py flujo playbooks/flujo/carp-ent.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET workflows` por nombre, `category` 5 | una fila, definición igual al archivo, `statecode` 0 (apagado) |
| 2 | Dueño | la cuenta de servicio |
| 3 | Pertenencia a la solución | figura en `solutioncomponents` (componenttype 29) |
| 4 | Disparador | `SharedMailboxOnNewEmailV2`, buzón = `sanic_carp_ev_buzonarea`, Inbox, sin `subjectFilter`, `includeAttachments` true |

## 6. Si ya existe

`flujo.py`: si la definición del entorno no es la del archivo, `difiere` sin tocar nada; con `--completar` la corrige (un service principal no puede modificar un flujo ya creado: `spikes/flujos-correo/RESULTADO.md` §6, 3; por eso se usa `como_servicio.py`).

## 7. Reversa

Está apagado: no hay nada que revertir. Borrarlo no afecta al modo `sss`.

## 8. Fuera de alcance

- **Encenderlo.** Solo en modo `flujos` (F3/F4 de `diseno/08`): antes se apaga la sincronización del buzón del área y se carga `sanic_carp_ev_modocorreo` = `flujos`. En Dev no se enciende mientras el buzón 1 use SSS. Lo enciende una persona dueña de las conexiones (un service principal no puede: `ConnectionAuthorizationFailed`).
- El flujo de salida SND y la columna `sanic_idoutlook` (F2).
- Permisos del rol `SR - CARP - Servicio` (Create/Write/Append de Email, `prvAppendToQueue`, `prvAssign`): cambio de rol de F3, decisión del usuario.
- Crear las conexiones (OAuth): paso manual (`pruebas-humo/lote-d/RESULTADO.md`).

## 9. Riesgos conocidos

- El `Foreach` de adjuntos y los `If` no admiten secure data: el historial de la corrida puede mostrar la lista de adjuntos que itera el `Foreach`. Las acciones internas sí están ocultas. Revisar al activarlo en F3.
- Si `Opcionales` (cola o categoría) falla, la corrida termina Succeeded (el email ya está creado y cerrado). SAL lo detecta por la categoría ausente (`diseno/08` §3, punto 9).
- El secure data del disparador no se probó en ejecución: se verifica en la primera activación (F3).
- Límite de adjuntos: `MaxUploadFileSize` (5 MB por defecto), igual que SSS.
