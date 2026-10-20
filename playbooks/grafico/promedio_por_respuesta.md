# Playbook: gráfico · CHT - CARP - Promedio por respuesta

## 1. Identidad

```json
{
  "tipo_playbook": "grafico",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.15",
  "fase": 1,
  "entorno_url": "https://org36e60d9d.crm.dynamics.com/",
  "solucion": "sanic_carp_sol_controlarreglospago",
  "publisher": "Sistemas_Abiertos_Nicaragua",
  "prefijo": "sanic",
  "abrev": "carp",
  "idiomas": [1033, 3082]
}
```

De dónde sale cada dato: `diseno/07-diseno-app-y-flujos.md` §6 (fila 12.15) y `diseno/06-inventario-componentes.md` fila 12.15. Columnas comprobadas contra Dev (`EntityDefinitions`) el 2026-10-20: `sanic_respondidoporid` es Lookup y `sanic_tiemporespuesta` es Integer (minutos).

Sin herramienta en `herramientas/construir/` (DA-05, cambiada el 2026-10-20): lo construye y verifica el script versionado `pruebas-humo/lote-e/scripts/e01_graficos_panel.py`, que lee el bloque de la sección 2. Es un gráfico de sistema (`savedqueryvisualization`, componente de solución tipo 59), de tipo ASP.NET (`charttype` 0).

## 2. Qué se crea

```json
{
  "tipo": "grafico",
  "nombre": "CHT - CARP - Promedio por respuesta",
  "tabla": "email",
  "descripcion": "Promedio de minutos por respuesta, por oficial. Solo correos con tiempo de respuesta calculado.",
  "categoria": "sanic_respondidoporid",
  "medida": "sanic_tiemporespuesta",
  "agregado": "avg",
  "filtros": [
    {"columna": "sanic_tiemporespuesta", "operador": "not-null"}
  ],
  "serie": "Bar",
  "vista_del_panel": "Respuestas enviadas"
}
```

## 3. Notas

- El título que pide el diseño ("Promedio de minutos por respuesta, por oficial") va en la descripción: la grilla y el panel muestran el `name` del gráfico como encabezado, y el `<Title>` de la presentación queda vacío, como en los gráficos que arma el diseñador.
- El filtro va dentro del `fetch` del gráfico, además del filtro de la vista con que se muestra en el panel.
- Los datos los llena S-35 cuando sale una respuesta real por el buzón. Sin correos reales el gráfico se ve vacío.
