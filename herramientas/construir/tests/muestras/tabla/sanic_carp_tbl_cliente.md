# Playbook: tabla · muestra

## 1. Identidad

```json
{
  "tipo_playbook": "tabla",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "muestra",
  "fase": 1,
  "entorno_url": "https://ensayo.crm.dynamics.com/",
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
  "nombre": "sanic_carp_tbl_cliente",
  "displayname": {
    "1033": "Cliente",
    "3082": "Cliente"
  },
  "displayname_plural": {
    "1033": "Clientes",
    "3082": "Clientes"
  },
  "descripcion": "Persona que solicita o tiene un arreglo de pago.",
  "propiedad": "usuario",
  "notas": false,
  "actividades": false,
  "auditoria": true,
  "primaria": {
    "nombre": "sanic_nombre",
    "displayname": {
      "1033": "Nombre",
      "3082": "Nombre"
    },
    "descripcion": "Nombre del cliente.",
    "largo": 200,
    "requerida": true,
    "autonumerico": ""
  },
  "columnas": [
    {
      "nombre": "sanic_cedula",
      "displayname": {
        "1033": "Cedula",
        "3082": "Cedula"
      },
      "descripcion": "Número de cédula del cliente.",
      "tipo": "texto",
      "largo": 9,
      "requerida": true,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_celular",
      "displayname": {
        "1033": "Celular",
        "3082": "Celular"
      },
      "descripcion": "Número de celular del cliente.",
      "tipo": "texto",
      "largo": 12,
      "requerida": true,
      "protegida": false,
      "auditoria": true
    }
  ]
}
```
