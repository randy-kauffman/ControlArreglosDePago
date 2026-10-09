# Validación: Control de Arreglos de Pago

<!-- Plantilla del documento de validación (etapa 1). La completa el orquestador (skill `validacion`); los agentes evaluadores no escriben archivos, solo devuelven contenido en el formato de retorno definido en el archivo del agente. -->

## 1. Metadatos

- id: 2026-003-control-arreglos-pago
- fecha: 2026-10-07
- tipo: interno
- panorama: bac-credomatic/arreglos-de-pago
- estado: dictaminada
- ficha_actualizada: 2026-10-07

<!-- ficha_actualizada = el valor de `actualizado` leído de ficha-solicitud.md en el momento de evaluar. Si la ficha se vuelve a guardar después con una fecha distinta, este dictamen queda desactualizado y hay que reevaluar desde 1A. -->

## 2. Resumen del pedido

Deyanira Ramírez, Vice Gerente de Cobranza, pide controlar el proceso del departamento nuevo de Arreglos de Pago (3 oficiales, unos 80 correos de solicitud por semana, de los que unos 60 terminan en arreglo). El disparador son las quejas de clientes por correos de solicitud que nunca se responden. Además, el historial de arreglos de cada cliente está disperso en Excel dentro de SharePoint, con datos clasificados como "Sensible". El alcance cubre 5 piezas, todas imprescindibles: recepción de correos, formalización con impresión del formato controlado F-NIC-0000052, seguimiento de cuotas, anulación e historial por cliente. Aplica a préstamos y tarjetas, con fecha límite 2026-10-31.

## 3. Evaluación por dimensión

<!-- Escala de Resultado: ✅ favorable | ⚠️ con reservas | ❌ desfavorable | ❓ sin información suficiente. -->
<!-- "—" (guion largo) SOLO es válido en la fila "Factibilidad técnica" y significa "todavía no evaluada" (etapa 1B pendiente). En cualquier otra fila es un valor inválido. -->
<!-- Fundamento obligatorio salvo cuando Resultado es "—". Cada fundamento cita su origen: [fuente: ficha §N], [fuente: panorama §N] o [fuente: evaluador]. Un dato sin cita se considera ❓. -->

| Dimensión | Resultado | Fundamento |
|---|---|---|
| Claridad del problema | ✅ | El problema está bien delimitado: no hay seguimiento de las solicitudes por correo (si se respondió, qué se respondió, cuánto se tardó) ni una vista consolidada de los arreglos por cliente [fuente: ficha §4]. Hay 6 criterios de éxito verificables [fuente: ficha §6] y el alcance tiene 5 piezas con exclusiones explícitas [fuente: ficha §7]. La línea base de correos sin respuesta no se mide, pero el criterio es 100 % y eso no impide entender qué se pide [fuente: ficha §6]. |
| Valor | ✅ | Se alinea con la meta corporativa de automatizar y ser más eficientes [fuente: evaluador] [fuente: panorama §2]. El disparador son quejas de clientes [fuente: ficha §3] y el proceso actual es manual: Excel disperso, asignación informal y supervisión correo por correo [fuente: ficha §5]. Con unos 80 correos y 60 arreglos por semana, el volumen justifica automatizar [fuente: ficha §5]. El ahorro no está cuantificado; el valor descansa en la alineación estratégica y el control [fuente: ficha §6]. |
| Factibilidad técnica | ⚠️ | Conclusión de factibilidad-tecnica.md §10: sin incógnitas bloqueantes; las 4 candidatas (model-driven, canvas, code app y model-driven con buzón sincronizado) quedan con reservas, todas talla M, sin costo de licencias incremental. Reservas: fidelidad del relleno de la plantilla controlada F-NIC-0000052 (spike), salida PDF sin mecanismo nativo sin OneDrive ni SharePoint, versión de plantilla para 60 cuotas y un plazo de unos 17 días hábiles, en el que ninguna candidata entra completa [fuente: factibilidad-tecnica §3, §7, §8, §10]. |
| Alternativas | ✅ | El área consultó resolverlo con flujos en el CRM corporativo y lo descartó: los flujos son lentos y poco personalizables, y el CRM está en migración hasta 2027 [fuente: ficha §9] [fuente: evaluador]. Power Platform con Dataverse es la plataforma obligatoria para automatizaciones nuevas [fuente: panorama §6], todos los usuarios ya tienen Power Apps Premium [fuente: panorama §3] y no hay presupuesto adicional por defecto, lo que hace poco realista comprar [fuente: panorama §6]. Ningún sistema del panorama cubre el proceso [fuente: panorama §4]. |
| Riesgo | ⚠️ | (1) Datos "Sensible" de clientes [fuente: ficha §8] [fuente: panorama §7], hoy en Excel dentro de SharePoint, algo que el panorama prohíbe [fuente: ficha §5]; la regulación externa (SIBOIF, sigilo bancario) sigue sin respuesta [fuente: ficha §10]. (2) Plazo 2026-10-31 con 5 piezas imprescindibles y sin MVP, unas 3 semanas desde hoy [fuente: ficha §7, §8] [fuente: evaluador]. (3) Dependencia de Héctor Romero, dueño del formato controlado F-NIC-0000052, y adopción de un equipo que hoy asigna de forma informal [fuente: ficha §2, §5, §8]. (4) Entornos Dev/Test/Prod compartidos con otras soluciones [fuente: panorama §6]. Son mitigables con condiciones. |

