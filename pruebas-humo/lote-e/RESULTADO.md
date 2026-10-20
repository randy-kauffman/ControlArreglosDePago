# Lote E en Dev: panel del jefe (2026-10-20)

Inventario 12.14, 12.15 y 12.16, y la entrada `panel` del sitemap (12.23). DA-05 cambiada: se construyó por Web API con un script versionado e idempotente, no a mano.

## Qué quedó

| Componente | Id | Estado | Solución |
|---|---|---|---|
| `CHT - CARP - Promedio por respuesta` (Email) | `dd7dcd22-b2cc-f111-a05b-00224808ffcd` | `ya_existia` | fila propia (Email está en la solución sin subcomponentes) |
| `CHT - CARP - Tiempo total` (Conversación) | `e17dcd22-b2cc-f111-a05b-00224808ffcd` | `ya_existia` | viaja con la tabla (en la solución con todos sus subcomponentes) |
| `DASH - CARP - Supervision` | `e47dcd22-b2cc-f111-a05b-00224808ffcd` | `ya_existia`, publicado | fila propia (tipo 60) |
| Entrada `panel` del sitemap y dashboard como componente de la app | app `f13895ff-…` | `ya_existia` (20 entradas, ValidateApp sin errores) | — |

Los 42 steps del paquete siguen en `statecode` 0 después del lote.

## Cómo se verificó

- `python3 pruebas-humo/lote-e/scripts/e01_graficos_panel.py --solo-verificar`: los tres dan `ya_existia`, en la solución. El formxml publicado coincide con el playbook en vistas, gráficos, modos, tablas, rol y títulos 1033 (evidencia `evidencia/02_verificacion.json`).
- Las consultas de los gráficos corren con HTTP 200. Promedio por respuesta: 0 filas, porque S-35 llena `sanic_tiemporespuesta` y `sanic_respondidoporid` solo con respuestas reales por el buzón. **Queda creado y sin datos hasta la tanda 3 de correo.** Tiempo total: 1 fila (promedio 347 min, sin oficial asignado: las conversaciones HUMO se finalizaron sin asignar).
- Las cuatro vistas del panel responden 200 con `?savedQuery=` (Correos sin respuesta 10 filas, Conversaciones finalizadas 5, las dos de Email 0).
- La solución exportada (unmanaged) trae los dos gráficos, el dashboard con sus etiquetas 3082 y `<AppModuleComponent type="60" id="{e47dcd22-…}" />` dentro de la app (evidencia `evidencia/03_exportacion_y_app.json`).
- `app.py --solo-verificar`: `ya_existia`.

## Decisiones

1. **DA-05 cambiada**: script versionado (`scripts/e01_graficos_panel.py`) que lee `playbooks/grafico/*.md` y `playbooks/dashboard/supervision.md`, en vez de construir a mano. No se hizo una herramienta `grafico.py` en `herramientas/construir/`: tres componentes no la justifican.
2. **Filtros dentro del fetch del gráfico**, además del filtro de la vista con que se muestra en el panel. La exclusión de "Sin respuesta" en Tiempo total sigue a confirmar con negocio (D-27).
3. **El título del diseño va en la descripción** del gráfico; el encabezado que se ve es el nombre del gráfico. En el panel cada componente lleva su propio título en 1033 y 3082.
4. **Dashboard visible para el Jefe** (`DisplayConditions` con `SR - CARP - Jefe`, `FallbackForm="true"`); la entrada del sitemap ya pide `Create` sobre Versión de plantilla, como las otras del jefe.
5. **`app.py`** (TDD, 5 pruebas nuevas; suite de herramientas 1503 OK):
   - agrega el dashboard como componente de la app (`AddAppComponents` con `systemform`; a diferencia de `entity`, sí funciona, verificado en Dev) y verifica que haya quedado;
   - corrige un falso "error": tras el PATCH del sitemap, `GET sitemaps?` devuelve el XML publicado hasta que se publica la app; ahora compara después de publicar.

## Prueba manual en el navegador (usuario)

Abrí la app con un usuario Jefe (o administrador): https://org36e60d9d.crm.dynamics.com/main.aspx?appid=f13895ff-9bcc-f111-a05b-00224808ffcd

1. Supervision › Seguimiento › **Panel de supervision** abre el panel con cuatro bloques: dos gráficos arriba y dos listas abajo.
2. "Tiempo total promedio, por oficial" muestra una barra (sin oficial). "Promedio de minutos por respuesta" aparece vacío: es esperado hasta que haya respuestas reales.
3. "Correos sin respuesta" lista las conversaciones HUMO; un clic en una fila abre la conversación.
4. Con un Oficial, la entrada "Panel de supervision" no aparece.
5. En la grilla de Conversaciones finalizadas, el selector de gráficos ofrece "CHT - CARP - Tiempo total".
