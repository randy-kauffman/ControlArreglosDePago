# Playbook (spike F0): variable de entorno · EV - CARP - SPK Buzon de prueba 4

> Spike de `diseno/08` (correo por flujos). Vive en la solución `sanic_carp_sol_spikecorreo`, fuera de la principal. El valor NO está en este archivo: lo carga `v50_solucion_y_variables.py` desde memoria.

## 1. Identidad

```json
{
  "tipo_playbook": "variable-entorno",
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
  "tipo": "variable-entorno",
  "nombre": "sanic_carp_ev_spkbuzon",
  "displayname": "EV - CARP - SPK Buzon de prueba 4",
  "descripcion": "Direccion del buzon de prueba 4 (PP_MAIL_BUZON_4) que leen y usan los flujos del spike F0. El valor solo vive en este entorno.",
  "tipovalor": "Texto",
  "obligatoria": false,
  "valor": null
}
```
