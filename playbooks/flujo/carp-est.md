# Playbook: flujo · Cloud Flow - CARP - EST - Avisar estado del mailbox

> **Tipo con 0 ciclos en este proyecto** (`power-platform-especificar`). El formato es el que lee `herramientas/construir/flujo.py` (heredado del proyecto 2026-001). La definición vive en `playbooks/flujo/carp-est.json`, sin GUID.

## 1. Identidad

```json
{
  "tipo_playbook": "flujo",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "11.1",
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

Diseño: `diseno/07-diseno-app-y-flujos.md` §8.3 (ID `CARP-EST`, `COD` = `EST`). Inventario 11.1.

## 2. Qué se crea

```json
{
  "tipo": "flujo",
  "nombre": "Cloud Flow - CARP - EST - Avisar estado del mailbox",
  "descripcion": "Avisa por correo a los destinatarios de alertas cuando la entrada o la salida del buzon del area pasan a falla.",
  "archivo": "playbooks/flujo/carp-est.json",
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
    "sanic_carp_ev_destinatariosalertas"
  ],
  "hijos": [],
  "activar": true
}
```

Disparador Dataverse "Cuando se agrega, modifica o elimina una fila" (`SubscribeWebhookTrigger`): `message` 3 (Modificada), `entityname` `mailbox`, `scope` 2 (Unidad de negocio, como dice el diseño: `Servicio` lee `mailbox` en L). Condición: la dirección del buzón igual a `sanic_carp_ev_buzonarea` (sin mayúsculas). Acción: `SendEmailV2` con la dirección del buzón y cuál de las dos direcciones falló.

**Desviación de Dev (2026-10-20).** En Dev el dueño es la cuenta de servicio de Dev, que está en la BU raíz (no en la del área: ver `pruebas-humo/lote-d/RESULTADO.md`). Con ámbito Unidad de negocio el disparador ve los buzones de la BU raíz, **no** el del área, cuyo dueño es el team del sistema de la BU hija: en Dev el flujo queda encendido pero no dispara por el buzón del área. En Test y Prod, con la cuenta dedicada dentro de la BU, sí. No se cambió el ámbito para Dev porque `Padre: BU secundaria` exigiría a `Servicio` lectura Profunda sobre `mailbox` en Test y Prod.

Reglas comunes (`diseno/07` §8.1): secure inputs y outputs en toda acción sobre tablas de ARP; los avisos llevan solo conteos y la dirección del buzón (nada de asunto, remitente, cliente ni cédula); destinatarios por `sanic_carp_ev_destinatariosalertas` (en Dev, SOLO el buzón de prueba 3).

## 3. Precondiciones

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, publisher e idiomas | los de la sección 1 |
| 2 | Las connection references existen | `sanic_carp_conr_dataverse` y `sanic_carp_conr_outlook` (10.5, 10.6). Si una no tiene conexión, el flujo se crea APAGADO y la herramienta devuelve `bloqueado` |
| 3 | Las variables de entorno existen y tienen valor | `sanic_carp_ev_buzonarea`, `sanic_carp_ev_destinatariosalertas` |

## 4. Cómo se construye

Como la cuenta de servicio (queda a su nombre, 13.6; la plataforma exige que quien enciende el flujo tenga permiso sobre sus conexiones):

```
python3 pruebas-humo/lote-d/scripts/como_servicio.py flujo playbooks/flujo/carp-est.md
```

En Test y Prod el flujo llega con la solución managed; el dueño y las conexiones son pasos del procedimiento 13.13 y 13.15.

## 5. Verificación

```
python3 pruebas-humo/lote-d/scripts/como_servicio.py flujo playbooks/flujo/carp-est.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET workflows` por nombre, `category` 5 | una fila, definición igual al archivo, `statecode` 1 (o 0 con `bloqueado` si falta una conexión) |
| 2 | Dueño | la cuenta de servicio |
| 3 | Pertenencia a la solución | el flujo figura en `solutioncomponents` (componenttype 29) |
| 4 | Disparador | `mailbox`, Modificada, ámbito Unidad de negocio, columnas `incomingemailstatus,outgoingemailstatus`, filtro `... eq 2` |

## 6. Si ya existe

`flujo.py`: si la definición del entorno no es la del archivo, `difiere` sin tocar nada; con `--completar` lo apaga, corrige y vuelve a encender.

## 7. Reversa

Apagar el flujo (`statecode` 0). Borrarlo saca la red de seguridad de la entrada de correo (D-03) y el aviso de falla del buzón.

## 8. Fuera de alcance

- Crear las conexiones (OAuth) y asociarlas a las referencias: paso manual (`pruebas-humo/lote-d/RESULTADO.md`).
