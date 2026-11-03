# Playbook: flujo · Cloud Flow - CARP - SND - Enviar respuestas

> **Tipo con 0 ciclos en este proyecto** (`power-platform-especificar`). El formato es el que lee `herramientas/construir/flujo.py`, igual que `carp-ent.md`. La definición vive en `playbooks/flujo/carp-snd.json`, sin GUID. **El JSON no se edita a mano: lo genera `spikes/flujos-correo/generar_flujos.py`** (función `flujo_snd_produccion`, que comparte con el spike SND (r2) el cuerpo `_salida`), para que producción y spike no se separen.

## 1. Identidad

```json
{
  "tipo_playbook": "flujo",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "11.5",
  "fase": 2,
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

Diseño: `diseno/08-alternativa-correo-por-flujos.md` §4 (salida), §10 (modo por entorno) y §11 (desviaciones). Spike que lo valida: `spikes/flujos-correo/RESULTADO.md` (V-4 salida integral: SND (r2) pasó la respuesta del Oficial de Pending Send a Sent en 16 s, con Send As desde el buzón del área y la contestación del cliente correlacionada por In-Reply-To). Inventario 11.5.

## 2. Qué se crea

```json
{
  "tipo": "flujo",
  "nombre": "Cloud Flow - CARP - SND - Enviar respuestas",
  "descripcion": "Modo flujos: toma el email saliente del area en Pending Send, lo envia desde el buzon del area con Graph (createReply conservando el hilo, o mensaje nuevo si no hay original), con adjuntos, y lo pasa a Sent con el messageid real, o a Failed. Solo trabaja si sanic_carp_ev_modocorreo vale flujos.",
  "archivo": "playbooks/flujo/carp-snd.json",
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
    "sanic_carp_ev_modocorreo"
  ],
  "hijos": [],
  "activar": false
}
```

**`activar` es `false` a propósito.** El flujo se crea APAGADO y se enciende solo en modo `flujos` (F3/F4 de `diseno/08` §8). En Dev el buzón del área (buzón 1) usa sincronización del lado del servidor: la regla es **nunca SSS y flujos sobre el mismo buzón**. Con los dos activos, SSS manda el correo en Pending Send al mismo tiempo que el flujo y el cliente recibe la respuesta doble. Además el modo vale `sss`, así que aunque alguien lo encendiera, el primer paso termina sin hacer nada.

### 2.1 Qué hace

Disparador de Dataverse «Cuando se agrega, modifica o elimina una fila» (webhook) sobre `email`, Modificar, columna `statuscode`, filtro `statuscode eq 6 and directioncode eq true`, **concurrencia 1** (dos corridas no toman el mismo correo) y salidas ocultas. Pasos, en orden:

1. **Compuerta de modo (`Verificar_modo`).** Si `sanic_carp_ev_modocorreo` no vale `flujos` (vacío o cualquier otro texto, sin distinguir mayúsculas), `Terminate` en Succeeded y nada más.
2. **Guardas** (para no tocar los Pending Send de otros proyectos del entorno): el Regarding es una conversación, el remitente es el buzón del área (`sanic_carp_ev_buzonarea`) y el correo sigue en Pending Send. Si alguna falla, el flujo no hace nada.
3. **Bloqueo:** el correo pasa a `statuscode` 7 (Sending). Una segunda corrida ya no lo ve en 6.
4. **Lee** asunto, cuerpo y padre del correo; el `messageid` del padre (el correo del cliente); y las partes Para (máscara 2) y Copia (máscara 3).
5. **Busca el original en Graph** por `internetMessageId eq '<messageid del padre>'` en el buzón del área (no hace falta ninguna columna con el id de Outlook: ver `diseno/08` §11, punto 1).
6. **Con original: `createReply`** (Exchange pone el In-Reply-To y las References), `PATCH` del borrador con el cuerpo HTML y los destinatarios del email de Dataverse. **Sin original: mensaje nuevo** (§2.3).
7. **Adjuntos:** un `fileAttachment` por cada `activitymimeattachment` del correo (imágenes en línea con `isInline` y `contentId` desde `attachmentcontentid`).
8. **Lee el borrador** para obtener su `internetMessageId`, **envía** (`send`) y **cierra** el email en Sent (`statecode` 1, `statuscode` 3) con ese `messageid` (lo que permite que la respuesta del cliente vuelva a la misma conversación). S-35 despierta por `statuscode` y calcula los tiempos.
9. **Si el envío falla** (Failed o TimedOut en el `Scope` `Envio`) y el correo ya estaba bloqueado: el email pasa a Failed (**`statecode` 0 y `statuscode` 8**, porque Failed pertenece al estado Open: `RESULTADO.md`, V-1), aparece en «Respuestas con error de envío», y la corrida termina `Terminate` Failed con código `CARP-SND-FALLO` para que el monitoreo (SAL) la vea. Si falla el propio bloqueo, un `Terminate` Failed (`CARP-SND-FALLO`) al final de la corrida; el correo sigue en Pending Send.

Diferencias con el spike SND (r2), todas por diseño: sin `SPK-DIAG`, sin `Armar_diagnostico`, `Si_era_del_area` ni `varModo`; remitente desde `sanic_carp_ev_buzonarea` (el spike usaba `sanic_carp_ev_spksalida`); con compuerta de modo; con cierre en Failed; con secure data; con la rama «sin original». La lógica de correo con original es la misma, generada por la misma función. Los reintentos de las llamadas a Graph (3, cada 10 s) son los del spike.

### 2.2 Trampas del conector (cada una costó una vuelta de activación; NO cambiar)

| Qué | Cómo tiene que ir |
|---|---|
| Failed | `statecode` 0 y `statuscode` 8 juntos; `statuscode` 8 solo da 400 |
| `senton` | no se escribe (no es escribible); S-35 usa `actualend` |
| Variables | sin autorreferencia en `SetVariable`; el borrador se guarda en `varBorrador` desde dos ramas distintas |
| Graph | solo por `HttpRequest` del conector de Office 365 Outlook; NO el conector «HTTP con Microsoft Entra ID» (descartado por el usuario). Dos conexiones en total: Outlook y Dataverse |
| Padre | `ListRecords` con GUID nulo si no hay padre; `GetItem` fallaría con un id vacío |
| Permisos | Full Access **y Send As** de la cuenta que posee la conexión sobre el buzón del área (Full Access sola no deja enviar) |

### 2.3 Rama sin original (**NO validada en Dev; va a F4**)

El spike solo probó la respuesta a un correo del cliente que está en el buzón. Pero un email saliente puede no tener padre (por ejemplo, un correo que el Oficial crea con «Nuevo correo» en la conversación en vez de responder) o tener un padre cuyo `messageid` ya no está en el buzón (movido, borrado, o de antes de que el buzón existiera). Entonces `createReply` no tiene a qué responder y, sin esta rama, el correo quedaría en Failed. En ese caso el flujo:

1. `POST /users/{buzón}/messages` con `subject`, `body` (HTML), `toRecipients` y `ccRecipients` → borrador nuevo.
2. Los mismos pasos de adjuntos, lectura de `internetMessageId`, `send` y cierre en Sent con ese `messageid`.

Consecuencias: el mensaje sale **sin In-Reply-To** (el cliente lo ve como conversación nueva en su cliente de correo), pero queda grabado con su `messageid` real, así que si el cliente le contesta, la respuesta vuelve a la misma conversación del Oficial. Los asuntos no llevan «Re:» automático: el que escribió el Oficial es el que sale. **No hay nada en Dev que lo haya ejecutado**; F4 debe agregar un caso: Pending Send sin padre en una conversación, con el flujo encendido en una ventana sin SSS.

Nota: la Custom API «iniciar conversación nueva» (S-37, `IniciarConversacionNuevaLogica`) **no crea correos salientes**: mueve correos entrantes posteriores a una conversación nueva. No pasa por este flujo. El caso sin padre es el del «Nuevo correo» manual y el del padre ausente en el buzón.

### 2.4 Secure inputs y outputs (`diseno/07` §8.1)

`runtimeConfiguration.secureData.properties = [inputs, outputs]` en toda acción que lee o escribe correo o filas de Dataverse (conectores, `Compose`, `Query`, `Select`, `SetVariable`, también dentro de `If`, `Scope` y `Foreach`), y `[outputs]` en el disparador (trae el correo entero). Igual que ENT, es obligatorio porque SND lee cuerpos y adjuntos.

## 3. Precondiciones

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, publisher e idiomas | los de la sección 1 |
| 2 | Las connection references existen | `sanic_carp_conr_dataverse` y `sanic_carp_conr_outlook` (10.5, 10.6). Sin conexión, el flujo se crea APAGADO |
| 3 | Las variables de entorno existen | `sanic_carp_ev_buzonarea`, `sanic_carp_ev_modocorreo` (10.8, 10.9) |

## 4. Cómo se construye

Como la cuenta de servicio (queda a su nombre, 13.6):

```
python3 spikes/flujos-correo/generar_flujos.py        # regenera playbooks/flujo/carp-snd.json (y el spike y ENT); no cambia nada si ya está al día
python3 pruebas-humo/lote-d/scripts/como_servicio.py flujo playbooks/flujo/carp-snd.md
```

En Test y Prod el flujo llega con la solución managed; el dueño y las conexiones son pasos del procedimiento 13.13 y 13.15.

## 5. Verificación

```
python3 pruebas-humo/lote-d/scripts/como_servicio.py flujo playbooks/flujo/carp-snd.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET workflows` por nombre, `category` 5 | una fila, definición igual al archivo, `statecode` 0 (apagado) |
| 2 | Dueño | la cuenta de servicio |
| 3 | Pertenencia a la solución | figura en `solutioncomponents` (componenttype 29) |
| 4 | Disparador | webhook de Dataverse sobre `email`, `statuscode eq 6 and directioncode eq true`, concurrencia 1 |

## 6. Si ya existe

`flujo.py`: si la definición del entorno no es la del archivo, `difiere` sin tocar nada; con `--completar` la corrige (un service principal no puede modificar un flujo ya creado: `spikes/flujos-correo/RESULTADO.md` §6, 3; por eso se usa `como_servicio.py`).

## 7. Reversa

Está apagado: no hay nada que revertir. Borrarlo no afecta al modo `sss`.

## 8. Fuera de alcance

- **Encenderlo.** Solo en modo `flujos` (F3/F4 de `diseno/08`): antes se apaga la salida SSS del buzón del área y se carga `sanic_carp_ev_modocorreo` = `flujos`. Cambiar la entrega de un mailbox con SSS suspende la salida hasta un nuevo *Test & Enable* (`RESULTADO.md`). Lo enciende una persona dueña de las conexiones (un service principal no puede: `ConnectionAuthorizationFailed`).
- Permisos del rol `SR - CARP - Servicio` (Write del Email, etc.): cambio de rol de F3, decisión del usuario.
- Full Access y Send As sobre el buzón del área: lo da el administrador de Exchange de BAC.
- Crear las conexiones (OAuth): paso manual (`pruebas-humo/lote-d/RESULTADO.md`).

## 9. Riesgos conocidos

- **Rama sin original sin validar** (§2.3).
- El `Foreach` de adjuntos y los `If` no admiten secure data: el historial puede mostrar la lista de adjuntos que itera el `Foreach`. Revisar al activarlo en F3.
- El secure data del disparador de Dataverse (webhook) no se probó en ejecución; se verifica en la primera activación (F3).
- Nombre o tipo de archivo con comillas: el JSON de cada adjunto se arma por concatenación, igual que en el spike (r2). Un nombre con `"` rompe ese adjunto y el correo queda en Failed. Los adjuntos de la prueba no las tenían; revisar en F4.
- Límite de adjuntos: `MaxUploadFileSize` (5 MB por defecto), igual que SSS. Graph acepta hasta ~3 MB por `fileAttachment` simple; un adjunto mayor falla y el correo queda en Failed (los adjuntos de 3 a 5 MB necesitarían una sesión de carga; no está en el alcance de F2).
