# Playbook (spike F0): flujo · Cloud Flow - CARP - SPK - V2 Encabezados y respuesta

> Spike de `diseno/08` (correo por flujos). Temporal, en la solución `sanic_carp_sol_spikecorreo`, fuera de la principal. Se crea APAGADO (`activar: false`); lo activa una persona (admin@) porque un service principal no puede (ConnectionAuthorizationFailed). La definición se genera con `generar_flujos.py`.

## 1. Identidad

```json
{
  "tipo_playbook": "flujo",
  "version_skill": "0.3.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "SPK-F0",
  "fase": 0,
  "entorno_url": "https://org36e60d9d.crm.dynamics.com/",
  "solucion": "sanic_carp_sol_spikecorreo",
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

## 2. Qué se crea

```json
{
  "tipo": "flujo",
  "nombre": "Cloud Flow - CARP - SPK - V2 Encabezados y respuesta",
  "descripcion": "Spike F0 V-2: lee los encabezados del correo nuevo del buzon de prueba 4 por Graph, responde con createReply y send y lee el internetMessageId. Deja una fila SPK-DIAG.",
  "archivo": "spikes/flujos-correo/flujos/carp-spk-v2.json",
  "conexiones": {
    "outlook": {
      "referencia": "sanic_carp_conr_outlook",
      "api": "shared_office365"
    },
    "dataverse": {
      "referencia": "sanic_carp_conr_dataverse",
      "api": "shared_commondataserviceforapps"
    }
  },
  "variables": [
    "sanic_carp_ev_spkbuzon"
  ],
  "hijos": [],
  "activar": false
}
```
