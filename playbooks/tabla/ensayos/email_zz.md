# Playbook de ensayo: columnas-estandar · email

Columna descartable (`_zz`) sobre `email` para ensayar la variante `columnas-estandar` y la pertenencia a la solución antes de construir `playbooks/tabla/email.md`. Se borra al terminar.

## 1. Identidad

```json
{
  "tipo_playbook": "columnas-estandar",
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
  "tipo": "columnas-estandar",
  "tabla": "email",
  "columnas": [
    {
      "nombre": "sanic_zztiemporespuesta",
      "displayname": {
        "1033": "Ensayo tiempo",
        "3082": "Ensayo tiempo"
      },
      "descripcion": "Minutos entre el primer correo del cliente que esperaba respuesta y el envío de esta respuesta (senton menos sinrespuestadesde). Vacío si no había nada pendiente, por ejemplo en un recordatorio. Sostiene el promedio por respuesta y por oficial.",
      "tipo": "entero",
      "requerida": false,
      "protegida": false,
      "auditoria": false,
      "minimo": 0,
      "maximo": 2147483647
    }
  ]
}
```
