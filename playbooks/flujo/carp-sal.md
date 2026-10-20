# Playbook: flujo · Cloud Flow - CARP - SAL - Salud del buzon

> **Tipo con 0 ciclos en este proyecto** (`power-platform-especificar`). El formato es el que lee `herramientas/construir/flujo.py` (heredado del proyecto 2026-001). La definición vive en `playbooks/flujo/carp-sal.json`, sin GUID.

## 1. Identidad

```json
{
  "tipo_playbook": "flujo",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "11.2",
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

Diseño: `diseno/07-diseno-app-y-flujos.md` §8.4 (ID `CARP-SAL`). Inventario 11.2.

## 2. Qué se crea

```json
{
  "tipo": "flujo",
  "nombre": "Cloud Flow - CARP - SAL - Salud del buzon",
  "descripcion": "Cada 30 minutos en horario habil revisa el buzon del area: correo entrante reciente, correos sin conversacion (reconciliacion), respuestas con error de envio y purgas fallidas; avisa en un unico correo.",
  "archivo": "playbooks/flujo/carp-sal.json",
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
    "sanic_carp_ev_destinatariosalertas",
    "sanic_carp_ev_umbralesmonitoreo"
  ],
  "hijos": [],
  "activar": true
}
```

Disparador: recurrencia semanal de lunes a viernes, de 07:00 a 18:30 cada 30 minutos (`hours` 7 a 18, `minutes` 0 y 30), zona `Central America Standard Time` (UTC−6, sin horario de verano). Es la forma nativa de "horario hábil" que el diseño admite; no hace falta la condición inicial. **El horario sigue a confirmar con el jefe (DA-03).**

Comprobaciones (umbrales de `sanic_carp_ev_umbralesmonitoreo`):
1. **Entrada reciente**: `emails` entrantes creados en las últimas `horasSinEntrada` horas **dirigidos al buzón del área** (`contains(torecipients, buzonarea)`; el filtro por destinatario lo agrega este playbook: en Dev el dueño es administrador y vería los correos de otros proyectos).
2. **Correos sin conversación**: `sanic_carp_capi_reconciliarcorreos` con `maximo` 20 y `minutosminimos` = `minutosReconciliar`; aviso si `conerror` > 0, si `haymas` o si la llamada falla. Nunca el `detalle`.
3. **Errores de envío**: `emails` salientes con `statuscode` 8 modificados en los últimos `ventanaErroresMin` minutos **enviados desde el buzón del área** (`contains(sender, buzonarea)`).
4. **Purgas fallidas**: `bulkdeleteoperations` con `statuscode` 31 (Failed, Learn) creadas en las últimas `horasPurga` horas y **con `CARP` en el nombre** (decisión de este playbook: los trabajos de 13.12 tienen que llevar `CARP` en el nombre; en Dev el dueño vería las purgas de otros proyectos). D-09: `prvReadAsyncOperation` en Básico alcanza (`pruebas-humo/lote-d/evidencia/02_d09_purga.json`), porque los trabajos son de la cuenta de servicio.

Un único correo con todo lo que falló; nada si todo está bien.

Reglas comunes (`diseno/07` §8.1): secure inputs y outputs en toda acción sobre tablas de ARP; los avisos llevan solo conteos y la dirección del buzón (nada de asunto, remitente, cliente ni cédula); destinatarios por `sanic_carp_ev_destinatariosalertas` (en Dev, SOLO el buzón de prueba 3).

## 3. Precondiciones

| # | Qué tiene que cumplirse | Esperado |
|---|---|---|
| 1 | La solución existe, no es managed, publisher e idiomas | los de la sección 1 |
| 2 | Las connection references existen | `sanic_carp_conr_dataverse` y `sanic_carp_conr_outlook` (10.5, 10.6). Si una no tiene conexión, el flujo se crea APAGADO y la herramienta devuelve `bloqueado` |
| 3 | Las variables de entorno existen y tienen valor | `sanic_carp_ev_buzonarea`, `sanic_carp_ev_destinatariosalertas`, `sanic_carp_ev_umbralesmonitoreo` |

## 4. Cómo se construye

Como la cuenta de servicio (queda a su nombre, 13.6; la plataforma exige que quien enciende el flujo tenga permiso sobre sus conexiones):

```
python3 pruebas-humo/lote-d/scripts/como_servicio.py flujo playbooks/flujo/carp-sal.md
```

En Test y Prod el flujo llega con la solución managed; el dueño y las conexiones son pasos del procedimiento 13.13 y 13.15.

## 5. Verificación

```
python3 pruebas-humo/lote-d/scripts/como_servicio.py flujo playbooks/flujo/carp-sal.md --solo-verificar
```

| # | Comprobación | Esperado |
|---|---|---|
| 1 | `GET workflows` por nombre, `category` 5 | una fila, definición igual al archivo, `statecode` 1 (o 0 con `bloqueado` si falta una conexión) |
| 2 | Dueño | la cuenta de servicio |
| 3 | Pertenencia a la solución | el flujo figura en `solutioncomponents` (componenttype 29) |
| 4 | Primera corrida | `flowruns`/historial: las 4 comprobaciones `Succeeded`; si hubo aviso, un único correo al buzón de prueba 3 |

## 6. Si ya existe

`flujo.py`: si la definición del entorno no es la del archivo, `difiere` sin tocar nada; con `--completar` lo apaga, corrige y vuelve a encender.

## 7. Reversa

Apagar el flujo (`statecode` 0). Borrarlo saca la red de seguridad de la entrada de correo (D-03) y el aviso de falla del buzón.

## 8. Fuera de alcance

- Crear las conexiones (OAuth) y asociarlas a las referencias: paso manual (`pruebas-humo/lote-d/RESULTADO.md`).
