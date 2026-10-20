# Playbook: gráfico · CHT - CARP - Tiempo total

## 1. Identidad

```json
{
  "tipo_playbook": "grafico",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.16",
  "fase": 1,
  "entorno_url": "https://org36e60d9d.crm.dynamics.com/",
  "solucion": "sanic_carp_sol_controlarreglospago",
  "publisher": "Sistemas_Abiertos_Nicaragua",
  "prefijo": "sanic",
  "abrev": "carp",
  "idiomas": [1033, 3082]
}
```

De dónde sale cada dato: `diseno/07-diseno-app-y-flujos.md` §6 (fila 12.16). Columnas comprobadas contra Dev el 2026-10-20: `sanic_oficialasignadoid` Lookup, `sanic_tiempototal` Integer (minutos), `sanic_resultado` Picklist con 159460003 = "Sin respuesta".

Lo construye y verifica `pruebas-humo/lote-e/scripts/e01_graficos_panel.py` (DA-05, cambiada el 2026-10-20).

## 2. Qué se crea

```json
{
  "tipo": "grafico",
  "nombre": "CHT - CARP - Tiempo total",
  "tabla": "sanic_carp_tbl_conversacion",
  "descripcion": "Promedio de minutos desde el primer correo hasta el cierre, por oficial asignado. Sin las conversaciones finalizadas sin respuesta.",
  "categoria": "sanic_oficialasignadoid",
  "medida": "sanic_tiempototal",
  "agregado": "avg",
  "filtros": [
    {"columna": "statecode", "operador": "eq", "valor": "1"},
    {"columna": "statuscode", "operador": "eq", "valor": "2"},
    {"columna": "sanic_resultado", "operador": "ne", "valor": "159460003"},
    {"columna": "sanic_tiempototal", "operador": "not-null"}
  ],
  "serie": "Bar",
  "vista_del_panel": "Conversaciones finalizadas"
}
```

## 3. Notas

- Excluir "Sin respuesta" sigue a confirmar con negocio (D-27). Si negocio lo quiere incluir, se quita la tercera condición y se vuelve a correr el script.
- `ne` en FetchXML también deja afuera los nulos de `sanic_resultado`. Una conversación finalizada siempre tiene resultado, así que no cambia el promedio.
