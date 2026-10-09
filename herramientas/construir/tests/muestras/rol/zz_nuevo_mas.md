# Playbook: rol · ensayo

## 1. Identidad

```json
{
  "tipo_playbook": "rol",
  "version_skill": "0.2.0",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "ensayo",
  "fase": 1,
  "entorno_url": "https://ensayo.crm.dynamics.com/",
  "solucion": "sanic_carp_sol_zzensayo",
  "publisher": "Sistemas_Abiertos_Nicaragua",
  "prefijo": "sanic",
  "abrev": "carp",
  "prefijo_opciones": 15946,
  "idiomas": [1033, 3082]
}
```

## 2. Qué se crea

```json
{
  "tipo": "rol",
  "nombre": "SR - CARP - ZZ Nuevo",
  "descripcion": "Rol descartable: varios alcances y un privilegio suelto.",
  "base": "App Opener",
  "tablas": {
    "sanic_carp_tbl_cliente": {
      "leer": "organizacion",
      "asignar": "organizacion"
    },
    "sanic_carp_tbl_cuota": {
      "leer": "organizacion",
      "escribir": "organizacion",
      "anexar": "usuario",
      "anexar_a": "unidad"
    }
  },
  "otros_privilegios": {
    "prvBulkDelete": "organizacion"
  }
}
```

## 3. Precondiciones

(no hace falta para las pruebas)
