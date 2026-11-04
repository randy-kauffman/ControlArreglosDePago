# Playbook de ensayo: tabla · sanic_carp_tbl_zzarchivo

Tabla descartable para ensayar una columna de archivo con seguridad por columna y auditoría (`sanic_documentofirmado` del Arreglo, 2026-11-04). Se borra al terminar.

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
  "nombre": "sanic_carp_tbl_zzarchivo",
  "displayname": {
    "1033": "Ensayo archivo",
    "3082": "Ensayo archivo"
  },
  "displayname_plural": {
    "1033": "Ensayos archivo",
    "3082": "Ensayos archivo"
  },
  "descripcion": "Tabla descartable para ensayar archivo protegido y auditado. Se borra al terminar.",
  "propiedad": "usuario",
  "notas": false,
  "actividades": false,
  "auditoria": true,
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
    },
    {
      "nombre": "sanic_documentofirmado",
      "displayname": {
        "1033": "Documento firmado",
        "3082": "Documento firmado"
      },
      "descripcion": "Ensayo del archivo protegido y auditado.",
      "tipo": "archivo",
      "requerida": false,
      "protegida": true,
      "auditoria": true,
      "tamano_kb": 10240
    }
  ]
}
```
