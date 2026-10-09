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
  "solucion": "sanic_carp_sol_zzensayo",
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
  "descripcion": "Tabla descartable para comprobar contra la plataforma cada característica del formato. Se borra al terminar.",
  "propiedad": "usuario",
  "notas": false,
  "actividades": false,
  "auditoria": true,
  "primaria": {
    "nombre": "sanic_nombre",
    "displayname": {
      "1033": "Numero",
      "3082": "Numero"
    },
    "descripcion": "Primaria autonumérica de ensayo.",
    "largo": 100,
    "requerida": true,
    "autonumerico": "ZZ-{SEQNUM:8}"
  },
  "columnas": [
    {
      "nombre": "sanic_numeroproducto",
      "displayname": {
        "1033": "Numero de prestamo o tarjeta",
        "3082": "Numero de prestamo o tarjeta"
      },
      "descripcion": "Columna de ensayo: Numero de prestamo o tarjeta.",
      "tipo": "texto",
      "requerida": true,
      "protegida": false,
      "auditoria": true,
      "largo": 9
    },
    {
      "nombre": "sanic_comentarios",
      "displayname": {
        "1033": "Comentarios",
        "3082": "Comentarios"
      },
      "descripcion": "Columna de ensayo: Comentarios.",
      "tipo": "memo",
      "requerida": false,
      "protegida": false,
      "auditoria": true,
      "largo": 2000
    },
    {
      "nombre": "sanic_plazo",
      "displayname": {
        "1033": "Plazo",
        "3082": "Plazo"
      },
      "descripcion": "Columna de ensayo: Plazo.",
      "tipo": "entero",
      "requerida": false,
      "protegida": false,
      "auditoria": true,
      "minimo": 0,
      "maximo": 9999
    },
    {
      "nombre": "sanic_moneda",
      "displayname": {
        "1033": "Moneda",
        "3082": "Moneda"
      },
      "descripcion": "Columna de ensayo: Moneda.",
      "tipo": "choice",
      "requerida": false,
      "protegida": false,
      "auditoria": true,
      "choice": "sanic_carp_ch_moneda"
    },
    {
      "nombre": "sanic_correonuevoenfinalizada",
      "displayname": {
        "1033": "Correo nuevo en finalizada",
        "3082": "Correo nuevo en finalizada"
      },
      "descripcion": "Columna de ensayo: Correo nuevo en finalizada.",
      "tipo": "sino",
      "requerida": false,
      "protegida": false,
      "auditoria": true,
      "etiqueta_si": {
        "1033": "Si",
        "3082": "Si"
      },
      "etiqueta_no": {
        "1033": "No",
        "3082": "No"
      },
      "defecto": false
    },
    {
      "nombre": "sanic_fechainicio",
      "displayname": {
        "1033": "Fecha de inicio",
        "3082": "Fecha de inicio"
      },
      "descripcion": "Columna de ensayo: Fecha de inicio.",
      "tipo": "fecha",
      "requerida": false,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_fechacongelamiento",
      "displayname": {
        "1033": "Fecha de congelamiento",
        "3082": "Fecha de congelamiento"
      },
      "descripcion": "Columna de ensayo: Fecha de congelamiento.",
      "tipo": "fechahora",
      "requerida": false,
      "protegida": false,
      "auditoria": true
    },
    {
      "nombre": "sanic_archivo",
      "displayname": {
        "1033": "Archivo",
        "3082": "Archivo"
      },
      "descripcion": "Columna de ensayo: Archivo.",
      "tipo": "archivo",
      "requerida": false,
      "protegida": false,
      "auditoria": false,
      "tamano_kb": 10240
    },
    {
      "nombre": "sanic_folio",
      "displayname": {
        "1033": "Folio",
        "3082": "Folio"
      },
      "descripcion": "Columna de ensayo: Folio.",
      "tipo": "autonumerico",
      "requerida": false,
      "protegida": false,
      "auditoria": true,
      "largo": 100,
      "formato": "F-{SEQNUM:6}"
    },
    {
      "nombre": "sanic_cuentadebitar",
      "displayname": {
        "1033": "Cuenta a debitar",
        "3082": "Cuenta a debitar"
      },
      "descripcion": "Columna de ensayo: Cuenta a debitar.",
      "tipo": "texto",
      "requerida": false,
      "protegida": true,
      "auditoria": true,
      "largo": 20
    }
  ]
}
```
