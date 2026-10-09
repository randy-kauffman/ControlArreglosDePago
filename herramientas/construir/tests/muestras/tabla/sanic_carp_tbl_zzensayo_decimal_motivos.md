# Playbook de ensayo: tabla · sanic_carp_tbl_zzensayo

Tabla descartable (infijo `zz`) para ensayar `tipo:decimal` y `motivos_estado` antes de construir Conversación, Arreglo y Cuota. Copia los motivos de estado y dos decimales de `sanic_carp_tbl_arreglo.md`, más un decimal de precisión 4, con negativos y auditoría. Se borra al terminar.

## 1. Identidad

```json
{
  "tipo_playbook": "tabla",
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
  "tipo": "tabla",
  "nombre": "sanic_carp_tbl_zzensayo",
  "displayname": {
    "1033": "Ensayo",
    "3082": "Ensayo"
  },
  "displayname_plural": {
    "1033": "Ensayos",
    "3082": "Ensayos"
  },
  "descripcion": "Tabla descartable para ensayar contra la plataforma decimal y motivos de estado. Se borra al terminar.",
  "propiedad": "usuario",
  "notas": false,
  "actividades": false,
  "auditoria": true,
  "motivos_estado": [
    {
      "statecode": 0,
      "valor": 1,
      "origen": "defecto",
      "etiqueta": {
        "1033": "Borrador",
        "3082": "Borrador"
      },
      "descripcion": ""
    },
    {
      "statecode": 0,
      "valor": 159460001,
      "origen": "agregado",
      "etiqueta": {
        "1033": "Vigente",
        "3082": "Vigente"
      },
      "descripcion": ""
    },
    {
      "statecode": 1,
      "valor": 2,
      "origen": "defecto",
      "etiqueta": {
        "1033": "Terminado",
        "3082": "Terminado"
      },
      "descripcion": ""
    },
    {
      "statecode": 1,
      "valor": 159460002,
      "origen": "agregado",
      "etiqueta": {
        "1033": "Anulado",
        "3082": "Anulado"
      },
      "descripcion": ""
    },
    {
      "statecode": 1,
      "valor": 159460003,
      "origen": "agregado",
      "etiqueta": {
        "1033": "Descartado",
        "3082": "Descartado"
      },
      "descripcion": ""
    }
  ],
  "primaria": {
    "nombre": "sanic_nombre",
    "displayname": {
      "1033": "Codigo interno",
      "3082": "Codigo interno"
    },
    "descripcion": "Código interno correlativo del arreglo, asignado por el sistema al crear, también en borrador. No es el número de arreglo del crédito.",
    "largo": 20,
    "requerida": false,
    "autonumerico": "ARR-{SEQNUM:6}"
  },
  "columnas": [
    {
      "nombre": "sanic_saldoinicial",
      "displayname": {
        "1033": "Saldo inicial",
        "3082": "Saldo inicial"
      },
      "descripcion": "Saldo inicial del crédito al momento del arreglo, en la moneda del arreglo.",
      "tipo": "decimal",
      "precision": 2,
      "minimo": 0,
      "maximo": 999999999.99,
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_montoabonado",
      "displayname": {
        "1033": "Monto abonado",
        "3082": "Monto abonado"
      },
      "descripcion": "Monto abonado a la fecha, en la moneda del arreglo.",
      "tipo": "decimal",
      "precision": 2,
      "minimo": 0,
      "maximo": 999999999.99,
      "requerida": true,
      "protegida": false,
      "auditoria": false
    },
    {
      "nombre": "sanic_zzauditado",
      "displayname": {
        "1033": "Auditado",
        "3082": "Auditado"
      },
      "descripcion": "Saldo inicial del crédito al momento del arreglo, en la moneda del arreglo.",
      "tipo": "decimal",
      "precision": 4,
      "minimo": -1000,
      "maximo": 1000,
      "requerida": false,
      "protegida": false,
      "auditoria": true
    }
  ]
}
```
