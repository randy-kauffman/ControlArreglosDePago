# Lote C de la app: resultado de la construcción en Dev (2026-10-20)

## Estado de los componentes

| Componente | Estado | Nota |
|---|---|---|
| Vistas 12.3 Finalizadas sin respuesta, 12.6 Borradores sin movimiento, 12.7 Arreglos en riesgo, 12.8 Arreglos de prestamo, 12.9 Arreglos de tarjeta | `creado`, luego `ya_existia` (`--solo-verificar`) | `?savedQuery=` da 200: 0 filas salvo Arreglos de prestamo (19). 12.7 usa `link-type="any"` con `olderthan-x-days 1` o `yesterday`: el servidor lo acepta, pero no hay datos de humo con una cuota vencida sin pago, así que no se vio una fila en riesgo. |
| Vista 12.4 Respuestas con error de envio | `creado` (2.º intento), luego `ya_existia` | El filtro `regardingobjecttypecode = 'sanic_carp_tbl_conversacion'` da HTTP 400 (el atributo espera un entero, el ObjectTypeCode, distinto en cada entorno). Se usó el respaldo de `diseno/07` §3.3: enlace `inner` a Conversación por `regardingobjectid`. `?savedQuery=` 200, 0 filas. |
| Web resource `sanic_carp_wr_js_comandos` | `actualizado` con `--completar`, luego `ya_existia` | 38591 bytes; agrega `SanicCarp.Conversacion.alCargar` (aviso de otra conversación abierta, DA-06). Todo lo de los lotes A y B queda intacto. |
| Formulario Conversación | `actualizado` con `--completar`, luego `ya_existia` | Manejador `onload` → `SanicCarp.Conversacion.alCargar` (2 manejadores). |
| Formulario Arreglo | `actualizado` con `--completar`, luego `ya_existia` | Cuatro pestañas: General, Cuotas, Formatos, Cierre (10 secciones, 39 campos, 2 subgrillas). |
| Sitemap y app | `ya_existia` (`--solo-verificar`), 19 entradas | Se sumaron 6 entradas: `enriesgo`, `prestamo`, `tarjeta` (sin privilegio) y `cierres`, `errores`, `borradoressinmov` (con `Privilege Create` sobre Versión de plantilla). Falta solo el panel (lote E). `ValidateApp` sin errores, 8 tablas, 2 roles. |
| Steps de `sanic_carp_pkg_plugins` | `step.py --solo-verificar`: `ya_existia`, 37 de 37 | Por GET: los 42 steps del ensamblado tienen `statecode` 0 (habilitados); ninguno apagado. No se corrió ImportSolution en este lote (las 8 tablas ya estaban en la app). |

No se hizo "ocultar botones estándar" (DA-15, G-I3): no estaba en el alcance pedido y no tiene fila de inventario.

Hallazgos de las herramientas:

1. `app.py` sin `--solo-verificar` siempre reporta `actualizado` (escribe siempre: AddAppComponents, roles y publicación); la prueba de idempotencia es `--solo-verificar` → `ya_existia`.
2. Tras el PATCH del sitemap, la primera lectura puede devolver el XML viejo: el primer `--completar` terminó en `error` ("el sitemapxml no coincide") y la corrida siguiente, sin cambiar nada, dio `ya_existia`. Es una lectura con retraso, no un defecto del XML.

Pruebas de JavaScript (fuera del repo, con mocks de Node): `humo_js.js` ampliada con 6 escenarios del aviso (una del mismo cliente, varias por remitente con asunto largo recortado, ninguna, no Abierta o alta o sin cliente ni remitente, solo remitente, consulta que falla) y `probar_lote_b.js` (28 casos): todas en verde.

## Prueba manual en el navegador

URL de la app: https://org36e60d9d.crm.dynamics.com/main.aspx?appid=f13895ff-9bcc-f111-a05b-00224808ffcd

1. Abrir la app con el Jefe: en Supervisión deben verse Correos sin respuesta, Finalizadas sin respuesta, Respuestas con error de envio, Respuestas enviadas y Borradores sin movimiento; con el Oficial, ninguna de ellas (ni Supervisión ni Administración del formato).
2. En Clientes y arreglos › Arreglos, abrir Arreglos en riesgo, Arreglos de prestamo y Arreglos de tarjeta: cargan sin error. Prestamo debe mostrar los arreglos HUMO (19 filas al verificar).
3. Arreglos en riesgo: crear o usar un arreglo Vigente con una cuota de fecha de vencimiento anterior a hoy y sin pago; debe aparecer una sola vez aunque tenga varias cuotas vencidas. Pagar la cuota: sale de la vista. Ojo con la zona horaria (riesgo residual del spike 5).
4. Aviso de otra conversación abierta: con dos conversaciones Abiertas del mismo cliente (o del mismo remitente), abrir una: aparece arriba la nota azul "Este cliente tiene otra conversación abierta: CONV-… «asunto». Si es el mismo tema, use Unir a otra conversación." Con tres o más, lista hasta dos más (máximo 3 en la consulta). Abrir una conversación Finalizada o una sin otras abiertas: no aparece.
5. Arreglo: el formulario muestra cuatro pestañas (General, Cuotas, Formatos, Cierre). En Cuotas se ven las 60 cuotas sin paginar; en Cierre, los campos de descarte, anulación y reapertura; los botones de gesto del lote B siguen apareciendo en la barra.
6. Consola del navegador (F12): sin errores de `sanic_carp_wr_js_comandos` al abrir Conversación y Arreglo, ni siquiera en una conversación sin cliente.
7. Respuestas con error de envio: abre sin error. Para ver una fila hace falta un correo saliente de una conversación con estado Failed (no se pudo sembrar en Dev).