## 4. Riesgos clave

| Riesgo | Probabilidad | Impacto |
|---|---|---|
| Normativa externa (SIBOIF, sigilo bancario: auditoría de accesos, retención, ubicación de datos) que obligue a rehacer el diseño de datos o accesos | media | alto |
| Los Excel existentes con datos "Sensible" siguen en SharePoint, contra la política, porque no se migran | alta | medio |
| Plazo 2026-10-31 con 5 piezas imprescindibles y sin MVP: entrega incompleta o con calidad insuficiente en una solución con datos sensibles | alta | alto |
| Cambio en el formato controlado F-NIC-0000052 o desacuerdo de su dueño sobre la impresión parametrizable | media | medio |
| Adopción baja: equipo chico, asignación hoy informal y pagos cargados a mano, de modo que la lista de arreglos en riesgo depende de que el oficial registre los pagos | media | medio |
| Entornos compartidos con otras soluciones: coordinación de despliegue y recursos únicos por entorno | media | medio |
| Sin línea base de correos sin respuesta, cuesta demostrar la mejora | media | bajo |

## 5. Recomendación

resultado: go-con-condiciones

Justificación: con la regla de `criterios-validacion`, ninguna dimensión es ❌ ni ❓. Claridad, Valor y Alternativas son ✅. Factibilidad técnica es ⚠️: es viable sin licencias adicionales, pero la impresión del formato controlado necesita un spike y ninguna candidata entra completa al 2026-10-31. Riesgo es ⚠️: datos "Sensible", regulación externa sin resolver, plazo sin MVP y dependencia del dueño del formato. Con dos ⚠️ y Factibilidad ya evaluada, el resultado es go-con-condiciones.

## 6. Condiciones

- Factibilidad técnica (RF-06): antes de cerrar el diseño (etapa 2), un spike con la plantilla real F-NIC-0000052 confirma que el mecanismo elegido rellena el Excel desde la plantilla con mapeo celda → campo, conservando el formato y con hasta 60 cuotas. Héctor Romero aprueba una versión de plantilla con 60 filas y totales corregidos, y define con Deyanira Ramírez si el PDF puede salir del Excel descargado. Se cumple con el resultado del spike validado por Héctor y su decisión sobre el PDF registrada en la definición.
- Riesgo (normativa y datos sensibles): antes de cerrar el diseño de datos, Seguridad de la Información o Cumplimiento confirma por escrito si la SIBOIF o el sigilo bancario imponen requisitos de auditoría de accesos, retención o ubicación de datos, y que Dataverse en los entornos de Nicaragua es un almacén aprobado para datos "Sensible". Se cumple con la respuesta escrita registrada en la ficha o en el panorama.
- Riesgo (plazo): Deyanira Ramírez confirma por escrito, después de la estimación de la etapa 2, si la fecha 2026-10-31 queda sujeta a ese resultado o si acuerda un recorte o entrega por fases de las 5 piezas (los evaluadores técnicos coinciden en que al 2026-10-31 solo entra un primer tramo, del orden de RF-01 a RF-05). Se cumple con una nota en la ficha o en el documento de decisión.
- Riesgo (datos existentes y formato): Héctor Romero se compromete por escrito a un plan para los Excel con datos "Sensible" que quedan en SharePoint (restricción de uso o plazo de retiro). Se cumple con su confirmación escrita.

## 7. Información faltante

<!-- Sin ítems: las incógnitas técnicas bloqueantes registradas en la primera ronda de 1B quedaron resueltas el 2026-10-07 (DLP Outlook+Dataverse en el mismo grupo, capacidad React/TS confirmada, server-side sync habilitable, remitentes desconocidos verificados en Microsoft Learn y RF-06 precisado con dos mecanismos independientes). -->

## 8. Decisión

<!-- Se completa SOLO después de que el usuario responda explícitamente en la conversación principal (checkpoint humano de dictamen). Nunca completar esta sección ni fijar estado: dictaminada sin esa respuesta. La decisión puede diferir de la recomendación de la sección 5. -->
<!-- Formato:
- resultado: go|go-con-condiciones|no-go|falta-info
- rol: <quién decide>
- fecha: {{AAAA-MM-DD}}
- comentarios: <opcional>
-->

- resultado: go-con-condiciones
- rol: Desarrollador de soluciones de automatización
- fecha: 2026-10-07
- comentarios: sin comentarios
