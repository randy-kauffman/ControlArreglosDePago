# Playbook de ensayo: relacion · sanic_carp_zzrel_zzrel

Relación autorreferida descartable (variante A), igual en forma a REL-09. Se borra al terminar.

## 1. Identidad

```json
{
  "tipo_playbook": "relacion",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "ensayo",
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

## 2. Qué se crea

```json
{
  "tipo": "relacion",
  "nombre": "sanic_carp_zzrel_zzrel",
  "tabla_padre": "sanic_carp_tbl_zzrel",
  "tabla_hija": "sanic_carp_tbl_zzrel",
  "jerarquica": false,
  "comportamiento": "quitar_vinculo",
  "lookup": {
    "nombre": "sanic_zzunidaaid",
    "displayname": {
      "1033": "Unida a",
      "3082": "Unida a"
    },
    "descripcion": "Registro al que este quedó unido.",
    "requerida": false,
    "auditoria": true
  }
}
```
