# Playbook de ensayo: tabla · sanic_carp_tbl_zzrel

Tabla descartable para ensayar la relación autorreferida (variante A de `relacion.md`). Se borra al terminar.

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
  "nombre": "sanic_carp_tbl_zzrel",
  "displayname": {
    "1033": "Ensayo relacion",
    "3082": "Ensayo relacion"
  },
  "displayname_plural": {
    "1033": "Ensayos relacion",
    "3082": "Ensayos relacion"
  },
  "descripcion": "Tabla descartable para ensayar la relación autorreferida. Se borra al terminar.",
  "propiedad": "usuario",
  "notas": false,
  "actividades": false,
  "auditoria": false,
  "motivos_estado": null,
  "primaria": {
    "nombre": "sanic_nombre",
    "displayname": {
      "1033": "Nombre",
      "3082": "Nombre"
    },
    "descripcion": "Nombre con el que se muestra el cliente en listas y lookups. Lo calcula el sistema a partir del nombre completo; nadie lo digita.",
    "largo": 200,
    "requerida": true,
    "autonumerico": ""
  },
  "columnas": [
    {
      "nombre": "sanic_nombrecompleto",
      "displayname": {
        "1033": "Nombre completo",
        "3082": "Nombre completo"
      },
      "descripcion": "Nombre completo del cliente tal como lo digita el oficial.",
      "tipo": "texto",
      "largo": 200,
      "requerida": true,
      "protegida": false,
      "auditoria": false
    }
  ]
}
```
