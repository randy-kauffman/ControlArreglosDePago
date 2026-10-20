# Playbook: dashboard · DASH - CARP - Supervision

## 1. Identidad

```json
{
  "tipo_playbook": "dashboard",
  "proyecto": "2026-003-control-arreglos-pago",
  "inventario": "12.14",
  "fase": 1,
  "entorno_url": "https://org36e60d9d.crm.dynamics.com/",
  "solucion": "sanic_carp_sol_controlarreglospago",
  "publisher": "Sistemas_Abiertos_Nicaragua",
  "prefijo": "sanic",
  "abrev": "carp",
  "idiomas": [1033, 3082]
}
```

De dónde sale cada dato: `diseno/07-diseno-app-y-flujos.md` §6 (fila 12.14) y §7.2 (entrada `panel`). Es un dashboard de organización clásico (`systemform` con `type` 0, componente de solución tipo 60); los interactivos no se pueden crear por API (Learn, "Analyze data with dashboards").

Lo construye y verifica `pruebas-humo/lote-e/scripts/e01_graficos_panel.py` (DA-05, cambiada el 2026-10-20). Depende de 12.15, 12.16, 12.1 (`Correos sin respuesta`) y 12.4 (`Respuestas con error de envio`). La entrada del sitemap la agrega `app.py` con `playbooks/app/controlarreglospago.md`.

## 2. Qué se crea

```json
{
  "tipo": "dashboard",
  "nombre": "DASH - CARP - Supervision",
  "descripcion": "Panel del jefe: tiempos promedio por oficial, correos sin respuesta y respuestas con error de envío.",
  "rol_que_lo_ve": "SR - CARP - Jefe",
  "componentes": [
    {"fila": 1, "titulo": {"1033": "Average minutes per reply, by officer", "3082": "Promedio de minutos por respuesta, por oficial"},
     "modo": "Chart", "tabla": "email", "grafico": "CHT - CARP - Promedio por respuesta", "vista": "Respuestas enviadas"},
    {"fila": 1, "titulo": {"1033": "Average total time, by officer", "3082": "Tiempo total promedio, por oficial"},
     "modo": "Chart", "tabla": "sanic_carp_tbl_conversacion", "grafico": "CHT - CARP - Tiempo total", "vista": "Conversaciones finalizadas"},
    {"fila": 2, "titulo": {"1033": "Unanswered emails", "3082": "Correos sin respuesta"},
     "modo": "Grid", "tabla": "sanic_carp_tbl_conversacion", "vista": "Correos sin respuesta"},
    {"fila": 2, "titulo": {"1033": "Replies with send errors", "3082": "Respuestas con error de envio"},
     "modo": "Grid", "tabla": "email", "vista": "Respuestas con error de envio"}
  ]
}
```

## 3. Notas

- Diseño de dos filas por dos columnas: cada componente ocupa la mitad del ancho (sección de 4 columnas, `colspan` 2).
- `DisplayConditions` con el rol del Jefe y `FallbackForm="true"`. La seguridad no depende de esto: la entrada del sitemap ya pide `Create` sobre Versión de plantilla, y los datos los acota cada rol.
- Tras crear o cambiar el `formxml` el script publica el dashboard (`PublishXml` con `<dashboards>`).
